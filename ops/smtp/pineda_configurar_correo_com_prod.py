#!/usr/bin/env python3
"""Apply the verified .com mailbox to Production without changing recipient routing."""
from __future__ import annotations

import argparse
import fcntl
import json
import os
from pathlib import Path
import re
import smtplib
import sys

import pineda_configurar_correo_com_dev as shared

SERVICE = "facturas-api.service"
PUBLISH = Path("/var/www/facturas-backend/publish")
HEALTH_BASE = "http://127.0.0.1:5006"
ENV_FILE = Path("/etc/pineda/facturas/smtp-com.env")
DROP_IN = Path("/etc/systemd/system/facturas-api.service.d/zzzz-pineda-smtp-com.conf")
BACKUPS = Path("/root/pineda-smtp-com-prod-backups")
TEST_RECIPIENT = "pinedaautorefacciones@gmail.com"
OLD_MAILBOX = "facturacion@autorefaccionespineda.site"


def snapshot() -> dict:
    return shared.snapshot(service=SERVICE, publish=PUBLISH, environment="Production")


def rollback(folder: Path, check_hash: bool = True) -> None:
    shared.restore(folder, check_hash, backup_root=BACKUPS, service=SERVICE,
                   targets={ENV_FILE, DROP_IN}, health_base=HEALTH_BASE)


def verify(current: dict, report: dict, send_test: bool = False) -> None:
    password = current["config"].get("smtpemail:password", "")
    if not password.strip():
        raise shared.Stop("Producción no tiene contraseña SMTP efectiva.")
    shared.verify_values(current, current, password)
    shared.health(HEALTH_BASE)
    report["smtp"] = shared.smtp_probe(password, TEST_RECIPIENT, send_test, label="PRODUCCION")
    report.update(configurationVerified=True, healthVerified=True, recipientPolicyPreserved=True)


