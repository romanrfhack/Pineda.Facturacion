import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { PaymentComplementsApiService } from '../infrastructure/payment-complements-api.service';
import { PaymentComplementExternalBaseDocumentsPageComponent } from './payment-complement-external-base-documents-page.component';

describe('PaymentComplementExternalBaseDocumentsPageComponent', () => {
  async function configure() {
    await TestBed.configureTestingModule({
      imports: [PaymentComplementExternalBaseDocumentsPageComponent],
      providers: [
        {
          provide: PaymentComplementsApiService,
          useValue: {
            importExternalBaseDocumentXml: vi.fn().mockReturnValue(of({
              outcome: 'Accepted',
              isSuccess: true,
              externalRepBaseDocumentId: 123,
              validationStatus: 'Accepted',
              reasonCode: 'Accepted',
              reasonMessage: 'Factura importada',
              isDuplicate: false
            })),
            searchExternalBaseDocuments: vi.fn().mockReturnValue(of({
              page: 1,
              pageSize: 25,
              totalCount: 1,
              totalPages: 1,
              summaryCounts: {
                infoCount: 1,
                warningCount: 0,
                errorCount: 0,
                criticalCount: 0,
                blockedCount: 0,
                alertCounts: [{ code: 'StampedRepAvailable', count: 1 }],
                nextRecommendedActionCounts: [{ code: 'RegisterPayment', count: 1 }]
              },
              items: [{
                externalRepBaseDocumentId: 123,
                accountsReceivableInvoiceId: null,
                uuid: 'UUID-EXT-123',
                cfdiVersion: '4.0',
                documentType: 'I',
                series: 'EXT',
                folio: '123',
                issuedAtUtc: '2026-04-01T10:00:00Z',
                issuerRfc: 'AAA010101AAA',
                issuerLegalName: 'Emisor externo',
                receiverRfc: 'BBB010101BBB',
                receiverLegalName: 'Cliente externo',
                currencyCode: 'MXN',
                exchangeRate: 1,
                subtotal: 100,
                total: 116,
                paidTotal: 0,
                outstandingBalance: 116,
                paymentMethodSat: 'PPD',
                paymentFormSat: '99',
                validationStatus: 'Accepted',
                reasonCode: 'Accepted',
                reasonMessage: 'Aceptado',
                satStatus: 'Active',
                importedAtUtc: '2026-04-01T11:00:00Z',
                sourceFileName: 'external.xml',
                xmlHash: 'HASH-1',
                registeredPaymentCount: 0,
                paymentComplementCount: 0,
                stampedPaymentComplementCount: 0,
                operationalStatus: 'ReadyForPayment',
                isEligible: true,
                isBlocked: false,
                primaryReasonCode: 'ReadyForPayment',
                primaryReasonMessage: 'Listo para registrar pago.',
                hasAppliedPaymentsWithoutStampedRep: false,
                hasPreparedRepPendingStamp: false,
                hasRepWithError: false,
                hasBlockedOperation: false,
                nextRecommendedAction: 'RegisterPayment',
                availableActions: ['ViewDetail', 'RegisterPayment'],
                alerts: []
              }]
            })),
            getExternalBaseDocumentById: vi.fn().mockReturnValue(of({
              summary: {
                externalRepBaseDocumentId: 123,
                accountsReceivableInvoiceId: null,
                uuid: 'UUID-EXT-123',
                cfdiVersion: '4.0',
                documentType: 'I',
                series: 'EXT',
                folio: '123',
                issuedAtUtc: '2026-04-01T10:00:00Z',
                issuerRfc: 'AAA010101AAA',
                issuerLegalName: 'Emisor externo',
                receiverRfc: 'BBB010101BBB',
                receiverLegalName: 'Cliente externo',
                currencyCode: 'MXN',
                exchangeRate: 1,
                subtotal: 100,
                total: 116,
                paidTotal: 0,
                outstandingBalance: 116,
                paymentMethodSat: 'PPD',
                paymentFormSat: '99',
                validationStatus: 'Accepted',
                reasonCode: 'Accepted',
                reasonMessage: 'Aceptado',
                satStatus: 'Active',
                sourceFileName: 'external.xml',
                xmlHash: 'HASH-1',
                importedAtUtc: '2026-04-01T11:00:00Z',
                registeredPaymentCount: 0,
                paymentComplementCount: 0,
                stampedPaymentComplementCount: 0,
                operationalStatus: 'ReadyForPayment',
                isEligible: true,
                isBlocked: false,
                primaryReasonCode: 'ReadyForPayment',
                primaryReasonMessage: 'Listo para registrar pago.',
                hasAppliedPaymentsWithoutStampedRep: false,
                hasPreparedRepPendingStamp: false,
                hasRepWithError: false,
                hasBlockedOperation: false,
                nextRecommendedAction: 'RefreshRepStatus',
                availableActions: ['ViewDetail', 'RegisterPayment', 'RefreshRepStatus', 'CancelRep'],
                alerts: [
                  { code: 'StampedRepAvailable', severity: 'info', message: 'El CFDI externo ya cuenta con REP timbrado y sólo requiere seguimiento o refresh de estatus.' }
                ]
              },
              paymentHistory: [],
              paymentApplications: [],
              issuedReps: [
                {
                  paymentComplementId: 7001,
                  accountsReceivablePaymentId: 9001,
                  status: 'Stamped',
                  uuid: 'UUID-REP-1',
                  paymentDateUtc: '2026-04-01T12:00:00Z',
                  issuedAtUtc: '2026-04-01T12:10:00Z',
                  stampedAtUtc: '2026-04-01T12:12:00Z',
                  cancelledAtUtc: null,
                  providerName: 'FacturaloPlus',
                  installmentNumber: 1,
                  previousBalance: 116,
                  paidAmount: 40,
                  remainingBalance: 76
                }
              ]
            })),
            registerExternalBaseDocumentPayment: vi.fn().mockReturnValue(of({
              outcome: 'RegisteredAndApplied',
              isSuccess: true,
              warningMessages: [],
              externalRepBaseDocumentId: 123,
              accountsReceivableInvoiceId: 401,
              accountsReceivablePaymentId: 9001,
              appliedAmount: 40,
              remainingBalance: 76,
              remainingPaymentAmount: 0,
              applications: []
            })),
            prepareExternalBaseDocumentPaymentComplement: vi.fn().mockReturnValue(of({
              outcome: 'Prepared',
              isSuccess: true,
              warningMessages: [],
              externalRepBaseDocumentId: 123,
              accountsReceivablePaymentId: 9001,
              paymentComplementDocumentId: 7001,
              status: 'ReadyForStamping',
              relatedDocumentCount: 1
            })),
            stampExternalBaseDocumentPaymentComplement: vi.fn().mockReturnValue(of({
              outcome: 'Stamped',
              isSuccess: true,
              warningMessages: [],
              externalRepBaseDocumentId: 123,
              accountsReceivablePaymentId: 9001,
              paymentComplementDocumentId: 7001,
              paymentComplementStampId: 8001,
              stampUuid: 'UUID-REP-1',
              stampedAtUtc: '2026-04-01T13:00:00Z',
              xmlAvailable: true
            })),
            refreshExternalBaseDocumentPaymentComplementStatus: vi.fn().mockReturnValue(of({
              outcome: 'Refreshed',
              isSuccess: true,
              externalRepBaseDocumentId: 123,
              paymentComplementDocumentId: 7001,
              paymentComplementStatus: 'Stamped',
              lastKnownExternalStatus: 'VIGENTE',
              nextRecommendedAction: 'RefreshRepStatus',
              availableActions: ['ViewDetail', 'RefreshRepStatus', 'CancelRep'],
              alerts: []
            })),
            cancelExternalBaseDocumentPaymentComplement: vi.fn().mockReturnValue(of({
              outcome: 'Cancelled',
              isSuccess: true,
              externalRepBaseDocumentId: 123,
              paymentComplementDocumentId: 7001,
              paymentComplementStatus: 'Cancelled',
              cancellationStatus: 'Cancelled',
              availableActions: ['ViewDetail'],
              alerts: []
            }))
          }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(PaymentComplementExternalBaseDocumentsPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('renders the external tray with imported invoices', async () => {
    const fixture = await configure();

    expect(fixture.nativeElement.textContent).toContain('Bandeja de CFDI externos importados');
    expect(fixture.nativeElement.textContent).toContain('UUID-EXT-123');
    expect(fixture.nativeElement.textContent).toContain('Listo para registrar pago');
    expect(fixture.nativeElement.textContent).toContain('Advertencias (0)');
    expect(fixture.nativeElement.textContent).toContain('Bloqueados (0)');
  });

  it('opens external detail context', async () => {
    const fixture = await configure();

    await fixture.componentInstance['openDetail'](123);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Detalle operativo del CFDI importado');
    expect(fixture.nativeElement.textContent).toContain('Emisor externo');
  });

  it('renders external operation actions in detail', async () => {
    const fixture = await configure();

    await fixture.componentInstance['openDetail'](123);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Registrar pago');
  });

  it('renders operational alerts and uses refresh/cancel from external detail', async () => {
    const fixture = await configure();

    await fixture.componentInstance['openDetail'](123);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Seguimiento operativo');
    expect(fixture.nativeElement.textContent).toContain('REP timbrado disponible');
    expect(fixture.nativeElement.textContent).toContain('Refrescar');
    expect(fixture.nativeElement.textContent).toContain('Cancelar');

    const api = TestBed.inject(PaymentComplementsApiService) as unknown as Record<string, ReturnType<typeof vi.fn>>;
    const complement = fixture.componentInstance['selectedDetail']()!.issuedReps[0];

    await fixture.componentInstance['refreshRep'](complement);
    await fixture.componentInstance['cancelRep'](complement);

    expect(api['refreshExternalBaseDocumentPaymentComplementStatus']).toHaveBeenCalledWith(123, { paymentComplementDocumentId: 7001 });
    expect(api['cancelExternalBaseDocumentPaymentComplement']).toHaveBeenCalledWith(123, {
      paymentComplementDocumentId: 7001,
      cancellationReasonCode: '02',
      replacementUuid: null
    });
  });
});
