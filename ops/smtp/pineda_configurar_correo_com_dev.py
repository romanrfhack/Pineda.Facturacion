#!/usr/bin/env python3
"""Migrate only DEV SMTP to .com; Python 3.10+, systemd, root, no packages."""
from __future__ import annotations

import argparse
import concurrent.futures
import fcntl
import getpass
import hashlib
import json
import os
from pathlib import Path
import pwd
import re
import smtplib
import ssl
import subprocess
import sys
import tempfile
import time
import urllib.parse
import urllib.request
import uuid
from email.message import EmailMessage
from email.utils import formatdate, make_msgid

SERVICE = "facturas-dev-api.service"
PUBLISH = Path("/var/www/facturas-dev-backend/publish")
ENV_FILE = Path("/etc/facturas-dev/smtp-com.env")
DROP_IN = Path("/etc/systemd/system/facturas-dev-api.service.d/zzzz-pineda-smtp-com.conf")
BACKUPS = Path("/root/pineda-smtp-com-backups")
MAILBOX = "facturacion@autorefaccionespineda.com"
HOST = "mail.privateemail.com"
DOMAIN = "autorefaccionespineda.com"
USER_SECRETS_ID = "9d09450b-e8a2-4e09-9d51-1fabf63e1f7b"
SMTP_VALUES = {"Host": HOST, "Port": "587", "EnableSsl": "true",
               "Username": MAILBOX, "FromAddress": MAILBOX}


class Stop(RuntimeError):
    """Operator-safe error: never put credentials or subprocess output here."""


def command(*args: str) -> str:
    result = subprocess.run(args, capture_output=True, text=True, timeout=45)
    if result.returncode:
        raise Stop(f"Falló {args[0]} (código {result.returncode}); revisar en el VPS.")
    return result.stdout.strip()


def service_state(service: str = SERVICE) -> dict:
    raw = command("systemctl", "show", service, "--no-pager",
                  "-p", "MainPID", "-p", "ActiveState", "-p", "InvocationID")
    return dict(line.split("=", 1) for line in raw.splitlines() if "=" in line)


def flatten(value: dict, prefix: str = "") -> dict:
    result = {}
    for key, item in value.items():
        name = f"{prefix}:{key}" if prefix else key
        if isinstance(item, dict):
            result.update(flatten(item, name))
        elif item is not None:
            result[name.lower()] = str(item).lower() if isinstance(item, bool) else str(item)
    return result


def load_json(path: Path) -> dict:
    if not path.exists():
        return {}
    try:
        return flatten(json.loads(path.read_text(encoding="utf-8-sig")))
    except (ValueError, AttributeError, TypeError):
        raise Stop("No se pudo interpretar un JSON de configuración; no se modificó.") from None


def protected_setting(key: str) -> bool:
    return key.startswith(("smtpemail:", "email:")) or key in {
        "email_safe_recipient", "email_production_bcc", "secretreferences:externaljsonpath"}


def valid_address(address: str) -> str:
    address = address.strip()
    if not re.fullmatch(r"[^\s@<>,;\x00-\x1f\x7f]+@[^\s@<>,;\x00-\x1f\x7f]+\.[^\s@<>,;\x00-\x1f\x7f]+", address):
        raise Stop("El destinatario seguro no es una dirección única válida; revisar Email:SafeRecipient.")
    return address


