# Decision Log

## ADR-001 - Use Pragmatic Clean Architecture with Minimal APIs
- Date: 2026-03-18
- Status: Accepted

### Context
The new billing system must read from a legacy MySQL database without modifying it, persist its own write model in a separate MySQL database, and integrate with an external PAC for CFDI stamping and cancellation.

### Decision
Use Pragmatic Clean Architecture for separation of concerns and Minimal APIs for the HTTP layer.

### Consequences
- Business rules stay out of HTTP endpoints.
- Infrastructure concerns remain isolated.
- The system can evolve incrementally by use case.
- Codex can work with clearer project boundaries.

---

## ADR-002 - Legacy database is read-only
- Date: 2026-03-18
- Status: Accepted

### Context
The legacy ERP is still in operation and must continue working while the new billing system is being built.

### Decision
The new system will never write to the legacy database.

### Consequences
- All legacy access must be isolated in Infrastructure.LegacyRead.
- Billing decisions require imported snapshots.
- No triggers, schema changes, or write paths are allowed in legacy.

---

## ADR-003 - New billing database will be independent
- Date: 2026-03-18
- Status: Accepted

### Context
The new system requires its own model for snapshots, billing documents, fiscal documents, PAC responses, and audit trails.

### Decision
Create an independent MySQL database for the new billing system.

### Consequences
- The write model can be designed cleanly.
- Legacy schema limitations do not constrain the new system.
- Migration can proceed without disrupting current operations.

---

## ADR-004 - Billing uses snapshot-based import
- Date: 2026-03-18
- Status: Accepted

### Context
Legacy data may change between the moment an order is created and the moment it is billed.

### Decision
Import a snapshot of the legacy order and use that snapshot as the source for billing and CFDI generation.

### Consequences
- Billing decisions are stable and auditable.
- The system avoids mutable-read inconsistencies.
- Snapshot traceability becomes mandatory.

---

## ADR-005 - FacturaloPlus integration must be isolated
- Date: 2026-03-18
- Status: Accepted

### Context
The PAC provider may change details over time, and provider-specific contracts should not leak into the domain model.

### Decision
Keep FacturaloPlus isolated behind an abstraction defined in Application.

### Consequences
- Provider-specific payloads stay in infrastructure.
- Business logic remains portable.
- Testing becomes easier with mocks/fakes.

---

## ADR-006 - Initial import idempotency strategy
- Date: 2026-03-18
- Status: Accepted

### Context
The new billing system imports legacy orders into snapshot tables and must avoid duplicate imports while preserving traceability.

### Decision
For the MVP, each legacy order import will be identified by:
- source_system
- source_table
- source_document_id

A deterministic source_hash will also be generated from the imported order content.

If the same source document is imported again:
- if the source hash matches the existing imported record, the operation is considered idempotent;
- if the source hash differs, the operation must not silently overwrite prior imported data.

Replacement or re-import with changed source data is not defined yet and must be treated as a controlled future flow.

### Consequences
- Duplicate imports can be detected safely.
- Changed source documents will surface explicitly instead of being overwritten silently.
- The first import use case can be implemented with predictable behavior.
