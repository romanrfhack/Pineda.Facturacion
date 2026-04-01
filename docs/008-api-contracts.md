# API Contracts

## POST `/api/orders/{legacyOrderId}/import`

Successful and idempotent imports keep the existing response contract.

When the legacy order was already imported and the newly read snapshot produces a different source hash, the endpoint still returns HTTP `409 Conflict`, but now includes structured context for safe operator navigation.

Conflict payload fields:
- `errorCode = LegacyOrderAlreadyImportedWithDifferentSourceHash`
- `legacyOrderId`
- `existingSalesOrderId`
- `existingSalesOrderStatus`
- `existingBillingDocumentId`
- `existingBillingDocumentStatus`
- `existingFiscalDocumentId`
- `existingFiscalDocumentStatus`
- `fiscalUuid`
- `importedAtUtc`
- `existingSourceHash`
- `currentSourceHash`
- `allowedActions`

`allowedActions` is machine-readable and currently includes only non-destructive navigation and visibility actions:
- `view_existing_sales_order`
- `view_existing_billing_document`
- `view_existing_fiscal_document`
- `reimport_not_available`
- `reimport_preview_not_available_yet`

This phase does not implement reimport or overwrite behavior. The conflict remains blocked by default.
