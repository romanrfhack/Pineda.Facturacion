# Auditoria REP / Cuentas por cobrar faltantes

## Resumen ejecutivo

- El endpoint `GET /api/payment-complements/attention-items` no parte de Cuentas por Cobrar. Parte del universo de `fiscal_document` internos de tipo `I` y despues evalua elegibilidad REP. Por eso un CFDI puede aparecer en `attention-items` aunque no exista `accounts_receivable_invoice`.
- `fiscalDocumentId = 262` aparece porque el repositorio interno de REP incluye cualquier `fiscal_document.document_type = 'I'` que cumpla filtros de fecha/RFC/query. Luego la regla de elegibilidad lo marca `Blocked` con `AccountsReceivableMissing` cuando no encuentra `accounts_receivable_invoice`.
- El mismo documento no aparece en Cuentas por Cobrar porque el portafolio de AR inicia en `accounts_receivable_invoice`, no en `fiscal_document`.
- Hay una divergencia de negocio importante en el codigo actual:
  - La bandeja REP interna exige `PPD/99`, UUID, no cancelado, MXN y AR existente.
  - La autocreacion de AR despues del timbrado exige ademas `is_credit_sale = 1` y `credit_days > 0`.
  - Por tanto, un CFDI `PPD/99` con `is_credit_sale = 0` puede aparecer bloqueado para REP y, al mismo tiempo, omitir correctamente la autocreacion de AR segun el codigo actual.
- Para `fiscalDocumentId IN (768, 539)`, la explicacion mas fuerte hoy es una discrepancia de precision:
  - `fiscal_document.total` conserva 6 decimales.
  - `accounts_receivable_invoice.total`, `paid_total` y `outstanding_balance` se normalizan operativamente a 2 decimales.
  - La regla REP compara `ari.outstanding_balance > fd.total` usando el total fiscal crudo. Casos como `736.999999` vs `737.00` y `415.999998` vs `416.00` disparan `OperationalBalanceInconsistent` aunque AR este consistente en su propia aritmetica operativa.

## Por que fiscalDocumentId 262 aparece en attention-items

### Endpoint y filtros

- Handler exacto: `src/Pineda.Facturacion.Api/Endpoints/PaymentComplementsEndpoints.cs:478-525`
- Mapeo de ruta: `src/Pineda.Facturacion.Api/Endpoints/PaymentComplementsEndpoints.cs:108-112`
- Parametros soportados:
  - `page`
  - `pageSize`
  - `fromDate`
  - `toDate`
  - `receiverRfc`
  - `query`
  - `sourceType`
  - `alertCode`
  - `severity`
  - `nextRecommendedAction`
- Normalizacion relevante:
  - `page` minimo efectivo = `1`
  - `pageSize` por default = `25`
  - `pageSize` maximo efectivo = `50`
  - `fromDate > toDate` devuelve `400`

### Servicio que construye attention-items

- Servicio exacto: `src/Pineda.Facturacion.Application/UseCases/PaymentComplements/SearchRepAttentionItemsService.cs:6-223`
- Flujo:
  - Si `sourceType` es vacio, incluye internos y externos.
  - Para internos llama `IRepBaseDocumentRepository.SearchInternalAsync(...)`.
  - Para externos llama `IExternalRepBaseDocumentRepository.SearchOperationalAsync(...)`.
  - Solo sobreviven items con al menos una alerta de atencion (`MatchesAttentionFilter`).

### Como decide incluir CFDI internos

- Repositorio exacto: `src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Repositories/RepBaseDocumentRepository.cs:17-55, 250-359`
- Regla de universo interno:
  - `where fiscalDocument.DocumentType == "I"` en `BuildSummaryQuery()` (`:262`)
- Filtros reales:
  - `fromDate` / `toDate` sobre `fiscal_document.issued_at_utc`
  - `receiverRfc` con `Contains`, no igualdad exacta (`:35-39`)
  - `query` contra RFC receptor, razon social, serie, folio o UUID (`:41-50`)

### Como calcula sourceType, sourceId, operationalStatus, primaryReasonCode, availableActions y outstandingBalance

- Mapeo interno exacto: `src/Pineda.Facturacion.Application/UseCases/PaymentComplements/SearchRepAttentionItemsService.cs:121-148`
- Valores:
  - `sourceType = "Internal"`
  - `sourceId = item.FiscalDocumentId`
  - `fiscalDocumentId = item.FiscalDocumentId`
  - `billingDocumentId = item.BillingDocumentId`
  - `operationalStatus = item.RepOperationalStatus`
  - `primaryReasonCode = item.Eligibility.PrimaryReasonCode`
  - `primaryReasonMessage = item.Eligibility.PrimaryReasonMessage`
  - `availableActions = item.AvailableActions`
  - `outstandingBalance = item.OutstandingBalance`
- Origen de `outstandingBalance`:
  - `accounts_receivable_invoice.outstanding_balance` si existe AR
  - `0m` si no existe AR (`RepBaseDocumentRepository.cs:282-284`)

### Como se construye el item interno REP

- Servicio exacto: `src/Pineda.Facturacion.Application/UseCases/PaymentComplements/SearchInternalRepBaseDocumentsService.cs:83-147`
- `BuildListItem(...)` usa:
  - `InternalRepBaseDocumentEligibilityRule.Evaluate(...)`
  - `InternalRepOperationalInsightBuilder.Build(...)`

### Por que 262 aparece especificamente

Con el codigo actual, `262` aparece si y solo si:

1. Existe en `fiscal_document`.
2. `document_type = 'I'`.
3. Coincide con los filtros enviados al endpoint.
4. Genera al menos una alerta de atencion.

Para `262`, el motivo mas directo es:

1. Entra al universo interno por ser `document_type = 'I'`.
2. No tiene `accounts_receivable_invoice`.
3. La regla REP lo marca `Blocked` con `AccountsReceivableMissing`.
4. `Blocked` genera alerta `BlockedOperation`.
5. `attention-items` solo muestra documentos con alertas de atencion; por eso queda visible.

## Por que esta bloqueado con AccountsReceivableMissing

### Regla exacta

- Regla exacta: `src/Pineda.Facturacion.Application/UseCases/PaymentComplements/InternalRepBaseDocumentEligibilityRule.cs:28-109`
- Orden real de evaluacion:
  1. `DocumentTypeNotIncome`
  2. `FiscalDocumentCancelled`
  3. `FiscalCancellationPending`
  4. `FiscalDocumentNotStamped`
  5. `MissingStampedUuid`
  6. `PaymentMethodNotPpd`
  7. `PaymentFormNot99`
  8. `CurrencyNotSupported`
  9. `AccountsReceivableMissing`
  10. `AccountsReceivableCancelled`
  11. `InvalidDocumentTotal`
  12. `OperationalBalanceInconsistent`
  13. `NoOutstandingBalance`
  14. `EligibleInternalRep`

### Condicion exacta de AccountsReceivableMissing

- `if (!snapshot.HasAccountsReceivableInvoice) return Blocked("AccountsReceivableMissing", ...)`
- Referencia: `InternalRepBaseDocumentEligibilityRule.cs:70-73`

### OperationalStatus, BlockedOperation y nextRecommendedAction

- `RepOperationalStatus = evaluation.Status.ToString()` en `SearchInternalRepBaseDocumentsService.cs:123`
- Cuando `evaluation.IsBlocked = true`:
  - `HasBlockedOperation = true`
  - Se agrega alerta bloqueante con `RepOperationalAlertCatalog.CreateBlockedAlert(...)`
  - Referencia: `InternalRepOperationalInsightBuilder.cs:52-56`
- `AccountsReceivableMissing` y `OperationalBalanceInconsistent` no tienen mapeo especial de alerta:
  - ambos terminan en `RepOperationalAlertCode.BlockedOperation`
  - referencia: `RepOperationalAlertCatalog.cs:41-64`
- `nextRecommendedAction = Blocked` cuando `hasBlockedOperation = true`
  - referencia: `InternalRepOperationalInsightBuilder.cs:121-124`

### AvailableActions

- Las acciones parten siempre con `ViewDetail`
- `OpenInternalWorkflow`, `RegisterPayment`, `PrepareRep` y `StampRep` solo se agregan si no hay bloqueo o si el documento es elegible
- Referencia: `InternalRepOperationalInsightBuilder.cs:17-50`
- Si `262` no tiene AR y no tiene REP ya timbrado/cancelable, lo esperable es:
  - `availableActions = [ViewDetail]`

## Por que no aparece en Cuentas por Cobrar

- Servicio: `src/Pineda.Facturacion.Application/UseCases/AccountsReceivable/SearchAccountsReceivablePortfolioService.cs:18-97`
- Repositorio: `src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Repositories/AccountsReceivableInvoiceRepository.cs:105-215`
- El portafolio inicia en:
  - `from invoice in _dbContext.AccountsReceivableInvoices.AsNoTracking()`
- Implicacion:
  - si no existe fila en `accounts_receivable_invoice`, no hay forma de que `262` aparezca en la vista de Cuentas por Cobrar.

## Repositorios y tablas reales involucradas

### CFDI / snapshot fiscal

- `fiscal_document`
- `fiscal_document_item`
- `fiscal_stamp`
- `fiscal_cancellation`
- `fiscal_receiver`
- `issuer_profile`

### Documento comercial origen

- `billing_document`
- `billing_document_item`

### Cuentas por cobrar

- `accounts_receivable_invoice`
- `accounts_receivable_payment`
- `accounts_receivable_payment_application`
- `collection_commitment`
- `collection_note`

### REP / complementos de pago

- `payment_complement_document`
- `payment_complement_payment`
- `payment_complement_related_document`
- `payment_complement_stamp`
- `payment_complement_cancellation`
- `external_rep_base_document`
- `internal_rep_base_document_state`

### Auditoria

- `audit_event`

### Nota importante sobre nombres reales

- La tabla real no es `payment_complement_document_related_document`.
- El nombre real en el repo es `payment_complement_related_document`.

## Servicio que deberia crear la cuenta por cobrar

### Servicios exactos

- `src/Pineda.Facturacion.Application/UseCases/AccountsReceivable/EnsureAccountsReceivableInvoiceForFiscalDocumentService.cs`
- `src/Pineda.Facturacion.Application/UseCases/AccountsReceivable/CreateAccountsReceivableInvoiceFromFiscalDocumentService.cs`

### Cuando se llama automaticamente

- Despues de un timbrado exitoso:
  - `FiscalDocumentsEndpoints.cs:312-313`
  - `FiscalDocumentsEndpoints.cs:504-507`
  - helper: `FiscalDocumentsEndpoints.cs:1265-1315`
- Tambien existe endpoint manual para crear/asegurar:
  - `AccountsReceivableEndpoints.cs:154-198`
  - `AccountsReceivableEndpoints.cs:228-268`

### Condiciones reales exigidas por Ensure/Create

- `EnsureAccountsReceivableInvoiceForFiscalDocumentService.ShouldEnsure(...)` exige:
  - `fiscal_document.status = Stamped`
  - `is_credit_sale = true`
  - `payment_method_sat = 'PPD'`
  - `payment_form_sat = '99'`
  - referencia: `EnsureAccountsReceivableInvoiceForFiscalDocumentService.cs:100-128`
- `CreateAccountsReceivableInvoiceFromFiscalDocumentService` ademas exige:
  - no existir AR previa
  - `fiscal_stamp.uuid` persistido
  - `currency_code = 'MXN'`
  - `credit_days > 0`
  - referencia: `CreateAccountsReceivableInvoiceFromFiscalDocumentService.cs:50-95`

