# Legacy Integration Rules

## Purpose
Define the mandatory rules for reading data from the legacy ERP database and importing it into the new billing system.

## Core principle
The legacy MySQL database is read-only for the new system.

## Mandatory rules
1. The new system must never insert, update, delete, alter, or create objects in the legacy database.
2. All access to legacy data must happen through the Infrastructure.LegacyRead project.
3. The API layer must never query the legacy database directly.
4. The Application layer must depend on abstractions, not on SQL or MySQL-specific code.
5. Legacy data must be imported into the new system as snapshots before billing decisions are made.
6. Billing decisions must be based on imported snapshots, not on mutable live reads after import.
7. Each imported legacy order must be tracked with an idempotent import record.
8. The system must prevent duplicate billing attempts for the same legacy source document unless a documented replacement flow exists.
9. Imported data must preserve enough traceability to identify source system, source table, source document id, and import timestamp.
10. Any uncertainty in legacy field meaning must be documented explicitly before implementing transformations.

## Integration model
- Source of sales truth: legacy ERP
- Source of fiscal truth: new billing system
- Import style: controlled snapshot import
- Write target: new MySQL database only

## Minimum traceability requirements
Every imported order must preserve:
- Source system
- Source table
- Source document id
- Source hash or equivalent change fingerprint
- Import timestamp
- Import status
- Related billing document id when applicable

## Allowed architecture
- Infrastructure.LegacyRead may read legacy MySQL
- Infrastructure.BillingWrite may write new MySQL
- Application coordinates import use cases
- Api triggers use cases only

## Forbidden practices
- Writing to legacy tables
- Sharing write models with legacy tables
- Coupling business rules to raw SQL in endpoints
- Reusing legacy schema as the new domain model
- Billing directly from mutable legacy reads without snapshot persistence

## Open questions policy
If a legacy field or rule is unclear:
1. document the uncertainty,
2. avoid inventing a rule,
3. isolate the assumption,
4. request confirmation before hardening behavior.
