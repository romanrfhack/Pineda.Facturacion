import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ReceiverImportsPageComponent } from './receiver-imports-page.component';
import { FiscalImportsApiService } from '../infrastructure/fiscal-imports-api.service';
import { PermissionService } from '../../../core/auth/permission.service';
import { FeedbackService } from '../../../core/ui/feedback.service';

describe('ReceiverImportsPageComponent', () => {
  beforeEach(() => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  function createSummary() {
    return {
      batchId: 10,
      sourceFileName: 'receivers.xlsx',
      status: 'Validated',
      totalRows: 10,
      validRows: 8,
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
    };
  }

  function createRows() {
    return [
      {
        rowNumber: 1,
        status: 'Valid',
        suggestedAction: 'Create',
        normalizedRfc: 'AAA010101AAA',
        normalizedLegalName: 'Cliente A',
        validationErrors: [],
        existingMasterEntityId: null,
        applyStatus: 'Pending'
      },
      {
        rowNumber: 2,
        status: 'Valid',
        suggestedAction: 'Update',
        normalizedRfc: 'BBB010101BBB',
        normalizedLegalName: 'Cliente B',
        validationErrors: [],
        existingMasterEntityId: 22,
        applyStatus: 'Pending'
      },
      {
        rowNumber: 3,
        status: 'Invalid',
        suggestedAction: 'Conflict',
        normalizedRfc: 'CCC010101CCC',
        normalizedLegalName: 'Cliente C',
        validationErrors: ['RFC inválido'],
        existingMasterEntityId: null,
        applyStatus: 'Pending'
      }
    ];
  }

  async function configure(overrides?: Partial<Record<keyof FiscalImportsApiService, unknown>>) {
    const applyReceiverBatch = vi.fn().mockReturnValue(of({
      batchId: 10,
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
      imports: [ReceiverImportsPageComponent],
      providers: [
        {
          provide: FiscalImportsApiService,
          useValue: {
            previewReceivers: vi.fn(),
            getReceiverBatch: vi.fn().mockReturnValue(of(createSummary())),
            listReceiverRows: vi.fn().mockReturnValue(of(createRows())),
            applyReceiverBatch,
            ...overrides
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

    const fixture = TestBed.createComponent(ReceiverImportsPageComponent);
    fixture.componentInstance['summary'].set(createSummary());
    fixture.componentInstance['rows'].set(createRows());
    fixture.componentInstance['eligibleRowsCount'].set(2);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, applyReceiverBatch };
  }

  it('disables apply when the user cannot manage catalog imports', async () => {
    await TestBed.configureTestingModule({
      imports: [ReceiverImportsPageComponent],
      providers: [
        {
          provide: FiscalImportsApiService,
          useValue: {
            previewReceivers: vi.fn(),
            getReceiverBatch: vi.fn(),
            listReceiverRows: vi.fn(),
            applyReceiverBatch: vi.fn()
          }
        },
        {
          provide: PermissionService,
          useValue: {
            canWriteMasterData: vi.fn().mockReturnValue(false)
          }
        },
        {
          provide: FeedbackService,
          useValue: { show: vi.fn() }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(ReceiverImportsPageComponent);
    (fixture.componentInstance as any).summary.set(createSummary());
    fixture.detectChanges();

    const applyButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find((button) => button.textContent?.includes('Aplicar lote de receptores')) as HTMLButtonElement;

    expect(applyButton.disabled).toBe(true);
  });

  it('applies all eligible rows without requiring manual row numbers', async () => {
    const { fixture, applyReceiverBatch } = await configure();

    const applyButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find((button) => button.textContent?.includes('Aplicar lote de receptores')) as HTMLButtonElement;

    applyButton.click();
    await fixture.whenStable();

    expect(applyReceiverBatch).toHaveBeenCalledWith(10, {
      applyMode: 'CreateOnly',
      selectedRowNumbers: undefined,
      stopOnFirstError: false
    });
    expect(fixture.nativeElement.textContent).toContain('Actualmente hay 2');
  });

  it('applies specific rows after parsing comma-separated input', async () => {
    const { fixture, applyReceiverBatch } = await configure();

    fixture.componentInstance['applySelectionMode'] = 'specificRows';
    fixture.componentInstance['selectedRowsText'] = '7, 2, 7, 1';
    fixture.detectChanges();

    const applyButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find((button) => button.textContent?.includes('Aplicar lote de receptores')) as HTMLButtonElement;

    applyButton.click();
    await fixture.whenStable();

    expect(applyReceiverBatch).toHaveBeenCalledWith(10, {
      applyMode: 'CreateOnly',
      selectedRowNumbers: [1, 2, 7],
      stopOnFirstError: false
    });
  });

  it('shows a validation message when specific row input is invalid', async () => {
    const { fixture, applyReceiverBatch } = await configure();

    fixture.componentInstance['applySelectionMode'] = 'specificRows';
    fixture.componentInstance['selectedRowsText'] = 'abc, 0';
    fixture.detectChanges();

    const applyButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find((button) => button.textContent?.includes('Aplicar lote de receptores')) as HTMLButtonElement;

    applyButton.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(applyReceiverBatch).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Ingresa números de fila válidos separados por comas.');
  });

  it('renders backend validation detail when apply fails', async () => {
    const { fixture } = await configure({
      applyReceiverBatch: vi.fn().mockReturnValue(throwError(() => ({
        error: {
          title: 'One or more validation errors occurred.',
          errors: {
            applyMode: ['El modo de aplicación es obligatorio.']
          }
        }
      })))
    });

    const applyButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find((button) => button.textContent?.includes('Aplicar lote de receptores')) as HTMLButtonElement;

    applyButton.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('El modo de aplicación es obligatorio.');
  });
});
