# Visión del proyecto

## Nombre
Pineda.Facturacion

## Objetivo
Construir un nuevo backend de facturación en .NET 10 que permita leer información del sistema legacy en MySQL en modo solo lectura, tomar snapshots controlados de pedidos y clientes, y emitir CFDI mediante un proveedor externo de timbrado, sin afectar la operación del sistema actual.

## Contexto
El sistema legacy actual continúa en operación y seguirá capturando pedidos, clientes y ventas.
La nueva solución no debe escribir ni modificar la base de datos legacy.
La nueva solución tendrá su propia base de datos MySQL para almacenar snapshots, documentos internos, resultados de timbrado, cancelaciones, artefactos fiscales y trazabilidad técnica.

## Problema a resolver
Actualmente la operación comercial depende de un sistema legacy que no debe tocarse mientras se desarrolla la nueva solución.
Se requiere habilitar con urgencia la facturación electrónica sin poner en riesgo la continuidad operativa del sistema actual.

## Alcance del MVP
- Leer pedidos elegibles desde la base de datos legacy.
- Importar y congelar snapshots de pedidos, clientes y conceptos en la nueva base de datos.
- Construir un documento interno facturable.
- Timbrar CFDI con el proveedor FacturaloPlus.
- Guardar UUID, XML timbrado, respuesta del proveedor y evidencia fiscal.
- Consultar estatus del CFDI.
- Cancelar CFDI con motivo de cancelación.
- Exponer endpoints HTTP para importación, vista previa, emisión, cancelación y consulta.

## Fuera de alcance inicial
- Reemplazar completamente el ERP legacy.
- Escribir en la base de datos legacy.
- Migrar todos los módulos del sistema anterior.
- Construir de inicio el frontend final.
- Implementar desde el día uno todos los flujos alternos del sistema anterior.
- Generar PDF propio en la primera iteración si el proveedor ya devuelve representaciones útiles.

## Principios
- No afectar la operación del sistema legacy.
- No duplicar reglas de negocio en múltiples capas.
- Mantener trazabilidad completa de importación, timbrado y cancelación.
- Favorecer diseño incremental y documentación viva.
- Diseñar primero para claridad, control e idempotencia.

## Resultado esperado
Tener un backend confiable, auditable y desacoplado del legacy, que permita emitir y cancelar CFDI a partir de pedidos originados en el sistema actual, mientras se prepara la transición hacia una solución más moderna y completa.
