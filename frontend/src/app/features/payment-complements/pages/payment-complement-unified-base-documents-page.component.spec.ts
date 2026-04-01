import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { PaymentComplementsApiService } from '../infrastructure/payment-complements-api.service';
import { PaymentComplementUnifiedBaseDocumentsPageComponent } from './payment-complement-unified-base-documents-page.component';

describe('PaymentComplementUnifiedBaseDocumentsPageComponent', () => {
  async function configure() {
    await TestBed.configureTestingModule({
      imports: [PaymentComplementUnifiedBaseDocumentsPageComponent],
      providers: [
        {
          provide: PaymentComplementsApiService,
          useValue: {
            searchBaseDocuments: vi.fn().mockReturnValue(of({
              page: 1,
              pageSize: 25,
              totalCount: 2,
              totalPages: 1,
              items: [
                {
                  sourceType: 'Internal',
                  sourceId: 501,
                  fiscalDocumentId: 501,
                  uuid: 'UUID-INT-501',
                  series: 'INT',
                  folio: '501',
                  issuedAtUtc: '2026-04-01T09:00:00Z',
                  receiverRfc: 'BBB010101BBB',
                  receiverLegalName: 'Cliente interno',
                  currencyCode: 'MXN',
                  total: 116,
                  paymentMethodSat: 'PPD',
                  paymentFormSat: '99',
                  operationalStatus: 'Eligible',
                  outstandingBalance: 116,
                  repCount: 0,
                  isEligible: true,
                  isBlocked: false,
                  primaryReasonCode: 'EligibleOpenBalance',
                  primaryReasonMessage: 'CFDI interno listo para operación.',
                  availableActions: ['ViewDetail', 'OpenInternalWorkflow']
                },
                {
                  sourceType: 'External',
                  sourceId: 901,
                  externalRepBaseDocumentId: 901,
                  uuid: 'UUID-EXT-901',
                  series: 'EXT',
                  folio: '901',
                  issuedAtUtc: '2026-04-01T08:00:00Z',
                  issuerRfc: 'AAA010101AAA',
                  receiverRfc: 'CCC010101CCC',
                  receiverLegalName: 'Cliente externo',
                  currencyCode: 'MXN',
                  total: 232,
                  paymentMethodSat: 'PPD',
                  paymentFormSat: '99',
                  operationalStatus: 'ReadyForNextPhase',
                  validationStatus: 'Accepted',
                  satStatus: 'Active',
                  isEligible: true,
                  isBlocked: false,
                  primaryReasonCode: 'Accepted',
                  primaryReasonMessage: 'Listo para siguiente fase.',
                  availableActions: ['ViewDetail'],
                  importedAtUtc: '2026-04-01T11:00:00Z'
                }
              ]
            })),
            getExternalBaseDocumentById: vi.fn().mockReturnValue(of({
              id: 901,
              uuid: 'UUID-EXT-901',
              cfdiVersion: '4.0',
              documentType: 'I',
              series: 'EXT',
              folio: '901',
              issuedAtUtc: '2026-04-01T08:00:00Z',
              issuerRfc: 'AAA010101AAA',
              receiverRfc: 'CCC010101CCC',
              currencyCode: 'MXN',
              exchangeRate: 1,
              subtotal: 200,
              total: 232,
              paymentMethodSat: 'PPD',
              paymentFormSat: '99',
              validationStatus: 'Accepted',
              reasonCode: 'Accepted',
              reasonMessage: 'Aceptado',
              satStatus: 'Active',
              sourceFileName: 'external.xml',
              xmlHash: 'HASH-901',
              importedAtUtc: '2026-04-01T11:00:00Z',
              operationalStatus: 'ReadyForNextPhase',
              isEligible: true,
              isBlocked: false,
              primaryReasonCode: 'Accepted',
              primaryReasonMessage: 'Listo para siguiente fase.',
              availableActions: ['ViewDetail']
            })),
            getInternalBaseDocumentByFiscalDocumentId: vi.fn().mockReturnValue(of({
              summary: {
                fiscalDocumentId: 501,
                uuid: 'UUID-INT-501',
                series: 'INT',
                folio: '501',
                receiverRfc: 'BBB010101BBB',
                receiverLegalName: 'Cliente interno',
                issuedAtUtc: '2026-04-01T09:00:00Z',
                paymentMethodSat: 'PPD',
                paymentFormSat: '99',
                currencyCode: 'MXN',
                total: 116,
                paidTotal: 0,
                outstandingBalance: 116,
                fiscalStatus: 'Stamped',
                repOperationalStatus: 'Eligible',
                isEligible: true,
                isBlocked: false,
                eligibilityReason: 'Elegible',
                eligibility: {
                  status: 'Eligible',
                  primaryReasonCode: 'EligibleOpenBalance',
                  primaryReasonMessage: 'CFDI interno listo para operación.',
                  evaluatedAtUtc: '2026-04-01T10:00:00Z',
                  secondarySignals: []
                },
                registeredPaymentCount: 0,
                paymentComplementCount: 0,
                stampedPaymentComplementCount: 0
              },
              operationalState: {
                lastEligibilityEvaluatedAtUtc: '2026-04-01T10:00:00Z',
                lastEligibilityStatus: 'Eligible',
                lastPrimaryReasonCode: 'EligibleOpenBalance',
                lastPrimaryReasonMessage: 'CFDI interno listo para operación.',
                repPendingFlag: true,
                repCount: 0,
                totalPaidApplied: 0
              },
              paymentHistory: [],
              paymentApplications: [],
              issuedReps: []
            }))
          }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(PaymentComplementUnifiedBaseDocumentsPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('renders the unified tray with internal and external rows', async () => {
    const fixture = await configure();

    expect(fixture.nativeElement.textContent).toContain('Bandeja base REP interna y externa');
    expect(fixture.nativeElement.textContent).toContain('UUID-INT-501');
    expect(fixture.nativeElement.textContent).toContain('UUID-EXT-901');
  });

  it('opens unified detail for an external row', async () => {
    const fixture = await configure();
    const externalItem = fixture.componentInstance['items']().find((item) => item.sourceType === 'External');

    await fixture.componentInstance['openDetail'](externalItem!);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Detalle operativo del documento base');
    expect(fixture.nativeElement.textContent).toContain('Fase 4');
  });
});
