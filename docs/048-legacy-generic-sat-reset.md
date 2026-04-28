# Reset controlado de clave SAT genérica `01010101`

## Estado objetivo

El estado que la app ahora interpreta como “producto pendiente de definición fiscal” es:

- `product_fiscal_assignment.review_status = 'pending_review'`
- `product_fiscal_assignment.review_reason = 'legacy_generic_01010101_reset'`

Efecto real en aplicación:

- [`ProductFiscalProfileRepository.GetEffectiveByInternalCodeAsync`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Repositories/ProductFiscalProfileRepository.cs) devuelve `null` solo cuando el assignment efectivo está en:
  - `review_status = 'pending_review'`
  - `review_reason = 'legacy_generic_01010101_reset'`
- [`PrepareFiscalDocumentService`](/home/romanrfhack/code/Pineda.Facturacion/src/Pineda.Facturacion.Application/UseCases/FiscalDocuments/PrepareFiscalDocumentService.cs) trata ese caso como `MissingProductFiscalProfile`.
- El mismo flujo suprime autoprefill y candidatos históricos de:
  - `product_fiscal_assignment`
  - `product_fiscal_profile`
  - `billing_document_item`
  - snapshots legacy

## Estrategia de limpieza

- No se hace `DELETE`.
- No se nulifica `sat_product_service_code`.
- Si ya existe un `product_fiscal_assignment` genérico elegible, se actualiza in-place a `pending_review`.
- Si existe sólo `product_fiscal_profile` activo con `01010101` y no existe assignment efectivo, se crea un shadow assignment:
  - `source = 'product_fiscal_profile_pending_review'`
  - `confidence = 0`
  - `review_status = 'pending_review'`
  - `review_reason = 'legacy_generic_01010101_reset'`

## Exclusiones automáticas

El reset no modifica:

- `product_fiscal_assignment.source = 'product_fiscal_profile_manual'` en assignments abiertos o históricos
- `product_fiscal_assignment.source = 'product_fiscal_profile_import'` en assignments abiertos o históricos
- assignments ya marcados con:
  - `review_status = 'pending_review'`
  - `review_reason = 'legacy_generic_01010101_reset'`
- perfiles con evidencia manual en `audit_event`:
  - `ProductFiscalProfile.Create`
  - `ProductFiscalProfile.Update`
  - `ProductFiscalProfile.LegacyAssignmentApprove`
  - donde `request_summary_json` contiene `01010101`

## Persistencia de evidencia

La migración aditiva crea:

- `product_fiscal_review_cleanup_batch`
- `product_fiscal_review_cleanup_entry`

Se guarda evidencia before/after de:

- `product_fiscal_assignment`
- `product_fiscal_profile`
- `audit_event`
- hints de `billing_document_item`

## Rollback

- No hace `DELETE`.
- Para assignments ya existentes, restaura exactamente los campos respaldados por el lote.
- Para shadow assignments creados por el reset, el rollback es funcional y auditado:
  - la fila queda cerrada con `valid_to_utc`
  - deja de ser efectiva
  - la resolución efectiva vuelve al estado previo
- El shape físico no vuelve a “sin fila” para esos shadow assignments; se conserva trazabilidad del lote.

## Comandos

### Dry-run en staging

```bash
dotnet run --project src/Pineda.Facturacion.Api -- \
  reset-legacy-generic-sat-assignments \
  --requested-by=tu_usuario \
  --notes="dry-run staging legacy generic SAT reset"
```

### Dry-run en producción

```bash
dotnet run --project src/Pineda.Facturacion.Api -- \
  reset-legacy-generic-sat-assignments \
  --requested-by=tu_usuario \
  --notes="dry-run production legacy generic SAT reset"
```

### Commit en staging

```bash
dotnet run --project src/Pineda.Facturacion.Api -- \
  reset-legacy-generic-sat-assignments \
  --commit \
  --cleanup-batch-id=legacy-generic-reset-20260427-staging-01 \
  --expected-database-name=TU_DB_STAGING \
  --requested-by=tu_usuario \
  --notes="commit staging legacy generic SAT reset"
```

### Commit en producción

Condiciones previas:

- dry-run revisado y aprobado
- nombre exacto de base validado
- lote exacto documentado
- respaldo lógico guardado

```bash
ALLOW_PROD_SAT_GENERIC_RESET=true \
dotnet run --project src/Pineda.Facturacion.Api -- \
  reset-legacy-generic-sat-assignments \
  --commit \
  --cleanup-batch-id=legacy-generic-reset-20260427-prod-01 \
  --expected-database-name=TU_DB_PROD \
  --requested-by=tu_usuario \
  --notes="commit production legacy generic SAT reset"
```

### Rollback por lote

