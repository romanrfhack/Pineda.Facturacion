# Audit Viewer UI

## Scope
This step adds a read-only audit viewer to the secured Angular operations UI.

It is limited to:
- listing audit events
- filtering audit events
- inspecting safe event details

It does not add any write, edit, delete, export, or analytics workflow.

## Route structure
- `/app/audit`

The route is lazy loaded and protected by the existing auth/session foundation.

## Role-aware behavior
- `Admin`
  - audit read access
- `FiscalSupervisor`
  - audit read access
- `Auditor`
  - audit read access
- `FiscalOperator`
  - no audit route access in the UI

Backend authorization remains the final source of truth.

## Endpoint consumed
- `GET /api/audit-events`

Supported query parameters:
- `page`
- `pageSize`
- `actorUsername`
- `actionType`
- `entityType`
- `entityId`
- `outcome`
- `fromUtc`
- `toUtc`
- `correlationId`

## Safe fields shown
List view:
- `occurredAtUtc`
- `actorUsername`
- `actionType`
- `entityType`
- `entityId`
- `outcome`
- `correlationId`

Detail view:
- `errorMessage`
- `requestSummaryJson`
- `responseSummaryJson`
- `ipAddress`
- `userAgent`

Only already-redacted stored summaries are shown.

## Hidden fields
The UI does not show:
- passwords
- password hashes
- JWTs
- secret references
- private key references
- raw PAC request payloads
- raw secret resolver values

## UI structure
- filter bar
- read-only events table
- safe detail card

The page prioritizes operational clarity over dense analytics.

## Deferred work
- pagination controls beyond the initial page/query support
- correlation-based deep links into related operational screens
- export/reporting
- richer JSON formatting and diff views
- audit analytics/dashboarding