### Si se llama despues de timbrar

- Si, pero solo si el outcome del timbrado fue `Stamped`
- El helper siempre deja auditoria:
  - accion `AccountsReceivableInvoice.EnsureAfterFiscalStamp`
  - outcome posible: `Created`, `AlreadyExists`, `Skipped`, `ValidationFailed`, `UnexpectedError`

### Si solo aplica a PPD

- Si. El flujo AR actual aplica solo a PPD/99 y ademas a venta a credito.
- `PrepareFiscalDocumentService` permite una combinacion relevante:
  - `PPD` exige `99`
  - pero `PPD/99` no obliga por si solo a `is_credit_sale = true`
  - solo cuando `command.IsCreditSale` es `true` se exige `credit_days > 0`
  - referencia: `PrepareFiscalDocumentService.cs:112-139`

## Explicacion preliminar para fiscalDocumentId 262

Con evidencia de codigo, `262` debio generar AR automaticamente solo si al momento de timbrarse cumplia todas estas condiciones persistidas:

1. `status = Stamped`
2. `is_credit_sale = 1`
3. `payment_method_sat = 'PPD'`
4. `payment_form_sat = '99'`
5. `credit_days > 0`
6. `currency_code = 'MXN'`
7. `fiscal_stamp.uuid` no vacio
8. No existir ya una `accounts_receivable_invoice`

Lo que no puede afirmarse todavia sin SQL:

- si `262` realmente cumple `is_credit_sale = 1`
- si `credit_days` es valido
- si hubo intento automatico y se salto por `Skipped` o `ValidationFailed`
- si existe algun REP/pago historico que vuelva inseguro el backfill

La hipotesis mas fuerte si `262` es `PPD/99` pero no tiene AR es una de estas dos:

1. `is_credit_sale = 0` o `credit_days` invalido, por lo que la bandeja REP lo considera base interna pero el flujo AR actual lo omite.
2. El documento si era elegible para AR y hubo una brecha operativa o historica que impidio crear la fila.

## Queries SQL de diagnostico para fiscalDocumentId 262

### Notas

- Todas las queries son `SELECT`.
- Estan escritas para el esquema real del repo.
- No imprimen `xml_content`; solo `CHAR_LENGTH(xml_content)`.
- Si el motor es MySQL/MariaDB, estas queries son compatibles con la sintaxis observada en el repo.

### Status ids utiles

```sql
-- fiscal_document.status
-- 0 Draft
-- 1 ReadyForStamping
-- 2 StampingRequested
-- 3 Stamped
-- 4 StampingRejected
-- 5 CancellationRequested
-- 6 Cancelled
-- 7 CancellationRejected
-- 8 DiscardedUnstamped

-- accounts_receivable_invoice.status
-- 0 Open
-- 1 PartiallyPaid
-- 2 Paid
-- 3 Overpaid
-- 4 Cancelled

-- payment_complement_document.status
-- 0 Draft
-- 1 ReadyForStamping
-- 2 StampingRequested
-- 3 Stamped
-- 4 StampingRejected
-- 5 CancellationRequested
-- 6 Cancelled
-- 7 CancellationRejected

-- fiscal_stamp.status / payment_complement_stamp.status
-- 0 Pending
-- 1 Succeeded
-- 2 Rejected
-- 3 Unavailable
-- 4 ValidationFailed
```

### Q262-0. Reproducir la decision actual de attention-items

```sql
WITH target AS (
    SELECT
        262 AS fiscal_document_id,
        276 AS billing_document_id,
        '93919a07-9b16-4550-a30c-f9e16826f519' AS target_uuid,
        'PSC9603298Z8' AS receiver_rfc
),
registered_payment_counts AS (
    SELECT
        arpa.accounts_receivable_invoice_id,
        COUNT(DISTINCT arpa.accounts_receivable_payment_id) AS registered_payment_count
    FROM accounts_receivable_payment_application arpa
    GROUP BY arpa.accounts_receivable_invoice_id
),
rep_counts AS (
    SELECT
        pcrd.fiscal_document_id,
        COUNT(DISTINCT pcrd.payment_complement_document_id) AS payment_complement_count,
        COUNT(DISTINCT CASE WHEN pcd.status IN (1, 2) THEN pcd.id END) AS prepared_pending_stamp_count,
        COUNT(DISTINCT CASE WHEN pcd.status = 4 THEN pcd.id END) AS stamping_rejected_payment_complement_count,
        COUNT(DISTINCT CASE WHEN pcd.status = 7 THEN pcd.id END) AS cancellation_rejected_payment_complement_count,
        COUNT(DISTINCT CASE WHEN pcd.status IN (3, 7) THEN pcd.id END) AS cancelable_payment_complement_count,
        COUNT(DISTINCT CASE WHEN pcd.status IN (3, 5, 6, 7) THEN pcd.id END) AS stamped_payment_complement_count,
        MAX(COALESCE(pcs.stamped_at_utc, pcd.issued_at_utc)) AS last_rep_issued_at_utc
    FROM payment_complement_related_document pcrd
    JOIN payment_complement_document pcd
        ON pcd.id = pcrd.payment_complement_document_id
    LEFT JOIN payment_complement_stamp pcs
        ON pcs.payment_complement_document_id = pcd.id
    GROUP BY pcrd.fiscal_document_id
)
SELECT
    fd.id AS fiscal_document_id,
    fd.billing_document_id,
    bd.sales_order_id,
    fs.id AS fiscal_stamp_id,
    fs.uuid,
    ari.id AS accounts_receivable_invoice_id,
    fd.document_type,
    fd.series,
    fd.folio,
    fd.issued_at_utc,
    fd.receiver_rfc,
    fd.receiver_legal_name,
    fd.payment_method_sat,
    fd.payment_form_sat,
    fd.currency_code,
    fd.is_credit_sale,
    fd.credit_days,
    fd.total AS fiscal_total_raw,
    ROUND(fd.total, 2) AS fiscal_total_operational_2dp,
    COALESCE(ari.total, NULL) AS ar_total,
    COALESCE(ari.paid_total, 0.000000) AS ar_paid_total,
    COALESCE(ari.outstanding_balance, 0.000000) AS attention_items_outstanding_balance,
    COALESCE(rpc.registered_payment_count, 0) AS registered_payment_count,
    COALESCE(rc.payment_complement_count, 0) AS payment_complement_count,
    COALESCE(rc.prepared_pending_stamp_count, 0) AS prepared_pending_stamp_count,
    COALESCE(rc.stamping_rejected_payment_complement_count, 0) AS stamping_rejected_payment_complement_count,
    COALESCE(rc.cancellation_rejected_payment_complement_count, 0) AS cancellation_rejected_payment_complement_count,
    COALESCE(rc.cancelable_payment_complement_count, 0) AS cancelable_payment_complement_count,
    COALESCE(rc.stamped_payment_complement_count, 0) AS stamped_payment_complement_count,
    rc.last_rep_issued_at_utc,
    CASE fd.status
        WHEN 0 THEN 'Draft'
        WHEN 1 THEN 'ReadyForStamping'
        WHEN 2 THEN 'StampingRequested'
        WHEN 3 THEN 'Stamped'
        WHEN 4 THEN 'StampingRejected'
        WHEN 5 THEN 'CancellationRequested'
        WHEN 6 THEN 'Cancelled'
        WHEN 7 THEN 'CancellationRejected'
        WHEN 8 THEN 'DiscardedUnstamped'
        ELSE CONCAT('Unknown(', fd.status, ')')
    END AS fiscal_status_name,
    CASE fs.status
        WHEN 0 THEN 'Pending'
        WHEN 1 THEN 'Succeeded'
        WHEN 2 THEN 'Rejected'
        WHEN 3 THEN 'Unavailable'
        WHEN 4 THEN 'ValidationFailed'
        ELSE CONCAT('Unknown(', fs.status, ')')
    END AS fiscal_stamp_status_name,
    CASE ari.status
        WHEN 0 THEN 'Open'
        WHEN 1 THEN 'PartiallyPaid'
        WHEN 2 THEN 'Paid'
        WHEN 3 THEN 'Overpaid'
        WHEN 4 THEN 'Cancelled'
        ELSE NULL
    END AS ar_status_name,
    CASE
        WHEN fd.document_type <> 'I' THEN 'DocumentTypeNotIncome'
        WHEN fd.status = 6 THEN 'FiscalDocumentCancelled'
        WHEN fd.status = 5 THEN 'FiscalCancellationPending'
        WHEN fd.status NOT IN (3, 7) THEN 'FiscalDocumentNotStamped'
        WHEN COALESCE(fs.uuid, '') = '' THEN 'MissingStampedUuid'
        WHEN UPPER(fd.payment_method_sat) <> 'PPD' THEN 'PaymentMethodNotPpd'
        WHEN UPPER(fd.payment_form_sat) <> '99' THEN 'PaymentFormNot99'
        WHEN UPPER(fd.currency_code) <> 'MXN' THEN 'CurrencyNotSupported'
        WHEN ari.id IS NULL THEN 'AccountsReceivableMissing'
        WHEN ari.status = 4 THEN 'AccountsReceivableCancelled'
        WHEN fd.total <= 0 THEN 'InvalidDocumentTotal'
        WHEN ari.paid_total < 0
          OR ari.outstanding_balance < 0
          OR ari.paid_total > fd.total
          OR ari.outstanding_balance > fd.total THEN 'OperationalBalanceInconsistent'
        WHEN ari.outstanding_balance = 0 THEN 'NoOutstandingBalance'
        ELSE 'EligibleInternalRep'
    END AS current_primary_reason_code,
    CASE
        WHEN fd.document_type <> 'I' THEN 'Ineligible'
        WHEN fd.status IN (5, 6) THEN 'Blocked'
        WHEN fd.status NOT IN (3, 7) THEN 'Ineligible'
        WHEN COALESCE(fs.uuid, '') = '' THEN 'Ineligible'
        WHEN UPPER(fd.payment_method_sat) <> 'PPD' THEN 'Ineligible'
        WHEN UPPER(fd.payment_form_sat) <> '99' THEN 'Ineligible'
        WHEN UPPER(fd.currency_code) <> 'MXN' THEN 'Blocked'
        WHEN ari.id IS NULL THEN 'Blocked'
        WHEN ari.status = 4 THEN 'Blocked'
        WHEN fd.total <= 0 THEN 'Blocked'
        WHEN ari.paid_total < 0
          OR ari.outstanding_balance < 0
          OR ari.paid_total > fd.total
          OR ari.outstanding_balance > fd.total THEN 'Blocked'
        WHEN ari.outstanding_balance = 0 THEN 'Ineligible'
        ELSE 'Eligible'
    END AS current_operational_status,
    CASE
        WHEN (
            CASE
                WHEN fd.document_type <> 'I' THEN 'Ineligible'
                WHEN fd.status IN (5, 6) THEN 'Blocked'
                WHEN fd.status NOT IN (3, 7) THEN 'Ineligible'
                WHEN COALESCE(fs.uuid, '') = '' THEN 'Ineligible'
                WHEN UPPER(fd.payment_method_sat) <> 'PPD' THEN 'Ineligible'
                WHEN UPPER(fd.payment_form_sat) <> '99' THEN 'Ineligible'
                WHEN UPPER(fd.currency_code) <> 'MXN' THEN 'Blocked'
                WHEN ari.id IS NULL THEN 'Blocked'
                WHEN ari.status = 4 THEN 'Blocked'
                WHEN fd.total <= 0 THEN 'Blocked'
                WHEN ari.paid_total < 0
                  OR ari.outstanding_balance < 0
                  OR ari.paid_total > fd.total
                  OR ari.outstanding_balance > fd.total THEN 'Blocked'
                WHEN ari.outstanding_balance = 0 THEN 'Ineligible'
                ELSE 'Eligible'
            END
        ) = 'Blocked' THEN 'Blocked'
        WHEN COALESCE(rc.prepared_pending_stamp_count, 0) > 0
          OR COALESCE(rc.stamping_rejected_payment_complement_count, 0) > 0 THEN 'StampRep'
        WHEN COALESCE(rpc.registered_payment_count, 0) > COALESCE(rc.payment_complement_count, 0) THEN 'PrepareRep'
        WHEN ari.id IS NOT NULL
          AND COALESCE(ari.outstanding_balance, 0) > 0
          AND fd.status IN (3, 7)
          AND UPPER(fd.payment_method_sat) = 'PPD'
          AND UPPER(fd.payment_form_sat) = '99'
          AND UPPER(fd.currency_code) = 'MXN'
          AND ari.status <> 4 THEN 'RegisterPayment'
        WHEN COALESCE(rc.cancellation_rejected_payment_complement_count, 0) > 0
          OR COALESCE(rc.stamped_payment_complement_count, 0) > 0 THEN 'RefreshRepStatus'
        ELSE 'NoAction'
    END AS current_next_recommended_action,
    CONCAT_WS(
        ',',
        'ViewDetail',
        CASE
            WHEN ari.id IS NOT NULL
             AND fd.status IN (3, 7)
             AND UPPER(fd.payment_method_sat) = 'PPD'
             AND UPPER(fd.payment_form_sat) = '99'
             AND UPPER(fd.currency_code) = 'MXN'
             AND ari.status <> 4
             AND COALESCE(ari.outstanding_balance, 0) > 0
             AND NOT (
                 ari.paid_total < 0
                 OR ari.outstanding_balance < 0
                 OR ari.paid_total > fd.total
                 OR ari.outstanding_balance > fd.total
             )
             THEN 'OpenInternalWorkflow'
        END,
        CASE
            WHEN ari.id IS NOT NULL
             AND fd.status IN (3, 7)
             AND UPPER(fd.payment_method_sat) = 'PPD'
             AND UPPER(fd.payment_form_sat) = '99'
             AND UPPER(fd.currency_code) = 'MXN'
             AND ari.status <> 4
             AND COALESCE(ari.outstanding_balance, 0) > 0
             AND NOT (
                 ari.paid_total < 0
                 OR ari.outstanding_balance < 0
                 OR ari.paid_total > fd.total
                 OR ari.outstanding_balance > fd.total
             )
             THEN 'RegisterPayment'
        END,
        CASE
            WHEN COALESCE(rpc.registered_payment_count, 0) > COALESCE(rc.payment_complement_count, 0)
             AND ari.id IS NOT NULL
             AND fd.status NOT IN (5, 6)
             THEN 'PrepareRep'
        END,
        CASE
            WHEN COALESCE(rc.prepared_pending_stamp_count, 0) > 0
              OR COALESCE(rc.stamping_rejected_payment_complement_count, 0) > 0
              THEN 'StampRep'
        END,
        CASE
            WHEN COALESCE(rc.stamped_payment_complement_count, 0) > 0 THEN 'RefreshRepStatus'
        END,
        CASE
            WHEN COALESCE(rc.cancelable_payment_complement_count, 0) > 0 THEN 'CancelRep'
        END
    ) AS current_available_actions_csv,
    CASE
        WHEN fd.status = 3
         AND fd.is_credit_sale = 1
         AND UPPER(fd.payment_method_sat) = 'PPD'
         AND UPPER(fd.payment_form_sat) = '99'
         AND UPPER(fd.currency_code) = 'MXN'
         AND COALESCE(fd.credit_days, 0) > 0
         AND COALESCE(fs.uuid, '') <> ''
         AND ari.id IS NULL
        THEN 'YES'
        ELSE 'NO'
    END AS should_auto_create_ar_under_current_code,
    CASE
        WHEN ari.id IS NULL AND fd.is_credit_sale = 0 THEN 'PPD/99 visible para REP pero marcado como no venta a credito.'
        WHEN ari.id IS NULL AND COALESCE(fd.credit_days, 0) <= 0 THEN 'PPD/99 sin credit_days validos.'
        WHEN ari.id IS NULL AND fd.status <> 3 THEN 'Sin AR y CFDI no timbrado vigente.'
        WHEN ari.id IS NULL AND UPPER(fd.currency_code) <> 'MXN' THEN 'Sin AR y moneda no soportada por AR.'
        WHEN ari.id IS NULL THEN 'Sin AR pese a cumplir la bandeja REP; revisar auditoria post-stamp.'
        ELSE 'Tiene AR o la regla actual no aplica.'
    END AS likely_root_cause_hint
FROM target t
JOIN fiscal_document fd
    ON fd.id = t.fiscal_document_id
LEFT JOIN billing_document bd
    ON bd.id = fd.billing_document_id
LEFT JOIN fiscal_stamp fs
    ON fs.fiscal_document_id = fd.id
LEFT JOIN accounts_receivable_invoice ari
    ON ari.fiscal_document_id = fd.id
LEFT JOIN registered_payment_counts rpc
    ON rpc.accounts_receivable_invoice_id = ari.id
LEFT JOIN rep_counts rc
    ON rc.fiscal_document_id = fd.id
WHERE fd.receiver_rfc = t.receiver_rfc;
```

