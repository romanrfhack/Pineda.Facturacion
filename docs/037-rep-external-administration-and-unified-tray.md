# REP 3B: administración externa y bandeja unificada

## Estrategia elegida

Se implementó la opción `A`:

- bandeja interna existente sin reescritura
- bandeja externa específica para CFDI importados
- bandeja unificada liviana por composición

La razón es reducir riesgo sobre el flujo interno ya operativo y exponer un modelo común sólo donde agrega valor real: listado, filtros y detalle contextual.

## Endpoints

Se mantienen:

- `GET /api/payment-complements/base-documents/internal`
- `GET /api/payment-complements/base-documents/internal/{fiscalDocumentId}`
- `GET /api/payment-complements/external-base-documents/{externalRepBaseDocumentId}`

Se agregan:

- `GET /api/payment-complements/base-documents/external`
- `GET /api/payment-complements/base-documents`

## Bandeja externa

`GET /api/payment-complements/base-documents/external`

Filtros:

- `page`
- `pageSize`
- `fromDate`
- `toDate`
- `receiverRfc`
- `query`
- `validationStatus`
- `eligible`
- `blocked`

Cada fila devuelve:

- `externalRepBaseDocumentId`
- `uuid`
- `series`
- `folio`
- `issuedAtUtc`
- `issuerRfc`
- `issuerLegalName`
- `receiverRfc`
- `receiverLegalName`
- `currencyCode`
- `total`
- `paymentMethodSat`
- `paymentFormSat`
- `validationStatus`
- `satStatus`
- `importedAtUtc`
- `operationalStatus`
- `isEligible`
- `isBlocked`
- `primaryReasonCode`
- `primaryReasonMessage`
- `availableActions`

## Bandeja unificada

`GET /api/payment-complements/base-documents`

Filtros:

- `page`
- `pageSize`
- `fromDate`
- `toDate`
- `receiverRfc`
- `query`
- `sourceType`
- `validationStatus`
- `eligible`
- `blocked`

Contrato común por fila:

- `sourceType`
- `sourceId`
- `fiscalDocumentId`
- `externalRepBaseDocumentId`
- `billingDocumentId`
- `uuid`
- `series`
- `folio`
- `issuedAtUtc`
- `issuerRfc`
- `issuerLegalName`
- `receiverRfc`
- `receiverLegalName`
- `currencyCode`
- `total`
- `paymentMethodSat`
- `paymentFormSat`
- `operationalStatus`
- `validationStatus`
- `satStatus`
- `outstandingBalance`
- `repCount`
- `isEligible`
- `isBlocked`
- `primaryReasonCode`
- `primaryReasonMessage`
- `availableActions`
- `importedAtUtc`

## Estados operativos

### Internos

Se conserva la evaluación REP ya existente:

- `Eligible`
- `Blocked`
- `Ineligible`

### Externos

Se introduce un estado operativo simple:

- `ReadyForNextPhase`: validado y con SAT vigente
- `Blocked`: validación o SAT bloquean operación futura
- `Imported`: importado pero todavía sólo en seguimiento

Esto no significa que el CFDI externo ya sea operable para pagos o REP en 3B. Sólo indica su preparación para Fase 4.

## UX

La página de complementos ahora queda organizada por pestañas:

- `Unificada`
- `Internos`
- `Externos`

Regla de operación:

- internos: detalle y flujo operativo completo vigente
- externos: detalle fiscal/operativo básico, sin pagos ni REP
- unificada: seguimiento común y detalle contextual, no reemplaza la operación interna avanzada

## Limitaciones de 3B

- no se registran pagos sobre externos
- no se emite REP sobre externos
- la unificada no sustituye el detalle operativo completo de internos
- los externos siguen limitados al snapshot importado y su validación SAT/local

## Puente hacia Fase 4

La Fase 4 debe reutilizar `ExternalRepBaseDocument` y el contrato común de bandeja para agregar:

- captura/aplicación de pagos externos
- preparación REP sobre externos
- timbrado REP sobre externos
