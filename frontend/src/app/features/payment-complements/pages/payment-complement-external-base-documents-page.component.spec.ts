import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { FeedbackService } from '../../../core/ui/feedback.service';
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
                uuid: 'UUID-EXT-123',
                series: 'EXT',
                folio: '123',
                issuedAtUtc: '2026-04-01T10:00:00Z',
                issuerRfc: 'AAA010101AAA',
                receiverRfc: 'BBB010101BBB',
                receiverLegalName: 'Cliente externo',
                currencyCode: 'MXN',
                total: 116,
                paymentMethodSat: 'PPD',
                paymentFormSat: '99',
                validationStatus: 'Accepted',
                satStatus: 'Active',
                importedAtUtc: '2026-04-01T11:00:00Z',
                operationalStatus: 'ReadyForNextPhase',
                isEligible: true,
                isBlocked: false,
                primaryReasonCode: 'Accepted',
                primaryReasonMessage: 'Listo para siguiente fase.',
                availableActions: ['ViewDetail']
              }]
            })),
            getExternalBaseDocumentById: vi.fn().mockReturnValue(of({
              id: 123,
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
              paymentMethodSat: 'PPD',
              paymentFormSat: '99',
              validationStatus: 'Accepted',
              reasonCode: 'Accepted',
              reasonMessage: 'Aceptado',
              satStatus: 'Active',
              sourceFileName: 'external.xml',
              xmlHash: 'HASH-1',
              importedAtUtc: '2026-04-01T11:00:00Z',
              operationalStatus: 'ReadyForNextPhase',
              isEligible: true,
              isBlocked: false,
              primaryReasonCode: 'Accepted',
              primaryReasonMessage: 'Listo para siguiente fase.',
              availableActions: ['ViewDetail']
            }))
          }
        },
        { provide: FeedbackService, useValue: { show: vi.fn() } }
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
    expect(fixture.nativeElement.textContent).toContain('Listo para siguiente fase');
  });

  it('opens external detail context', async () => {
    const fixture = await configure();

    await fixture.componentInstance['openDetail'](123);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Detalle de CFDI importado');
    expect(fixture.nativeElement.textContent).toContain('Emisor externo');
  });
});
