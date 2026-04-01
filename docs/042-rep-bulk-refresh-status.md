# Fase 3 - Refresh masivo de estatus REP

## Objetivo

Agregar refresh masivo de estatus REP sobre las bandejas operativas existentes sin introducir jobs, colas ni otra taxonomía operativa.

## Endpoints

- `POST /api/payment-complements/base-documents/internal/refresh-rep-status/bulk`
- `POST /api/payment-complements/base-documents/external/refresh-rep-status/bulk`
- `POST /api/payment-complements/base-documents/refresh-rep-status/bulk`

## Request

Todos los endpoints aceptan un request común:

```json
{
  "mode": "Selected",
  "documents": [
    { "sourceType": "Internal", "sourceId": 123 },
    { "sourceType": "External", "sourceId": 456 }
  ],
  "fromDate": "2026-04-01",
  "toDate": "2026-04-30",
  "receiverRfc": "BBB010101BBB",
  "query": "UUID-REP-1",
  "sourceType": "External",
  "validationStatus": "Accepted",
  "eligible": true,
  "blocked": false,
  "withOutstandingBalance": true,
  "hasRepEmitted": false,
  "alertCode": "SatValidationUnavailable",
  "severity": "warning",
  "nextRecommendedAction": "RefreshRepStatus",
  "quickView": "PendingRefresh"
}
```

### Reglas

- `mode = Selected`
  - usa `documents`
  - permite mezcla de internos y externos sólo en la ruta unificada
- `mode = Filtered`
  - ignora `documents`
  - resuelve el conjunto objetivo a partir de los filtros actuales de la bandeja
- límite máximo por operación: `50` documentos

## Response

```json
{
  "isSuccess": true,
  "errorMessage": null,
  "mode": "Filtered",
  "maxDocuments": 50,
  "totalRequested": 3,
  "totalAttempted": 3,
  "refreshedCount": 1,
  "noChangesCount": 1,
  "blockedCount": 0,
  "failedCount": 1,
  "items": [
    {
      "sourceType": "Internal",
      "sourceId": 123,
      "attempted": true,
      "outcome": "NoChanges",
      "message": "Sin cambios operativos. Estado externo actual: VIGENTE.",
      "paymentComplementDocumentId": 7001,
      "paymentComplementStatus": "Stamped",
      "lastKnownExternalStatus": "VIGENTE",
      "updatedState": {
        "operationalStatus": "RepIssued",
        "isEligible": true,
        "isBlocked": false,
        "primaryReasonMessage": "El CFDI cuenta con REP timbrado.",
        "nextRecommendedAction": "RefreshRepStatus",
        "alerts": [
          {
            "code": "StampedRepAvailable",
            "severity": "info",
            "message": "El CFDI ya cuenta con REP timbrado y sólo requiere seguimiento o refresh de estatus."
          }
        ]
      }
    }
  ]
}
```

## Outcomes por documento

- `Refreshed`
  - el refresh terminó y el estatus del complemento cambió
- `NoChanges`
  - el refresh terminó correctamente pero el estatus del complemento no cambió
- `Blocked`
  - el documento no tiene un REP elegible para refresh
- `Failed`
  - el refresh no pudo ejecutarse o el documento seleccionado no es válido

## UX operativa

- Las bandejas interna, externa y unificada permiten:
  - seleccionar filas visibles
  - refrescar seleccionados
  - refrescar el conjunto filtrado actual
- La UI muestra:
  - cantidad seleccionada
  - resumen agregado de la operación
  - resultado por documento
- Después de ejecutar:
  - el grid se recarga
  - los contadores y quick views se recalculan
  - si el detalle abierto pertenece a un documento refrescado, se recarga también

## Limitaciones de Fase 3

- no existe cancelación masiva
- no hay procesamiento en background
- el resultado `NoChanges` sigue siendo éxito operativo; sólo indica que el estatus del REP no cambió
- el refresh masivo no reemplaza el refresh individual; ambos conviven

## Preparación para Fase 4

Fase 4 puede reutilizar la misma selección/bulk toolbar para timeline enriquecido y refrescar el detalle con eventos adicionales sin cambiar este contrato.