### Q262-1. Fiscal document completo

```sql
SELECT
    fd.id,
    fd.billing_document_id,
    fd.issuer_profile_id,
    fd.fiscal_receiver_id,
    fd.status AS status_id,
    CASE fd.status
        WHEN 0 THEN 'Draft'
        WHEN 1 THEN 'ReadyForStamping'
        WHEN 2 THEN 'StampingRequested'
        WHEN 3 THEN 'Stamped'
        WHEN 4 THEN 'StampingRejected'
        WHEN 5 THEN 'CancellationRequested'
        WHEN 6 THEN 'Cancelled'
        WHEN 7 THEN 'CancellationRejected'
        WHEN 8 THEN 'DiscardedUnstamped'
        ELSE CONCAT('Unknown(', fd.status, ')')
    END AS status_name,
    fd.document_type,
    fd.series,
    fd.folio,
    fd.issued_at_utc,
    fd.payment_method_sat,
    fd.payment_form_sat,
    fd.payment_condition,
    fd.currency_code,
    fd.exchange_rate,
    fd.subtotal,
    fd.discount_total,
    fd.tax_total,
    fd.total,
    ROUND(fd.total, 2) AS total_2dp,
    fd.is_credit_sale,
    fd.credit_days,
    fd.receiver_rfc,
    fd.receiver_legal_name,
    fd.receiver_fiscal_regime_code,
    fd.receiver_cfdi_use_code,
    fd.receiver_postal_code,
    fd.receiver_country_code,
    fd.receiver_foreign_tax_registration,
    fd.issuer_rfc,
    fd.issuer_legal_name,
    fd.issuer_fiscal_regime_code,
    fd.issuer_postal_code,
    fd.created_at_utc,
    fd.updated_at_utc
FROM fiscal_document fd
WHERE fd.id = 262;
```

### Q262-2. Fiscal stamp

```sql
SELECT
    fs.id,
    fs.fiscal_document_id,
    fs.status AS status_id,
    CASE fs.status
        WHEN 0 THEN 'Pending'
        WHEN 1 THEN 'Succeeded'
        WHEN 2 THEN 'Rejected'
        WHEN 3 THEN 'Unavailable'
        WHEN 4 THEN 'ValidationFailed'
        ELSE CONCAT('Unknown(', fs.status, ')')
    END AS status_name,
    fs.uuid,
    fs.stamped_at_utc,
    fs.provider_name,
    fs.provider_operation,
    fs.provider_tracking_id,
    fs.provider_code,
    fs.provider_message,
    fs.error_code,
    fs.error_message,
    CHAR_LENGTH(fs.xml_content) AS xml_content_length,
    fs.created_at_utc,
    fs.updated_at_utc,
    fs.last_status_check_at_utc,
    fs.last_known_external_status,
    fs.last_status_provider_code,
    fs.last_status_provider_message,
    fs.last_remote_query_at_utc,
    fs.last_remote_provider_code,
    fs.last_remote_provider_message,
    fs.xml_recovered_from_provider_at_utc
FROM fiscal_stamp fs
WHERE fs.fiscal_document_id = 262;
```

### Q262-3. Billing document

```sql
SELECT
    bd.id,
    bd.sales_order_id,
    bd.status AS status_id,
    CASE bd.status
        WHEN 0 THEN 'Draft'
        WHEN 1 THEN 'ReadyToStamp'
        WHEN 2 THEN 'Stamping'
        WHEN 3 THEN 'Stamped'
        WHEN 4 THEN 'StampFailed'
        WHEN 5 THEN 'Cancelled'
        ELSE CONCAT('Unknown(', bd.status, ')')
    END AS status_name,
    bd.document_type,
    bd.series,
    bd.folio,
    bd.payment_condition,
    bd.payment_method_sat,
    bd.payment_form_sat,
    bd.currency_code,
    bd.exchange_rate,
    bd.issued_at_utc,
    bd.subtotal,
    bd.discount_total,
    bd.tax_total,
    bd.total,
    bd.active_sales_order_id,
    bd.created_at_utc,
    bd.updated_at_utc
FROM billing_document bd
WHERE bd.id = 276;
```

### Q262-4. Fiscal receiver e issuer profile

```sql
SELECT
    fr.id,
    fr.rfc,
    fr.legal_name,
    fr.cfdi_use_code_default AS cfdi_use,
    fr.fiscal_regime_code AS fiscal_regime,
    fr.postal_code,
    fr.email,
    fr.phone,
    fr.search_alias,
    fr.is_active,
    ip.id AS issuer_profile_id,
    ip.rfc AS issuer_rfc,
    ip.legal_name AS issuer_legal_name,
    ip.fiscal_regime_code AS issuer_fiscal_regime,
    ip.postal_code AS issuer_postal_code,
    ip.cfdi_version,
    ip.pac_environment,
    ip.is_active AS issuer_is_active
FROM fiscal_document fd
LEFT JOIN fiscal_receiver fr
    ON fr.id = fd.fiscal_receiver_id
LEFT JOIN issuer_profile ip
    ON ip.id = fd.issuer_profile_id
WHERE fd.id = 262;
```

### Q262-5. Cuentas por cobrar potencialmente relacionadas

