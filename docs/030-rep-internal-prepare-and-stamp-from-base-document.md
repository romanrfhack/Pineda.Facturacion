# 030. REP interno: preparar y timbrar desde documento base

## Objetivo

Completar el flujo operativo REP interno desde el contexto del CFDI base:

1. registrar/aplicar pago
2. preparar REP
3. timbrar REP

Sin obligar al usuario a cambiar al flujo legado centrado en `paymentId`.

## Endpoints

### Preparar REP desde documento base

`POST /api/payment-complements/base-documents/internal/{fiscalDocumentId}/prepare`

Request:

```json
{
  "accountsReceivablePaymentId": 123
}
```

Notas:

- `accountsReceivablePaymentId` es opcional.
- Si no se envía, el backend intenta resolver el primer pago aplicado al CFDI que todavía no tenga REP ligado.
- Si el pago ya tiene REP preparado, se reutiliza el complemento existente y la respuesta regresa `AlreadyPrepared`.

Response principal:

- `outcome`
- `isSuccess`
- `errorMessage`
- `warningMessages[]`
- `fiscalDocumentId`
- `accountsReceivablePaymentId`
- `paymentComplementDocumentId`
- `status`
- `relatedDocumentCount`
- `operationalState`

### Timbrar REP desde documento base

`POST /api/payment-complements/base-documents/internal/{fiscalDocumentId}/stamp`

Request:

```json
{
  "paymentComplementDocumentId": 456,
  "retryRejected": false
}
```

Notas:

- `paymentComplementDocumentId` es opcional.
- Si no se envía, el backend intenta resolver el primer REP del CFDI con estado `ReadyForStamping` o `StampingRejected`.
- Si el REP ya estaba timbrado, se reutiliza la evidencia existente y la respuesta regresa `AlreadyStamped`.

Response principal:

- `outcome`
- `isSuccess`
- `errorMessage`
- `warningMessages[]`
- `fiscalDocumentId`
- `accountsReceivablePaymentId`
- `paymentComplementDocumentId`
- `status`
- `paymentComplementStampId`
- `stampUuid`
- `stampedAtUtc`
- `xmlAvailable`
- `operationalState`

## Reglas operativas aplicadas

### Preparar REP

Se bloquea cuando:

- el CFDI no es de ingreso
- el CFDI está cancelado o en cancelación
- el CFDI no está vigente para REP
- no tiene UUID fiscal persistido
- no usa `MetodoPago = PPD`
- no usa `FormaPago = 99`
- no está en `MXN`
- no existe cuenta por cobrar operativa
- no existe pago aplicado elegible sobre el documento base

### Timbrar REP

Se bloquea cuando:

- el CFDI está cancelado o en cancelación
- el CFDI no está en `MXN`
- no existe REP preparado elegible para timbrar

## UX

La página `payment-complement-base-documents-page` queda como flujo principal interno:

- registrar pago desde el detalle del CFDI
- preparar REP desde la fila del pago aplicado
- timbrar REP desde la fila del complemento preparado
- refrescar resumen, historial y snapshot operativo después de cada paso

El flujo legado por `paymentId` se conserva como compatibilidad.

## Limitaciones de Fase 2B

- no cubre facturas externas
- no unifica todavía internos y externos
- no elimina el flujo legado
- no agrega cancelación/refresco SAT desde la bandeja nueva
- sigue limitado al flujo interno en `MXN`

## Base lista para Fase 3A

La capa nueva deja separadas dos cosas:

- la UX operativa por documento base
- el motor legado de REP por `paymentId`

Eso permite reutilizar la misma estrategia para facturas externas en 3A+, cambiando únicamente la resolución del documento base y la validación de origen.
