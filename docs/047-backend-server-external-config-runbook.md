# Backend Server External Configuration Runbook

## Goal
Keep backend secrets outside the repo and outside the published output so future deploys can replace binaries without breaking the DEV environment.

## Confirmed DEV server layout
- systemd service: `facturas-dev-api.service`
- published backend: `/var/www/facturas-dev-backend/publish`
- runtime environment: `ASPNETCORE_ENVIRONMENT=Sandbox`
- internal smoke URL: `http://localhost:5007`
- persistent env file: `/etc/facturas-dev/facturas-dev-api.env`
- persistent secret references file: `/etc/facturas-dev/facturas-dev-secretreferences.json`
- systemd drop-in: `/etc/systemd/system/facturas-dev-api.service.d/override.conf`

## Versioned artifacts in this repo
- `ops/env/facturas-dev-api.env.example`
- `ops/systemd/facturas-dev-api.override.conf.example`
- `ops/systemd/install-service-envfile-dropin.sh`
- `ops/bootstrap/bootstrap-facturas-dev-server.sh`

## One-time bootstrap on the DEV server
Run from a checkout of this repo on the server:

```bash
sudo bash ops/bootstrap/bootstrap-facturas-dev-server.sh
sudoedit /etc/facturas-dev/facturas-dev-api.env
sudoedit /etc/facturas-dev/facturas-dev-secretreferences.json
sudo systemctl cat facturas-dev-api
sudo systemctl restart facturas-dev-api
sudo systemctl status facturas-dev-api --no-pager -l
curl -fsS http://localhost:5007/health/live
curl -fsS http://localhost:5007/health/ready
```

What the bootstrap script does:
- creates `/etc/facturas-dev`
- creates `/var/www/facturas-dev-backend/publish`
- preserves an existing `/etc/facturas-dev/facturas-dev-api.env`
- installs or updates the systemd drop-in so the service reads that env file
- reloads systemd

## Required DEV env values
The DEV Sandbox server must provide real values for:
- `RuntimeSafety__AllowSandboxEnvironment=true`
- `LegacyRead__ConnectionString`
- `ConnectionStrings__BillingWrite`
- `Auth__Jwt__SigningKey`
- `Auth__BootstrapAdmin__Password`
- `Bootstrap__DefaultTestUserPassword`
- `SmtpEmail__Host`
- `SmtpEmail__Port`
- `SmtpEmail__EnableSsl`
- `SmtpEmail__Username`
- `SmtpEmail__Password`
- `SmtpEmail__FromAddress`
- `SmtpEmail__FromDisplayName`
- `Email__SafeRecipient`
- `Email__ProductionBccRecipient`
- `SecretReferences__ExternalJsonPath` when PAC/CSD secret material is stored in a persistent JSON file

Rules:
- `LegacyRead__ConnectionString` must use a dedicated read-only MySQL user
- `ConnectionStrings__BillingWrite` must use a dedicated read/write MySQL user
- do not use MySQL `root` for either connection
- avoid `%` in new passwords stored in the systemd `EnvironmentFile` unless you handle escaping explicitly
- prefer storing multiline PAC/CSD secret material in `/etc/facturas-dev/facturas-dev-secretreferences.json` referenced by `SecretReferences__ExternalJsonPath`

## SMTP configuration architecture
Versioned `appsettings*.json` files may contain only placeholders or non-secret defaults for SMTP. Real SMTP credentials and operative values must not be stored in the repo or in published `appsettings` files.

DEV Sandbox gets the effective SMTP configuration from the systemd `EnvironmentFile`:

```text
/etc/facturas-dev/facturas-dev-api.env
```

That file lives outside `/var/www/facturas-dev-backend/publish`, so a normal deploy that replaces only the publish directory should not delete it. The systemd service must keep the drop-in or unit configuration that includes:

```ini
EnvironmentFile=/etc/facturas-dev/facturas-dev-api.env
```

.NET maps environment variables with double underscores to configuration sections. For example, `SmtpEmail__Host` overrides `SmtpEmail:Host` from `appsettings.json` or `appsettings.Sandbox.json`.

`appsettings.Sandbox.json` may intentionally carry placeholders such as:
- `SmtpEmail:Host=smtp-sandbox-placeholder.local`
- `SmtpEmail:Username=sandbox-user-placeholder`
- `SmtpEmail:Password=` or another placeholder
- `SmtpEmail:FromAddress=sandbox@example.com`

