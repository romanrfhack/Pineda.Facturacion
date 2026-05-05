# Reporte de notas timbradas

## Objetivo

El reporte de notas timbradas permite consultar, por rango de fechas, las notas o pedidos legacy que ya quedaron timbrados en un CFDI. La salida está pensada para grid operativo y exportación a Excel.

## Fuente de datos

La proyección read-only usa `FiscalDocument`, `FiscalStamp`, `BillingDocument`, `BillingDocumentItem`, `SalesOrder`, `LegacyImportRecord` y `FiscalCancellation`.

No se cargan entidades completas para reporteo. La consulta agrupa `BillingDocumentItem` por `FiscalDocument` + nota/pedido legacy para evitar duplicados por partidas.

## Fecha oficial

El filtro de fechas usa `FiscalStamp.StampedAtUtc`.

No se usa `FiscalDocument.IssuedAtUtc`, `BillingDocument.IssuedAtUtc`, `CreatedAtUtc` ni `UpdatedAtUtc`, porque esas fechas no representan el momento real de timbrado.

## Granularidad

La granularidad es una fila por nota/pedido legacy + CFDI.

Esto permite mostrar facturas que agrupan varias notas sin duplicar una nota con múltiples partidas. `LegacyOrderId` corresponde a `LegacyImportRecord.SourceDocumentId` (`noPedido`) y `LegacyOrderNumber` corresponde a `SalesOrder.LegacyOrderNumber` (`refPedido`).

## Timbrados incluidos

Un registro se considera timbrado si cumple:

- `FiscalStamp.Status == Succeeded`.
- `FiscalStamp.Uuid` no está vacío.
- `FiscalStamp.StampedAtUtc` no es nulo.
- `FiscalDocument.Status` es `Stamped` o `CancellationRejected`.

## Cancelados excluidos

Por default se excluyen:

- `FiscalDocument.Status == Cancelled`.
- `FiscalDocument.Status == CancellationRequested`.
- `FiscalCancellation.Status == Cancelled`, si existe registro de cancelación.

## Endpoints

- `GET /api/reports/stamped-legacy-notes`: búsqueda paginada.
- `GET /api/reports/stamped-legacy-notes/export`: exportación XLSX con los filtros actuales, sin limitarse a la página visible.

Ambos endpoints usan la política `AuditRead`, que incluye `Admin`, `FiscalSupervisor` y `Auditor`.

## Columnas

El reporte expone fecha de timbrado, `noPedido`, `refPedido`, receptor, RFC receptor, serie, folio, UUID, total completo del CFDI, importe de la nota dentro del CFDI, moneda, `BillingDocumentId`, `FiscalDocumentId`, estatus fiscal, estatus de cancelación y cantidad de partidas agrupadas.

## Zona horaria

El usuario captura fechas locales de México. El backend convierte el rango a UTC usando `America/Mexico_City` y compara con rango semiabierto:

`fromUtc <= FiscalStamp.StampedAtUtc < toUtcExclusive`

Si el usuario captura fecha final `2026-05-04`, se incluye todo el día local `2026-05-04`.

## Riesgos conocidos

Puede haber CFDIs históricos recreados si una nota fue cancelada y refacturada. Una factura puede agrupar varias notas. Una nota puede tener varias partidas, por eso la query agrupa antes de proyectar. Si el volumen crece, conviene evaluar un índice sobre `FiscalStamp.Status`, `FiscalStamp.StampedAtUtc` y `FiscalStamp.FiscalDocumentId`.
