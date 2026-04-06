import { UpsertProductFiscalProfileRequest } from '../../catalogs/models/catalogs.models';
import { PrepareFiscalDocumentResponse } from '../models/fiscal-documents.models';

export interface MissingProductFiscalProfileContext {
  internalCode: string;
  lineNumber?: number | null;
  description: string;
  draft: UpsertProductFiscalProfileRequest;
}

export function resolveMissingProductFiscalProfileContext(
  response: PrepareFiscalDocumentResponse,
  options?: {
    fallbackDescription?: string | null;
  }
): MissingProductFiscalProfileContext | null {
  if (response.outcome !== 'MissingProductFiscalProfile') {
    return null;
  }

  const parsed = parseMissingProductFiscalProfileError(response.errorMessage);
  if (!parsed.internalCode) {
    return null;
  }

  const description = (options?.fallbackDescription?.trim() || parsed.internalCode).trim();

  return {
    internalCode: parsed.internalCode,
    lineNumber: parsed.lineNumber,
    description,
    draft: {
      internalCode: parsed.internalCode,
      description,
      satProductServiceCode: '',
      satUnitCode: 'H87',
      taxObjectCode: '02',
      vatRate: 0.16,
      defaultUnitText: 'PIEZA',
      isActive: true
    }
  };
}

export function extractMissingProductFiscalProfileContext(
  error: unknown,
  options?: {
    fallbackDescription?: string | null;
  }
): MissingProductFiscalProfileContext | null {
  if (typeof error !== 'object' || !error || !('error' in error)) {
    return null;
  }

  const payload = (error as { error?: Partial<PrepareFiscalDocumentResponse> }).error;
  if (!payload || payload.outcome !== 'MissingProductFiscalProfile') {
    return null;
  }

  return resolveMissingProductFiscalProfileContext({
    outcome: payload.outcome,
    isSuccess: payload.isSuccess ?? false,
    errorMessage: payload.errorMessage ?? null,
    billingDocumentId: payload.billingDocumentId ?? 0,
    fiscalDocumentId: payload.fiscalDocumentId ?? null,
    status: payload.status ?? null
  }, options);
}

export function parseMissingProductFiscalProfileError(errorMessage?: string | null): {
  lineNumber?: number | null;
  internalCode?: string | null;
} {
  if (!errorMessage) {
    return {};
  }

  const lineMatch = errorMessage.match(/item line '(\d+)'/i);
  const codeMatch = errorMessage.match(/internal code '([^']+)'/i);

  return {
    lineNumber: lineMatch ? Number(lineMatch[1]) : null,
    internalCode: codeMatch?.[1] ?? null
  };
}
