import { extractMissingProductFiscalProfileContext, parseMissingProductFiscalProfileError, resolveMissingProductFiscalProfileContext } from './missing-product-fiscal-profile';

describe('missing-product-fiscal-profile helper', () => {
  it('extracts line number and internal code from the backend error message', () => {
    expect(parseMissingProductFiscalProfileError(
      "No active product fiscal profile exists for item line '1' and internal code 'MTE-4259'."
    )).toEqual({
      lineNumber: 1,
      internalCode: 'MTE-4259'
    });
  });

  it('builds a prefilled draft for recovery', () => {
    const context = resolveMissingProductFiscalProfileContext({
      outcome: 'MissingProductFiscalProfile',
      isSuccess: false,
      errorMessage: "No active product fiscal profile exists for item line '1' and internal code 'MTE-4259'.",
      billingDocumentId: 3,
      fiscalDocumentId: null,
      status: null
    });

    expect(context).toEqual({
      internalCode: 'MTE-4259',
      lineNumber: 1,
      description: 'MTE-4259',
      draft: {
        internalCode: 'MTE-4259',
        description: 'MTE-4259',
        satProductServiceCode: '01010101',
        satUnitCode: 'H87',
        taxObjectCode: '02',
        vatRate: 0.16,
        defaultUnitText: 'PIEZA',
        isActive: true
      }
    });
  });

  it('prefers the real fallback description when it is provided', () => {
    const context = resolveMissingProductFiscalProfileContext({
      outcome: 'MissingProductFiscalProfile',
      isSuccess: false,
      errorMessage: "No active product fiscal profile exists for item line '1' and internal code 'GP-149'.",
      billingDocumentId: 3,
      fiscalDocumentId: null,
      status: null
    }, {
      fallbackDescription: 'FILTRO DE ACEITE'
    });

    expect(context?.description).toBe('FILTRO DE ACEITE');
    expect(context?.draft.description).toBe('FILTRO DE ACEITE');
    expect(context?.draft.internalCode).toBe('GP-149');
  });

  it('extracts recovery context from HttpErrorResponse-like payload', () => {
    expect(extractMissingProductFiscalProfileContext({
      status: 400,
      error: {
        outcome: 'MissingProductFiscalProfile',
        isSuccess: false,
        errorMessage: "No active product fiscal profile exists for item line '2' and internal code 'ABC-123'.",
        billingDocumentId: 3,
        fiscalDocumentId: null,
        status: null
      }
    })).toEqual({
      internalCode: 'ABC-123',
      lineNumber: 2,
      description: 'ABC-123',
      draft: {
        internalCode: 'ABC-123',
        description: 'ABC-123',
        satProductServiceCode: '01010101',
        satUnitCode: 'H87',
        taxObjectCode: '02',
        vatRate: 0.16,
        defaultUnitText: 'PIEZA',
        isActive: true
      }
    });
  });
});
