# Fiscal Architecture

## Purpose
Define the agreed fiscal architecture that evolves the current BillingDocument foundation into a full CFDI-capable backend without coupling the system to one PAC or to mutable legacy reads.

## Architecture overview
The fiscal path starts only after a SalesOrder snapshot already exists and a BillingDocument has been created from that snapshot.

High-level flow:
1. Legacy ERP order is imported into `SalesOrder` + `SalesOrderItem`.
2. Application creates `BillingDocument` + `BillingDocumentItem` from the imported snapshot.
3. Application generates a persisted `FiscalDocument` snapshot from the BillingDocument plus fiscal master data.
4. PAC integration consumes the persisted FiscalDocument snapshot and produces stamping or cancellation results.
5. PAC results are persisted into `FiscalStamp`, `FiscalCancellation`, and normalized status fields on `FiscalDocument`.
6. Accounts receivable and payment applications are created from persisted fiscal evidence and later become the source for payment-complement snapshots.
7. Payment complements are prepared from persisted payment events, payment applications, and stamped invoice evidence, then stamped through the PAC adapter.
8. Payment-complement cancellation and status refresh operate from persisted complement snapshots and persisted complement stamp evidence only.

## Source-of-truth rules
- Legacy ERP is the source of sales origin only.
- `SalesOrder` is the source of truth for imported commercial snapshot data.
- `BillingDocument` is the source of truth for the internal billable document derived from the snapshot.
- `FiscalDocument` is the source of truth for the fiscal snapshot sent to the PAC.
- `FiscalStamp` is the source of truth for successful stamping evidence.
- `FiscalCancellation` is the source of truth for cancellation attempts and outcomes.
- `PaymentComplementDocument` is the source of truth for payment-complement lifecycle.
- `PaymentComplementStamp` is the source of truth for successful payment-complement stamping evidence.
- `PaymentComplementCancellation` is the source of truth for payment-complement cancellation attempts and outcomes.
- PAC responses must enrich persisted fiscal records, not replace them as the primary source of truth.

## Separation of responsibilities

### SalesOrder
- Immutable imported sales snapshot.
- Mirrors the validated commercial data from legacy.
- Must not be reconstructed from PAC responses.
- Must not be mutated to represent fiscal lifecycle.

### BillingDocument
- Internal billing projection generated from exactly one SalesOrder.
- Represents the commercial-to-billable transition.
- Holds internal numbering, payment condition, currency semantics, and line data needed before fiscal generation.
- Is not the fiscal CFDI result.
- Should not be treated as the primary fiscal stamping/cancellation lifecycle owner.
- In the current MVP, BillingDocument commercial currency semantics are limited to `MXN` until import snapshots carry a reliable exchange rate.

### FiscalDocument
- Persisted fiscal snapshot derived from BillingDocument plus issuer/receiver/product fiscal master data.
- Must be complete enough to generate PAC payloads without re-reading SalesOrder or live master data assumptions.
- Holds the primary fiscal lifecycle state for stamping, cancellation, and SAT/PAC status refresh.
- Remains the main operational record for fiscal transitions after BillingDocument creation.

### FiscalDocumentItem
- Persisted line-level fiscal snapshot derived from BillingDocumentItem plus ProductFiscalProfile.
- Stores SAT product/service code, SAT unit code, tax object code, VAT rate, and line amounts needed by later PAC payload generation.
- Must be created together with FiscalDocument as part of one atomic fiscal snapshot.
- Prevents later PAC integration from reconstructing line SAT data from live master tables.

### FiscalStamp
- Stores successful stamping artifacts and evidence.
- Includes stamped XML, tracking identifiers, seals, certificate metadata or serial data, and provider raw response references.
- Exists only after a successful PAC stamping outcome.
- In the current MVP one current `FiscalStamp` row per `FiscalDocument` is sufficient, and retries update that current evidence record.
- Also stores the latest known external PAC/SAT-facing status snapshot in the current MVP.

