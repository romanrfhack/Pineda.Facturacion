import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { of } from 'rxjs';
import { FiscalReceiverFormComponent } from './fiscal-receiver-form.component';
import { FiscalReceiversApiService } from '../infrastructure/fiscal-receivers-api.service';
import { FiscalReceiver, UpsertFiscalReceiverRequest } from '../models/catalogs.models';

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
    const fixture = await renderComponent({
      initialValue: buildRequest({
        fiscalRegimeCode: '601',
        cfdiUseCodeDefault: 'G03'
      })
    });

    const selects = fixture.debugElement.queryAll(By.css('select'));
    expect(selects.length).toBeGreaterThanOrEqual(2);
    expect(fixture.nativeElement.textContent).toContain('General de Ley Personas Morales');
    expect(fixture.nativeElement.textContent).toContain('Gastos en general');
  });

  it('filters CFDI uses by the selected fiscal regime and clears incompatible current value', async () => {
    const fixture = await renderComponent({
      initialValue: buildRequest({
        fiscalRegimeCode: '601',
        cfdiUseCodeDefault: 'G03'
      })
    });

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
    const fixture = await renderComponent({
      receiver: buildReceiver({
        fiscalRegimeCode: '999',
        cfdiUseCodeDefault: 'ZZZ'
      })
    });

    expect(fixture.nativeElement.textContent).toContain('999 - Régimen legacy no encontrado en catálogo');
    expect(fixture.nativeElement.textContent).toContain('ZZZ - Uso CFDI legacy no encontrado o incompatible');
  });

  it('renders the Correo(s) label and helper guidance for multiple recipients', async () => {
    const fixture = await renderComponent();

    expect(fixture.nativeElement.textContent).toContain('Correo(s)');
    expect(fixture.nativeElement.textContent).toContain('Para varios correos, agrégalos uno por uno o pégalos separados con punto y coma (;).');
  });

  it('starts a new receiver without registered emails', async () => {
    const fixture = await renderComponent();

    expect(fixture.nativeElement.textContent).toContain('Sin correos registrados.');
    expect(getEmailRecipientChipTexts(fixture)).toEqual([]);
    expect(getInvalidEmailRecipientChipTexts(fixture)).toEqual([]);
  });

  it('adds a single valid email', async () => {
    const fixture = await renderComponent();

    await setEmailInputValue(fixture, 'cliente@example.com');
    await clickAddEmailButton(fixture);

    expect(getEmailRecipientChipTexts(fixture)).toEqual(['cliente@example.com']);
    expect(getEmailInput(fixture).value).toBe('');
  });

  it('adds multiple pasted emails separated by semicolon', async () => {
    const fixture = await renderComponent();

    await setEmailInputValue(fixture, 'a@x.com; b@y.com');
    await clickAddEmailButton(fixture);

    expect(getEmailRecipientChipTexts(fixture)).toEqual(['a@x.com', 'b@y.com']);
  });

  it('adds multiple pasted emails separated by comma', async () => {
    const fixture = await renderComponent();

    await setEmailInputValue(fixture, 'a@x.com, b@y.com');
    await clickAddEmailButton(fixture);

    expect(getEmailRecipientChipTexts(fixture)).toEqual(['a@x.com', 'b@y.com']);
  });

  it('adds multiple pasted emails separated by line breaks', async () => {
    const fixture = await renderComponent();

    await setEmailInputValue(fixture, 'a@x.com\nb@y.com');
    await clickAddEmailButton(fixture);

    expect(getEmailRecipientChipTexts(fixture)).toEqual(['a@x.com', 'b@y.com']);
  });

  it('deduplicates emails case-insensitively while preserving capture order', async () => {
    const fixture = await renderComponent();

    await setEmailInputValue(fixture, 'A@x.com');
    await clickAddEmailButton(fixture);
    await setEmailInputValue(fixture, 'a@x.com; B@y.com; b@y.com');
    await clickAddEmailButton(fixture);

    expect(getEmailRecipientChipTexts(fixture)).toEqual(['A@x.com', 'B@y.com']);
  });

  it('rejects invalid emails and does not partially add the batch', async () => {
    const fixture = await renderComponent();

    await setEmailInputValue(fixture, 'a@x.com; invalido');
    await clickAddEmailButton(fixture);

    expect(getEmailRecipientChipTexts(fixture)).toEqual([]);
    expect(fixture.nativeElement.textContent).toContain('Correo inválido: invalido. Corrige el correo antes de agregarlo.');
  });

  it('emits the canonical email string when saving', async () => {
    const fixture = await renderComponent({
      initialValue: buildRequest()
    });
    const emitted = vi.spyOn(fixture.componentInstance.submitted, 'emit');

    await setEmailInputValue(fixture, 'a@x.com, b@y.com');
    await clickAddEmailButton(fixture);
    await submitForm(fixture);

    expect(emitted).toHaveBeenCalledWith(
      expect.objectContaining({
        email: 'a@x.com; b@y.com',
      }),
    );
  });

  it('keeps the current empty email behavior when there are no emails', async () => {
    const fixture = await renderComponent({
      initialValue: buildRequest({ email: null })
    });
    const emitted = vi.spyOn(fixture.componentInstance.submitted, 'emit');

    await submitForm(fixture);

    expect(emitted).toHaveBeenCalledWith(
      expect.objectContaining({
        email: null,
      }),
    );
  });

  it('initializes the list when editing an existing receiver with multiple emails', async () => {
    const fixture = await renderComponent({
      receiver: buildReceiver({
        email: ' a@x.com ,  b@y.com '
      })
    });

    expect(getEmailRecipientChipTexts(fixture)).toEqual(['a@x.com', 'b@y.com']);
    expect(getInvalidEmailRecipientChipTexts(fixture)).toEqual([]);
  });

  it('shows historical invalid emails and blocks saving until they are corrected or removed', async () => {
    const fixture = await renderComponent({
      receiver: buildReceiver({
        email: 'a@x.com; invalido'
      })
    });
    const emitted = vi.spyOn(fixture.componentInstance.submitted, 'emit');

    expect(getEmailRecipientChipTexts(fixture)).toEqual(['a@x.com']);
    expect(getInvalidEmailRecipientChipTexts(fixture)).toEqual(['invalido']);
    expect(fixture.nativeElement.textContent).toContain('Se detectaron correos inválidos en este receptor: invalido');

    await submitForm(fixture);

    expect(emitted).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Correo inválido: invalido. Corrige o quita esos valores antes de guardar.');
  });

  it('updates the emitted email value after removing a current email', async () => {
    const fixture = await renderComponent({
      receiver: buildReceiver({
        email: 'a@x.com; b@y.com'
      })
    });
    const emitted = vi.spyOn(fixture.componentInstance.submitted, 'emit');

    await clickRemoveEmailButton(fixture, 0);
    await submitForm(fixture);

    expect(getEmailRecipientChipTexts(fixture)).toEqual(['b@y.com']);
    expect(emitted).toHaveBeenCalledWith(
      expect.objectContaining({
        email: 'b@y.com',
      }),
    );
  });
});

