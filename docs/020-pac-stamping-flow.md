# PAC Stamping Flow

## Scope
This phase documents the stamping slice that was implemented before cancellation/status refresh.

Explicit non-goals in this step:
- no cancellation in this slice
- no SAT status refresh in this slice
- no payment complements
- no XML generation inside the local system

Later phases extend this with:
- cancellation
- status refresh

## Why FiscalDocument is the PAC source of truth
`FiscalDocument` and `FiscalDocumentItem` are the persisted fiscal snapshot.

PAC stamping must consume:
- fiscal header snapshot fields
- fiscal line SAT fields
- payment method/form/condition snapshot fields
- issuer operational references snapshotted from the selected issuer profile

PAC stamping must not reconstruct payload data from:
- `SalesOrder`
- `BillingDocument`
- live `FiscalReceiver`
- live `ProductFiscalProfile`
- live `IssuerProfile`

## Chosen payload mode for the MVP
Chosen mode:
- provider-specific JSON payload generation inside `Infrastructure.FacturaloPlus`

Reason:
- the repository does not yet contain a local XML generator or signature pipeline
- the application boundary must remain provider-agnostic
- JSON mode is sufficient to implement a real stamping transport slice while keeping XML/signature work deferred

Rule:
- the JSON payload builder is an infrastructure detail only
- `Application` works with normalized stamping request/result contracts

## Provider-agnostic boundary
`Application` owns:
- normalized stamp request shape
- normalized stamp result shape
- stamping use case orchestration

`Infrastructure.FacturaloPlus` owns:
- secret resolution
- HTTP transport
- provider DTOs
- JSON payload construction
- response normalization

## Secret resolution strategy
`FiscalDocument` snapshots only safe references:
- `PacEnvironment`
- `CertificateReference`
- `PrivateKeyReference`
- `PrivateKeyPasswordReference`

Actual secret values are resolved at runtime inside `Infrastructure.FacturaloPlus` through a secret-reference resolver.

Rules:
- no secret values are stored in `FiscalDocument`
- no secret values are returned by endpoints
- no raw secret-bearing provider request body is persisted
- request audit stores only a request hash plus redacted operational summaries

## Stamping lifecycle
Allowed input status:
- `ReadyForStamping`
- `StampingRejected` only when retry is explicitly requested

Status flow:
1. service validates `FiscalDocument` snapshot completeness
2. service sets `FiscalDocument.Status = StampingRequested`
3. provider call is executed
4. on success:
   - `FiscalDocument.Status = Stamped`
   - `FiscalStamp.Status = Succeeded`
5. on provider business rejection:
   - `FiscalDocument.Status = StampingRejected`
   - `FiscalStamp.Status = Rejected`
6. on transport/unavailability/config-resolution failure:
   - `FiscalDocument.Status` returns to `ReadyForStamping`
   - `FiscalStamp.Status` stores `Unavailable` or `ValidationFailed`

## What is persisted in FiscalStamp
Current persisted stamp evidence includes:
- provider name and operation
- provider request hash
- provider tracking id
- provider code and message
- UUID
- stamped timestamp
- stamped XML when returned by provider
- XML hash
- original string when returned
- QR text or URL when returned
- redacted response summary JSON
- normalized error code and message

## Duplicate-stamp prevention
- `FiscalDocument.Status = Stamped` blocks another stamping attempt
- `fiscal_stamp.fiscal_document_id` is unique for the current MVP
- a retry updates the current stamp evidence record instead of creating multiple current rows

## MXN-only assumption
Current stamping flow is `MXN`-only.

Rules:
- stamp request builder validates `FiscalDocument.CurrencyCode = MXN`
- non-`MXN` fiscal documents fail validation before the provider is called
- multi-currency remains deferred until commercial snapshot import can provide reliable exchange-rate data

Operational deployment and support guidance for the completed MVP is documented in [025-mvp-operational-hardening.md](./025-mvp-operational-hardening.md).
