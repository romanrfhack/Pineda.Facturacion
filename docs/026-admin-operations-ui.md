# Admin and Operations UI

## Scope
This step adds the first Angular 21 operations frontend on top of the secured backend. It covers:
- login and session restoration
- role-aware protected shell
- operator path from legacy-order import through fiscal preparation
- AR and payment-ledger operations
- payment-complement operations
- audit visibility
- evidence and XML viewing for stamped fiscal documents and payment complements
- Spanish-first operator-facing UI text across the secured frontend

This step does not add new backend business flows.

## Frontend architecture
The UI uses a practical feature-vertical structure:

- `core`
  - auth/session
  - permission helpers
  - HTTP interceptors
  - app shell and navigation
  - global feedback handling
- `shared`
  - small reusable UI components such as status badges
- `features`
- `auth`
- `catalogs`
- `orders`
- `fiscal-documents`
- `accounts-receivable`
- `payment-complements`

The goal is to keep orchestration close to each feature while leaving cross-cutting concerns centralized.

## Auth and session flow
- root route redirects to `/login`
- `/login` is public
- all secured routes live under `/app`
- `/app` is protected by an auth guard
- session state uses Angular signals
- the token is stored locally in browser storage for the MVP
- session restore calls `/api/auth/me`
- `401` clears the session and returns the user to login
- `403` shows a clear authorization message without pretending the action succeeded

## Role-aware navigation and actions
Navigation is driven from a config object filtered by current roles.

Current policy alignment in UI:
- `Admin` and `FiscalSupervisor`
  - can stamp/cancel fiscal documents
  - can stamp/cancel payment complements
  - can execute fiscal write operations
- `FiscalOperator`
  - can import orders
  - can create billing documents
  - can prepare fiscal documents
  - can create AR invoices, payments, and payment applications
  - cannot stamp or cancel fiscal documents/complements
- `Auditor`
  - read-only operational view

The UI hides or disables actions based on current role, but backend authorization remains the final source of truth.

## Language note
The current UI is normalized to Spanish for operator-facing text, while technical acronyms such as RFC, UUID, XML, SAT, PAC, CFDI, IVA, CSD, and JWT remain unchanged.

Chosen operator-facing terms:
- `Órdenes`
- `Documentos fiscales`
- `Cuentas por cobrar`
- `Complementos de pago`
- `Catálogos`
- `Auditoría`
- `Estatus`
- `Razón social`

See [035-frontend-spanish-localization.md](/home/romanrfhack/code/Pineda.Facturacion/docs/035-frontend-spanish-localization.md).

## Pages and modules
- `LoginPage`
- `CatalogsHomePage`
- `IssuerProfilePage`
- `FiscalReceiversPage`
- `ProductFiscalProfilesPage`
- `ReceiverImportsPage`
- `ProductImportsPage`
- `AuditEventsPage`
- `OrdersOperationsPage`
- `FiscalDocumentOperationsPage`
- `AccountsReceivablePage`
- `PaymentComplementsPage`
- protected `AppShell`

Supporting components:
- `BillingDocumentCard`
- `FiscalReceiverForm`
- `ProductFiscalProfileForm`
- `ImportBatchSummaryCard`
- `AuditFilters`
- `AuditEventsTable`
- `AuditEventDetail`
- `FiscalDocumentCard`
- `FiscalStampEvidenceCard`
- `FiscalCancellationCard`
- `AccountsReceivableCard`
- `PaymentCreateForm`
- `PaymentApplicationForm`
- `PaymentComplementCard`
- `PaymentComplementStampCard`
- `PaymentComplementCancellationCard`
- `FiscalStampEvidenceDetail`
- `PaymentComplementStampEvidenceDetail`
- `XmlViewerPanel`

