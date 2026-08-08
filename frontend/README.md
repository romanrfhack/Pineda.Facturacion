# Frontend

Frontend Angular de Pineda.Facturacion.

## Regla UX obligatoria: feedback de procesamiento

El proyecto dispone de un **loader global personalizado de Auto Refacciones Pineda**. Antes de agregar o modificar cualquier flujo asíncrono, revisa la guía:

- [`../docs/FRONTEND_GLOBAL_LOADER.md`](../docs/FRONTEND_GLOBAL_LOADER.md)

Regla resumida:

- `POST`, `PUT`, `PATCH` y `DELETE` del backend: loader global automático.
- PDF/XML/reportes/exportaciones/descargas: loader global automático.
- GET principal perceptible por el usuario: hacer opt-in con `createGlobalLoaderContext(...)`.
- Autocomplete, búsqueda por tecla, polling y cargas secundarias: no bloquear toda la aplicación; usar feedback local cuando corresponda.
- Procesos asíncronos sin `HttpClient`: usar `GlobalLoaderService.track()` o `begin()` con cierre garantizado en `finally`.
- Toda nueva integración debe incluir una prueba que valide la política elegida.

**No implementar spinners globales paralelos ni booleanos globales alternativos.** La infraestructura existente maneja concurrencia, anti-parpadeo y cierre seguro.

## Development server

To start a local development server, run:

```bash
ng serve
```

Once the server is running, open your browser and navigate to `http://localhost:4200/`. The application will automatically reload whenever you modify any of the source files.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile the project and store the build artifacts in the `dist/` directory.

## Running unit tests

To execute unit tests with Vitest:

```bash
ng test
```

## Running end-to-end tests

```bash
npm run e2e:ci
```

## Validación mínima antes de integrar cambios frontend

```bash
npm ci
npm run build
npm run test
npm run e2e:ci
```