def snapshot() -> dict:
    state = service_state()
    if state.get("ActiveState") != "active" or int(state.get("MainPID", "0")) <= 0:
        raise Stop("DEV debe estar activo para inspeccionar su configuración efectiva.")
    proc = Path("/proc") / state["MainPID"]
    env = dict(part.decode().split("=", 1) for part in (proc / "environ").read_bytes().split(b"\0") if b"=" in part)
    env_keys = {}
    for key in env:
        canonical = key.replace("__", ":").lower()
        if canonical in env_keys and protected_setting(canonical):
            raise Stop("Hay variables de correo duplicadas con distinta capitalización; resolver antes.")
        env_keys[canonical] = key
    folded_env = {key.lower(): value for key, value in env.items()}
    aspnet = folded_env.get("aspnetcore_environment", "")
    dotnet = folded_env.get("dotnet_environment", "")
    if (dotnet or aspnet).lower() != "sandbox" or (dotnet and aspnet and dotnet.lower() != aspnet.lower()):
        raise Stop("Esta herramienta sólo admite el servicio DEV en ambiente Sandbox.")
    cwd = (proc / "cwd").resolve()
    if cwd != PUBLISH:
        raise Stop("El directorio de ejecución no coincide con el publish de DEV esperado.")
    if any(key.lower() in {"aspnetcore_contentroot", "dotnet_contentroot"} for key in env):
        raise Stop("Hay un content root personalizado; revisar antes de aplicar.")
    args = [part.decode() for part in (proc / "cmdline").read_bytes().split(b"\0") if part]
    # Accept only the normal dotnet app invocation and optional URL flags.
    if not any(arg.endswith("Pineda.Facturacion.Api.dll") for arg in args):
        raise Stop("El proceso DEV no corresponde a Pineda.Facturacion.Api.dll.")
    app_index = next(i for i, arg in enumerate(args) if arg.endswith("Pineda.Facturacion.Api.dll"))
    tail = args[app_index + 1:]
    while tail:
        option = tail.pop(0)
        if option == "--urls" and tail:
            tail.pop(0)
        elif not option.startswith("--urls="):
            raise Stop("DEV usa argumentos adicionales; revisar precedencia antes de aplicar.")
    config = load_json(cwd / "appsettings.json")
    config.update(load_json(cwd / f"appsettings.{dotnet or aspnet}.json"))
    config.update({key.replace("__", ":").lower(): value for key, value in env.items()})
    # Program.cs adds UserSecrets and external JSON AFTER the environment provider.
    # Refuse higher-priority mail overrides rather than claiming an env override wins.
    process_uid = (proc / "environ").stat().st_uid
    home = env.get("HOME") or pwd.getpwuid(process_uid).pw_dir
    secret_roots = [Path(home) / ".microsoft/usersecrets" / USER_SECRETS_ID]
    if env.get("APPDATA"):
        secret_roots.append(Path(env["APPDATA"]) / "Microsoft/UserSecrets" / USER_SECRETS_ID)
    for root in secret_roots:
        user_settings = load_json(root / "secrets.json")
        if any(protected_setting(key) for key in user_settings):
            raise Stop("UserSecrets contiene ajustes de correo o precedencia; revisar antes de aplicar.")
        config.update(user_settings)
    external_path = config.get("secretreferences:externaljsonpath", "").strip()
    if external_path:
        external = Path(external_path)
        if not external.is_absolute():
            raise Stop("ExternalJsonPath relativo: revisar antes de aplicar.")
        extra = load_json(external)
        if any(protected_setting(key) for key in extra):
            raise Stop("El JSON externo contiene ajustes de correo; revisar antes de aplicar.")
        config.update(extra)
    safe = valid_address(config.get("email:saferecipient", "").strip() or config.get("email_safe_recipient", ""))
    return {"state": state, "env": env, "envKeys": env_keys, "config": config, "safe": safe}


def public_snapshot(current: dict) -> dict:
    config = current["config"]
    return {"service": SERVICE, "environment": "Sandbox", "safeRecipient": current["safe"],
            "host": config.get("smtpemail:host"), "port": config.get("smtpemail:port"),
            "enableSsl": config.get("smtpemail:enablessl"),
            "username": config.get("smtpemail:username"), "fromAddress": config.get("smtpemail:fromaddress"),
            "passwordConfigured": bool(config.get("smtpemail:password", "").strip())}


def dns_record(name: str, record_type: str) -> list[str]:
    query = urllib.parse.urlencode({"name": name, "type": record_type})
    with urllib.request.urlopen("https://dns.google/resolve?" + query, timeout=6) as response:
        answer = json.load(response)
    if answer.get("Status") not in {0, 3}:
        raise Stop("No se pudo verificar DNS público.")
    number = {"MX": 15, "TXT": 16}[record_type]
    # DNS JSON quotes individual TXT chunks. Joining quoted chunks supports long DKIM keys.
    return [re.sub(r'"\s*"', "", item["data"]).strip('"')
            for item in answer.get("Answer", []) if item.get("type") == number]


