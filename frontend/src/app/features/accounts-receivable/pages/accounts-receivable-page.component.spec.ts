import { ComponentFixture, TestBed } from '@angular/core/testing';
import { convertToParamMap, provideRouter } from '@angular/router';
import { of, ReplaySubject } from 'rxjs';
import { AccountsReceivablePageComponent } from './accounts-receivable-page.component';
import { AccountsReceivableApiService } from '../infrastructure/accounts-receivable-api.service';
import { PermissionService } from '../../../core/auth/permission.service';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { ActivatedRoute } from '@angular/router';
import { FiscalReceiversApiService } from '../../catalogs/infrastructure/fiscal-receivers-api.service';
import {
  AccountsReceivablePortfolioItemResponse,
  AccountsReceivableReceiverWorkspaceResponse,
  AccountsReceivableReceiverWorkspaceSummaryResponse,
} from '../models/accounts-receivable.models';

describe('AccountsReceivablePageComponent', () => {
  const queryParams$ = new ReplaySubject<ReturnType<typeof convertToParamMap>>(1);
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
  };

  beforeEach(() => {
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
        { provide: FeedbackService, useValue: { show: vi.fn() } },
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
    expect(fixture.nativeElement.textContent).not.toContain('Crear pago');
    expect(fixture.nativeElement.textContent).not.toContain('Aplicar pago a esta cuenta');
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