```sql
SELECT
    ari.id,
    ari.billing_document_id,
    ari.fiscal_document_id,
    ari.fiscal_stamp_id,
    ari.external_rep_base_document_id,
    ari.fiscal_receiver_id,
    ari.status AS status_id,
    CASE ari.status
        WHEN 0 THEN 'Open'
        WHEN 1 THEN 'PartiallyPaid'
        WHEN 2 THEN 'Paid'
        WHEN 3 THEN 'Overpaid'
        WHEN 4 THEN 'Cancelled'
        ELSE CONCAT('Unknown(', ari.status, ')')
    END AS status_name,
    ari.payment_method_sat,
    ari.payment_form_sat_initial,
    ari.is_credit_sale,
    ari.credit_days,
    ari.issued_at_utc,
    ari.due_at_utc,
    ari.currency_code,
    ari.total,
    ari.paid_total,
    ari.outstanding_balance,
    ROUND(ari.total - ari.paid_total, 2) AS expected_outstanding_2dp,
    ROUND(ari.outstanding_balance - ROUND(ari.total - ari.paid_total, 2), 6) AS outstanding_vs_formula_delta,
    fs.uuid AS linked_fiscal_stamp_uuid,
    fd.id AS linked_fiscal_document_id,
    fd.series AS linked_fiscal_series,
    fd.folio AS linked_fiscal_folio
FROM accounts_receivable_invoice ari
LEFT JOIN fiscal_stamp fs
    ON fs.id = ari.fiscal_stamp_id
LEFT JOIN fiscal_document fd
    ON fd.id = ari.fiscal_document_id
WHERE ari.fiscal_document_id = 262
   OR ari.billing_document_id = 276
   OR ari.fiscal_stamp_id = (SELECT id FROM fiscal_stamp WHERE fiscal_document_id = 262)
ORDER BY ari.id;
```

### Q262-6A. Pagos registrados para el receptor y remanente no aplicado

```sql
SELECT
    arp.id AS accounts_receivable_payment_id,
    arp.payment_date_utc,
    arp.payment_form_sat,
    arp.currency_code,
    arp.amount,
    COALESCE(SUM(arpa.applied_amount), 0.000000) AS applied_total_any_invoice,
    ROUND(arp.amount - COALESCE(SUM(arpa.applied_amount), 0.000000), 2) AS unapplied_amount_2dp,
    arp.unapplied_disposition,
    arp.reference,
    arp.notes,
    arp.received_from_fiscal_receiver_id,
    fr.rfc AS receiver_rfc,
    fr.legal_name AS receiver_legal_name,
    GROUP_CONCAT(
        DISTINCT CONCAT(
            'invoice=', arpa.accounts_receivable_invoice_id,
            ':amount=', FORMAT(arpa.applied_amount, 6)
        )
        ORDER BY arpa.accounts_receivable_invoice_id
        SEPARATOR '; '
    ) AS applications_summary,
    arp.created_at_utc,
    arp.updated_at_utc
FROM accounts_receivable_payment arp
LEFT JOIN accounts_receivable_payment_application arpa
    ON arpa.accounts_receivable_payment_id = arp.id
LEFT JOIN fiscal_receiver fr
    ON fr.id = arp.received_from_fiscal_receiver_id
WHERE arp.received_from_fiscal_receiver_id = (
        SELECT fiscal_receiver_id
        FROM fiscal_document
        WHERE id = 262
    )
GROUP BY
    arp.id,
    arp.payment_date_utc,
    arp.payment_form_sat,
    arp.currency_code,
    arp.amount,
    arp.unapplied_disposition,
    arp.reference,
    arp.notes,
    arp.received_from_fiscal_receiver_id,
    fr.rfc,
    fr.legal_name,
    arp.created_at_utc,
    arp.updated_at_utc
ORDER BY arp.payment_date_utc DESC, arp.id DESC;
```

### Q262-6B. Aplicaciones especificamente relacionadas a la factura si existe AR

```sql
SELECT
    arpa.id AS application_id,
    arpa.accounts_receivable_payment_id,
    arpa.accounts_receivable_invoice_id,
    arpa.application_sequence,
    arpa.applied_amount,
    arpa.previous_balance,
    arpa.new_balance,
    arp.payment_date_utc,
    arp.payment_form_sat,
    arp.amount AS payment_amount,
    arp.reference,
    arp.notes,
    ROUND(arp.amount - COALESCE(total_apps.applied_total_any_invoice, 0.000000), 2) AS payment_unapplied_amount_2dp,
    arpa.created_at_utc
FROM accounts_receivable_payment_application arpa
JOIN accounts_receivable_payment arp
    ON arp.id = arpa.accounts_receivable_payment_id
LEFT JOIN (
    SELECT
        accounts_receivable_payment_id,
        SUM(applied_amount) AS applied_total_any_invoice
    FROM accounts_receivable_payment_application
    GROUP BY accounts_receivable_payment_id
) total_apps
    ON total_apps.accounts_receivable_payment_id = arp.id
WHERE arpa.accounts_receivable_invoice_id IN (
    SELECT id
    FROM accounts_receivable_invoice
    WHERE fiscal_document_id = 262
       OR billing_document_id = 276
)
ORDER BY arp.payment_date_utc DESC, arpa.application_sequence DESC, arpa.id DESC;
```

### Q262-7. REP / payment complements relacionados

```sql
SELECT
    pcd.id AS payment_complement_document_id,
    pcd.accounts_receivable_payment_id,
    pcd.status AS payment_complement_status_id,
    CASE pcd.status
        WHEN 0 THEN 'Draft'
        WHEN 1 THEN 'ReadyForStamping'
        WHEN 2 THEN 'StampingRequested'
        WHEN 3 THEN 'Stamped'
        WHEN 4 THEN 'StampingRejected'
        WHEN 5 THEN 'CancellationRequested'
        WHEN 6 THEN 'Cancelled'
        WHEN 7 THEN 'CancellationRejected'
        ELSE CONCAT('Unknown(', pcd.status, ')')
    END AS payment_complement_status_name,
    pcd.document_type,
    pcd.issued_at_utc,
    pcd.payment_date_utc,
    pcd.currency_code,
    pcd.total_payments_amount,
    pcd.receiver_rfc,
    pcd.receiver_legal_name,
    pcp.id AS payment_complement_payment_id,
    pcp.amount AS payment_row_amount,
    pcp.payment_form_sat AS payment_row_form_sat,
    pcrd.id AS related_document_id,
    pcrd.accounts_receivable_invoice_id,
    pcrd.fiscal_document_id,
    pcrd.fiscal_stamp_id,
    pcrd.external_rep_base_document_id,
    pcrd.related_document_uuid,
    pcrd.series,
    pcrd.folio,
    pcrd.installment_number,
    pcrd.previous_balance,
    pcrd.paid_amount,
    pcrd.remaining_balance,
    pcrd.currency_code AS related_document_currency_code,
    pcs.id AS payment_complement_stamp_id,
    pcs.status AS payment_complement_stamp_status_id,
    CASE pcs.status
        WHEN 0 THEN 'Pending'
        WHEN 1 THEN 'Succeeded'
        WHEN 2 THEN 'Rejected'
        WHEN 3 THEN 'Unavailable'
        WHEN 4 THEN 'ValidationFailed'
        ELSE NULL
    END AS payment_complement_stamp_status_name,
    pcs.uuid AS payment_complement_uuid,
    pcs.stamped_at_utc,
    pcc.id AS payment_complement_cancellation_id,
    pcc.status AS payment_complement_cancellation_status_id,
    pcc.cancelled_at_utc
FROM payment_complement_related_document pcrd
JOIN payment_complement_document pcd
    ON pcd.id = pcrd.payment_complement_document_id
LEFT JOIN payment_complement_payment pcp
    ON pcp.id = pcrd.payment_complement_payment_id
LEFT JOIN payment_complement_stamp pcs
    ON pcs.payment_complement_document_id = pcd.id
LEFT JOIN payment_complement_cancellation pcc
    ON pcc.payment_complement_document_id = pcd.id
WHERE pcrd.fiscal_document_id = 262
   OR pcrd.related_document_uuid = '93919a07-9b16-4550-a30c-f9e16826f519'
   OR pcrd.accounts_receivable_invoice_id IN (
        SELECT id
        FROM accounts_receivable_invoice
        WHERE fiscal_document_id = 262
           OR billing_document_id = 276
   )
ORDER BY pcd.issued_at_utc DESC, pcd.id DESC, pcrd.installment_number DESC;
```

### Q262-8A. Conteos y sumas de items

```sql
SELECT
    fd.id AS fiscal_document_id,
    fd.billing_document_id,
    fd.subtotal AS fiscal_header_subtotal,
    fd.discount_total AS fiscal_header_discount_total,
    fd.tax_total AS fiscal_header_tax_total,
    fd.total AS fiscal_header_total,
    bd.subtotal AS billing_header_subtotal,
    bd.discount_total AS billing_header_discount_total,
    bd.tax_total AS billing_header_tax_total,
    bd.total AS billing_header_total,
    COALESCE(ari.total, NULL) AS ar_total,
    COALESCE(ari.paid_total, NULL) AS ar_paid_total,
    COALESCE(ari.outstanding_balance, NULL) AS ar_outstanding_balance,
    (SELECT COUNT(*) FROM billing_document_item bdi WHERE bdi.billing_document_id = fd.billing_document_id) AS billing_document_item_count,
    (SELECT COUNT(*) FROM fiscal_document_item fdi WHERE fdi.fiscal_document_id = fd.id) AS fiscal_document_item_count,
    (SELECT COALESCE(SUM(bdi.line_total), 0.000000) FROM billing_document_item bdi WHERE bdi.billing_document_id = fd.billing_document_id) AS billing_items_subtotal_like_sum,
    (SELECT COALESCE(SUM(bdi.tax_amount), 0.000000) FROM billing_document_item bdi WHERE bdi.billing_document_id = fd.billing_document_id) AS billing_items_tax_sum,
    (SELECT COALESCE(SUM(bdi.line_total + bdi.tax_amount), 0.000000) FROM billing_document_item bdi WHERE bdi.billing_document_id = fd.billing_document_id) AS billing_items_total_sum,
    (SELECT COALESCE(SUM(fdi.subtotal), 0.000000) FROM fiscal_document_item fdi WHERE fdi.fiscal_document_id = fd.id) AS fiscal_items_subtotal_sum,
    (SELECT COALESCE(SUM(fdi.tax_total), 0.000000) FROM fiscal_document_item fdi WHERE fdi.fiscal_document_id = fd.id) AS fiscal_items_tax_sum,
    (SELECT COALESCE(SUM(fdi.total), 0.000000) FROM fiscal_document_item fdi WHERE fdi.fiscal_document_id = fd.id) AS fiscal_items_total_sum,
    ROUND(fd.total - ROUND(fd.total, 2), 6) AS fiscal_rounding_delta_vs_2dp,
    ROUND(fd.total - COALESCE(ari.total, 0.000000), 6) AS fiscal_vs_ar_total_delta,
    ROUND(
        (SELECT COALESCE(SUM(fdi.total), 0.000000) FROM fiscal_document_item fdi WHERE fdi.fiscal_document_id = fd.id) - fd.total,
        6
    ) AS fiscal_items_vs_header_total_delta
FROM fiscal_document fd
LEFT JOIN billing_document bd
    ON bd.id = fd.billing_document_id
LEFT JOIN accounts_receivable_invoice ari
    ON ari.fiscal_document_id = fd.id
WHERE fd.id = 262;
```

### Q262-8B. Comparacion linea por linea billing vs fiscal