```bash
dotnet run --project src/Pineda.Facturacion.Api -- \
  rollback-legacy-generic-sat-assignments \
  --cleanup-batch-id=legacy-generic-reset-20260427-staging-01 \
  --expected-database-name=TU_DB_STAGING
```

Producción requiere además:

```bash
ALLOW_PROD_SAT_GENERIC_RESET=true
```

## Queries de diagnóstico

### 1. Totales base por tabla

```sql
SELECT
  (SELECT COUNT(*) FROM product_fiscal_profile WHERE sat_product_service_code = '01010101') AS product_fiscal_profile_generic_total,
  (SELECT COUNT(*) FROM product_fiscal_assignment WHERE sat_product_service_code = '01010101') AS product_fiscal_assignment_generic_total,
  (SELECT COUNT(*) FROM billing_document_item WHERE sat_product_service_code = '01010101') AS billing_document_item_generic_total,
  (SELECT COUNT(*) FROM sales_order_item WHERE sat_product_service_code = '01010101') AS sales_order_item_generic_total;
```

### 2. Duplicados abiertos por `internal_code`

```sql
SELECT
  internal_code,
  COUNT(*) AS open_assignment_count
FROM product_fiscal_assignment
WHERE valid_to_utc IS NULL
GROUP BY internal_code
HAVING COUNT(*) > 1
ORDER BY internal_code;
```

### 3. Assignments genéricos efectivos hoy

```sql
SELECT
  id,
  internal_code,
  source,
  review_status,
  review_reason,
  valid_from_utc,
  valid_to_utc,
  updated_at_utc
FROM product_fiscal_assignment
WHERE sat_product_service_code = '01010101'
  AND valid_from_utc <= UTC_TIMESTAMP()
  AND valid_to_utc IS NULL
ORDER BY internal_code, id;
```

### 4. Perfiles genéricos activos sin assignment efectivo

```sql
SELECT
  p.id,
  p.internal_code,
  p.sat_product_service_code,
  p.sat_unit_code,
  p.updated_at_utc
FROM product_fiscal_profile p
WHERE p.is_active = 1
  AND p.sat_product_service_code = '01010101'
  AND NOT EXISTS (
    SELECT 1
    FROM product_fiscal_assignment a
    WHERE a.internal_code = p.internal_code
      AND a.valid_from_utc <= UTC_TIMESTAMP()
      AND (a.valid_to_utc IS NULL OR a.valid_to_utc > UTC_TIMESTAMP())
  )
ORDER BY p.internal_code, p.id;
```

### 5. Clasificación real tipo dry-run

