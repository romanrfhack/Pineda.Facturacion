# Auth, Authorization, and Audit Foundation

## Scope
This step adds the minimum backend security foundation required before the admin and operations UI:
- local username/password authentication
- JWT bearer authentication for API access
- role-based authorization for operational endpoints
- audit persistence for critical actions

This step does not add:
- external identity providers
- MFA
- password reset flows
- approval workflows
- fine-grained permission models beyond roles

## Local auth strategy
- users are stored locally in `app_user`
- passwords are stored only as secure hashes
- usernames are normalized to uppercase for lookup and uniqueness
- inactive users cannot log in
- API access uses short-lived JWT bearer tokens

Implemented endpoints:
- `POST /api/auth/login`
- `GET /api/auth/me`

Frontend assumption added in Step 7A:
- the current admin UI treats bare `401` from login as invalid credentials
- all other `401` responses are treated as expired/invalid session and force return to login

## Role model
Implemented roles:
- `Admin`
- `FiscalSupervisor`
- `FiscalOperator`
- `Auditor`

## Authorization policy mapping
- `AdminOnly`
  - full access
- `SupervisorOrAdmin`
  - invoice stamp/cancel/refresh
  - payment-complement stamp/cancel/refresh
  - fiscal master-data write operations
  - staging import preview/apply
- `OperatorOrAbove`
  - legacy order import
  - billing document creation
  - fiscal document preparation
  - AR invoice creation
  - payment creation
  - payment application
  - payment-complement preparation
- `Authenticated`
  - operational read endpoints
  - `/api/auth/me`

Anonymous access is allowed only for `POST /api/auth/login`.

## Audit-event model
Critical operations persist `audit_event` rows with:
- actor identity
- action type
- target entity type/id
- outcome
- correlation id
- safe request/response summaries
- IP address
- user agent
- timestamps

Current critical actions audited include:
- login success/failure
- legacy order import
- billing document creation
- fiscal document preparation
- invoice stamp/cancel/refresh
- fiscal master-data create/update
- staging preview/apply
- AR invoice creation
- payment creation
- payment application
- payment-complement prepare/stamp/cancel/refresh

## Safe logging and audit rules
Never persist or expose:
- plain-text passwords
- JWT tokens
- private keys
- certificate values
- password references in API responses
- raw PAC request bodies
- resolved secret values

Audit summaries should contain only operationally useful metadata such as ids, outcomes, provider tracking ids, UUIDs, and timestamps.

## Bootstrap admin strategy
Implemented MVP approach:
- configuration-driven bootstrap admin
- enabled only in `Development`, `Local`, or `Testing`
- values must come from placeholders or deployment-time configuration

Recommended production posture:
- keep bootstrap admin disabled in production
- provision the first admin user through a controlled operational process
- rotate bootstrap credentials immediately if temporary local enablement is ever used

Expanded non-production bootstrap:
- roles can be ensured automatically
- seeded non-production test users can be enabled explicitly through `Bootstrap:*`
- seeded users are intended only for local, testing, and sandbox environments

## Required configuration
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

Checked-in config must use placeholders only.

## Deferred items
- MFA
- external identity provider / SSO
- refresh-token flows
- fine-grained permissions beyond roles
- dual approval workflows
- immutable audit-history / event-sourcing model

## Audit read access
The MVP now includes a safe read-only audit endpoint:
- `GET /api/audit-events`

Access is limited to:
- `Admin`
- `FiscalSupervisor`
- `Auditor`

The endpoint returns only persisted safe audit fields and supports basic filtering and paging. It does not provide any audit mutation capability.