```sql
SELECT
    fdi.id AS fiscal_document_item_id,
    fdi.line_number AS fiscal_line_number,
    fdi.billing_document_item_id,
    fdi.internal_code,
    fdi.description AS fiscal_description,
    fdi.quantity AS fiscal_quantity,
    fdi.unit_price AS fiscal_unit_price,
    fdi.discount_amount AS fiscal_discount_amount,
    fdi.subtotal AS fiscal_subtotal,
    fdi.tax_total AS fiscal_tax_total,
    fdi.total AS fiscal_total,
    bdi.id AS billing_document_item_id_resolved,
    bdi.line_number AS billing_line_number,
    bdi.description AS billing_description,
    bdi.quantity AS billing_quantity,
    bdi.unit_price AS billing_unit_price,
    bdi.discount_amount AS billing_discount_amount,
    bdi.line_total AS billing_subtotal_like,
    bdi.tax_amount AS billing_tax_amount,
    bdi.line_total + bdi.tax_amount AS billing_total,
    ROUND(fdi.subtotal - COALESCE(bdi.line_total, 0.000000), 6) AS subtotal_delta,
    ROUND(fdi.tax_total - COALESCE(bdi.tax_amount, 0.000000), 6) AS tax_delta,
    ROUND(fdi.total - COALESCE(bdi.line_total + bdi.tax_amount, 0.000000), 6) AS total_delta
FROM fiscal_document_item fdi
LEFT JOIN billing_document_item bdi
    ON bdi.id = fdi.billing_document_item_id
WHERE fdi.fiscal_document_id = 262
ORDER BY fdi.line_number, fdi.id;
```

### Q262-9. Auditoria completa

```sql
WITH context_ids AS (
    SELECT 'FiscalDocument' AS entity_type, CAST(fd.id AS CHAR(50)) AS entity_id
    FROM fiscal_document fd
    WHERE fd.id = 262
    UNION ALL
    SELECT 'AccountsReceivableInvoice', CAST(ari.id AS CHAR(50))
    FROM accounts_receivable_invoice ari
    WHERE ari.fiscal_document_id = 262 OR ari.billing_document_id = 276
    UNION ALL
    SELECT 'AccountsReceivablePayment', CAST(arpa.accounts_receivable_payment_id AS CHAR(50))
    FROM accounts_receivable_payment_application arpa
    WHERE arpa.accounts_receivable_invoice_id IN (
        SELECT id
        FROM accounts_receivable_invoice
        WHERE fiscal_document_id = 262 OR billing_document_id = 276
    )
    UNION ALL
    SELECT 'PaymentComplementDocument', CAST(pcd.id AS CHAR(50))
    FROM payment_complement_document pcd
    WHERE pcd.id IN (
        SELECT DISTINCT pcrd.payment_complement_document_id
        FROM payment_complement_related_document pcrd
        WHERE pcrd.fiscal_document_id = 262
           OR pcrd.related_document_uuid = '93919a07-9b16-4550-a30c-f9e16826f519'
    )
)
SELECT
    ae.id,
    ae.occurred_at_utc,
    ae.action_type,
    ae.entity_type,
    ae.entity_id,
    ae.outcome,
    ae.actor_username,
    ae.correlation_id,
    ae.error_message,
    ae.request_summary_json,
    ae.response_summary_json
FROM audit_event ae
LEFT JOIN context_ids c
    ON c.entity_type = ae.entity_type
   AND c.entity_id = ae.entity_id
WHERE c.entity_id IS NOT NULL
   OR ae.request_summary_json LIKE '%"fiscalDocumentId":262%'
   OR ae.response_summary_json LIKE '%"fiscalDocumentId":262%'
   OR ae.request_summary_json LIKE '%"billingDocumentId":276%'
   OR ae.response_summary_json LIKE '%"billingDocumentId":276%'
   OR ae.request_summary_json LIKE '%93919a07-9b16-4550-a30c-f9e16826f519%'
   OR ae.response_summary_json LIKE '%93919a07-9b16-4550-a30c-f9e16826f519%'
ORDER BY ae.occurred_at_utc DESC, ae.id DESC;
```

### Q262-10. Auditoria enfocada en el intento automatico de crear AR despues del timbrado

```sql
SELECT
    ae.id,
    ae.occurred_at_utc,
    ae.action_type,
    ae.entity_type,
    ae.entity_id,
    ae.outcome,
    ae.error_message,
    ae.request_summary_json,
    ae.response_summary_json
FROM audit_event ae
WHERE ae.action_type IN (
        'FiscalDocument.Stamp',
        'FiscalDocument.StampAndEmail',
        'AccountsReceivableInvoice.EnsureAfterFiscalStamp',
        'AccountsReceivableInvoice.Ensure',
        'AccountsReceivableInvoice.Create'
    )
  AND (
        ae.entity_id = '262'
        OR ae.request_summary_json LIKE '%"fiscalDocumentId":262%'
        OR ae.response_summary_json LIKE '%"fiscalDocumentId":262%'
      )
ORDER BY ae.occurred_at_utc DESC, ae.id DESC;
```

## Queries SQL de diagnostico para fiscalDocumentId 768 y 539

### Q768-539-1. Resumen comparativo y prueba de hipotesis de redondeo

```sql
SELECT
    fd.id AS fiscal_document_id,
    fd.billing_document_id,
    fs.uuid,
    fd.series,
    fd.folio,
    fd.status AS fiscal_status_id,
    fd.payment_method_sat,
    fd.payment_form_sat,
    fd.currency_code,
    fd.total AS fiscal_total_raw,
    ROUND(fd.total, 2) AS fiscal_total_2dp,
    ari.id AS accounts_receivable_invoice_id,
    ari.status AS ar_status_id,
    ari.total AS ar_total,
    ari.paid_total,
    ari.outstanding_balance,
    ROUND(ari.total - ari.paid_total, 2) AS ar_expected_outstanding_2dp,
    ROUND(fd.total - ari.outstanding_balance, 6) AS raw_fiscal_minus_outstanding_delta,
    ROUND(ari.total - fd.total, 6) AS ar_total_minus_raw_fiscal_delta,
    ROUND(ari.outstanding_balance - fd.total, 6) AS outstanding_minus_raw_fiscal_delta,
    CASE
        WHEN ari.paid_total < 0
          OR ari.outstanding_balance < 0
          OR ari.paid_total > fd.total
          OR ari.outstanding_balance > fd.total
        THEN 'OperationalBalanceInconsistent'
        WHEN ari.outstanding_balance = 0
        THEN 'NoOutstandingBalance'
        ELSE 'NotBlockedByBalance'
    END AS current_rep_balance_reason,
    CASE
        WHEN ROUND(fd.total, 2) = ari.total
         AND ROUND(ari.total - ari.paid_total, 2) = ROUND(ari.outstanding_balance, 2)
         AND ari.outstanding_balance > fd.total
        THEN 'LIKELY_ROUNDING_FALSE_POSITIVE'
        WHEN ROUND(fd.total, 2) <> ari.total
        THEN 'AR_TOTAL_DIFFERS_FROM_2DP_FISCAL_TOTAL'
        WHEN ROUND(ari.total - ari.paid_total, 2) <> ROUND(ari.outstanding_balance, 2)
        THEN 'AR_SELF_INCONSISTENT'
        ELSE 'REQUIRES_DEEPER_REVIEW'
    END AS diagnostic_hint
FROM fiscal_document fd
LEFT JOIN fiscal_stamp fs
    ON fs.fiscal_document_id = fd.id
LEFT JOIN accounts_receivable_invoice ari
    ON ari.fiscal_document_id = fd.id
WHERE fd.id IN (768, 539)
ORDER BY fd.id;
```

### Q768-539-2. Aplicaciones de pago y posibles duplicidades

```sql
SELECT
    fd.id AS fiscal_document_id,
    ari.id AS accounts_receivable_invoice_id,
    arpa.accounts_receivable_payment_id,
    arpa.application_sequence,
    arpa.applied_amount,
    arpa.previous_balance,
    arpa.new_balance,
    arp.payment_date_utc,
    arp.payment_form_sat,
    arp.amount AS payment_amount,
    ROUND(arp.amount - COALESCE(total_apps.applied_total_any_invoice, 0.000000), 2) AS payment_unapplied_amount_2dp,
    arp.reference,
    arp.notes,
    CASE
        WHEN dup.invoice_payment_row_count > 1 THEN 'MULTIPLE_ROWS_SAME_PAYMENT_TO_SAME_INVOICE'
        ELSE 'OK'
    END AS duplicate_pattern
FROM fiscal_document fd
JOIN accounts_receivable_invoice ari
    ON ari.fiscal_document_id = fd.id
LEFT JOIN accounts_receivable_payment_application arpa
    ON arpa.accounts_receivable_invoice_id = ari.id
LEFT JOIN accounts_receivable_payment arp
    ON arp.id = arpa.accounts_receivable_payment_id
LEFT JOIN (
    SELECT
        accounts_receivable_payment_id,
        SUM(applied_amount) AS applied_total_any_invoice
    FROM accounts_receivable_payment_application
    GROUP BY accounts_receivable_payment_id
) total_apps
    ON total_apps.accounts_receivable_payment_id = arp.id
LEFT JOIN (
    SELECT
        accounts_receivable_invoice_id,
        accounts_receivable_payment_id,
        COUNT(*) AS invoice_payment_row_count
    FROM accounts_receivable_payment_application
    GROUP BY accounts_receivable_invoice_id, accounts_receivable_payment_id
) dup
    ON dup.accounts_receivable_invoice_id = arpa.accounts_receivable_invoice_id
   AND dup.accounts_receivable_payment_id = arpa.accounts_receivable_payment_id
WHERE fd.id IN (768, 539)
ORDER BY fd.id, arp.payment_date_utc DESC, arpa.application_sequence DESC, arpa.id DESC;
```

### Q768-539-3. REP relacionados y parcialidades

```sql
SELECT
    pcrd.fiscal_document_id,
    pcd.id AS payment_complement_document_id,
    pcd.accounts_receivable_payment_id,
    pcd.status AS payment_complement_status_id,
    CASE pcd.status
        WHEN 0 THEN 'Draft'
        WHEN 1 THEN 'ReadyForStamping'
        WHEN 2 THEN 'StampingRequested'
        WHEN 3 THEN 'Stamped'
        WHEN 4 THEN 'StampingRejected'
        WHEN 5 THEN 'CancellationRequested'
        WHEN 6 THEN 'Cancelled'
        WHEN 7 THEN 'CancellationRejected'
        ELSE CONCAT('Unknown(', pcd.status, ')')
    END AS payment_complement_status_name,
    pcs.uuid AS payment_complement_uuid,
    pcs.stamped_at_utc,
    pcrd.installment_number,
    pcrd.previous_balance,
    pcrd.paid_amount,
    pcrd.remaining_balance,
    pcrd.related_document_uuid,
    pcrd.accounts_receivable_invoice_id,
    pcc.status AS payment_complement_cancellation_status_id,
    pcc.cancelled_at_utc
FROM payment_complement_related_document pcrd
JOIN payment_complement_document pcd
    ON pcd.id = pcrd.payment_complement_document_id
LEFT JOIN payment_complement_stamp pcs
    ON pcs.payment_complement_document_id = pcd.id
LEFT JOIN payment_complement_cancellation pcc
    ON pcc.payment_complement_document_id = pcd.id
WHERE pcrd.fiscal_document_id IN (768, 539)
ORDER BY pcrd.fiscal_document_id, pcd.issued_at_utc DESC, pcd.id DESC, pcrd.installment_number DESC;
```

### Q768-539-4. Reconciliacion operativa AR vs fiscal vs REP