def dns_check() -> dict:
    queries = [(DOMAIN, "MX"), (DOMAIN, "TXT"),
               (f"privateemail._domainkey.{DOMAIN}", "TXT"),
               (f"default._domainkey.{DOMAIN}", "TXT"), (f"_dmarc.{DOMAIN}", "TXT")]
    try:
        with concurrent.futures.ThreadPoolExecutor(max_workers=5) as pool:
            results = list(pool.map(lambda pair: dns_record(*pair), queries))
    except Exception:
        return {"verified": False, "ready": False, "reason": "DNS no consultable; revisar desde el VPS."}
    mx, txt, new_dkim, old_dkim, dmarc = results
    spf = [value for value in txt if value.lower().startswith("v=spf1")]
    mx_ok = {value.split()[-1].rstrip(".").lower() for value in mx} == {"mx1.privateemail.com", "mx2.privateemail.com"}
    spf_ok = len(spf) == 1 and "include:spf.privateemail.com" in spf[0].lower().split()
    selectors = [name for name, values in [("privateemail", new_dkim), ("default", old_dkim)]
                 if any(re.search(r"(?:^|;)\s*p\s*=\s*[A-Za-z0-9+/]+", value) for value in values)]
    return {"verified": True, "ready": mx_ok and spf_ok and bool(selectors),
            "mxReady": mx_ok, "spfReady": spf_ok, "dkimSelectors": selectors,
            "dmarcPresent": any(value.lower().startswith("v=dmarc1") for value in dmarc), "mx": mx}


def smtp_probe(password: str, safe: str, send: bool = False) -> dict:
    # STARTTLS on 587 matches System.Net.Mail.SmtpClient in this application.
    with smtplib.SMTP(HOST, 587, timeout=20) as client:
        client.ehlo()
        if not client.has_extn("starttls"):
            raise Stop("El servidor SMTP no anunció STARTTLS; no se enviaron credenciales.")
        client.starttls(context=ssl.create_default_context())
        client.ehlo()
        client.login(MAILBOX, password)
        result = {"authenticationVerified": True, "smtpTestAccepted": False}
        if send:
            message = EmailMessage()
            message["From"] = MAILBOX
            message["To"] = valid_address(safe)
            message["Subject"] = "[SANDBOX] Pineda - prueba SMTP dominio .com"
            message["Date"] = formatdate(localtime=False)
            message["Message-ID"] = make_msgid(domain=DOMAIN)
            message.set_content("Prueba técnica de correo DEV desde el buzón .com.\n"
                                "El destinatario de pruebas permanece sin cambios.\n"
                                "No contiene facturas ni datos de clientes.\n")
            if client.send_message(message, from_addr=MAILBOX, to_addrs=[safe]):
                raise Stop("El proveedor rechazó al destinatario de prueba.")
            result.update(smtpTestAccepted=True, testRecipient=safe, messageId=message["Message-ID"])
        return result


def environment_text(current: dict, password: str) -> str:
    if not password or any(ord(char) < 32 or ord(char) == 127 for char in password):
        raise Stop("La contraseña debe ser no vacía y no contener caracteres de control.")
    values = dict(SMTP_VALUES, Password=password)
    lines = ["# Pineda DEV SMTP .com. Private: root:root, 0600. No recipient overrides."]
    for name, value in values.items():
        canonical = f"smtpemail:{name}".lower()
        key = current["envKeys"].get(canonical, f"SmtpEmail__{name}")
        escaped = value.replace("\\", "\\\\").replace('"', '\\"').replace("$", "\\$").replace("`", "\\`")
        lines.append(f'{key}="{escaped}"')
    return "\n".join(lines) + "\n"


def secure_parent(path: Path) -> None:
    for part in [path, *path.parents]:
        if part.is_symlink():
            raise Stop("Ruta con enlace simbólico: revisar manualmente.")
        if part.exists() and (part.stat().st_uid != 0 or part.stat().st_mode & 0o022):
            raise Stop("Una ruta de configuración no es exclusiva de root; revisar manualmente.")


def atomic_write(path: Path, data: bytes, mode: int = 0o600) -> None:
    secure_parent(path)
    path.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
    descriptor, temporary = tempfile.mkstemp(prefix=".pineda-", dir=path.parent)
    try:
        with os.fdopen(descriptor, "wb") as stream:
            os.fchmod(stream.fileno(), mode)
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def digest(path: Path) -> str | None:
    return hashlib.sha256(path.read_bytes()).hexdigest() if path.exists() else None


