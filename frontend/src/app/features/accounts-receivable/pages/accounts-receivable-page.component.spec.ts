import { TestBed } from '@angular/core/testing';
import { convertToParamMap, provideRouter } from '@angular/router';
import { of, ReplaySubject } from 'rxjs';
import { AccountsReceivablePageComponent } from './accounts-receivable-page.component';
import { AccountsReceivableApiService } from '../infrastructure/accounts-receivable-api.service';
import { PermissionService } from '../../../core/auth/permission.service';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { ActivatedRoute } from '@angular/router';
import { FiscalReceiversApiService } from '../../catalogs/infrastructure/fiscal-receivers-api.service';

describe('AccountsReceivablePageComponent', () => {
  const queryParams$ = new ReplaySubject<ReturnType<typeof convertToParamMap>>(1);
  const api = {
    getInvoiceById: vi.fn(),
    getReceiverWorkspace: vi.fn(),
    getPaymentById: vi.fn(),
    searchPortfolio: vi.fn(),
    createPayment: vi.fn(),
    applyPayment: vi.fn(),
    getInvoiceByFiscalDocumentId: vi.fn(),
    searchPayments: vi.fn().mockReturnValue(of({ items: [] }))
  };

  beforeEach(async () => {
    queryParams$.next(convertToParamMap({ invoiceId: '2', paymentId: '6' }));
    api.getReceiverWorkspace.mockReturnValue(of({
      fiscalReceiverId: 77,
      rfc: 'AAA010101AAA',
      legalName: 'Receiver',
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
        paymentsStampedRepCount: 0
      },
      invoices: [],
      payments: [],
      pendingCommitments: [],
      recentNotes: []
    }));
    api.getInvoiceById.mockReturnValue(of({
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
      applications: []
    }));
    api.getPaymentById.mockReturnValue(of({
      id: 6,
      paymentDateUtc: '2026-04-03T00:00:00Z',
      paymentFormSat: '03',
      currencyCode: 'MXN',
      amount: 5000,
      appliedTotal: 1722,
      remainingAmount: 3278,
      reference: 'DEP-1',
      notes: null,
      receivedFromFiscalReceiverId: 77,
      operationalStatus: 'PartiallyApplied',
      repStatus: 'PendingApplications',
      repDocumentStatus: null,
      repReservedAmount: 0,
      repFiscalizedAmount: 0,
      applicationsCount: 1,
      linkedFiscalDocumentId: 31809,
      createdAtUtc: '2026-04-03T00:00:00Z',
      updatedAtUtc: '2026-04-03T00:00:00Z',
      applications: []
    }));
    api.searchPortfolio.mockReturnValue(of({
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
          followUpPending: false
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
          followUpPending: false
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
          followUpPending: false
        }
      ]
    }));
    api.createPayment.mockReturnValue(of({
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
        reference: 'DEP-1',
        notes: null,
        receivedFromFiscalReceiverId: 77,
        operationalStatus: 'CapturedUnapplied',
        repStatus: 'NoApplications',
        repDocumentStatus: null,
        repReservedAmount: 0,
        repFiscalizedAmount: 0,
        applicationsCount: 0,
        linkedFiscalDocumentId: 31809,
        createdAtUtc: '2026-04-03T00:00:00Z',
        updatedAtUtc: '2026-04-03T00:00:00Z',
        applications: []
      }
    }));

    await TestBed.configureTestingModule({
      imports: [AccountsReceivablePageComponent],
      providers: [
        provideRouter([]),
        { provide: AccountsReceivableApiService, useValue: api },
        {
          provide: FiscalReceiversApiService,
          useValue: {
            search: vi.fn().mockReturnValue(of([])),
            getSatCatalog: vi.fn().mockReturnValue(of({
              regimenFiscal: [],
              usoCfdi: [],
              byRegimenFiscal: [],
              paymentMethods: [],
              paymentForms: []
            }))
          }
        },
        { provide: FeedbackService, useValue: { show: vi.fn() } },
        {
          provide: PermissionService,
          useValue: { canManagePayments: () => true }
        },
        {
          provide: ActivatedRoute,
          useValue: { queryParamMap: queryParams$.asObservable(), snapshot: { queryParamMap: convertToParamMap({}) } }
        }
      ]
    }).compileComponents();
  });

  it('queries the same receiver portfolio when loading remainder candidates from invoice detail', async () => {
    queryParams$.next(convertToParamMap({ invoiceId: '2' }));

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.searchPortfolio).toHaveBeenCalledWith({ fiscalReceiverId: 77, hasPendingBalance: true });
  });

  it('sends the current receiver id when creating a payment from invoice detail', async () => {
    queryParams$.next(convertToParamMap({ invoiceId: '2' }));

    const fixture = TestBed.createComponent(AccountsReceivablePageComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    await fixture.componentInstance['createPayment']({
      paymentDateUtc: '2026-04-03T10:00',
      paymentFormSat: '03',
      amount: 5000,
      reference: 'DEP-1',
      notes: null
    });

    expect(api.createPayment).toHaveBeenCalledWith(expect.objectContaining({
      receivedFromFiscalReceiverId: 77
    }));
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
});
