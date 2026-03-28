import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { of } from 'rxjs';
import { FiscalReceiverFormComponent } from './fiscal-receiver-form.component';
import { FiscalReceiversApiService } from '../infrastructure/fiscal-receivers-api.service';

describe('FiscalReceiverFormComponent', () => {
  const catalog = {
    regimenFiscal: [
      { code: '601', description: 'General de Ley Personas Morales' },
      { code: '605', description: 'Sueldos y Salarios' }
    ],
    usoCfdi: [
      { code: 'G03', description: 'Gastos en general' },
      { code: 'CN01', description: 'Nómina' }
    ],
    byRegimenFiscal: [
      {
        code: '601',
        description: 'General de Ley Personas Morales',
        allowedUsoCfdi: [{ code: 'G03', description: 'Gastos en general' }]
      },
      {
        code: '605',
        description: 'Sueldos y Salarios',
        allowedUsoCfdi: [{ code: 'CN01', description: 'Nómina' }]
      }
    ],
    paymentMethods: [
      { code: 'PUE', description: 'Pago en una sola exhibición' },
      { code: 'PPD', description: 'Pago en parcialidades o diferido' }
    ],
    paymentForms: [
      { code: '03', description: 'Transferencia electrónica de fondos' },
      { code: '99', description: 'Por definir' }
    ]
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FiscalReceiverFormComponent],
      providers: [
        {
          provide: FiscalReceiversApiService,
          useValue: {
            getSatCatalog: vi.fn().mockReturnValue(of(catalog))
          }
        }
      ]
    }).compileComponents();
  });

  it('renders fiscal regime and CFDI use as selects with catalog options', async () => {
    const fixture = TestBed.createComponent(FiscalReceiverFormComponent);
    fixture.componentRef.setInput('initialValue', {
      rfc: 'AAA010101AAA',
      legalName: 'Receiver',
      fiscalRegimeCode: '601',
      cfdiUseCodeDefault: 'G03',
      postalCode: '01000',
      countryCode: 'MX',
      foreignTaxRegistration: null,
      email: null,
      phone: null,
      searchAlias: null,
      isActive: true,
      specialFields: []
    });

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const selects = fixture.debugElement.queryAll(By.css('select'));
    expect(selects.length).toBeGreaterThanOrEqual(2);
    expect(fixture.nativeElement.textContent).toContain('General de Ley Personas Morales');
    expect(fixture.nativeElement.textContent).toContain('Gastos en general');
  });

  it('filters CFDI uses by the selected fiscal regime and clears incompatible current value', async () => {
    const fixture = TestBed.createComponent(FiscalReceiverFormComponent);
    fixture.componentRef.setInput('initialValue', {
      rfc: 'AAA010101AAA',
      legalName: 'Receiver',
      fiscalRegimeCode: '601',
      cfdiUseCodeDefault: 'G03',
      postalCode: '01000',
      countryCode: 'MX',
      foreignTaxRegistration: null,
      email: null,
      phone: null,
      searchAlias: null,
      isActive: true,
      specialFields: []
    });

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const regimeSelect = fixture.debugElement.queryAll(By.css('select'))[0].nativeElement as HTMLSelectElement;
    regimeSelect.value = '605';
    regimeSelect.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as { draft: { cfdiUseCodeDefault: string } };
    expect(component.draft.cfdiUseCodeDefault).toBe('');
    expect(fixture.nativeElement.textContent).toContain('Nómina');
    expect(fixture.nativeElement.textContent).not.toContain('Gastos en general');
  });

  it('keeps legacy stored codes visible without breaking the form', async () => {
    const fixture = TestBed.createComponent(FiscalReceiverFormComponent);
    fixture.componentRef.setInput('receiver', {
      id: 1,
      rfc: 'AAA010101AAA',
      legalName: 'Receiver legacy',
      postalCode: '01000',
      fiscalRegimeCode: '999',
      cfdiUseCodeDefault: 'ZZZ',
      countryCode: 'MX',
      foreignTaxRegistration: null,
      email: null,
      phone: null,
      searchAlias: null,
      isActive: true,
      createdAtUtc: '2026-03-25T12:00:00Z',
      updatedAtUtc: '2026-03-25T12:00:00Z',
      specialFields: []
    });

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('999 - Régimen legacy no encontrado en catálogo');
    expect(fixture.nativeElement.textContent).toContain('ZZZ - Uso CFDI legacy no encontrado o incompatible');
  });
});