def health() -> None:
    deadline = time.monotonic() + 50
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    while time.monotonic() < deadline:
        try:
            for suffix in ["live", "ready"]:
                with opener.open(f"http://127.0.0.1:5007/health/{suffix}", timeout=3) as response:
                    if response.status != 200:
                        raise Stop("Health check no satisfactorio.")
            return
        except Exception:
            time.sleep(1)
    raise Stop("DEV no superó health/live y health/ready; se restaurará el ajuste anterior.")


def verify_values(before: dict, after: dict, password: str) -> None:
    for name, value in dict(SMTP_VALUES, Password=password).items():
        if after["config"].get(f"smtpemail:{name}".lower()) != value:
            raise Stop("El correo efectivo no coincide con la configuración nueva.")
    before_email = {key: value for key, value in before["config"].items() if key.startswith("email:") or key.startswith("email_")}
    after_email = {key: value for key, value in after["config"].items() if key.startswith("email:") or key.startswith("email_")}
    if before["safe"] != after["safe"] or before_email != after_email:
        raise Stop("Cambió la política de destinatarios; se restaurará la configuración anterior.")


def backup_files(updates: dict[Path, bytes]) -> Path:
    folder = BACKUPS / (time.strftime("%Y%m%d-%H%M%S", time.gmtime()) + "-" + uuid.uuid4().hex[:8])
    secure_parent(folder)
    folder.mkdir(mode=0o700, parents=True)
    records = []
    for index, (target, data) in enumerate(updates.items()):
        secure_parent(target)
        record = {"target": str(target), "existed": target.exists(), "beforeSha256": digest(target),
                  "afterSha256": hashlib.sha256(data).hexdigest()}
        if target.exists():
            record.update(backup=f"{index}.before", mode=target.stat().st_mode & 0o777)
            atomic_write(folder / record["backup"], target.read_bytes())
        records.append(record)
    atomic_write(folder / "journal.json", json.dumps({"service": SERVICE, "files": records}, indent=2).encode())
    return folder


def restore(folder: Path, check_hash: bool = True) -> None:
    secure_parent(folder)
    if folder.parent != BACKUPS or not folder.is_dir():
        raise Stop("El rollback debe apuntar a un respaldo generado por esta herramienta.")
    secure_parent(folder / "journal.json")
    journal = json.loads((folder / "journal.json").read_text())
    records = journal["files"]
    if journal.get("service") != SERVICE or {row["target"] for row in records} != {str(ENV_FILE), str(DROP_IN)} or len(records) != 2:
        raise Stop("El respaldo no corresponde a los dos archivos DEV esperados.")
    for row in records:
        target = Path(row["target"])
        secure_parent(target)
        allowed = {row["afterSha256"]} if check_hash else {row["beforeSha256"], row["afterSha256"]}
        if digest(target) not in allowed:
            raise Stop("Un archivo cambió después del ajuste; no se sobrescribirá con rollback.")
        if row["existed"]:
            if row.get("backup") not in {"0.before", "1.before"}:
                raise Stop("Archivo de respaldo no válido.")
            secure_parent(folder / row["backup"])
            (folder / row["backup"]).read_bytes()  # Validate every source before changing any target.
    for row in records:
        target = Path(row["target"])
        if row["existed"]:
            atomic_write(target, (folder / row["backup"]).read_bytes(), row["mode"])
        else:
            target.unlink(missing_ok=True)
    command("systemctl", "daemon-reload")
    command("systemctl", "restart", SERVICE)
    health()


