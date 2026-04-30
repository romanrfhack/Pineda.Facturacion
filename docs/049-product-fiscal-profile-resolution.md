# Resolucion fiscal inteligente de productos

## Resumen

El modulo de Documentos Fiscales incorpora una resolucion fiscal inteligente de productos antes del timbrado. El objetivo es reducir la captura manual de clave SAT de producto/servicio y clave SAT de unidad cuando una linea del documento no tiene perfil fiscal completo.

La resolucion reutiliza decisiones ya confirmadas por el usuario y mappings historicos importados desde CSV del sistema de facturacion anterior. Cuando la coincidencia es confiable, el sistema puede resolver automaticamente el perfil fiscal necesario para preparar el documento. Cuando hay ambiguedad, mantiene el flujo manual actual y pide validacion.

Release note interna:

> Se agrego resolucion fiscal inteligente de productos en Documentos Fiscales antes del timbrado. El sistema ahora reutiliza perfiles fiscales previamente confirmados y mappings historicos importados desde CSV del sistema anterior para sugerir o resolver automaticamente claves SAT de producto/servicio y unidad SAT. La resolucion prioriza perfiles confirmados por el usuario, perfiles existentes y coincidencias confiables del historial importado. Los casos ambiguos requieren validacion manual y el codigo generico `01010101` no se asigna automaticamente.

## Componentes principales

- Resolver central: `ProductFiscalProfileResolver`.
- Integracion de pre-timbrado: `PrepareFiscalDocumentService`.
- Importador CSV: `ImportLegacyFiscalProductMappingsFromCsvService`.
- Normalizador de texto: `FiscalProductTextNormalization`.
- Repositorio legacy: `LegacyFiscalProductMappingRepository`.
- Tablas nuevas: `fiscal_product_mapping_import_batch` y `legacy_fiscal_product_mapping`.
- Migracion: `20260429153000_AddLegacyFiscalProductMappings`.
- Endpoint administrativo: `POST /api/fiscal/imports/products/legacy-mappings/csv`.
- UI de recuperacion: pantalla existente de Documentos Fiscales y formulario de perfil fiscal de producto.

## Arquitectura general del resolver

`ProductFiscalProfileResolver` recibe los datos de la linea fiscal pendiente:

- Codigo interno del producto.
- Descripcion del producto.
- Clave SAT de producto/servicio capturada en la linea, si existe.
- Clave SAT de unidad capturada en la linea, si existe.
- Objeto de impuesto capturado en la linea, si existe.
- Tasa de IVA de la linea.

El resolver devuelve:

- `status`: `resolved`, `suggested`, `ambiguous` o `unresolved`.
- `source`: origen de la decision o sugerencia.
- `confidence`: nivel numerico de confianza.
- `reason`: motivo legible para UI y diagnostico.
- `resolvedProfile`: perfil fiscal resuelto, cuando aplica.
- `candidates`: opciones candidatas, especialmente en sugerencias y ambiguedad.

La preparacion del documento fiscal solo continua sin bloquear cuando el resultado es `resolved` y contiene `resolvedProfile`. Los resultados `suggested`, `ambiguous` y `unresolved` mantienen el flujo de recuperacion manual existente.

## Orden de prioridad

El orden exacto de resolucion es:

1. Perfil confirmado/manual por usuario.
2. Perfil fiscal existente del producto.
3. Mapping legacy exacto por codigo interno normalizado mas descripcion normalizada.
4. Mapping legacy exacto por codigo interno normalizado unico.
5. Mapping legacy exacto por descripcion normalizada unica.
6. Fuzzy legacy por descripcion como sugerencia.
7. Logica automatica existente.
8. Flujo manual.

Este orden es intencional. Un perfil confirmado por usuario representa una decision explicita y siempre debe ganar sobre cualquier dato importado o inferido.

## Reglas de negocio

