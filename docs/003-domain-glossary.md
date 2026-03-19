# Domain Glossary

## Core concepts

### Legacy Order
A source order that exists in the legacy ERP database.
It is never mutated by the new billing system.

### Snapshot
An immutable imported copy of the minimum data required to make billing decisions.
A snapshot is taken from the legacy system before billing and becomes the stable source for the new system.

### Import Record
The traceability record that links a legacy source document with its import attempt and result in the new system.

### Sales Order
The internal snapshot header representation of the imported legacy order.

### Sales Order Item
A line item that belongs to an imported Sales Order snapshot.

### Billing Document
The internal billable document generated from a Sales Order snapshot.
It is not the same thing as the fiscal CFDI result.

### Billing Document Item
A line item belonging to the internal Billing Document.

### Fiscal Document
The fiscal representation associated with a Billing Document.
It tracks UUID, PAC/SAT status, and fiscal lifecycle.

### Fiscal Stamp
The evidence generated when the PAC successfully stamps a CFDI.

### Fiscal Cancellation
The evidence and lifecycle of a CFDI cancellation attempt.

### PAC
Authorized certification provider used to stamp, cancel, and query CFDI state.

## Source of truth rules
- Sales truth comes from the legacy ERP.
- Billing and fiscal truth come from the new billing system after snapshot import.
- Billing must never be generated directly from mutable legacy reads after import.

## Initial status enums

### ImportStatus
- Pending
- Imported
- Failed

### SalesOrderStatus
- SnapshotCreated
- ReadyForBilling
- BillingInProgress
- Billed
- Cancelled

### BillingDocumentStatus
- Draft
- ReadyToStamp
- Stamping
- Stamped
- StampFailed
- Cancelled

### FiscalStatus
- Pending
- Stamped
- CancelPending
- Cancelled
- Error

## Initial operational terms

### Source Hash
A deterministic fingerprint of the imported legacy document content used to detect meaningful changes and support idempotency.
Exact algorithm is still pending definition.

### Idempotent Import
An import process that prevents duplicate creation of equivalent records for the same legacy source document.

### Replacement Flow
A future documented process that will allow controlled rebilling or substitution when business rules require it.
This flow is not defined yet.

## Notes
These terms are authoritative for naming and implementation unless replaced by a documented decision in the decision log.
