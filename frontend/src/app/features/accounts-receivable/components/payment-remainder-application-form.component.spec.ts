import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { PaymentRemainderApplicationFormComponent } from './payment-remainder-application-form.component';
import { AccountsReceivablePortfolioItemResponse } from '../models/accounts-receivable.models';

describe('PaymentRemainderApplicationFormComponent', () => {
  const eligibleInvoices: AccountsReceivablePortfolioItemResponse[] = [
    {
      accountsReceivableInvoiceId: 10,
      fiscalDocumentId: 1001,
      fiscalReceiverId: 77,
      receiverRfc: 'AAA010101AAA',
      receiverLegalName: 'Receiver',
      fiscalSeries: 'A',
      fiscalFolio: '10',
      fiscalUuid: 'UUID-10',
      total: 1000,
      paidTotal: 0,
      outstandingBalance: 1000,
      issuedAtUtc: '2026-04-01T00:00:00Z',
      dueAtUtc: '2026-04-10T00:00:00Z',
      status: 'Open',
      daysPastDue: 0,
      agingBucket: 'Current',
      hasPendingCommitment: false,
      nextCommitmentDateUtc: null,
      nextFollowUpAtUtc: null,
      followUpPending: false
    },
    {
      accountsReceivableInvoiceId: 11,
      fiscalDocumentId: 1002,
      fiscalReceiverId: 77,
      receiverRfc: 'AAA010101AAA',
      receiverLegalName: 'Receiver',
      fiscalSeries: 'A',
      fiscalFolio: '11',
      fiscalUuid: 'UUID-11',
      total: 2000,
      paidTotal: 0,
      outstandingBalance: 2000,
      issuedAtUtc: '2026-04-02T00:00:00Z',
      dueAtUtc: '2026-04-12T00:00:00Z',
      status: 'Open',
      daysPastDue: 0,
      agingBucket: 'Current',
      hasPendingCommitment: false,
      nextCommitmentDateUtc: null,
      nextFollowUpAtUtc: null,
      followUpPending: false
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PaymentRemainderApplicationFormComponent]
    }).compileComponents();
  });

  it('caps per-row and total proposals to the available remainder', () => {
    const fixture = TestBed.createComponent(PaymentRemainderApplicationFormComponent);
    fixture.componentRef.setInput('eligibleInvoices', eligibleInvoices);
    fixture.componentRef.setInput('paymentAmount', 5000);
    fixture.componentRef.setInput('appliedAmount', 1722);
    fixture.componentRef.setInput('remainingAmount', 3278);
    fixture.detectChanges();

    const inputs = fixture.debugElement.queryAll(By.css('input'));
    const component = fixture.componentInstance as PaymentRemainderApplicationFormComponent & {
      proposals: { (): Record<number, number> };
      totalProposed: () => number;
      remainingAfterProposal: () => number;
    };

    const first = inputs[0].nativeElement as HTMLInputElement;
    const second = inputs[1].nativeElement as HTMLInputElement;

    first.value = '1000';
    first.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    second.value = '3000';
    second.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(component.proposals()[10]).toBe(1000);
    expect(component.proposals()[11]).toBe(2000);
    expect(component.totalProposed()).toBe(3000);
    expect(component.remainingAfterProposal()).toBe(278);
  });

  it('emits only positive proposal rows', () => {
    const fixture = TestBed.createComponent(PaymentRemainderApplicationFormComponent);
    fixture.componentRef.setInput('eligibleInvoices', eligibleInvoices);
    fixture.componentRef.setInput('paymentAmount', 5000);
    fixture.componentRef.setInput('appliedAmount', 1722);
    fixture.componentRef.setInput('remainingAmount', 3278);
    fixture.detectChanges();

    const emitSpy = vi.spyOn(fixture.componentInstance.submit, 'emit');
    const inputs = fixture.debugElement.queryAll(By.css('input'));
    const button = fixture.debugElement.query(By.css('button')).nativeElement as HTMLButtonElement;

    const first = inputs[0].nativeElement as HTMLInputElement;
    const second = inputs[1].nativeElement as HTMLInputElement;
    first.value = '1000';
    first.dispatchEvent(new Event('input'));
    second.value = '2000';
    second.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    button.click();

    expect(emitSpy).toHaveBeenCalledWith({
      applications: [
        { accountsReceivableInvoiceId: 10, appliedAmount: 1000 },
        { accountsReceivableInvoiceId: 11, appliedAmount: 2000 }
      ]
    });
  });
});