These placeholders are not production-ready values. The VPS env file is the source of truth for DEV Sandbox SMTP.

## DEV / Sandbox SMTP configuration
The confirmed DEV service configuration is:
- service: `facturas-dev-api.service`
- working directory: `/var/www/facturas-dev-backend/publish`
- runtime environment: `ASPNETCORE_ENVIRONMENT=Sandbox`
- external env file: `/etc/facturas-dev/facturas-dev-api.env`

Required SMTP and email-safety entries for DEV Sandbox:

```bash
RuntimeSafety__AllowSandboxEnvironment="true"

SmtpEmail__Host="mail.privateemail.com"
SmtpEmail__Port="587"
SmtpEmail__EnableSsl="true"
SmtpEmail__Username="facturacion@autorefaccionespineda.site"
SmtpEmail__Password="<SMTP_PASSWORD>"
SmtpEmail__FromAddress="facturacion@autorefaccionespineda.site"
SmtpEmail__FromDisplayName="Autorefacciones Pineda Sandbox"

Email__SafeRecipient="pinedaautorefacciones@gmail.com"
Email__ProductionBccRecipient="pinedaautorefacciones@gmail.com"
```

Rules for `SmtpEmail__Password`:
- obtain the real value from the server or the approved secret administrator
- do not commit it
- do not share it by chat or ticket
- do not add it to `appsettings.Sandbox.json`
- do not add it to `appsettings.json`

## Production SMTP configuration
Production runs as:
- service: `facturas-api.service`
- working directory: `/var/www/facturas-backend/publish`
- runtime environment: `ASPNETCORE_ENVIRONMENT=Production`

Production should use the same external-configuration model: real SMTP values and secrets live outside `publish` and outside the repo, preferably in a server-side systemd `EnvironmentFile`. Confirm the production env-file path on the VPS before documenting or automating it.

## Installing or updating only the drop-in
If the real env file already exists and you only need to re-point systemd without touching secrets:

```bash
sudo bash ops/systemd/install-service-envfile-dropin.sh facturas-dev-api /etc/facturas-dev/facturas-dev-api.env
sudo systemctl restart facturas-dev-api
```

## Post-deploy validation
The deploy workflow now validates:
- the persistent env file exists
- the service reads it through `EnvironmentFile=...`
- the service restarts cleanly
- `/health/live` responds
- `/health/ready` responds and includes `billing_write_db`

Exact manual validation after a deploy:

```bash
sudo systemctl status facturas-dev-api --no-pager -l
curl -fsS http://localhost:5007/health/live
curl -fsS http://localhost:5007/health/ready
```

If the service fails:

```bash
sudo journalctl -u facturas-dev-api -n 120 --no-pager
```

## SMTP post-deploy validation
Run as root or with equivalent sudo privileges on the DEV VPS:

```bash
systemctl restart facturas-dev-api.service

sleep 5

systemctl is-active facturas-dev-api.service

pid=$(systemctl show facturas-dev-api.service -p MainPID --value)

tr '\0' '\n' < /proc/$pid/environ \
| grep -E '^(ASPNETCORE_ENVIRONMENT|SmtpEmail__|Email__)' \
| sed -E 's/(SmtpEmail__Password=).*/\1********/'
```

Expected effective values:

```text
ASPNETCORE_ENVIRONMENT=Sandbox
SmtpEmail__Host=mail.privateemail.com
SmtpEmail__Port=587
SmtpEmail__EnableSsl=true
SmtpEmail__Username=facturacion@autorefaccionespineda.site
SmtpEmail__Password=********
SmtpEmail__FromAddress=facturacion@autorefaccionespineda.site
SmtpEmail__FromDisplayName=Autorefacciones Pineda Sandbox
Email__SafeRecipient=pinedaautorefacciones@gmail.com
Email__ProductionBccRecipient=pinedaautorefacciones@gmail.com
```

If the process environment cannot be read as the current user, run the `tr` pipeline from a root shell or use:

```bash
sudo sh -c "tr '\0' '\n' < /proc/$pid/environ" \
| grep -E '^(ASPNETCORE_ENVIRONMENT|SmtpEmail__|Email__)' \
| sed -E 's/(SmtpEmail__Password=).*/\1********/'
```

