import { HttpErrorResponse } from '@angular/common/http';
import { ImportLegacyOrderAllowedAction, ImportLegacyOrderResponse } from '../models/orders.models';

export const LEGACY_ORDER_HASH_CONFLICT_ERROR_CODE = 'LegacyOrderAlreadyImportedWithDifferentSourceHash';

export interface ImportLegacyOrderConflictViewModel {
  readonly legacyOrderId: string;
  readonly errorCode: string;
  readonly errorMessage: string;
  readonly existingSalesOrderId: number | null;
  readonly existingSalesOrderStatus: string | null;
  readonly existingBillingDocumentId: number | null;
  readonly existingBillingDocumentStatus: string | null;
  readonly existingFiscalDocumentId: number | null;
  readonly existingFiscalDocumentStatus: string | null;
  readonly fiscalUuid: string | null;
  readonly importedAtUtc: string | null;
  readonly existingSourceHash: string | null;
  readonly currentSourceHash: string | null;
  readonly allowedActions: ImportLegacyOrderAllowedAction[];
}

export function extractImportLegacyOrderConflict(error: unknown): ImportLegacyOrderConflictViewModel | null {
  if (!(error instanceof HttpErrorResponse) || typeof error.error !== 'object' || !error.error) {
    return null;
  }

  return adaptImportLegacyOrderConflict(error.error as Partial<ImportLegacyOrderResponse>);
}

export function adaptImportLegacyOrderConflict(payload: Partial<ImportLegacyOrderResponse>): ImportLegacyOrderConflictViewModel | null {
  if (payload.outcome !== 'Conflict' || payload.errorCode !== LEGACY_ORDER_HASH_CONFLICT_ERROR_CODE || !payload.legacyOrderId) {
    return null;
  }

  return {
    legacyOrderId: payload.legacyOrderId,
    errorCode: payload.errorCode,
    errorMessage: payload.errorMessage ?? `La orden legacy '${payload.legacyOrderId}' cambió desde la importación anterior.`,
    existingSalesOrderId: payload.existingSalesOrderId ?? payload.salesOrderId ?? null,
    existingSalesOrderStatus: payload.existingSalesOrderStatus ?? payload.importStatus ?? null,
    existingBillingDocumentId: payload.existingBillingDocumentId ?? null,
    existingBillingDocumentStatus: payload.existingBillingDocumentStatus ?? null,
    existingFiscalDocumentId: payload.existingFiscalDocumentId ?? null,
    existingFiscalDocumentStatus: payload.existingFiscalDocumentStatus ?? null,
    fiscalUuid: payload.fiscalUuid ?? null,
    importedAtUtc: payload.importedAtUtc ?? null,
    existingSourceHash: payload.existingSourceHash ?? null,
    currentSourceHash: payload.currentSourceHash ?? payload.sourceHash ?? null,
    allowedActions: payload.allowedActions ?? []
  };
}
