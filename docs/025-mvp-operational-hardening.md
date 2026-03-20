# MVP Operational Hardening

## Purpose
This document captures the operational checklist for the completed MVP fiscal lifecycle.

## Required configuration keys
- `LegacyRead:ConnectionString`
- `BillingWrite:ConnectionString`
- `Auth:Jwt:Issuer`
- `Auth:Jwt:Audience`
- `Auth:Jwt:SigningKey`
- `Auth:Jwt:ExpiresMinutes`
- `Auth:BootstrapAdmin:Enabled`
- `Auth:BootstrapAdmin:Username`
- `Auth:BootstrapAdmin:DisplayName`
- `Auth:BootstrapAdmin:Password`
- `Bootstrap:ApplyMigrationsOnStartup`
- `Bootstrap:SeedDefaultRoles`
- `Bootstrap:SeedDefaultTestUsers`
- `Bootstrap:DefaultTestUserPassword`
- `OpenApi:EnableSwagger`
- `FacturaloPlus:BaseUrl`
- `FacturaloPlus:StampPath`
- `FacturaloPlus:CancelPath`
- `FacturaloPlus:StatusQueryPath`
- `FacturaloPlus:PaymentComplementStampPath`
- `FacturaloPlus:PaymentComplementCancelPath`
- `FacturaloPlus:PaymentComplementStatusQueryPath`
- `FacturaloPlus:ProviderName`
- `FacturaloPlus:PayloadMode`
- `FacturaloPlus:ApiKeyHeaderName`
- `FacturaloPlus:ApiKeyReference`
- `FacturaloPlus:TimeoutSeconds`
- `SecretReferences:Values:*`

Rules:
- checked-in config remains placeholders only
- runtime secrets come from deployment-time configuration or secret stores
- certificate/key/password values are resolved indirectly from references
- JWT signing keys must be injected at deployment time and must not remain at placeholder values
- bootstrap admin must stay disabled in production unless there is a controlled one-time access procedure
- default test users must stay disabled in production
- Swagger must stay disabled in production unless explicitly and temporarily enabled for controlled support work

## Secret-reference expectations
Persisted fiscal snapshots store only:
- `CertificateReference`
- `PrivateKeyReference`
- `PrivateKeyPasswordReference`
- `PacEnvironment`

Resolved secret values must never appear in:
- API responses
- logs
- persisted PAC audit entities
- auth audit entities

## PAC timeout and retry expectations
Current MVP behavior:
- provider transport/unavailability returns `503`
- invoice stamp unavailable returns document status to `ReadyForStamping`
- invoice cancel unavailable returns document status to `Stamped`
- payment-complement stamp unavailable returns document status to `ReadyForStamping`
- payment-complement cancel unavailable returns document status to `Stamped`

Recommended operator policy:
- keep provider timeouts short and explicit
- use status refresh before manual retry when provider availability is uncertain
- avoid blind duplicate retries after timeout

## Migration and deployment order
Recommended order:
1. deploy binaries and configuration placeholders
2. apply EF Core migrations
3. verify JWT and PAC configuration binding
4. verify bootstrap role/user policy for the target environment
5. run smoke tests
6. enable traffic

## Recommended smoke-test sequence
1. log in with a known local user and verify `/api/auth/me`
   non-production can use the seeded test users if enabled
2. verify anonymous access is rejected for one protected write endpoint
   optional: use `/swagger` for backend-only API inspection in `Development`, `Local`, or `Sandbox`
3. import one known legacy order
4. create one billing document
5. prepare one fiscal document
6. stamp the fiscal document
7. read back stamp evidence
8. for one stamped `PPD` invoice, create AR invoice, payment, application, and payment complement
9. stamp the payment complement
10. validate cancellation/status-refresh endpoints against a controlled sandbox record

Automated browser e2e is intentionally separate from this smoke-test sequence. UI e2e uses deterministic mocked backend responses and does not replace real deployment or PAC sandbox verification.

## What evidence is stored in the database
Current MVP stores:
- app users, roles, and user-role assignments
- audit events for critical operations
- fiscal snapshots
- payment-complement snapshots
- normalized stamp evidence
- normalized cancellation evidence
- latest-known external status snapshots
- XML content in DB for stamped invoice/complement evidence
- hashes, UUIDs, tracking ids, provider codes/messages, and timestamps

Safe read behavior:
- stamp metadata is available through normal evidence endpoints
- XML is exposed only through explicit read-only XML endpoints
- XML is intended for operator inspection and manual support work, not as the default first view

Future recommendation:
- move large XML payloads to file/blob storage
- keep hashes, metadata, UUIDs, and storage references in DB

## Support and debug checklist
When stamping fails:
- verify local status is eligible
- verify secret references are present
- verify actor role is allowed to execute the operation
- verify deployment replaced placeholder config
- inspect normalized provider code/message
- inspect persisted response summary and XML hash

When cancellation fails:
- verify local UUID evidence exists
- verify reason-code and replacement-UUID rules
- inspect normalized rejection details
- run status refresh when timeout/unavailable occurred

When refresh fails:
- verify UUID evidence exists
- inspect provider availability and timeout settings
- verify local lifecycle was not advanced incorrectly

## Current limitations
- `MXN` only
- no multi-currency complements
- no advance-payment or unapplied-remainder flows
- no regrouping or splitting payment events
- latest status is stored as a snapshot, not full history
- local auth only
- no MFA
- role-based authorization only, without finer permission scopes
- automated UI e2e does not start the real backend or PAC provider
- issuer and fiscal catalog business data still require manual setup before real sandbox stamping
