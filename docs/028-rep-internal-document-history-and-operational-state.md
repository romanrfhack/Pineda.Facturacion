# 028. REP interno: historial por factura y snapshot operativo

## Objetivo
Fase 1B fortalece el seguimiento por CFDI base interno dentro de la bandeja REP.

El foco de esta fase es explicabilidad operativa:
- mostrar por qué un CFDI es `Eligible`, `Blocked` o `Ineligible`
- exponer historial de pagos, aplicaciones y REP emitidos
- persistir solo señales operativas de alto valor

## Estrategia elegida
Se implementó un modelo híbrido.

Se sigue computando al vuelo:
- historial detallado de pagos registrados
- aplicaciones de pago
- REP emitidos y su contexto
- elegibilidad actual del CFDI

Se persiste un snapshot operativo corto en `internal_rep_base_document_state`:
- `lastEligibilityEvaluatedAtUtc`
- `lastEligibilityStatus`
- `lastPrimaryReasonCode`
- `lastPrimaryReasonMessage`
- `repPendingFlag`
- `lastRepIssuedAtUtc`
- `repCount`
- `totalPaidApplied`

La intención del snapshot no es reemplazar el ledger transaccional, sino dejar trazabilidad operativa barata y reusable para fases posteriores.

## Contrato del detalle enriquecido

### GET `/api/payment-complements/base-documents/internal/{fiscalDocumentId}`
Devuelve:
- `summary`
- `operationalState`
- `paymentHistory`
- `paymentApplications`
- `issuedReps`

### `summary.eligibility`
Incluye:
- `status`
- `primaryReasonCode`
- `primaryReasonMessage`
- `evaluatedAtUtc`
- `secondarySignals[]`

### `paymentHistory[]`
Incluye por pago relacionado:
- `accountsReceivablePaymentId`
- `paymentDateUtc`
- `paymentFormSat`
- `paymentAmount`
- `amountAppliedToDocument`
- `remainingPaymentAmount`
- `reference`
- `notes`
- `paymentComplementId`
- `paymentComplementStatus`
- `paymentComplementUuid`
- `createdAtUtc`

### `paymentApplications[]`
Incluye por parcialidad aplicada:
- `accountsReceivablePaymentId`
- `applicationSequence`
- `paymentDateUtc`
- `paymentFormSat`
- `appliedAmount`
- `previousBalance`
- `newBalance`
- `reference`
- `notes`
- `paymentAmount`
- `remainingPaymentAmount`
- `createdAtUtc`

### `issuedReps[]`
Incluye por REP ligado al CFDI:
- `paymentComplementId`
- `accountsReceivablePaymentId`
- `status`
- `uuid`
- `paymentDateUtc`
- `issuedAtUtc`
- `stampedAtUtc`
- `cancelledAtUtc`
- `providerName`
- `installmentNumber`
- `previousBalance`
- `paidAmount`
- `remainingBalance`

## UI
El detalle del CFDI base ahora se organiza en cuatro bloques:
- resumen fiscal
- resumen operativo
- explicación de elegibilidad
- snapshot operativo persistido

Y tres bloques de historial:
- pagos registrados
- aplicaciones de pago
- REP emitidos y relacionados

## Regla operativa visible
La elegibilidad sigue centralizada en `InternalRepBaseDocumentEligibilityRule`, pero ahora la respuesta expone:
- motivo principal estructurado
- señales secundarias satisfechas o faltantes
- estado persistido de la última evaluación útil para operación

## Limitaciones de Fase 1B
- todavía no se registran pagos desde la bandeja
- todavía no se emite REP desde la bandeja
- el historial depende de los movimientos ya persistidos en CxC y complementos
- la persistencia de snapshot se actualiza en operaciones de consulta de bandeja/detalle
- el soporte operativo interno sigue limitado a `MXN`

## Follow-up
- Fase 2A: captura y aplicación de pagos desde el contexto del documento base
- Fase 2B: preparación y emisión de REP desde la bandeja
- Fase 3A: modelo equivalente para facturas externas importadas por XML
