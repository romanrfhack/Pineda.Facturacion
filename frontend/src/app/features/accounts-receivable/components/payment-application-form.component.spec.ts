import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { PaymentApplicationFormComponent } from './payment-application-form.component';

describe('PaymentApplicationFormComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PaymentApplicationFormComponent]
    }).compileComponents();
  });

  it('uses the current invoice context and does not ask for an editable invoice id', () => {
    const fixture = TestBed.createComponent(PaymentApplicationFormComponent);
    fixture.componentRef.setInput('currentInvoiceId', 2);
    fixture.componentRef.setInput('invoiceLabel', 'A-31809 / UUID-1');
    fixture.componentRef.setInput('outstandingBalance', 1722);
    fixture.componentRef.setInput('paymentAmount', 2000);
    fixture.componentRef.setInput('appliedAmount', 0);
    fixture.componentRef.setInput('remainingAmount', 2000);
    fixture.detectChanges();
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('input[name="invoice-0"]'))).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('#2');
    expect(fixture.nativeElement.textContent).toContain('A-31809 / UUID-1');
    expect(fixture.nativeElement.textContent).toContain('$1,722.00');
  });

  it('caps the applied amount to the current invoice max and emits the current invoice id', () => {
    const fixture = TestBed.createComponent(PaymentApplicationFormComponent);
    fixture.componentRef.setInput('currentInvoiceId', 2);
    fixture.componentRef.setInput('invoiceLabel', 'A-31809 / UUID-1');
    fixture.componentRef.setInput('outstandingBalance', 1722);
    fixture.componentRef.setInput('paymentAmount', 2000);
    fixture.componentRef.setInput('appliedAmount', 0);
    fixture.componentRef.setInput('remainingAmount', 2000);
    fixture.detectChanges();
    fixture.detectChanges();

    const emitSpy = vi.spyOn(fixture.componentInstance.submit, 'emit');
    const amountInput = fixture.debugElement.query(By.css('input[name="appliedAmount"]')).nativeElement as HTMLInputElement;
    const button = fixture.debugElement.query(By.css('button')).nativeElement as HTMLButtonElement;

    amountInput.value = '2000';
    amountInput.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    button.click();
    fixture.detectChanges();

    expect(emitSpy).toHaveBeenCalledWith({
      applications: [
        {
          accountsReceivableInvoiceId: 2,
          appliedAmount: 1722
        }
      ]
    });
    expect(fixture.nativeElement.textContent).toContain('se ajustó a');
    expect(fixture.nativeElement.textContent).toContain('$278.00');
  });

  it('rounds suggested and maximum amounts to 2 decimals without float artifacts', () => {
    const fixture = TestBed.createComponent(PaymentApplicationFormComponent);
    fixture.componentRef.setInput('currentInvoiceId', 2);
    fixture.componentRef.setInput('invoiceLabel', 'A-31809 / UUID-1');
    fixture.componentRef.setInput('outstandingBalance', 1721.999999);
    fixture.componentRef.setInput('paymentAmount', 2000);
    fixture.componentRef.setInput('appliedAmount', 0);
    fixture.componentRef.setInput('remainingAmount', 2000);
    fixture.detectChanges();
    fixture.detectChanges();

    const component = fixture.componentInstance as PaymentApplicationFormComponent & { draftAppliedAmount: number; draftAppliedAmountText: string; maxApplicable: () => number };

    expect(component.draftAppliedAmount).toBe(1722);
    expect(component.draftAppliedAmountText).toBe('1722.00');
    expect(component.maxApplicable()).toBe(1722);
    expect(fixture.nativeElement.textContent).toContain('$1,722.00');
    expect(fixture.nativeElement.textContent).not.toContain('1721.999999');
  });

  it('normalizes the editable amount to 2 decimals on blur and keeps submit numeric', () => {
    const fixture = TestBed.createComponent(PaymentApplicationFormComponent);
    fixture.componentRef.setInput('currentInvoiceId', 2);
    fixture.componentRef.setInput('invoiceLabel', 'A-31809 / UUID-1');
    fixture.componentRef.setInput('outstandingBalance', 5000);
    fixture.componentRef.setInput('paymentAmount', 5000);
    fixture.componentRef.setInput('appliedAmount', 0);
    fixture.componentRef.setInput('remainingAmount', 5000);
    fixture.detectChanges();
    fixture.detectChanges();

    const emitSpy = vi.spyOn(fixture.componentInstance.submit, 'emit');
    const amountInput = fixture.debugElement.query(By.css('input[name="appliedAmount"]')).nativeElement as HTMLInputElement;
    const button = fixture.debugElement.query(By.css('button')).nativeElement as HTMLButtonElement;
    const component = fixture.componentInstance as PaymentApplicationFormComponent & { draftAppliedAmountText: string };

    amountInput.value = '1722.5';
    amountInput.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(component.draftAppliedAmountText).toBe('1722.5');

    amountInput.dispatchEvent(new Event('blur'));
    fixture.detectChanges();
    expect(component.draftAppliedAmountText).toBe('1722.50');

    button.click();
    fixture.detectChanges();

    expect(emitSpy).toHaveBeenCalledWith({
      applications: [
        {
          accountsReceivableInvoiceId: 2,
          appliedAmount: 1722.5
        }
      ]
    });
  });
});
