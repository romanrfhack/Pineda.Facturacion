import { TestBed } from '@angular/core/testing';
import { PaymentApplicationReassignModalComponent } from './payment-application-reassign-modal.component';
import {
  AccountsReceivablePaymentResponse,
  AccountsReceivablePortfolioItemResponse,
} from '../models/accounts-receivable.models';

describe('PaymentApplicationReassignModalComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PaymentApplicationReassignModalComponent],
    }).compileComponents();
  });

  it('preloads current applications and calculates assigned and remaining totals', () => {
    const fixture = TestBed.createComponent(PaymentApplicationReassignModalComponent);
    fixture.componentRef.setInput('payment', createPayment());
    fixture.componentRef.setInput('fiscalReceiverId', 77);
    fixture.componentRef.setInput('candidateInvoices', [
      createInvoice({ accountsReceivableInvoiceId: 10, outstandingBalance: 0, status: 'Paid' }),
      createInvoice({ accountsReceivableInvoiceId: 11, fiscalFolio: '11', outstandingBalance: 300 }),
    ]);
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      rows: () => Array<{ accountsReceivableInvoiceId: number | null; amount: number }>;
      totalAssigned: () => number;
      remainingAfterReassign: () => number;
    };

    expect(component.rows()).toEqual([
      expect.objectContaining({ accountsReceivableInvoiceId: 10, amount: 700 }),
    ]);
    expect(component.totalAssigned()).toBe(700);
    expect(component.remainingAfterReassign()).toBe(300);
    expect(fixture.nativeElement.textContent).toContain('A-10');
    expect(fixture.nativeElement.textContent).toContain('Distribución actual');
  });

  it('validates reason length, positive amounts and max balance after reversing current applications', () => {
    const fixture = TestBed.createComponent(PaymentApplicationReassignModalComponent);
    fixture.componentRef.setInput('payment', createPayment());
    fixture.componentRef.setInput('fiscalReceiverId', 77);
    fixture.componentRef.setInput('candidateInvoices', [
      createInvoice({ accountsReceivableInvoiceId: 10, outstandingBalance: 0, status: 'Paid' }),
    ]);
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      updateReason: (value: string) => void;
      updateRowAmount: (rowId: number, value: string) => void;
      validationMessages: () => string[];
      canSubmit: () => boolean;
      rows: () => Array<{ rowId: number }>;
    };

    expect(component.validationMessages()).toContain(
      'Captura un motivo de al menos 10 caracteres.',
    );

    component.updateReason('Corrección validada');
    component.updateRowAmount(component.rows()[0].rowId, '701');
    fixture.detectChanges();

    expect(component.canSubmit()).toBe(false);
    expect(component.validationMessages()).toContain(
      'El importe por factura no puede exceder el saldo disponible después de revertir la aplicación actual.',
    );

    component.updateRowAmount(component.rows()[0].rowId, '0');
    fixture.detectChanges();

    expect(component.validationMessages()).toContain('Cada importe debe ser mayor a cero.');
  });

  it('emits the reassign request when the draft distribution is valid', () => {
    const fixture = TestBed.createComponent(PaymentApplicationReassignModalComponent);
    fixture.componentRef.setInput('payment', createPayment());
    fixture.componentRef.setInput('fiscalReceiverId', 77);
    fixture.componentRef.setInput('candidateInvoices', [
      createInvoice({ accountsReceivableInvoiceId: 10, outstandingBalance: 0, status: 'Paid' }),
      createInvoice({ accountsReceivableInvoiceId: 11, fiscalFolio: '11', outstandingBalance: 300 }),
    ]);
    fixture.detectChanges();

    const emitSpy = vi.spyOn(fixture.componentInstance.submitted, 'emit');
    const component = fixture.componentInstance as unknown as {
      updateReason: (value: string) => void;
      addRow: () => void;
      updateRowAmount: (rowId: number, value: string) => void;
      confirmSubmit: () => void;
      rows: () => Array<{ rowId: number; accountsReceivableInvoiceId: number | null }>;
      totalAssigned: () => number;
      remainingAfterReassign: () => number;
    };

    component.updateReason('Corrección validada por cobranza');
    component.addRow();
    const newRow = component.rows().find((row) => row.accountsReceivableInvoiceId === 11);
    expect(newRow).toBeDefined();
    if (!newRow) {
      throw new Error('Expected invoice 11 row to be created');
    }

    component.updateRowAmount(newRow.rowId, '300');
    fixture.detectChanges();

    expect(component.totalAssigned()).toBe(1000);
    expect(component.remainingAfterReassign()).toBe(0);

    component.confirmSubmit();

    expect(emitSpy).toHaveBeenCalledWith({
      reason: 'Corrección validada por cobranza',
      applications: [
        { accountsReceivableInvoiceId: 10, appliedAmount: 700 },
        { accountsReceivableInvoiceId: 11, appliedAmount: 300 },
      ],
    });
  });
});

