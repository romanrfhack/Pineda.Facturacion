# 039 - REP Operational Consolidation And Follow-up

## Scope

This phase closes the REP sprint by consolidating operational follow-up without rebuilding the existing architecture. The implementation keeps the internal and external document-base flows intact and brings refresh/cancel REP actions plus actionable alerts into the same operational experience.

## Base-document operations

New operations are exposed from the base-document context for both origins:

- `POST /api/payment-complements/base-documents/internal/{fiscalDocumentId}/refresh-rep-status`
- `POST /api/payment-complements/base-documents/internal/{fiscalDocumentId}/cancel-rep`
- `POST /api/payment-complements/base-documents/external/{externalRepBaseDocumentId}/refresh-rep-status`
- `POST /api/payment-complements/base-documents/external/{externalRepBaseDocumentId}/cancel-rep`

The API resolves the relevant `paymentComplementDocumentId` from the document-base context when possible, or accepts it explicitly when the UI already knows which REP row the operator selected.

The new services reuse the existing REP engine:

- `RefreshPaymentComplementStatusService`
- `CancelPaymentComplementService`

## Enriched operational model

Internal, external, and unified tray rows now expose a shared set of follow-up signals:

- `hasAppliedPaymentsWithoutStampedRep`
- `hasPreparedRepPendingStamp`
- `hasRepWithError`
- `hasBlockedOperation`
- `nextRecommendedAction`
- `availableActions`
- `alerts[]`

This is derived through:

- `InternalRepOperationalInsightBuilder`
- `ExternalRepOperationalInsightBuilder`

No new duplicate lifecycle store was introduced. Alerts and next action remain derived from the persisted operational state plus REP/payment counts.

## Alert catalog

Current alert codes:

- `BlockedOperation`
- `AppliedPaymentsWithoutStampedRep`
- `PreparedRepPendingStamp`
- `RepStampingRejected`
- `RepCancellationRejected`
- `StampedRepAvailable`

Current severities:

- `critical`
- `warning`
- `info`

## Next recommended action

Current action values:

- `RegisterPayment`
- `PrepareRep`
- `StampRep`
- `RefreshRepStatus`
- `CancelRep`
- `OpenInternalWorkflow`
- `ViewDetail`

The UI uses `nextRecommendedAction` only as an operational hint. It does not replace the explicit per-row validations already enforced by the backend.

## UX consolidation

The final sprint UX now includes:

- internal tray with operational badges and alert chips
- external tray with operational badges and alert chips
- unified tray with source-aware status, alert chips, and recommended action
- internal detail with refresh/cancel REP actions from issued REP rows
- external detail with refresh/cancel REP actions from issued REP rows

The legacy paymentId-based path remains available for compatibility.

## Remaining limitations

- MXN-only operational support remains in place
- refresh/cancel still acts on the selected REP row, not on bulk sets
- no automatic bank reconciliation or auto-follow-up rules were added
- alerts are derived at query time; there is no separate alert inbox or notification engine

## Suggested post-sprint backlog

- alert counters and tray filters by alert code / severity
- dashboard sections for prepared REP, REP errors, and blocked documents
- bulk refresh status for REP batches
- cancellation/rejection audit timeline in unified detail
- notification hooks for critical REP exceptions
