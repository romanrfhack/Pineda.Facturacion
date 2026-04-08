# Catalogo Fiscal SAT: propuesta aterrizada al repo

Fecha: 2026-04-07

Estado: aterrizado a código y migration EF Core, sin romper contratos actuales.

## Resumen ejecutivo
- No se crea una segunda tabla canónica de clave SAT producto/servicio. La fuente canónica sigue siendo [`sat_product_service_catalog`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Configurations/SatProductServiceCatalogEntryConfiguration.cs).
- Sí se crean `sat_catalog_imports`, `sat_clave_unidad` y `product_fiscal_assignment`.
- No se introduce `product_id`. El identificador operativo sigue siendo `internal_code`.
- `product_fiscal_profile` se conserva como contrato actual de alta/edición/importación. Ahora hace dual-write hacia `product_fiscal_assignment`.
- `PrepareFiscalDocumentService` y los rebuild flows dejan de depender semánticamente del `product_fiscal_profile` vigente cuando ya existe `fiscal_document`.
- Timbrado sigue leyendo sólo `fiscal_document` y `fiscal_document_item`.
- PDF sigue saliendo de `fiscal_stamp.xml_content`.

## Mapa actual del esquema

### Donde vive hoy cada cosa
- Catálogo operativo de producto fiscal: [`product_fiscal_profile`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Configurations/ProductFiscalProfileConfiguration.cs)
- Catálogo SAT producto/servicio: [`sat_product_service_catalog`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Configurations/SatProductServiceCatalogEntryConfiguration.cs)
- Clave SAT de unidad: hoy no existe tabla canónica; sólo copias en `sales_order_item`, `billing_document_item`, `fiscal_document_item` y `product_fiscal_profile`
- Conceptos comerciales: [`billing_document_item`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Configurations/BillingDocumentItemConfiguration.cs)
- Conceptos fiscales snapshot: [`fiscal_document_item`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Configurations/FiscalDocumentItemConfiguration.cs)
- XML timbrado: `fiscal_stamp.xml_content`
- JSON fiscal persistido: no existe request JSON persistido; sólo `provider_request_hash` y `raw_response_summary_json`
- PDF: render on demand desde XML timbrado en [`FiscalDocumentPdfRenderer`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure/Documents/FiscalDocumentPdfRenderer.cs)

### Snapshot actual
- `billing_document_item` ya guarda `description`, `sat_product_service_code`, `sat_unit_code`, `tax_rate`, `tax_amount`, `tax_object_code`.
- `fiscal_document_item` ya guarda `description`, `sat_product_service_code`, `sat_unit_code`, `tax_object_code`, `vat_rate`, `unit_text`.
- No hay FK desde conceptos a `product_fiscal_profile`. El riesgo actual no es una FK dura; es una resolución lógica en services.

## A. Diseño final aterrizado al repo

### Decisión 1: reutilizar `sat_product_service_catalog`
- Se mantiene como tabla canónica de prodserv.
- No se crea `sat_clave_prodserv`.
- La compatibilidad futura de import oficial SAT se resuelve cargando sobre la misma tabla.

### Decisión 2: crear tablas nuevas mínimas
- `sat_catalog_imports`: bitácora de importación oficial SAT.
- `sat_clave_unidad`: catálogo canónico de clave SAT de unidad.
- `product_fiscal_assignment`: historial versionado por `internal_code`.

### Decisión 3: conservar `product_fiscal_profile`
- No se elimina ni se renombra.
- Sigue siendo la API operativa actual para endpoints e imports existentes.
- Ahora también sincroniza una fila efectiva en `product_fiscal_assignment`.

### Decisión 4: cerrar el riesgo funcional real
- Antes:
  - [`PrepareFiscalDocumentService`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/FiscalDocuments/PrepareFiscalDocumentService.cs) resolvía SAT/impuestos desde `product_fiscal_profile` vigente.
  - Los rebuilds de [`AssignPendingBillingItemsService`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/BillingDocuments/AssignPendingBillingItemsService.cs), [`RemoveBillingDocumentItemService`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/BillingDocuments/RemoveBillingDocumentItemService.cs), [`UpdateBillingDocumentOrderAssociationService`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/BillingDocuments/UpdateBillingDocumentOrderAssociationService.cs) y [`ReimportLegacyOrderService`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/ImportLegacyOrder/ReimportLegacyOrderService.cs) volvían a tomar SAT/impuestos del catálogo vigente.
