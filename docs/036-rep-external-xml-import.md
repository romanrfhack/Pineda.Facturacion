# REP externo por XML

## Objetivo

La Fase 3A introduce la importación de CFDI externos por XML como base fiscal confiable para operación futura de REP. Esta fase no registra pagos ni emite REP sobre documentos externos; sólo valida y persiste el snapshot fiscal.

## Modelo

Se introduce el agregado `ExternalRepBaseDocument` en [ExternalRepBaseDocument.cs](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Domain/Entities/ExternalRepBaseDocument.cs).

Campos principales:

- `Uuid`
- `CfdiVersion`
- `DocumentType`
- `Series`
- `Folio`
- `IssuedAtUtc`
- `IssuerRfc`
- `IssuerLegalName`
- `ReceiverRfc`
- `ReceiverLegalName`
- `CurrencyCode`
- `ExchangeRate`
- `Subtotal`
- `Total`
- `PaymentMethodSat`
- `PaymentFormSat`
- `ValidationStatus`
- `ValidationReasonCode`
- `ValidationReasonMessage`
- `SatStatus`
- `LastSatCheckAtUtc`
- `SourceFileName`
- `XmlContent`
- `XmlHash`
- `ImportedAtUtc`
- `ImportedByUserId`
- `ImportedByUsername`

Persistencia:

- tabla `external_rep_base_document`
- índice único por `uuid`
- índice por `xml_hash`

## API

### Importar XML

`POST /api/payment-complements/external-base-documents/import-xml`

Formato:

- `multipart/form-data`
- campo `file`

Outcomes:

- `200 OK`: documento aceptado o bloqueado con snapshot persistido
- `400 Bad Request`: XML rechazado por validación estructural/fiscal
- `409 Conflict`: UUID duplicado

Response:

- `outcome`
- `isSuccess`
- `externalRepBaseDocumentId`
- `validationStatus`
- `reasonCode`
- `reasonMessage`
- `errorMessage`
- `uuid`
- `issuerRfc`
- `receiverRfc`
- `paymentMethodSat`
- `paymentFormSat`
- `currencyCode`
- `total`
- `isDuplicate`

### Consultar detalle importado

`GET /api/payment-complements/external-base-documents/{externalRepBaseDocumentId}`

Devuelve el snapshot fiscal persistido y el estado local de validación/SAT.

## Reglas de validación 3A

El XML externo sólo se acepta si cumple:

- XML bien formado
- CFDI parseable
- `TipoDeComprobante = I`
- UUID presente en `TimbreFiscalDigital`
- emisor y receptor con RFC presentes
- `MetodoPago = PPD`
- `FormaPago = 99`
- `Moneda = MXN`
- subtotal y total válidos
- UUID no duplicado
- estatus SAT verificable como vigente

Reason codes soportados:

- `Accepted`
- `InvalidXml`
- `UnsupportedVoucherType`
- `MissingUuid`
- `MissingIssuerOrReceiver`
- `UnsupportedPaymentMethod`
- `UnsupportedPaymentForm`
- `UnsupportedCurrency`
- `InvalidTotals`
- `DuplicateExternalInvoice`
- `CancelledExternalInvoice`
- `ValidationUnavailable`

## Estados locales

`ValidationStatus`:

- `Accepted`: snapshot externo listo para fases 3B/4
- `Blocked`: snapshot persistido, pero no operable todavía
- `Rejected`: validación fallida; no se persiste snapshot

`SatStatus`:

- `Unknown`
- `Active`
- `Cancelled`
- `Unavailable`

## UX

La UI incorpora una tarjeta de importación XML en la sección de complementos:

- selección de archivo XML
- envío al endpoint de importación
- render estructurado del resultado aceptado, bloqueado, rechazado o duplicado

Referencia principal:

- [external-rep-base-document-import-card.component.ts](/home/romanrfhack/code/Pineda.Facturacion/frontend/src/app/features/payment-complements/components/external-rep-base-document-import-card.component.ts)

## Limitaciones de 3A

- no existe todavía bandeja unificada internos/externos
- no se registran pagos sobre facturas externas
- no se emite REP sobre facturas externas
- la moneda operativa soportada en esta fase es `MXN`
- si la validación SAT no confirma vigencia, el documento queda `Blocked`

## Follow-up

La Fase 3B debe usar `ExternalRepBaseDocument` como base para:

- listar documentos externos importados
- administrar su estado dentro de una bandeja unificada
- preparar el puente funcional hacia pagos y REP en Fase 4
