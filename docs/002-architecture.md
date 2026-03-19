# Arquitectura

## Estilo arquitectónico
La solución usará una arquitectura limpia pragmática con Minimal APIs como capa de exposición HTTP.

## Decisión principal
No se usará un proyecto monolítico con toda la lógica mezclada en la API.
La lógica de negocio debe permanecer separada de HTTP, MySQL y del proveedor externo de timbrado.

## Capas de la solución

### 1. Api
Responsable de exponer endpoints HTTP, configurar OpenAPI, middleware y composición de dependencias.
Debe permanecer delgada.
No debe contener reglas de negocio ni acceso directo a base de datos.

### 2. Application
Responsable de los casos de uso del sistema.
Aquí viven la orquestación, DTOs, contratos, validaciones y coordinación entre dominio e infraestructura.

### 3. Domain
Responsable de las entidades, value objects, enums y reglas puras del negocio.
No debe depender de frameworks ni de infraestructura.

### 4. Infrastructure.LegacyRead
Responsable de leer la base de datos legacy en modo solo lectura.
No debe escribir en la base legacy bajo ninguna circunstancia.

### 5. Infrastructure.BillingWrite
Responsable de persistir la nueva base de datos del sistema de facturación.
Aquí vivirán el DbContext, migraciones, configuraciones y repositorios de escritura.

### 6. Infrastructure.FacturaloPlus
Responsable de encapsular la integración con el proveedor externo de timbrado y cancelación.
Toda llamada al PAC debe estar aislada detrás de contratos de Application.

### 7. Infrastructure
Responsable de piezas técnicas compartidas entre infraestructuras cuando aplique.

## Regla de dependencias
Las dependencias deben apuntar hacia adentro:

- Api -> Application
- Api -> Infrastructure.*
- Infrastructure.* -> Application
- Infrastructure.* -> Domain
- Application -> Domain
- Domain -> ninguna otra capa

## Bases de datos

### Legacy
La base de datos legacy será usada solo para lectura.
El sistema nuevo no puede escribir, alterar ni depender de cambios estructurales sobre esa base.

### Nueva base
La nueva base de datos será independiente y almacenará snapshots, documentos internos, resultados fiscales, cancelaciones, artefactos y trazabilidad.

## Estrategia de integración
La venta nace en el sistema legacy.
La facturación fiscal nace en el nuevo sistema.
El nuevo sistema importará snapshots de pedidos y clientes antes de emitir CFDI.

## Regla operativa clave
El pedido a facturar debe convertirse en snapshot antes del timbrado.
El CFDI debe generarse a partir de ese snapshot y no de lecturas posteriores del legacy.

## Estrategia HTTP
Se usarán Minimal APIs por simplicidad y claridad.
Los endpoints deben ser delgados y delegar todo a casos de uso de Application.

## Estrategia de persistencia
- LegacyRead: lectura controlada del MySQL legado.
- BillingWrite: escritura sobre la nueva base de datos.
- FacturaloPlus: integración HTTP aislada.

## Estrategia de crecimiento
La solución debe crecer por casos de uso pequeños y bien documentados.
Cada cambio estructural relevante debe reflejarse en la carpeta docs.

## Restricciones obligatorias
- No escribir en legacy.
- No duplicar reglas entre endpoints, servicios y repositorios.
- No mezclar reglas fiscales con infraestructura HTTP.
- No acoplar el dominio al proveedor FacturaloPlus.
