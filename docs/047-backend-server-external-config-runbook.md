# Backend Server External Configuration Runbook

## Goal
Keep backend secrets outside the repo and outside the published output so future deploys can replace binaries without breaking the DEV environment.

## Confirmed DEV server layout
- systemd service: `facturas-dev-api.service`
- published backend: `/var/www/facturas-dev-backend/publish`
- runtime environment: `ASPNETCORE_ENVIRONMENT=Sandbox`
- internal smoke URL: `http://localhost:5007`
- persistent env file: `/etc/facturas-dev/facturas-dev-api.env`
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

Rules:
- `LegacyRead__ConnectionString` must use a dedicated read-only MySQL user
- `ConnectionStrings__BillingWrite` must use a dedicated read/write MySQL user
- do not use MySQL `root` for either connection
- avoid `%` in new passwords stored in the systemd `EnvironmentFile` unless you handle escaping explicitly

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

## Production note
Production should follow the same operating model:
- persistent server-side secrets outside `publish`
- systemd `EnvironmentFile`
- deploy limited to publishing binaries and restarting the service

This repo intentionally does not force a production env-file path because the real production server layout must be confirmed before codifying it.
