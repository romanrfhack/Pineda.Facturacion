import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ProductImportsPageComponent } from './product-imports-page.component';
import { FiscalImportsApiService } from '../infrastructure/fiscal-imports-api.service';
import { PermissionService } from '../../../core/auth/permission.service';
import { FeedbackService } from '../../../core/ui/feedback.service';

describe('ProductImportsPageComponent', () => {
  beforeEach(() => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('applies all eligible product rows without manual row numbers', async () => {
    const applyProductBatch = vi.fn().mockReturnValue(of({
      batchId: 20,
      applyMode: 'CreateOnly',
      totalCandidateRows: 2,
      appliedRows: 2,
      skippedRows: 0,
      failedRows: 0,
      alreadyAppliedRows: 0,
      lastAppliedAtUtc: null,
      errorMessage: null,
      rows: []
    }));

    await TestBed.configureTestingModule({
      imports: [ProductImportsPageComponent],
      providers: [
        {
          provide: FiscalImportsApiService,
          useValue: {
            previewProducts: vi.fn(),
            getProductBatch: vi.fn(),
            listProductRows: vi.fn(),
            applyProductBatch
          }
        },
        {
          provide: PermissionService,
          useValue: {
            canWriteMasterData: vi.fn().mockReturnValue(true)
          }
        },
        {
          provide: FeedbackService,
          useValue: { show: vi.fn() }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(ProductImportsPageComponent);
    fixture.componentInstance['summary'].set({
      batchId: 20,
      sourceFileName: 'products.xlsx',
      status: 'Validated',
      totalRows: 5,
      validRows: 3,
      invalidRows: 1,
      ignoredRows: 1,
      existingMasterMatches: 0,
      duplicateRowsInFile: 0,
      appliedRows: 0,
      applyFailedRows: 0,
      applySkippedRows: 0,
      completedAtUtc: null,
      lastAppliedAtUtc: null,
      errorMessage: null
    });
    fixture.componentInstance['rows'].set([
      {
        rowNumber: 1,
        status: 'Valid',
        suggestedAction: 'Create',
        normalizedInternalCode: 'SKU-1',
        normalizedDescription: 'Producto 1',
        normalizedTaxObjectCode: '02',
        normalizedVatRate: 0.16,
        validationErrors: [],
        existingMasterEntityId: null,
        applyStatus: 'Pending'
      },
      {
        rowNumber: 2,
        status: 'Valid',
        suggestedAction: 'Update',
        normalizedInternalCode: 'SKU-2',
        normalizedDescription: 'Producto 2',
        normalizedTaxObjectCode: '02',
        normalizedVatRate: 0.16,
        validationErrors: [],
        existingMasterEntityId: 40,
        applyStatus: 'Pending'
      }
    ]);
    fixture.componentInstance['eligibleRowsCount'].set(2);
    fixture.detectChanges();
    await fixture.whenStable();

    const applyButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find((button) => button.textContent?.includes('Aplicar lote de productos')) as HTMLButtonElement;

    applyButton.click();
    await fixture.whenStable();

    expect(applyProductBatch).toHaveBeenCalledWith(20, {
      applyMode: 'CreateOnly',
      selectedRowNumbers: undefined,
      stopOnFirstError: false
    });
  });
});
