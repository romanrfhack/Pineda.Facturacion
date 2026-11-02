"""Offline tests for SMTP migration guards and filesystem recovery."""
import copy
import importlib.util
import json
import os
from pathlib import Path
import smtplib
import ssl
import tempfile
import unittest
from unittest.mock import MagicMock, patch

spec = importlib.util.spec_from_file_location("smtp_com_dev", Path(__file__).with_name("pineda_configurar_correo_com_dev.py"))
tool = importlib.util.module_from_spec(spec)
spec.loader.exec_module(tool)


class ProcessConfigurationTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.publish = self.root / "publish"
        self.publish.mkdir()
        self.proc_root = self.root / "proc"
        self.proc = self.proc_root / "100"
        self.proc.mkdir(parents=True)
        (self.proc / "cwd").symlink_to(self.publish, target_is_directory=True)
        (self.proc / "cmdline").write_bytes(b"dotnet\0/var/www/facturas-dev-backend/publish/Pineda.Facturacion.Api.dll\0")
        self.env = {"HOME": str(self.root / "home"), "ASPNETCORE_ENVIRONMENT": "Sandbox",
                    "SMTPEmail__Host": "mail.privateemail.com", "SMTPEmail__Port": "587",
                    "SMTPEmail__EnableSsl": "true", "SMTPEmail__Username": "facturacion@autorefaccionespineda.site",
                    "SMTPEmail__FromAddress": "facturacion@autorefaccionespineda.site", "SMTPEmail__Password": "test-only-secret"}
        self.write_env()
        self.config = {"Email": {"SafeRecipient": "current-test@example.org", "ProductionBccRecipient": "monitor@example.org"}}
        self.write_config()
        path_factory = lambda value=".": self.proc_root if str(value) == "/proc" else Path(value)
        for patcher in [patch.object(tool, "Path", side_effect=path_factory),
                        patch.object(tool, "PUBLISH", self.publish),
                        patch.object(tool, "service_state", return_value={"MainPID": "100", "ActiveState": "active", "InvocationID": "dev-1"})]:
            patcher.start()
            self.addCleanup(patcher.stop)

    def write_env(self):
        (self.proc / "environ").write_bytes(b"\0".join(f"{key}={value}".encode() for key, value in self.env.items()) + b"\0")

    def write_config(self):
        (self.publish / "appsettings.json").write_text(json.dumps(self.config))

    def test_effective_recipient_and_secret_are_not_inferred_from_example(self):
        current = tool.snapshot()
        self.assertEqual(current["safe"], "current-test@example.org")
        public = tool.public_snapshot(current)
        self.assertTrue(public["passwordConfigured"])
        self.assertNotIn("test-only-secret", json.dumps(public))

    def test_json_comments_and_trailing_commas_preserve_strings(self):
        (self.publish / "appsettings.json").write_text('''{
          // Same syntax accepted by the .NET JSON provider.
          "Email": { "SafeRecipient": "test@example.org", /* keep comma */ },
          "Url": "https://example.org/a//b/*literal*/, }",
          "Quoted": "a\\\"/*still literal*/",
        }''')
        current = tool.snapshot()
        self.assertEqual(current["safe"], "test@example.org")
        self.assertEqual(current["config"]["url"], "https://example.org/a//b/*literal*/, }")
        self.assertEqual(current["config"]["quoted"], 'a"/*still literal*/')

    def test_environment_recipient_overrides_json_without_using_fallback_alias(self):
        self.env.update(Email__SafeRecipient="effective-test@example.org", EMAIL_SAFE_RECIPIENT="ignored@example.org")
        self.write_env()
        self.assertEqual(tool.snapshot()["safe"], "effective-test@example.org")

    def test_fallback_alias_only_used_when_bound_setting_empty(self):
        self.config["Email"]["SafeRecipient"] = ""
        self.write_config()
        self.env["EMAIL_SAFE_RECIPIENT"] = "fallback@example.org"
        self.write_env()
        self.assertEqual(tool.snapshot()["safe"], "fallback@example.org")

    def test_production_environment_is_refused(self):
        self.env["DOTNET_ENVIRONMENT"] = "Production"
        self.write_env()
        with self.assertRaises(tool.Stop):
            tool.snapshot()

    def test_commandline_override_is_refused_without_printing_secret(self):
        with (self.proc / "cmdline").open("ab") as stream:
            stream.write(b"--SmtpEmail:Password=do-not-log\0")
        with self.assertRaises(tool.Stop) as result:
            tool.snapshot()
        self.assertNotIn("do-not-log", str(result.exception))

    def test_explicit_production_snapshot_requires_production_environment(self):
        with self.assertRaises(tool.Stop):
            tool.snapshot(service="facturas-api.service", publish=self.publish, environment="Production")
        self.env["ASPNETCORE_ENVIRONMENT"] = "Production"
        self.write_env()
        current = tool.snapshot(service="facturas-api.service", publish=self.publish, environment="Production")
        self.assertEqual(current["environment"], "Production")
        self.assertEqual(current["service"], "facturas-api.service")

    def test_production_does_not_load_sandbox_usersecrets(self):
        self.env["ASPNETCORE_ENVIRONMENT"] = "Production"
        self.write_env()
        secret_file = self.root / "home/.microsoft/usersecrets" / tool.USER_SECRETS_ID / "secrets.json"
        secret_file.parent.mkdir(parents=True)
        secret_file.write_text(json.dumps({"Email:SafeRecipient": "sandbox-only@example.org"}))
        current = tool.snapshot(service="facturas-api.service", publish=self.publish, environment="Production")
        self.assertEqual(current["safe"], "current-test@example.org")

    def test_case_colliding_environment_variables_are_refused(self):
        self.env["SmtpEmail__Password"] = "different-secret"
        self.write_env()
        with self.assertRaises(tool.Stop):
            tool.snapshot()

    def test_external_json_mail_override_is_refused(self):
        external = self.root / "external.json"
        external.write_text(json.dumps({"sMTPEmail": {"Password": "do-not-log"}}))
        self.env["SecretReferences__ExternalJsonPath"] = str(external)
        self.write_env()
        with self.assertRaises(tool.Stop) as result:
            tool.snapshot()
        self.assertNotIn("do-not-log", str(result.exception))

    def test_secret_references_only_external_json_is_supported(self):
        external = self.root / "external.json"
        external.write_text(json.dumps({"SecretReferences": {"Values": {"PAC_KEY": "do-not-log"}}}))
        self.env["SecretReferences__ExternalJsonPath"] = str(external)
        self.write_env()
        self.assertEqual(tool.snapshot()["safe"], "current-test@example.org")

    def test_usersecrets_higher_priority_mail_override_is_refused(self):
        secret_file = self.root / "home/.microsoft/usersecrets" / tool.USER_SECRETS_ID / "secrets.json"
        secret_file.parent.mkdir(parents=True)
        secret_file.write_text(json.dumps({"Email:SafeRecipient": "not-the-effective-env@example.org"}))
        with self.assertRaises(tool.Stop):
            tool.snapshot()

    def test_multiple_recipients_are_refused(self):
        self.config["Email"]["SafeRecipient"] = "one@example.org;two@example.org"
        self.write_config()
        with self.assertRaises(tool.Stop):
            tool.snapshot()


