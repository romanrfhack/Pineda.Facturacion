# Loader global del frontend

## Propósito

El frontend dispone de un loader global bloqueante con identidad de Auto Refacciones Pineda para comunicar al usuario que una operación sigue en curso y evitar acciones simultáneas o dobles clics mientras el sistema procesa información.

Esta guía es una **regla de desarrollo del frontend**. Al agregar un nuevo flujo HTTP o un proceso asíncrono perceptible, se debe decidir explícitamente qué feedback de espera corresponde: loader global, loader local/skeleton o ninguno.

## Componentes principales

- `frontend/src/app/core/ui/global-loader.service.ts`
  - Estado global basado en handles, no en un booleano simple.
  - Soporta operaciones concurrentes.
  - Expone `begin()`, `track()` y snapshots públicos sin filtrar datos internos.
- `frontend/src/app/core/ui/pineda-loader-overlay.component.ts`
  - Overlay visual bloqueante.
  - Mensaje contextual, animación institucional y accesibilidad.
- `frontend/src/app/core/http/interceptors/global-loader.interceptor.ts`
  - Integra el loader con `HttpClient`.
  - Evita parpadeos con retardo antes de mostrar y permanencia mínima una vez visible.
- `frontend/src/app/core/http/global-loader-context.tokens.ts`
  - `GLOBAL_LOADER_OPTIONS`: opt-in/configuración por petición.
  - `SKIP_GLOBAL_LOADER`: opt-out para trabajo silencioso.
  - `createGlobalLoaderContext(...)`: helper estándar para GET perceptibles.
- `/app/loader-preview`
  - Laboratorio visual disponible para administradores.

## Cobertura automática

El interceptor cubre automáticamente las operaciones del backend que normalmente representan una acción explícita del usuario:

- `POST`
- `PUT`
- `PATCH`
- `DELETE`

También cubre lecturas asociadas a documentos o procesos largos cuyo URL contiene patrones como:

- `/pdf`
- `/xml`
- `/report`
- `/summary`
- `/download`
- `/export`

Para estas operaciones **no se debe agregar un `GlobalLoaderService.begin()` adicional** salvo que exista un proceso cliente más amplio que continúe después de la petición HTTP.

## Cobertura selectiva actual

Las siguientes consultas GET principales hacen opt-in explícito porque el usuario percibe la espera y necesita el resultado para continuar:

- Órdenes
  - búsqueda/paginación de órdenes legacy.
- CFDI emitidos
  - consulta paginada con filtros.
- Cuentas por cobrar
  - cartera.
  - workspace del receptor.
  - búsqueda de pagos.
  - candidatos para resumen de adeudos.
- Complementos de pago (REP)
  - bandeja interna.
  - bandeja externa.
  - bandeja unificada.
  - pendientes de atención.
- Auditoría
  - consulta paginada de eventos.
- Importaciones de catálogos
  - carga manual de lotes de receptores por ID y sus filas.
  - carga manual de lotes de productos por ID y sus filas.

## Matriz de decisión para nuevos desarrollos

| Caso | Feedback recomendado | Implementación |
|---|---|---|
| `POST`, `PUT`, `PATCH`, `DELETE` iniciado por el usuario | Loader global | Automático por interceptor |
| PDF, XML, reporte, exportación o descarga HTTP | Loader global | Automático por interceptor |
| GET principal de página, paginación, workspace o detalle que bloquea la siguiente acción | Loader global | `createGlobalLoaderContext(...)` |
| Autocompletado o búsqueda por cada tecla | No loader global | Indicador local discreto si hace falta |
| Polling, health check, precarga o refresh silencioso | No loader global | `SKIP_GLOBAL_LOADER` si el interceptor lo capturaría |
| Carga secundaria dentro de una pantalla ya usable | Loader local / skeleton | Estado local del componente |
| Proceso cliente asíncrono sin `HttpClient` y perceptible | Loader global | `GlobalLoaderService.track()` o `begin()` + `finally` |
| Operación local casi instantánea | Ninguno | No agregar feedback artificial |

## Regla para GET perceptibles

Usar el helper estándar en el servicio API:

```ts
import { createGlobalLoaderContext } from '../../../core/http/global-loader-context.tokens';

return this.http.get<Response>(buildApiUrl('/endpoint'), {
  context: createGlobalLoaderContext({
    message: 'Consultando información',
    detail: 'Estamos recuperando los datos necesarios para continuar.'
  })
});
```

Los mensajes deben describir la operación concreta. Preferir:

- `Consultando órdenes`
- `Cargando cartera`
- `Cargando workspace del receptor`
- `Consultando CFDI emitidos`
- `Cargando lote de productos`