def apply(current: dict, report: dict, send_test: bool) -> None:
    if not report["dns"].get("ready"):
        raise Stop("DNS de .com no verificado: revisar MX, SPF y DKIM antes de aplicar.")
    if not sys.stdin.isatty():
        raise Stop("Usa ssh -t: la contraseña sólo se captura en terminal, sin eco.")
    health()
    password = getpass.getpass(f"Contraseña de {MAILBOX} (no se mostrará): ")
    text = environment_text(current, password)
    report["smtpPreflight"] = smtp_probe(password, current["safe"])
    if snapshot()["state"] != current["state"]:
        raise Stop("DEV reinició durante la preparación; ejecutar otra vez para inspeccionar el estado actual.")
    updates = {ENV_FILE: text.encode(), DROP_IN: f"[Service]\nEnvironmentFile={ENV_FILE}\n".encode()}
    folder = backup_files(updates)
    report["backup"] = str(folder)
    try:
        for target, data in updates.items():
            atomic_write(target, data, 0o644 if target == DROP_IN else 0o600)
        command("systemctl", "daemon-reload")
        env_files = command("systemctl", "show", SERVICE, "-p", "EnvironmentFiles", "--value")
        listed = re.findall(r"(\S+)\s+\(ignore_errors=(?:yes|no)\)", env_files)
        if not listed or listed[-1] != str(ENV_FILE):
            raise Stop("El EnvironmentFile nuevo no es el último; revisar precedencia de systemd.")
        command("systemctl", "restart", SERVICE)
        health()
        after = snapshot()
        verify_values(current, after, password)
        report["smtp"] = smtp_probe(after["config"]["smtpemail:password"], after["safe"], send_test)
        report.update(status="dev_smtp_configured", applied=True, configurationVerified=True,
                      after=public_snapshot(after), healthVerified=True)
    except BaseException:
        try:
            restore(folder, check_hash=False)
            report["rollback"] = "restored_and_healthy"
        except Exception as error:
            report["rollback"] = "requires_manual_review"
            report["rollbackErrorType"] = type(error).__name__
        raise


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    modes = parser.add_mutually_exclusive_group()
    modes.add_argument("--inspect", action="store_true", help="Sólo inspeccionar (predeterminado).")
    modes.add_argument("--apply", action="store_true", help="Aplicar sólo en DEV, tras validar SMTP y DNS.")
    modes.add_argument("--verify", action="store_true", help="Verificar la configuración .com efectiva.")
    modes.add_argument("--rollback", type=Path, help="Restaurar un respaldo de esta herramienta.")
    parser.add_argument("--send-test", action="store_true", help="Enviar un mensaje técnico únicamente al destinatario seguro de DEV.")
    parser.add_argument("--output", type=Path, default=Path("/root/pineda_correo_com_dev_resultado.json"))
    args = parser.parse_args()
    if args.send_test and not (args.apply or args.verify):
        parser.error("--send-test requiere --apply o --verify")
    report = {"status": "not_applied", "applied": False, "configurationVerified": False,
              "productionChanged": False, "applicationEmailVerified": False,
              "inboxDeliveryVerified": False, "errors": []}
    exit_code = 0
    try:
        if os.geteuid() != 0:
            raise Stop("Ejecutar como root en el VPS.")
        os.umask(0o077)
        lock_path = Path("/root/pineda-smtp-com-dev.lock")
        secure_parent(lock_path)
        with lock_path.open("a") as lock:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            prod = service_state("facturas-api.service")
            if args.rollback:
                if command("systemctl", "show", SERVICE, "-p", "WorkingDirectory", "--value") != str(PUBLISH):
                    raise Stop("El servicio no declara el directorio de DEV esperado; revisar antes del rollback.")
                restore(args.rollback)
                report.update(status="dev_smtp_rolled_back", rollback="restored_and_healthy")
            else:
                before = snapshot()
                report["before"] = public_snapshot(before)
                report["dns"] = dns_check()
                if args.apply:
                    apply(before, report, args.send_test)
                elif args.verify:
                    verify_values(before, before, before["config"].get("smtpemail:password", ""))
                    if not before["config"].get("smtpemail:password", "").strip() or not report["dns"].get("ready"):
                        raise Stop("Falta contraseña efectiva o la verificación DNS no está lista.")
                    health()
                    report["smtp"] = smtp_probe(before["config"]["smtpemail:password"], before["safe"], args.send_test)
                    report.update(status="dev_smtp_verified", configurationVerified=True, healthVerified=True)
                else:
                    report["status"] = "dev_smtp_inspected"
            report["productionStateUnchanged"] = prod == service_state("facturas-api.service")
    except Exception as error:
        exit_code = 1
        report["status"] = "blocked_or_failed"
        report["errors"].append(str(error) if isinstance(error, Stop) else type(error).__name__)
        if isinstance(error, smtplib.SMTPResponseException):
            report["smtpErrorCode"] = error.smtp_code
    except KeyboardInterrupt:
        exit_code = 130
        report["status"] = "interrupted"
    try:
        if os.geteuid() == 0:
            atomic_write(args.output, (json.dumps(report, indent=2, ensure_ascii=False) + "\n").encode())
    except Exception:
        report["errors"].append("No se pudo guardar el JSON; conservar la salida de la terminal.")
        exit_code = 1
    print(json.dumps(report, indent=2, ensure_ascii=False))
    return exit_code


if __name__ == "__main__":
    sys.exit(main())
