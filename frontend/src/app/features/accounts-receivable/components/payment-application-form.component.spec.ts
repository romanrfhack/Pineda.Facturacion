import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { PaymentApplicationFormComponent } from './payment-application-form.component';

describe('PaymentApplicationFormComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PaymentApplicationFormComponent]
    }).compileComponents();
  });

  it('uses the current invoice context and does not ask for an editable invoice id', async () => {
    const fixture = TestBed.createComponent(PaymentApplicationFormComponent);
    fixture.componentRef.setInput('currentInvoiceId', 2);
    fixture.componentRef.setInput('invoiceLabel', 'A-31809 / UUID-1');
    fixture.componentRef.setInput('outstandingBalance', 1722);
    fixture.componentRef.setInput('paymentAmount', 2000);
    fixture.componentRef.setInput('appliedAmount', 0);
    fixture.componentRef.setInput('remainingAmount', 2000);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('input[name="invoice-0"]'))).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('#2');
    expect(fixture.nativeElement.textContent).toContain('A-31809 / UUID-1');
    expect(fixture.nativeElement.textContent).toContain('$1,722.00');
  });

  it('caps the applied amount to the current invoice max and emits the current invoice id', async () => {
    const fixture = TestBed.createComponent(PaymentApplicationFormComponent);
    fixture.componentRef.setInput('currentInvoiceId', 2);
    fixture.componentRef.setInput('invoiceLabel', 'A-31809 / UUID-1');
    fixture.componentRef.setInput('outstandingBalance', 1722);
    fixture.componentRef.setInput('paymentAmount', 2000);
    fixture.componentRef.setInput('appliedAmount', 0);
    fixture.componentRef.setInput('remainingAmount', 2000);
    fixture.detectChanges();
    await fixture.whenStable();
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
});
