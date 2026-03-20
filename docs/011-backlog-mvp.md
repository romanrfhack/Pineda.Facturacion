# MVP Backlog

## Goal
Deliver a safe first version of the billing backend without touching the legacy ERP write path.

## Current status
The first end-to-end slice for snapshot import and internal billing document creation is working:
- Legacy order read from MySQL legacy database
- Snapshot import persisted into facturacion_v2
- Idempotent re-import validated
- BillingDocument foundation persisted and callable from the API
- FiscalDocument foundation from BillingDocument plus local fiscal master data is implemented
- FacturaloPlus stamping from persisted FiscalDocument snapshots is implemented for the current MVP
- Fiscal cancellation and latest-status refresh from persisted fiscal evidence are implemented for the current MVP
- Accounts receivable invoices, payments, and payment applications are implemented for stamped credit-sale `PPD` fiscal documents
- Payment complement snapshot preparation and PAC stamping are implemented for persisted payment events in the current MVP
- Payment-complement cancellation and latest-status refresh are implemented from persisted complement evidence in the current MVP
- Integration coverage and operational hardening are implemented for the completed MVP lifecycle
- Local JWT authentication, role-based authorization, and critical-action audit trail are implemented for the backend MVP
- Initial snapshot migration applied successfully

## Completed phases

### Phase 1 - Foundation
- Core domain enums created
- Snapshot domain entities created
- BillingDocument domain entities created
- Application abstractions created
- Dependency injection entry points created

### Phase 2 - New billing database write model
- EF Core write model created for:
  - legacy_import_record
  - sales_order
  - sales_order_item
  - billing_document
  - billing_document_item
- Initial migrations created and applied successfully

### Phase 3 - Legacy import use case
- Legacy read models created
- Import use case implemented
- Idempotent import behavior implemented
- Typed outcome mapping implemented
- Real legacy pricing formulas validated and applied

### Phase 4 - HTTP exposure for import
- Endpoint implemented:
  - POST /api/orders/{legacyOrderId}/import

### Phase 5 - Snapshot financial field validation
- Legacy pricing formulas validated from real TipoDocPedido = 'F' data
- Snapshot import now persists:
  - subtotal
  - discount_total
  - tax_total
  - line_number
  - detail discount amount
- Header and detail totals are now internally consistent

### BillingDocument foundation
- BillingDocument entity and persistence foundation implemented
- BillingDocument creation use case implemented from SalesOrder snapshot
- Endpoint implemented:
  - POST /api/sales-orders/{salesOrderId}/billing-documents
- BillingDocument creation respects draft-only foundation rules from ADR-008
- BillingDocument now persists currency semantics and the commercial product key needed for later fiscal preparation
- Current MVP BillingDocument creation is explicitly `MXN`-only; multi-currency is deferred until the imported commercial snapshot carries a reliable exchange rate

## Verified behavior
- Import of a valid legacy order succeeded
- Re-import of the same order returned Idempotent
- Snapshot data persisted correctly into:
  - legacy_import_record
  - sales_order
  - sales_order_item
- BillingDocument creation from a valid SalesOrder succeeded
- Duplicate BillingDocument creation for the same SalesOrder returns Conflict

## Pending phases

### Phase 6 - Fiscal foundation
Scope:
- Add FiscalDocument, FiscalDocumentItem, FiscalStamp, and FiscalCancellation domain/application/persistence foundation
- Introduce persisted fiscal snapshot generation from BillingDocument
- Define fiscal state transitions before PAC integration

Acceptance criteria:
- A BillingDocument can generate exactly one persisted FiscalDocument draft snapshot
- FiscalDocumentItem persists the line-level fiscal snapshot content required by the later PAC payload path
- Fiscal snapshot content is stored locally before any PAC call is attempted
- FiscalDocument lifecycle states are explicit and typed
- FiscalStamp and FiscalCancellation persistence shells exist without PAC coupling
- No fiscal payload is reconstructed directly from SalesOrder at call time
- Issuer, receiver, and product SAT data are snapshotted into FiscalDocument/FiscalDocumentItem at creation time
- Payment method/form fields are persisted on FiscalDocument for later PAC and credit-sale flows
- Current MVP fiscal preparation assumes `MXN`-only BillingDocuments until multi-currency snapshot import is implemented

