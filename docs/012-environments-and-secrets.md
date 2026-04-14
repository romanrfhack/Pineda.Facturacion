# Environments and Secrets

## Runtime rule
- No runtime secret or operative credential must remain in versioned `appsettings*.json`.
- `launchSettings.json` applies only to local `dotnet run` / IDE profiles. It does not configure the VPS service.
- Published `appsettings*.json` remain placeholders and non-secret defaults only.
- The backend deploy workflows sync the `publish` folder with `rsync --delete`, so `publish` is not a persistent or safe place for secrets.
- Production must run with `ASPNETCORE_ENVIRONMENT=Production`.
- Sandbox is allowed only for intentional non-production deployments and now requires `RuntimeSafety__AllowSandboxEnvironment=true` from external configuration.

## Server-side configuration model
DEV backend on the VPS is expected to use persistent external configuration through systemd:
- service: `facturas-dev-api.service`
- publish directory: `/var/www/facturas-dev-backend/publish`
- systemd drop-in: `/etc/systemd/system/facturas-dev-api.service.d/override.conf`
- persistent env file: `/etc/facturas-dev/facturas-dev-api.env`

The drop-in stays minimal:

```ini
[Service]
EnvironmentFile=/etc/facturas-dev/facturas-dev-api.env
```

This keeps secrets outside the repo and outside `publish`, so future deploys can replace binaries without destroying runtime configuration.

## Required external configuration
Minimum server-side variables or secret injections:
- `LegacyRead__ConnectionString`
- `ConnectionStrings__BillingWrite`
- `Auth__Jwt__SigningKey`
- `FacturaloPlus__ApiKeyReference` when PAC flows are enabled
- `SecretReferences__Values__FACTURALOPLUS_API_KEY_REFERENCE` when PAC flows are enabled
- `SecretReferences__Values__CSD_CERTIFICATE_REFERENCE` when issuer certificates are configured
- `SecretReferences__Values__CSD_PRIVATE_KEY_REFERENCE` when issuer certificates are configured
- `SecretReferences__Values__CSD_PRIVATE_KEY_PASSWORD_REFERENCE` when issuer certificates are configured
- `SmtpEmail__Host`, `SmtpEmail__FromAddress` and related SMTP credentials when email delivery is enabled

Conditional non-production variables:
- `Auth__BootstrapAdmin__Enabled=true` requires `Auth__BootstrapAdmin__Password`
- `Bootstrap__SeedDefaultTestUsers=true` requires `Bootstrap__DefaultTestUserPassword`
- `RuntimeSafety__AllowSandboxEnvironment=true` only on intentional sandbox servers

Minimum DEV Sandbox startup set confirmed for the VPS:
- `RuntimeSafety__AllowSandboxEnvironment=true`
- `LegacyRead__ConnectionString`
- `ConnectionStrings__BillingWrite`
- `Auth__Jwt__SigningKey`
- `Auth__BootstrapAdmin__Password`
- `Bootstrap__DefaultTestUserPassword`

## Legacy MySQL hardening
- `LegacyRead__ConnectionString` must use a dedicated SELECT-only MySQL user.
- `LegacyRead__ConnectionString` must not use `root` outside local development.
- The repo no longer carries a working legacy credential by default.

## BillingWrite hardening
- `ConnectionStrings__BillingWrite` must use a dedicated read/write application user.
- Do not use MySQL `root` for `ConnectionStrings__BillingWrite` in server environments.

## Production guardrails
The app now fails startup in non-local environments when:
- `LegacyRead__ConnectionString` is missing, placeholder-based or uses `root`
- `ConnectionStrings__BillingWrite` is missing or placeholder-based
- `Auth__Jwt__SigningKey` is missing, placeholder-based or shorter than 32 chars
- bootstrap passwords are missing while their bootstrap flags are enabled

The app also fails startup in `Production` when any of these are enabled:
- `Auth__BootstrapAdmin__Enabled`
- `Bootstrap__ApplyMigrationsOnStartup`
- `Bootstrap__ApplyStandardVat16BackfillOnStartup`
- `Bootstrap__SeedDefaultTestUsers`
- `OpenApi__EnableSwagger`

## Rotation playbook still required outside the repo
These steps are operational and were not auto-executed from the repository:
1. Rotate legacy MySQL credentials.
2. Rotate BillingWrite MySQL credentials if the previous values were ever used outside local development.
3. Rotate the JWT signing key currently configured on servers.
4. Rotate SMTP credentials if they were previously shared through runtime files.
5. Rotate PAC / certificate / private-key references or values if they ever existed in deployed config.

## VPS / systemd checklist
- Production service must export `ASPNETCORE_ENVIRONMENT=Production`.
- Dev sandbox service must export `ASPNETCORE_ENVIRONMENT=Sandbox`.
- Dev sandbox service must explicitly export `RuntimeSafety__AllowSandboxEnvironment=true`.
- Production service must not export `RuntimeSafety__AllowSandboxEnvironment=true`.
- Server-side environment files must provide the required secrets listed above.
- Do not place real secrets under `/var/www/.../publish`.
- Prefer new passwords and signing secrets without `%` when storing them in a systemd `EnvironmentFile`, unless you handle systemd escaping explicitly.
- Verify `/health/live` and `/health/ready` after each deployment.

## Deploy expectation
- Deploy workflows publish binaries, copy them to the server, reload systemd, restart the service, and verify health.
- Deploy must not re-inject secrets into `publish`.
- Persistent server env files are prepared one time and then preserved across future deploys.

## DBA checklist
- Create a dedicated legacy MySQL user with `SELECT` only on the required legacy schema.
- Remove app access to legacy using `root`.
- Before applying the active-issuer migration in shared environments, review current `issuer_profile` rows with `is_active=1`.
- The migration keeps the most recently updated active issuer and deactivates any additional active issuer rows before creating the unique guard index.
