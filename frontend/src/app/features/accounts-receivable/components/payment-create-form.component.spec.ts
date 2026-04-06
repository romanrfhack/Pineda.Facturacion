import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { of } from 'rxjs';
import { formatDateTimeLocalValue, PaymentCreateFormComponent } from './payment-create-form.component';
import { FiscalReceiverSatCatalogService } from '../../catalogs/application/fiscal-receiver-sat-catalog.service';

describe('PaymentCreateFormComponent', () => {
  const catalog = {
    regimenFiscal: [],
    usoCfdi: [],
    byRegimenFiscal: [],
    paymentMethods: [
      { code: 'PUE', description: 'Pago en una sola exhibicion' },
      { code: 'PPD', description: 'Pago en parcialidades o diferido' }
    ],
    paymentForms: [
      { code: '03', description: 'Transferencia electronica de fondos' },
      { code: '28', description: 'Tarjeta de debito' },
      { code: '99', description: 'Por definir' }
    ]
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PaymentCreateFormComponent],
      providers: [
        {
          provide: FiscalReceiverSatCatalogService,
          useValue: {
            getCatalog: vi.fn().mockReturnValue(of(catalog))
          }
        }
      ]
    }).compileComponents();
  });

  it('renders payment form as select with SAT catalog options excluding 99', async () => {
    const fixture = TestBed.createComponent(PaymentCreateFormComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('input[name="paymentFormSat"]'))).toBeNull();

    const select = fixture.debugElement.query(By.css('select[name="paymentFormSat"]')).nativeElement as HTMLSelectElement;
    const optionValues = Array.from(select.options).map((option) => option.value);

    expect(optionValues).toContain('03');
    expect(optionValues).toContain('28');
    expect(optionValues).not.toContain('99');
    expect(select.classList.contains('field-control')).toBe(true);
    expect(select.classList.contains('payment-form-select')).toBe(true);
  });

  it('submits the selected SAT payment form code', async () => {
    const fixture = TestBed.createComponent(PaymentCreateFormComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const emitSpy = vi.spyOn(fixture.componentInstance.submit, 'emit');
    const amountInput = fixture.debugElement.query(By.css('input[name="amount"]')).nativeElement as HTMLInputElement;
    const paymentFormSelect = fixture.debugElement.query(By.css('select[name="paymentFormSat"]')).nativeElement as HTMLSelectElement;
    const form = fixture.debugElement.query(By.css('form')).nativeElement as HTMLFormElement;

    paymentFormSelect.value = '28';
    paymentFormSelect.dispatchEvent(new Event('change'));
    amountInput.value = '125.50';
    amountInput.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(emitSpy).toHaveBeenCalledWith(expect.objectContaining({
      paymentFormSat: '28',
      amount: 125.5
    }));
  });

  it('allows an exact payment equal to the operational outstanding balance', async () => {
    const fixture = TestBed.createComponent(PaymentCreateFormComponent);
    fixture.componentRef.setInput('maxOperationalAmount', 1190);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const emitSpy = vi.spyOn(fixture.componentInstance.submit, 'emit');
    const amountInput = fixture.debugElement.query(By.css('input[name="amount"]')).nativeElement as HTMLInputElement;
    const form = fixture.debugElement.query(By.css('form')).nativeElement as HTMLFormElement;

    amountInput.value = '1190.00';
    amountInput.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(emitSpy).toHaveBeenCalledWith(expect.objectContaining({ amount: 1190 }));
    expect(fixture.nativeElement.textContent).not.toContain('excede el saldo pendiente');
  });

  it('blocks submission when the payment amount exceeds the operational outstanding balance', async () => {
    const fixture = TestBed.createComponent(PaymentCreateFormComponent);
    fixture.componentRef.setInput('maxOperationalAmount', 1190);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const emitSpy = vi.spyOn(fixture.componentInstance.submit, 'emit');
    const amountInput = fixture.debugElement.query(By.css('input[name="amount"]')).nativeElement as HTMLInputElement;
    const submitButton = fixture.debugElement.query(By.css('button[type="submit"]')).nativeElement as HTMLButtonElement;
    const form = fixture.debugElement.query(By.css('form')).nativeElement as HTMLFormElement;

    amountInput.value = '1190.04';
    amountInput.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(emitSpy).not.toHaveBeenCalled();
    expect(submitButton.disabled).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('El monto capturado excede el saldo pendiente por 0.04');
  });

  it('formats datetime-local defaults using local time instead of raw UTC text', () => {
    const utcDate = new Date('2026-04-04T18:45:00.000Z');
    const offsetSpy = vi.spyOn(utcDate, 'getTimezoneOffset').mockReturnValue(360);

    expect(formatDateTimeLocalValue(utcDate)).toBe('2026-04-04T12:45');

    offsetSpy.mockRestore();
  });
});