function createPayment(
  overrides: Partial<AccountsReceivablePaymentResponse> = {},
): AccountsReceivablePaymentResponse {
  return {
    id: overrides.id ?? 37,
    paymentDateUtc: overrides.paymentDateUtc ?? '2026-04-03T00:00:00Z',
    paymentFormSat: overrides.paymentFormSat ?? '03',
    currencyCode: overrides.currencyCode ?? 'MXN',
    amount: overrides.amount ?? 1000,
    appliedTotal: overrides.appliedTotal ?? 700,
    remainingAmount: overrides.remainingAmount ?? 300,
    customerCreditBalanceAmount: overrides.customerCreditBalanceAmount ?? 0,
    reference: overrides.reference ?? 'DEP-37',
    notes: overrides.notes ?? null,
    receivedFromFiscalReceiverId: overrides.receivedFromFiscalReceiverId ?? 77,
    operationalStatus: overrides.operationalStatus ?? 'PartiallyApplied',
    repStatus: overrides.repStatus ?? 'PendingApplications',
    readyToPrepareRep: overrides.readyToPrepareRep ?? false,
    repBlockReason: overrides.repBlockReason ?? null,
    unappliedDisposition: overrides.unappliedDisposition ?? 'PendingAllocation',
    repDocumentStatus: overrides.repDocumentStatus ?? null,
    repReservedAmount: overrides.repReservedAmount ?? 0,
    repFiscalizedAmount: overrides.repFiscalizedAmount ?? 0,
    applicationsCount: overrides.applicationsCount ?? 1,
    linkedFiscalDocumentId: overrides.linkedFiscalDocumentId ?? 31809,
    createdAtUtc: overrides.createdAtUtc ?? '2026-04-03T00:00:00Z',
    updatedAtUtc: overrides.updatedAtUtc ?? '2026-04-03T00:00:00Z',
    applications: overrides.applications ?? [
      {
        id: 900,
        accountsReceivablePaymentId: 37,
        accountsReceivableInvoiceId: 10,
        applicationSequence: 1,
        appliedAmount: 700,
        previousBalance: 700,
        newBalance: 0,
        createdAtUtc: '2026-04-03T00:00:00Z',
      },
    ],
  };
}

function createInvoice(
  overrides: Partial<AccountsReceivablePortfolioItemResponse> = {},
): AccountsReceivablePortfolioItemResponse {
  return {
    accountsReceivableInvoiceId: overrides.accountsReceivableInvoiceId ?? 10,
    fiscalDocumentId: overrides.fiscalDocumentId ?? 31810,
    fiscalReceiverId: overrides.fiscalReceiverId ?? 77,
    receiverRfc: overrides.receiverRfc ?? 'AAA010101AAA',
    receiverLegalName: overrides.receiverLegalName ?? 'Receiver',
    fiscalSeries: overrides.fiscalSeries ?? 'A',
    fiscalFolio: overrides.fiscalFolio ?? '10',
    fiscalUuid: overrides.fiscalUuid ?? 'UUID-10',
    total: overrides.total ?? 1000,
    paidTotal: overrides.paidTotal ?? 1000 - (overrides.outstandingBalance ?? 0),
    outstandingBalance: overrides.outstandingBalance ?? 0,
    issuedAtUtc: overrides.issuedAtUtc ?? '2026-04-01T00:00:00Z',
    dueAtUtc: overrides.dueAtUtc ?? '2026-04-10T00:00:00Z',
    currencyCode: overrides.currencyCode ?? 'MXN',
    status: overrides.status ?? 'Paid',
    daysPastDue: overrides.daysPastDue ?? 0,
    agingBucket: overrides.agingBucket ?? 'Current',
    hasPendingCommitment: overrides.hasPendingCommitment ?? false,
    nextCommitmentDateUtc: overrides.nextCommitmentDateUtc ?? null,
    nextFollowUpAtUtc: overrides.nextFollowUpAtUtc ?? null,
    followUpPending: overrides.followUpPending ?? false,
  };
}
