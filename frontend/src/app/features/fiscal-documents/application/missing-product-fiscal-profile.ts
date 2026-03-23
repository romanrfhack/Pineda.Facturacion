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
      satProductServiceCode: '01010101',
      satUnitCode: 'H87',
      taxObjectCode: '02',
      vatRate: 0.16,
      defaultUnitText: 'PIEZA',
      isActive: true
    }
  };
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
