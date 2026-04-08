import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { PermissionService } from '../../../core/auth/permission.service';
import { FiscalImportsApiService } from '../infrastructure/fiscal-imports-api.service';
import { SatCatalogImportPageComponent } from './sat-catalog-import-page.component';

describe('SatCatalogImportPageComponent', () => {
  beforeEach(() => {
    vi.stubGlobal('crypto', {
      subtle: {
        digest: vi.fn().mockResolvedValue(Uint8Array.from([0xaa, 0xbb, 0xcc]).buffer)
      }
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('calculates the checksum locally and renders the import summary without manual metadata fields', async () => {
    await TestBed.configureTestingModule({
      imports: [SatCatalogImportPageComponent],
      providers: [
        {
          provide: FiscalImportsApiService,
          useValue: {
            importOfficialSatCatalog: vi.fn().mockReturnValue(of())
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

    const fixture = TestBed.createComponent(SatCatalogImportPageComponent);
    fixture.detectChanges();

    const file = new File(['sat workbook'], 'catalogos_sat.xlsx', {
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
    });

    await fixture.componentInstance['onFileSelected']({
      target: { files: [file] }
    } as unknown as Event);

    fixture.detectChanges();
    await fixture.whenStable();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('catalogos_sat.xlsx');
    expect(text).toContain('sha256:aabbcc');
    expect(text).toContain('4.0');
    expect(text).not.toContain('sourceVersion');
    expect(text).not.toContain('sourceChecksum');
    expect(text).not.toContain('sourceFileName');
  });

  it('shows the backend result and marks the file as already imported', async () => {
    const importOfficialSatCatalog = vi.fn().mockReturnValue(of({
      outcome: 'AlreadyImported',
      isSuccess: true,
      errorMessage: null,
      correlationId: 'corr-123',
      sourceFileName: 'catalogos_sat.xlsx',
      sourceVersion: '4.0',
      sourceChecksum: 'sha256:aabbcc',
      clientChecksumMatchesServer: true,
      productServices: {
        catalogType: 'sat_product_service',
        importId: 10,
        status: 'alreadyImported',
        wasAlreadyImported: true,
        totalRows: 2,
        insertedRows: 0,
        updatedRows: 0,
        deactivatedRows: 0,
        errorMessage: null
      },
      units: {
        catalogType: 'sat_clave_unidad',
        importId: 11,
        status: 'alreadyImported',
        wasAlreadyImported: true,
        totalRows: 2,
        insertedRows: 0,
        updatedRows: 0,
        deactivatedRows: 0,
        errorMessage: null
      }
    }));
    const feedbackService = { show: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [SatCatalogImportPageComponent],
      providers: [
        {
          provide: FiscalImportsApiService,
          useValue: { importOfficialSatCatalog }
        },
        {
          provide: PermissionService,
          useValue: {
            canWriteMasterData: vi.fn().mockReturnValue(true)
          }
        },
        {
          provide: FeedbackService,
          useValue: feedbackService
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(SatCatalogImportPageComponent);
    const file = new File(['sat workbook'], 'catalogos_sat.xlsx', {
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
    });

    await fixture.componentInstance['onFileSelected']({
      target: { files: [file] }
    } as unknown as Event);
    await fixture.componentInstance['importCatalog']();

    fixture.detectChanges();
    await fixture.whenStable();

    expect(importOfficialSatCatalog).toHaveBeenCalledWith(file, 'sha256:aabbcc');
    expect(fixture.componentInstance['importState']()).toBe('alreadyImported');
    expect(fixture.nativeElement.textContent).toContain('Ya importado');
    expect(fixture.nativeElement.textContent).toContain('corr-123');
    expect(feedbackService.show).toHaveBeenCalledWith('info', 'El archivo SAT ya había sido importado.');
  });
});
