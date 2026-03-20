# Expanded E2E Journeys

## Scope
This step expands automated browser coverage for the operational fiscal UI without adding new business flows.

Automated e2e still uses deterministic mocked backend responses at the browser boundary. It does not call a real PAC provider.

## Journeys now covered
- login and role-aware shell entry
- legacy order import -> billing document creation -> fiscal document preparation
- full invoice stamping journey with visible stamp evidence
- stamped fiscal evidence and explicit XML viewing
- AR invoice creation -> payment creation -> payment application
- payment complement preparation -> payment complement stamping
- audit viewer read flow
- catalogs receiver create/search

## Guardrail and negative coverage
- operator sees read-only fiscal-document actions
- provider unavailable feedback is shown for invoice stamping
- mixed-receiver validation is surfaced during payment-complement preparation
- empty not-stamped evidence state is shown before stamp evidence exists

## Deterministic starting states
Not every browser journey recreates the entire fiscal lifecycle from the beginning.

For reliability, some tests start from explicit deterministic mocked states:
- AR/payment journey starts from a known stamped fiscal document id
- payment-complement journey starts from a known payment event ready for complement preparation
- evidence and audit journeys start from known persisted operational states

This keeps individual specs short and avoids hidden inter-test coupling.

## Why real PAC is not used in automated e2e
- automated browser tests should validate operator behavior, not external provider availability
- real PAC traffic would make the suite slower, less deterministic, and harder to run in CI
- real sandbox validation remains a separate manual smoke-test activity after automated coverage passes

## Helper conventions
Current helpers are intentionally lightweight:
- `LoginPage` for sign-in
- scenario-specific mocked backend helpers under `frontend/e2e/support/`
- each spec owns its own deterministic scenario setup

The suite avoids a heavy page-object framework.

## Manual sandbox readiness
The expanded browser suite now covers the main operator paths and several high-value guardrails. That gives a safer baseline before running manual sandbox smoke tests against a real backend and PAC sandbox.

## Deferred e2e work
- invoice cancellation browser journey
- payment-complement cancellation and refresh browser journeys
- fuller forbidden-route coverage across all roles
- larger read-only evidence browsing scenarios
- optional backend-hosted deterministic browser runs in CI, if later needed
