# 027. REP interno: modelo operativo y bandeja base

## Objetivo
Fase 1A introduce una bandeja operativa para CFDI internos candidatos a REP sin mover todavía la emisión del complemento a esa nueva UX.

La unidad operativa deja de ser el `paymentId` y pasa a ser el CFDI base interno.

## Alcance de Fase 1A
- Read model operativo para CFDI internos REP.
- Regla computable y centralizada de elegibilidad.
- Bandeja con filtros, estados y contexto básico.
- Detalle por `fiscalDocumentId` con pagos aplicados y REP relacionados.
- Protección operativa para que CFDI cancelados no se marquen como elegibles.

## Contratos API

### GET `/api/payment-complements/base-documents/internal`
Filtros soportados:
- `page`
- `pageSize`
- `fromDate`
- `toDate`
- `receiverRfc`
- `query`
- `eligible`
- `blocked`
- `withOutstandingBalance`
- `hasRepEmitted`

Respuesta:
- paginada
- cada fila incluye `fiscalDocumentId`, `billingDocumentId`, `salesOrderId`, `accountsReceivableInvoiceId`, `uuid`, `series`, `folio`, receptor, total, pagado, saldo, estatus fiscal, estatus operativo REP, conteo de pagos y conteo de REP emitidos

### GET `/api/payment-complements/base-documents/internal/{fiscalDocumentId}`
Devuelve:
- `summary`
- `paymentApplications`
- `paymentComplements`

## Regla de elegibilidad REP interna
La regla quedó centralizada en `InternalRepBaseDocumentEligibilityRule`.

Un CFDI interno queda `Eligible` si:
- `DocumentType = I`
- `FiscalStatus = Stamped` o `CancellationRejected`
- UUID timbrado persistido
- `PaymentMethodSat = PPD`
- `PaymentFormSat = 99`
- `CurrencyCode = MXN`
- existe `AccountsReceivableInvoice`
- saldo pendiente mayor a cero
- no hay inconsistencia de saldo

Queda `Blocked` si:
- está cancelado
- tiene cancelación en proceso
- no existe cuenta por cobrar operativa
- la cuenta por cobrar está cancelada
- la moneda no es soportada en el flujo actual
- el saldo operativo es inconsistente

Queda `Ineligible` si:
- no es CFDI de ingreso
- no está realmente timbrado/vigente para REP
- no tiene UUID persistido
- no usa `PPD`
- no usa `99`
- ya no tiene saldo pendiente

## UI
La ruta `/app/payment-complements` ahora se comporta así:
- sin `paymentId`: muestra la nueva bandeja REP interna
- con `paymentId`: conserva el flujo puntual existente

La bandeja muestra:
- filtros
- grid operativo
- estado `Eligible`, `Blocked` o `Ineligible`
- motivo operativo
- modal de contexto con pagos aplicados y REP relacionados

## Protección adicional en flujo legado
`PreparePaymentComplementService` dejó de aceptar CFDI cancelados como documentos base válidos para preparar un REP.

## Limitaciones de Fase 1A
- la bandeja no emite REP todavía
- no registra pagos desde la bandeja todavía
- no importa facturas externas
- sigue dependiendo de `AccountsReceivableInvoice` para saldo interno confiable
- el soporte operativo actual sigue limitado a `MXN`

## Follow-up
- Fase 1B: historial más rico por factura y persistencia explícita de criterios operativos
- Fase 2A: registrar pagos y aplicar desde la bandeja
- Fase 2B: disparar preparación/emisión de REP desde el contexto del documento base
