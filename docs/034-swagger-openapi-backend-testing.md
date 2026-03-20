# Swagger / OpenAPI Backend Testing

## Scope
This step adds Swagger / OpenAPI support for backend-only manual testing in safe non-production environments.

It is intended for:
- local backend debugging
- sandbox-ready endpoint exploration
- manual JWT-authenticated API testing

It does not replace:
- the Angular admin UI
- automated integration tests
- automated e2e tests
- controlled manual sandbox smoke tests

## Enabled environments
Swagger is enabled by default only in:
- `Development`
- `Local`
- `Sandbox`

Swagger is disabled by default in:
- `Production`
- any other environment not explicitly allowed above

Explicit override:
- `OpenApi:EnableSwagger`

Production recommendation:
- keep `OpenApi:EnableSwagger=false`
- only enable it deliberately for temporary controlled support/debug work

## Access
When enabled:
- Swagger UI: `/swagger`
- OpenAPI JSON: `/swagger/v1/swagger.json`

## JWT Bearer support
Swagger UI includes a Bearer token security definition.

Recommended manual flow:
1. call `POST /api/auth/login`
2. copy the returned JWT token
3. click `Authorize` in Swagger UI
4. enter `Bearer <token>`
5. call protected endpoints

No tokens, passwords, secrets, or PAC credentials are prefilled.

## Endpoint grouping
Swagger groups the current API under practical tags:
- `Auth`
- `Orders`
- `SalesOrders`
- `BillingDocuments`
- `FiscalDocuments`
- `AccountsReceivable`
- `PaymentComplements`
- `Catalogs`
- `Audit`

`Catalogs` includes:
- issuer profile
- fiscal receivers
- product fiscal profiles
- fiscal import preview/apply flows

## Safety rules
- no real PAC credentials in checked-in config
- no real passwords in examples
- no certificate/private-key material in examples
- no seeded JWT tokens in Swagger UI
- no production-by-default Swagger exposure

Swagger exposes the existing API surface only. It is not a separate privileged path.

## Intended use in this project
Swagger is useful for:
- validating login/auth flow
- manual endpoint troubleshooting
- inspecting request/response shapes
- controlled non-production backend testing when the frontend is not the fastest tool

Swagger is not the primary operator workflow once the admin UI is available.