```sql
SELECT
    fd.id AS fiscal_document_id,
    fs.uuid,
    fd.total AS fiscal_total_raw,
    ROUND(fd.total, 2) AS fiscal_total_2dp,
    ari.total AS ar_total,
    ari.paid_total,
    ari.outstanding_balance,
    ROUND(ari.total - ari.paid_total, 2) AS ar_formula_outstanding_2dp,
    COALESCE(rep.sum_rep_paid_amount, 0.000000) AS total_paid_amount_in_rep_rows,
    COALESCE(rep.max_installment_number, 0) AS max_installment_number,
    ROUND(COALESCE(rep.sum_rep_paid_amount, 0.000000) - ari.paid_total, 6) AS rep_paid_vs_ar_paid_delta,
    ROUND(COALESCE(rep.last_rep_remaining_balance, 0.000000) - ari.outstanding_balance, 6) AS last_rep_remaining_vs_ar_outstanding_delta
FROM fiscal_document fd
JOIN accounts_receivable_invoice ari
    ON ari.fiscal_document_id = fd.id
LEFT JOIN fiscal_stamp fs
    ON fs.fiscal_document_id = fd.id
LEFT JOIN (
    SELECT
        pcrd.fiscal_document_id,
        SUM(pcrd.paid_amount) AS sum_rep_paid_amount,
        MAX(pcrd.installment_number) AS max_installment_number,
        SUBSTRING_INDEX(
            GROUP_CONCAT(pcrd.remaining_balance ORDER BY pcrd.installment_number DESC SEPARATOR ','),
            ',',
            1
        ) AS last_rep_remaining_balance
    FROM payment_complement_related_document pcrd
    GROUP BY pcrd.fiscal_document_id
) rep
    ON rep.fiscal_document_id = fd.id
WHERE fd.id IN (768, 539)
ORDER BY fd.id;
```

## Queries SQL para universo afectado

### U1. CFDI timbrados PPD/99 sin accounts_receivable_invoice

```sql
SELECT
    fd.id AS fiscal_document_id,
    fd.billing_document_id,
    fs.uuid,
    fd.issued_at_utc,
    fd.receiver_rfc,
    fd.receiver_legal_name,
    fd.payment_method_sat,
    fd.payment_form_sat,
    fd.currency_code,
    fd.is_credit_sale,
    fd.credit_days,
    fd.total,
    CASE fd.status
        WHEN 3 THEN 'Stamped'
        WHEN 7 THEN 'CancellationRejected'
        ELSE CONCAT('Other(', fd.status, ')')
    END AS fiscal_status_name
FROM fiscal_document fd
LEFT JOIN fiscal_stamp fs
    ON fs.fiscal_document_id = fd.id
LEFT JOIN accounts_receivable_invoice ari
    ON ari.fiscal_document_id = fd.id
WHERE fd.document_type = 'I'
  AND fd.status IN (3, 7)
  AND UPPER(fd.payment_method_sat) = 'PPD'
  AND UPPER(fd.payment_form_sat) = '99'
  AND COALESCE(fs.uuid, '') <> ''
  AND ari.id IS NULL
ORDER BY fd.issued_at_utc DESC, fd.id DESC;
```

### U1B. Subconjunto critico: PPD/99 sin AR y sin venta a credito

```sql
SELECT
    fd.id AS fiscal_document_id,
    fd.billing_document_id,
    fs.uuid,
    fd.issued_at_utc,
    fd.receiver_rfc,
    fd.receiver_legal_name,
    fd.is_credit_sale,
    fd.credit_days,
    fd.total
FROM fiscal_document fd
LEFT JOIN fiscal_stamp fs
    ON fs.fiscal_document_id = fd.id
LEFT JOIN accounts_receivable_invoice ari
    ON ari.fiscal_document_id = fd.id
WHERE fd.document_type = 'I'
  AND fd.status IN (3, 7)
  AND UPPER(fd.payment_method_sat) = 'PPD'
  AND UPPER(fd.payment_form_sat) = '99'
  AND COALESCE(fs.uuid, '') <> ''
  AND ari.id IS NULL
  AND (fd.is_credit_sale = 0 OR COALESCE(fd.credit_days, 0) <= 0)
ORDER BY fd.issued_at_utc DESC, fd.id DESC;
```

### U2. CFDI que hoy caen en AccountsReceivableMissing dentro del flujo REP interno

```sql
SELECT
    fd.id AS fiscal_document_id,
    fd.billing_document_id,
    fs.uuid,
    fd.receiver_rfc,
    fd.receiver_legal_name,
    fd.issued_at_utc,
    fd.payment_method_sat,
    fd.payment_form_sat,
    fd.currency_code,
    fd.is_credit_sale,
    fd.credit_days,
    fd.total
FROM fiscal_document fd
LEFT JOIN fiscal_stamp fs
    ON fs.fiscal_document_id = fd.id
LEFT JOIN accounts_receivable_invoice ari
    ON ari.fiscal_document_id = fd.id
WHERE fd.document_type = 'I'
  AND fd.status IN (3, 7)
  AND COALESCE(fs.uuid, '') <> ''
  AND UPPER(fd.payment_method_sat) = 'PPD'
  AND UPPER(fd.payment_form_sat) = '99'
  AND UPPER(fd.currency_code) = 'MXN'
  AND ari.id IS NULL
ORDER BY fd.issued_at_utc DESC, fd.id DESC;
```

### U3. CFDI con AR pero bloqueados por OperationalBalanceInconsistent segun la regla actual

```sql
SELECT
    fd.id AS fiscal_document_id,
    fd.billing_document_id,
    fs.uuid,
    fd.receiver_rfc,
    fd.receiver_legal_name,
    fd.issued_at_utc,
    fd.total AS fiscal_total_raw,
    ROUND(fd.total, 2) AS fiscal_total_2dp,
    ari.id AS accounts_receivable_invoice_id,
    ari.status AS ar_status_id,
    ari.total AS ar_total,
    ari.paid_total,
    ari.outstanding_balance,
    ROUND(ari.total - ari.paid_total, 2) AS ar_formula_outstanding_2dp,
    ROUND(ari.total - fd.total, 6) AS ar_total_minus_fiscal_total_delta,
    ROUND(ari.outstanding_balance - fd.total, 6) AS ar_outstanding_minus_fiscal_total_delta
FROM fiscal_document fd
JOIN accounts_receivable_invoice ari
    ON ari.fiscal_document_id = fd.id
LEFT JOIN fiscal_stamp fs
    ON fs.fiscal_document_id = fd.id
WHERE fd.document_type = 'I'
  AND fd.status IN (3, 7)
  AND COALESCE(fs.uuid, '') <> ''
  AND UPPER(fd.payment_method_sat) = 'PPD'
  AND UPPER(fd.payment_form_sat) = '99'
  AND UPPER(fd.currency_code) = 'MXN'
  AND ari.status <> 4
  AND (
        ari.paid_total < 0
        OR ari.outstanding_balance < 0
        OR ari.paid_total > fd.total
        OR ari.outstanding_balance > fd.total
      )
ORDER BY fd.issued_at_utc DESC, fd.id DESC;
```

### U4. Cuentas por cobrar con saldo operativo distinto a total - paid_total

```sql
SELECT
    ari.id AS accounts_receivable_invoice_id,
    ari.fiscal_document_id,
    ari.billing_document_id,
    ari.status AS ar_status_id,
    ari.total,
    ari.paid_total,
    ari.outstanding_balance,
    ROUND(ari.total - ari.paid_total, 2) AS expected_outstanding_2dp,
    ROUND(ari.outstanding_balance - ROUND(ari.total - ari.paid_total, 2), 6) AS delta_vs_formula,
    fd.total AS fiscal_total_raw,
    ROUND(fd.total, 2) AS fiscal_total_2dp
FROM accounts_receivable_invoice ari
LEFT JOIN fiscal_document fd
    ON fd.id = ari.fiscal_document_id
WHERE ROUND(ari.outstanding_balance, 2) <> ROUND(ari.total - ari.paid_total, 2)
ORDER BY ABS(ROUND(ari.outstanding_balance - ROUND(ari.total - ari.paid_total, 2), 6)) DESC, ari.id DESC;
```

### U5. Pagos aplicados o REP relacionados sin AR vigente para el fiscal document

```sql
SELECT
    fd.id AS fiscal_document_id,
    fs.uuid,
    COUNT(DISTINCT pcrd.payment_complement_document_id) AS rep_document_count,
    COUNT(DISTINCT arpa.accounts_receivable_payment_id) AS payment_count_via_applications,
    COUNT(DISTINCT ari.id) AS ar_invoice_count
FROM fiscal_document fd
LEFT JOIN fiscal_stamp fs
    ON fs.fiscal_document_id = fd.id
LEFT JOIN accounts_receivable_invoice ari
    ON ari.fiscal_document_id = fd.id
LEFT JOIN payment_complement_related_document pcrd
    ON pcrd.fiscal_document_id = fd.id
LEFT JOIN accounts_receivable_payment_application arpa
    ON arpa.accounts_receivable_invoice_id = ari.id
WHERE fd.document_type = 'I'
GROUP BY fd.id, fs.uuid
HAVING COUNT(DISTINCT ari.id) = 0
   AND (
        COUNT(DISTINCT pcrd.payment_complement_document_id) > 0
        OR COUNT(DISTINCT arpa.accounts_receivable_payment_id) > 0
   )
ORDER BY fd.id DESC;
```

### U6. CFDI cancelados o con cancelacion en proceso que siguen en universo REP interno

```sql
SELECT
    fd.id AS fiscal_document_id,
    fd.billing_document_id,
    fs.uuid,
    fd.status AS fiscal_status_id,
    CASE fd.status
        WHEN 5 THEN 'CancellationRequested'
        WHEN 6 THEN 'Cancelled'
        ELSE CONCAT('Other(', fd.status, ')')
    END AS fiscal_status_name,
    fd.payment_method_sat,
    fd.payment_form_sat,
    fd.total,
    COALESCE(ari.id, NULL) AS accounts_receivable_invoice_id,
    COUNT(DISTINCT pcrd.payment_complement_document_id) AS rep_document_count
FROM fiscal_document fd
LEFT JOIN fiscal_stamp fs
    ON fs.fiscal_document_id = fd.id
LEFT JOIN accounts_receivable_invoice ari
    ON ari.fiscal_document_id = fd.id
LEFT JOIN payment_complement_related_document pcrd
    ON pcrd.fiscal_document_id = fd.id
WHERE fd.document_type = 'I'
  AND fd.status IN (5, 6)
GROUP BY
    fd.id,
    fd.billing_document_id,
    fs.uuid,
    fd.status,
    fd.payment_method_sat,
    fd.payment_form_sat,
    fd.total,
    ari.id
ORDER BY fd.id DESC;
```

### U7. CFDI PUE en el universo interno REP con artefactos operativos

```sql
SELECT
    fd.id AS fiscal_document_id,
    fd.billing_document_id,
    fs.uuid,
    fd.status AS fiscal_status_id,
    fd.payment_method_sat,
    fd.payment_form_sat,
    fd.total,
    ari.id AS accounts_receivable_invoice_id,
    COUNT(DISTINCT pcrd.payment_complement_document_id) AS rep_document_count
FROM fiscal_document fd
LEFT JOIN fiscal_stamp fs
    ON fs.fiscal_document_id = fd.id
LEFT JOIN accounts_receivable_invoice ari
    ON ari.fiscal_document_id = fd.id
LEFT JOIN payment_complement_related_document pcrd
    ON pcrd.fiscal_document_id = fd.id
WHERE fd.document_type = 'I'
  AND UPPER(fd.payment_method_sat) = 'PUE'
GROUP BY
    fd.id,
    fd.billing_document_id,
    fs.uuid,
    fd.status,
    fd.payment_method_sat,
    fd.payment_form_sat,
    fd.total,
    ari.id
ORDER BY fd.id DESC;
```

