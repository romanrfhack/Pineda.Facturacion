# Pineda.Facturacion

## Descripción
Backend de facturación en .NET 10 para leer pedidos desde un sistema legacy en MySQL en modo solo lectura, importar snapshots controlados a una nueva base de datos MySQL y emitir CFDI mediante un proveedor externo de timbrado.

## Objetivo
Habilitar la facturación electrónica sin afectar la operación del sistema actual.

## Principios clave
- La base de datos legacy es solo lectura para este sistema.
- La nueva base de datos es independiente.
- El timbrado se realiza desde el nuevo sistema.
- Las decisiones de facturación se basan en snapshots importados.
- La lógica de negocio no debe vivir en los endpoints HTTP.

## Arquitectura
La solución usa una arquitectura limpia pragmática con Minimal APIs como capa HTTP.

## Estructura de la solución
- src/Pineda.Facturacion.Api: endpoints HTTP y configuración
- src/Pineda.Facturacion.Application: casos de uso y contratos
- src/Pineda.Facturacion.Domain: entidades y reglas puras
- src/Pineda.Facturacion.Infrastructure: piezas técnicas compartidas
- src/Pineda.Facturacion.Infrastructure.LegacyRead: lectura del legacy
- src/Pineda.Facturacion.Infrastructure.BillingWrite: persistencia de la nueva BD
- src/Pineda.Facturacion.Infrastructure.FacturaloPlus: integración con PAC
- 	ests/*: pruebas

## Documentación
La carpeta docs/ contiene la visión, arquitectura, reglas de integración, diseño de BD y acuerdos de trabajo con Codex.

## Estado actual
Bootstrap inicial de la solución y documentación base.

## Regla crítica
Este sistema nunca debe escribir en la base de datos legacy.
