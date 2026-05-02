import {
  ProductFiscalProfileRecoverySuggestion,
  UpsertProductFiscalProfileRequest,
} from '../../catalogs/models/catalogs.models';
import {
  MissingProductFiscalProfileResponse,
  PrepareFiscalDocumentResponse,
} from '../models/fiscal-documents.models';

export interface MissingProductFiscalProfileContext {
  internalCode: string;
  billingDocumentItemId?: number | null;
  lineNumber?: number | null;
  description: string;
  existingProfileStatus: 'Active' | 'Inactive' | 'None' | string;
  existingProductFiscalProfileId?: number | null;
  canUseExplicitGeneric: boolean;
  reviewMessages?: string[];
  requiresExplicitProductServiceConfirmation: boolean;
  suggestions: ProductFiscalProfileRecoverySuggestion[];
  draft: UpsertProductFiscalProfileRequest;
}

export function resolveMissingProductFiscalProfileContext(
  response: PrepareFiscalDocumentResponse,
  options?: {
    fallbackDescription?: string | null;
  },
): MissingProductFiscalProfileContext | null {
  if (response.outcome !== 'MissingProductFiscalProfile') {
    return null;
  }

  const missingProfile = response.missingProductFiscalProfile;
  if (!missingProfile?.internalCode?.trim()) {
    return null;
  }

  return buildMissingProductFiscalProfileContext(missingProfile, options);
}

export function extractMissingProductFiscalProfileContext(
  error: unknown,
  options?: {
    fallbackDescription?: string | null;
  },
): MissingProductFiscalProfileContext | null {
  if (typeof error !== 'object' || !error || !('error' in error)) {
    return null;
  }

  const payload = (error as { error?: Partial<PrepareFiscalDocumentResponse> }).error;
  if (
    !payload
    || payload.outcome !== 'MissingProductFiscalProfile'
    || !payload.missingProductFiscalProfile
  ) {
    return null;
  }

  return buildMissingProductFiscalProfileContext(payload.missingProductFiscalProfile, options);
}

function buildMissingProductFiscalProfileContext(
  missingProfile: MissingProductFiscalProfileResponse,
  options?: {
    fallbackDescription?: string | null;
  },
): MissingProductFiscalProfileContext | null {
  const internalCode = missingProfile.internalCode?.trim();
  if (!internalCode) {
    return null;
  }

  const description = (
    options?.fallbackDescription?.trim()
    || missingProfile.description?.trim()
    || internalCode
  ).trim();
  const prefill = missingProfile.prefill;
  const reviewMessages = (missingProfile.reviewMessages ?? [])
    .map((message) => message.trim())
    .filter(Boolean);

  return {
    internalCode,
    billingDocumentItemId: missingProfile.billingDocumentItemId ?? null,
    lineNumber: missingProfile.lineNumber ?? null,
    description,
    existingProfileStatus: missingProfile.existingProfileStatus ?? 'None',
    existingProductFiscalProfileId: missingProfile.existingProductFiscalProfileId ?? null,
    canUseExplicitGeneric: missingProfile.canUseExplicitGeneric ?? true,
    ...(reviewMessages.length ? { reviewMessages } : {}),
    requiresExplicitProductServiceConfirmation:
      prefill?.requiresExplicitProductServiceConfirmation ?? false,
    suggestions: (missingProfile.suggestions ?? []).slice(),
    draft: {
      internalCode,
      description,
      satProductServiceCode: prefill?.satProductServiceCode?.trim() || '',
      satUnitCode: prefill?.satUnitCode?.trim() || 'H87',
      taxObjectCode: prefill?.taxObjectCode?.trim() || '02',
      vatRate: prefill?.vatRate ?? 0.16,
      defaultUnitText: prefill?.defaultUnitText?.trim() || 'PIEZA',
      isActive: prefill?.isActive ?? true,
    },
  };
}
