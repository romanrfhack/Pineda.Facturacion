# REP 4: pagos y emisión de REP sobre facturas externas

## Estrategia elegida

Se implementó la opción `A` para ledger externo:

- reutilizar `AccountsReceivableInvoice`
- reutilizar `AccountsReceivablePayment`
- reutilizar `AccountsReceivablePaymentApplication`
- introducir soporte de origen externo por `ExternalRepBaseDocumentId`

La razón es evitar duplicar el ledger que ya soporta saldos, aplicaciones y parcialidades para REP interno.

Sobre timbrado REP se mantuvo la misma decisión:

- reutilizar `PreparePaymentComplementService`
- reutilizar `StampPaymentComplementService`
- agregar una capa orientada a `ExternalRepBaseDocument`

## Cambios de modelo

### Accounts receivable

`AccountsReceivableInvoice` ahora puede anclarse a:

- flujo interno: `BillingDocumentId`, `FiscalDocumentId`, `FiscalStampId`
- flujo externo: `ExternalRepBaseDocumentId`

Regla:

- un CFDI externo importado aceptado puede tener una cuenta por cobrar propia
- esa cuenta por cobrar se crea on-demand al registrar el primer pago

### Payment complement related documents

`PaymentComplementRelatedDocument` ahora soporta dos anclas:

- relacionada a CFDI interno por `FiscalDocumentId` y `FiscalStampId`
- relacionada a CFDI externo por `ExternalRepBaseDocumentId`

Esto permite seguir usando el mismo documento REP y el mismo motor PAC sin crear un segundo agregado de complemento.

## Endpoints

Se agregan:

- `POST /api/payment-complements/base-documents/external/{externalRepBaseDocumentId}/payments`
- `POST /api/payment-complements/base-documents/external/{externalRepBaseDocumentId}/prepare`
- `POST /api/payment-complements/base-documents/external/{externalRepBaseDocumentId}/stamp`

Se mantiene:

- `GET /api/payment-complements/external-base-documents/{externalRepBaseDocumentId}`
- `GET /api/payment-complements/base-documents/external`
- `GET /api/payment-complements/base-documents`

## Flujo operativo externo

### 1. Registrar pago

Request:

```json
{
  "paymentDate": "2026-04-07",
  "paymentFormSat": "03",
  "amount": 116.00,
  "reference": "TRANS-EXT-4-1001",
  "notes": "Pago total sobre CFDI externo"
}
```

Response útil:

- `outcome`
- `accountsReceivableInvoiceId`
- `accountsReceivablePaymentId`
- `appliedAmount`
- `remainingBalance`
- `remainingPaymentAmount`
- `repOperationalStatus`
- `isEligible`
- `isBlocked`
- `eligibilityReason`
- `applications[]`

Outcomes principales:

- `RegisteredAndApplied`
- `NotFound`
- `Conflict`
- `ValidationError`

### 2. Preparar REP

Request opcional:

```json
{
  "accountsReceivablePaymentId": 123
}
```

Response útil:

- `outcome`
- `paymentComplementDocumentId`
- `accountsReceivablePaymentId`
- `status`
- `relatedDocumentCount`
- `repOperationalStatus`

Outcomes principales:

- `Prepared`
- `AlreadyPrepared`
- `NotFound`
- `Conflict`
- `ValidationError`

### 3. Timbrar REP

Request opcional:

```json
{
  "paymentComplementDocumentId": 456
}
```

Response útil:

- `outcome`
- `paymentComplementDocumentId`
- `status`
- `paymentComplementStampId`
- `stampUuid`
- `stampedAtUtc`
- `xmlAvailable`
- `repOperationalStatus`

Outcomes principales:

- `Stamped`
- `AlreadyStamped`
- `NotFound`
- `Conflict`
- `ProviderRejected`
- `ProviderUnavailable`

## Reglas operativas externas

Un CFDI externo sólo puede operar REP si:

- `validationStatus = Accepted`
- `satStatus = Active`
- `documentType = I`
- `paymentMethodSat = PPD`
- `paymentFormSat = 99`
- `currencyCode = MXN`
- existe un `IssuerProfile` activo cuyo RFC coincide con el emisor
- existe `FiscalReceiver` activo para el RFC receptor

Bloqueos explícitos:

- `Rejected`
- `Duplicate`
- `Blocked`
- SAT no vigente
- monto mayor al saldo
- monto menor o igual a cero
- intento de preparar REP sin pago aplicado

## Estados operativos externos

- `ReadyForPayment`
- `ReadyForRepPreparation`
- `ReadyForRepStamping`
- `RepIssued`
- `Blocked`

Acciones disponibles:

- `ViewDetail`
- `RegisterPayment`
- `PrepareRep`
- `StampRep`

## UX

La pestaña `Externos` conserva el patrón del flujo interno:

- resumen fiscal
- resumen operativo
- historial de pagos
- aplicaciones
- REP emitidos
- acciones contextuales

La bandeja unificada sigue siendo de seguimiento. La operación externa completa se ejecuta desde el detalle externo, no desde la bandeja unificada.

## Diferencias respecto al flujo interno

Similitudes:

- mismo patrón de pantalla
- mismo ledger de pagos/aplicaciones
- mismo motor de preparación y timbrado REP
- mismo concepto de historial y estado operativo

Diferencias:

- el documento base es `ExternalRepBaseDocument`
- la cuenta por cobrar interna se materializa on-demand
- el ancla del REP relacionado puede ser externa
- la elegibilidad depende también de validación SAT/importación

## Limitaciones de Fase 4

- sigue limitado a `MXN`
- no hay conciliación bancaria ni automatización avanzada
- la operación externa depende de que emisor y receptor existan en catálogos activos de la plataforma
- cancelación/refresh avanzado del REP sigue concentrado en el flujo legado del complemento ya emitido

## Puente hacia Fase 5

La Fase 5 debe enfocarse en:

- seguimiento avanzado y alertas
- operación cruzada internos/externos más rica
- refresh/cancelación REP desde la experiencia unificada
- trazabilidad operativa y monitoreo de excepciones