- Ahora:
  - `PrepareFiscalDocumentService` usa una lectura efectiva: `product_fiscal_assignment` primero y `product_fiscal_profile` sólo como fallback.
  - Si ya existe `fiscal_document`, los rebuilds preservan la semántica SAT/impuestos por llave lógica de línea:
    - `SOI:{sales_order_item_id}`
    - `REM:{source_billing_document_item_removal_id}`
  - Si la línea es nueva y no tiene snapshot fiscal previo, se resuelve desde la asignación efectiva vigente.

### Decisión 5: timbrado y PDF sin cambios
- [`StampFiscalDocumentService`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/FiscalDocuments/StampFiscalDocumentService.cs) sigue leyendo sólo `fiscal_document` y `fiscal_document_item`.
- [`GetFiscalDocumentPdfService`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/FiscalDocuments/GetFiscalDocumentPdfService.cs) sigue requiriendo `fiscal_stamp.xml_content`.

## B. Lista exacta de entidades/tablas/columnas

### Nuevas entidades/tablas
- [`ProductFiscalAssignment`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Domain/Entities/ProductFiscalAssignment.cs)
  - `id`
  - `internal_code`
  - `sat_product_service_code`
  - `sat_unit_code`
  - `tax_object_code`
  - `vat_rate`
  - `default_unit_text`
  - `source`
  - `confidence`
  - `review_status`
  - `review_reason`
  - `valid_from_utc`
  - `valid_to_utc`
  - `created_at_utc`
  - `updated_at_utc`
- [`SatCatalogImport`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Domain/Entities/SatCatalogImport.cs)
  - `id`
  - `catalog_type`
  - `source_file_name`
  - `source_format`
  - `source_version`
  - `source_checksum`
  - `status`
  - `total_rows`
  - `inserted_rows`
  - `updated_rows`
  - `deactivated_rows`
  - `error_message`
  - `created_at_utc`
  - `completed_at_utc`
- [`SatClaveUnidad`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Domain/Entities/SatClaveUnidad.cs)
  - `code`
  - `description`
  - `normalized_description`
  - `symbol`
  - `notes`
  - `is_active`
  - `source_version`
  - `created_at_utc`
  - `updated_at_utc`

### Tablas existentes modificadas
- Ningún cambio de esquema en `product_fiscal_profile`.
- Ningún cambio de esquema en `billing_document_item`.
- Ningún cambio de esquema en `fiscal_document_item`.
- Ningún cambio de esquema en `sat_product_service_catalog`.
- Sí cambia el `DbContext` para mapear las nuevas tablas en [`BillingDbContext`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/BillingDbContext.cs).

## C. Migraciones EF Core reales
- Migration generada: [`20260407223146_AddFiscalAssignmentCatalogs.cs`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Migrations/20260407223146_AddFiscalAssignmentCatalogs.cs)
- Designer generado: [`20260407223146_AddFiscalAssignmentCatalogs.Designer.cs`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Migrations/20260407223146_AddFiscalAssignmentCatalogs.Designer.cs)
- Snapshot actualizado: [`BillingDbContextModelSnapshot.cs`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Migrations/BillingDbContextModelSnapshot.cs)

### Nota importante de la migration
- La migration hace backfill inicial desde `product_fiscal_profile` hacia `product_fiscal_assignment`.
- `source = 'product_fiscal_profile_backfill'`
- `confidence = 1.0000`
- `review_status = 'approved'`
- Esto permite salir a producción en fase dual-read sin intervención manual inmediata.

## D. Cambios exactos de aplicación

### Repositorio de producto fiscal
- [`IProductFiscalProfileRepository.cs`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/Abstractions/Persistence/IProductFiscalProfileRepository.cs)
  - nuevo `GetEffectiveByInternalCodeAsync(...)`
  - nuevo `EnsureEffectiveAssignmentAsync(...)`
- [`ProductFiscalProfileRepository.cs`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Repositories/ProductFiscalProfileRepository.cs)
  - lee `product_fiscal_assignment` primero
  - cae a `product_fiscal_profile` si todavía no existe assignment
  - versiona o actualiza la fila abierta de `product_fiscal_assignment`

