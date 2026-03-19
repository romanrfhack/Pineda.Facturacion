# MVP Backlog

## Goal
Deliver a safe first version of the billing backend without touching the legacy ERP write path.

## Implementation order

### Phase 1 - Foundation
1. Define core domain enums and entities for snapshot import.
2. Define Application abstractions for legacy read and billing write.
3. Remove template placeholder classes and replace them with real folders and namespaces.
4. Add shared dependency injection entry points by project.

### Phase 2 - New billing database write model
1. Add EF Core packages for the new write database.
2. Create BillingDbContext.
3. Add initial entity configurations.
4. Create the first migration for:
   - legacy_import_record
   - sales_order
   - sales_order_item

### Phase 3 - Legacy import use case
1. Add legacy read contracts and models.
2. Implement the first import use case for a single legacy order by id.
3. Persist snapshot header and lines into the new database.
4. Enforce idempotent import behavior using source document identity.

### Phase 4 - HTTP exposure for import
1. Add the first Minimal API endpoint:
   - POST /api/orders/{legacyOrderId}/import
2. Return safe technical and business response data.
3. Add OpenAPI metadata.

### Phase 5 - Billing document foundation
1. Add BillingDocument and BillingDocumentItem domain model.
2. Add BillingWrite persistence for billing_document and billing_document_item.
3. Add a preview-oriented use case without PAC integration yet.

### Phase 6 - PAC integration
1. Add FacturaloPlus abstraction in Application.
2. Add infrastructure implementation for the selected timbrado method.
3. Persist PAC request/response traces.
4. Add stamping flow only after snapshot import and billing document creation are stable.

### Phase 7 - Cancellation and status refresh
1. Add fiscal document lifecycle entities.
2. Add cancellation flow.
3. Add SAT status refresh flow.

## Rules for sequencing
- Do not start PAC integration before snapshot import is working.
- Do not create HTTP endpoints before the use case exists.
- Do not create persistence for future phases unless required by the current phase.
- Prefer one thin vertical slice at a time.

## First coding slice
The first coding slice is:
- remove template classes,
- create initial Domain folders and enums,
- create Application abstractions for snapshot import,
- create BillingWrite persistence shell,
- leave PAC integration untouched for now.
