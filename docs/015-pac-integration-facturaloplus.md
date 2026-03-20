# PAC Integration - FacturaloPlus

## Purpose
Define the recommended integration strategy for FacturaloPlus without coupling the domain or application core to one provider-specific payload format.

## FacturaloPlus capabilities summary
At the system-documentation level, the integration must support:
- stamping CFDI documents
- stamping CFDI payment complements
- cancelling stamped CFDI documents
- cancelling stamped CFDI payment complements
- querying fiscal status
- querying payment-complement status
- returning normalized provider results and raw evidence references

Provider-specific implementation details must remain inside `Infrastructure.FacturaloPlus`.

## Recommended adapter abstraction
`Application` should define a PAC abstraction such as:
- stamp fiscal document
- cancel fiscal document
- query fiscal document status

The abstraction should use normalized request/response contracts, not provider DTOs.

Rules:
- `Infrastructure.FacturaloPlus` translates normalized requests into FacturaloPlus-specific payloads
- `Application` never depends on FacturaloPlus transport, auth, or raw provider models
- `Api` never talks to FacturaloPlus directly

## Preferred production path
Preferred path:
1. Local system creates and persists `FiscalDocument` snapshot.
2. Application validates that issuer, receiver, and product fiscal data are complete.
3. Adapter generates provider payload from the persisted local fiscal snapshot.
4. PAC request is sent.
5. Provider response is normalized and persisted locally.

Disallowed path:
- reconstructing fiscal payloads directly from `SalesOrder` or ad hoc request data at the last moment

## Supported stamping modes
The architecture must remain open to multiple provider payload modes at the documentation level:
- XML mode
- JSON mode
- TXT mode

Rules:
- Domain entities must not encode one provider mode as a core business rule
- payload mode selection belongs to the adapter/integration configuration
- normalized application contracts should remain provider-agnostic

Current MVP choice:
- JSON payload generation inside `Infrastructure.FacturaloPlus`

Reason:
- local XML generation/signing is not implemented yet
- the system can still execute real stamping from persisted `FiscalDocument` snapshots without leaking provider DTOs into `Application`

## Required cancellation and query operations
The system requires these PAC-facing operations:
- stamp fiscal snapshot
- cancel stamped fiscal document with SAT motive code
- query UUID/fiscal status
- refresh local fiscal status from provider/SAT-facing query results

Expected normalized outputs:
- success/failure flag
- provider operation id or tracking id
- provider code
- provider message
- normalized fiscal status
- raw response reference
- timestamps

## Response/result normalization strategy
Provider responses must be normalized before the rest of the system consumes them.

Recommended normalized result shape:
- `OperationName`
- `Succeeded`
- `NormalizedStatus`
- `ProviderCode`
- `ProviderMessage`
- `ProviderTrackingId`
- `RawRequestReference`
- `RawResponseReference`
- `OccurredAtUtc`

Rules:
- Local status transitions must consume normalized results only
- raw provider payloads are persistence/audit artifacts, not the primary business contract
- normalization must be deterministic and testable
- current MVP uses configured FacturaloPlus endpoints for:
  - `POST /cfdi/stamp`
  - `POST /cfdi/cancel`
  - `POST /cfdi/status`

## Secret-handling rules
Never place real secrets in source-controlled docs or code examples.

Use placeholders only, for example:
- `FACTURALOPLUS_API_KEY`
- `FACTURALOPLUS_API_SECRET`
- `PAC_USERNAME`
- `PAC_PASSWORD`
- `CSD_CERTIFICATE_REFERENCE`
- `CSD_PRIVATE_KEY_REFERENCE`
- `CSD_PRIVATE_KEY_PASSWORD_REFERENCE`

Rules:
- Secrets must come from external configuration or secret stores
- docs may show placeholder names only
- certificate/key references should point to secure external storage, not inline blobs
- PAC adapters must receive resolved secrets through configuration/secret providers, not hardcoded literals

## Immediate implementation direction
- Define normalized PAC contracts in `Application`
- Implement FacturaloPlus adapter in `Infrastructure.FacturaloPlus`
- Persist normalized request/response audit records in BillingWrite
- Keep stamping, cancellation, and query flows driven from persisted fiscal snapshots
- Current implemented slice is stamping only; cancellation and status refresh remain deferred

Current implemented state:
- stamping is implemented
- cancellation is implemented
- latest-status refresh is implemented
- payment-complement stamping is implemented from persisted payment-complement snapshots
- payment-complement cancellation is implemented from persisted complement evidence
- payment-complement latest-status refresh is implemented from persisted complement evidence

Operational deployment and support guidance for the completed MVP is documented in [025-mvp-operational-hardening.md](./025-mvp-operational-hardening.md).
