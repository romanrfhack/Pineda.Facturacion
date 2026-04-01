import { adaptImportLegacyOrderPreview } from './import-legacy-order-preview';

describe('import-legacy-order-preview', () => {
  it('adapts preview response to a UI view model', () => {
    const viewModel = adaptImportLegacyOrderPreview({
      isSuccess: true,
      legacyOrderId: '1173481',
      existingSourceHash: 'abc',
      currentSourceHash: 'xyz',
      hasChanges: true,
      changedOrderFields: ['paymentCondition'],
      changeSummary: {
        addedLines: 1,
        removedLines: 2,
        modifiedLines: 1,
        unchangedLines: 3,
        oldSubtotal: 600,
        newSubtotal: 300,
        oldTotal: 696,
        newTotal: 348
      },
      lineChanges: [],
      reimportEligibility: {
        status: 'BlockedByStampedFiscalDocument',
        reasonCode: 'FiscalDocumentStamped',
        reasonMessage: 'Reimport is blocked because the related fiscal document is already stamped.'
      },
      allowedActions: ['view_existing_sales_order', 'preview_reimport']
    });

    expect(viewModel.legacyOrderId).toBe('1173481');
    expect(viewModel.existingSourceHash).toBe('abc');
    expect(viewModel.currentSourceHash).toBe('xyz');
    expect(viewModel.hasChanges).toBe(true);
    expect(viewModel.addedLines).toBe(1);
    expect(viewModel.oldTotal).toBe(696);
    expect(viewModel.newTotal).toBe(348);
    expect(viewModel.eligibilityStatus).toBe('BlockedByStampedFiscalDocument');
    expect(viewModel.allowedActions).toEqual(['view_existing_sales_order', 'preview_reimport']);
  });
});