Evitar mensajes genéricos cuando se conoce la acción real.

## Procesos cliente sin HttpClient

Para una promesa que representa toda la unidad de trabajo:

```ts
private readonly loader = inject(GlobalLoaderService);

await this.loader.track(
  () => this.generarDocumento(),
  {
    message: 'Generando documento',
    detail: 'Estamos preparando la información para continuar.'
  }
);
```

Cuando no se pueda usar `track()`:

```ts
const handle = this.loader.begin({
  message: 'Procesando información',
  detail: 'Estamos completando la operación.'
});

try {
  await this.operacion();
} finally {
  handle.close();
}
```

Nunca dejar un `begin()` sin `finally`, `finalize()` o una garantía equivalente de cierre.

## Cuándo NO usar el loader global

### Autocompletados y búsquedas incrementales

No bloquear toda la aplicación mientras el usuario escribe. Ejemplos deliberadamente excluidos:

- búsqueda de receptores.
- búsqueda incremental de productos/perfiles fiscales.
- búsquedas auxiliares de documentos que ya muestran `Buscando...` en el control.

### Estados locales existentes

Si una pantalla ya comunica adecuadamente una carga secundaria sin impedir otras acciones, conservar ese comportamiento. Ejemplos actuales:

- perfil del emisor: `Cargando perfil del emisor...` y `Cargando logotipo...`.
- historial de mappings SAT: botón/estado `Actualizando...`.
- autorizaciones pendientes de cancelación: estado local `Consultando autorizaciones pendientes...`.
- panel XML: propiedad local `loading`.

El objetivo no es mostrar el overlay en toda petición, sino eliminar momentos donde el usuario no sabe si el sistema sigue trabajando.

## Antiparpadeo

El interceptor aplica tiempos centrales para evitar que una petición rápida provoque un flash visual. No ajustar `delayMs` o `minimumVisibleMs` por pantalla sin una razón UX comprobable.

Si una petición termina antes del retardo, el overlay no se muestra.

## Concurrencia

`GlobalLoaderService` maneja cada operación con un handle independiente. El overlay permanece activo mientras exista al menos una operación abierta.

Por esta razón:

- no sustituir el servicio por un booleano global.
- no llamar `clear()` desde flujos funcionales para cerrar “lo que esté abierto”.
- cerrar únicamente el handle que pertenece a la operación actual.

## Pruebas obligatorias

Cuando un nuevo GET haga opt-in al loader, su prueba del servicio API debe comprobar el contexto:

```ts
expect(req.request.context.get(GLOBAL_LOADER_OPTIONS)?.message)
  .toBe('Mensaje esperado');
```

Cuando se modifique el interceptor o el servicio global, conservar cobertura para:

- anti-parpadeo.
- tiempo mínimo visible.
- éxito y error HTTP.
- operaciones concurrentes.
- cierre idempotente.
- opt-in de GET.
- opt-out mediante `SKIP_GLOBAL_LOADER`.
- recursos externos al backend.

## Checklist de revisión para PR

Para cada nuevo flujo asíncrono del frontend revisar:

- [ ] ¿El usuario inicia una operación y debe esperar para continuar?
- [ ] ¿La operación ya está cubierta automáticamente por el interceptor?
- [ ] Si es GET perceptible, ¿usa `createGlobalLoaderContext(...)`?
- [ ] Si es búsqueda incremental/autocomplete, ¿se evitó el overlay global?
- [ ] Si es una carga secundaria, ¿existe feedback local suficiente?
- [ ] ¿El mensaje describe la acción real?
- [ ] ¿El loader siempre se cierra también en error?
- [ ] ¿Se agregó o actualizó una prueba que valide la política elegida?
- [ ] ¿Se verificó que no se introduzcan dobles loaders globales?

## Auditoría de cobertura 2026-08-08

La revisión posterior a la Fase 3 encontró una brecha concreta: los botones **Cargar lote por id** de importaciones de receptores y productos ejecutaban dos lecturas secuenciales (resumen + filas) sin un estado de carga local. Esas lecturas se incorporaron al loader global mediante `createGlobalLoaderContext(...)`.

No se recomienda ampliar el overlay de forma indiscriminada al resto de GET actuales. Los demás casos revisados están cubiertos por alguna de estas condiciones:

1. ya forman parte de la cobertura automática;
2. ya hacen opt-in selectivo;
3. disponen de feedback local explícito;
4. son autocompletados, lecturas auxiliares o trabajo que conviene mantener no bloqueante.

Si aparece una nueva espera visible en DEV, debe evaluarse con la matriz anterior antes de agregar el loader.
