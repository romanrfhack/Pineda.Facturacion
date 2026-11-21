"""Offline production migration tests. No live credentials, network or systemd."""
import copy
import json
from pathlib import Path
import smtplib
import tempfile
import unittest
from unittest.mock import patch

import pineda_configurar_correo_com_dev as shared
import pineda_configurar_correo_com_prod as prod


class ProductionMigrationTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.env = self.root / "production-smtp.env"
        self.drop = self.root / "production-smtp.conf"
        self.backups = self.root / "production-backups"
        self.current = {"service": prod.SERVICE, "environment": "Production", "state": {"InvocationID": "prod-1"},
                        "safe": prod.TEST_RECIPIENT, "envKeys": {"smtpemail:username": "SMTPEmail__Username"},
                        "config": {"smtpemail:host": shared.HOST, "smtpemail:port": "587", "smtpemail:enablessl": "true",
                                   "smtpemail:username": prod.OLD_MAILBOX, "smtpemail:fromaddress": prod.OLD_MAILBOX,
                                   "smtpemail:password": "previous-test-secret", "email:saferecipient": prod.TEST_RECIPIENT,
                                   "email:productionbccrecipient": "monitor@example.org"}}
        self.source = copy.deepcopy(self.current)
        self.source.update(service=shared.SERVICE, environment="Sandbox")
        self.source["config"].update({f"smtpemail:{key}".lower(): value
                                      for key, value in dict(shared.SMTP_VALUES, Password="new-test-secret").items()})
        self.after = copy.deepcopy(self.source)
        self.after.update(service=prod.SERVICE, environment="Production")
        self.report = {"dns": {"ready": True}, "productionChanged": False}
        patchers = [patch.object(prod, "ENV_FILE", self.env), patch.object(prod, "DROP_IN", self.drop),
                    patch.object(prod, "BACKUPS", self.backups), patch.object(shared, "secure_parent"),
                    patch.object(shared, "health"), patch.object(shared, "snapshot", return_value=self.source),
                    patch.object(prod, "snapshot", side_effect=[self.current, self.after]),
                    patch.object(shared, "smtp_probe", return_value={"authenticationVerified": True, "smtpTestAccepted": True}),
                    patch.object(shared, "command", side_effect=self.command)]
        for patcher in patchers:
            patcher.start()
            self.addCleanup(patcher.stop)

    def command(self, *args):
        if "EnvironmentFiles" in args:
            return f"/etc/existing-production.env (ignore_errors=no) {self.env} (ignore_errors=no)"
        return ""

    def test_migration_changes_only_smtp_and_restarts_only_production(self):
        prod.apply(self.current, self.report, True)
        self.assertEqual(self.env.stat().st_mode & 0o777, 0o600)
        self.assertIn('SMTPEmail__Username="' + shared.MAILBOX + '"', self.env.read_text())
        self.assertNotIn("Email__SafeRecipient", self.env.read_text())
        self.assertNotIn("Email__ProductionBccRecipient", self.env.read_text())
        shared.command.assert_any_call("systemctl", "restart", prod.SERVICE)
        for call in shared.command.call_args_list:
            self.assertNotIn(shared.SERVICE, call.args)
        self.assertTrue(self.report["recipientPolicyPreserved"])
        self.assertTrue(self.report["productionChanged"])
        self.assertNotIn("new-test-secret", json.dumps(self.report))
        self.assertNotIn("previous-test-secret", json.dumps(self.report))

    def test_test_message_is_requested_only_for_authorized_internal_recipient(self):
        prod.apply(self.current, self.report, True)
        shared.smtp_probe.assert_called_with("new-test-secret", prod.TEST_RECIPIENT, True, label="PRODUCCION")

    def test_login_failure_keeps_production_untouched(self):
        shared.smtp_probe.side_effect = smtplib.SMTPAuthenticationError(535, b"not printed")
        with self.assertRaises(smtplib.SMTPAuthenticationError):
            prod.apply(self.current, self.report, True)
        self.assertFalse(self.env.exists())
        shared.command.assert_not_called()

    def test_dns_not_ready_keeps_production_untouched(self):
        self.report["dns"]["ready"] = False
        with self.assertRaises(shared.Stop):
            prod.apply(self.current, self.report, True)
        self.assertFalse(self.env.exists())
        shared.smtp_probe.assert_not_called()

    def test_source_must_be_the_verified_com_mailbox(self):
        self.source["config"]["smtpemail:username"] = prod.OLD_MAILBOX
        with self.assertRaises(shared.Stop):
            prod.apply(self.current, self.report, True)
        self.assertFalse(self.env.exists())

    def test_source_test_recipient_must_match_authorized_internal_address(self):
        self.source["safe"] = "unexpected@example.org"
        with self.assertRaises(shared.Stop):
            prod.apply(self.current, self.report, True)
        shared.smtp_probe.assert_not_called()

    def test_arbitrary_production_sender_is_not_overwritten(self):
        self.current["config"]["smtpemail:fromaddress"] = "another-sender@example.org"
        with self.assertRaises(shared.Stop):
            prod.apply(self.current, self.report, True)
        shared.snapshot.assert_not_called()

    def test_changed_production_bcc_triggers_restore_of_previous_files(self):
        self.env.write_text("old production configuration\n")
        self.env.chmod(0o600)
        self.drop.write_text("old drop-in\n")
        self.after["config"]["email:productionbccrecipient"] = "changed@example.org"
        with self.assertRaises(shared.Stop):
            prod.apply(self.current, self.report, True)
        self.assertEqual(self.env.read_text(), "old production configuration\n")
        self.assertEqual(self.drop.read_text(), "old drop-in\n")
        self.assertEqual(self.report["rollback"], "restored_and_healthy")
        self.assertFalse(self.report["productionChanged"])

    def test_later_environment_file_triggers_restore(self):
        shared.command.side_effect = lambda *args: f"{self.env} (ignore_errors=no) /etc/later.env (ignore_errors=no)"
        with self.assertRaises(shared.Stop):
            prod.apply(self.current, self.report, True)
        self.assertFalse(self.env.exists())
        self.assertFalse(self.drop.exists())
        self.assertEqual(self.report["rollback"], "restored_and_healthy")

    def test_future_deploy_does_not_depend_on_dev_or_copy_future_dev_passwords(self):
        current = copy.deepcopy(self.after)
        current["config"]["smtpemail:password"] = "production-independent-secret"
        prod.apply(current, self.report, True)
        shared.snapshot.assert_not_called()
        shared.smtp_probe.assert_called_once_with("production-independent-secret", prod.TEST_RECIPIENT, False, label="PRODUCCION")
        shared.command.assert_not_called()
        self.assertFalse(self.env.exists())
        self.assertEqual(self.report["status"], "production_smtp_already_configured")
        self.assertFalse(self.report["productionChanged"])

    def test_changed_runtime_during_preflight_blocks_mutation(self):
        changed = copy.deepcopy(self.current)
        changed["state"]["InvocationID"] = "prod-2"
        prod.snapshot.side_effect = [changed]
        with self.assertRaises(shared.Stop):
            prod.apply(self.current, self.report, True)
        self.assertFalse(self.env.exists())

    def test_manual_rollback_refuses_later_edits_before_changing_any_file(self):
        prod.apply(self.current, self.report, True)
        drop_before = self.drop.read_bytes()
        self.env.write_text("later operator update\n")
        with self.assertRaises(shared.Stop):
            prod.rollback(Path(self.report["backup"]))
        self.assertEqual(self.env.read_text(), "later operator update\n")
        self.assertEqual(self.drop.read_bytes(), drop_before)


if __name__ == "__main__":
    unittest.main()