### U8. CFDI con payment_method_sat distinto de PPD que aun asi caerian en attention-items por alertas operativas

```sql
SELECT
    fd.id AS fiscal_document_id,
    fs.uuid,
    fd.payment_method_sat,
    fd.payment_form_sat,
    COUNT(DISTINCT CASE WHEN pcd.status = 4 THEN pcd.id END) AS stamping_rejected_rep_count,
    COUNT(DISTINCT CASE WHEN pcd.status = 7 THEN pcd.id END) AS cancellation_rejected_rep_count
FROM fiscal_document fd
LEFT JOIN fiscal_stamp fs
    ON fs.fiscal_document_id = fd.id
LEFT JOIN payment_complement_related_document pcrd
    ON pcrd.fiscal_document_id = fd.id
LEFT JOIN payment_complement_document pcd
    ON pcd.id = pcrd.payment_complement_document_id
WHERE fd.document_type = 'I'
  AND UPPER(fd.payment_method_sat) <> 'PPD'
GROUP BY fd.id, fs.uuid, fd.payment_method_sat, fd.payment_form_sat
HAVING COUNT(DISTINCT CASE WHEN pcd.status = 4 THEN pcd.id END) > 0
    OR COUNT(DISTINCT CASE WHEN pcd.status = 7 THEN pcd.id END) > 0
ORDER BY fd.id DESC;
```

### U9. AR con fiscal_document inexistente o cancelado

```sql
SELECT
    ari.id AS accounts_receivable_invoice_id,
    ari.fiscal_document_id,
    ari.billing_document_id,
    ari.status AS ar_status_id,
    fd.status AS fiscal_status_id,
    CASE fd.status
        WHEN 5 THEN 'CancellationRequested'
        WHEN 6 THEN 'Cancelled'
        ELSE NULL
    END AS fiscal_status_name,
    fs.uuid,
    ari.total,
    ari.paid_total,
    ari.outstanding_balance
FROM accounts_receivable_invoice ari
LEFT JOIN fiscal_document fd
    ON fd.id = ari.fiscal_document_id
LEFT JOIN fiscal_stamp fs
    ON fs.id = ari.fiscal_stamp_id
WHERE fd.id IS NULL
   OR fd.status IN (5, 6)
ORDER BY ari.id DESC;
```

### U10. Duplicados de accounts_receivable_invoice para el mismo fiscal_document_id

```sql
SELECT
    fiscal_document_id,
    COUNT(*) AS invoice_count,
    GROUP_CONCAT(id ORDER BY id SEPARATOR ',') AS invoice_ids
FROM accounts_receivable_invoice
WHERE fiscal_document_id IS NOT NULL
GROUP BY fiscal_document_id
HAVING COUNT(*) > 1
ORDER BY invoice_count DESC, fiscal_document_id DESC;
```

## Interpretacion de resultados

### Caso 262

1. Si `Q262-1` y `Q262-0` muestran:
   - `status = Stamped`
   - `payment_method_sat = PPD`
   - `payment_form_sat = 99`
   - `currency_code = MXN`
   - `fiscal_stamp.uuid` no vacio
   - `accounts_receivable_invoice_id IS NULL`
   entonces el bloqueo `AccountsReceivableMissing` queda completamente explicado por codigo.

2. Si ademas:
   - `is_credit_sale = 1`
   - `credit_days > 0`
   - `Q262-10` no muestra `Created` ni `AlreadyExists`
   entonces `262` es candidato real a backfill controlado de AR.

3. Si `is_credit_sale = 0`:
   - el CFDI probablemente no debio entrar al flujo REP interno como base document operable
   - no es seguro crear AR sin evidencia de negocio
   - aqui el problema base es de elegibilidad/filtro o de datos de origen, no automaticamente de ausencia de AR

4. Si `payment_method_sat = PUE`:
   - no deberia ser base REP interna elegible
   - la correccion prioritaria seria el filtro/eligibilidad, no un backfill de AR

5. Si `Q262-7` encuentra REP timbrado relacionado:
   - detener cualquier idea de backfill automatico
   - `payment_complement_related_document.accounts_receivable_invoice_id` es requerido en el esquema
   - si existe REP ligado a `262`, debe auditarse la trazabilidad completa antes de tocar AR

6. Si `status` del CFDI es `Cancelled` o `CancellationRequested`:
   - no crear AR activa
   - la salida correcta es corregir la visibilidad o resolver el estado cancelatorio

7. Si `attention_items_outstanding_balance = 0` y no hay AR:
   - no significa que el CFDI este pagado
   - significa que el repositorio REP pone `0m` cuando no hay `accounts_receivable_invoice`
   - referencia: `RepBaseDocumentRepository.cs:283-284`

8. Si hay `accounts_receivable_payment_application` o `payment_complement_related_document` pero no `accounts_receivable_invoice`:
   - inconsistencia critica
   - requiere investigar integridad de datos antes de cualquier reparacion

### Casos 768 y 539

1. Si `Q768-539-1` muestra:
   - `ROUND(fd.total, 2) = ari.total`
   - `ROUND(ari.total - ari.paid_total, 2) = ROUND(ari.outstanding_balance, 2)`
   - pero `ari.outstanding_balance > fd.total`
   entonces el bloqueo es casi seguro un falso positivo por precision.

2. Si `Q768-539-2` y `Q768-539-4` muestran:
   - pagos/aplicaciones consistentes
   - sin duplicidades raras
   - sin delta entre `REP paid_amount` y `ari.paid_total`
   entonces no conviene corregir datos primero; conviene corregir el comparador del backend.

3. Si hay:
   - aplicaciones duplicadas
   - `ROUND(ari.total - ari.paid_total, 2) <> ROUND(ari.outstanding_balance, 2)`
   - o delta fuerte entre REP y AR
   entonces si puede existir un problema real de saldo y no solo de redondeo.

## Opciones de correccion

| Opcion | Cuando aplica | Que toca | Riesgo | Reversible | Recomendacion |
|---|---|---|---|---|---|
| A. Backfill controlado de `accounts_receivable_invoice` para `262` | Solo si `262` esta timbrado, `PPD/99`, `MXN`, `is_credit_sale = 1`, `credit_days > 0`, sin AR previa, sin cancelacion y sin REP/pagos ambiguos | `accounts_receivable_invoice` y `audit_event` | Medio: puede habilitar operaciones REP nuevas sobre un CFDI historico | Si, mientras no se creen dependencias nuevas | Recomendable solo si las queries confirman elegibilidad operativa completa |
| B. Backfill masivo de todos los PPD timbrados sin AR | Solo despues de segmentar por `is_credit_sale`, `credit_days`, cancelaciones, moneda, REP ya emitido y evidencia de negocio | `accounts_receivable_invoice` y `audit_event` por lote | Alto: mezcla casos sanos con casos donde el CFDI nunca debio ser AR | Parcialmente, con mucho control por batch | No recomendable como primer movimiento |
| C. Corregir filtro/eligibilidad de `attention-items` | Si aparecen CFDI `PPD/99` sin venta a credito o casos que la bandeja REP no deberia promover | Codigo de REP interno (`eligibility` / `tray`) | Bajo en datos, medio funcional | Si, via deploy | Muy recomendable para evitar recurrencia |
| D. Corregir saldos operativos inconsistentes recalculando desde aplicaciones | Si `768/539` muestran inconsistencia real de datos, no solo de precision | `accounts_receivable_invoice`, posiblemente `audit_event` y utileria de reparacion | Medio/alto | Si se hace por script transaccional | No es la primera opcion si el problema es solo precision |
| D-alt. Corregir comparacion REP para usar precision operativa 2dp | Si `768/539` resultan falsos positivos por redondeo | Codigo REP (`InternalRepBaseDocumentEligibilityRule`) | Bajo/medio funcional | Si, via deploy | Muy recomendable si las queries confirman el patron |
| E. Script operativo seguro con `dryRun` | Si se decide reparar uno o varios casos con evidencia | Script operativo, `accounts_receivable_invoice`, `audit_event` | Controlable si se valida por batch | Si, con batch y prechecks | Recomendado como mecanismo de ejecucion, no como decision de negocio |

## Correccion recomendada

### Recomendacion para `262`

La decision correcta depende completamente de `Q262-0`, `Q262-5` y `Q262-10`.

Recomendacion:

1. Si `262` cumple todos los prechecks de AR actual (`Stamped`, `PPD/99`, `MXN`, `is_credit_sale = 1`, `credit_days > 0`, UUID persistido, sin AR, sin REP timbrado, sin aplicaciones/pagos ambiguos):
   - aplicar `Opcion A` mediante `Opcion E`
   - backfill controlado y documentado para ese `fiscal_document_id`

2. Si `262` es `PPD/99` pero `is_credit_sale = 0` o `credit_days <= 0`:
   - no backfillear AR automaticamente
   - primero corregir el criterio de la bandeja (`Opcion C`)
   - solo considerar reparacion de datos si existe evidencia externa de que el CFDI fue emitido como venta a credito real

### Recomendacion para `768` y `539`

Si las queries confirman que la inconsistencia es solo:

- `fiscal_document.total` a 6 decimales
- contra `accounts_receivable_invoice.total/outstanding_balance` a 2 decimales

entonces la correccion recomendada es:

1. No tocar datos todavia.
2. Corregir la comparacion REP para usar precision operativa (`Opcion D-alt`).
3. Volver a evaluar si siguen bloqueados.

## Diseno de script/backfill seguro

Si la correccion aprobada para `262` o para otros casos elegibles implica backfill, el script futuro deberia cumplir esto:

1. `dryRun = true` por default.
2. Lista explicita de `fiscal_document_id` permitidos.
3. `expected_database_name` obligatorio y validado al inicio.
4. Guard de ambiente:
   - abortar si no coincide `expected_database_name`
   - abortar si el ambiente no es el esperado
5. Pre-lectura y backup logico de filas relacionadas antes de tocar nada:
   - `fiscal_document`
   - `fiscal_stamp`
   - `accounts_receivable_invoice`
   - `accounts_receivable_payment_application`
   - `payment_complement_related_document`
   - `audit_event` de contexto
6. Transaccion por batch completo o por documento, segun tamano.
7. Idempotencia:
   - si ya existe `accounts_receivable_invoice`, no insertar
   - si el batch ya corrio para ese documento, no repetir
8. Validaciones por documento:
   - `fiscal_document` existe
   - `document_type = 'I'`
   - `status = Stamped`
   - no `Cancelled`
   - no `CancellationRequested`
   - `payment_method_sat = 'PPD'`
   - `payment_form_sat = '99'`
   - `currency_code = 'MXN'`
   - `is_credit_sale = 1`
   - `credit_days > 0`
   - `total > 0`
   - existe `fiscal_stamp.uuid`
   - existe `fiscal_receiver` operativo
   - no existe `accounts_receivable_invoice` previa
   - no existe REP timbrado o aplicaciones que hagan inseguro el backfill
9. Monto operativo calculado igual que el servicio actual:
   - `ROUND(fiscal_document.total, 2)`
10. Insercion minima:
   - solo `accounts_receivable_invoice`
   - no tocar `fiscal_document`
   - no tocar `fiscal_stamp`
   - no tocar UUID/XML/timbrado/cancelaciones
