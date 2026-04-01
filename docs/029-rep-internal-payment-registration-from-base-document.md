# 029. REP interno: registro y aplicación de pagos desde documento base

## Objetivo
Fase 2A mueve la operación de pago al contexto del CFDI base interno REP.

Desde el detalle del documento base ahora se puede:
- capturar un pago
- registrarlo
- aplicarlo al CFDI actual sin pedir ids técnicos
- refrescar saldo, historial y snapshot operativo

## Estrategia elegida
Se implementó una operación unificada:

`POST /api/payment-complements/base-documents/internal/{fiscalDocumentId}/payments`

La operación:
- valida el CFDI base y su elegibilidad operativa actual
- crea el pago en CxC
- lo aplica por completo al `AccountsReceivableInvoice` resuelto por `fiscalDocumentId`
- refresca el snapshot operativo REP

No se pidió `accountsReceivableInvoiceId` ni `paymentId` en UI.

## Contrato API

Request:

```json
{
  "paymentDate": "2026-04-01",
  "paymentFormSat": "03",
  "amount": 500.00,
  "reference": "TRANS-123",
  "notes": "Pago parcial"
}
```

Response:
- `outcome`
- `isSuccess`
- `errorMessage`
- `warningMessages[]`
- `fiscalDocumentId`
- `accountsReceivableInvoiceId`
- `accountsReceivablePaymentId`
- `appliedAmount`
- `remainingBalance`
- `remainingPaymentAmount`
- `repOperationalStatus`
- `isEligible`
- `isBlocked`
- `eligibilityReason`
- `operationalState`
- `applications[]`

## Reglas operativas
El flujo 2A permite registrar pagos solo si el documento base:
- sigue `Eligible`
- no está cancelado
- tiene cuenta por cobrar operativa
- mantiene saldo pendiente
- sigue en `MXN`

Además:
- `amount` debe ser mayor a cero
- `amount` no puede exceder el saldo pendiente del CFDI
- el pago se aplica completo al documento base actual

En esta fase no se deja pago parcialmente sin aplicar desde esta UX.

## Reuso técnico
La operación unificada reutiliza:
- `CreateAccountsReceivablePaymentService`
- `ApplyAccountsReceivablePaymentService`
- `GetInternalRepBaseDocumentByFiscalDocumentIdService`

Con esto se preserva el flujo legado por `paymentId` y se evita refactor masivo de CxC.

## UI
La bandeja REP interna ahora agrega:
- acción `Registrar pago` por fila elegible
- botón `Registrar pago` dentro del detalle del CFDI
- formulario con fecha, forma SAT, monto, referencia y notas

Después de aplicar:
- se refresca el detalle
- se refresca la bandeja
- se actualizan `paymentHistory`, `paymentApplications`, saldo y snapshot operativo

## Limitaciones de Fase 2A
- todavía no se prepara ni timbra REP desde esta vista
- la aplicación es contra un solo CFDI base por operación
- no se soporta todavía multi-moneda
- no cubre facturas externas

## Follow-up
- Fase 2B: preparar y emitir REP desde el mismo contexto del documento base
- reutilizar el `paymentId` recién generado/aplicado como punto de extensión hacia la preparación del complemento
