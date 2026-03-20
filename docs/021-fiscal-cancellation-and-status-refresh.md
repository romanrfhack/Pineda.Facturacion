# Fiscal Cancellation And Status Refresh

## Scope
This phase adds:
- fiscal cancellation
- latest-status refresh from the PAC/provider

Explicit non-goals:
- no payment complements
- no accounts receivable
- no SAT live catalog validation

## Why cancellation and query use persisted fiscal evidence
Cancellation and status refresh must run from persisted fiscal records only.

Required persisted inputs:
- `FiscalDocument` snapshot fields
- `FiscalStamp.UUID`
- persisted issuer RFC, receiver RFC, and total

Disallowed sources:
- `SalesOrder`
- `BillingDocument`
- live issuer/receiver/product master data

## Chosen provider operations for the MVP
Current FacturaloPlus operations:
- `POST /cfdi/cancel`
- `POST /cfdi/status`

These paths are adapter configuration values, not domain rules.

## Cancellation reason rules
- cancellation reason code is required
- reason `01` requires `ReplacementUuid`
- reasons `02`, `03`, and `04` do not use `ReplacementUuid` in the current MVP flow

## Duplicate-cancellation prevention
- `FiscalDocument.Status = Cancelled` blocks another cancellation attempt
- one current `FiscalCancellation` row per `FiscalDocument` is sufficient for the current MVP
- a retry after rejection updates that current record instead of creating a second current row

## Lifecycle handling
Cancellation:
1. service validates the fiscal document exists and is eligible
2. service validates stamped UUID evidence exists
3. service sets `FiscalDocument.Status = CancellationRequested`
4. provider cancellation is executed
5. on success:
   - `FiscalDocument.Status = Cancelled`
   - `FiscalCancellation.Status = Cancelled`
6. on provider rejection:
   - `FiscalDocument.Status = CancellationRejected`
   - `FiscalCancellation.Status = Rejected`
7. on provider unavailable/transport failure:
   - `FiscalDocument.Status` returns to `Stamped`
   - `FiscalCancellation.Status = Unavailable`

Status refresh:
1. service validates stamped UUID evidence exists
2. provider status query is executed
3. latest known external status is persisted on `FiscalStamp`
4. local `FiscalDocument.Status` is aligned when the provider confirms a stronger external state such as cancelled

## Latest-known external status strategy
The MVP stores the latest status snapshot on `FiscalStamp`:
- `LastStatusCheckAtUtc`
- `LastKnownExternalStatus`
- `LastStatusProviderCode`
- `LastStatusProviderMessage`
- `LastStatusRawResponseSummaryJson`

This is intentionally lean:
- enough for operational visibility
- enough to align local lifecycle status
- no separate status-history table yet

## Security and audit rules
- no raw secret-bearing request payload is persisted
- no certificate values, private keys, or passwords are stored in cancellation/query evidence
- provider request bodies remain inside infrastructure only
- persisted audit contains normalized codes/messages and redacted response summaries only

Operational deployment and support guidance for the completed MVP is documented in [025-mvp-operational-hardening.md](./025-mvp-operational-hardening.md).
