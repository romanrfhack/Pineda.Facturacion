# MVP Backlog

## Goal
Deliver a safe first version of the billing backend without touching the legacy ERP write path.

## Current status
The first end-to-end slice for snapshot import is working:
- Legacy order read from MySQL legacy database
- Snapshot import persisted into facturacion_v2
- Idempotent re-import validated
- Initial snapshot migration applied successfully

## Completed phases

### Phase 1 - Foundation
- Core domain enums created
- Snapshot domain entities created
- Application abstractions created
- Initial DI entry points created

### Phase 2 - New billing database write model
- EF Core write model created for:
  - legacy_import_record
  - sales_order
  - sales_order_item
- Initial migration created and applied successfully

### Phase 3 - Legacy import use case
- Legacy read models created
- Import use case implemented
- Idempotent import behavior implemented
- Typed outcome mapping implemented

### Phase 4 - HTTP exposure for import
- Endpoint implemented:
  - POST /api/orders/{legacyOrderId}/import

## Verified behavior
- Import of a valid legacy order succeeded
- Re-import of the same order returned Idempotent
- Data persisted correctly into:
  - legacy_import_record
  - sales_order
  - sales_order_item

## Pending phases

### Phase 5 - Improve imported financial fields
Still pending because the legacy mapping is not fully resolved for:
- subtotal
- discount_total
- tax_total
- line_number
- SAT codes

### Phase 6 - Billing document foundation
- BillingDocument
- BillingDocumentItem
- Preview flow

### Phase 7 - PAC integration
- FacturaloPlus abstraction
- Stamping flow
- Persist fiscal evidence

### Phase 8 - Cancellation and status refresh
- Fiscal cancellation flow
- SAT/PAC status refresh

## Current next priority
Resolve remaining legacy mapping TBDs and then build the internal BillingDocument foundation before PAC integration.
