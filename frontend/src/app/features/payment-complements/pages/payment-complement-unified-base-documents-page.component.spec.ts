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
              summaryCounts: {
                infoCount: 0,
                warningCount: 1,
                errorCount: 0,
                criticalCount: 0,
                blockedCount: 0,
                alertCounts: [{ code: 'AppliedPaymentsWithoutStampedRep', count: 1 }],
                nextRecommendedActionCounts: [
                  { code: 'PrepareRep', count: 1 },
                  { code: 'RegisterPayment', count: 1 }
                ],
                quickViewCounts: [
                  { code: 'AppliedPaymentWithoutStampedRep', count: 1 },
                  { code: 'Blocked', count: 0 }
                ]
              },
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
                  hasAppliedPaymentsWithoutStampedRep: true,
                  hasPreparedRepPendingStamp: false,
                  hasRepWithError: false,
                  hasBlockedOperation: false,
                  nextRecommendedAction: 'PrepareRep',
                  availableActions: ['ViewDetail', 'OpenInternalWorkflow'],
                  alerts: [
                    { code: 'AppliedPaymentsWithoutStampedRep', severity: 'warning', message: 'Hay pagos aplicados sin REP timbrado en este CFDI.' }
                  ]
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
                  operationalStatus: 'ReadyForPayment',
                  validationStatus: 'Accepted',
                  satStatus: 'Active',
                  outstandingBalance: 232,
                  repCount: 0,
                  isEligible: true,
                  isBlocked: false,
                  primaryReasonCode: 'ReadyForPayment',
                  primaryReasonMessage: 'Listo para registrar pago.',
                  availableActions: ['ViewDetail', 'RegisterPayment'],
                  hasAppliedPaymentsWithoutStampedRep: false,
                  hasPreparedRepPendingStamp: false,
                  hasRepWithError: false,
                  hasBlockedOperation: false,
                  nextRecommendedAction: 'RegisterPayment',
                  alerts: [],
                  importedAtUtc: '2026-04-01T11:00:00Z'
                }
              ]
            })),
            getExternalBaseDocumentById: vi.fn().mockReturnValue(of({
              summary: {
                externalRepBaseDocumentId: 901,
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
                paidTotal: 0,
                outstandingBalance: 232,
                paymentMethodSat: 'PPD',
                paymentFormSat: '99',
                validationStatus: 'Accepted',
                reasonCode: 'Accepted',
                reasonMessage: 'Aceptado',
                satStatus: 'Active',
                sourceFileName: 'external.xml',
                xmlHash: 'HASH-901',
                importedAtUtc: '2026-04-01T11:00:00Z',
                registeredPaymentCount: 0,
                paymentComplementCount: 0,
                stampedPaymentComplementCount: 0,
                operationalStatus: 'ReadyForPayment',
                isEligible: true,
                isBlocked: false,
                primaryReasonCode: 'ReadyForPayment',
                primaryReasonMessage: 'Listo para registrar pago.',
                nextRecommendedAction: 'RegisterPayment',
                availableActions: ['ViewDetail', 'RegisterPayment'],
                alerts: []
              },
              paymentHistory: [],
              paymentApplications: [],
              issuedReps: []
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
                stampedPaymentComplementCount: 0,
                nextRecommendedAction: 'PrepareRep',
                availableActions: ['ViewDetail', 'OpenInternalWorkflow'],
                alerts: [
                  { code: 'AppliedPaymentsWithoutStampedRep', severity: 'warning', message: 'Hay pagos aplicados sin REP timbrado en este CFDI.' }
                ]
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
            })),
            bulkRefreshBaseDocuments: vi.fn().mockReturnValue(of({
              isSuccess: true,
              mode: 'Selected',
              maxDocuments: 50,
              totalRequested: 2,
              totalAttempted: 2,
              refreshedCount: 2,
              noChangesCount: 0,
              blockedCount: 0,
              failedCount: 0,
              items: [
                {
                  sourceType: 'Internal',
                  sourceId: 501,
                  attempted: true,
                  outcome: 'Refreshed',
                  message: 'Estatus refrescado correctamente.',
                  paymentComplementDocumentId: 7001,
                  paymentComplementStatus: 'Stamped',
                  lastKnownExternalStatus: 'VIGENTE'
                },
                {
                  sourceType: 'External',
                  sourceId: 901,
                  attempted: true,
                  outcome: 'Refreshed',
                  message: 'Estatus refrescado correctamente.',
                  paymentComplementDocumentId: 8001,
                  paymentComplementStatus: 'Stamped',
                  lastKnownExternalStatus: 'VIGENTE'
                }
              ]
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
    expect(fixture.nativeElement.textContent).toContain('Pendientes de timbrar');
    expect(fixture.nativeElement.textContent).toContain('Pago aplicado sin REP (1)');
  });

  it('applies a quick view and reloads the unified tray', async () => {
    const fixture = await configure();
    const api = TestBed.inject(PaymentComplementsApiService) as unknown as { searchBaseDocuments: ReturnType<typeof vi.fn> };

    await fixture.componentInstance['applyQuickView']('AppliedPaymentWithoutStampedRep');

    expect(api.searchBaseDocuments).toHaveBeenLastCalledWith(expect.objectContaining({
      quickView: 'AppliedPaymentWithoutStampedRep'
    }));
  });

  it('opens unified detail for an external row', async () => {
    const fixture = await configure();
    const externalItem = fixture.componentInstance['items']().find((item) => item.sourceType === 'External');

    await fixture.componentInstance['openDetail'](externalItem!);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Detalle operativo del documento base');
    expect(fixture.nativeElement.textContent).toContain('pestaña Externos');
  });

  it('renders next recommended action and operational alert badges in the unified tray', async () => {
    const fixture = await configure();

    expect(fixture.nativeElement.textContent).toContain('Siguiente: Preparar REP');
    expect(fixture.nativeElement.textContent).toContain('Pago aplicado sin REP timbrado');
  });

  it('executes bulk refresh from selected mixed documents', async () => {
    const fixture = await configure();
    const api = TestBed.inject(PaymentComplementsApiService) as unknown as Record<string, ReturnType<typeof vi.fn>>;
    const items = fixture.componentInstance['items']();

    fixture.componentInstance['toggleSelection'](items[0], true);
    fixture.componentInstance['toggleSelection'](items[1], true);
    await fixture.componentInstance['refreshSelectedDocuments']();
    fixture.detectChanges();

    expect(api['bulkRefreshBaseDocuments']).toHaveBeenCalledWith(expect.objectContaining({
      mode: 'Selected',
      documents: [
        { sourceType: 'Internal', sourceId: 501 },
        { sourceType: 'External', sourceId: 901 }
      ]
    }));
    expect(fixture.nativeElement.textContent).toContain('Resultado del refresh masivo');
  });
});
