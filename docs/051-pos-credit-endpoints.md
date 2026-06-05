# POS credit endpoints

Los endpoints POS bajo `/api/pos` exponen una superficie mínima para descubrimiento de receptores y validación operativa de venta a crédito, sin abrir el workspace completo de cuentas por cobrar al cliente POS.

## Authorization

Los tres endpoints POS Credit requieren la policy `PosCreditRead`.

La policy exige:

- Usuario autenticado
- Claim `scope=pos.credit.read` o `permission=pos.credit.read`
- Temporalmente también acepta los roles operativos internos mínimos `Admin`, `FiscalSupervisor` y `FiscalOperator`

## Endpoints

`GET /api/pos/receivers/search?term={term}`

Endpoint de descubrimiento mínimo. Solo devuelve receptores activos y únicamente:

- `fiscalReceiverId`
- `rfc`
- `legalName`

Reglas:

- `term` es requerido
- Se aplica `Trim()`
- `term` debe tener al menos 3 caracteres
- El máximo de resultados es 20
- No devuelve montos, saldos ni flags de crédito

`GET /api/pos/receivers/{fiscalReceiverId}/credit-status`

Devuelve el snapshot operativo de crédito para un receptor activo:

- `fiscalReceiverId`
- `rfc`
- `legalName`
- `creditEnabled`
- `approvedCreditLimitAmount`
- `pendingBalanceTotal`
- `overdueBalanceTotal`
- `currentBalanceTotal`
- `availableCreditAmount`
- `openInvoicesCount`
- `overdueInvoicesCount`
- `canSellOnCredit`
- `blockReason`

Los montos de crédito y saldo solo se consultan aquí, no en `search`.

`POST /api/pos/receivers/{fiscalReceiverId}/credit-check`

Valida si una venta puede aprobarse contra la línea de crédito del receptor activo.

Request:

```json
{
  "saleAmount": 3000.00,
  "currencyCode": "MXN"
}
```

Response aprobada:

```json
{
  "approved": true,
  "availableCreditAmount": 5208.00,
  "saleAmount": 3000.00,
  "remainingCreditAmount": 2208.00,
  "blockReason": null
}
```

## Reglas operativas

- Solo receptores activos son operables desde POS
- `search` no devuelve receptores inactivos
- `credit-status` y `credit-check` devuelven `404` si el receptor no existe o está inactivo
- `availableCreditAmount = approvedCreditLimitAmount - pendingBalanceTotal`
- `CREDIT_DISABLED` bloquea
- `NO_APPROVED_CREDIT` bloquea
- `INSUFFICIENT_CREDIT` bloquea
- `overdueBalanceTotal` solo informa; todavía no bloquea por sí solo

## Currency

Por ahora solo se soporta `MXN`.

Reglas:

- `currencyCode` es requerido
- `currencyCode` debe ser `MXN`
- `saleAmount` debe ser mayor que `0`
- No se realizan cálculos multi-moneda en este patch

## Error codes

Errores `400` funcionales:

- `TERM_TOO_SHORT`: el término de búsqueda tiene menos de 3 caracteres después de `Trim()`
- `UNSUPPORTED_CURRENCY`: `currencyCode` es nulo, vacío o distinto de `MXN`
- `INVALID_SALE_AMOUNT`: `saleAmount` es menor o igual a `0`

Bloqueos operativos reportados en `credit-status` y/o `credit-check`:

- `CREDIT_DISABLED`
- `NO_APPROVED_CREDIT`
- `INSUFFICIENT_CREDIT`

## CORS

- `Production` no permite `*`
- `Development`, `Sandbox` y `Testing` pueden permitir orígenes dev configurados
- No se hardcodea dominio productivo; producción debe configurar `Cors:Pos:Origins`
