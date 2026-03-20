# Title
Receivers And Product Fiscal Profiles

## Status
Accepted

## Context
Fiscal generation requires stable receiver information and SAT product mappings. Recomputing or guessing these values on each fiscal operation is error-prone and not auditable.

## Decision
Store receiver master data locally and make it searchable by RFC with autocomplete support. Store SAT mappings in a dedicated local product fiscal profile model. Fiscal snapshot generation must fail explicitly when mandatory receiver or product fiscal data is missing.

## Consequences
- RFC lookup becomes a local business capability.
- SAT mappings become reusable and auditable.
- The system avoids silent inference of fiscal data.
- Excel import and validation workflows can target explicit local master data models.