def apply(current: dict, report: dict, send_test: bool) -> None:
    config = current["config"]
    # Once migrated, use Production's own credentials. Future deploys do not copy
    # new DEV passwords or send another test merely because DEV changed.
    if config.get("smtpemail:fromaddress", "").lower() == shared.MAILBOX:
        verify(current, report)
        report["status"] = "production_smtp_already_configured"
        return
    if config.get("smtpemail:fromaddress", "").lower() != OLD_MAILBOX:
        raise shared.Stop("El remitente de producción no es el buzón .site esperado; revisar antes de cambiarlo.")
    if config.get("smtpemail:username", "").lower() not in {OLD_MAILBOX, shared.MAILBOX}:
        raise shared.Stop("El usuario SMTP de producción no corresponde a la migración solicitada.")
    source = shared.snapshot()
    password = source["config"].get("smtpemail:password", "")
    if not password.strip():
        raise shared.Stop("DEV no tiene una contraseña .com efectiva.")
    shared.verify_values(source, source, password)
    if source["safe"].lower() != TEST_RECIPIENT:
        raise shared.Stop("El destinatario de pruebas DEV no coincide con el autorizado para esta migración.")
    if not report["dns"].get("ready"):
        raise shared.Stop("DNS .com no verificado; se conserva la configuración de producción.")
    shared.health(HEALTH_BASE)
    report["smtpPreflight"] = shared.smtp_probe(password, TEST_RECIPIENT)
    text = shared.environment_text(current, password)
    latest = snapshot()
    if latest["state"] != current["state"] or latest["config"] != current["config"]:
        raise shared.Stop("La configuración de producción cambió durante la preparación; volver a inspeccionar.")
    updates = {ENV_FILE: text.encode(), DROP_IN: f"[Service]\nEnvironmentFile={ENV_FILE}\n".encode()}
    folder = shared.backup_files(updates, backup_root=BACKUPS, service=SERVICE)
    report["backup"] = str(folder)
    try:
        for target, data in updates.items():
            shared.atomic_write(target, data, 0o644 if target == DROP_IN else 0o600)
            report["productionChanged"] = True
        shared.command("systemctl", "daemon-reload")
        env_files = shared.command("systemctl", "show", SERVICE, "-p", "EnvironmentFiles", "--value")
        listed = re.findall(r"(\S+)\s+\(ignore_errors=(?:yes|no)\)", env_files)
        if not listed or listed[-1] != str(ENV_FILE):
            raise shared.Stop("El EnvironmentFile SMTP de producción no es el último; revisar precedencia.")
        shared.command("systemctl", "restart", SERVICE)
        shared.health(HEALTH_BASE)
        after = snapshot()
        shared.verify_values(current, after, password)
        report["smtp"] = shared.smtp_probe(after["config"]["smtpemail:password"], TEST_RECIPIENT,
                                           send_test, label="PRODUCCION")
        report.update(status="production_smtp_configured", applied=True, productionChanged=True,
                      configurationVerified=True, healthVerified=True, recipientPolicyPreserved=True,
                      after=shared.public_snapshot(after))
    except BaseException:
        try:
            rollback(folder, check_hash=False)
            report["rollback"] = "restored_and_healthy"
            report["productionChanged"] = False
        except Exception as error:
            report["rollback"] = "requires_manual_review"
            report["rollbackErrorType"] = type(error).__name__
        raise


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    modes = parser.add_mutually_exclusive_group()
    modes.add_argument("--inspect", action="store_true")
    modes.add_argument("--apply", action="store_true")
    modes.add_argument("--verify", action="store_true")
    modes.add_argument("--rollback", type=Path)
    parser.add_argument("--send-test", action="store_true", help="Prueba técnica sólo al destinatario interno autorizado.")
    parser.add_argument("--output", type=Path, default=Path("/root/pineda_correo_com_prod_resultado.json"))
    args = parser.parse_args()
    if args.send_test and not (args.apply or args.verify):
        parser.error("--send-test requiere --apply o --verify")
    report = {"status": "not_applied", "applied": False, "productionChanged": False,
              "configurationVerified": False, "applicationEmailVerified": False,
              "inboxDeliveryVerified": False, "devChanged": False, "errors": []}
    result = 0
    try:
        if os.geteuid() != 0:
            raise shared.Stop("Ejecutar como root en el VPS.")
        os.umask(0o077)
        lock_path = Path("/root/pineda-smtp-com-prod.lock")
        shared.secure_parent(lock_path)
        with lock_path.open("a") as lock:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            if args.rollback:
                if shared.command("systemctl", "show", SERVICE, "-p", "WorkingDirectory", "--value") != str(PUBLISH):
                    raise shared.Stop("El servicio no declara el directorio de producción esperado.")
                rollback(args.rollback)
                report.update(status="production_smtp_rolled_back", rollback="restored_and_healthy")
            else:
                current = snapshot()
                report["before"] = shared.public_snapshot(current)
                report["dns"] = shared.dns_check()
                if args.apply:
                    apply(current, report, args.send_test)
                elif args.verify:
                    verify(current, report, args.send_test)
                    report["status"] = "production_smtp_verified"
                else:
                    report["status"] = "production_smtp_inspected"
    except Exception as error:
        result = 1
        report["status"] = "blocked_or_failed"
        report["errors"].append(str(error) if isinstance(error, shared.Stop) else type(error).__name__)
        if isinstance(error, smtplib.SMTPResponseException):
            report["smtpErrorCode"] = error.smtp_code
    except KeyboardInterrupt:
        result = 130
        report["status"] = "interrupted"
    try:
        if os.geteuid() == 0:
            shared.atomic_write(args.output, (json.dumps(report, indent=2, ensure_ascii=False) + "\n").encode())
    except Exception:
        report["errors"].append("No se pudo guardar el reporte privado; conservar la salida sanitizada.")
        result = 1
    print(json.dumps(report, indent=2, ensure_ascii=False))
    return result


if __name__ == "__main__":
    sys.exit(main())
