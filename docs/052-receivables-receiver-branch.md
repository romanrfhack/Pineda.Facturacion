# Sucursal del cliente en el estado de cuenta

## Regla confirmada (2026-10-14)
Kioto tiene dos ubicaciones. Operación confirmó que solo agrupa órdenes de una misma
sucursal en una factura. Se conserva un valor por documento fiscal; no se infiere
la sucursal desde el receptor, el pedido legacy, el domicilio ni otro documento.
La separación de órdenes sigue siendo responsabilidad de operación: este cambio
no agrega una validación automática por pedido.

## Configuración y operación
1. Editar el receptor en Catálogo de receptores → Campos especiales de facturación.
2. Agregar clave **SUCURSAL**, nombre **Sucursal del cliente**, tipo **texto**,
   activo y requerido. Se recomienda longitud máxima 100 y escribir en la ayuda
   los dos nombres acordados con Kioto. No inventar nombres ni duplicar receptores.
3. Al preparar cada documento fiscal, capturar el nombre de su sucursal.
   La captura actual es texto libre; no se implementa un catálogo/desplegable.
4. Revisar el valor antes de timbrar. El PDF de factura ya muestra este snapshot
   en Datos adicionales; la edición posterior al timbrado sigue bloqueada.
5. En Cuentas por cobrar → Resumen de adeudos, revisar la sucursal por factura.
   Aparece en la selección, correo HTML y PDF digital/impresión. WhatsApp conserva
   el PDF digital generado por el flujo existente. En HTML se muestra debajo del
   folio; en PDF debajo del estado, aprovechando la columna ancha sin reducir las
   columnas monetarias ni cambiar el tamaño carta.

## Datos y compatibilidad
- Se añade `receiverBranchName` opcional a los candidatos del resumen.
- La consulta de cartera proyecta el valor de `FiscalDocumentSpecialFieldValue`
  cuya clave es `SUCURSAL`, sin consultas individuales por factura.
- Se usa el valor histórico guardado, aunque la definición se desactive después.
- Si el receptor tiene el campo activo o su cartera contiene ese snapshot, las
  facturas sin valor muestran **Sin sucursal registrada**. No hay backfill.
- Los receptores sin configuración ni snapshots de sucursal conservan su salida.
- No se alteran saldos, pagos, totales, elegibilidad, timbrado, XML, ni tablas.
- No requiere migración. No se escribe en MySQL legacy.
- Configurar el campo en un receptor es una operación separada; este cambio de
  código no modifica automáticamente la ficha de Kioto ni documentos históricos.

## Validación
Pruebas de repositorio: dos snapshots distintos, otro campo ajeno a sucursal y
un documento externo sin snapshot. Pruebas de aplicación: propagación, histórico,
receptor sin campo, HTML escapado y totales conservados. Ambos PDFs conservan
formato carta. Pruebas Angular: nombre visible/escapado y receptor sin sucursal.

Validación ejecutada: 102 pruebas .NET (AccountsReceivableServicesTests y
RepositoryQueryOptimizationTests), 8 pruebas Angular del modal y build Angular.
Render de muestra con 40 facturas: 3 páginas tamaño carta, con sucursal y saldo
por fila, revisado visualmente. Build con avisos preexistentes de tamaño de estilos
y dependencias NuGet; no se cambian paquetes en este incremento.
