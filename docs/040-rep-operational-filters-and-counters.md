# REP operational filters and counters

## Scope

Phase 1 of the `Operacion y monitoreo REP` sprint closes the operational catalogs used by REP trays and exposes the first filtering and counting layer without introducing quick views yet.

Covered in this phase:

- stable operational alert codes
- stable severity catalog
- stable `nextRecommendedAction` values without `null`
- tray filters for internal, external, and unified searches
- aggregated counters for UI chips and semaphores
- copy cleanup in the unified experience

Not covered in this phase:

- quick views such as `Pendientes de timbrar` or `Con error`
- mass refresh
- enriched timeline
- notification hooks

## Operational catalogs

### Alert codes

- `AppliedPaymentsWithoutStampedRep`
- `PreparedRepPendingStamp`
- `RepStampingRejected`
- `RepCancellationRejected`
- `BlockedOperation`
- `CancelledBaseDocument`
- `ValidationBlocked`
- `SatValidationUnavailable`
- `UnsupportedCurrency`
- `DuplicateExternalInvoice`
- `StampedRepAvailable`

Reason codes remain independent from alert codes. Builders map internal and external operational reasons to the alert catalog without leaking validation reason codes directly into tray filters.

### Severities

- `info`
- `warning`
- `error`
- `critical`

`error` is now first-class and is used for rejected stamping/cancellation and validation blocking cases. `critical` remains reserved for hard operational blocks such as cancelled base documents.

### Recommended actions

- `RegisterPayment`
- `PrepareRep`
- `StampRep`
- `RefreshRepStatus`
- `CancelRep`
- `ViewDetail`
- `Blocked`
- `NoAction`

The trays no longer leave `nextRecommendedAction` as `null`. `Blocked` and `NoAction` are explicit values for filtering and counting.

## Search endpoints

The following endpoints were enriched:

- `GET /api/payment-complements/base-documents/internal`
- `GET /api/payment-complements/base-documents/external`
- `GET /api/payment-complements/base-documents`

New query parameters:

- `alertCode`
- `severity`
- `nextRecommendedAction`

Existing filters remain supported.

## Response contract

List responses now include `summaryCounts`:

```json
{
  "page": 1,
  "pageSize": 25,
  "totalCount": 10,
  "totalPages": 1,
  "items": [],
  "summaryCounts": {
    "infoCount": 1,
    "warningCount": 3,
    "errorCount": 2,
    "criticalCount": 1,
    "blockedCount": 1,
    "alertCounts": [
      { "code": "PreparedRepPendingStamp", "count": 2 }
    ],
    "nextRecommendedActionCounts": [
      { "code": "StampRep", "count": 2 }
    ]
  }
}
```

Counters are calculated on the current tray scope after business filters such as date, RFC, source, validation status, eligible, and blocked, but before the new operational filters are applied. This keeps chips useful while the user narrows the list.

## UI behavior

Internal, external, and unified trays now show:

- severity badge derived from the highest active alert
- quick chips with counters
- stable recommended-action labels
- alert-code/severity/action filters

The unified hub copy was also updated to reflect that external REP operation is already available.

## Limitations

- quick views still belong to Phase 2
- counters are tray-level, not global dashboard metrics
- no background refresh or alert push yet
- `OpenInternalWorkflow` remains as an available action for compatibility, but it is not used as `nextRecommendedAction`