- El perfil confirmado/manual por el usuario siempre tiene prioridad sobre cualquier mapping legacy.
- El perfil fiscal existente del producto tiene prioridad sobre mappings legacy.
- El sistema puede resolver automaticamente cuando hay coincidencia exacta confiable y no ambigua.
- Si la misma descripcion normalizada apunta a varias claves SAT, el resultado debe ser `ambiguous`.
- Si el mismo codigo interno normalizado apunta a varias claves SAT, el resultado debe ser `ambiguous`.
- Los casos ambiguos no se autoasignan; deben pedir validacion del usuario.
- Las coincidencias fuzzy solo devuelven candidatos sugeridos; nunca autoasignan.
- El codigo SAT generico `01010101` nunca se asigna automaticamente.
- `01010101` solo puede usarse si el usuario lo elige explicitamente.
- El CSV legacy solo aporta `Clave Producto/Servicio SAT` y `Clave Unidad SAT`.
- El CSV legacy no aporta objeto de impuesto, tasa de IVA ni texto de unidad.
- Objeto de impuesto, tasa de IVA y texto de unidad siguen resolviendose con las reglas actuales del sistema.
- La UI debe conservar el flujo actual de `Guardar y reintentar`.
- La UI debe mostrar fuente, motivo y confianza de la sugerencia o resolucion.

## Fuentes y significado

- `confirmed_profile`: asignacion o perfil confirmado manualmente por el usuario.
- `existing_profile`: perfil o asignacion fiscal efectiva ya existente para el producto.
- `legacy_mapping`: mapping historico importado desde CSV.
- `current_auto_detection`: logica automatica existente del sistema.
- `manual`: fallback manual cuando no hay resolucion ni sugerencia suficiente.

## Persistencia y aprendizaje

Cuando el usuario corrige o confirma una sugerencia desde la UI y guarda el perfil fiscal, el flujo actual crea o actualiza `ProductFiscalProfile` y sincroniza `ProductFiscalAssignment` con fuente manual.

Efecto esperado:

- La decision queda como perfil confirmado/manual.
- En documentos posteriores con el mismo producto, el resolver encuentra primero la asignacion/perfil confirmado.
- El sistema ya no debe volver a pedir seleccion para ese producto, salvo que el perfil quede inactivo o pendiente de revision por reglas existentes.
- Un mapping legacy no debe sobrescribir automaticamente una decision manual posterior.

## Importacion CSV de mappings legacy

### Endpoint

- Metodo: `POST`.
- Ruta: `/api/fiscal/imports/products/legacy-mappings/csv`.
- Content type: `multipart/form-data`.
- Campo requerido: `file`.
- Campo opcional: `sourceName`.
- Permisos: usuario Supervisor/Admin.

Ejemplo:

```bash
curl -X POST "https://host/api/fiscal/imports/products/legacy-mappings/csv" \
  -H "Authorization: Bearer <token>" \
  -F "file=@legacy-product-mappings.csv;type=text/csv" \
  -F "sourceName=facturacion-anterior-2026-04"
```

### Columnas esperadas

El CSV esperado debe venir en UTF-8, con soporte para BOM, y con estas columnas:

- `Id`
- `Descripcion`
- `Clave Producto/Servicio`
- `Clave Unidad`
- `No. Catalogo Interno`
- `Codigo EAN`
- `Codigo SKU`

Los encabezados con acentos tambien son reconocidos:

- `Descripción`
- `No. Catálogo Interno`
- `Código EAN`
- `Código SKU`

### Validaciones

La importacion aplica estas validaciones:

- Valida que existan las columnas esperadas.
- Normaliza descripcion, codigo interno, EAN y SKU para comparacion.
- Valida que `Clave Producto/Servicio` tenga exactamente 8 digitos.
- Valida `Clave Unidad` contra el catalogo SAT local cuando viene informada.
- Ignora registros sin clave SAT de producto/servicio.
- Marca como invalidos registros con clave SAT de producto/servicio mal formada.
- Marca como invalidos registros sin descripcion ni identificadores comparables.
- Marca como invalidos registros con clave de unidad inexistente o inactiva cuando el catalogo local permite validarlo.

### Dedupe e idempotencia

La importacion usa un checksum SHA-256 del archivo completo para evitar duplicar imports del mismo archivo. Si se sube el mismo archivo otra vez, el resultado es `AlreadyImported` y no se crean registros nuevos.

Dentro del archivo, registros repetidos con la misma combinacion normalizada de identificadores y claves SAT se omiten como duplicados del batch.

### Deteccion de ambiguedad

La importacion marca registros ambiguos cuando:

