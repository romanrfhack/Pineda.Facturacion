# API Contracts

## POST `/api/orders/{legacyOrderId}/import`

Successful and idempotent imports keep the existing response contract.

When the legacy order was already imported and the newly read snapshot produces a different source hash, the endpoint still returns HTTP `409 Conflict`, but now includes structured context for safe operator navigation.

Conflict payload fields:
- `errorCode = LegacyOrderAlreadyImportedWithDifferentSourceHash`
- `legacyOrderId`
- `currentRevisionNumber`
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

`allowedActions` is machine-readable and currently includes non-destructive navigation plus preview visibility actions:
- `view_existing_sales_order`
- `view_existing_billing_document`
- `view_existing_fiscal_document`
- `reimport_not_available`
- `reimport_preview_not_available_yet`

The conflict remains non-destructive by default. Operators must explicitly preview before applying any controlled reimport.

## GET `/api/orders/{legacyOrderId}/import-preview`

This endpoint is read-only and does not persist changes.

Purpose:
- read the current legacy order
- compare it with the existing imported snapshot
- summarize line-level and total-level changes
- evaluate whether a future controlled reimport would be allowed or blocked

Response includes:
- existing linked entities (`sales_order`, `billing_document`, `fiscal_document`)
- `existingSourceHash`
- `currentSourceHash`
- `currentRevisionNumber`
- `hasChanges`
- `changedOrderFields`
- `changeSummary`
- `lineChanges`
- `reimportEligibility`
- `allowedActions`

Stable string values:
- `lineChanges[].changeType`: `Added`, `Removed`, `Modified`
- `reimportEligibility.status`: `Allowed`, `BlockedByStampedFiscalDocument`, `BlockedByProtectedState`, `NotNeededNoChanges`, `NotAvailableYet`
- `reimportEligibility.reasonCode`: `None`, `FiscalDocumentStamped`, `ProtectedDocumentState`, `NoChangesDetected`, `PreviewOnly`

Preview remains read-only. Reimport execution is a separate confirmed step.

## POST `/api/orders/{legacyOrderId}/reimport`

This endpoint executes the controlled replace step for an already imported legacy order.

Request body:
- `expectedExistingSourceHash`
- `expectedCurrentSourceHash`
- `confirmationMode = ReplaceExistingImport`

Required safety checks at apply time:
- rerun preview/elegibility logic
- block when preview no longer matches the expected hashes
- block when `reimportEligibility.status` is not `Allowed`
- block when the related billing document is not `Draft`
- block when the related fiscal document is `Stamped`, `StampingRequested`, `CancellationRequested`, `Cancelled`, or `CancellationRejected`

Successful response includes:
- `outcome = Reimported`
- `legacyOrderId`
- `legacyImportRecordId`
- `salesOrderId`
- `salesOrderStatus`
- `billingDocumentId`
- `billingDocumentStatus`
- `fiscalDocumentId`
- `fiscalDocumentStatus`
- `previousSourceHash`
- `newSourceHash`
- `currentRevisionNumber`
- `reimportApplied`
- `reimportMode`
- `reimportEligibility`
- `allowedActions`
- `warnings`

Conflict response includes:
- `outcome = Conflict`
- `errorCode`
- `errorMessage`
- `reimportEligibility`
- existing linked entity ids/statuses when available

Current 2B replace strategy:
- update the existing `sales_order` snapshot in place
- if the linked `billing_document` is `Draft`, rebuild its composition in place and recalculate totals
- if the linked `fiscal_document` is editable (`Draft`, `ReadyForStamping`, `StampingRejected`), rebuild its item composition in place, recalculate totals, and move it back to `ReadyForStamping`
- preserve the existing ids and relationships instead of creating versioned replacements
- update the existing `legacy_import_record` hash and timestamps

## GET `/api/orders/{legacyOrderId}/import-revisions`

This endpoint exposes the formal revision history for a previously imported legacy order.

Response includes:
- `legacyOrderId`
- `currentRevisionNumber`
- `revisions[]`

Each revision currently persists:
- `revisionNumber`
- `previousRevisionNumber`
- `actionType`: `Imported` or `Reimported`
- `outcome`
- `sourceHash`
- `previousSourceHash`
- `appliedAtUtc`
- `isCurrent`
- actor metadata when available (`actorUserId`, `actorUsername`)
- impacted entities (`salesOrderId`, `billingDocumentId`, `fiscalDocumentId`)
- `eligibilityStatus`, `eligibilityReasonCode`, `eligibilityReasonMessage`
- `changeSummary`
- `snapshotJson`
- `diffJson`

Phase 3 revision model:
- keep operational entities (`sales_order`, `billing_document`, `fiscal_document`) in place
- persist a formal `legacy_import_revision` history row on initial import and every successful reimport
- mark exactly one revision as current per `legacy_import_record`
- preserve previous revisions for audit and operator traceability
- if an older imported record has no persisted revision rows yet, the history endpoint synthesizes a compatibility `revision 1` view instead of failing

Still out of scope after this phase:
- rollback/revert execution from a historical revision
- deep entity snapshot/version branching beyond the revision payload
- overwrite of stamped or protected fiscal states
