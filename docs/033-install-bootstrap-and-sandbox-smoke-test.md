# Install, Bootstrap, and Sandbox Smoke Test

## Scope
This step improves practical local and sandbox setup without adding new fiscal business flows.

It covers:
- startup migration/bootstrap behavior for non-production environments
- baseline role and test-user seeding
- install and startup commands
- minimum manual setup required before real sandbox validation
- a manual sandbox smoke-test checklist

## Environment gating rules
Bootstrap behavior is intentionally split by environment.

Non-production identity bootstrap is allowed only in:
- `Development`
- `Local`
- `Testing`
- `Sandbox`

Production guardrails:
- default test users are never auto-created in `Production`
- automatic startup migrations are disabled unless explicitly enabled in a non-production environment
- bootstrap admin should remain disabled in production except for a controlled one-time access process

## Seeded roles and users
When enabled, the system ensures these roles exist:
- `Admin`
- `FiscalSupervisor`
- `FiscalOperator`
- `Auditor`

When `Bootstrap:SeedDefaultTestUsers=true` in a non-production environment, the system ensures these users exist:
- `admin.test` -> `Admin`
- `supervisor.test` -> `FiscalSupervisor`
- `operator.test` -> `FiscalOperator`
- `auditor.test` -> `Auditor`

## Password seeding
Seeded user passwords come from configuration only:
- `Bootstrap:DefaultTestUserPassword`

Rules:
- passwords are hashed through the normal password-hashing service
- no plain-text password is persisted
- checked-in values must stay obvious non-production placeholders only
- when non-production default-user seeding is enabled, seeded test-user passwords are refreshed to the configured value on startup for deterministic access

Bootstrap admin uses:
- `Auth:BootstrapAdmin:*`

Unlike the default seeded users, bootstrap admin is only created when explicitly enabled.

## Configuration placeholders
Key settings:
- `Bootstrap:ApplyMigrationsOnStartup`
- `Bootstrap:SeedDefaultRoles`
- `Bootstrap:SeedDefaultTestUsers`
- `Bootstrap:DefaultTestUserPassword`
- `Auth:BootstrapAdmin:Enabled`
- `Auth:BootstrapAdmin:Username`
- `Auth:BootstrapAdmin:DisplayName`
- `Auth:BootstrapAdmin:Password`
- `BillingWrite:ConnectionString`
- `LegacyRead:ConnectionString`
- `FacturaloPlus:*`
- `SecretReferences:Values:*`
- `Auth:Jwt:*`
- `OpenApi:EnableSwagger`

Checked-in placeholders now exist in:
- `appsettings.json`
- `appsettings.Development.json`
- `appsettings.Sandbox.json`

## Practical commands
Manual migration:
- `bash scripts/apply-billing-migrations.sh`

Run backend locally:
- `bash scripts/run-api-local.sh`

Run frontend locally:
- `bash scripts/run-frontend.sh`

Frontend local dev proxy:
- Angular dev server proxies `/api/*` to `https://localhost:7278`
- `frontend/src/environments/environment.ts` can keep `apiBaseUrl = "/api"` for local development
- the proxy is defined in `frontend/proxy.conf.json`

Typical local flow:
1. configure local connection strings and JWT values
2. optionally keep `ASPNETCORE_ENVIRONMENT=Local` or use `Development`
3. run migrations
4. start backend on `https://localhost:7278`
5. start frontend on `http://localhost:4200`
6. sign in with a seeded non-production user if enabled
7. optionally use `/swagger` for backend-only manual endpoint checks in `Development`, `Local`, or `Sandbox`

## Startup automation behavior
When enabled in a non-production environment:
- BillingWrite migrations can be applied automatically on API startup
- baseline roles are ensured
- seeded non-production test users are ensured

This is controlled entirely by configuration. There is no public seed endpoint.

## Minimum manual setup after install
The system does not auto-seed fiscal business data beyond identity bootstrap.

Required manual setup before meaningful sandbox testing:
1. configure a valid active issuer profile
2. configure certificate/private-key/password references for that issuer
3. configure at least one fiscal receiver
4. configure product fiscal profiles for the commercial product codes used by the test order
5. configure PAC sandbox placeholders and secret references
6. ensure a known legacy order exists in the legacy read source

This is intentional. Automatic seeding of issuer fiscal identity or fake business transactions would be too opinionated and unsafe for a production-adjacent environment.

## Sandbox smoke-test checklist
Recommended manual sandbox sequence:

1. Start backend with `ASPNETCORE_ENVIRONMENT=Sandbox`.
   Optional: verify `/swagger` loads if backend-only diagnostics are needed.
2. Verify migrations applied or apply them manually.
3. Sign in with `supervisor.test` or `admin.test`.
4. Open catalogs and verify:
   - active issuer profile exists and is valid
   - fiscal receiver exists or create one
   - product fiscal profile exists for the target product code
5. Import a known legacy order.
6. Create a billing document.
7. Prepare a fiscal document.
8. Stamp the invoice against sandbox configuration.
9. Verify:
   - invoice status becomes stamped
   - UUID is visible
   - XML evidence is readable
10. If the invoice is credit-sale `PPD`, create an AR invoice.
11. Create a payment.
12. Apply the payment.
13. Prepare a payment complement.
14. Stamp the payment complement.
15. Verify:
   - complement UUID is visible
   - complement XML evidence is readable
16. If sandbox conditions allow it, manually validate cancellation and refresh flows.

## Pass/fail criteria
Pass:
- login succeeds with seeded non-production user
- issuer/receiver/product setup is valid
- invoice stamps successfully
- invoice evidence and XML are visible
- payment is recorded and applied correctly
- payment complement stamps successfully
- complement evidence and XML are visible

Fail:
- missing issuer/receiver/product mappings
- secret-reference resolution failures
- PAC sandbox connectivity or credential failures
- provider rejection or unavailable responses
- status/evidence not matching the executed operation

## Troubleshooting
If login fails:
- verify non-production seed flags
- verify default test password override
- verify environment name is one of the allowed bootstrap environments

If startup fails:
- verify DB connection strings
- verify startup migrations are either enabled correctly or run manually

If invoice stamping fails:
- verify issuer references
- verify PAC sandbox base URL and API key reference
- verify product and receiver master data
- inspect persisted stamp evidence and audit events

If payment complement fails:
- verify payment event has fully applied rows under current MVP rules
- verify all applied invoices belong to the same receiver
- inspect complement evidence and provider messages
