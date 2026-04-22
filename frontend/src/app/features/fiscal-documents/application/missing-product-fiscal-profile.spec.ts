import {
  extractMissingProductFiscalProfileContext,
  resolveMissingProductFiscalProfileContext,
} from './missing-product-fiscal-profile';

describe('missing-product-fiscal-profile helper', () => {
  it('builds a prefilled recovery context from the structured backend payload', () => {
    const context = resolveMissingProductFiscalProfileContext({
      outcome: 'MissingProductFiscalProfile',
      isSuccess: false,
      errorMessage: 'Product fiscal profile exists but is inactive.',
      billingDocumentId: 3,
      fiscalDocumentId: null,
      status: null,
      missingProductFiscalProfile: {
        billingDocumentItemId: 10,
        lineNumber: 1,
        internalCode: 'MTE-4259',
        description: 'FILTRO DE ACEITE',
        existingProfileStatus: 'Inactive',
        existingProductFiscalProfileId: 33,
        canUseExplicitGeneric: true,
        prefill: {
          satProductServiceCode: '40161513',
          satUnitCode: 'H87',
          taxObjectCode: '02',
          vatRate: 0.16,
          defaultUnitText: 'PIEZA',
          isActive: true,
          requiresExplicitProductServiceConfirmation: false,
        },
        suggestions: [
          {
            satProductServiceCode: '40161513',
            satProductServiceDescription: 'Filtro de aceite',
            satUnitCode: 'H87',
            satUnitDescription: 'Pieza',
            taxObjectCode: '02',
            vatRate: 0.16,
            defaultUnitText: 'PIEZA',
            score: 0.97,
            confidence: 0.88,
            source: 'product_fiscal_profile_current',
            matchKind: 'currentProfile',
            reason: 'Perfil fiscal actual encontrado para el mismo codigo interno, pero el maestro esta inactivo.',
            isActive: true,
            requiresExplicitConfirmation: false,
          },
        ],
      },
    });

    expect(context).toEqual({
      internalCode: 'MTE-4259',
      billingDocumentItemId: 10,
      lineNumber: 1,
      description: 'FILTRO DE ACEITE',
      existingProfileStatus: 'Inactive',
      existingProductFiscalProfileId: 33,
      canUseExplicitGeneric: true,
      requiresExplicitProductServiceConfirmation: false,
      suggestions: [
        {
          satProductServiceCode: '40161513',
          satProductServiceDescription: 'Filtro de aceite',
          satUnitCode: 'H87',
          satUnitDescription: 'Pieza',
          taxObjectCode: '02',
          vatRate: 0.16,
          defaultUnitText: 'PIEZA',
          score: 0.97,
          confidence: 0.88,
          source: 'product_fiscal_profile_current',
          matchKind: 'currentProfile',
          reason: 'Perfil fiscal actual encontrado para el mismo codigo interno, pero el maestro esta inactivo.',
          isActive: true,
          requiresExplicitConfirmation: false,
        },
      ],
      draft: {
        internalCode: 'MTE-4259',
        description: 'FILTRO DE ACEITE',
        satProductServiceCode: '40161513',
        satUnitCode: 'H87',
        taxObjectCode: '02',
        vatRate: 0.16,
        defaultUnitText: 'PIEZA',
        isActive: true,
      },
    });
  });

  it('prefers the real fallback description when it is provided', () => {
    const context = resolveMissingProductFiscalProfileContext(
      {
        outcome: 'MissingProductFiscalProfile',
        isSuccess: false,
        errorMessage: 'No active product fiscal profile exists.',
        billingDocumentId: 3,
        fiscalDocumentId: null,
        status: null,
        missingProductFiscalProfile: {
          billingDocumentItemId: 10,
          lineNumber: 1,
          internalCode: 'GP-149',
          description: 'GP-149',
          existingProfileStatus: 'None',
          existingProductFiscalProfileId: null,
          canUseExplicitGeneric: true,
          prefill: {
            satProductServiceCode: '',
            satUnitCode: 'H87',
            taxObjectCode: '02',
            vatRate: 0.16,
            defaultUnitText: 'PIEZA',
            isActive: true,
            requiresExplicitProductServiceConfirmation: true,
          },
          suggestions: [],
        },
      },
      {
        fallbackDescription: 'FILTRO DE ACEITE',
      },
    );

    expect(context?.description).toBe('FILTRO DE ACEITE');
    expect(context?.draft.description).toBe('FILTRO DE ACEITE');
    expect(context?.draft.internalCode).toBe('GP-149');
  });

  it('extracts recovery context from HttpErrorResponse-like payload without parsing the message', () => {
    expect(
      extractMissingProductFiscalProfileContext({
        status: 400,
        error: {
          outcome: 'MissingProductFiscalProfile',
          isSuccess: false,
          errorMessage: 'Mensaje libre no parseable.',
          billingDocumentId: 3,
          fiscalDocumentId: null,
          status: null,
          missingProductFiscalProfile: {
            billingDocumentItemId: 12,
            lineNumber: 2,
            internalCode: 'ABC-123',
            description: 'Producto ABC',
            existingProfileStatus: 'None',
            existingProductFiscalProfileId: null,
            canUseExplicitGeneric: true,
            prefill: {
              satProductServiceCode: '',
              satUnitCode: 'H87',
              taxObjectCode: '02',
              vatRate: 0.16,
              defaultUnitText: 'PIEZA',
              isActive: true,
              requiresExplicitProductServiceConfirmation: true,
            },
            suggestions: [],
          },
        },
      }),
    ).toEqual({
      internalCode: 'ABC-123',
      billingDocumentItemId: 12,
      lineNumber: 2,
      description: 'Producto ABC',
      existingProfileStatus: 'None',
      existingProductFiscalProfileId: null,
      canUseExplicitGeneric: true,
      requiresExplicitProductServiceConfirmation: true,
      suggestions: [],
      draft: {
        internalCode: 'ABC-123',
        description: 'Producto ABC',
        satProductServiceCode: '',
        satUnitCode: 'H87',
        taxObjectCode: '02',
        vatRate: 0.16,
        defaultUnitText: 'PIEZA',
        isActive: true,
      },
    });
  });
});
