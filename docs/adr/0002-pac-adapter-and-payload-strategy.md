# Title
PAC Adapter And Payload Strategy

## Status
Accepted

## Context
The system must integrate with FacturaloPlus but should remain portable and avoid leaking provider-specific payload rules into Domain or Application.

## Decision
Define PAC contracts in Application and implement FacturaloPlus as an infrastructure adapter. The adapter may support XML, JSON, or TXT provider payload modes, but payload generation must always start from a persisted local FiscalDocument snapshot and normalized application contracts.

## Consequences
- Provider-specific DTOs stay isolated in Infrastructure.FacturaloPlus.
- Application remains independent from one PAC transport or payload mode.
- PAC result normalization becomes a required integration concern.
- Future PAC replacement is less disruptive.