### Dual-write desde la operación actual
- [`CreateProductFiscalProfileService`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/ProductFiscalProfiles/CreateProductFiscalProfileService.cs)
- [`UpdateProductFiscalProfileService`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/ProductFiscalProfiles/UpdateProductFiscalProfileService.cs)
- [`ApplyProductFiscalProfileImportBatchService`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/ProductFiscalProfiles/ApplyProductFiscalProfileImportBatchService.cs)
- Convenciones usadas: [`ProductFiscalAssignmentConventions.cs`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/ProductFiscalProfiles/ProductFiscalAssignmentConventions.cs)

### Cierre del riesgo de rebuild
- Helpers nuevos:
  - [`BillingDocumentItemFiscalSemanticKey.cs`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/Common/BillingDocumentItemFiscalSemanticKey.cs)
  - [`FiscalDocumentItemCompositionBuilder.cs`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/Common/FiscalDocumentItemCompositionBuilder.cs)
  - [`BillingDocumentItemCompositionApplier.cs`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/Common/BillingDocumentItemCompositionApplier.cs)
- Servicios tocados:
  - [`PrepareFiscalDocumentService`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/FiscalDocuments/PrepareFiscalDocumentService.cs)
  - [`AssignPendingBillingItemsService`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/BillingDocuments/AssignPendingBillingItemsService.cs)
  - [`RemoveBillingDocumentItemService`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/BillingDocuments/RemoveBillingDocumentItemService.cs)
  - [`UpdateBillingDocumentOrderAssociationService`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/BillingDocuments/UpdateBillingDocumentOrderAssociationService.cs)
  - [`ReimportLegacyOrderService`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/ImportLegacyOrder/ReimportLegacyOrderService.cs)

### Interferencia de `SatProductServiceCatalogBootstrapHostedService`
- Sí había interferencia potencial.
- Antes podía pisar `sat_product_service_catalog` con el JSON embebido aunque ya existiera una carga oficial distinta.
- Se corrigió en [`SatProductServiceCatalogBootstrapHostedService.cs`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure/Security/SatProductServiceCatalogBootstrapHostedService.cs):
  - si existe un import `completed` para `sat_product_service` en `sat_catalog_imports`, el bootstrap embebido se omite
- Resultado:
  - greenfield vacío: sigue sembrando catálogo embebido
  - ambiente ya migrado a import oficial: no pisa la fuente canónica

## E. Pruebas

### Nuevas o ajustadas
- [`FiscalDocumentServicesTests.cs`](/home/romanrfhack/code/Pineda.Facturacion/tests/Pineda.Facturacion.UnitTests/FiscalDocumentServicesTests.cs)
  - valida que `PrepareFiscalDocumentService` use la lectura efectiva
- [`FiscalMasterDataServicesTests.cs`](/home/romanrfhack/code/Pineda.Facturacion/tests/Pineda.Facturacion.UnitTests/FiscalMasterDataServicesTests.cs)
  - valida dual-write desde create
- [`FiscalImportApplyServicesTests.cs`](/home/romanrfhack/code/Pineda.Facturacion/tests/Pineda.Facturacion.UnitTests/FiscalImportApplyServicesTests.cs)
  - valida dual-write desde import apply
- [`ReimportLegacyOrderServiceTests.cs`](/home/romanrfhack/code/Pineda.Facturacion/tests/Pineda.Facturacion.UnitTests/ReimportLegacyOrderServiceTests.cs)
  - valida preservación de semántica fiscal al reconstruir
- [`SatProductServiceCatalogBootstrapHostedServiceTests.cs`](/home/romanrfhack/code/Pineda.Facturacion/tests/Pineda.Facturacion.UnitTests/SatProductServiceCatalogBootstrapHostedServiceTests.cs)
  - valida que el bootstrap se salte si ya existe import oficial

### Ejecución realizada
- `dotnet build src/Pineda.Facturacion.Api/Pineda.Facturacion.Api.csproj`
- `dotnet test tests/Pineda.Facturacion.UnitTests/Pineda.Facturacion.UnitTests.csproj`
- Resultado: `331/331` pruebas unitarias pasaron.
- No se agregaron integration tests nuevas en esta fase; el cambio se cerró con migration real, unit tests sobre servicios/repositorio/hosted service y compatibilidad aditiva de esquema.

## F. Plan de despliegue por fases

