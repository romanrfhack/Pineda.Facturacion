# Title
Fiscal Source Of Truth

## Status
Accepted

## Context
The system already imports immutable SalesOrder snapshots and creates BillingDocuments from them. The next fiscal phase needs a clear rule for which model drives PAC interactions and fiscal lifecycle updates.

## Decision
The local system is the source of truth. PAC integration must consume a persisted FiscalDocument snapshot generated from BillingDocument data and fiscal master data. The system must never reconstruct fiscal payloads directly from SalesOrder on demand at PAC call time.

## Consequences
- Fiscal generation is auditable and reproducible.
- BillingDocument and FiscalDocument have distinct responsibilities.
- PAC calls can be retried from persisted fiscal state.
- Late changes in sales or master data do not silently alter an in-flight fiscal payload.
