# REP enriched timeline (Fase 4)

## Objetivo

Fase 4 agrega un timeline enriquecido al detalle operativo de documentos base REP internos y externos.

El timeline:

- se construye como read model cronológico derivado
- reutiliza timestamps y estados ya persistidos
- no introduce event sourcing
- no agrega tablas nuevas
- no reemplaza alertas operativas ni snapshots

## Endpoints enriquecidos

No se abrieron endpoints nuevos.

Se enriquecieron los endpoints de detalle existentes:

- `GET /api/payment-complements/base-documents/internal/{fiscalDocumentId}`
- `GET /api/payment-complements/external-base-documents/{externalRepBaseDocumentId}`

Ambos ahora incluyen el bloque `timeline`.

## Contrato

Cada entrada del timeline expone:

- `eventType`
- `occurredAtUtc`
- `sourceType`
- `severity`
- `title`
- `description`
- `status`
- `referenceId`
- `referenceUuid`
- `metadata`

El timeline se entrega ordenado cronológicamente por `occurredAtUtc`.

Cuando dos eventos comparten el mismo timestamp, el orden secundario es semántico para preservar lectura operativa:

1. importación / validación
2. registro y aplicación de pago
3. preparación / timbrado / refresh REP
4. cancelación REP

## Catálogo de tipos de evento

Fase 4 usa los siguientes `eventType`:

- `ExternalXmlImported`
- `ExternalValidationAccepted`
- `ExternalValidationBlocked`
- `PaymentRegistered`
- `PaymentApplied`
- `RepPrepared`
- `RepStamped`
- `RepStatusRefreshed`
- `RepStampingRejected`
- `RepCancellationRequested`
- `RepCancelled`
- `RepCancellationRejected`
- `SatValidationUnavailable`

## Fuentes derivadas

El timeline reutiliza información ya existente en:

- `AccountsReceivablePayment`
- `AccountsReceivablePaymentApplication`
- `PaymentComplementDocument`
- `PaymentComplementStamp`
- `PaymentComplementCancellation`
- `ExternalRepBaseDocument`

## Alertas vs timeline

Alertas operativas y timeline coexisten, pero no son lo mismo.

Alertas operativas:

- representan el estado actual del documento para operación
- alimentan filtros, semáforos, quick views y siguiente acción recomendada
- pueden existir sin que haya un evento puntual nuevo

Timeline:

- representa hechos cronológicos derivados del flujo REP
- prioriza trazabilidad y soporte
- no redefine severidades ni taxonomía operativa

## Limitaciones de Fase 4

- No se inventan timestamps.
- Si un evento no puede derivarse con fecha confiable desde el estado persistido, se omite.
- `BaseDocumentBlocked` no se materializa todavía como evento dedicado porque el modelo actual no conserva una transición histórica confiable del bloqueo; hoy el bloqueo sigue representado por el estado operativo y sus alertas.
- En CFDI externos, `ExternalValidationAccepted` y `ExternalValidationBlocked` reutilizan `ImportedAtUtc` porque la validación se deriva del mismo flujo de importación y no existe un timestamp histórico separado para esa validación.
- El timeline se reconstruye al consultar el detalle; no se persiste como historia propia.

## Base para Fase 5

Fase 5 podrá apoyarse en este timeline para disparar hooks o notificaciones sobre eventos críticos ya derivables, por ejemplo:

- `RepStampingRejected`
- `RepCancellationRejected`
- `SatValidationUnavailable`

La intención es reutilizar el mismo catálogo y contrato antes de introducir nuevas salidas operativas.
