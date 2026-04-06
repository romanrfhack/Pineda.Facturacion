# 044. REP Operational Attention Hooks

## Objetivo

Cerrar el sprint de Operación y monitoreo REP con una capacidad pequeña, útil y reusable para seguimiento operativo de alertas críticas, sin introducir infraestructura pesada de notificaciones.

## Estrategia elegida

Fase 5 se resuelve con una opción híbrida:

- listado operativo de documentos REP que requieren atención;
- contrato reusable de alertas notificables para futuro consumo por hooks reales.

No se agregan colas, jobs, push notifications ni persistencia nueva. La solución reutiliza la taxonomía operativa y los read models ya existentes.

## Alertas notificables de Fase 5

En esta fase califican como alertas de atención/notificación:

- `RepStampingRejected`
- `RepCancellationRejected`
- `SatValidationUnavailable`
- `BlockedOperation`
- `CancelledBaseDocument`

Criterios:

- deben ser accionables;
- deben explicar claramente por qué el documento requiere atención;
- deben conservar `nextRecommendedAction`;
- no deben confundirse con el timeline histórico.

## Endpoint operativo

Se agrega:

- `GET /api/payment-complements/attention-items`

Filtros soportados:

- paginación;
- rango de fechas;
- RFC receptor;
- búsqueda general;
- origen (`Internal` / `External`);
- código de alerta;
- severidad;
- acción recomendada.

La respuesta mantiene:

- `summaryCounts` reutilizando la misma lógica operativa de conteos;
- `items` con contexto mínimo del documento;
- `attentionAlerts` con la proyección reusable para hooks futuros.

## Contrato reusable

Cada item expone una lista `attentionAlerts`. Cada alerta incluye:

- `alertCode`
- `severity`
- `title`
- `message`
- `hookKey`

`hookKey` es la clave estable para integraciones futuras. En Fase 5 se definen:

- `rep.stamping-rejected`
- `rep.cancellation-rejected`
- `rep.sat-validation-unavailable`
- `rep.cancelled-base-document`
- `rep.blocked-operation`

Este contrato no representa una notificación emitida; representa un candidato notificable derivado del estado operativo actual.

## UX de operación

La bandeja/hub REP agrega una vista `Atención` orientada a soporte y operación.

La vista:

- concentra documentos internos y externos que requieren atención;
- resalta severidad y acción recomendada;
- permite filtrar por alerta crítica;
- permite navegar rápidamente al detalle del documento;
- muestra los hooks candidatos y el timeline reciente para dar contexto.

## Relación con timeline y alertas operativas

- Las alertas operativas siguen siendo el origen de la clasificación.
- El timeline sigue siendo historial cronológico.
- La vista de atención es una proyección de documentos actualmente accionables.

No se duplica historia ni se introduce un subsistema nuevo.

## Limitaciones de Fase 5

- No hay emisión real de correos, webhooks o push notifications.
- No hay histórico de “alerta enviada” o “alerta atendida”.
- Los attention items se recalculan al consultar, igual que el resto de los read models operativos.
- La prioridad se basa en severidad y recencia, no en SLA persistido.

## Backlog post-sprint sugerido

1. Registrar `attention acknowledgements` o asignación operativa por documento.
2. Agregar hooks reales de salida para `hookKey` críticos con feature flag.
3. Incorporar umbrales/SLA operativos sobre documentos en atención.
4. Añadir filtros rápidos por “sin atender” y “atendidos recientemente”.
5. Evaluar persistencia liviana sólo si la operación requiere trazabilidad de entrega o acuse.
