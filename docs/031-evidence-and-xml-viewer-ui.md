# Evidence and XML Viewer UI

## Scope
This step adds safe read-only evidence visibility to the Angular admin UI for:
- stamped fiscal documents
- stamped payment complements

It does not add new fiscal business flows.

## Where evidence appears
Evidence is surfaced inside the existing operational pages:
- `/app/fiscal-documents/:id`
- `/app/payment-complements?paymentId={id}`

The UI keeps evidence close to the lifecycle screen that produced it instead of creating a detached evidence module.

## Role-aware behavior
Evidence visibility follows the existing protected operational routes:
- `Admin`: read access
- `FiscalSupervisor`: read access
- `FiscalOperator`: read access where the existing operational route already allows it
- `Auditor`: read access

Backend authorization remains the final source of truth.

## Endpoints consumed
Fiscal documents:
- `GET /api/fiscal-documents/{fiscalDocumentId}`
- `GET /api/fiscal-documents/{fiscalDocumentId}/stamp`
- `GET /api/fiscal-documents/{fiscalDocumentId}/stamp/xml`

Payment complements:
- `GET /api/accounts-receivable/payments/{paymentId}/payment-complement`
- `GET /api/payment-complements/{paymentComplementId}/stamp`
- `GET /api/payment-complements/{paymentComplementId}/stamp/xml`

## Summary vs detail vs XML
Summary cards show:
- UUID
- provider name
- provider tracking id or XML hash
- stamped timestamp
- provider message or error message

Detail panels show safe secondary fields only:
- stamp id
- provider operation
- provider tracking id
- QR text or URL
- original string
- created and updated timestamps

XML is a separate explicit action:
- not shown by default
- loaded only on demand
- rendered in a secondary monospaced viewer
- closed explicitly by the operator

## Safety rules
The UI does not show:
- secrets
- password references
- JWTs
- raw provider request payloads
- internal exception dumps

The XML endpoints are read-only and return only persisted XML content. Main evidence metadata stays on the normal stamp GET responses.

## Testing
This step adds:
- typed API service tests for XML retrieval
- component/page tests for evidence summary, empty state, detail, and XML viewer behavior
- a stable Playwright flow for logging in, opening a stamped fiscal document, and viewing XML

## Deferred work
- download action for XML
- dedicated evidence deep-link routes
- richer syntax highlighting or XML formatting
- evidence search across multiple documents
- operator-friendly diff/history views for restamps or future multi-attempt models
