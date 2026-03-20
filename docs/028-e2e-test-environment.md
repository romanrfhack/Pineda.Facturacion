# E2E Test Environment

## Scope
This document defines the automated browser test strategy for the Angular operations UI.

The purpose of this step is reliability first, then focused operational journey coverage. Automated e2e validates a small set of real browser journeys while detailed domain permutations remain covered by backend integration tests and frontend unit tests.

## Chosen architecture
The automated e2e flow uses:
- Angular frontend started by Playwright through the local dev server
- browser-driven UI interactions through Playwright
- mocked backend HTTP responses at the browser boundary for deterministic execution

This means automated e2e does **not**:
- start the .NET backend
- call real PAC services
- require real secrets
- depend on external connectivity

## Why this strategy was chosen
The frontend already had a valid Playwright spec, but the previous execution failed because the Playwright `webServer` startup flow was not reliable in the current environment.

The concrete failure was that the Angular dev server could not bind to the configured local port when started inside the sandboxed execution environment. The observed error was an `EPERM` listen failure on `127.0.0.1:4200`.

The test itself was not the root problem. The unstable part was process startup and port binding.

Using mocked backend responses keeps the browser flow deterministic and removes ambiguity about:
- backend boot order
- PAC provider behavior
- test credentials
- database seed state

## Frontend startup
Playwright starts the frontend automatically with:
- `npm run start:e2e`

This resolves to:
- `ng serve --host 127.0.0.1 --port 4200 --configuration development`

The Playwright base URL is:
- `http://127.0.0.1:4200`

## Backend strategy for automated e2e
Automated e2e does not start the backend.

Instead, Playwright intercepts the frontend's HTTP calls and returns deterministic responses for:
- login
- session restore
- order import
- billing document creation
- issuer lookup
- fiscal receiver search
- fiscal document preparation
- fiscal document readback
- invoice stamping
- AR invoice creation
- payment creation and payment application
- payment-complement preparation and stamping
- audit and evidence reads

This keeps automated UI tests stable while backend behavior continues to be validated by existing .NET integration tests.

## Fake PAC behavior
There are no real PAC calls in automated e2e.

PAC-facing UI behavior is validated only through deterministic mocked provider outcomes. Automated e2e does not use live FacturaloPlus traffic.

Manual sandbox or provider smoke tests remain a separate operational activity.

## Test user and deterministic data
The suite uses deterministic mocked data such as:
- `supervisor` / `Secret123!`
- `operator` / `Secret123!`
- `auditor` / `Secret123!`
- known ids like `LEG-7001`, `LEG-7101`, fiscal document `401`, payment `702`

These values are test-only fixtures returned by the Playwright mocks. They are not real production credentials and are not used by the backend runtime.

Expanded convention:
- each major browser journey owns a dedicated scenario mock helper
- some journeys start from a deterministic pre-stamped or pre-applied state instead of recreating every earlier step
- specs must not depend on previous specs having run

## Commands
Local commands:
- `npm run e2e`
- `npm run e2e:headed`
- `npm run e2e:debug`
- `npm run e2e:ci`

Useful support commands:
- `npm run start:e2e`
- `npx playwright test --list`

## CI suitability
The current setup is CI-friendly because it:
- uses a fixed frontend port
- uses Playwright-managed web server startup
- avoids external dependencies
- avoids real PAC traffic
- avoids interactive manual setup

CI still needs:
- Node/npm
- Playwright browsers installed
- permission to bind the local frontend port

## Automated e2e scope vs manual smoke tests
Automated e2e should cover:
- login and session restore behavior
- route protection
- key browser-level operational happy paths
- visible status and validation handling
- safe evidence and XML viewing
- a small number of role and provider guardrails

Manual smoke tests should cover:
- real backend deployment configuration
- real database migrations
- real PAC sandbox/provider connectivity
- production-like timeout and retry behavior

## Troubleshooting
If `npm run e2e` fails before opening the browser:
- verify `npm install` completed in `frontend/`
- run `npx playwright test --list`
- run `npm run start:e2e` directly
- confirm the machine allows binding `127.0.0.1:4200`

If Playwright reports `webServer` startup failure:
- inspect whether Angular dev server failed to bind the port
- check for an existing process already using port `4200`
- ensure the execution environment allows local server processes

If the app opens but the test fails:
- keep traces enabled and inspect the Playwright trace
- verify the mocked endpoint URLs still match the frontend API clients
- update the test helper if the UI route or button labels change

## Current limitations
- automated e2e still mocks backend HTTP rather than running a full backend test host
- cancellation and status-refresh browser journeys are not yet covered end-to-end
- some role/forbidden cases still rely more heavily on component and integration tests than on browser tests
- no manual sandbox/provider smoke test is automated here
