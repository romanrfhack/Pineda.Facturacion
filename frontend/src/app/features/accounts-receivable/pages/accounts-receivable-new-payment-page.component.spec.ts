import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of, ReplaySubject, throwError } from 'rxjs';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { FiscalReceiverSatCatalogService } from '../../catalogs/application/fiscal-receiver-sat-catalog.service';
import { AccountsReceivableApiService } from '../infrastructure/accounts-receivable-api.service';
import { AccountsReceivableNewPaymentPageComponent } from './accounts-receivable-new-payment-page.component';

describe('AccountsReceivableNewPaymentPageComponent', () => {
  const queryParams$ = new ReplaySubject<ReturnType<typeof convertToParamMap>>(1);
  const feedbackService = { show: vi.fn() };
  const api = {
    getInvoiceById: vi.fn(),
    getInvoiceByFiscalDocumentId: vi.fn(),
    getReceiverWorkspace: vi.fn(),
    createPayment: vi.fn(),
  };

  beforeEach(() => {
    feedbackService.show.mockReset();
    queryParams$.next(convertToParamMap({ invoiceId: '2' }));
    api.getInvoiceById.mockReturnValue(of(createInvoice()));
    api.getInvoiceByFiscalDocumentId.mockReturnValue(of(createInvoice()));
    api.getReceiverWorkspace.mockReturnValue(of(createWorkspace()));
    api.createPayment.mockReturnValue(
      of({
        outcome: 'Created',
        isSuccess: true,
        payment: {
          id: 6,
          paymentDateUtc: '2026-04-03T00:00:00Z',
          paymentFormSat: '03',
          currencyCode: 'MXN',
          amount: 5000,
          appliedTotal: 0,
          remainingAmount: 5000,
          customerCreditBalanceAmount: 0,
          reference: 'DEP-1',
          notes: null,
          receivedFromFiscalReceiverId: 77,
          operationalStatus: 'CapturedUnapplied',
          repStatus: 'NoApplications',
          readyToPrepareRep: false,
          repBlockReason: null,
          unappliedDisposition: 'PendingAllocation',
          repDocumentStatus: null,
          repReservedAmount: 0,
          repFiscalizedAmount: 0,
          applicationsCount: 0,
          linkedFiscalDocumentId: 31809,
          createdAtUtc: '2026-04-03T00:00:00Z',
          updatedAtUtc: '2026-04-03T00:00:00Z',
          applications: [],
        },
      }),
    );

    TestBed.configureTestingModule({
      imports: [AccountsReceivableNewPaymentPageComponent],
      providers: [
        provideRouter([]),
        { provide: AccountsReceivableApiService, useValue: api },
        { provide: FeedbackService, useValue: feedbackService },
        {
          provide: FiscalReceiverSatCatalogService,
          useValue: {
            getCatalog: vi.fn().mockReturnValue(
              of({
                regimenFiscal: [],
                usoCfdi: [],
                byRegimenFiscal: [],
                paymentMethods: [],
                paymentForms: [{ code: '03', description: 'Transferencia electronica de fondos' }],
              }),
            ),
          },
        },
        {
          provide: ActivatedRoute,
          useValue: {
            queryParamMap: queryParams$.asObservable(),
            snapshot: { queryParamMap: convertToParamMap({}) },
          },
        },
      ],
    });
  });

  it('renders a creation-only view without payment application controls', async () => {
    queryParams$.next(convertToParamMap({ fiscalReceiverId: '77' }));

    const fixture = TestBed.createComponent(AccountsReceivableNewPaymentPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Nuevo pago');
    expect(fixture.nativeElement.textContent).not.toContain('Aplicar remanente seleccionado');
    expect(fixture.nativeElement.textContent).not.toContain(
      'Conservar como saldo a favor del cliente',
    );
    expect(fixture.nativeElement.textContent).not.toContain('Abrir flujo de complemento de pago');
  });

  it('creates a payment from invoice context and redirects to the payment detail view', async () => {
    queryParams$.next(convertToParamMap({ invoiceId: '2' }));
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    const fixture = TestBed.createComponent(AccountsReceivableNewPaymentPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    await fixture.componentInstance['createPayment']({
      paymentDateUtc: '2026-04-03T10:00',
      paymentFormSat: '03',
      amount: 1722,
      reference: 'DEP-1',
      notes: null,
    });

    expect(api.createPayment).toHaveBeenCalledWith(
      expect.objectContaining({
        accountsReceivableInvoiceId: 2,
        receivedFromFiscalReceiverId: 77,
      }),
    );
    expect(feedbackService.show).toHaveBeenCalledWith('success', 'Pago registrado.');
    expect(feedbackService.show).not.toHaveBeenCalledWith(
      'error',
      'No se pudo completar la operación.',
    );
    expect(navigateSpy).toHaveBeenCalledWith(['/app/accounts-receivable'], {
      queryParams: { paymentId: 6, invoiceId: 2 },
      state: {
        accountsReceivablePaymentCreated: true,
        accountsReceivableCreatedPaymentId: 6,
      },
    });
  });

  it('creates a payment from workspace context and redirects to the payment detail view', async () => {
    queryParams$.next(convertToParamMap({ fiscalReceiverId: '77' }));
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    const fixture = TestBed.createComponent(AccountsReceivableNewPaymentPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    await fixture.componentInstance['createPayment']({
      paymentDateUtc: '2026-04-03T10:00',
      paymentFormSat: '03',
      amount: 1722.01,
      reference: 'DEP-WS',
      notes: null,
    });

    expect(api.createPayment).toHaveBeenCalledWith(
      expect.objectContaining({
        accountsReceivableInvoiceId: null,
        receivedFromFiscalReceiverId: 77,
        amount: 1722.01,
      }),
    );
    expect(feedbackService.show).toHaveBeenCalledWith('success', 'Pago registrado.');
    expect(feedbackService.show).not.toHaveBeenCalledWith(
      'error',
      'No se pudo completar la operación.',
    );
    expect(navigateSpy).toHaveBeenCalledWith(['/app/accounts-receivable'], {
      queryParams: { paymentId: 6 },
      state: {
        accountsReceivablePaymentCreated: true,
        accountsReceivableCreatedPaymentId: 6,
      },
    });
  });

  it('shows a specific error and does not navigate when payment creation fails', async () => {
    queryParams$.next(convertToParamMap({ fiscalReceiverId: '77' }));
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    api.createPayment.mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 500 })),
    );

    const fixture = TestBed.createComponent(AccountsReceivableNewPaymentPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    await fixture.componentInstance['createPayment']({
      paymentDateUtc: '2026-04-03T10:00',
      paymentFormSat: '03',
      amount: 1722.01,
      reference: 'DEP-WS',
      notes: null,
    });

    expect(feedbackService.show).toHaveBeenCalledWith('error', 'No se pudo registrar el pago.');
    expect(feedbackService.show).not.toHaveBeenCalledWith('success', 'Pago registrado.');
    expect(feedbackService.show).not.toHaveBeenCalledWith(
      'error',
      'No se pudo completar la operación.',
    );
    expect(navigateSpy).not.toHaveBeenCalled();
  });

  it('keeps the creation success toast and shows a specific warning when navigation to the detail fails', async () => {
    queryParams$.next(convertToParamMap({ fiscalReceiverId: '77' }));
    const router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(false);

    const fixture = TestBed.createComponent(AccountsReceivableNewPaymentPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    await fixture.componentInstance['createPayment']({
      paymentDateUtc: '2026-04-03T10:00',
      paymentFormSat: '03',
      amount: 1722.01,
      reference: 'DEP-WS',
      notes: null,
    });

    expect(feedbackService.show).toHaveBeenNthCalledWith(1, 'success', 'Pago registrado.');
    expect(feedbackService.show).toHaveBeenNthCalledWith(
      2,
      'warning',
      'El pago fue registrado, pero no se pudo abrir el detalle.',
    );
    expect(feedbackService.show).not.toHaveBeenCalledWith(
      'error',
      'No se pudo completar la operación.',
    );
  });
});

function createInvoice() {
  return {
    id: 2,
    billingDocumentId: 1,
    fiscalDocumentId: 31809,
    fiscalStampId: 1,
    fiscalReceiverId: 77,
    receiverRfc: 'AAA010101AAA',
    receiverLegalName: 'Receiver',
    fiscalSeries: 'A',
    fiscalFolio: '31809',
    fiscalUuid: 'UUID-2',
    status: 'Open',
    paymentMethodSat: 'PPD',
    paymentFormSatInitial: '99',
    isCreditSale: true,
    creditDays: 15,
    issuedAtUtc: '2026-04-01T00:00:00Z',
    dueAtUtc: '2026-04-15T00:00:00Z',
    currencyCode: 'MXN',
    total: 1722,
    paidTotal: 0,
    outstandingBalance: 1722,
    createdAtUtc: '2026-04-01T00:00:00Z',
    updatedAtUtc: '2026-04-01T00:00:00Z',
    agingBucket: 'Current',
    hasPendingCommitment: false,
    nextCommitmentDateUtc: null,
    nextFollowUpAtUtc: null,
    followUpPending: false,
    collectionCommitments: [],
    collectionNotes: [],
    relatedPayments: [],
    relatedPaymentComplements: [],
    timeline: [],
    applications: [],
  };
}

function createWorkspace() {
  return {
    fiscalReceiverId: 77,
    rfc: 'AAA010101AAA',
    legalName: 'Receiver',
    summary: {
      pendingBalanceTotal: 2722,
      overdueBalanceTotal: 0,
      currentBalanceTotal: 2722,
      openInvoicesCount: 2,
      overdueInvoicesCount: 0,
      paymentsCount: 0,
      paymentsWithUnappliedAmountCount: 0,
      paymentsPendingRepCount: 0,
      nextFollowUpAtUtc: null,
      hasPendingCommitment: false,
      pendingCommitmentsCount: 0,
      recentNotesCount: 0,
      paymentsReadyToPrepareRepCount: 0,
      paymentsPreparedRepCount: 0,
      paymentsStampedRepCount: 0,
    },
    invoices: [],
    payments: [],
    pendingCommitments: [],
    recentNotes: [],
  };
}
