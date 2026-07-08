import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { By } from '@angular/platform-browser';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of, ReplaySubject, throwError } from 'rxjs';
import { AccountsReceivablePageComponent } from './accounts-receivable-page.component';
import { PaymentApplicationReassignModalComponent } from '../components/payment-application-reassign-modal.component';
import { AccountsReceivableApiService } from '../infrastructure/accounts-receivable-api.service';
import { PermissionService } from '../../../core/auth/permission.service';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { FiscalReceiversApiService } from '../../catalogs/infrastructure/fiscal-receivers-api.service';
import {
  AccountsReceivableInvoiceResponse,
  AccountsReceivablePaymentResponse,
  AccountsReceivablePaymentSummaryItemResponse,
  AccountsReceivablePortfolioItemResponse,
  AccountsReceivableReceiverWorkspaceResponse,
  AccountsReceivableReceiverWorkspaceSummaryResponse,
} from '../models/accounts-receivable.models';

describe('AccountsReceivablePageComponent', () => {
  const queryParams$ = new ReplaySubject<ReturnType<typeof convertToParamMap>>(1);
  const feedbackService = { show: vi.fn() };
  const api = {
    getInvoiceById: vi.fn(),
    getReceiverWorkspace: vi.fn(),
    getPaymentById: vi.fn(),
    setPaymentUnappliedDisposition: vi.fn(),
    searchPortfolio: vi.fn(),
    createPayment: vi.fn(),
    applyPayment: vi.fn(),
    getInvoiceByFiscalDocumentId: vi.fn(),
    searchPayments: vi.fn().mockReturnValue(of({ items: [] })),
    updatePaymentAmount: vi.fn(),
    deletePayment: vi.fn(),
    reassignPaymentApplications: vi.fn(),
  };

  beforeEach(() => {
    vi.restoreAllMocks();
    for (const mock of Object.values(api) as Array<{ mockReset: () => void }>) {
      mock.mockReset();
    }
    feedbackService.show.mockReset();
    queryParams$.next(convertToParamMap({ invoiceId: '2', paymentId: '6' }));
    api.getReceiverWorkspace.mockReturnValue(of(createWorkspace()));
    api.getInvoiceById.mockReturnValue(
      of({
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
      }),
    );
    api.getPaymentById.mockReturnValue(
      of({
        id: 6,
        paymentDateUtc: '2026-04-03T00:00:00Z',
        paymentFormSat: '03',
        currencyCode: 'MXN',
        amount: 5000,
        appliedTotal: 1722,
        remainingAmount: 3278,
        customerCreditBalanceAmount: 0,
        reference: 'DEP-1',
        notes: null,
        receivedFromFiscalReceiverId: 77,
        operationalStatus: 'PartiallyApplied',
        repStatus: 'PendingApplications',
        readyToPrepareRep: false,
        repBlockReason:
          'Unapplied payment remainder must be explicitly assigned before preparing REP.',
        unappliedDisposition: 'PendingAllocation',
        repDocumentStatus: null,
        repReservedAmount: 0,
        repFiscalizedAmount: 0,
        applicationsCount: 1,
        linkedFiscalDocumentId: 31809,
        createdAtUtc: '2026-04-03T00:00:00Z',
        updatedAtUtc: '2026-04-03T00:00:00Z',
        applications: [],
      }),
    );
    api.setPaymentUnappliedDisposition.mockReturnValue(
      of({
        outcome: 'Updated',
        isSuccess: true,
        accountsReceivablePaymentId: 6,
      }),
    );
    api.searchPortfolio.mockReturnValue(
      of({
        items: [
          {
            accountsReceivableInvoiceId: 2,
            fiscalDocumentId: 31809,
            fiscalReceiverId: 77,
            receiverRfc: 'AAA010101AAA',
            receiverLegalName: 'Receiver',
            fiscalSeries: 'A',
            fiscalFolio: '31809',
            fiscalUuid: 'UUID-2',
            total: 1722,
            paidTotal: 0,
            outstandingBalance: 1722,
            issuedAtUtc: '2026-04-01T00:00:00Z',
            dueAtUtc: '2026-04-15T00:00:00Z',
            status: 'Open',
            daysPastDue: 0,
            agingBucket: 'Current',
            hasPendingCommitment: false,
            nextCommitmentDateUtc: null,
            nextFollowUpAtUtc: null,
            followUpPending: false,
          },
          {
            accountsReceivableInvoiceId: 3,
            fiscalDocumentId: 31810,
            fiscalReceiverId: 77,
            receiverRfc: 'AAA010101AAA',
            receiverLegalName: 'Receiver',
            fiscalSeries: 'A',
            fiscalFolio: '31810',
            fiscalUuid: 'UUID-3',
            total: 1000,
            paidTotal: 0,
            outstandingBalance: 1000,
            issuedAtUtc: '2026-04-02T00:00:00Z',
            dueAtUtc: '2026-04-16T00:00:00Z',
            status: 'Open',
            daysPastDue: 0,
            agingBucket: 'Current',
            hasPendingCommitment: false,
            nextCommitmentDateUtc: null,
            nextFollowUpAtUtc: null,
            followUpPending: false,
          },
          {
            accountsReceivableInvoiceId: 4,
            fiscalDocumentId: 31811,
            fiscalReceiverId: 88,
            receiverRfc: 'BBB010101BBB',
            receiverLegalName: 'Other',
            fiscalSeries: 'B',
            fiscalFolio: '31811',
            fiscalUuid: 'UUID-4',
            total: 500,
            paidTotal: 0,
            outstandingBalance: 500,
            issuedAtUtc: '2026-04-02T00:00:00Z',
            dueAtUtc: '2026-04-16T00:00:00Z',
            status: 'Open',
            daysPastDue: 0,
            agingBucket: 'Current',
            hasPendingCommitment: false,
            nextCommitmentDateUtc: null,
            nextFollowUpAtUtc: null,
            followUpPending: false,
          },
        ],
      }),
    );
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
    api.searchPayments.mockReturnValue(of({ items: [] }));
    api.updatePaymentAmount.mockReturnValue(
      of({
        outcome: 'Updated',
        isSuccess: true,
        accountsReceivablePaymentId: 6,
        previousAmount: 5000,
        updatedAmount: 5250,
      }),
    );
    api.deletePayment.mockReturnValue(
      of({
        outcome: 'Deleted',
        isSuccess: true,
        accountsReceivablePaymentId: 6,
        deletedAmount: 5000,
        receivedFromFiscalReceiverId: 77,
      }),
    );
    api.reassignPaymentApplications.mockReturnValue(
      of({
        outcome: 'Reassigned',
        isSuccess: true,
        accountsReceivablePaymentId: 6,
        previousAppliedAmount: 700,
        newAppliedAmount: 700,
        remainingPaymentAmount: 300,
        payment: createPaymentDetail(),
        previousApplications: [],
        newApplications: [],
        affectedInvoiceIds: [10],
      }),
    );

    TestBed.configureTestingModule({
      imports: [AccountsReceivablePageComponent],
      providers: [
        provideRouter([]),
        { provide: AccountsReceivableApiService, useValue: api },
        {
          provide: FiscalReceiversApiService,
          useValue: {
            search: vi.fn().mockReturnValue(of([])),
            getSatCatalog: vi.fn().mockReturnValue(
              of({
                regimenFiscal: [],
                usoCfdi: [],
                byRegimenFiscal: [],
                paymentMethods: [],
                paymentForms: [],
              }),
            ),
          },
        },
        { provide: FeedbackService, useValue: feedbackService },
        {
          provide: PermissionService,
          useValue: { canManagePayments: () => true },
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

  it('queries the same receiver portfolio when loading remainder candidates from invoice detail', async () => {
    queryParams$.next(convertToParamMap({ invoiceId: '2' }));

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.searchPortfolio).toHaveBeenCalledWith({
      fiscalReceiverId: 77,
      hasPendingBalance: true,
    });
  });

  it('uses the payment receiver to load eligible remainder invoices when opening payment detail without invoice context', async () => {
    queryParams$.next(convertToParamMap({ paymentId: '6' }));
    api.getInvoiceById.mockClear();
    api.searchPortfolio.mockClear();
    api.getPaymentById.mockReset().mockReturnValue(
      of({
        id: 6,
        paymentDateUtc: '2026-04-03T00:00:00Z',
        paymentFormSat: '03',
        currencyCode: 'MXN',
        amount: 5000,
        appliedTotal: 1722,
        remainingAmount: 3278,
        customerCreditBalanceAmount: 0,
        reference: 'DEP-1',
        notes: null,
        receivedFromFiscalReceiverId: 77,
        operationalStatus: 'PartiallyApplied',
        repStatus: 'PendingApplications',
        readyToPrepareRep: false,
        repBlockReason:
          'Unapplied payment remainder must be explicitly assigned before preparing REP.',
        unappliedDisposition: 'PendingAllocation',
        repDocumentStatus: null,
        repReservedAmount: 0,
        repFiscalizedAmount: 0,
        applicationsCount: 1,
        linkedFiscalDocumentId: 31809,
        createdAtUtc: '2026-04-03T00:00:00Z',
        updatedAtUtc: '2026-04-03T00:00:00Z',
        applications: [
          {
            id: 900,
            accountsReceivablePaymentId: 6,
            accountsReceivableInvoiceId: 2,
            applicationSequence: 1,
            appliedAmount: 1722,
            previousBalance: 1722,
            newBalance: 0,
            createdAtUtc: '2026-04-03T00:00:00Z',
          },
        ],
      }),
    );

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.searchPortfolio).toHaveBeenCalledWith({
      fiscalReceiverId: 77,
      hasPendingBalance: true,
    });
    expect(api.getInvoiceById).not.toHaveBeenCalled();
    await vi.waitFor(() => {
      expect(fixture.componentInstance['payment']()?.id).toBe(6);
      expect(fixture.componentInstance['eligibleReceiverInvoices']()).toEqual([
        expect.objectContaining({ accountsReceivableInvoiceId: 3 }),
      ]);
      expect(feedbackService.show).not.toHaveBeenCalled();
    });
    expect(fixture.nativeElement.textContent).not.toContain('Crear pago');
    expect(fixture.nativeElement.textContent).not.toContain('Aplicar pago a esta cuenta');
  });

  it('shows a specific error when the payment detail cannot be loaded without recent creation context', async () => {
    queryParams$.next(convertToParamMap({ paymentId: '6' }));
    api.getInvoiceById.mockClear();
    api.searchPortfolio.mockClear();
    api.getPaymentById.mockReset().mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 500 })),
    );

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(feedbackService.show).toHaveBeenCalledWith(
      'error',
      'No se pudo cargar el detalle del pago.',
    );
    expect(feedbackService.show).not.toHaveBeenCalledWith(
      'error',
      'No se pudo completar la operación.',
    );
    expect(api.searchPortfolio).not.toHaveBeenCalled();
    expect(fixture.componentInstance['payment']()).toBeNull();
  });

  it('shows a specific warning when the freshly created payment detail cannot be loaded', async () => {
    queryParams$.next(convertToParamMap({ paymentId: '6' }));
    api.getInvoiceById.mockClear();
    api.searchPortfolio.mockClear();
    api.getPaymentById.mockReset().mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 500 })),
    );
    const angularRouter = TestBed.inject(Router);
    vi.spyOn(angularRouter, 'getCurrentNavigation').mockReturnValue({
      extras: {
        state: {
          accountsReceivablePaymentCreated: true,
          accountsReceivableCreatedPaymentId: 6,
        },
      },
    } as never);

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(feedbackService.show).toHaveBeenCalledWith(
      'warning',
      'El pago fue registrado, pero no se pudo cargar el detalle.',
    );
    expect(feedbackService.show).not.toHaveBeenCalledWith(
      'error',
      'No se pudo completar la operación.',
    );
    expect(api.searchPortfolio).not.toHaveBeenCalled();
    expect(fixture.componentInstance['payment']()).toBeNull();
  });

  it('keeps the payment visible and shows a specific warning when pending invoices cannot be loaded', async () => {
    queryParams$.next(convertToParamMap({ paymentId: '6' }));
    api.getInvoiceById.mockClear();
    api.searchPortfolio.mockReset().mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 500 })),
    );

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    await vi.waitFor(() => {
      expect(fixture.componentInstance['payment']()?.id).toBe(6);
      expect(fixture.componentInstance['eligibleReceiverInvoices']()).toEqual([]);
      expect(feedbackService.show).toHaveBeenCalledWith(
        'warning',
        'No se pudieron cargar las facturas pendientes del receptor.',
      );
    });
    expect(feedbackService.show).not.toHaveBeenCalledWith(
      'error',
      'No se pudo completar la operación.',
    );
  });

  it('renders the customer credit balance action when a payment has pending remainder allocation', async () => {
    queryParams$.next(convertToParamMap({ invoiceId: '2', paymentId: '6' }));

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Remanente no aplicado');
    expect(fixture.nativeElement.textContent).toContain('Conservar como saldo a favor del cliente');
    expect(fixture.nativeElement.textContent).toContain(
      'Complemento de pago bloqueado temporalmente',
    );
  });

  it('confirms customer credit balance and refreshes the payment detail', async () => {
    queryParams$.next(convertToParamMap({ invoiceId: '2', paymentId: '6' }));
    api.getPaymentById
      .mockReset()
      .mockReturnValueOnce(
        of({
          id: 6,
          paymentDateUtc: '2026-04-03T00:00:00Z',
          paymentFormSat: '03',
          currencyCode: 'MXN',
          amount: 2250,
          appliedTotal: 2241,
          remainingAmount: 9,
          customerCreditBalanceAmount: 0,
          reference: 'DEP-1',
          notes: null,
          receivedFromFiscalReceiverId: 77,
          operationalStatus: 'PartiallyApplied',
          repStatus: 'PendingApplications',
          readyToPrepareRep: false,
          repBlockReason:
            'Unapplied payment remainder must be explicitly assigned before preparing REP.',
          unappliedDisposition: 'PendingAllocation',
          repDocumentStatus: null,
          repReservedAmount: 0,
          repFiscalizedAmount: 0,
          applicationsCount: 1,
          linkedFiscalDocumentId: 31809,
          createdAtUtc: '2026-04-03T00:00:00Z',
          updatedAtUtc: '2026-04-03T00:00:00Z',
          applications: [],
        }),
      )
      .mockReturnValueOnce(
        of({
          id: 6,
          paymentDateUtc: '2026-04-03T00:00:00Z',
          paymentFormSat: '03',
          currencyCode: 'MXN',
          amount: 2250,
          appliedTotal: 2241,
          remainingAmount: 9,
          customerCreditBalanceAmount: 9,
          reference: 'DEP-1',
          notes: null,
          receivedFromFiscalReceiverId: 77,
          operationalStatus: 'PartiallyApplied',
          repStatus: 'ReadyToPrepare',
          readyToPrepareRep: true,
          repBlockReason: null,
          unappliedDisposition: 'CustomerCreditBalance',
          repDocumentStatus: null,
          repReservedAmount: 0,
          repFiscalizedAmount: 0,
          applicationsCount: 1,
          linkedFiscalDocumentId: 31809,
          createdAtUtc: '2026-04-03T00:00:00Z',
          updatedAtUtc: '2026-04-03T00:00:00Z',
          applications: [],
        }),
      );

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    await fixture.componentInstance['confirmCustomerCreditBalance']();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.setPaymentUnappliedDisposition).toHaveBeenCalledWith(6, {
      unappliedDisposition: 'CustomerCreditBalance',
    });
    expect(api.getPaymentById).toHaveBeenCalledTimes(2);
    expect(fixture.nativeElement.textContent).toContain('Saldo a favor confirmado');
    expect(fixture.nativeElement.textContent).not.toContain(
      'Conservar como saldo a favor del cliente',
    );
  });

  it('loads the receiver workspace when fiscalReceiverId is present in query params', async () => {
    queryParams$.next(convertToParamMap({ fiscalReceiverId: '77' }));

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.getReceiverWorkspace).toHaveBeenCalledWith(77);
    expect(fixture.nativeElement.textContent).toContain('Workspace del receptor');
    expect(fixture.nativeElement.textContent).toContain('Receiver');
    expect(fixture.nativeElement.textContent).toContain('AAA010101AAA');
  });

  it('shows Nuevo pago next to the empty workspace state when there are no payments', async () => {
    queryParams$.next(convertToParamMap({ fiscalReceiverId: '77' }));

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No hay pagos asociados a este receptor.');
    expect(fixture.nativeElement.textContent).toContain('Nuevo pago');
  });

  it('keeps Ver detalle and also shows Nuevo pago when the workspace already has payments', async () => {
    queryParams$.next(convertToParamMap({ fiscalReceiverId: '77' }));
    api.getReceiverWorkspace.mockReturnValueOnce(
      of(
        createWorkspace({
          payments: [
            {
              paymentId: 6,
              receivedAtUtc: '2026-04-03T00:00:00Z',
              amount: 5000,
              appliedAmount: 1722,
              unappliedAmount: 3278,
              customerCreditBalanceAmount: 0,
              currencyCode: 'MXN',
              fiscalReceiverId: 77,
              operationalStatus: 'PartiallyApplied',
              repStatus: 'PendingApplications',
              readyToPrepareRep: false,
              unappliedDisposition: 'PendingAllocation',
              applicationsCount: 1,
              linkedFiscalDocumentId: 31809,
              repReservedAmount: 0,
              repFiscalizedAmount: 0,
              reference: 'DEP-1',
            },
          ],
        }),
      ),
    );

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Ver detalle');
    expect(fixture.nativeElement.textContent).toContain('Nuevo pago');
  });

  it('shows payment mutation actions for unapplied workspace payments without REP references', async () => {
    queryParams$.next(convertToParamMap({ fiscalReceiverId: '77' }));
    api.getReceiverWorkspace.mockReturnValueOnce(
      of(
        createWorkspace({
          payments: [
            createWorkspacePayment({
              applicationsCount: 0,
              operationalStatus: 'CapturedUnapplied',
              repStatus: 'NoApplications',
            }),
          ],
        }),
      ),
    );

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(
      fixture.nativeElement.querySelector('[data-testid="workspace-payment-edit-button"]'),
    ).not.toBeNull();
    expect(
      fixture.nativeElement.querySelector('[data-testid="workspace-payment-delete-button"]'),
    ).not.toBeNull();
  });

  it('hides payment mutation actions for applied workspace payments', async () => {
    queryParams$.next(convertToParamMap({ fiscalReceiverId: '77' }));
    api.getReceiverWorkspace.mockReturnValueOnce(
      of(
        createWorkspace({
          payments: [
            createWorkspacePayment({
              applicationsCount: 1,
              operationalStatus: 'PartiallyApplied',
              repStatus: 'PendingApplications',
            }),
          ],
        }),
      ),
    );

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(
      fixture.nativeElement.querySelector('[data-testid="workspace-payment-edit-button"]'),
    ).toBeNull();
    expect(
      fixture.nativeElement.querySelector('[data-testid="workspace-payment-delete-button"]'),
    ).toBeNull();
  });

  it('shows and disables the reassign action for applied workspace payments without REP references', async () => {
    queryParams$.next(convertToParamMap({ fiscalReceiverId: '77' }));
    api.getReceiverWorkspace.mockReturnValueOnce(
      of(
        createWorkspace({
          payments: [
            createWorkspacePayment({
              applicationsCount: 1,
              appliedAmount: 700,
              unappliedAmount: 300,
              amount: 1000,
              operationalStatus: 'PartiallyApplied',
              repStatus: 'PendingApplications',
            }),
          ],
        }),
      ),
    );

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const reassignButton = fixture.nativeElement.querySelector(
      '[data-testid="workspace-payment-reassign-button"]',
    ) as HTMLButtonElement;

    expect(reassignButton).not.toBeNull();
    expect(reassignButton.disabled).toBe(false);

    fixture.componentInstance['loading'].set(true);
    fixture.detectChanges();

    const disabledReassignButton = fixture.nativeElement.querySelector(
      '[data-testid="workspace-payment-reassign-button"]',
    ) as HTMLButtonElement;
    expect(disabledReassignButton.disabled).toBe(true);
  });

  it('hides the reassign action for unapplied payments and payments with REP association', async () => {
    queryParams$.next(convertToParamMap({ fiscalReceiverId: '77' }));
    api.getReceiverWorkspace.mockReturnValueOnce(
      of(
        createWorkspace({
          payments: [
            createWorkspacePayment({ paymentId: 6, applicationsCount: 0 }),
            createWorkspacePayment({
              paymentId: 7,
              applicationsCount: 1,
              appliedAmount: 700,
              unappliedAmount: 300,
              amount: 1000,
              repDocumentStatus: 'Prepared',
              repReservedAmount: 700,
              repStatus: 'Prepared',
            }),
          ],
        }),
      ),
    );

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(
      fixture.nativeElement.querySelector('[data-testid="workspace-payment-reassign-button"]'),
    ).toBeNull();
  });

  it('shows payment mutation actions in detail mode for unapplied payments', async () => {
    queryParams$.next(convertToParamMap({ paymentId: '6' }));
    api.getPaymentById.mockReset().mockReturnValue(
      of({
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
      }),
    );

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="detail-payment-edit-button"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="detail-payment-delete-button"]')).not.toBeNull();
  });

  it('updates the payment amount and refreshes the receiver workspace', async () => {
    queryParams$.next(convertToParamMap({ fiscalReceiverId: '77' }));
    api.getReceiverWorkspace
      .mockReturnValueOnce(
        of(
          createWorkspace({
            payments: [createWorkspacePayment({ amount: 5000, applicationsCount: 0 })],
          }),
        ),
      )
      .mockReturnValueOnce(
        of(
          createWorkspace({
            payments: [createWorkspacePayment({ amount: 5250, applicationsCount: 0 })],
          }),
        ),
      );
    api.updatePaymentAmount.mockReturnValueOnce(
      of({
        outcome: 'Updated',
        isSuccess: true,
        accountsReceivablePaymentId: 6,
        previousAmount: 5000,
        updatedAmount: 5250,
      }),
    );

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const editButton = fixture.nativeElement.querySelector(
      '[data-testid="workspace-payment-edit-button"]',
    ) as HTMLButtonElement;
    editButton.click();
    fixture.detectChanges();
    await fixture.whenStable();

    const amountInput = fixture.nativeElement.querySelector(
      '[data-testid="workspace-payment-amount-input"]',
    ) as HTMLInputElement;
    amountInput.value = '5250.00';
    amountInput.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await fixture.whenStable();

    const saveButton = fixture.nativeElement.querySelector(
      '[data-testid="workspace-payment-save-button"]',
    ) as HTMLButtonElement;
    saveButton.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.updatePaymentAmount).toHaveBeenCalledWith(6, { amount: 5250 });
    expect(api.getReceiverWorkspace).toHaveBeenCalledTimes(2);
    expect(feedbackService.show).toHaveBeenCalledWith(
      'success',
      'Importe del pago actualizado.',
    );
  });

  it('reassigns payment applications from the receiver workspace and refreshes data', async () => {
    queryParams$.next(convertToParamMap({ fiscalReceiverId: '77' }));
    api.getReceiverWorkspace
      .mockReturnValueOnce(
        of(
          createWorkspace({
            invoices: [
              createWorkspaceInvoice({
                accountsReceivableInvoiceId: 11,
                fiscalFolio: '11',
                outstandingBalance: 300,
              }),
            ],
            payments: [
              createWorkspacePayment({
                applicationsCount: 1,
                appliedAmount: 700,
                unappliedAmount: 300,
                amount: 1000,
                operationalStatus: 'PartiallyApplied',
                repStatus: 'PendingApplications',
              }),
            ],
          }),
        ),
      )
      .mockReturnValueOnce(
        of(
          createWorkspace({
            payments: [
              createWorkspacePayment({
                applicationsCount: 1,
                appliedAmount: 700,
                unappliedAmount: 300,
                amount: 1000,
                operationalStatus: 'PartiallyApplied',
                repStatus: 'ReadyToPrepare',
                readyToPrepareRep: true,
              }),
            ],
          }),
        ),
      );
    api.getPaymentById.mockReturnValueOnce(of(createPaymentDetail()));
    api.getInvoiceById.mockReturnValueOnce(
      of({
        ...createInvoiceDetail(),
        id: 10,
        fiscalFolio: '10',
        status: 'Paid',
        paidTotal: 1000,
        outstandingBalance: 0,
      }),
    );

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const reassignButton = fixture.nativeElement.querySelector(
      '[data-testid="workspace-payment-reassign-button"]',
    ) as HTMLButtonElement;
    reassignButton.click();
    await fixture.whenStable();
    fixture.detectChanges();

    const modalDebugElement = fixture.debugElement.query(
      By.directive(PaymentApplicationReassignModalComponent),
    );
    expect(modalDebugElement).not.toBeNull();

    const modal = modalDebugElement.componentInstance as unknown as {
      updateReason: (value: string) => void;
      confirmSubmit: () => void;
    };
    modal.updateReason('Corrección validada por cobranza');
    modal.confirmSubmit();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.reassignPaymentApplications).toHaveBeenCalledWith(6, {
      reason: 'Corrección validada por cobranza',
      applications: [{ accountsReceivableInvoiceId: 10, appliedAmount: 700 }],
    });
    expect(api.getReceiverWorkspace).toHaveBeenCalledTimes(2);
    expect(feedbackService.show).toHaveBeenCalledWith(
      'success',
      'La distribución del pago se actualizó correctamente.',
    );
    expect(
      fixture.debugElement.query(By.directive(PaymentApplicationReassignModalComponent)),
    ).toBeNull();
  });

  it('deletes the payment after confirmation and refreshes the receiver workspace', async () => {
    queryParams$.next(convertToParamMap({ fiscalReceiverId: '77' }));
    api.getReceiverWorkspace
      .mockReturnValueOnce(
        of(
          createWorkspace({
            payments: [createWorkspacePayment({ applicationsCount: 0 })],
          }),
        ),
      )
      .mockReturnValueOnce(
        of(
          createWorkspace({
            summary: {
              paymentsCount: 0,
              paymentsWithUnappliedAmountCount: 0,
              paymentsPendingRepCount: 0,
            },
            payments: [],
          }),
        ),
      );
    api.deletePayment.mockReturnValueOnce(
      of({
        outcome: 'Deleted',
        isSuccess: true,
        accountsReceivablePaymentId: 6,
        deletedAmount: 5000,
        receivedFromFiscalReceiverId: 77,
      }),
    );
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const deleteButton = fixture.nativeElement.querySelector(
      '[data-testid="workspace-payment-delete-button"]',
    ) as HTMLButtonElement;
    deleteButton.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(window.confirm).toHaveBeenCalled();
    expect(api.deletePayment).toHaveBeenCalledWith(6);
    expect(api.getReceiverWorkspace).toHaveBeenCalledTimes(2);
    expect(feedbackService.show).toHaveBeenCalledWith('success', 'Pago eliminado.');
  });

  it('shows the backend conflict message when updating a payment is blocked', async () => {
    queryParams$.next(convertToParamMap({ fiscalReceiverId: '77' }));
    api.getReceiverWorkspace.mockReturnValueOnce(
      of(
        createWorkspace({
          payments: [createWorkspacePayment({ applicationsCount: 0 })],
        }),
      ),
    );
    api.updatePaymentAmount.mockReturnValueOnce(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 409,
            error: {
              errorMessage:
                'El pago ya fue aplicado a una o más facturas y no puede editarse.',
            },
          }),
      ),
    );

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const editButton = fixture.nativeElement.querySelector(
      '[data-testid="workspace-payment-edit-button"]',
    ) as HTMLButtonElement;
    editButton.click();
    fixture.detectChanges();
    await fixture.whenStable();

    const saveButton = fixture.nativeElement.querySelector(
      '[data-testid="workspace-payment-save-button"]',
    ) as HTMLButtonElement;
    saveButton.click();
    await fixture.whenStable();

    expect(feedbackService.show).toHaveBeenCalledWith(
      'error',
      'No se pudo actualizar el importe del pago. El pago ya fue aplicado a una o más facturas y no puede editarse.',
    );
  });

  it('keeps the reassign modal open and shows the backend 409 business message', async () => {
    queryParams$.next(convertToParamMap({ fiscalReceiverId: '77' }));
    api.reassignPaymentApplications.mockReturnValueOnce(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 409,
            error: {
              errorMessage: 'El pago ya tiene REP asociado.',
            },
          }),
      ),
    );

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.componentInstance['reassignPayment'].set(createPaymentDetail());
    fixture.componentInstance['reassignFiscalReceiverId'].set(77);
    fixture.componentInstance['reassignCandidateInvoices'].set([
      createWorkspaceInvoice({ accountsReceivableInvoiceId: 10, outstandingBalance: 0, status: 'Paid' }),
    ]);
    fixture.detectChanges();

    await fixture.componentInstance['submitPaymentApplicationReassign']({
      reason: 'Corrección validada',
      applications: [{ accountsReceivableInvoiceId: 10, appliedAmount: 700 }],
    });
    fixture.detectChanges();

    expect(fixture.componentInstance['reassignPayment']()).not.toBeNull();
    expect(fixture.componentInstance['reassignErrorMessage']()).toBe(
      'El pago ya tiene REP asociado.',
    );
    expect(fixture.nativeElement.textContent).toContain('El pago ya tiene REP asociado.');
  });

  it('keeps the reassign modal open and shows the backend 400 business message', async () => {
    queryParams$.next(convertToParamMap({ fiscalReceiverId: '77' }));
    api.reassignPaymentApplications.mockReturnValueOnce(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 400,
            error: {
              errorMessage: 'El motivo es obligatorio.',
            },
          }),
      ),
    );

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.componentInstance['reassignPayment'].set(createPaymentDetail());
    fixture.componentInstance['reassignFiscalReceiverId'].set(77);
    fixture.componentInstance['reassignCandidateInvoices'].set([
      createWorkspaceInvoice({ accountsReceivableInvoiceId: 10, outstandingBalance: 0, status: 'Paid' }),
    ]);
    fixture.detectChanges();

    await fixture.componentInstance['submitPaymentApplicationReassign']({
      reason: 'Corrección',
      applications: [{ accountsReceivableInvoiceId: 10, appliedAmount: 700 }],
    });
    fixture.detectChanges();

    expect(fixture.componentInstance['reassignPayment']()).not.toBeNull();
    expect(fixture.componentInstance['reassignErrorMessage']()).toBe('El motivo es obligatorio.');
    expect(fixture.nativeElement.textContent).toContain('El motivo es obligatorio.');
  });

  it('sorts overdue invoices first in the receiver workspace and renders the collection label', async () => {
    queryParams$.next(convertToParamMap({ fiscalReceiverId: '77' }));
    api.getReceiverWorkspace.mockReturnValueOnce(
      of(
        createWorkspace({
          summary: {
            pendingBalanceTotal: 4900,
            overdueBalanceTotal: 1500,
            currentBalanceTotal: 3400,
            openInvoicesCount: 3,
            overdueInvoicesCount: 1,
          },
          invoices: [
            createWorkspaceInvoice({
              accountsReceivableInvoiceId: 11,
              fiscalFolio: '1011',
              dueAtUtc: '2026-04-24T00:00:00Z',
              total: 2400,
              outstandingBalance: 2400,
            }),
            createWorkspaceInvoice({
              accountsReceivableInvoiceId: 10,
              fiscalFolio: '1010',
              dueAtUtc: '2026-04-10T00:00:00Z',
              total: 1500,
              outstandingBalance: 1500,
            }),
            createWorkspaceInvoice({
              accountsReceivableInvoiceId: 12,
              fiscalFolio: '1012',
              dueAtUtc: '2026-04-15T00:00:00Z',
              total: 1000,
              outstandingBalance: 1000,
            }),
          ],
        }),
      ),
    );

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.componentInstance['receiverWorkspaceToday'].set(new Date('2026-04-15T12:00:00Z'));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const rows = getReceiverWorkspaceInvoiceRows(fixture);

    expect(rows).toHaveLength(3);
    expect(rows[0].textContent).toContain('A-1010');
    expect(rows[0].textContent).toContain('Vencida · hace 5 días');
    expect(rows[0].getAttribute('data-collection-status')).toBe('overdue');
    expect(rows[0].classList.contains('is-overdue')).toBe(true);
  });

  it('filters the workspace grid when Total vencido is selected', async () => {
    queryParams$.next(convertToParamMap({ fiscalReceiverId: '77' }));
    api.getReceiverWorkspace.mockReturnValueOnce(
      of(
        createWorkspace({
          summary: {
            pendingBalanceTotal: 3900,
            overdueBalanceTotal: 1500,
            currentBalanceTotal: 2400,
            openInvoicesCount: 2,
            overdueInvoicesCount: 1,
          },
          invoices: [
            createWorkspaceInvoice({
              accountsReceivableInvoiceId: 10,
              fiscalFolio: '1010',
              dueAtUtc: '2026-04-12T00:00:00Z',
              outstandingBalance: 1500,
            }),
            createWorkspaceInvoice({
              accountsReceivableInvoiceId: 11,
              fiscalFolio: '1011',
              dueAtUtc: '2026-04-18T00:00:00Z',
              outstandingBalance: 2400,
            }),
          ],
        }),
      ),
    );

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.componentInstance['receiverWorkspaceToday'].set(new Date('2026-04-15T12:00:00Z'));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const overdueButton = getSummaryCardButton(fixture, 'Total vencido');
    overdueButton.click();
    fixture.detectChanges();

    const rows = getReceiverWorkspaceInvoiceRows(fixture);

    expect(overdueButton.getAttribute('aria-pressed')).toBe('true');
    expect(fixture.nativeElement.textContent).toContain('Mostrando vencidas · 1 de 2');
    expect(rows).toHaveLength(1);
    expect(rows[0].textContent).toContain('A-1010');
  });

  it('keeps the overdue summary controls aligned with the visible overdue invoices and excludes zero-balance rows', async () => {
    queryParams$.next(convertToParamMap({ fiscalReceiverId: '77' }));
    api.getReceiverWorkspace.mockReturnValueOnce(
      of(
        createWorkspace({
          summary: {
            pendingBalanceTotal: 9999,
            overdueBalanceTotal: 9999,
            currentBalanceTotal: 9999,
            openInvoicesCount: 99,
            overdueInvoicesCount: 99,
          },
          invoices: [
            createWorkspaceInvoice({
              accountsReceivableInvoiceId: 10,
              fiscalFolio: '1010',
              dueAtUtc: '2026-04-12T00:00:00Z',
              outstandingBalance: 1500,
            }),
            createWorkspaceInvoice({
              accountsReceivableInvoiceId: 11,
              fiscalFolio: '1011',
              dueAtUtc: '2026-04-10T00:00:00Z',
              outstandingBalance: 0,
            }),
            createWorkspaceInvoice({
              accountsReceivableInvoiceId: 12,
              fiscalFolio: '1012',
              dueAtUtc: '2026-04-18T00:00:00Z',
              outstandingBalance: 2400,
            }),
          ],
        }),
      ),
    );

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.componentInstance['receiverWorkspaceToday'].set(new Date('2026-04-15T12:00:00Z'));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const overdueSummaryCard = getSummaryCardButton(fixture, 'Total vencido');
    const overdueCountButton = getOverdueCountButton(fixture);

    expect(overdueSummaryCard.textContent).toMatch(/1,500\.00/);
    expect(overdueCountButton.textContent).toContain('1 vencida');

    overdueSummaryCard.click();
    fixture.detectChanges();

    const rows = getReceiverWorkspaceInvoiceRows(fixture);
    expect(rows).toHaveLength(1);
    expect(rows[0].textContent).toContain('A-1010');
    expect(rows[0].textContent).not.toContain('A-1011');
    expect(fixture.nativeElement.textContent).toContain('Mostrando vencidas · 1 de 2');
  });

  it('returns to the full open list when Facturas abiertas is selected after filtering overdue', async () => {
    queryParams$.next(convertToParamMap({ fiscalReceiverId: '77' }));
    api.getReceiverWorkspace.mockReturnValueOnce(
      of(
        createWorkspace({
          summary: {
            pendingBalanceTotal: 3900,
            overdueBalanceTotal: 1500,
            currentBalanceTotal: 2400,
            openInvoicesCount: 2,
            overdueInvoicesCount: 1,
          },
          invoices: [
            createWorkspaceInvoice({
              accountsReceivableInvoiceId: 10,
              fiscalFolio: '1010',
              dueAtUtc: '2026-04-12T00:00:00Z',
              outstandingBalance: 1500,
            }),
            createWorkspaceInvoice({
              accountsReceivableInvoiceId: 11,
              fiscalFolio: '1011',
              dueAtUtc: '2026-04-18T00:00:00Z',
              outstandingBalance: 2400,
            }),
          ],
        }),
      ),
    );

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.componentInstance['receiverWorkspaceToday'].set(new Date('2026-04-15T12:00:00Z'));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    getSummaryCardButton(fixture, 'Total vencido').click();
    fixture.detectChanges();
    getSummaryCardButton(fixture, 'Facturas abiertas').click();
    fixture.detectChanges();

    const rows = getReceiverWorkspaceInvoiceRows(fixture);

    expect(fixture.nativeElement.textContent).toContain('2 cuenta(s) abierta(s)');
    expect(rows).toHaveLength(2);
    expect(rows[0].textContent).toContain('A-1010');
    expect(rows[1].textContent).toContain('A-1011');
  });

  it('shows an empty state when the overdue workspace filter has no results', async () => {
    queryParams$.next(convertToParamMap({ fiscalReceiverId: '77' }));
    api.getReceiverWorkspace.mockReturnValueOnce(
      of(
        createWorkspace({
          summary: {
            pendingBalanceTotal: 2400,
            overdueBalanceTotal: 0,
            currentBalanceTotal: 2400,
            openInvoicesCount: 1,
            overdueInvoicesCount: 0,
          },
          invoices: [
            createWorkspaceInvoice({
              accountsReceivableInvoiceId: 11,
              fiscalFolio: '1011',
              dueAtUtc: '2026-04-20T00:00:00Z',
              outstandingBalance: 2400,
            }),
          ],
        }),
      ),
    );

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.componentInstance['receiverWorkspaceToday'].set(new Date('2026-04-15T12:00:00Z'));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    getSummaryCardButton(fixture, 'Total vencido').click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain(
      'No hay facturas vencidas para este receptor.',
    );
    expect(getReceiverWorkspaceInvoiceRows(fixture)).toHaveLength(0);
  });

  it('shows the empty state for the current or due soon filter when nothing matches', async () => {
    queryParams$.next(convertToParamMap({ fiscalReceiverId: '77' }));
    api.getReceiverWorkspace.mockReturnValueOnce(
      of(
        createWorkspace({
          summary: {
            pendingBalanceTotal: 1200,
            overdueBalanceTotal: 1200,
            currentBalanceTotal: 0,
            openInvoicesCount: 1,
            overdueInvoicesCount: 1,
          },
          invoices: [
            createWorkspaceInvoice({
              accountsReceivableInvoiceId: 10,
              fiscalFolio: '1010',
              dueAtUtc: '2026-04-12T00:00:00Z',
              outstandingBalance: 1200,
            }),
          ],
        }),
      ),
    );

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.componentInstance['receiverWorkspaceToday'].set(new Date('2026-04-15T12:00:00Z'));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    getSummaryCardButton(fixture, 'Vigente / por vencer').click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain(
      'No hay facturas vigentes o por vencer para este receptor.',
    );
  });

  it('renders native buttons with clear aria state for the summary filters and overdue counter', async () => {
    queryParams$.next(convertToParamMap({ fiscalReceiverId: '77' }));
    api.getReceiverWorkspace.mockReturnValueOnce(
      of(
        createWorkspace({
          summary: {
            pendingBalanceTotal: 1500,
            overdueBalanceTotal: 1500,
            currentBalanceTotal: 0,
            openInvoicesCount: 1,
            overdueInvoicesCount: 1,
          },
          invoices: [
            createWorkspaceInvoice({
              accountsReceivableInvoiceId: 10,
              fiscalFolio: '1010',
              dueAtUtc: '2026-04-12T00:00:00Z',
              outstandingBalance: 1500,
            }),
          ],
        }),
      ),
    );

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.componentInstance['receiverWorkspaceToday'].set(new Date('2026-04-15T12:00:00Z'));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const overdueSummaryCard = getSummaryCardButton(fixture, 'Total vencido');
    const overdueCountButton = getOverdueCountButton(fixture);

    expect(overdueSummaryCard.tagName).toBe('BUTTON');
    expect(overdueSummaryCard.getAttribute('type')).toBe('button');
    expect(overdueSummaryCard.getAttribute('aria-pressed')).toBe('false');

    expect(overdueCountButton.tagName).toBe('BUTTON');
    expect(overdueCountButton.getAttribute('type')).toBe('button');
    expect(overdueCountButton.getAttribute('aria-label')).toBe('Mostrar 1 factura vencida');
    expect(overdueCountButton.getAttribute('aria-pressed')).toBe('false');

    overdueCountButton.click();
    fixture.detectChanges();

    expect(overdueCountButton.getAttribute('aria-pressed')).toBe('true');
    expect(overdueSummaryCard.getAttribute('aria-pressed')).toBe('true');
  });

  it('renders receiver workspace dates using the calendar date instead of shifting by UTC time', async () => {
    queryParams$.next(convertToParamMap({ fiscalReceiverId: '77' }));
    api.getReceiverWorkspace.mockReturnValueOnce(
      of(
        createWorkspace({
          summary: {
            pendingBalanceTotal: 1500,
            overdueBalanceTotal: 0,
            currentBalanceTotal: 1500,
            openInvoicesCount: 1,
            overdueInvoicesCount: 0,
          },
          invoices: [
            createWorkspaceInvoice({
              accountsReceivableInvoiceId: 10,
              fiscalFolio: '1010',
              issuedAtUtc: '2026-04-01T23:30:00-05:00',
              dueAtUtc: '2026-04-15T23:59:59-05:00',
              outstandingBalance: 1500,
            }),
          ],
        }),
      ),
    );

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.componentInstance['receiverWorkspaceToday'].set(new Date('2026-04-15T12:00:00-06:00'));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const firstRow = getReceiverWorkspaceInvoiceRows(fixture)[0];

    expect(firstRow.textContent).toContain('2026-04-01');
    expect(firstRow.textContent).toContain('2026-04-15');
    expect(firstRow.textContent).toContain('Vence hoy');
  });
});

