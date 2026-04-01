# 041 - REP quick views operativas

## Resumen

Fase 2 del sprint `Operación y monitoreo REP` agrega vistas rápidas sobre las bandejas REP existentes sin introducir otra taxonomía operativa.

La implementación reutiliza:

- `alertCode`
- `severity`
- `nextRecommendedAction`
- `summaryCounts`

No se abrieron endpoints nuevos. Las vistas rápidas se resuelven con un nuevo filtro `quickView` sobre los endpoints existentes:

- `GET /api/payment-complements/base-documents/internal`
- `GET /api/payment-complements/base-documents/external`
- `GET /api/payment-complements/base-documents`

## Contrato

### Query param nuevo

- `quickView`

Valores soportados:

- `PendingStamp`
- `WithError`
- `Blocked`
- `AppliedPaymentWithoutStampedRep`
- `PendingRefresh`
- `Stamped`

### summaryCounts enriquecido

`summaryCounts` ahora también devuelve:

```json
{
  "quickViewCounts": [
    { "code": "PendingStamp", "count": 3 },
    { "code": "Blocked", "count": 2 }
  ]
}
```

Los conteos se calculan dentro del conjunto base actual de filtros de negocio y antes de aplicar `quickView`, `alertCode`, `severity` o `nextRecommendedAction`.

## Reglas de quick view

### PendingStamp

Entra si:

- `alertCode = PreparedRepPendingStamp`
- o `nextRecommendedAction = StampRep`

### WithError

Entra si:

- `alertCode = RepStampingRejected`
- o `alertCode = RepCancellationRejected`
- o existe alguna alerta con `severity = error`

### Blocked

Entra si:

- `alertCode = BlockedOperation`
- o `alertCode = CancelledBaseDocument`
- o `alertCode = ValidationBlocked`
- o `alertCode = UnsupportedCurrency`
- o `nextRecommendedAction = Blocked`
- o existe alguna alerta con `severity = critical`

### AppliedPaymentWithoutStampedRep

Entra si:

- `alertCode = AppliedPaymentsWithoutStampedRep`

### PendingRefresh

Entra si:

- `nextRecommendedAction = RefreshRepStatus`
- o `alertCode = SatValidationUnavailable`

### Stamped

Entra si:

- el documento tiene al menos un REP timbrado
- y no tiene alertas `warning`, `error` o `critical`

## UX

Las tres bandejas ahora muestran una fila superior de quick views:

- `Todos`
- `Pendientes de timbrar`
- `Con error`
- `Bloqueado`
- `Pago aplicado sin REP`
- `Pendientes de refresh`
- `Timbrado`

Al seleccionar una quick view:

- el grid se consulta con `quickView`
- se mantienen disponibles los filtros manuales
- el usuario puede volver a `Todos` sin limpiar el resto de filtros de negocio

## Limitaciones

- No hay refresh masivo todavía
- No hay timeline enriquecido todavía
- Las quick views son filtros preconfigurados, no páginas independientes
- Los conteos de quick views no representan categorías mutuamente excluyentes

## Siguiente fase

Fase 3 reutilizará esta base para refresh masivo de estatus REP, sin redefinir alertas ni acciones recomendadas.
