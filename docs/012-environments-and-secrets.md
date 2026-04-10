# Environments and Secrets

## Runtime rule
- No runtime secret or operative credential must remain in versioned `appsettings*.json`.
- Production must run with `ASPNETCORE_ENVIRONMENT=Production`.
- Sandbox is allowed only for intentional non-production deployments and now requires `RuntimeSafety__AllowSandboxEnvironment=true` from external configuration.

## Required external configuration
Minimum server-side variables or secret injections:
- `LegacyRead__ConnectionString`
- `BillingWrite__ConnectionString`
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

## Legacy MySQL hardening
- `LegacyRead__ConnectionString` must use a dedicated SELECT-only MySQL user.
- `LegacyRead__ConnectionString` must not use `root` outside local development.
- The repo no longer carries a working legacy credential by default.

## Production guardrails
The app now fails startup in non-local environments when:
- `LegacyRead__ConnectionString` is missing, placeholder-based or uses `root`
- `BillingWrite__ConnectionString` is missing or placeholder-based
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
- Verify `/health/live` and `/health/ready` after each deployment.

## DBA checklist
- Create a dedicated legacy MySQL user with `SELECT` only on the required legacy schema.
- Remove app access to legacy using `root`.
- Before applying the active-issuer migration in shared environments, review current `issuer_profile` rows with `is_active=1`.
- The migration keeps the most recently updated active issuer and deactivates any additional active issuer rows before creating the unique guard index.
