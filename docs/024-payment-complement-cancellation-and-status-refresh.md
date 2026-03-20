# Payment Complement Cancellation And Status Refresh

## Scope
This phase adds:
- payment-complement cancellation
- payment-complement latest-status refresh
- normalized persistence of cancellation evidence
- normalized persistence of latest-known external status evidence

This phase does not add:
- multi-currency complement behavior
- advance-payment or unapplied-remainder flows
- regrouping or splitting payment events

## Why these flows use persisted complement evidence
Cancellation and refresh start from persisted local fiscal evidence only:
- `PaymentComplementDocument`
- `PaymentComplementRelatedDocument`
- `PaymentComplementStamp`

After the complement snapshot exists, the system does not reconstruct operations from live `SalesOrder`, `BillingDocument`, issuer master data, receiver master data, or product master data.

## Provider operations
For the current FacturaloPlus adapter, the chosen operations are:
- `POST /cfdi/payment-complement/cancel`
- `POST /cfdi/payment-complement/status`

These remain infrastructure configuration values. `Application` uses provider-agnostic contracts only.

## Cancellation rules
Required persisted evidence:
- stamped `PaymentComplementDocument`
- persisted `PaymentComplementStamp`
- complement UUID on the persisted stamp

Reason-code rules:
- `CancellationReasonCode` is required
- reason `01` requires `ReplacementUuid`
- non-`01` reasons do not use `ReplacementUuid`

Duplicate-cancellation prevention:
- already cancelled complements return conflict
- complements not in an eligible stamped lifecycle return conflict

Lifecycle handling:
- before provider call: `CancellationRequested`
- success: `Cancelled`
- provider rejection: `CancellationRejected`
- provider unavailable: local document returns to `Stamped`, and cancellation evidence is stored with status `Unavailable`

## Latest-known external status strategy
The current MVP stores the latest-known external status on `PaymentComplementStamp`:
- `LastStatusCheckAtUtc`
- `LastKnownExternalStatus`
- `LastStatusProviderCode`
- `LastStatusProviderMessage`
- `LastStatusRawResponseSummaryJson`

This step stores the latest-known snapshot, not a full refresh history.

Lifecycle alignment:
- external `CANCELLED` aligns local status to `Cancelled`
- external active/stamped statuses align local status back to `Stamped` when local state was in a cancellation-related branch

## Security rules
The complement snapshot stores only secret references:
- `CertificateReference`
- `PrivateKeyReference`
- `PrivateKeyPasswordReference`

Resolved secret values are never persisted in:
- `PaymentComplementCancellation`
- `PaymentComplementStamp`
- API responses

The system also avoids persisting raw provider request bodies that may contain secrets. Only normalized provider metadata and redacted response summaries are kept.

## Deferred items
- multi-currency complement cancellation/query behavior
- advance-payment and unapplied-remainder lifecycle
- regrouping or splitting payment events
- full attempt-history persistence for complement cancellation and refresh

Operational deployment and support guidance for the completed MVP is documented in [025-mvp-operational-hardening.md](./025-mvp-operational-hardening.md).
