# Reintento REP y correos del receptor (2026-10-16)

## Cambios

- La pantalla de operaciones por pago envía `retryRejected: true` únicamente para `StampingRejected`, con la acción **Reintentar timbrado**.
- Evita preparar de nuevo un complemento existente y bloquea solicitudes simultáneas. Recarga estado y evidencia incluso si el timbrado responde con un error HTTP.
- El límite conjunto de correos del receptor pasa de 200 a 500 caracteres en Angular, validación de alta/edición y MySQL. Incluye los separadores `; `.
- No se modifican pagos, aplicaciones, saldos ni las validaciones del backend contra timbrados duplicados. El rechazo original del pago #84 aún requiere evidencia del proveedor.

## Despliegue de producción

La migración `20261016164158_ExpandFiscalReceiverEmailTo500` amplía únicamente `fiscal_receiver.email` a `varchar(500)` y conserva su nulabilidad/charset.

El workflow de backend publica y ejecuta `ops/ReceiverEmailMigration` antes de reemplazar la API. Lee la conexión BillingWrite del entorno del proceso activo de `facturas-api`; no imprime ni pasa credenciales como argumentos. Requiere acceso sudo al runtime .NET y permisos de ALTER para la conexión de la aplicación.

El ejecutor rechaza la operación si hay otras migraciones pendientes, si no puede identificar el proceso/conexión, o si la columna tiene una capacidad inesperada. Aplica la migración EF y consulta `information_schema` para verificar la capacidad. Si ya fue aplicada, solo verifica. Cualquier fallo detiene el despliegue antes de reemplazar la API.

El frontend de producción se despliega tras el éxito del backend. La cadena automática es **CI → backend/migración → frontend**. No se habilitan migraciones automáticas al arrancar la API.

## Validación

- Frontend: primer timbrado, reintento, bloqueo de duplicados, errores HTTP y correos de 300/499/500/501 caracteres.
- Backend: alta y edición de receptor, y protecciones existentes de REP.
- Integración MySQL: migración desde el esquema previo, conservación de correos, persistencia de 500 caracteres, segunda ejecución idempotente y rechazo ante otras migraciones pendientes.

## Comprobación operativa

Después de verificar ambos despliegues, recargar el navegador y abrir el pago #84. Pulsar **Reintentar timbrado** una vez. Si el proveedor vuelve a rechazarlo, conservar la captura de **Evidencia de timbrado del complemento**, incluyendo código y mensaje; no preparar otro complemento.

En el receptor afectado, agregar el cuarto correo y guardar, respetando los 500 caracteres conjuntos. No se envían correos ni se timbran documentos como parte del despliegue.

## Reversión

La ampliación de columna es compatible con la versión anterior. Ante un rollback de código, conservar la columna en 500 caracteres. No ejecutar `Down` automáticamente: reducirla a 200 podría rechazar o truncar valores que ya superen ese límite.
