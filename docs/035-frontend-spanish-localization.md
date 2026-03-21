# Frontend Spanish Localization

## Scope
This step normalizes operator-facing frontend text to Spanish across the secured Angular application.

It covers:
- shell navigation
- page titles and section headings
- buttons, labels, placeholders, and helper text
- confirmation prompts
- operational feedback and empty/error states
- audit and evidence viewers
- status and outcome display labels

This step does not add new business features and does not change backend contracts.

## What was translated
- authentication flow
  - `Iniciar sesión`, `Cerrar sesión`, auth and session messages
- operational areas
  - `Órdenes`
  - `Documentos fiscales`
  - `Cuentas por cobrar`
  - `Complementos de pago`
  - `Catálogos`
  - `Auditoría`
  - `Evidencia` / `Ver XML`
- forms and actions
  - create/update/save/search/import/preview/apply/stamp/cancel/refresh actions
- cards, tables, and summaries
  - batch summaries
  - audit details
  - stamp evidence summaries
  - payment application summaries

## Style decisions
- Spanish-first, concise, operational wording
- neutral Mexican Spanish
- back-office terminology over consumer wording

Chosen terminology:
- `Orden` instead of `Pedido`
- `Estatus` for operational lifecycle/status displays
- `Razón social` instead of `Nombre legal`

## Terms intentionally kept as acronyms or technical identifiers
These remain unchanged where they are meaningful to operators:
- RFC
- UUID
- XML
- SAT
- PAC
- CFDI
- IVA
- CSD
- JWT

Technical identifiers coming directly from operational/audit data, such as `FiscalDocument.Stamp`, remain visible as-is when they represent audit action codes rather than friendly UI labels.

## Display mappings
A small frontend display-label helper was added to avoid exposing raw English enum/status values directly in the UI.

Mapped categories include:
- roles
- fiscal document and complement statuses
- cancellation statuses
- AR invoice statuses
- import row statuses and suggested actions
- operation outcomes
- import apply modes

Implementation:
- [display-labels.ts](/home/romanrfhack/code/Pineda.Facturacion/frontend/src/app/shared/ui/display-labels.ts)

## Notes on backend-provided messages
The UI now normalizes a small set of common backend error messages when they are surfaced directly to operators, for example:
- `Forbidden` -> `Acceso denegado.`
- `Unauthorized` -> `No autorizado.`
- `Not found` -> `No encontrado.`
- `Invalid credentials` -> `Credenciales inválidas.`

This keeps the frontend Spanish-first without requiring backend contract changes.

## Deferred localization work
- full multilingual i18n is still out of scope
- some audit action/entity codes remain technical identifiers by design
- provider-originated free-text messages may still appear in their original language if the backend persists them verbatim and they are not part of the small normalized set