```sql
WITH open_assignments AS (
  SELECT *
  FROM product_fiscal_assignment
  WHERE valid_to_utc IS NULL
),
effective_generic_assignments AS (
  SELECT
    a.id AS product_fiscal_assignment_id,
    a.internal_code,
    a.source,
    a.review_status,
    a.review_reason,
    p.id AS product_fiscal_profile_id,
    'assignment' AS candidate_kind
  FROM product_fiscal_assignment a
  LEFT JOIN product_fiscal_profile p ON p.internal_code = a.internal_code
  WHERE a.sat_product_service_code = '01010101'
    AND a.valid_from_utc <= UTC_TIMESTAMP()
    AND a.valid_to_utc IS NULL
),
profile_only_generic AS (
  SELECT
    NULL AS product_fiscal_assignment_id,
    p.internal_code,
    NULL AS source,
    NULL AS review_status,
    NULL AS review_reason,
    p.id AS product_fiscal_profile_id,
    'profile_only' AS candidate_kind
  FROM product_fiscal_profile p
  WHERE p.is_active = 1
    AND p.sat_product_service_code = '01010101'
    AND NOT EXISTS (
      SELECT 1
      FROM product_fiscal_assignment a
      WHERE a.internal_code = p.internal_code
        AND a.valid_from_utc <= UTC_TIMESTAMP()
        AND (a.valid_to_utc IS NULL OR a.valid_to_utc > UTC_TIMESTAMP())
    )
),
candidate_pool AS (
  SELECT * FROM effective_generic_assignments
  UNION ALL
  SELECT * FROM profile_only_generic
),
manual_audit AS (
  SELECT DISTINCT ae.entity_id
  FROM audit_event ae
  WHERE ae.entity_type = 'ProductFiscalProfile'
    AND ae.action_type IN (
      'ProductFiscalProfile.Create',
      'ProductFiscalProfile.Update',
      'ProductFiscalProfile.LegacyAssignmentApprove'
    )
    AND ae.request_summary_json LIKE '%01010101%'
),
open_assignment_counts AS (
  SELECT internal_code, COUNT(*) AS open_assignment_count
  FROM open_assignments
  GROUP BY internal_code
),
historical_managed_sources AS (
  SELECT
    internal_code,
    MAX(CASE WHEN source = 'product_fiscal_profile_manual' AND valid_to_utc IS NULL THEN 1 ELSE 0 END) AS has_open_manual_source,
    MAX(CASE WHEN source = 'product_fiscal_profile_import' AND valid_to_utc IS NULL THEN 1 ELSE 0 END) AS has_open_import_source,
    MAX(CASE WHEN source = 'product_fiscal_profile_manual' AND valid_to_utc IS NOT NULL THEN 1 ELSE 0 END) AS has_historical_manual_source,
    MAX(CASE WHEN source = 'product_fiscal_profile_import' AND valid_to_utc IS NOT NULL THEN 1 ELSE 0 END) AS has_historical_import_source
  FROM product_fiscal_assignment
  WHERE internal_code IN (SELECT internal_code FROM candidate_pool)
  GROUP BY internal_code
)
SELECT
  cp.internal_code,
  cp.product_fiscal_profile_id,
  cp.product_fiscal_assignment_id,
  cp.candidate_kind,
  cp.source,
  cp.review_status,
  cp.review_reason,
  CASE
    WHEN COALESCE(hms.has_open_manual_source, 0) = 1 THEN 'skipped_open_manual_source'
    WHEN COALESCE(hms.has_open_import_source, 0) = 1 THEN 'skipped_open_import_source'
    WHEN COALESCE(hms.has_historical_manual_source, 0) = 1 THEN 'skipped_historical_manual_source'
    WHEN COALESCE(hms.has_historical_import_source, 0) = 1 THEN 'skipped_historical_import_source'
    WHEN cp.review_status = 'pending_review'
      AND cp.review_reason = 'legacy_generic_01010101_reset' THEN 'skipped_already_pending'
    WHEN ma.entity_id IS NOT NULL THEN 'skipped_manual_audit'
    WHEN cp.candidate_kind = 'profile_only' AND COALESCE(oac.open_assignment_count, 0) > 0 THEN 'skipped_open_assignment_present'
    WHEN cp.candidate_kind = 'profile_only' THEN 'eligible_create_pending_assignment'
    ELSE 'eligible_update'
  END AS planned_outcome
FROM candidate_pool cp
LEFT JOIN manual_audit ma ON ma.entity_id = CAST(cp.product_fiscal_profile_id AS CHAR)
LEFT JOIN open_assignment_counts oac ON oac.internal_code = cp.internal_code
LEFT JOIN historical_managed_sources hms ON hms.internal_code = cp.internal_code
ORDER BY cp.internal_code, cp.product_fiscal_assignment_id;
```

### 6. Totales por exclusión y elegibilidad

