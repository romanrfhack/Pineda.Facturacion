import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
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

  it('accepts .xls files in the UI and exposes both supported extensions in the input', async () => {
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

    const input = fixture.nativeElement.querySelector('input[type="file"]') as HTMLInputElement;
    expect(input.accept).toBe('.xls,.xlsx');

    const file = new File(['sat workbook'], 'catalogos_sat.xls', {
      type: 'application/vnd.ms-excel'
    });

    await fixture.componentInstance['onFileSelected']({
      target: { files: [file] }
    } as unknown as Event);

    expect(fixture.componentInstance['selectedFile']()?.name).toBe('catalogos_sat.xls');
    expect(fixture.componentInstance['localChecksum']()).toBe('sha256:aabbcc');
    expect(fixture.componentInstance['importState']()).toBe('readyToImport');
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

  it('keeps persistent server detail inline for partial imports but uses a summary toast', async () => {
    const importOfficialSatCatalog = vi.fn().mockReturnValue(of({
      outcome: 'PartiallyCompleted',
      isSuccess: false,
      errorMessage: 'Se importó el catálogo, pero hubo incidencias al desactivar claves obsoletas.',
      correlationId: 'corr-456',
      sourceFileName: 'catalogos_sat.xlsx',
      sourceVersion: '4.0',
      sourceChecksum: 'sha256:aabbcc',
      clientChecksumMatchesServer: true,
      productServices: {
        catalogType: 'sat_product_service',
        importId: 12,
        status: 'completed',
        wasAlreadyImported: false,
        totalRows: 2,
        insertedRows: 1,
        updatedRows: 1,
        deactivatedRows: 0,
        errorMessage: null
      },
      units: {
        catalogType: 'sat_clave_unidad',
        importId: 13,
        status: 'failed',
        wasAlreadyImported: false,
        totalRows: 2,
        insertedRows: 0,
        updatedRows: 0,
        deactivatedRows: 0,
        errorMessage: 'No fue posible desactivar una clave obsoleta.'
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

    expect(feedbackService.show).toHaveBeenCalledWith('warning', 'Catálogo SAT importado con incidencias. Revisa el resultado.');
    expect(fixture.nativeElement.textContent).toContain('Se importó el catálogo, pero hubo incidencias al desactivar claves obsoletas.');
    expect(fixture.nativeElement.textContent).toContain('No fue posible desactivar una clave obsoleta.');
  });

  it('does not repeat the same server error inline when it already matches the toast summary', async () => {
    const importOfficialSatCatalog = vi.fn().mockReturnValue(of({
      outcome: 'Failed',
      isSuccess: false,
      errorMessage: 'No fue posible importar el catálogo SAT.',
      correlationId: 'corr-789',
      sourceFileName: 'catalogos_sat.xlsx',
      sourceVersion: '4.0',
      sourceChecksum: 'sha256:aabbcc',
      clientChecksumMatchesServer: true,
      productServices: {
        catalogType: 'sat_product_service',
        importId: 14,
        status: 'failed',
        wasAlreadyImported: false,
        totalRows: 2,
        insertedRows: 0,
        updatedRows: 0,
        deactivatedRows: 0,
        errorMessage: null
      },
      units: {
        catalogType: 'sat_clave_unidad',
        importId: 15,
        status: 'failed',
        wasAlreadyImported: false,
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

    expect(feedbackService.show).toHaveBeenCalledWith('error', 'No fue posible importar el catálogo SAT.');
    expect(fixture.nativeElement.textContent).not.toContain('No fue posible importar el catálogo SAT.');
  });

  it('uses toast feedback for invalid file selection instead of rendering an inline error block', async () => {
    const feedbackService = { show: vi.fn() };

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
          useValue: feedbackService
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(SatCatalogImportPageComponent);
    fixture.detectChanges();

    const file = new File(['plain text'], 'catalogos_sat.csv', {
      type: 'text/csv'
    });

    await fixture.componentInstance['onFileSelected']({
      target: { files: [file] }
    } as unknown as Event);

    fixture.detectChanges();

    expect(fixture.componentInstance['selectedFile']()).toBeNull();
    expect(fixture.componentInstance['importState']()).toBe('error');
    expect(feedbackService.show).toHaveBeenCalledWith('error', 'Selecciona un archivo .xls o .xlsx del catálogo SAT.');
    expect(fixture.nativeElement.textContent).not.toContain('Selecciona un archivo .xls o .xlsx del catálogo SAT.');
  });

  it('shows an error toast when the import request fails', async () => {
    const importOfficialSatCatalog = vi.fn().mockReturnValue(throwError(() => new Error('backend down')));
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

    expect(fixture.componentInstance['importState']()).toBe('error');
    expect(feedbackService.show).toHaveBeenCalledWith('error', 'No fue posible importar el catálogo SAT.');
  });
});