### Fase 1: schema aditivo
- aplicar migration [`20260407223146_AddFiscalAssignmentCatalogs.cs`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Migrations/20260407223146_AddFiscalAssignmentCatalogs.cs)
- no tocar CFDI ya timbrados
- validar que `product_fiscal_assignment` quedó backfilleada

### Fase 2: dual-read / dual-write
- desplegar la aplicación con estos cambios
- nuevos cambios a `product_fiscal_profile` seguirán versionando `product_fiscal_assignment`
- `PrepareFiscalDocumentService` y rebuilds ya operan con preservación de semántica

### Fase 3: carga oficial SAT
- cargar hojas oficiales SAT a staging externo o ETL
- registrar ejecución en `sat_catalog_imports`
- upsert a `sat_product_service_catalog`
- upsert a `sat_clave_unidad`
- dejar `sat_product_service_catalog` como fuente canónica única de prodserv

### Fase 4: backfill operativo guiado
- revisar productos sin assignment o con baja confianza
- asignar `source`, `confidence`, `review_status`, `review_reason`
- dejar cola manual por `review_status`

## G. Plan de rollback

### Rollback de aplicación
- simple mientras sólo se haya hecho Fase 1/Fase 2:
  - desplegar binario anterior
  - las tablas nuevas son aditivas y el código anterior las ignora
- no se toca ningún CFDI timbrado

### Rollback de base
- opcional y sólo si no hay consumidores de las tablas nuevas
- revertir la migration [`20260407223146_AddFiscalAssignmentCatalogs.cs`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Migrations/20260407223146_AddFiscalAssignmentCatalogs.cs)
- como no se alteraron `billing_document_item`, `fiscal_document_item`, `product_fiscal_profile` ni `fiscal_stamp`, el rollback no afecta documentos ya preparados o timbrados

### Restricción importante
- después de Fase 3 no conviene volver a una versión anterior al guard de [`SatProductServiceCatalogBootstrapHostedService.cs`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure/Security/SatProductServiceCatalogBootstrapHostedService.cs), porque ese binario antiguo podría reescribir `sat_product_service_catalog` con el JSON embebido

## H. Riesgos técnicos y mitigaciones
- Riesgo: líneas reconstruidas históricamente ya podían tener `billing_document_item_id = null`.
  - Mitigación: el preservador usa match por `billing_document_item_id` cuando existe, y si no por `line_number` y ordinal como compatibilidad.
- Riesgo: no hay constraint SQL que impida dos filas abiertas para el mismo `internal_code`.
  - Mitigación: el repositorio asegura cierre de la fila abierta antes de insertar una nueva. Se dejó índice único por `(internal_code, valid_from_utc)` y consulta de fila abierta por `valid_to_utc is null`.
- Riesgo: un rename de `internal_code` no tiene `product_id` estable detrás.
  - Mitigación: en esta fase se privilegia compatibilidad. La asignación previa queda resoluble para documentos viejos y el nuevo código obtiene su propia asignación.
- Riesgo: agregar FKs duras a catálogos SAT en esta fase podría bloquear backfill o imports parciales.
  - Mitigación: no se agregaron FKs todavía; primero se estabiliza catálogo y asignación.
- Riesgo: el bootstrap embebido de prodserv interfiera con la fuente canónica importada.
  - Mitigación: ya quedó guardado por `sat_catalog_imports`.

## Archivos clave de la implementación
- [`IProductFiscalProfileRepository.cs`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/Abstractions/Persistence/IProductFiscalProfileRepository.cs)
- [`ProductFiscalProfileRepository.cs`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Repositories/ProductFiscalProfileRepository.cs)
- [`20260407223146_AddFiscalAssignmentCatalogs.cs`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Migrations/20260407223146_AddFiscalAssignmentCatalogs.cs)
- [`PrepareFiscalDocumentService.cs`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/FiscalDocuments/PrepareFiscalDocumentService.cs)
- [`AssignPendingBillingItemsService.cs`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/BillingDocuments/AssignPendingBillingItemsService.cs)
- [`RemoveBillingDocumentItemService.cs`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/BillingDocuments/RemoveBillingDocumentItemService.cs)
- [`UpdateBillingDocumentOrderAssociationService.cs`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/BillingDocuments/UpdateBillingDocumentOrderAssociationService.cs)
- [`ReimportLegacyOrderService.cs`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/ImportLegacyOrder/ReimportLegacyOrderService.cs)
