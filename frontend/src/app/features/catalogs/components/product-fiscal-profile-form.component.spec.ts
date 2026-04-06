import { TestBed } from '@angular/core/testing';
import { ProductFiscalProfileFormComponent } from './product-fiscal-profile-form.component';
import { of } from 'rxjs';
import { SatProductServicesApiService } from '../infrastructure/sat-product-services-api.service';

describe('ProductFiscalProfileFormComponent', () => {
  it('renders backend validation message and initial field values', async () => {
    await TestBed.configureTestingModule({
      imports: [ProductFiscalProfileFormComponent],
      providers: [
        {
          provide: SatProductServicesApiService,
          useValue: { search: vi.fn().mockReturnValue(of([])) }
        }
      ]
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
      updatedAtUtc: '2026-03-20T00:00:00Z'
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
          useValue: { search: vi.fn().mockReturnValue(of([])) }
        }
      ]
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
      isActive: true
    });
    fixture.detectChanges();

    const descriptionInput = fixture.nativeElement.querySelector('input[name="description"]') as HTMLInputElement;
    descriptionInput.value = 'Producto corregido';
    descriptionInput.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    fixture.componentRef.setInput('errorMessage', 'No fue posible guardar.');
    fixture.detectChanges();

    expect((fixture.nativeElement.querySelector('input[name="description"]') as HTMLInputElement).value).toBe('Producto corregido');
  });

  it('searches and selects a SAT product or service entry', async () => {
    const search = vi.fn().mockReturnValue(of([
      {
        code: '40161513',
        description: 'Filtro de aceite',
        displayText: '40161513 — Filtro de aceite',
        matchKind: 'text'
      }
    ]));

    await TestBed.configureTestingModule({
      imports: [ProductFiscalProfileFormComponent],
      providers: [
        {
          provide: SatProductServicesApiService,
          useValue: { search }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(ProductFiscalProfileFormComponent);
    fixture.detectChanges();

    await (fixture.componentInstance as any).onSatProductSearchChange('filtro');
    fixture.detectChanges();

    expect(search).toHaveBeenCalledWith('filtro', 8);
    expect(fixture.nativeElement.textContent).toContain('40161513');

    const suggestionButton = fixture.nativeElement.querySelector('.suggestion-button') as HTMLButtonElement;
    suggestionButton.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Seleccionado:');
    expect(fixture.nativeElement.textContent).toContain('Filtro de aceite');
  });

  it('does not assign 01010101 silently and allows explicit generic fallback', async () => {
    await TestBed.configureTestingModule({
      imports: [ProductFiscalProfileFormComponent],
      providers: [
        {
          provide: SatProductServicesApiService,
          useValue: { search: vi.fn().mockReturnValue(of([])) }
        }
      ]
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
      isActive: true
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Pendiente de seleccionar un producto/servicio SAT.');

    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[];
    const fallbackButton = buttons
      .find(button => button.textContent?.includes('Usar 01010101 explícitamente')) as HTMLButtonElement;
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
          useValue: { search: vi.fn().mockReturnValue(of([])) }
        }
      ]
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
      isActive: true
    });
    fixture.detectChanges();

    const emitSpy = vi.spyOn((fixture.componentInstance as any).submitted, 'emit');
    const submitButton = fixture.nativeElement.querySelector('button[type="submit"]') as HTMLButtonElement;

    expect(submitButton.disabled).toBe(true);

    (fixture.componentInstance as any).submitForm();
    fixture.detectChanges();

    expect(emitSpy).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Debes seleccionar o capturar un producto/servicio SAT antes de guardar.');
  });
});
