import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { PaymentComplementsApiService } from '../infrastructure/payment-complements-api.service';
import { PaymentComplementBaseDocumentsPageComponent } from './payment-complement-base-documents-page.component';

describe('PaymentComplementBaseDocumentsPageComponent', () => {
  function createApi(overrides?: Partial<Record<keyof PaymentComplementsApiService, unknown>>) {
    return {
      searchInternalBaseDocuments: vi.fn().mockReturnValue(of({
        page: 1,
        pageSize: 25,
        totalCount: 2,
        totalPages: 1,
        items: [
          {
            fiscalDocumentId: 501,
            billingDocumentId: 401,
            salesOrderId: 301,
            accountsReceivableInvoiceId: 201,
            fiscalStampId: 101,
            uuid: 'UUID-REP-1',
            series: 'A',
            folio: '1001',
            receiverRfc: 'BBB010101BBB',
            receiverLegalName: 'Cliente Elegible',
            issuedAtUtc: '2026-04-01T00:00:00Z',
            paymentMethodSat: 'PPD',
            paymentFormSat: '99',
            currencyCode: 'MXN',
            total: 116,
            paidTotal: 40,
            outstandingBalance: 76,
            fiscalStatus: 'Stamped',
            accountsReceivableStatus: 'PartiallyPaid',
            repOperationalStatus: 'Eligible',
            isEligible: true,
            isBlocked: false,
            eligibilityReason: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
            eligibility: {
              status: 'Eligible',
              primaryReasonCode: 'EligibleInternalRep',
              primaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
              evaluatedAtUtc: '2026-04-03T08:00:00Z',
              secondarySignals: [
                { code: 'PaymentMethodPpd', severity: 'Satisfied', message: 'Metodo de pago PPD confirmado.' }
              ]
            },
            registeredPaymentCount: 1,
            paymentComplementCount: 0,
            stampedPaymentComplementCount: 0,
            lastRepIssuedAtUtc: null,
            hasAppliedPaymentsWithoutStampedRep: true,
            hasPreparedRepPendingStamp: false,
            hasRepWithError: false,
            hasBlockedOperation: false,
            nextRecommendedAction: 'PrepareRep',
            availableActions: ['ViewDetail', 'RegisterPayment', 'PrepareRep'],
            alerts: [
              { code: 'AppliedPaymentsWithoutStampedRep', severity: 'warning', message: 'Hay pagos aplicados sin REP timbrado en este CFDI.' }
            ],
            operationalState: {
              lastEligibilityEvaluatedAtUtc: '2026-04-03T08:00:00Z',
              lastEligibilityStatus: 'Eligible',
              lastPrimaryReasonCode: 'EligibleInternalRep',
              lastPrimaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
              repPendingFlag: true,
              lastRepIssuedAtUtc: null,
              repCount: 0,
              totalPaidApplied: 40
            }
          },
          {
            fiscalDocumentId: 502,
            billingDocumentId: 402,
            salesOrderId: 302,
            accountsReceivableInvoiceId: 202,
            fiscalStampId: 102,
            uuid: 'UUID-REP-2',
            series: 'A',
            folio: '1002',
            receiverRfc: 'CCC010101CCC',
            receiverLegalName: 'Cliente Bloqueado',
            issuedAtUtc: '2026-04-02T00:00:00Z',
            paymentMethodSat: 'PPD',
            paymentFormSat: '99',
            currencyCode: 'MXN',
            total: 116,
            paidTotal: 0,
            outstandingBalance: 116,
            fiscalStatus: 'Cancelled',
            accountsReceivableStatus: 'Open',
            repOperationalStatus: 'Blocked',
            isEligible: false,
            isBlocked: true,
            eligibilityReason: 'El CFDI está cancelado.',
            eligibility: {
              status: 'Blocked',
              primaryReasonCode: 'FiscalDocumentCancelled',
              primaryReasonMessage: 'El CFDI está cancelado.',
              evaluatedAtUtc: '2026-04-03T08:05:00Z',
              secondarySignals: [
                { code: 'OutstandingBalancePositive', severity: 'Satisfied', message: 'El documento conserva saldo pendiente.' }
              ]
            },
            registeredPaymentCount: 0,
            paymentComplementCount: 0,
            stampedPaymentComplementCount: 0,
            lastRepIssuedAtUtc: null,
            hasAppliedPaymentsWithoutStampedRep: false,
            hasPreparedRepPendingStamp: false,
            hasRepWithError: false,
            hasBlockedOperation: true,
            nextRecommendedAction: null,
            availableActions: ['ViewDetail'],
            alerts: [
              { code: 'BlockedOperation', severity: 'critical', message: 'El CFDI está cancelado.' }
            ],
            operationalState: {
              lastEligibilityEvaluatedAtUtc: '2026-04-03T08:05:00Z',
              lastEligibilityStatus: 'Blocked',
              lastPrimaryReasonCode: 'FiscalDocumentCancelled',
              lastPrimaryReasonMessage: 'El CFDI está cancelado.',
              repPendingFlag: false,
              lastRepIssuedAtUtc: null,
              repCount: 0,
              totalPaidApplied: 0
            }
          }
        ]
      })),
      getInternalBaseDocumentByFiscalDocumentId: vi.fn().mockReturnValue(of({
        summary: {
          fiscalDocumentId: 501,
          billingDocumentId: 401,
          salesOrderId: 301,
          accountsReceivableInvoiceId: 201,
          fiscalStampId: 101,
          uuid: 'UUID-REP-1',
          series: 'A',
          folio: '1001',
          receiverRfc: 'BBB010101BBB',
          receiverLegalName: 'Cliente Elegible',
          issuedAtUtc: '2026-04-01T00:00:00Z',
          paymentMethodSat: 'PPD',
          paymentFormSat: '99',
          currencyCode: 'MXN',
          total: 116,
          paidTotal: 40,
          outstandingBalance: 76,
          fiscalStatus: 'Stamped',
          accountsReceivableStatus: 'PartiallyPaid',
          repOperationalStatus: 'Eligible',
          isEligible: true,
          isBlocked: false,
          eligibilityReason: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
          eligibility: {
            status: 'Eligible',
            primaryReasonCode: 'EligibleInternalRep',
            primaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
            evaluatedAtUtc: '2026-04-03T08:00:00Z',
            secondarySignals: [
              { code: 'PaymentMethodPpd', severity: 'Satisfied', message: 'Metodo de pago PPD confirmado.' },
              { code: 'OutstandingBalancePositive', severity: 'Satisfied', message: 'El documento conserva saldo pendiente.' }
            ]
          },
          registeredPaymentCount: 1,
          paymentComplementCount: 1,
          stampedPaymentComplementCount: 1,
          lastRepIssuedAtUtc: '2026-04-02T12:05:00Z',
          hasAppliedPaymentsWithoutStampedRep: false,
          hasPreparedRepPendingStamp: false,
          hasRepWithError: false,
          hasBlockedOperation: false,
          nextRecommendedAction: 'RefreshRepStatus',
          availableActions: ['ViewDetail', 'RefreshRepStatus', 'CancelRep'],
          alerts: [
            { code: 'StampedRepAvailable', severity: 'info', message: 'El CFDI ya cuenta con REP timbrado y sólo requiere seguimiento o refresh de estatus.' }
          ],
          operationalState: {
            lastEligibilityEvaluatedAtUtc: '2026-04-03T08:00:00Z',
            lastEligibilityStatus: 'Eligible',
            lastPrimaryReasonCode: 'EligibleInternalRep',
            lastPrimaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
            repPendingFlag: true,
            lastRepIssuedAtUtc: '2026-04-02T12:05:00Z',
            repCount: 1,
            totalPaidApplied: 40
          }
        },
        operationalState: {
          lastEligibilityEvaluatedAtUtc: '2026-04-03T08:00:00Z',
          lastEligibilityStatus: 'Eligible',
          lastPrimaryReasonCode: 'EligibleInternalRep',
          lastPrimaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
          repPendingFlag: true,
          lastRepIssuedAtUtc: '2026-04-02T12:05:00Z',
          repCount: 1,
          totalPaidApplied: 40
        },
        paymentHistory: [
          {
            accountsReceivablePaymentId: 9001,
            paymentDateUtc: '2026-04-02T10:00:00Z',
            paymentFormSat: '03',
            paymentAmount: 40,
            amountAppliedToDocument: 40,
            remainingPaymentAmount: 0,
            reference: 'TRX-1',
            notes: 'Pago parcial',
            paymentComplementId: 7001,
            paymentComplementStatus: 'Stamped',
            paymentComplementUuid: 'UUID-PC-1',
            createdAtUtc: '2026-04-02T10:00:00Z'
          }
        ],
        paymentApplications: [
          {
            accountsReceivablePaymentId: 9001,
            applicationSequence: 1,
            paymentDateUtc: '2026-04-02T10:00:00Z',
            paymentFormSat: '03',
            appliedAmount: 40,
            previousBalance: 116,
            newBalance: 76,
            reference: 'TRX-1',
            notes: 'Pago parcial',
            paymentAmount: 40,
            remainingPaymentAmount: 0,
            createdAtUtc: '2026-04-02T10:00:00Z'
          }
        ],
        issuedReps: [
          {
            paymentComplementId: 7001,
            accountsReceivablePaymentId: 9001,
            status: 'Stamped',
            uuid: 'UUID-PC-1',
            paymentDateUtc: '2026-04-02T10:00:00Z',
            issuedAtUtc: '2026-04-02T12:00:00Z',
            stampedAtUtc: '2026-04-02T12:05:00Z',
            cancelledAtUtc: null,
            providerName: 'FacturaloPlus',
            installmentNumber: 1,
            previousBalance: 116,
            paidAmount: 40,
            remainingBalance: 76
          }
        ]
      })),
      registerInternalBaseDocumentPayment: vi.fn().mockReturnValue(of({
        outcome: 'RegisteredAndApplied',
        isSuccess: true,
        warningMessages: [],
        fiscalDocumentId: 501,
        accountsReceivableInvoiceId: 201,
        accountsReceivablePaymentId: 9002,
        appliedAmount: 36,
        remainingBalance: 40,
        remainingPaymentAmount: 0,
        repOperationalStatus: 'Eligible',
        isEligible: true,
        isBlocked: false,
        eligibilityReason: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
        operationalState: {
          lastEligibilityEvaluatedAtUtc: '2026-04-03T09:00:00Z',
          lastEligibilityStatus: 'Eligible',
          lastPrimaryReasonCode: 'EligibleInternalRep',
          lastPrimaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
          repPendingFlag: true,
          lastRepIssuedAtUtc: '2026-04-02T12:05:00Z',
          repCount: 1,
          totalPaidApplied: 76
        },
        applications: [
          {
            applicationId: 9101,
            accountsReceivablePaymentId: 9002,
            accountsReceivableInvoiceId: 201,
            applicationSequence: 2,
            appliedAmount: 36,
            previousBalance: 76,
            newBalance: 40
          }
        ]
      })),
      prepareInternalBaseDocumentPaymentComplement: vi.fn().mockReturnValue(of({
        outcome: 'Prepared',
        isSuccess: true,
        errorMessage: null,
        warningMessages: [],
        fiscalDocumentId: 501,
        accountsReceivablePaymentId: 9002,
        paymentComplementDocumentId: 7002,
        status: 'ReadyForStamping',
        relatedDocumentCount: 1,
        operationalState: {
          lastEligibilityEvaluatedAtUtc: '2026-04-03T09:10:00Z',
          lastEligibilityStatus: 'Eligible',
          lastPrimaryReasonCode: 'EligibleInternalRep',
          lastPrimaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
          repPendingFlag: true,
          lastRepIssuedAtUtc: '2026-04-02T12:05:00Z',
          repCount: 1,
          totalPaidApplied: 76
        }
      })),
      stampInternalBaseDocumentPaymentComplement: vi.fn().mockReturnValue(of({
        outcome: 'Stamped',
        isSuccess: true,
        errorMessage: null,
        warningMessages: [],
        fiscalDocumentId: 501,
        accountsReceivablePaymentId: 9002,
        paymentComplementDocumentId: 7002,
        status: 'Stamped',
        paymentComplementStampId: 7102,
        stampUuid: 'UUID-PC-2',
        stampedAtUtc: '2026-04-03T09:15:00Z',
        xmlAvailable: true,
        operationalState: {
          lastEligibilityEvaluatedAtUtc: '2026-04-03T09:15:00Z',
          lastEligibilityStatus: 'Eligible',
          lastPrimaryReasonCode: 'EligibleInternalRep',
          lastPrimaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
          repPendingFlag: true,
          lastRepIssuedAtUtc: '2026-04-03T09:15:00Z',
          repCount: 2,
          totalPaidApplied: 76
        }
      })),
      refreshInternalBaseDocumentPaymentComplementStatus: vi.fn().mockReturnValue(of({
        outcome: 'Refreshed',
        isSuccess: true,
        fiscalDocumentId: 501,
        paymentComplementDocumentId: 7001,
        paymentComplementStatus: 'Stamped',
        lastKnownExternalStatus: 'VIGENTE',
        checkedAtUtc: '2026-04-03T10:00:00Z',
        nextRecommendedAction: 'RefreshRepStatus',
        availableActions: ['ViewDetail', 'RefreshRepStatus', 'CancelRep'],
        alerts: [{ code: 'StampedRepAvailable', severity: 'info', message: 'El CFDI ya cuenta con REP timbrado y sólo requiere seguimiento o refresh de estatus.' }]
      })),
      cancelInternalBaseDocumentPaymentComplement: vi.fn().mockReturnValue(of({
        outcome: 'Cancelled',
        isSuccess: true,
        fiscalDocumentId: 501,
        paymentComplementDocumentId: 7001,
        paymentComplementStatus: 'Cancelled',
        paymentComplementCancellationId: 7101,
        cancellationStatus: 'Cancelled',
        nextRecommendedAction: null,
        availableActions: ['ViewDetail'],
        alerts: []
      })),
      ...overrides
    };
  }

  async function configure(apiOverrides?: Partial<Record<keyof PaymentComplementsApiService, unknown>>) {
    await TestBed.configureTestingModule({
      imports: [PaymentComplementBaseDocumentsPageComponent],
      providers: [
        { provide: PaymentComplementsApiService, useValue: createApi(apiOverrides) },
        { provide: FeedbackService, useValue: { show: vi.fn() } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(PaymentComplementBaseDocumentsPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('loads and renders the internal REP tray', async () => {
    const fixture = await configure();

    expect(fixture.nativeElement.textContent).toContain('UUID-REP-1');
    expect(fixture.nativeElement.textContent).toContain('Cliente Elegible');
    expect(fixture.nativeElement.textContent).toContain('Elegible');
    expect(fixture.nativeElement.textContent).toContain('Bloqueado');
    expect(fixture.nativeElement.textContent).toContain('Registrar pago');
    expect(fixture.nativeElement.textContent).toContain('timbrar');
  });

  it('applies filters through the internal base-document search endpoint', async () => {
    const searchInternalBaseDocuments = vi.fn().mockReturnValue(of({
      page: 1,
      pageSize: 25,
      totalCount: 0,
      totalPages: 0,
      items: []
    }));
    const fixture = await configure({ searchInternalBaseDocuments });

    fixture.componentInstance['receiverRfc'] = 'BBB010101BBB';
    fixture.componentInstance['query'] = 'UUID-REP-1';
    fixture.componentInstance['eligibleFilter'] = 'true';
    await fixture.componentInstance['applyFilters']();

    expect(searchInternalBaseDocuments).toHaveBeenCalledWith(expect.objectContaining({
      page: 1,
      pageSize: 25,
      receiverRfc: 'BBB010101BBB',
      query: 'UUID-REP-1',
      eligible: true
    }));
  });

  it('opens the detail modal with payment and REP context', async () => {
    const fixture = await configure();

    await fixture.componentInstance['openDetailModal'](fixture.componentInstance['items']()[0]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Contexto del CFDI base');
    expect(fixture.nativeElement.textContent).toContain('Explicación de elegibilidad');
    expect(fixture.nativeElement.textContent).toContain('Snapshot operativo persistido');
    expect(fixture.nativeElement.textContent).toContain('Historial de pagos registrados');
    expect(fixture.nativeElement.textContent).toContain('Aplicaciones de pago');
    expect(fixture.nativeElement.textContent).toContain('REP emitidos y relacionados');
    expect(fixture.nativeElement.textContent).toContain('9001');
    expect(fixture.nativeElement.textContent).toContain('UUID-PC-1');
    expect(fixture.nativeElement.textContent).toContain('EligibleInternalRep');
    expect(fixture.nativeElement.textContent).toContain('FacturaloPlus');
  });

  it('renders operational alerts and refresh/cancel actions in detail', async () => {
    const refreshInternalBaseDocumentPaymentComplementStatus = vi.fn().mockReturnValue(of({
      outcome: 'Refreshed',
      isSuccess: true,
      fiscalDocumentId: 501,
      paymentComplementDocumentId: 7001,
      paymentComplementStatus: 'Stamped',
      lastKnownExternalStatus: 'VIGENTE',
      nextRecommendedAction: 'RefreshRepStatus',
      availableActions: ['ViewDetail', 'RefreshRepStatus', 'CancelRep'],
      alerts: []
    }));
    const cancelInternalBaseDocumentPaymentComplement = vi.fn().mockReturnValue(of({
      outcome: 'Cancelled',
      isSuccess: true,
      fiscalDocumentId: 501,
      paymentComplementDocumentId: 7001,
      paymentComplementStatus: 'Cancelled',
      cancellationStatus: 'Cancelled',
      availableActions: ['ViewDetail'],
      alerts: []
    }));

    const fixture = await configure({
      refreshInternalBaseDocumentPaymentComplementStatus,
      cancelInternalBaseDocumentPaymentComplement
    });

    await fixture.componentInstance['openDetailModal'](fixture.componentInstance['items']()[0]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Seguimiento operativo');
    expect(fixture.nativeElement.textContent).toContain('REP timbrado disponible');
    expect(fixture.nativeElement.textContent).toContain('Refrescar');
    expect(fixture.nativeElement.textContent).toContain('Cancelar');

    const complement = fixture.componentInstance['selectedDetail']()!.issuedReps[0];
    await fixture.componentInstance['refreshPaymentComplement'](complement);
    await fixture.componentInstance['cancelPaymentComplement'](complement);

    expect(refreshInternalBaseDocumentPaymentComplementStatus).toHaveBeenCalledWith(501, { paymentComplementDocumentId: 7001 });
    expect(cancelInternalBaseDocumentPaymentComplement).toHaveBeenCalledWith(501, {
      paymentComplementDocumentId: 7001,
      cancellationReasonCode: '02',
      replacementUuid: null
    });
  });

  it('renders the payment form from the base-document context and submits successfully', async () => {
    const getInternalBaseDocumentByFiscalDocumentId = vi.fn()
      .mockReturnValueOnce(of({
        summary: {
          fiscalDocumentId: 501,
          billingDocumentId: 401,
          salesOrderId: 301,
          accountsReceivableInvoiceId: 201,
          fiscalStampId: 101,
          uuid: 'UUID-REP-1',
          series: 'A',
          folio: '1001',
          receiverRfc: 'BBB010101BBB',
          receiverLegalName: 'Cliente Elegible',
          issuedAtUtc: '2026-04-01T00:00:00Z',
          paymentMethodSat: 'PPD',
          paymentFormSat: '99',
          currencyCode: 'MXN',
          total: 116,
          paidTotal: 40,
          outstandingBalance: 76,
          fiscalStatus: 'Stamped',
          accountsReceivableStatus: 'PartiallyPaid',
          repOperationalStatus: 'Eligible',
          isEligible: true,
          isBlocked: false,
          eligibilityReason: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
          eligibility: {
            status: 'Eligible',
            primaryReasonCode: 'EligibleInternalRep',
            primaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
            evaluatedAtUtc: '2026-04-03T08:00:00Z',
            secondarySignals: []
          },
          registeredPaymentCount: 1,
          paymentComplementCount: 1,
          stampedPaymentComplementCount: 1,
          lastRepIssuedAtUtc: '2026-04-02T12:05:00Z',
          operationalState: {
            lastEligibilityEvaluatedAtUtc: '2026-04-03T08:00:00Z',
            lastEligibilityStatus: 'Eligible',
            lastPrimaryReasonCode: 'EligibleInternalRep',
            lastPrimaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
            repPendingFlag: true,
            lastRepIssuedAtUtc: '2026-04-02T12:05:00Z',
            repCount: 1,
            totalPaidApplied: 40
          }
        },
        operationalState: {
          lastEligibilityEvaluatedAtUtc: '2026-04-03T08:00:00Z',
          lastEligibilityStatus: 'Eligible',
          lastPrimaryReasonCode: 'EligibleInternalRep',
          lastPrimaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
          repPendingFlag: true,
          lastRepIssuedAtUtc: '2026-04-02T12:05:00Z',
          repCount: 1,
          totalPaidApplied: 40
        },
        paymentHistory: [],
        paymentApplications: [],
        issuedReps: []
      }))
      .mockReturnValueOnce(of({
        summary: {
          fiscalDocumentId: 501,
          billingDocumentId: 401,
          salesOrderId: 301,
          accountsReceivableInvoiceId: 201,
          fiscalStampId: 101,
          uuid: 'UUID-REP-1',
          series: 'A',
          folio: '1001',
          receiverRfc: 'BBB010101BBB',
          receiverLegalName: 'Cliente Elegible',
          issuedAtUtc: '2026-04-01T00:00:00Z',
          paymentMethodSat: 'PPD',
          paymentFormSat: '99',
          currencyCode: 'MXN',
          total: 116,
          paidTotal: 76,
          outstandingBalance: 40,
          fiscalStatus: 'Stamped',
          accountsReceivableStatus: 'PartiallyPaid',
          repOperationalStatus: 'Eligible',
          isEligible: true,
          isBlocked: false,
          eligibilityReason: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
          eligibility: {
            status: 'Eligible',
            primaryReasonCode: 'EligibleInternalRep',
            primaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
            evaluatedAtUtc: '2026-04-03T09:00:00Z',
            secondarySignals: []
          },
          registeredPaymentCount: 2,
          paymentComplementCount: 1,
          stampedPaymentComplementCount: 1,
          lastRepIssuedAtUtc: '2026-04-02T12:05:00Z',
          operationalState: {
            lastEligibilityEvaluatedAtUtc: '2026-04-03T09:00:00Z',
            lastEligibilityStatus: 'Eligible',
            lastPrimaryReasonCode: 'EligibleInternalRep',
            lastPrimaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
            repPendingFlag: true,
            lastRepIssuedAtUtc: '2026-04-02T12:05:00Z',
            repCount: 1,
            totalPaidApplied: 76
          }
        },
        operationalState: {
          lastEligibilityEvaluatedAtUtc: '2026-04-03T09:00:00Z',
          lastEligibilityStatus: 'Eligible',
          lastPrimaryReasonCode: 'EligibleInternalRep',
          lastPrimaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
          repPendingFlag: true,
          lastRepIssuedAtUtc: '2026-04-02T12:05:00Z',
          repCount: 1,
          totalPaidApplied: 76
        },
        paymentHistory: [
          {
            accountsReceivablePaymentId: 9002,
            paymentDateUtc: '2026-04-03T00:00:00Z',
            paymentFormSat: '03',
            paymentAmount: 36,
            amountAppliedToDocument: 36,
            remainingPaymentAmount: 0,
            reference: 'TRANS-123',
            notes: 'Pago parcial',
            paymentComplementId: null,
            paymentComplementStatus: null,
            paymentComplementUuid: null,
            createdAtUtc: '2026-04-03T00:00:00Z'
          }
        ],
        paymentApplications: [
          {
            accountsReceivablePaymentId: 9002,
            applicationSequence: 2,
            paymentDateUtc: '2026-04-03T00:00:00Z',
            paymentFormSat: '03',
            appliedAmount: 36,
            previousBalance: 76,
            newBalance: 40,
            reference: 'TRANS-123',
            notes: 'Pago parcial',
            paymentAmount: 36,
            remainingPaymentAmount: 0,
            createdAtUtc: '2026-04-03T00:00:00Z'
          }
        ],
        issuedReps: []
      }));
    const registerInternalBaseDocumentPayment = vi.fn().mockReturnValue(of({
      outcome: 'RegisteredAndApplied',
      isSuccess: true,
      warningMessages: [],
      fiscalDocumentId: 501,
      accountsReceivableInvoiceId: 201,
      accountsReceivablePaymentId: 9002,
      appliedAmount: 36,
      remainingBalance: 40,
      remainingPaymentAmount: 0,
      applications: [
        {
          applicationId: 9101,
          accountsReceivablePaymentId: 9002,
          accountsReceivableInvoiceId: 201,
          applicationSequence: 2,
          appliedAmount: 36,
          previousBalance: 76,
          newBalance: 40
        }
      ]
    }));
    const fixture = await configure({
      getInternalBaseDocumentByFiscalDocumentId,
      registerInternalBaseDocumentPayment
    });

    await fixture.componentInstance['openDetailModal'](fixture.componentInstance['items']()[0], true);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Registro de pago');
    expect(fixture.nativeElement.textContent).toContain('Fecha de pago');

    fixture.componentInstance['paymentDate'] = '2026-04-03';
    fixture.componentInstance['paymentFormSat'] = '03';
    fixture.componentInstance['paymentAmount'] = 36;
    fixture.componentInstance['paymentReference'] = 'TRANS-123';
    fixture.componentInstance['paymentNotes'] = 'Pago parcial';
    await fixture.componentInstance['submitRegisterPayment']();
    fixture.detectChanges();

    expect(registerInternalBaseDocumentPayment).toHaveBeenCalledWith(501, {
      paymentDate: '2026-04-03',
      paymentFormSat: '03',
      amount: 36,
      reference: 'TRANS-123',
      notes: 'Pago parcial'
    });
    expect(getInternalBaseDocumentByFiscalDocumentId).toHaveBeenCalledTimes(2);
    expect(fixture.nativeElement.textContent).toContain('40.00');
    expect(fixture.nativeElement.textContent).toContain('TRANS-123');
  });

  it('renders validation errors returned by payment registration', async () => {
    const registerInternalBaseDocumentPayment = vi.fn().mockReturnValue(throwError(() => ({
      error: { errorMessage: 'El monto del pago no puede exceder el saldo pendiente del CFDI.' }
    })));
    const fixture = await configure({ registerInternalBaseDocumentPayment });

    await fixture.componentInstance['openDetailModal'](fixture.componentInstance['items']()[0], true);
    fixture.componentInstance['paymentDate'] = '2026-04-03';
    fixture.componentInstance['paymentFormSat'] = '03';
    fixture.componentInstance['paymentAmount'] = 999;
    await fixture.componentInstance['submitRegisterPayment']();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('El monto del pago no puede exceder el saldo pendiente del CFDI.');
  });

  it('prepares a REP from a payment row and refreshes the detail', async () => {
    const getInternalBaseDocumentByFiscalDocumentId = vi.fn()
      .mockReturnValueOnce(of({
        summary: {
          fiscalDocumentId: 501,
          billingDocumentId: 401,
          salesOrderId: 301,
          accountsReceivableInvoiceId: 201,
          fiscalStampId: 101,
          uuid: 'UUID-REP-1',
          series: 'A',
          folio: '1001',
          receiverRfc: 'BBB010101BBB',
          receiverLegalName: 'Cliente Elegible',
          issuedAtUtc: '2026-04-01T00:00:00Z',
          paymentMethodSat: 'PPD',
          paymentFormSat: '99',
          currencyCode: 'MXN',
          total: 116,
          paidTotal: 76,
          outstandingBalance: 40,
          fiscalStatus: 'Stamped',
          accountsReceivableStatus: 'PartiallyPaid',
          repOperationalStatus: 'Eligible',
          isEligible: true,
          isBlocked: false,
          eligibilityReason: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
          eligibility: {
            status: 'Eligible',
            primaryReasonCode: 'EligibleInternalRep',
            primaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
            evaluatedAtUtc: '2026-04-03T09:05:00Z',
            secondarySignals: []
          },
          registeredPaymentCount: 2,
          paymentComplementCount: 1,
          stampedPaymentComplementCount: 1,
          lastRepIssuedAtUtc: '2026-04-02T12:05:00Z',
          operationalState: {
            lastEligibilityEvaluatedAtUtc: '2026-04-03T09:05:00Z',
            lastEligibilityStatus: 'Eligible',
            lastPrimaryReasonCode: 'EligibleInternalRep',
            lastPrimaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
            repPendingFlag: true,
            lastRepIssuedAtUtc: '2026-04-02T12:05:00Z',
            repCount: 1,
            totalPaidApplied: 76
          }
        },
        operationalState: {
          lastEligibilityEvaluatedAtUtc: '2026-04-03T09:05:00Z',
          lastEligibilityStatus: 'Eligible',
          lastPrimaryReasonCode: 'EligibleInternalRep',
          lastPrimaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
          repPendingFlag: true,
          lastRepIssuedAtUtc: '2026-04-02T12:05:00Z',
          repCount: 1,
          totalPaidApplied: 76
        },
        paymentHistory: [
          {
            accountsReceivablePaymentId: 9002,
            paymentDateUtc: '2026-04-03T00:00:00Z',
            paymentFormSat: '03',
            paymentAmount: 36,
            amountAppliedToDocument: 36,
            remainingPaymentAmount: 0,
            reference: 'TRANS-123',
            notes: 'Pago parcial',
            paymentComplementId: null,
            paymentComplementStatus: null,
            paymentComplementUuid: null,
            createdAtUtc: '2026-04-03T00:00:00Z'
          }
        ],
        paymentApplications: [],
        issuedReps: []
      }))
      .mockReturnValueOnce(of({
        summary: {
          fiscalDocumentId: 501,
          billingDocumentId: 401,
          salesOrderId: 301,
          accountsReceivableInvoiceId: 201,
          fiscalStampId: 101,
          uuid: 'UUID-REP-1',
          series: 'A',
          folio: '1001',
          receiverRfc: 'BBB010101BBB',
          receiverLegalName: 'Cliente Elegible',
          issuedAtUtc: '2026-04-01T00:00:00Z',
          paymentMethodSat: 'PPD',
          paymentFormSat: '99',
          currencyCode: 'MXN',
          total: 116,
          paidTotal: 76,
          outstandingBalance: 40,
          fiscalStatus: 'Stamped',
          accountsReceivableStatus: 'PartiallyPaid',
          repOperationalStatus: 'Eligible',
          isEligible: true,
          isBlocked: false,
          eligibilityReason: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
          eligibility: {
            status: 'Eligible',
            primaryReasonCode: 'EligibleInternalRep',
            primaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
            evaluatedAtUtc: '2026-04-03T09:10:00Z',
            secondarySignals: []
          },
          registeredPaymentCount: 2,
          paymentComplementCount: 2,
          stampedPaymentComplementCount: 1,
          lastRepIssuedAtUtc: '2026-04-02T12:05:00Z',
          operationalState: {
            lastEligibilityEvaluatedAtUtc: '2026-04-03T09:10:00Z',
            lastEligibilityStatus: 'Eligible',
            lastPrimaryReasonCode: 'EligibleInternalRep',
            lastPrimaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
            repPendingFlag: true,
            lastRepIssuedAtUtc: '2026-04-02T12:05:00Z',
            repCount: 1,
            totalPaidApplied: 76
          }
        },
        operationalState: {
          lastEligibilityEvaluatedAtUtc: '2026-04-03T09:10:00Z',
          lastEligibilityStatus: 'Eligible',
          lastPrimaryReasonCode: 'EligibleInternalRep',
          lastPrimaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
          repPendingFlag: true,
          lastRepIssuedAtUtc: '2026-04-02T12:05:00Z',
          repCount: 1,
          totalPaidApplied: 76
        },
        paymentHistory: [
          {
            accountsReceivablePaymentId: 9002,
            paymentDateUtc: '2026-04-03T00:00:00Z',
            paymentFormSat: '03',
            paymentAmount: 36,
            amountAppliedToDocument: 36,
            remainingPaymentAmount: 0,
            reference: 'TRANS-123',
            notes: 'Pago parcial',
            paymentComplementId: 7002,
            paymentComplementStatus: 'ReadyForStamping',
            paymentComplementUuid: null,
            createdAtUtc: '2026-04-03T00:00:00Z'
          }
        ],
        paymentApplications: [],
        issuedReps: [
          {
            paymentComplementId: 7002,
            accountsReceivablePaymentId: 9002,
            status: 'ReadyForStamping',
            uuid: null,
            paymentDateUtc: '2026-04-03T00:00:00Z',
            issuedAtUtc: '2026-04-03T09:10:00Z',
            stampedAtUtc: null,
            cancelledAtUtc: null,
            providerName: null,
            installmentNumber: 2,
            previousBalance: 76,
            paidAmount: 36,
            remainingBalance: 40
          }
        ]
      }));
    const prepareInternalBaseDocumentPaymentComplement = vi.fn().mockReturnValue(of({
      outcome: 'Prepared',
      isSuccess: true,
      errorMessage: null,
      warningMessages: [],
      fiscalDocumentId: 501,
      accountsReceivablePaymentId: 9002,
      paymentComplementDocumentId: 7002,
      status: 'ReadyForStamping',
      relatedDocumentCount: 1,
      operationalState: null
    }));
    const fixture = await configure({
      getInternalBaseDocumentByFiscalDocumentId,
      prepareInternalBaseDocumentPaymentComplement
    });

    await fixture.componentInstance['openDetailModal'](fixture.componentInstance['items']()[0]);
    fixture.detectChanges();

    await fixture.componentInstance['preparePaymentComplement'](fixture.componentInstance['selectedDetail']()!.paymentHistory[0]);
    fixture.detectChanges();

    expect(prepareInternalBaseDocumentPaymentComplement).toHaveBeenCalledWith(501, {
      accountsReceivablePaymentId: 9002
    });
    expect(getInternalBaseDocumentByFiscalDocumentId).toHaveBeenCalledTimes(2);
    expect(fixture.nativeElement.textContent).toContain('Listo para timbrar');
  });

  it('stamps a prepared REP from the base-document context and refreshes the detail', async () => {
    const getInternalBaseDocumentByFiscalDocumentId = vi.fn()
      .mockReturnValueOnce(of({
        summary: {
          fiscalDocumentId: 501,
          billingDocumentId: 401,
          salesOrderId: 301,
          accountsReceivableInvoiceId: 201,
          fiscalStampId: 101,
          uuid: 'UUID-REP-1',
          series: 'A',
          folio: '1001',
          receiverRfc: 'BBB010101BBB',
          receiverLegalName: 'Cliente Elegible',
          issuedAtUtc: '2026-04-01T00:00:00Z',
          paymentMethodSat: 'PPD',
          paymentFormSat: '99',
          currencyCode: 'MXN',
          total: 116,
          paidTotal: 76,
          outstandingBalance: 40,
          fiscalStatus: 'Stamped',
          accountsReceivableStatus: 'PartiallyPaid',
          repOperationalStatus: 'Eligible',
          isEligible: true,
          isBlocked: false,
          eligibilityReason: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
          eligibility: {
            status: 'Eligible',
            primaryReasonCode: 'EligibleInternalRep',
            primaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
            evaluatedAtUtc: '2026-04-03T09:10:00Z',
            secondarySignals: []
          },
          registeredPaymentCount: 2,
          paymentComplementCount: 2,
          stampedPaymentComplementCount: 1,
          lastRepIssuedAtUtc: '2026-04-02T12:05:00Z',
          operationalState: {
            lastEligibilityEvaluatedAtUtc: '2026-04-03T09:10:00Z',
            lastEligibilityStatus: 'Eligible',
            lastPrimaryReasonCode: 'EligibleInternalRep',
            lastPrimaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
            repPendingFlag: true,
            lastRepIssuedAtUtc: '2026-04-02T12:05:00Z',
            repCount: 1,
            totalPaidApplied: 76
          }
        },
        operationalState: {
          lastEligibilityEvaluatedAtUtc: '2026-04-03T09:10:00Z',
          lastEligibilityStatus: 'Eligible',
          lastPrimaryReasonCode: 'EligibleInternalRep',
          lastPrimaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
          repPendingFlag: true,
          lastRepIssuedAtUtc: '2026-04-02T12:05:00Z',
          repCount: 1,
          totalPaidApplied: 76
        },
        paymentHistory: [
          {
            accountsReceivablePaymentId: 9002,
            paymentDateUtc: '2026-04-03T00:00:00Z',
            paymentFormSat: '03',
            paymentAmount: 36,
            amountAppliedToDocument: 36,
            remainingPaymentAmount: 0,
            reference: 'TRANS-123',
            notes: 'Pago parcial',
            paymentComplementId: 7002,
            paymentComplementStatus: 'ReadyForStamping',
            paymentComplementUuid: null,
            createdAtUtc: '2026-04-03T00:00:00Z'
          }
        ],
        paymentApplications: [],
        issuedReps: [
          {
            paymentComplementId: 7002,
            accountsReceivablePaymentId: 9002,
            status: 'ReadyForStamping',
            uuid: null,
            paymentDateUtc: '2026-04-03T00:00:00Z',
            issuedAtUtc: '2026-04-03T09:10:00Z',
            stampedAtUtc: null,
            cancelledAtUtc: null,
            providerName: null,
            installmentNumber: 2,
            previousBalance: 76,
            paidAmount: 36,
            remainingBalance: 40
          }
        ]
      }))
      .mockReturnValueOnce(of({
        summary: {
          fiscalDocumentId: 501,
          billingDocumentId: 401,
          salesOrderId: 301,
          accountsReceivableInvoiceId: 201,
          fiscalStampId: 101,
          uuid: 'UUID-REP-1',
          series: 'A',
          folio: '1001',
          receiverRfc: 'BBB010101BBB',
          receiverLegalName: 'Cliente Elegible',
          issuedAtUtc: '2026-04-01T00:00:00Z',
          paymentMethodSat: 'PPD',
          paymentFormSat: '99',
          currencyCode: 'MXN',
          total: 116,
          paidTotal: 76,
          outstandingBalance: 40,
          fiscalStatus: 'Stamped',
          accountsReceivableStatus: 'PartiallyPaid',
          repOperationalStatus: 'Eligible',
          isEligible: true,
          isBlocked: false,
          eligibilityReason: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
          eligibility: {
            status: 'Eligible',
            primaryReasonCode: 'EligibleInternalRep',
            primaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
            evaluatedAtUtc: '2026-04-03T09:15:00Z',
            secondarySignals: []
          },
          registeredPaymentCount: 2,
          paymentComplementCount: 2,
          stampedPaymentComplementCount: 2,
          lastRepIssuedAtUtc: '2026-04-03T09:15:00Z',
          operationalState: {
            lastEligibilityEvaluatedAtUtc: '2026-04-03T09:15:00Z',
            lastEligibilityStatus: 'Eligible',
            lastPrimaryReasonCode: 'EligibleInternalRep',
            lastPrimaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
            repPendingFlag: true,
            lastRepIssuedAtUtc: '2026-04-03T09:15:00Z',
            repCount: 2,
            totalPaidApplied: 76
          }
        },
        operationalState: {
          lastEligibilityEvaluatedAtUtc: '2026-04-03T09:15:00Z',
          lastEligibilityStatus: 'Eligible',
          lastPrimaryReasonCode: 'EligibleInternalRep',
          lastPrimaryReasonMessage: 'CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.',
          repPendingFlag: true,
          lastRepIssuedAtUtc: '2026-04-03T09:15:00Z',
          repCount: 2,
          totalPaidApplied: 76
        },
        paymentHistory: [
          {
            accountsReceivablePaymentId: 9002,
            paymentDateUtc: '2026-04-03T00:00:00Z',
            paymentFormSat: '03',
            paymentAmount: 36,
            amountAppliedToDocument: 36,
            remainingPaymentAmount: 0,
            reference: 'TRANS-123',
            notes: 'Pago parcial',
            paymentComplementId: 7002,
            paymentComplementStatus: 'Stamped',
            paymentComplementUuid: 'UUID-PC-2',
            createdAtUtc: '2026-04-03T00:00:00Z'
          }
        ],
        paymentApplications: [],
        issuedReps: [
          {
            paymentComplementId: 7002,
            accountsReceivablePaymentId: 9002,
            status: 'Stamped',
            uuid: 'UUID-PC-2',
            paymentDateUtc: '2026-04-03T00:00:00Z',
            issuedAtUtc: '2026-04-03T09:10:00Z',
            stampedAtUtc: '2026-04-03T09:15:00Z',
            cancelledAtUtc: null,
            providerName: 'FacturaloPlus',
            installmentNumber: 2,
            previousBalance: 76,
            paidAmount: 36,
            remainingBalance: 40
          }
        ]
      }));
    const stampInternalBaseDocumentPaymentComplement = vi.fn().mockReturnValue(of({
      outcome: 'Stamped',
      isSuccess: true,
      errorMessage: null,
      warningMessages: [],
      fiscalDocumentId: 501,
      accountsReceivablePaymentId: 9002,
      paymentComplementDocumentId: 7002,
      status: 'Stamped',
      paymentComplementStampId: 7102,
      stampUuid: 'UUID-PC-2',
      stampedAtUtc: '2026-04-03T09:15:00Z',
      xmlAvailable: true,
      operationalState: null
    }));
    const fixture = await configure({
      getInternalBaseDocumentByFiscalDocumentId,
      stampInternalBaseDocumentPaymentComplement
    });

    await fixture.componentInstance['openDetailModal'](fixture.componentInstance['items']()[0]);
    fixture.detectChanges();

    await fixture.componentInstance['stampPaymentComplement'](fixture.componentInstance['selectedDetail']()!.issuedReps[0]);
    fixture.detectChanges();

    expect(stampInternalBaseDocumentPaymentComplement).toHaveBeenCalledWith(501, {
      paymentComplementDocumentId: 7002,
      retryRejected: false
    });
    expect(getInternalBaseDocumentByFiscalDocumentId).toHaveBeenCalledTimes(2);
    expect(fixture.nativeElement.textContent).toContain('UUID-PC-2');
    expect(fixture.nativeElement.textContent).toContain('FacturaloPlus');
  });

  it('renders validation errors returned by REP preparation and stamping', async () => {
    const prepareInternalBaseDocumentPaymentComplement = vi.fn().mockReturnValue(throwError(() => ({
      error: { errorMessage: 'No existe un pago aplicado elegible para preparar REP en este CFDI.' }
    })));
    const stampInternalBaseDocumentPaymentComplement = vi.fn().mockReturnValue(throwError(() => ({
      error: { errorMessage: 'No existe un REP preparado elegible para timbrar en este CFDI.' }
    })));
    const fixture = await configure({
      prepareInternalBaseDocumentPaymentComplement,
      stampInternalBaseDocumentPaymentComplement
    });

    await fixture.componentInstance['openDetailModal'](fixture.componentInstance['items']()[0]);
    fixture.componentInstance['selectedDetail']()!.paymentHistory[0].paymentComplementId = null;
    await fixture.componentInstance['preparePaymentComplement'](fixture.componentInstance['selectedDetail']()!.paymentHistory[0]);
    fixture.componentInstance['selectedDetail']()!.issuedReps[0].status = 'ReadyForStamping';
    await fixture.componentInstance['stampPaymentComplement'](fixture.componentInstance['selectedDetail']()!.issuedReps[0]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No existe un REP preparado elegible para timbrar en este CFDI.');
  });

  it('renders explicit eligibility explanation for blocked documents in the tray', async () => {
    const fixture = await configure();

    expect(fixture.nativeElement.textContent).toContain('El CFDI está cancelado.');
  });

  it('renders empty history states in the detail modal', async () => {
    const fixture = await configure({
      getInternalBaseDocumentByFiscalDocumentId: vi.fn().mockReturnValue(of({
        summary: {
          fiscalDocumentId: 502,
          billingDocumentId: 402,
          salesOrderId: 302,
          accountsReceivableInvoiceId: 202,
          fiscalStampId: 102,
          uuid: 'UUID-REP-2',
          series: 'A',
          folio: '1002',
          receiverRfc: 'CCC010101CCC',
          receiverLegalName: 'Cliente Bloqueado',
          issuedAtUtc: '2026-04-02T00:00:00Z',
          paymentMethodSat: 'PPD',
          paymentFormSat: '99',
          currencyCode: 'MXN',
          total: 116,
          paidTotal: 0,
          outstandingBalance: 116,
          fiscalStatus: 'Cancelled',
          accountsReceivableStatus: 'Open',
          repOperationalStatus: 'Blocked',
          isEligible: false,
          isBlocked: true,
          eligibilityReason: 'El CFDI está cancelado.',
          eligibility: {
            status: 'Blocked',
            primaryReasonCode: 'FiscalDocumentCancelled',
            primaryReasonMessage: 'El CFDI está cancelado.',
            evaluatedAtUtc: '2026-04-03T08:05:00Z',
            secondarySignals: []
          },
          registeredPaymentCount: 0,
          paymentComplementCount: 0,
          stampedPaymentComplementCount: 0,
          lastRepIssuedAtUtc: null,
          operationalState: {
            lastEligibilityEvaluatedAtUtc: '2026-04-03T08:05:00Z',
            lastEligibilityStatus: 'Blocked',
            lastPrimaryReasonCode: 'FiscalDocumentCancelled',
            lastPrimaryReasonMessage: 'El CFDI está cancelado.',
            repPendingFlag: false,
            lastRepIssuedAtUtc: null,
            repCount: 0,
            totalPaidApplied: 0
          }
        },
        operationalState: {
          lastEligibilityEvaluatedAtUtc: '2026-04-03T08:05:00Z',
          lastEligibilityStatus: 'Blocked',
          lastPrimaryReasonCode: 'FiscalDocumentCancelled',
          lastPrimaryReasonMessage: 'El CFDI está cancelado.',
          repPendingFlag: false,
          lastRepIssuedAtUtc: null,
          repCount: 0,
          totalPaidApplied: 0
        },
        paymentHistory: [],
        paymentApplications: [],
        issuedReps: []
      }))
    });

    await fixture.componentInstance['openDetailModal'](fixture.componentInstance['items']()[1]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Todavía no hay pagos registrados relacionados');
    expect(fixture.nativeElement.textContent).toContain('Todavía no hay pagos aplicados');
    expect(fixture.nativeElement.textContent).toContain('Aún no hay REP ligados');
  });
});
