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
                availableActions: ['ViewDetail', 'RegisterPayment']
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
                availableActions: ['ViewDetail', 'RegisterPayment']
              },
              paymentHistory: [],
              paymentApplications: [],
              issuedReps: []
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
});
