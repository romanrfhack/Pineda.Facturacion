# MVP Backlog

## Goal
Deliver a safe first version of the billing backend without touching the legacy ERP write path.

## Current status
The current backend now supports two validated end-to-end flows:
- Legacy order snapshot import from the legacy MySQL database into acturacion_v2
- Internal BillingDocument creation from an imported SalesOrder snapshot

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

### Phase 5 - BillingDocument foundation
- BillingDocument and BillingDocumentItem persistence created
- CreateBillingDocument use case implemented
- Endpoint implemented:
  - POST /api/sales-orders/{salesOrderId}/billing-documents
- Conflict behavior validated:
  - one active BillingDocument per SalesOrder for the current MVP
- BillingDocument creation validated against imported SalesOrder snapshots

## Verified behavior
- Import of a valid legacy order succeeded
- Re-import of the same order returned Idempotent
- Snapshot data persisted correctly into:
  - legacy_import_record
  - sales_order
  - sales_order_item
- BillingDocument creation succeeded from imported SalesOrder snapshots
- Re-creation for the same SalesOrder returned Conflict
- BillingDocument data persisted correctly into:
  - billing_document
  - billing_document_item

## Pending phases

### Phase 6 - Fiscal foundation
- FiscalDocument
- FiscalStamp
- FiscalCancellation
- PAC request/response trace tables if additional behavior is needed

### Phase 7 - PAC integration
- FacturaloPlus abstraction and infrastructure implementation
- Stamping flow
- Persist fiscal evidence
- UUID/XML lifecycle

### Phase 8 - Cancellation and status refresh
- Fiscal cancellation flow
- SAT/PAC status refresh

## Current next priority
Build the fiscal foundation in Domain/Application/Infrastructure before integrating FacturaloPlus.