## Endpoint groups consumed
- `auth/login`
- `auth/me`
- `fiscal/issuer-profile/active`
- `fiscal/issuer-profile/{id}`
- `fiscal/receivers/search`
- `fiscal/receivers/by-rfc/{rfc}`
- `fiscal/receivers/{id}`
- `fiscal/product-fiscal-profiles/search`
- `fiscal/product-fiscal-profiles/by-code/{internalCode}`
- `fiscal/product-fiscal-profiles/{id}`
- `fiscal/imports/receivers/preview`
- `fiscal/imports/receivers/batches/{batchId}`
- `fiscal/imports/receivers/batches/{batchId}/rows`
- `fiscal/imports/receivers/batches/{batchId}/apply`
- `fiscal/imports/products/preview`
- `fiscal/imports/products/batches/{batchId}`
- `fiscal/imports/products/batches/{batchId}/rows`
- `fiscal/imports/products/batches/{batchId}/apply`
- `audit-events`
- `orders/{legacyOrderId}/import`
- `sales-orders/{salesOrderId}/billing-documents`
- `billing-documents/{billingDocumentId}/fiscal-documents`
- `fiscal-documents/{id}`
- `fiscal-documents/{id}/stamp`
- `fiscal-documents/{id}/stamp/xml`
- `fiscal-documents/{id}/cancel`
- `fiscal-documents/{id}/refresh-status`
- `fiscal-documents/{id}/accounts-receivable`
- `accounts-receivable/payments`
- `accounts-receivable/payments/{id}`
- `accounts-receivable/payments/{id}/apply`
- `accounts-receivable/payments/{id}/payment-complements`
- `accounts-receivable/payments/{id}/payment-complement`
- `payment-complements/{id}/stamp`
- `payment-complements/{id}/stamp/xml`
- `payment-complements/{id}/cancel`
- `payment-complements/{id}/refresh-status`

## Operator-facing statuses
The UI surfaces:
- current local document status
- latest operation outcome
- validation and provider error messages
- UUIDs
- provider tracking ids
- current balances
- latest-known external status when refreshed

## Receiver selection note
In fiscal-document preparation, receiver selection now uses a single autocomplete interaction:
- the operator types RFC or razón social
- the UI debounces and queries the existing fiscal-receiver search endpoint
- up to 5 suggestions are shown
- selecting one suggestion immediately sets the receiver used for preparation

This replaces the previous two-step search-plus-select flow.

## Billing document continuity
The Orders and Fiscal Documents areas now keep continuity around billing documents:
- when billing-document creation succeeds, the UI navigates directly to fiscal-document preparation with the `billingDocumentId`
- when billing-document creation returns `Conflict`, the UI reuses the returned `billingDocumentId` and opens the existing document instead of leaving the operator blocked
- the fiscal-documents screen includes a compact billing-document selector that can load an existing billing document by billing id, sales-order id, or legacy-order id

This uses the existing create response plus a small billing-document lookup/search read surface.

## Legacy import hash conflict
Manual legacy-order import now exposes an enriched non-destructive conflict when `/api/orders/{legacyOrderId}/import` detects that the order was already imported but the current legacy snapshot has a different source hash.

The UI behavior for this phase is:
- keep the import blocked
- show the existing linked `sales_order`, `billing_document`, and `fiscal_document` when available
- show source-hash traceability (`existingSourceHash` and `currentSourceHash`)
- use `errorCode = LegacyOrderAlreadyImportedWithDifferentSourceHash` plus `allowedActions` instead of parsing message text
- allow only safe navigation or visibility actions

This phase does not implement reimport, overwrite, or automatic reconciliation.

## Destructive-action confirmations
The UI requires explicit confirmation before:
- invoice cancellation
- payment-complement cancellation

## Deferred UI work
- audit-events viewer, unless a safe read endpoint is added later
- richer search/list pages for existing fiscal documents and payments
- real toast/notification system beyond the current MVP feedback banner
- refresh-token handling
- advanced form catalogs and input masks
- broader end-to-end browser coverage beyond the current deterministic happy path

## Automated e2e note
Automated browser tests are intentionally narrow and deterministic.

Current strategy:
- Playwright starts the Angular frontend locally
- browser tests mock backend HTTP at the browser boundary
- no real PAC calls are made in automated e2e

This keeps UI e2e stable while backend operational behavior remains covered by the existing .NET integration suite.

See [028-e2e-test-environment.md](/home/romanrfhack/code/Pineda.Facturacion/docs/028-e2e-test-environment.md).

## Catalogs note
The first admin catalog slice is now available under `/app/catalogs` with:
- issuer profile
- fiscal receivers
- product fiscal profiles
- receiver imports
- product imports

See [029-fiscal-catalogs-ui.md](/home/romanrfhack/code/Pineda.Facturacion/docs/029-fiscal-catalogs-ui.md).

## Audit note
The first safe read-only audit viewer is now available under `/app/audit`.

See [030-audit-viewer-ui.md](/home/romanrfhack/code/Pineda.Facturacion/docs/030-audit-viewer-ui.md).

## Evidence note
Stamped invoice and payment-complement evidence is now visible from the existing operational screens, with XML exposed only through an explicit secondary action.

See [031-evidence-and-xml-viewer-ui.md](/home/romanrfhack/code/Pineda.Facturacion/docs/031-evidence-and-xml-viewer-ui.md).