class DnsAndSmtpTests(unittest.TestCase):
    def dns_values(self, name, kind):
        if kind == "MX":
            return ["10 mx1.privateemail.com.", "10 mx2.privateemail.com."]
        if name.startswith("privateemail."):
            return ["v=DKIM1; k=rsa; p=TESTONLYKEY123"]
        if name == tool.DOMAIN:
            return ["v=spf1 include:spf.privateemail.com ~all"]
        return []

    def test_dns_requires_provider_mx_single_spf_and_nonempty_dkim(self):
        with patch.object(tool, "dns_record", side_effect=self.dns_values):
            result = tool.dns_check()
        self.assertTrue(result["ready"])
        self.assertFalse(result["dmarcPresent"])
        self.assertEqual(result["dkimSelectors"], ["privateemail"])

    def test_duplicate_spf_blocks_readiness(self):
        def values(name, kind):
            result = self.dns_values(name, kind)
            return result + ["v=spf1 -all"] if name == tool.DOMAIN and kind == "TXT" else result
        with patch.object(tool, "dns_record", side_effect=values):
            self.assertFalse(tool.dns_check()["ready"])

    def test_revoked_empty_dkim_blocks_readiness(self):
        def values(name, kind):
            return ["v=DKIM1; p="] if "_domainkey" in name else self.dns_values(name, kind)
        with patch.object(tool, "dns_record", side_effect=values):
            self.assertFalse(tool.dns_check()["ready"])

    def test_dns_failure_is_explicit_not_assumed_ready(self):
        with patch.object(tool, "dns_record", side_effect=OSError("not logged")):
            result = tool.dns_check()
        self.assertFalse(result["verified"])
        self.assertFalse(result["ready"])

    def test_no_credentials_are_sent_before_starttls(self):
        with patch.object(tool.smtplib, "SMTP") as factory:
            client = factory.return_value.__enter__.return_value
            client.has_extn.return_value = False
            with self.assertRaises(tool.Stop):
                tool.smtp_probe("synthetic-password", "safe@example.org")
            client.login.assert_not_called()

    def test_test_message_has_only_the_safe_recipient_and_tls_certificate_validation(self):
        with patch.object(tool.smtplib, "SMTP") as factory:
            client = factory.return_value.__enter__.return_value
            client.send_message.return_value = {}
            result = tool.smtp_probe("synthetic-password", "safe@example.org", True)
            self.assertTrue(result["smtpTestAccepted"])
            context = client.starttls.call_args.kwargs["context"]
            self.assertEqual(context.verify_mode, ssl.CERT_REQUIRED)
            self.assertTrue(context.check_hostname)
            client.login.assert_called_once_with(tool.MAILBOX, "synthetic-password")
            message = client.send_message.call_args.args[0]
            self.assertEqual(message["From"], tool.MAILBOX)
            self.assertEqual(message["To"], "safe@example.org")
            self.assertIsNone(message["Cc"])
            self.assertIsNone(message["Bcc"])
            self.assertEqual(client.send_message.call_args.kwargs["to_addrs"], ["safe@example.org"])
            self.assertNotIn("synthetic-password", message.as_string())


class MutationTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.env_file = self.root / "smtp-com.env"
        self.drop_in = self.root / "smtp-com.conf"
        self.backups = self.root / "backups"
        self.current = {"state": {"InvocationID": "dev-1"},
                        "config": {"email:saferecipient": "safe@example.org", "email:productionbccrecipient": "bcc@example.org"},
                        "envKeys": {"smtpemail:username": "SMTPEmail__Username"}, "safe": "safe@example.org"}
        self.report = {"dns": {"ready": True}}
        self.patches = [patch.object(tool, "ENV_FILE", self.env_file), patch.object(tool, "DROP_IN", self.drop_in),
                        patch.object(tool, "BACKUPS", self.backups),
                        # Test sandbox is under /tmp. The production function refuses its world-writable parent.
                        patch.object(tool, "secure_parent"), patch.object(tool, "health"),
                        patch.object(tool.sys.stdin, "isatty", return_value=True),
                        patch.object(tool.getpass, "getpass", return_value="test-password"),
                        patch.object(tool, "smtp_probe", return_value={"authenticationVerified": True, "smtpTestAccepted": False}),
                        patch.object(tool, "command", side_effect=self.command),
                        patch.object(tool, "snapshot", side_effect=[self.current, self.after()])]
        for patcher in self.patches:
            patcher.start()
            self.addCleanup(patcher.stop)

    def command(self, *args):
        if "EnvironmentFiles" in args:
            return f"/etc/facturas-dev/facturas-dev-api.env (ignore_errors=no) {self.env_file} (ignore_errors=no)"
        return ""

    def after(self):
        result = copy.deepcopy(self.current)
        result["config"].update({f"smtpemail:{key}".lower(): value for key, value in dict(tool.SMTP_VALUES, Password="test-password").items()})
        return result

    def test_success_uses_private_separate_env_and_preserves_recipient(self):
        untouched = self.root / "existing-service.env"
        untouched.write_text("Email__SafeRecipient=safe@example.org\nDbPassword=unchanged\n")
        tool.apply(self.current, self.report, False)
        self.assertEqual(self.env_file.stat().st_mode & 0o777, 0o600)
        self.assertIn('SMTPEmail__Username="' + tool.MAILBOX + '"', self.env_file.read_text())
        self.assertNotIn("Email__SafeRecipient", self.env_file.read_text())
        self.assertIn("DbPassword=unchanged", untouched.read_text())
        self.assertTrue(self.report["configurationVerified"])
        self.assertNotIn("test-password", json.dumps(self.report))

    def test_authentication_failure_does_not_write_or_restart(self):
        tool.smtp_probe.side_effect = smtplib.SMTPAuthenticationError(535, b"Authentication failed")
        with self.assertRaises(smtplib.SMTPAuthenticationError):
            tool.apply(self.current, self.report, False)
        self.assertFalse(self.env_file.exists())
        self.assertFalse(self.drop_in.exists())
        tool.command.assert_not_called()

    def test_changed_safe_recipient_after_restart_triggers_rollback(self):
        self.env_file.write_text("previous secret\n")
        self.env_file.chmod(0o600)
        self.drop_in.write_text("previous drop-in\n")
        after = self.after()
        after["safe"] = "unexpected@example.org"
        after["config"]["email:saferecipient"] = after["safe"]
        tool.snapshot.side_effect = [self.current, after]
        with self.assertRaises(tool.Stop):
            tool.apply(self.current, self.report, False)
        self.assertEqual(self.env_file.read_text(), "previous secret\n")
        self.assertEqual(self.drop_in.read_text(), "previous drop-in\n")
        self.assertEqual(self.report["rollback"], "restored_and_healthy")

    def test_wrong_password_in_effective_process_triggers_rollback(self):
        after = self.after()
        after["config"]["smtpemail:password"] = "old-or-overridden-secret"
        tool.snapshot.side_effect = [self.current, after]
        with self.assertRaises(tool.Stop):
            tool.apply(self.current, self.report, False)
        self.assertFalse(self.env_file.exists())
        self.assertFalse(self.drop_in.exists())
        self.assertEqual(self.report["rollback"], "restored_and_healthy")

    def test_later_env_file_blocks_restart_and_restores_managed_files(self):
        tool.command.side_effect = lambda *args: f"{self.env_file} (ignore_errors=no) /etc/another.env (ignore_errors=no)"
        with self.assertRaises(tool.Stop):
            tool.apply(self.current, self.report, False)
        self.assertFalse(self.env_file.exists())
        self.assertEqual(self.report["rollback"], "restored_and_healthy")

    def test_manual_rollback_detects_concurrent_changes_before_any_write(self):
        tool.apply(self.current, self.report, False)
        drop_before = self.drop_in.read_bytes()
        self.env_file.write_text("concurrent change\n")
        with self.assertRaises(tool.Stop):
            tool.restore(Path(self.report["backup"]))
        self.assertEqual(self.env_file.read_text(), "concurrent change\n")
        self.assertEqual(self.drop_in.read_bytes(), drop_before)

    def test_dns_not_ready_never_prompts_for_password_or_writes(self):
        self.report["dns"]["ready"] = False
        with self.assertRaises(tool.Stop):
            tool.apply(self.current, self.report, False)
        tool.getpass.getpass.assert_not_called()
        self.assertFalse(self.env_file.exists())

    def test_password_control_characters_are_rejected(self):
        for password in ["", "line1\nline2", "nul\0byte", "a\r"]:
            with self.subTest(password=repr(password)), self.assertRaises(tool.Stop):
                tool.environment_text(self.current, password)


if __name__ == "__main__":
    unittest.main()