function buildRequest(overrides: Partial<UpsertFiscalReceiverRequest> = {}): UpsertFiscalReceiverRequest {
  return {
    rfc: 'AAA010101AAA',
    legalName: 'Receiver',
    fiscalRegimeCode: '601',
    cfdiUseCodeDefault: 'G03',
    postalCode: '01000',
    countryCode: 'MX',
    foreignTaxRegistration: null,
    email: '',
    phone: null,
    searchAlias: null,
    isActive: true,
    specialFields: [],
    ...overrides,
  };
}

function buildReceiver(overrides: Partial<FiscalReceiver> = {}): FiscalReceiver {
  return {
    id: 1,
    rfc: 'AAA010101AAA',
    legalName: 'Receiver legacy',
    postalCode: '01000',
    fiscalRegimeCode: '601',
    cfdiUseCodeDefault: 'G03',
    countryCode: 'MX',
    foreignTaxRegistration: null,
    email: null,
    phone: null,
    searchAlias: null,
    isActive: true,
    createdAtUtc: '2026-03-25T12:00:00Z',
    updatedAtUtc: '2026-03-25T12:00:00Z',
    specialFields: [],
    ...overrides,
  };
}

async function renderComponent(options: {
  initialValue?: UpsertFiscalReceiverRequest | null;
  receiver?: FiscalReceiver | null;
} = {}): Promise<ComponentFixture<FiscalReceiverFormComponent>> {
  const fixture = TestBed.createComponent(FiscalReceiverFormComponent);

  if (options.initialValue !== undefined) {
    fixture.componentRef.setInput('initialValue', options.initialValue);
  }

  if (options.receiver !== undefined) {
    fixture.componentRef.setInput('receiver', options.receiver);
  }

  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();

  return fixture;
}

async function setEmailInputValue(
  fixture: ComponentFixture<FiscalReceiverFormComponent>,
  value: string,
): Promise<void> {
  const input = getEmailInput(fixture);
  input.value = value;
  input.dispatchEvent(new Event('input'));
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
}

async function clickAddEmailButton(fixture: ComponentFixture<FiscalReceiverFormComponent>): Promise<void> {
  const button = fixture.nativeElement.querySelector('[data-testid="add-email-recipient-button"]') as HTMLButtonElement;
  button.click();
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
}

async function clickRemoveEmailButton(
  fixture: ComponentFixture<FiscalReceiverFormComponent>,
  index: number,
): Promise<void> {
  const buttons = fixture.nativeElement.querySelectorAll('[data-testid="remove-email-recipient-button"]') as NodeListOf<HTMLButtonElement>;
  buttons[index].click();
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
}

async function submitForm(fixture: ComponentFixture<FiscalReceiverFormComponent>): Promise<void> {
  const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
  form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
}

function getEmailInput(fixture: ComponentFixture<FiscalReceiverFormComponent>): HTMLTextAreaElement {
  return fixture.nativeElement.querySelector('[data-testid="email-recipient-input"]') as HTMLTextAreaElement;
}

function getEmailRecipientChipTexts(fixture: ComponentFixture<FiscalReceiverFormComponent>): string[] {
  return Array.from(fixture.nativeElement.querySelectorAll('[data-testid="email-recipient-chip"]')).map((element) =>
    normalizeTextContent((element as HTMLElement).querySelector('.email-chip-text')?.textContent ?? null),
  );
}

function getInvalidEmailRecipientChipTexts(fixture: ComponentFixture<FiscalReceiverFormComponent>): string[] {
  return Array.from(fixture.nativeElement.querySelectorAll('[data-testid="invalid-email-recipient-chip"]')).map((element) =>
    normalizeTextContent((element as HTMLElement).querySelector('.email-chip-text')?.textContent ?? null),
  );
}

function normalizeTextContent(value: string | null): string {
  return (value ?? '').replace(/\s+/g, ' ').trim();
}
