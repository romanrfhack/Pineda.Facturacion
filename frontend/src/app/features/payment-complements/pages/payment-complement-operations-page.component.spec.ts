import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { PermissionService } from '../../../core/auth/permission.service';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { AccountsReceivableApiService } from '../../accounts-receivable/infrastructure/accounts-receivable-api.service';
import { PaymentComplementsApiService } from '../infrastructure/payment-complements-api.service';
import { PaymentComplementOperationsPageComponent } from './payment-complement-operations-page.component';

describe('PaymentComplementOperationsPageComponent', () => {
  function createPayment(overrides: Record<string, unknown> = {}) {
    return {
      id: 7,
      paymentDateUtc: '2026-04-06T12:00:00Z',
      paymentFormSat: '03',
      currencyCode: 'MXN',
      amount: 1190.04,
      appliedTotal: 1190,
      remainingAmount: 0.04,
      customerCreditBalanceAmount: 0,
      reference: 'TRX-7',
      notes: null,
      receivedFromFiscalReceiverId: 7171,
      operationalStatus: 'PartiallyApplied',
      repStatus: 'PendingApplications',
      readyToPrepareRep: false,
      repBlockReason: 'Unapplied payment remainder must be explicitly assigned before preparing REP.',
      unappliedDisposition: 'PendingAllocation',
      repDocumentStatus: null,
      repReservedAmount: 0,
      repFiscalizedAmount: 0,
      applicationsCount: 1,
      linkedFiscalDocumentId: 406,
      createdAtUtc: '2026-04-06T12:00:00Z',
      updatedAtUtc: '2026-04-06T12:00:00Z',
      applications: [],
      ...overrides
    };
  }

  function createComplement() {
    return {
      id: 70,
      accountsReceivablePaymentId: 7,
      status: 'ReadyForStamping',
      providerName: null,
      cfdiVersion: '4.0',
      documentType: 'P',
      appliesToIncomePpdInvoices: true,
      eligibilitySummary: 'OK',
      issuedAtUtc: '2026-04-06T12:00:00Z',
      paymentDateUtc: '2026-04-06T12:00:00Z',
      currencyCode: 'MXN',
      totalPaymentsAmount: 1190,
      issuerProfileId: 1,
      fiscalReceiverId: 7171,
      issuerRfc: 'AAA010101AAA',
      issuerLegalName: 'Issuer',
      issuerFiscalRegimeCode: '601',
      issuerPostalCode: '01000',
      receiverRfc: 'BBB010101BBB',
      receiverLegalName: 'Receiver',
      receiverFiscalRegimeCode: '601',
      receiverPostalCode: '01000',
      receiverCountryCode: 'MEX',
      receiverForeignTaxRegistration: null,
      pacEnvironment: 'Sandbox',
      hasCertificateReference: true,
      hasPrivateKeyReference: true,
      hasPrivateKeyPasswordReference: true,
      relatedDocuments: []
    };
  }

  async function configure(paymentOverrides: Record<string, unknown> = {}) {
    const arApi = {
      getPaymentById: vi.fn().mockReturnValue(of(createPayment(paymentOverrides))),
      getPaymentComplementByPaymentId: vi.fn().mockReturnValue(of(createComplement())),
      preparePaymentComplement: vi.fn().mockReturnValue(of({
        outcome: 'Created',
        isSuccess: true,
        accountsReceivablePaymentId: 7,
        paymentComplementId: 70,
        status: 'ReadyForStamping'
      }))
    };

    const paymentComplementsApi = {
      getStamp: vi.fn().mockReturnValue(of({
        paymentComplementDocumentId: 70,
        outcome: 'Found',
        providerName: 'Facturalo',
        stampedAtUtc: '2026-04-06T12:05:00Z'
      })),
      getCancellation: vi.fn().mockReturnValue(of({
        paymentComplementDocumentId: 70,
        outcome: 'NotFound'
      }))
    };

    await TestBed.configureTestingModule({
      imports: [PaymentComplementOperationsPageComponent],
      providers: [
        { provide: AccountsReceivableApiService, useValue: arApi },
        { provide: PaymentComplementsApiService, useValue: paymentComplementsApi },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: convertToParamMap({ paymentId: '7' })
            }
          }
        },
        {
          provide: PermissionService,
          useValue: {
            canManagePayments: () => true,
            canStampFiscal: () => true
          }
        },
        {
          provide: FeedbackService,
          useValue: {
            show: vi.fn()
          }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(PaymentComplementOperationsPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    return { fixture, arApi, paymentComplementsApi };
  }

  it('skips payment-complement lookup when the payment has no prepared REP yet', async () => {
    const { arApi, paymentComplementsApi } = await configure();

    expect(arApi.getPaymentById).toHaveBeenCalledWith(7);
    expect(arApi.getPaymentComplementByPaymentId).not.toHaveBeenCalled();
    expect(paymentComplementsApi.getStamp).not.toHaveBeenCalled();
    expect(paymentComplementsApi.getCancellation).not.toHaveBeenCalled();
  });

  it('loads the persisted complement when the payment projection already points to a REP', async () => {
    const { arApi, paymentComplementsApi, fixture } = await configure({
      repDocumentStatus: 'ReadyForStamping',
      repReservedAmount: 1190
    });

    expect(arApi.getPaymentById).toHaveBeenCalledWith(7);
    expect(arApi.getPaymentComplementByPaymentId).toHaveBeenCalledWith(7);
    expect(paymentComplementsApi.getStamp).toHaveBeenCalledWith(70);
    expect(fixture.nativeElement.textContent).toContain('Evento de pago #7');
  });
});
