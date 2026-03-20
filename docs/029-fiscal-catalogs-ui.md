# Fiscal Catalogs UI

## Scope
This step adds the Angular operations UI for fiscal master-data catalogs and import staging flows.

Covered areas:
- active issuer profile
- fiscal receivers
- product fiscal profiles
- receiver import preview/apply
- product import preview/apply

This step does not add new backend catalog behavior.

## Route structure
The catalogs area lives under the protected shell:

- `/app/catalogs`
- `/app/catalogs/issuer-profile`
- `/app/catalogs/receivers`
- `/app/catalogs/product-fiscal-profiles`
- `/app/catalogs/imports/receivers`
- `/app/catalogs/imports/products`

The feature is lazy loaded and stays aligned with the existing Angular 21 feature-vertical structure.

## Role-aware behavior
- `Admin`
  - full catalog read/write/import/apply access
- `FiscalSupervisor`
  - full catalog read/write/import/apply access
- `FiscalOperator`
  - read-only catalog access
  - no issuer update
  - no import apply
- `Auditor`
  - read-only catalog access

The UI hides or disables unauthorized actions, but backend authorization remains the final source of truth.

## Endpoint groups consumed
- `GET /api/fiscal/issuer-profile/active`
- `POST /api/fiscal/issuer-profile/`
- `PUT /api/fiscal/issuer-profile/{id}`
- `GET /api/fiscal/receivers/search?q=...`
- `GET /api/fiscal/receivers/by-rfc/{rfc}`
- `POST /api/fiscal/receivers/`
- `PUT /api/fiscal/receivers/{id}`
- `GET /api/fiscal/product-fiscal-profiles/search?q=...`
- `GET /api/fiscal/product-fiscal-profiles/by-code/{internalCode}`
- `POST /api/fiscal/product-fiscal-profiles/`
- `PUT /api/fiscal/product-fiscal-profiles/{id}`
- `POST /api/fiscal/imports/receivers/preview`
- `GET /api/fiscal/imports/receivers/batches/{batchId}`
- `GET /api/fiscal/imports/receivers/batches/{batchId}/rows`
- `POST /api/fiscal/imports/receivers/batches/{batchId}/apply`
- `POST /api/fiscal/imports/products/preview`
- `GET /api/fiscal/imports/products/batches/{batchId}`
- `GET /api/fiscal/imports/products/batches/{batchId}/rows`
- `POST /api/fiscal/imports/products/batches/{batchId}/apply`

## Screen model
### Issuer profile
- active issuer inspection
- safe presence indicators for secret-related references
- update form for authorized roles only
- no secret/reference values shown from the backend response

### Fiscal receivers
- search/list table
- receiver create form
- receiver edit/inspect flow
- duplicate RFC and backend validation surfaced directly

### Product fiscal profiles
- search/list table
- create/edit form
- SAT-related validation surfaced clearly
- no guessing of missing SAT values

### Import staging screens
- upload and preview
- load batch by id
- batch summary card
- row table with normalized values, validation errors, and apply status
- explicit apply modes:
  - `CreateOnly`
  - `CreateAndUpdate`

## Preview vs apply
The UI keeps preview and apply visually separate.

Preview shows:
- batch summary
- row statuses
- validation errors
- suggested actions

Apply shows:
- selected apply mode
- optional selected row numbers
- stop-on-first-error flag
- explicit confirmation before master-data mutation

Product import screens also surface the optional preview defaults and make `NeedsEnrichment` rows visible instead of guessing missing SAT values.

## Deferred work
- dedicated list/history pages for existing import batches
- richer table filtering, pagination, and sorting
- better receiver/product edit drawers or dialogs
- audit viewer when a safe read endpoint exists
- catalog field masks and dropdown catalogs for SAT codes