### Phase 7 - Fiscal master data
Scope:
- Add issuer fiscal configuration
- Add receiver master data searchable by RFC
- Add product fiscal profile model with SAT mappings
- Add preview-only Excel staging/import audit for receivers and product fiscal profiles
- Define import and validation rules for master data

Acceptance criteria:
- Issuer fiscal profile can be stored locally with placeholder-based secret references only
- Receivers can be searched by RFC with autocomplete-ready queries
- Product fiscal profiles can be linked to commercial products without live SAT inference
- Missing required SAT mappings block fiscal snapshot generation with explicit validation errors
- Excel preview import persists batches and rows into staging tables only
- Duplicate RFC/internal-code rows inside one file are detected explicitly in preview
- Existing master matches are previewed as create/update/conflict decisions without mutating master data
- Product preview reports `NeedsEnrichment` when `TaxObjectCode` or `VatRate` is still missing after optional batch defaults
- Excel import strategy is documented and decomposed into staging, validation, and later apply stages

### Phase 8 - PAC integration
Scope:
- Add FacturaloPlus adapter behind Application abstractions
- Support payload generation from persisted FiscalDocument snapshot
- Normalize PAC responses into local result models

Acceptance criteria:
- Application depends only on PAC abstraction, never on FacturaloPlus-specific DTOs
- Stamping request flow starts from persisted FiscalDocument snapshot, not from SalesOrder
- PAC request/response normalization stores provider code, provider message, raw payload references, and timestamps
- Secret values are externalized and referenced by placeholders or configuration keys only
- No endpoint contains PAC payload construction logic
- Current implemented slice covers stamping only; cancellation and SAT status refresh remain pending

### Phase 9 - Cancellation and SAT status refresh
Scope:
- Support cancellation reasons 01/02/03/04
- Support replacement UUID flow where required
- Add periodic or on-demand SAT/PAC status refresh

Acceptance criteria:
- Cancellation requests persist fiscal motive code and optional replacement UUID
- FiscalCancellation records capture request time, result time, and normalized provider response
- SAT/PAC status refresh updates FiscalDocument state without mutating historical snapshot content
- Cancellation and refresh flows are auditable from local persistence
- Unsupported motive/status combinations fail with explicit typed results
- Current implemented slice uses one current cancellation record per fiscal document and latest-known status fields on FiscalStamp

### Phase 10 - Credit sales, payments, and payment complements
Scope:
- Add credit terms and accounts receivable concepts
- Add payment records and payment applications
- Drive payment complements from payment events

Acceptance criteria:
- Credit terms can be attached to internal billing documents without PAC coupling
- Payment records and payment applications persist enough data to compute outstanding balance locally
- Outstanding balance is derived from billing totals minus applied payments, not from ad hoc flags
- Payment complement generation is triggered from payment application events, not only from “fully paid” status
- The model distinguishes commercial payment tracking from fiscal CFDI payment complement generation
- Partial payments and multiple applications across documents are supported by the design
- Current implemented slice creates AR invoices only for stamped credit-sale `PPD` fiscal documents
- Current implemented slice persists:
  - accounts_receivable_invoice
  - accounts_receivable_payment
  - accounts_receivable_payment_application
- Current implemented slice supports partial and multi-invoice payment application with atomic validation and local balance updates
- Current implemented slice prepares one payment complement per payment event and stamps it from persisted payment/fiscal evidence only
- Current implemented slice requires all invoices in the payment event to belong to the same receiver before complement preparation
- Current implemented slice derives complement installment numbers from persisted payment-application history on each invoice
- Current implemented slice supports payment-complement cancellation and latest-status refresh from persisted complement evidence only

## Cross-cutting constraints
- The local system remains the source of truth after import.
- PAC integration must consume persisted FiscalDocument snapshots only.
- Receiver and product fiscal data must be stored locally, not inferred live on demand.
- Secrets, API keys, certificates, and passwords must be externalized and documented with placeholders only.

## Current next priority
Build the admin and operations UI on top of the secured backend, then extend the completed MVP toward richer attempt history, multi-currency, and advance-payment scenarios without bypassing persisted fiscal evidence.
