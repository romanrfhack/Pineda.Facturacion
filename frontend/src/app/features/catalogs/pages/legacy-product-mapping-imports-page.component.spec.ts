import { TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';
import { PermissionService } from '../../../core/auth/permission.service';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { FiscalImportsApiService } from '../infrastructure/fiscal-imports-api.service';
import { LegacyProductMappingImportResponse } from '../models/catalogs.models';
import { LegacyProductMappingImportsPageComponent } from './legacy-product-mapping-imports-page.component';

describe('LegacyProductMappingImportsPageComponent', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  async function configure(options?: {
    canWrite?: boolean;
    importLegacyProductMappingsCsv?: ReturnType<typeof vi.fn>;
    listLegacyProductMappingBatches?: ReturnType<typeof vi.fn>;
  }) {
    const api = {
      importLegacyProductMappingsCsv: options?.importLegacyProductMappingsCsv ?? vi.fn().mockReturnValue(of(createImportResponse())),
      listLegacyProductMappingBatches: options?.listLegacyProductMappingBatches ?? vi.fn().mockReturnValue(of([]))
    };
    const feedbackService = { show: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [LegacyProductMappingImportsPageComponent],
      providers: [
        {
          provide: FiscalImportsApiService,
          useValue: api
        },
        {
          provide: PermissionService,
          useValue: {
            canWriteMasterData: vi.fn().mockReturnValue(options?.canWrite ?? true)
          }
        },
        {
          provide: FeedbackService,
          useValue: feedbackService
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(LegacyProductMappingImportsPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    return { fixture, api, feedbackService };
  }

  it('renders the screen description, expected columns and operational notes', async () => {
    const { fixture } = await configure();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Importar mappings SAT de productos');
    expect(text).toContain('Carga archivos CSV provenientes del sistema anterior');
    expect(text).toContain('Clave Producto/Servicio');
    expect(text).toContain('Código SKU');
    expect(text).toContain('Objeto de impuesto, IVA y texto de unidad');
    expect(text).toContain('01010101 no se asigna automáticamente');
  });

  it('requires a selected CSV file before sending', async () => {
    const { fixture, api } = await configure();

    await fixture.componentInstance['importFile']();
    fixture.detectChanges();

    expect(api.importLegacyProductMappingsCsv).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Selecciona un archivo CSV para continuar.');
  });

  it('rejects non CSV files', async () => {
    const { fixture, api } = await configure();
    const file = new File(['excel'], 'mappings.xlsx');

    fixture.componentInstance['onFileSelected']({
      target: { files: [file] }
    } as unknown as Event);
    fixture.detectChanges();

    expect(fixture.componentInstance['selectedFile']()).toBeNull();
    expect(api.importLegacyProductMappingsCsv).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('El archivo debe tener extensión .csv.');
  });

  it('sends the selected file and source name through the imports API', async () => {
    const importLegacyProductMappingsCsv = vi.fn().mockReturnValue(of(createImportResponse()));
    const { fixture } = await configure({ importLegacyProductMappingsCsv });
    const file = new File(['csv'], 'Listado_Conceptos.csv', { type: 'text/csv' });

    fixture.componentInstance['sourceName'] = 'Sistema anterior';
    fixture.componentInstance['onFileSelected']({
      target: { files: [file] }
    } as unknown as Event);

    await fixture.componentInstance['importFile']();

    expect(importLegacyProductMappingsCsv).toHaveBeenCalledWith(file, 'Sistema anterior');
  });

  it('blocks double submit while the import is running', async () => {
    const importResult = new Subject<LegacyProductMappingImportResponse>();
    const importLegacyProductMappingsCsv = vi.fn().mockReturnValue(importResult.asObservable());
    const { fixture } = await configure({ importLegacyProductMappingsCsv });
    const file = new File(['csv'], 'mappings.csv', { type: 'text/csv' });

    fixture.componentInstance['onFileSelected']({
      target: { files: [file] }
    } as unknown as Event);

    const firstSubmit = fixture.componentInstance['importFile']();
    const secondSubmit = fixture.componentInstance['importFile']();

    expect(importLegacyProductMappingsCsv).toHaveBeenCalledTimes(1);

    importResult.next(createImportResponse());
    importResult.complete();

    await firstSubmit;
    await secondSubmit;
  });

  it('shows the import summary after a successful import', async () => {
    const importLegacyProductMappingsCsv = vi.fn().mockReturnValue(of(createImportResponse({
      batchId: 55,
      fileName: 'Listado_Conceptos_2026_04_29.csv',
      totalRows: 12,
      validRows: 10,
      invalidRows: 1,
      ambiguousRows: 1,
      skippedRows: 0
    })));
    const { fixture, feedbackService } = await configure({ importLegacyProductMappingsCsv });
    const file = new File(['csv'], 'Listado_Conceptos_2026_04_29.csv', { type: 'text/csv' });

    fixture.componentInstance['onFileSelected']({
      target: { files: [file] }
    } as unknown as Event);
    await fixture.componentInstance['importFile']();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Resultado de importación');
    expect(text).toContain('Listado_Conceptos_2026_04_29.csv');
    expect(text).toContain('55');
    expect(text).toContain('12');
    expect(text).toContain('10');
    expect(feedbackService.show).toHaveBeenCalledWith('warning', 'La importación terminó con advertencias. Revisa el resumen.');
  });

  it('shows a clear import error when the backend request fails', async () => {
    const importLegacyProductMappingsCsv = vi.fn().mockReturnValue(throwError(() => new Error('network down')));
    const { fixture } = await configure({ importLegacyProductMappingsCsv });
    const file = new File(['csv'], 'mappings.csv', { type: 'text/csv' });

    fixture.componentInstance['onFileSelected']({
      target: { files: [file] }
    } as unknown as Event);
    await fixture.componentInstance['importFile']();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No se pudo importar el archivo. Verifica el formato del CSV.');
  });

  it('renders history and refreshes it explicitly', async () => {
    const listLegacyProductMappingBatches = vi.fn()
      .mockReturnValueOnce(of([]))
      .mockReturnValueOnce(of([
        {
          id: 77,
          fileName: 'legacy.csv',
          sourceName: 'Sistema anterior',
          importedAtUtc: '2026-04-29T12:30:00Z',
          importedByUser: 'supervisor.test',
          totalRows: 3,
          validRows: 2,
          invalidRows: 1,
          ambiguousRows: 0,
          skippedRows: 0,
          status: 'Validated',
          errorMessage: null
        }
      ]));
    const { fixture } = await configure({ listLegacyProductMappingBatches });

    await fixture.componentInstance['loadHistory']();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(listLegacyProductMappingBatches).toHaveBeenCalledTimes(2);
    expect(text).toContain('legacy.csv');
    expect(text).toContain('supervisor.test');
    expect(text).toContain('Validado');
  });

  it('blocks the screen for users without Supervisor or Admin permission', async () => {
    const listLegacyProductMappingBatches = vi.fn().mockReturnValue(of([]));
    const { fixture } = await configure({ canWrite: false, listLegacyProductMappingBatches });

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('No tienes permiso para importar mappings SAT.');
    expect(text).not.toContain('Importar archivo');
    expect(listLegacyProductMappingBatches).not.toHaveBeenCalled();
  });
});

function createImportResponse(overrides: Partial<LegacyProductMappingImportResponse> = {}): LegacyProductMappingImportResponse {
  return {
    outcome: 'Completed',
    isSuccess: true,
    wasAlreadyImported: false,
    errorMessage: null,
    batchId: 44,
    fileName: 'mappings.csv',
    sourceName: 'Sistema anterior',
    sourceChecksum: 'sha256:test',
    importedAtUtc: '2026-04-29T12:00:00Z',
    importedByUserId: 1,
    totalRows: 1,
    validRows: 1,
    invalidRows: 0,
    ambiguousRows: 0,
    skippedRows: 0,
    status: 'Validated',
    ...overrides
  };
}
