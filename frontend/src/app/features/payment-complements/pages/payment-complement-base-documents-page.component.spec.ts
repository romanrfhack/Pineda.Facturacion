import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
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