function createWorkspace(
  overrides: Omit<Partial<AccountsReceivableReceiverWorkspaceResponse>, 'summary'> & {
    summary?: Partial<AccountsReceivableReceiverWorkspaceSummaryResponse>;
  } = {},
): AccountsReceivableReceiverWorkspaceResponse {
  return {
    fiscalReceiverId: overrides.fiscalReceiverId ?? 77,
    rfc: overrides.rfc ?? 'AAA010101AAA',
    legalName: overrides.legalName ?? 'Receiver',
    summary: {
      pendingBalanceTotal: 2722,
      overdueBalanceTotal: 0,
      currentBalanceTotal: 2722,
      openInvoicesCount: 2,
      overdueInvoicesCount: 0,
      paymentsCount: 1,
      paymentsWithUnappliedAmountCount: 1,
      paymentsPendingRepCount: 1,
      nextFollowUpAtUtc: null,
      hasPendingCommitment: false,
      pendingCommitmentsCount: 0,
      recentNotesCount: 0,
      paymentsReadyToPrepareRepCount: 0,
      paymentsPreparedRepCount: 0,
      paymentsStampedRepCount: 0,
      ...overrides.summary,
    },
    invoices: overrides.invoices ?? [],
    payments: overrides.payments ?? [],
    pendingCommitments: overrides.pendingCommitments ?? [],
    recentNotes: overrides.recentNotes ?? [],
  };
}