### FiscalCancellation
- Stores cancellation intent, reason code, optional replacement UUID, and provider/SAT outcomes.
- Represents the cancellation lifecycle separately from the original FiscalDocument snapshot content.
- In the current MVP one current `FiscalCancellation` row per `FiscalDocument` is sufficient.

## Recommended PAC boundary
The PAC boundary belongs in `Infrastructure.FacturaloPlus`.

Rules:
- `Application` defines PAC abstractions and normalized request/response contracts.
- `Infrastructure.FacturaloPlus` translates between normalized application contracts and FacturaloPlus-specific payload formats.
- PAC payload generation must start from persisted `FiscalDocument` snapshots.
- The PAC adapter must not query legacy sales data directly.
- HTTP endpoints must never build PAC payloads.

## Recommended payment / AR boundary
Accounts receivable and payment tracking belong outside the PAC adapter boundary.

Rules:
- Payment and receivable records are local business data owned by the billing system.
- Payment events drive payment complement eligibility.
- Receivable invoices are created from persisted stamped fiscal evidence, not from live master data queries.
- Payment application rows are the authoritative local source for outstanding balance.
- Fiscal payment complements are a downstream fiscal consequence of local payment applications.
- PAC integration only consumes normalized complement-ready fiscal snapshots; it does not own receivable logic.
- Current MVP creates one payment complement per payment event and requires all related invoices to belong to the same receiver.

## High-level state transitions

### BillingDocument
- `Draft` -> initial internal creation state
- `ReadyToStamp` -> fiscally complete and approved for PAC call
- `Stamping` -> PAC request in progress
- `Stamped` -> PAC stamping completed successfully
- `StampFailed` -> PAC stamping failed and requires correction/retry
- `Cancelled` -> billing document cancelled through fiscal workflow

### FiscalDocument
- `Draft` -> incomplete internal fiscal draft if a future phase intentionally supports incomplete persistence
- `ReadyForStamping` -> fiscal snapshot created and complete enough for later PAC stamping
- `StampingRequested` -> provider call has been initiated
- `Stamped` -> CFDI stamped successfully
- `StampingRejected` -> provider rejected the current stamping attempt
- `CancellationRequested` -> provider cancellation is in progress
- `Cancelled` -> cancellation completed successfully
- `CancellationRejected` -> provider rejected the current cancellation attempt

### Cancellation flow
1. User or process requests cancellation with motive code.
2. System creates `FiscalCancellation` intent record.
3. PAC adapter executes cancellation against the persisted fiscal identity.
4. System normalizes result locally.
5. `FiscalDocument` and `BillingDocument` statuses are updated consistently from normalized results.

## Operational boundaries
- Fiscal snapshot generation must fail fast when issuer, receiver, or product fiscal master data is incomplete.
- Secret material such as PAC credentials or CSD files must never be embedded in domain entities or docs.
- Payload mode selection such as XML, JSON, or TXT is an adapter concern, not a domain concern.
- Later credit sales and payment complements must extend the fiscal pipeline without rewriting the BillingDocument foundation.
- Current MVP AR creation is intentionally limited to credit-sale `PPD` fiscal documents; cash / `PUE` invoices remain outside the receivable ledger.
- Current MVP payment complements are also limited to `MXN` and one persisted complement per payment event.

## Immediate implementation direction
- Build fiscal persistence and use cases around `BillingDocument -> FiscalDocument`.
- Add master data before PAC calls.
- Treat PAC integration as a pure adapter over persisted fiscal snapshots.
- Treat `FiscalDocumentItem` as part of the fiscal foundation, not as a later optimization.
- Keep BillingDocument as the commercial source of truth for currency/exchange-rate semantics used by FiscalDocument preparation.
- Assume `MXN`-only currency semantics for the current MVP until legacy import and SalesOrder snapshot are extended with trustworthy exchange-rate data.
- Execute stamping from persisted `FiscalDocument` snapshots only, with secret references resolved inside infrastructure at call time.
- Execute cancellation and status refresh from persisted `FiscalDocument` and `FiscalStamp` evidence only.
