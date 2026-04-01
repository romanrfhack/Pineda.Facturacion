import { HttpErrorResponse } from '@angular/common/http';
import { adaptImportLegacyOrderConflict, extractImportLegacyOrderConflict, LEGACY_ORDER_HASH_CONFLICT_ERROR_CODE } from './import-legacy-order-conflict';

describe('import-legacy-order-conflict', () => {
  it('adapts the enriched conflict payload to the UI model', () => {
    const model = adaptImportLegacyOrderConflict({
      outcome: 'Conflict',
      isSuccess: false,
      errorCode: LEGACY_ORDER_HASH_CONFLICT_ERROR_CODE,
      errorMessage: "Legacy order '1173481' was already imported with a different source hash.",
      legacyOrderId: '1173481',
      existingSalesOrderId: 123,
      existingSalesOrderStatus: 'SnapshotCreated',
      existingBillingDocumentId: 45,
      existingBillingDocumentStatus: 'Draft',
      existingFiscalDocumentId: 78,
      existingFiscalDocumentStatus: 'Stamped',
      fiscalUuid: 'UUID-1',
      importedAtUtc: '2026-04-01T12:00:00Z',
      existingSourceHash: 'abc',
      currentSourceHash: 'xyz',
      allowedActions: ['view_existing_sales_order', 'view_existing_billing_document', 'view_existing_fiscal_document', 'reimport_not_available']
    });

    expect(model).toEqual({
      legacyOrderId: '1173481',
      errorCode: LEGACY_ORDER_HASH_CONFLICT_ERROR_CODE,
      errorMessage: "Legacy order '1173481' was already imported with a different source hash.",
      existingSalesOrderId: 123,
      existingSalesOrderStatus: 'SnapshotCreated',
      existingBillingDocumentId: 45,
      existingBillingDocumentStatus: 'Draft',
      existingFiscalDocumentId: 78,
      existingFiscalDocumentStatus: 'Stamped',
      fiscalUuid: 'UUID-1',
      importedAtUtc: '2026-04-01T12:00:00Z',
      existingSourceHash: 'abc',
      currentSourceHash: 'xyz',
      allowedActions: ['view_existing_sales_order', 'view_existing_billing_document', 'view_existing_fiscal_document', 'reimport_not_available']
    });
  });

  it('extracts the enriched conflict from HttpErrorResponse', () => {
    const model = extractImportLegacyOrderConflict(new HttpErrorResponse({
      status: 409,
      error: {
        outcome: 'Conflict',
        isSuccess: false,
        errorCode: LEGACY_ORDER_HASH_CONFLICT_ERROR_CODE,
        errorMessage: "Legacy order '1173481' was already imported with a different source hash.",
        legacyOrderId: '1173481',
        existingSalesOrderId: 123,
        allowedActions: ['view_existing_sales_order', 'reimport_not_available']
      }
    }));

    expect(model?.legacyOrderId).toBe('1173481');
    expect(model?.existingSalesOrderId).toBe(123);
    expect(model?.allowedActions).toEqual(['view_existing_sales_order', 'reimport_not_available']);
  });
});
