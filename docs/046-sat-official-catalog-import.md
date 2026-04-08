# Importación oficial de catálogo SAT

## Resumen

- Endpoint: `POST /api/fiscal/imports/sat/official`
- Pantalla: `Catálogos / Importar catálogo SAT`
- Flujo separado del staging de `product_fiscal_profile`
- El backend es la autoridad de `sourceFileName`, `sourceChecksum` y `sourceVersion`

## Límite de request

- El endpoint SAT quedó con metadata específica:
  - `multipart/form-data`: `128 MB`
  - `MaxRequestBodySize`: `128 MB`
- El cambio está aplicado sólo al endpoint `POST /api/fiscal/imports/sat/official`.
- En despliegues detrás de reverse proxy todavía se debe alinear el límite aguas arriba:
  - IIS: `maxAllowedContentLength`
  - Nginx: `client_max_body_size`
  - cualquier proxy/LB equivalente debe permitir al menos `128 MB`

## Contrato del endpoint

### Request

- Obligatorio:
  - `file`
- Opcionales y deprecados por compatibilidad:
  - `sourceChecksum`
  - `sourceVersion`
  - `sourceFileName`

Notas:

- `sourceChecksum` enviado por cliente se usa sólo para diagnóstico.
- `sourceVersion` ya no se toma del cliente.
- `sourceFileName` manual ya no se requiere; el backend usa `IFormFile.FileName`.

### Respuesta

- `outcome`
- `isSuccess`
- `errorMessage`
- `correlationId`
- `sourceFileName`
- `sourceVersion`
- `sourceChecksum`
- `clientChecksumMatchesServer`
- `productServices`
- `units`

## Reglas de metadatos

- `sourceFileName`: se toma del archivo subido.
- `sourceChecksum`: se recalcula server-side como `SHA-256` y se persiste en `sat_catalog_imports`.
- `sourceVersion`: se fija server-side en `4.0`.

Trade-off de `sourceVersion`:

- Se mantuvo `4.0` estable para no depender del nombre del archivo y para que la UI pueda mostrar exactamente la versión CFDI usada.
- La trazabilidad fina del archivo queda en `sourceFileName + sourceChecksum`.

## Idempotencia

- `AlreadyImported` ahora se resuelve por:
  - `catalogType`
  - `sourceChecksum`
- Ya no depende de `sourceVersion` ni de `sourceFileName`.

## Contrato real del workbook

Lectura confirmada en código actual:

- Implementación: [`ClosedXmlWorksheetReader.cs`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure/Excel/ClosedXmlWorksheetReader.cs)
- Servicio: [`ImportOfficialSatCatalogService.cs`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/SatCatalogs/ImportOfficialSatCatalogService.cs)

### Formatos soportados

- Soportado por esta app: `.xls` y `.xlsx`
- La UI permite seleccionar ambos formatos.
- El backend valida por contenido del workbook cuando es posible:
  - `.xlsx`: contenedor OpenXML (`ZIP`)
  - `.xls`: workbook binario BIFF/OLE2
- El backend no depende únicamente de la extensión.

### Hojas requeridas

- `c_ClaveProdServ`
- `c_ClaveUnidad`

La detección normaliza mayúsculas, acentos y caracteres no alfanuméricos. También puede identificar una hoja por encabezados si el nombre no coincide exactamente.

### Columnas obligatorias

#### Hoja `c_ClaveProdServ`

- Código:
  - `c_ClaveProdServ`
  - `cClaveProdServ`
- Descripción:
  - `Descripción`
  - `Nombre`

#### Hoja `c_ClaveUnidad`

- Código:
  - `ClaveUnidad`
  - `cClaveUnidad`
  - `Clave`
- Descripción:
  - `Nombre`
  - `Descripción`

### Columnas opcionales

#### Hoja `c_ClaveProdServ`

- palabras similares:
  - `Palabras similares`
  - `Palabras clave`
  - `Palabras similar`
- vigencia/estatus:
  - `Estatus`
  - `Activo`
  - `Vigente`
  - `FechaFinVigencia`
  - `FinVigencia`
  - `FechaFinDeVigencia`

#### Hoja `c_ClaveUnidad`

- símbolo:
  - `Símbolo`
  - `Simbol`
- notas:
  - `Notas`
  - `Nota`
- vigencia/estatus:
  - `Estatus`
  - `Activo`
  - `Vigente`
  - `FechaFinVigencia`
  - `FinVigencia`
  - `FechaFinDeVigencia`

## Mensajes de error

- Hojas faltantes:
  - mensaje claro indicando `c_ClaveProdServ` y/o `c_ClaveUnidad`
- Columnas obligatorias faltantes:
  - mensaje claro indicando la hoja y los aliases esperados
- Workbook corrupto o formato no soportado:
  - formato no soportado:
    - `The SAT file format is not supported. Upload a valid .xls or .xlsx workbook.`
  - workbook corrupto:
    - `The SAT workbook is corrupted or could not be read as a valid .xls or .xlsx file.`

## Lectura del workbook

- La lectura conserva la ruta actual de `.xlsx`.
- Se agrega lectura nativa para `.xls`.
- Si el archivo no coincide con una firma Excel soportada, responde formato no soportado.
- Si la firma corresponde a Excel pero el workbook no puede abrirse, responde workbook corrupto.

## Alcance preservado

- Sigue poblando:
  - `sat_product_service_catalog`
  - `sat_clave_unidad`
  - `sat_catalog_imports`
- No mezcla el flujo con:
  - `product_fiscal_profile`
  - `product_fiscal_assignment`
- No toca timbrado ni PDF