- La misma `DescriptionNormalized` aparece con mas de una `Clave Producto/Servicio`.
- El mismo codigo interno normalizado, SKU normalizado o EAN normalizado aparece con mas de una `Clave Producto/Servicio`.

Los registros ambiguos pueden persistirse como validos, pero el resolver debe tratarlos como candidatos que requieren validacion.

### Resumen del batch

Cada importacion guarda un batch en `fiscal_product_mapping_import_batch` con:

- `FileName`
- `SourceName`
- `SourceChecksum`
- `ImportedAtUtc`
- `ImportedByUserId`
- `TotalRows`
- `ValidRows`
- `InvalidRows`
- `AmbiguousRows`
- `SkippedRows`
- `Status`
- `ErrorMessage`

Los mappings validos se guardan en `legacy_fiscal_product_mapping`.

## Normalizacion de texto

El sistema normaliza textos e identificadores para hacer comparaciones robustas:

- Convierte a mayusculas.
- Quita acentos.
- Elimina espacios duplicados.
- Normaliza guiones, puntos, diagonales y signos comparables.
- Conserva letras y numeros.
- Reduce diferencias menores de puntuacion.

Ejemplos:

- `Switch de ignición` compara como `SWITCH DE IGNICION`.
- `MANGUERA DE CALEFACCIÓN` compara como `MANGUERA DE CALEFACCION`.
- `SW-1`, `SW 1` y variantes con guiones o espacios pueden compararse correctamente para mappings legacy.
- `7E0 905 865` se mantiene comparable aunque tenga dobles espacios o separadores menores.

Nota importante:

- La llave persistida del perfil fiscal sigue usando la normalizacion maestra actual de `ProductFiscalProfile`.
- La llave comparable legacy usa la normalizacion fiscal inteligente para buscar mappings importados.

## Ejemplo real: SWITCH DE IGNICION

Producto en documento:

- Codigo interno: `7E0 905 865`.
- Descripcion: `SWITCH DE IGNICION`.

Mapping legacy importado:

- Descripcion: `SWITCH DE IGNICION`.
- Clave Producto/Servicio: `25173900`.
- Clave Unidad: `H87`.
- Codigo interno distinto o no coincidente.

Resultado esperado:

- El sistema resuelve o sugiere por descripcion exacta normalizada.
- Clave SAT sugerida o resuelta: `25173900`.
- Unidad SAT: `H87`.
- Fuente: historial fiscal importado (`legacy_mapping`).
- Motivo: coincidencia exacta por descripcion.
- No requiere que el codigo interno coincida.

Si la descripcion normalizada `SWITCH DE IGNICION` apunta a una sola clave SAT, el resultado puede ser `resolved` con confianza alta. Si apunta a varias claves SAT, el resultado debe ser `ambiguous`.

## Ambiguedad

Ejemplo:

- `SWITCH DE IGNICION` con clave SAT `25173900`.
- `SWITCH DE IGNICION` con clave SAT `40161513`.

Resultado:

- `status = ambiguous`.
- No se autoasigna.
- Se muestran candidatos.
- El usuario debe seleccionar la opcion correcta.
- Al guardar, la decision manual queda persistida y tendra prioridad en documentos futuros.

La misma regla aplica cuando un mismo codigo interno normalizado, SKU o EAN apunta a mas de una clave SAT.

## Fuzzy legacy

La coincidencia aproximada por descripcion ayuda cuando no hay coincidencia exacta.

Reglas:

- Devuelve `status = suggested`.
- Nunca autoasigna.
- Nunca persiste automaticamente.
- No reemplaza perfiles confirmados.
- No reemplaza perfiles existentes.
- No reemplaza matches exactos confiables.
- Requiere validacion del usuario antes de continuar.

El umbral actual busca evitar sugerencias demasiado debiles. Aun asi, por definicion, fuzzy es ayuda operativa y no una decision fiscal automatica.

## Codigo generico 01010101

`01010101` representa el codigo generico SAT. En este flujo:

- Nunca debe asignarse automaticamente.
- No debe usarse como fallback silencioso.
- Si aparece en un mapping legacy, solo puede mostrarse como opcion que requiere confirmacion explicita.
- La UI mantiene la accion explicita para usar `01010101` cuando el usuario lo decide.