```sql
WITH classified AS (
  WITH open_assignments AS (
    SELECT *
    FROM product_fiscal_assignment
    WHERE valid_to_utc IS NULL
  ),
  effective_generic_assignments AS (
    SELECT
      a.id AS product_fiscal_assignment_id,
      a.internal_code,
      a.source,
      a.review_status,
      a.review_reason,
      p.id AS product_fiscal_profile_id,
      'assignment' AS candidate_kind
    FROM product_fiscal_assignment a
    LEFT JOIN product_fiscal_profile p ON p.internal_code = a.internal_code
    WHERE a.sat_product_service_code = '01010101'
      AND a.valid_from_utc <= UTC_TIMESTAMP()
      AND a.valid_to_utc IS NULL
  ),
  profile_only_generic AS (
    SELECT
      NULL AS product_fiscal_assignment_id,
      p.internal_code,
      NULL AS source,
      NULL AS review_status,
      NULL AS review_reason,
      p.id AS product_fiscal_profile_id,
      'profile_only' AS candidate_kind
    FROM product_fiscal_profile p
    WHERE p.is_active = 1
      AND p.sat_product_service_code = '01010101'
      AND NOT EXISTS (
        SELECT 1
        FROM product_fiscal_assignment a
        WHERE a.internal_code = p.internal_code
          AND a.valid_from_utc <= UTC_TIMESTAMP()
          AND (a.valid_to_utc IS NULL OR a.valid_to_utc > UTC_TIMESTAMP())
      )
  ),
  candidate_pool AS (
    SELECT * FROM effective_generic_assignments
    UNION ALL
    SELECT * FROM profile_only_generic
  ),
  manual_audit AS (
    SELECT DISTINCT ae.entity_id
    FROM audit_event ae
    WHERE ae.entity_type = 'ProductFiscalProfile'
      AND ae.action_type IN (
        'ProductFiscalProfile.Create',
        'ProductFiscalProfile.Update',
        'ProductFiscalProfile.LegacyAssignmentApprove'
      )
      AND ae.request_summary_json LIKE '%01010101%'
  ),
  open_assignment_counts AS (
    SELECT internal_code, COUNT(*) AS open_assignment_count
    FROM open_assignments
    GROUP BY internal_code
  ),
  historical_managed_sources AS (
    SELECT
      internal_code,
      MAX(CASE WHEN source = 'product_fiscal_profile_manual' AND valid_to_utc IS NULL THEN 1 ELSE 0 END) AS has_open_manual_source,
      MAX(CASE WHEN source = 'product_fiscal_profile_import' AND valid_to_utc IS NULL THEN 1 ELSE 0 END) AS has_open_import_source,
      MAX(CASE WHEN source = 'product_fiscal_profile_manual' AND valid_to_utc IS NOT NULL THEN 1 ELSE 0 END) AS has_historical_manual_source,
      MAX(CASE WHEN source = 'product_fiscal_profile_import' AND valid_to_utc IS NOT NULL THEN 1 ELSE 0 END) AS has_historical_import_source
    FROM product_fiscal_assignment
    WHERE internal_code IN (SELECT internal_code FROM candidate_pool)
    GROUP BY internal_code
  )
  SELECT
    CASE
      WHEN COALESCE(hms.has_open_manual_source, 0) = 1 THEN 'skipped_open_manual_source'
      WHEN COALESCE(hms.has_open_import_source, 0) = 1 THEN 'skipped_open_import_source'
      WHEN COALESCE(hms.has_historical_manual_source, 0) = 1 THEN 'skipped_historical_manual_source'
      WHEN COALESCE(hms.has_historical_import_source, 0) = 1 THEN 'skipped_historical_import_source'
      WHEN cp.review_status = 'pending_review'
        AND cp.review_reason = 'legacy_generic_01010101_reset' THEN 'skipped_already_pending'
      WHEN ma.entity_id IS NOT NULL THEN 'skipped_manual_audit'
      WHEN cp.candidate_kind = 'profile_only' AND COALESCE(oac.open_assignment_count, 0) > 0 THEN 'skipped_open_assignment_present'
      WHEN cp.candidate_kind = 'profile_only' THEN 'eligible_create_pending_assignment'
      ELSE 'eligible_update'
    END AS planned_outcome
  FROM candidate_pool cp
  LEFT JOIN manual_audit ma ON ma.entity_id = CAST(cp.product_fiscal_profile_id AS CHAR)
  LEFT JOIN open_assignment_counts oac ON oac.internal_code = cp.internal_code
  LEFT JOIN historical_managed_sources hms ON hms.internal_code = cp.internal_code
)
SELECT planned_outcome, COUNT(*) AS total
FROM classified
GROUP BY planned_outcome
ORDER BY planned_outcome;
```

### 7. Uso en facturas y CFDI timbrados

```sql
SELECT COUNT(DISTINCT product_internal_code) AS products_used_in_billing_documents
FROM billing_document_item
WHERE product_internal_code IN (
  SELECT internal_code
  FROM product_fiscal_profile
  WHERE sat_product_service_code = '01010101'
  UNION
  SELECT internal_code
  FROM product_fiscal_assignment
  WHERE sat_product_service_code = '01010101'
);
```

```sql
SELECT
  COUNT(DISTINCT fdi.internal_code) AS products_used_in_stamped_cfdi,
  COUNT(DISTINCT fs.fiscal_document_id) AS stamped_cfdi_count
FROM fiscal_document_item fdi
JOIN fiscal_stamp fs ON fs.fiscal_document_id = fdi.fiscal_document_id
WHERE fs.uuid IS NOT NULL
  AND fs.uuid <> ''
  AND fdi.internal_code IN (
    SELECT internal_code
    FROM product_fiscal_profile
    WHERE sat_product_service_code = '01010101'
    UNION
    SELECT internal_code
    FROM product_fiscal_assignment
    WHERE sat_product_service_code = '01010101'
  );
```

### 8. Ya convertidos a pending review por este flujo

```sql
SELECT
  internal_code,
  id,
  source,
  review_status,
  review_reason,
  valid_from_utc,
  valid_to_utc,
  updated_at_utc
FROM product_fiscal_assignment
WHERE review_status = 'pending_review'
  AND review_reason = 'legacy_generic_01010101_reset'
ORDER BY internal_code, id;
```

## Validación posterior

- `PrepareFiscalDocument` debe regresar `MissingProductFiscalProfile` para los `internal_code` afectados.
- `existingProfileStatus` debe llegar como `PendingReview`.
- `Prefill.SatProductServiceCode` no debe seguir en `01010101`.
- `billing_document_item.sat_product_service_code = '01010101'` ya no debe dominar sugerencias para esos casos.
- `fiscal_document_item` y `fiscal_stamp.xml_content` deben permanecer invariantes.
