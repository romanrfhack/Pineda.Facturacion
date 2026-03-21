import { TestBed } from '@angular/core/testing';
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
    (fixture.componentInstance as any).summary.set({
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
    });
    fixture.detectChanges();

    const applyButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find((button) => button.textContent?.includes('Aplicar lote de receptores')) as HTMLButtonElement;

    expect(applyButton.disabled).toBe(true);
  });
});