## UI de recuperacion

La UI conserva el flujo existente de recuperacion fiscal de producto:

- No se redisenio el flujo completo.
- Se mantiene `Guardar y reintentar`.
- Se permite editar cualquier sugerencia.
- Se muestran fuente, motivo y confianza.
- Se distinguen mappings historicos importados.
- En ambiguedad, se muestran candidatos y se requiere seleccion manual.
- El usuario puede cambiar clave SAT, unidad SAT, objeto de impuesto, IVA y texto de unidad antes de guardar.

Textos esperados cuando hay sugerencia legacy:

- `Sugerido por historial fiscal importado`.
- `Coincidencia exacta por descripcion`.
- Confianza: `Alta`, `Media`, `Baja` o `Ambigua`.

## Prueba manual recomendada

1. Importar un CSV legacy con la fila de `SWITCH DE IGNICION`, clave SAT `25173900` y unidad `H87`.
2. Preparar una nota con producto codigo interno `7E0 905 865` y descripcion `SWITCH DE IGNICION`.
3. Confirmar que el sistema resuelve o sugiere clave SAT `25173900`.
4. Confirmar que la unidad SAT sugerida o resuelta es `H87`.
5. Confirmar que la fuente es historial fiscal importado.
6. Confirmar que el motivo indica coincidencia exacta por descripcion.
7. Guardar y reintentar si el flujo pidio validacion.
8. Preparar otra nota con el mismo producto.
9. Confirmar que ya no vuelve a pedir el perfil fiscal.
10. Probar un caso ambiguo con la misma descripcion y dos claves SAT distintas.
11. Confirmar que no autoasigna y muestra candidatos.
12. Probar un mapping legacy con `01010101`.
13. Confirmar que no se autoasigna automaticamente.
14. Timbrar un documento completo con productos resueltos automaticamente.

## Limitacion actual: mappings globales

Actualmente `legacy_fiscal_product_mapping` es global.

Esto es consistente con el modelo actual de:

- `ProductFiscalProfile`.
- `ProductFiscalAssignment`.

Estas entidades tampoco estan scoped por empresa, emisor o tenant. Para el MVP es aceptable porque mantiene consistencia con el diseno actual del modulo.

Riesgo:

- Si en el futuro distintos emisores o empresas requieren perfiles fiscales distintos para el mismo producto, una resolucion global podria aplicar un mapping de contexto incorrecto.

Mitigacion actual:

- El perfil confirmado/manual del usuario tiene prioridad.
- Los casos ambiguos no se autoasignan.
- La UI permite editar cualquier sugerencia antes de guardar.

## Deuda tecnica futura: agregar scope multiempresa a perfiles fiscales de producto y mappings legacy

Actualmente los perfiles fiscales de producto, asignaciones fiscales y mappings legacy se resuelven de forma global. Si en el futuro distintos emisores o empresas requieren perfiles fiscales distintos para el mismo producto, sera necesario agregar un identificador de contexto, empresa, emisor o tenant a estas entidades y ajustar el resolver para resolver dentro del contexto correcto.

Criterios futuros:

1. Agregar identificador de empresa, emisor o tenant segun el modelo definitivo.
2. Evitar que mappings de una empresa se apliquen a otra.
3. Migrar perfiles existentes con scope compatible.
4. Ajustar `ProductFiscalProfileResolver` para resolver dentro del contexto correcto.
5. Agregar pruebas de aislamiento entre empresas.
6. Mantener compatibilidad con mappings globales durante una transicion, si aplica.

Entidades y servicios que deberian evaluarse:

- `ProductFiscalProfile`.
- `ProductFiscalAssignment`.
- `LegacyFiscalProductMapping`.
- `FiscalProductMappingImportBatch`.
- `ProductFiscalProfileResolver`.
- Importador CSV de mappings legacy.
- UI y endpoint de importacion.

## Alcance fuera de esta funcionalidad

- No se infiere objeto de impuesto desde CSV.
- No se infiere tasa de IVA desde CSV.
- No se infiere texto de unidad desde CSV.
- No se asigna `01010101` automaticamente.
- No se reemplaza el flujo manual.
- No se crea scope multiempresa en el MVP.
- No se hace validacion fiscal externa contra SAT en tiempo real.