## SMTP connectivity test
This validates network connectivity and STARTTLS only. It does not send email and does not use the SMTP password.

```bash
timeout 15 openssl s_client -starttls smtp -connect mail.privateemail.com:587 -servername mail.privateemail.com </dev/null 2>/dev/null \
| sed -n '1,25p;/Verify return code/p'
```

Expected result:
- connection opens with `CONNECTED`
- the certificate chain is valid for `mail.privateemail.com`
- `Verify return code: 0 (ok)` appears in the output

## Functional email test in DEV / Sandbox
After the service is active and the SMTP connectivity test passes:
- send a test email from the DEV UI or the DEV API endpoint that exercises the desired flow
- use `pinedaautorefacciones@gmail.com` as the safe recipient
- confirm the email arrives
- confirm the subject/body reflect the `Sandbox` environment when the safety policy applies
- if the flow sends CFDI files, confirm XML/PDF attachments arrive
- do not use real customer recipients until the safety policy behavior has been validated

## Email delivery safety policy
The policy is implemented in:
- `src/Pineda.Facturacion.Infrastructure/Communication/EmailDeliverySafetyPolicy.cs`
- `src/Pineda.Facturacion.Infrastructure/Communication/SmtpEmailSender.cs`
- `src/Pineda.Facturacion.Infrastructure/Options/EmailDeliverySafetyOptions.cs`

Production behavior:
- sends to the original recipients
- preserves subject, body and attachments
- adds monitoring BCC from `Email:ProductionBccRecipient`
- avoids duplicating that BCC if it already exists in To, Cc or Bcc

Non-production behavior, including `Sandbox`:
- must not send to real customer recipients
- redirects delivery to `Email:SafeRecipient`
- clears Cc and Bcc
- prefixes the subject with the environment
- adds a visible notice with the original To, Cc and Bcc recipients
- preserves attachments

## Will this survive a deploy?
Yes, as long as the deploy only replaces `/var/www/facturas-dev-backend/publish`.

The DEV SMTP configuration survives normal deploys because:
- the external config lives in `/etc/facturas-dev/facturas-dev-api.env`
- the env file is outside the publish directory
- the systemd service reads it through `EnvironmentFile=/etc/facturas-dev/facturas-dev-api.env`
- normal publish replacement should not touch `/etc/facturas-dev`

Risks that can break it:
- deleting `/etc/facturas-dev/facturas-dev-api.env`
- removing `EnvironmentFile` from the service unit or drop-in
- changing configuration variable names in code
- changing the runtime environment name from `Sandbox` without documenting the new name
- migrating to Docker or another container model without injecting the same variables
- replacing `facturas-dev-api.service` with another service name
- overwriting external configuration from deploy scripts

If the unit file or drop-in is replaced, validate that `systemctl cat facturas-dev-api.service` still references `/etc/facturas-dev/facturas-dev-api.env`.

## Rollback
Before editing the DEV env file, create a protected backup:

```bash
cp -a /etc/facturas-dev/facturas-dev-api.env /etc/facturas-dev/facturas-dev-api.env.bak.$(date +%Y%m%d_%H%M%S)
```

To rollback:

```bash
cp -a /etc/facturas-dev/facturas-dev-api.env.bak.YYYYMMDD_HHMMSS /etc/facturas-dev/facturas-dev-api.env
systemctl restart facturas-dev-api.service
```

The backup contains secrets. Keep it on the server, protect file permissions, and do not copy it into the repo.

## Email and secret security rules
- do not commit `SmtpEmail__Password`
- do not paste Bearer tokens in tickets, chats or commit messages
- do not print environment variables without redacting secrets
- do not upload `appsettings.Production.json` with real secrets
- do not share complete connection strings
- if a DEV Bearer token was exposed, rotate the Sandbox JWT signing key or invalidate sessions where applicable
- logs must avoid email bodies, real customer recipients and secrets

## Production note
Production should follow the same operating model:
- persistent server-side secrets outside `publish`
- systemd `EnvironmentFile`
- deploy limited to publishing binaries and restarting the service

This repo intentionally does not force a production env-file path because the real production server layout must be confirmed before codifying it.
