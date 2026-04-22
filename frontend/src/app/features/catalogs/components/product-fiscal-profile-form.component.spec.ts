import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ProductFiscalProfileFormComponent } from './product-fiscal-profile-form.component';
import { SatProductServicesApiService } from '../infrastructure/sat-product-services-api.service';

describe('ProductFiscalProfileFormComponent', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  function createSatProductServicesApi(overrides?: Partial<SatProductServicesApiService>) {
    return {
      search: vi.fn().mockReturnValue(of([])),
      searchPaged: vi.fn().mockReturnValue(of({ page: 1, pageSize: 12, hasMore: false, items: [] })),
      searchBestEffort: vi.fn().mockReturnValue(of([])),
      ...overrides,
    };
  }

  it('renders backend validation message and initial field values', async () => {
    await TestBed.configureTestingModule({
      imports: [ProductFiscalProfileFormComponent],
      providers: [
        {
          provide: SatProductServicesApiService,
          useValue: createSatProductServicesApi(),
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ProductFiscalProfileFormComponent);
    fixture.componentRef.setInput('profile', {
      id: 1,
      internalCode: 'SKU-1',
      description: 'Product Uno',
      satProductServiceCode: '01010101',
      satUnitCode: 'H87',
      taxObjectCode: '02',
      vatRate: 0.16,
      defaultUnitText: 'Pieza',
      isActive: true,
      createdAtUtc: '2026-03-20T00:00:00Z',
      updatedAtUtc: '2026-03-20T00:00:00Z',
    });
    fixture.componentRef.setInput('errorMessage', 'SAT data is required.');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('SAT data is required.');
    expect(fixture.nativeElement.textContent).toContain('Código interno');
    expect(fixture.nativeElement.textContent).toContain('Tasa de IVA');
  });

  it('keeps user-entered draft values when only the error message changes', async () => {
    await TestBed.configureTestingModule({
      imports: [ProductFiscalProfileFormComponent],
      providers: [
        {
          provide: SatProductServicesApiService,
          useValue: createSatProductServicesApi(),
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ProductFiscalProfileFormComponent);
    fixture.componentRef.setInput('initialValue', {
      internalCode: 'MTE-4259',
      description: 'Producto faltante',
      satProductServiceCode: '01010101',
      satUnitCode: 'H87',
      taxObjectCode: '02',
      vatRate: 0.16,
      defaultUnitText: 'PIEZA',
      isActive: true,
    });
    fixture.detectChanges();

    const descriptionInput = fixture.nativeElement.querySelector(
      'input[name="description"]',
    ) as HTMLInputElement;
    descriptionInput.value = 'Producto corregido';
    descriptionInput.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    fixture.componentRef.setInput('errorMessage', 'No fue posible guardar.');
    fixture.detectChanges();

    expect(
      (fixture.nativeElement.querySelector('input[name="description"]') as HTMLInputElement).value,
    ).toBe('Producto corregido');
  });

  it('searches SAT product or service entries with debounce and selects one result', async () => {
    vi.useFakeTimers();
    const searchBestEffort = vi.fn().mockReturnValue(
      of([
        {
          code: '40161513',
          description: 'Filtro de aceite',
          displayText: '40161513 — Filtro de aceite',
          matchKind: 'text',
          score: 0.86,
        },
      ]),
    );

    await TestBed.configureTestingModule({
      imports: [ProductFiscalProfileFormComponent],
      providers: [
        {
          provide: SatProductServicesApiService,
          useValue: createSatProductServicesApi({ searchBestEffort }),
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ProductFiscalProfileFormComponent);
    fixture.detectChanges();

    (fixture.componentInstance as any).onSatProductSearchChange('filtro');
    expect(searchBestEffort).not.toHaveBeenCalled();

    vi.advanceTimersByTime(350);
    await Promise.resolve();
    fixture.detectChanges();

    expect(searchBestEffort).toHaveBeenCalledWith('filtro', 12);
    expect(fixture.nativeElement.textContent).toContain('40161513');

    const suggestionButton = fixture.nativeElement.querySelector(
      '.suggestion-button',
    ) as HTMLButtonElement;
    suggestionButton.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Seleccionado:');
    expect(fixture.nativeElement.textContent).toContain('Filtro de aceite');
  });

  it('does not query the SAT catalog until the user captures at least 3 characters', async () => {
    vi.useFakeTimers();
    const searchBestEffort = vi.fn().mockReturnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [ProductFiscalProfileFormComponent],
      providers: [
        {
          provide: SatProductServicesApiService,
          useValue: createSatProductServicesApi({ searchBestEffort }),
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ProductFiscalProfileFormComponent);
    fixture.detectChanges();

    (fixture.componentInstance as any).onSatProductSearchChange('fi');
    vi.advanceTimersByTime(350);
    await Promise.resolve();
    fixture.detectChanges();

    expect(searchBestEffort).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Captura al menos 3 caracteres');
  });

  it('shows deterministic recovery suggestions and lets the user apply one explicitly', async () => {
    await TestBed.configureTestingModule({
      imports: [ProductFiscalProfileFormComponent],
      providers: [
        {
          provide: SatProductServicesApiService,
          useValue: createSatProductServicesApi(),
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ProductFiscalProfileFormComponent);
    fixture.componentRef.setInput('initialValue', {
      internalCode: 'MTE-4259',
      description: 'Producto faltante',
      satProductServiceCode: '',
      satUnitCode: 'H87',
      taxObjectCode: '02',
      vatRate: 0.16,
      defaultUnitText: 'PIEZA',
      isActive: true,
    });
    fixture.componentRef.setInput('recoverySuggestions', [
      {
        satProductServiceCode: '40161513',
        satProductServiceDescription: 'Filtro de aceite',
        satUnitCode: 'H87',
        satUnitDescription: 'Pieza',
        taxObjectCode: '02',
        vatRate: 0.16,
        defaultUnitText: 'PIEZA',
        score: 0.72,
        confidence: 0.68,
        source: 'catalog_search',
        matchKind: 'text',
        reason: 'Coincidencia por descripción o keywords en el catálogo SAT local.',
        isActive: true,
        requiresExplicitConfirmation: true,
      },
    ]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Sugerencias determinísticas');
    expect(fixture.nativeElement.textContent).toContain('requiere confirmación');

    const suggestionButton = fixture.nativeElement.querySelector(
      '.recovery-suggestions .suggestion-button',
    ) as HTMLButtonElement;
    suggestionButton.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Seleccionado:');
    expect(fixture.nativeElement.textContent).toContain('40161513');
  });

  it('does not assign 01010101 silently and allows explicit generic fallback', async () => {
    await TestBed.configureTestingModule({
      imports: [ProductFiscalProfileFormComponent],
      providers: [
        {
          provide: SatProductServicesApiService,
          useValue: createSatProductServicesApi(),
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ProductFiscalProfileFormComponent);
    fixture.componentRef.setInput('initialValue', {
      internalCode: 'MTE-4259',
      description: 'Producto faltante',
      satProductServiceCode: '',
      satUnitCode: 'H87',
      taxObjectCode: '02',
      vatRate: 0.16,
      defaultUnitText: 'PIEZA',
      isActive: true,
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain(
      'Pendiente de seleccionar un producto/servicio SAT.',
    );

    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[];
    const fallbackButton = buttons.find((button) =>
      button.textContent?.includes('Usar 01010101 explícitamente'),
    ) as HTMLButtonElement;
    fallbackButton.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('01010101');
    expect(fixture.nativeElement.textContent).toContain('No existe en el catálogo');
  });

  it('does not submit while satProductServiceCode is empty and shows a clear validation message', async () => {
    await TestBed.configureTestingModule({
      imports: [ProductFiscalProfileFormComponent],
      providers: [
        {
          provide: SatProductServicesApiService,
          useValue: createSatProductServicesApi(),
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ProductFiscalProfileFormComponent);
    fixture.componentRef.setInput('initialValue', {
      internalCode: 'SKU-9',
      description: 'Producto sin clasificar',
      satProductServiceCode: '',
      satUnitCode: 'H87',
      taxObjectCode: '02',
      vatRate: 0.16,
      defaultUnitText: 'PIEZA',
      isActive: true,
    });
    fixture.detectChanges();

    const emitSpy = vi.spyOn((fixture.componentInstance as any).submitted, 'emit');
    const submitButton = fixture.nativeElement.querySelector(
      'button[type="submit"]',
    ) as HTMLButtonElement;

    expect(submitButton.disabled).toBe(true);

    (fixture.componentInstance as any).submitForm();
    fixture.detectChanges();

    expect(emitSpy).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain(
      'Debes seleccionar o capturar un producto/servicio SAT antes de guardar.',
    );
  });
});