function createPaymentDetail(
  overrides: Partial<AccountsReceivablePaymentResponse> = {},
): AccountsReceivablePaymentResponse {
  return {
    id: overrides.id ?? 6,
    paymentDateUtc: overrides.paymentDateUtc ?? '2026-04-03T00:00:00Z',
    paymentFormSat: overrides.paymentFormSat ?? '03',
    currencyCode: overrides.currencyCode ?? 'MXN',
    amount: overrides.amount ?? 1000,
    appliedTotal: overrides.appliedTotal ?? 700,
    remainingAmount: overrides.remainingAmount ?? 300,
    customerCreditBalanceAmount: overrides.customerCreditBalanceAmount ?? 0,
    reference: overrides.reference ?? 'DEP-1',
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
        accountsReceivablePaymentId: 6,
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

function createWorkspacePayment(
  overrides: Partial<AccountsReceivablePaymentSummaryItemResponse> = {},
): AccountsReceivablePaymentSummaryItemResponse {
  return {
    paymentId: overrides.paymentId ?? 6,
    receivedAtUtc: overrides.receivedAtUtc ?? '2026-04-03T00:00:00Z',
    amount: overrides.amount ?? 5000,
    appliedAmount: overrides.appliedAmount ?? 0,
    unappliedAmount: overrides.unappliedAmount ?? 5000,
    customerCreditBalanceAmount: overrides.customerCreditBalanceAmount ?? 0,
    currencyCode: overrides.currencyCode ?? 'MXN',
    reference: overrides.reference ?? 'DEP-1',
    payerName: overrides.payerName ?? 'Receiver',
    fiscalReceiverId: overrides.fiscalReceiverId ?? 77,
    operationalStatus: overrides.operationalStatus ?? 'CapturedUnapplied',
    repStatus: overrides.repStatus ?? 'NoApplications',
    readyToPrepareRep: overrides.readyToPrepareRep ?? false,
    repBlockReason: overrides.repBlockReason ?? null,
    unappliedDisposition: overrides.unappliedDisposition ?? 'PendingAllocation',
    repDocumentStatus: overrides.repDocumentStatus ?? null,
    applicationsCount: overrides.applicationsCount ?? 0,
    linkedFiscalDocumentId: overrides.linkedFiscalDocumentId ?? 31809,
    repReservedAmount: overrides.repReservedAmount ?? 0,
    repFiscalizedAmount: overrides.repFiscalizedAmount ?? 0,
  };
}

function createWorkspaceInvoice(
  overrides: Partial<AccountsReceivablePortfolioItemResponse> = {},
): AccountsReceivablePortfolioItemResponse {
  return {
    accountsReceivableInvoiceId: overrides.accountsReceivableInvoiceId ?? 10,
    fiscalDocumentId: overrides.fiscalDocumentId ?? 31810,
    fiscalReceiverId: overrides.fiscalReceiverId ?? 77,
    receiverRfc: overrides.receiverRfc ?? 'AAA010101AAA',
    receiverLegalName: overrides.receiverLegalName ?? 'Receiver',
    fiscalSeries: overrides.fiscalSeries ?? 'A',
    fiscalFolio: overrides.fiscalFolio ?? '1010',
    fiscalUuid: overrides.fiscalUuid ?? `UUID-${overrides.accountsReceivableInvoiceId ?? 10}`,
    total: overrides.total ?? 1000,
    paidTotal: overrides.paidTotal ?? 0,
    outstandingBalance: overrides.outstandingBalance ?? 1000,
    issuedAtUtc: overrides.issuedAtUtc ?? '2026-04-01T00:00:00Z',
    dueAtUtc: overrides.dueAtUtc ?? '2026-04-20T00:00:00Z',
    status: overrides.status ?? 'Open',
    daysPastDue: overrides.daysPastDue ?? 0,
    agingBucket: overrides.agingBucket ?? 'Current',
    hasPendingCommitment: overrides.hasPendingCommitment ?? false,
    nextCommitmentDateUtc: overrides.nextCommitmentDateUtc ?? null,
    nextFollowUpAtUtc: overrides.nextFollowUpAtUtc ?? null,
    followUpPending: overrides.followUpPending ?? false,
  };
}

function createInvoiceDetail(
  overrides: Partial<AccountsReceivableInvoiceResponse> = {},
): AccountsReceivableInvoiceResponse {
  return {
    id: 10,
    billingDocumentId: 1,
    fiscalDocumentId: 31810,
    fiscalStampId: 1,
    fiscalReceiverId: 77,
    receiverRfc: 'AAA010101AAA',
    receiverLegalName: 'Receiver',
    fiscalSeries: 'A',
    fiscalFolio: '10',
    fiscalUuid: 'UUID-10',
    status: 'Paid',
    paymentMethodSat: 'PPD',
    paymentFormSatInitial: '99',
    isCreditSale: true,
    creditDays: 15,
    issuedAtUtc: '2026-04-01T00:00:00Z',
    dueAtUtc: '2026-04-15T00:00:00Z',
    currencyCode: 'MXN',
    total: 1000,
    paidTotal: 1000,
    outstandingBalance: 0,
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
    ...overrides,
  };
}

function getReceiverWorkspaceInvoiceRows(
  fixture: ComponentFixture<AccountsReceivablePageComponent>,
) {
  return Array.from(
    fixture.nativeElement.querySelectorAll('.receiver-workspace-table tbody tr'),
  ) as HTMLTableRowElement[];
}

function getSummaryCardButton(
  fixture: ComponentFixture<AccountsReceivablePageComponent>,
  title: string,
) {
  const button = Array.from(
    fixture.nativeElement.querySelectorAll('.summary-card-action') as NodeListOf<HTMLButtonElement>,
  ).find((element) => element.textContent?.includes(title));

  if (!button) {
    throw new Error(`Summary card button not found: ${title}`);
  }

  return button as HTMLButtonElement;
}

function getOverdueCountButton(fixture: ComponentFixture<AccountsReceivablePageComponent>) {
  const button = fixture.nativeElement.querySelector('.detail-badges .badge-button');

  if (!button) {
    throw new Error('Overdue count button not found');
  }

  return button as HTMLButtonElement;
}
