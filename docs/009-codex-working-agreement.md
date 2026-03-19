# Codex Working Agreement

## Objective
Implement the new billing backend incrementally, without touching the legacy ERP write path.

## Mandatory rules
1. Read the /docs folder before making structural changes.
2. Do not write to the legacy MySQL database.
3. All business rules must live in Domain or Application, never inside HTTP endpoints.
4. Endpoints must stay thin and only orchestrate request/response.
5. Do not duplicate logic across endpoints, services, repositories, or integration clients.
6. Any important architectural change must be reflected in docs/010-decision-log.md.
7. Prefer explicit use cases over generic service classes with mixed responsibilities.
8. Infrastructure must depend only on Application and Domain, never the other way around.
9. Keep FacturaloPlus isolated behind an abstraction defined in Application.
10. Persist enough audit data for every PAC call.
11. Before adding a new package, explain why it is necessary.
12. For every implemented feature, add or update tests.
13. Do not infer undocumented business rules silently; document uncertainty explicitly.
14. Never move business rules into Program.cs.
15. Never connect the new write model directly to legacy tables for mutation.
16. Use snapshots for billing decisions; do not depend on mutable reads after import.
17. Update documentation when introducing new flows, constraints, or models.

## Coding expectations
- Keep classes small and focused.
- Prefer explicit names over ambiguous names.
- Avoid god services.
- Avoid premature abstractions.
- Favor readability and maintainability over cleverness.
- Keep integration code isolated from domain rules.
- Use cancellation tokens in async flows where applicable.

## Delivery expectations
- Propose small, reviewable changes.
- Explain touched files before large refactors.
- Preserve architectural boundaries.
- Call out risks when modifying persistence, import flow, or PAC integration.