11. Auditoria:
   - escribir `audit_event` con `batch_id`, actor tecnico, prechecks, resultado y `invoice_id`
12. Salida de `dryRun`:
   - documentos elegibles
   - documentos rechazados y motivo
   - SQL/JSON de respaldo
13. Rollback por `batch_id`:
   - solo permitido si no existen dependencias posteriores (`accounts_receivable_payment_application`, `payment_complement_related_document`) sobre las filas insertadas por el batch

## Validaciones antes de tocar produccion

1. Ejecutar `Q262-0` y confirmar el `current_primary_reason_code`.
2. Ejecutar `Q262-10` y verificar si hubo `EnsureAfterFiscalStamp` con `Skipped`, `ValidationFailed` o ausencia total.
3. Confirmar si `262` tiene `is_credit_sale = 1`.
4. Confirmar `credit_days > 0`.
5. Confirmar `status = Stamped`, no cancelado.
6. Confirmar `payment_method_sat = PPD` y `payment_form_sat = 99`.
7. Confirmar `currency_code = MXN`.
8. Confirmar ausencia de `accounts_receivable_invoice`.
9. Confirmar ausencia de `accounts_receivable_payment_application` y REP ligados que vuelvan ambiguo el caso.
10. Ejecutar `Q768-539-1` y `Q768-539-4` para separar precision vs corrupcion real.
11. Ejecutar `U1`, `U1B`, `U3` y `U4` para dimensionar el universo afectado antes de cualquier accion puntual.

## Validaciones despues de corregir

### Si se hace backfill de AR para `262`

1. `accounts_receivable_invoice` existe y apunta a `fiscal_document_id = 262`.
2. `total = ROUND(fiscal_document.total, 2)`.
3. `paid_total = 0`.
4. `outstanding_balance = total`.
5. El portafolio AR ya muestra el documento.
6. `attention-items` deja de mostrar `AccountsReceivableMissing`.
7. No hubo cambios en:
   - `fiscal_stamp.uuid`
   - `fiscal_stamp.xml_content`
   - `fiscal_document.total`
   - cancelaciones

#### Query sugerida para validar `262` despues del backfill

```sql
SELECT
    fd.id AS fiscal_document_id,
    fd.billing_document_id,
    fs.id AS fiscal_stamp_id,
    fs.uuid,
    fd.status AS fiscal_status_id,
    fs.status AS fiscal_stamp_status_id,
    fd.payment_method_sat,
    fd.payment_form_sat,
    fd.is_credit_sale,
    fd.credit_days,
    fd.currency_code,
    fd.total AS fiscal_total_raw,
    ROUND(fd.total, 2) AS fiscal_total_2dp,
    ari.id AS accounts_receivable_invoice_id,
    ari.status AS ar_status_id,
    ari.total AS ar_total,
    ari.paid_total,
    ari.outstanding_balance,
    ari.issued_at_utc,
    ari.due_at_utc,
    CHAR_LENGTH(fs.xml_content) AS fiscal_stamp_xml_length
FROM fiscal_document fd
LEFT JOIN fiscal_stamp fs
    ON fs.fiscal_document_id = fd.id
LEFT JOIN accounts_receivable_invoice ari
    ON ari.fiscal_document_id = fd.id
WHERE fd.id = 262;
```

#### Query sugerida para asegurar que `262` sigue sin REP ni aplicaciones despues del backfill inicial

```sql
SELECT
    fd.id AS fiscal_document_id,
    COALESCE(rep.rep_count, 0) AS payment_complement_related_count,
    COALESCE(app.application_count, 0) AS payment_application_count
FROM fiscal_document fd
LEFT JOIN (
    SELECT
        rd.fiscal_document_id,
        COUNT(*) AS rep_count
    FROM payment_complement_related_document rd
    WHERE rd.fiscal_document_id = 262
    GROUP BY rd.fiscal_document_id
) rep
    ON rep.fiscal_document_id = fd.id
LEFT JOIN (
    SELECT
        ari.fiscal_document_id,
        COUNT(*) AS application_count
    FROM accounts_receivable_invoice ari
    INNER JOIN accounts_receivable_payment_application arpa
        ON arpa.accounts_receivable_invoice_id = ari.id
    WHERE ari.fiscal_document_id = 262
    GROUP BY ari.fiscal_document_id
) app
    ON app.fiscal_document_id = fd.id
WHERE fd.id = 262;
```

### Si se corrige la comparacion de precision para `768/539`

1. Dejan de aparecer como `OperationalBalanceInconsistent` si no habia anomalia real.
2. Siguen coincidiendo:
   - `ROUND(fd.total, 2)`
   - `ari.total`
   - `ROUND(ari.total - ari.paid_total, 2)`
   - `ROUND(ari.outstanding_balance, 2)`
3. Ningun otro documento legitimo queda habilitado por error.

#### Query sugerida para comparar regla antigua vs regla nueva en `539` y `768`

```sql
SELECT
    fd.id AS fiscal_document_id,
    fs.uuid,
    fd.total AS fiscal_total_raw,
    ROUND(fd.total, 2) AS fiscal_total_2dp,
    ari.total AS ar_total,
    ari.paid_total,
    ari.outstanding_balance,
    ROUND(ari.total - ari.paid_total, 2) AS ar_expected_outstanding_2dp,
    CASE
        WHEN ari.paid_total < 0
            OR ari.outstanding_balance < 0
            OR ari.paid_total > fd.total
            OR ari.outstanding_balance > fd.total
        THEN 1 ELSE 0
    END AS would_block_old_rule,
    CASE
        WHEN ari.paid_total < 0 THEN 1
        WHEN ari.outstanding_balance < 0 THEN 1
        WHEN ari.total < 0 THEN 1
        WHEN ROUND(ari.paid_total, 2) > ROUND(fd.total, 2) + 0.01 THEN 1
        WHEN ROUND(ari.outstanding_balance, 2) > ROUND(fd.total, 2) + 0.01 THEN 1
        WHEN ABS(ROUND(ari.total, 2) - ROUND(fd.total, 2)) > 0.01 THEN 1
        WHEN ABS((ROUND(ari.paid_total, 2) + ROUND(ari.outstanding_balance, 2)) - ROUND(fd.total, 2)) > 0.01 THEN 1
        WHEN ABS((ROUND(ari.paid_total, 2) + ROUND(ari.outstanding_balance, 2)) - ROUND(ari.total, 2)) > 0.01 THEN 1
        ELSE 0
    END AS would_block_new_rule
FROM fiscal_document fd
INNER JOIN fiscal_stamp fs
    ON fs.fiscal_document_id = fd.id
INNER JOIN accounts_receivable_invoice ari
    ON ari.fiscal_document_id = fd.id
WHERE fd.id IN (539, 768)
ORDER BY fd.id;
```

#### Validacion funcional posterior al deploy

Estas dos comprobaciones ya no son solo SQL; requieren reproducir la bandeja REP:

1. `GET /api/payment-complements/attention-items?page=1&pageSize=25&receiverRfc=PSC9603298Z8`
   - `262` no debe salir con `AccountsReceivableMissing` una vez insertada la AR.
   - `539` y `768` ya no deben salir con `OperationalBalanceInconsistent` si su unico problema era precision.
2. `GET /api/accounts-receivable/portfolio?...`
   - `262` debe aparecer en el portafolio AR despues del backfill.

## Implementacion aplicada en codigo

### Fix REP / precision monetaria

- `src/Pineda.Facturacion.Application/Common/CfdiMonetaryRules.cs`
- `src/Pineda.Facturacion.Application/UseCases/PaymentComplements/InternalRepBaseDocumentEligibilityRule.cs`
- `src/Pineda.Facturacion.Application/UseCases/PaymentComplements/InternalRepBaseDocumentEligibilitySnapshot.cs`
- `src/Pineda.Facturacion.Application/UseCases/PaymentComplements/InternalRepBaseDocumentSummaryReadModel.cs`
- `src/Pineda.Facturacion.Application/UseCases/PaymentComplements/SearchInternalRepBaseDocumentsService.cs`
- `src/Pineda.Facturacion.Application/UseCases/PaymentComplements/RegisterInternalRepBaseDocumentPaymentService.cs`
- `src/Pineda.Facturacion.Infrastructure.BillingWrite/Persistence/Repositories/RepBaseDocumentRepository.cs`

La regla actualizada deja de comparar `paid_total` y `outstanding_balance` contra `fiscal_document.total` crudo y pasa a:

1. Normalizar `fiscal_document.total`, `accounts_receivable_invoice.total`, `paid_total` y `outstanding_balance` a precision monetaria de la moneda.
2. Aplicar tolerancia monetaria por moneda (`MXN = 0.01`).
3. Seguir bloqueando negativos, excesos sobre el total normalizado y descuadres reales entre `paid_total + outstanding_balance` vs total operativo.

### Comando operativo de backfill seguro

- `src/Pineda.Facturacion.Api/OperationalHardening/MissingAccountsReceivableBackfillCli.cs`
- `src/Pineda.Facturacion.Infrastructure.BillingWrite/Operations/AccountsReceivable/BackfillMissingAccountsReceivableInvoicesService.cs`
- `src/Pineda.Facturacion.Api/Program.cs`
- `src/Pineda.Facturacion.Infrastructure.BillingWrite/DependencyInjection/ServiceCollectionExtensions.cs`

Caracteristicas implementadas:

1. `dryRun` por default.
2. Lista explicita de `--fiscal-document-ids`.
3. `--expected-database-name` obligatorio para `--commit`.
4. `--requested-by` obligatorio para `--commit`.
5. `--batch-id` opcional para correlacion y futura reversibilidad manual.
6. Guard de produccion con `ALLOW_PROD_MISSING_AR_BACKFILL=true`.
7. Reutiliza `CreateAccountsReceivableInvoiceFromFiscalDocumentService` para la insercion real.
8. No toca `fiscal_document`, `fiscal_stamp`, `uuid`, `xml_content`, timbrado ni cancelaciones.
9. Escribe `audit_event` solo en `commit`, con `correlation_id = batch_id` y snapshot de prechecks.

## Riesgos

1. Crear AR para un CFDI que nunca debio ser venta a credito puede habilitar pagos/REP sobre una base documental equivocada.
2. Si existe REP timbrado previo o aplicaciones historicas fuera del flujo normal, un backfill tardio puede duplicar trazabilidad operativa.
3. Corregir datos sin separar problema de precision vs problema real puede esconder anomalias en vez de repararlas.
4. Un backfill masivo sin segmentacion por `is_credit_sale`, `credit_days`, cancelacion y REP previo tiene alto riesgo de introducir deuda operativa.
5. Cambiar la bandeja REP sin alinear reglas de negocio puede ocultar casos que todavia requieren reparacion de datos.

## Preguntas abiertas

1. `262` fue emitido intencionalmente como venta a credito o solo como `PPD/99` sin `is_credit_sale`?
2. En produccion hubo despliegue del hook `EnsureAfterFiscalStamp` al momento en que se timbro `262`?
3. Existe evidencia comercial externa de plazo/credito para `262`?
4. `768` y `539` presentan solo precision o hay aplicaciones/REP que tambien descuadran?
5. Existen mas documentos `PPD/99` con `is_credit_sale = 0` entrando a la bandeja REP?
6. Hay algun caso en produccion con REP ligado a CFDI sin AR que indique bypass manual de integridad?
