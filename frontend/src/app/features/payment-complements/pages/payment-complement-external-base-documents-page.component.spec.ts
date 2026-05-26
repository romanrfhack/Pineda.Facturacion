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
            searchExternalBaseDocuments: vi.fn((filters: { page: number }) => of({
              page: filters.page,
              pageSize: 25,
              totalCount: 26,
              totalPages: 2,
              summaryCounts: {
                infoCount: 1,
                warningCount: 0,
                errorCount: 0,
                criticalCount: 0,
                blockedCount: 0,
                alertCounts: [{ code: 'StampedRepAvailable', count: 1 }],
                nextRecommendedActionCounts: [{ code: 'RegisterPayment', count: 1 }],
                quickViewCounts: [
                  { code: 'Stamped', count: 1 },
                  { code: 'Blocked', count: 0 }
                ]
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
              timeline: [
                {
                  eventType: 'ExternalXmlImported',
                  occurredAtUtc: '2026-04-01T11:00:00Z',
                  sourceType: 'ExternalRepBaseDocument',
                  severity: 'info',
                  title: 'XML externo importado',
                  description: 'Se importó el CFDI externo UUID-EXT-123 desde external.xml.',
                  status: 'Accepted',
                  referenceId: 123,
                  referenceUuid: 'UUID-EXT-123',
                  metadata: { sourceFileName: 'external.xml' }
                },
                {
                  eventType: 'RepStamped',
                  occurredAtUtc: '2026-04-01T12:12:00Z',
                  sourceType: 'PaymentComplementStamp',
                  severity: 'info',
                  title: 'REP timbrado',
                  description: 'El REP #7001 quedó timbrado para el receptor BBB010101BBB.',
                  status: 'Stamped',
                  referenceId: 7001,
                  referenceUuid: 'UUID-REP-1',
                  metadata: { providerName: 'FacturaloPlus' }
                }
              ],
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
                  xmlAvailable: true,
                  pdfAvailable: true,
                  canGeneratePdf: false,
                  canEmail: true,
                  canDownloadXml: true,
                  canDownloadPdf: true,
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
              xmlAvailable: true,
              email: {
                attempted: true,
                sent: true,
                status: 'sent',
                recipients: ['cliente@example.com'],
                invalidRecipients: [],
                message: 'El correo fue enviado automáticamente a: cliente@example.com.'
              }
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
            })),
            bulkRefreshExternalBaseDocuments: vi.fn().mockReturnValue(of({
              isSuccess: true,
              mode: 'Selected',
              maxDocuments: 50,
              totalRequested: 1,
              totalAttempted: 1,
              refreshedCount: 1,
              noChangesCount: 0,
              blockedCount: 0,
              failedCount: 0,
              items: [
                {
                  sourceType: 'External',
                  sourceId: 123,
                  attempted: true,
                  outcome: 'Refreshed',
                  message: 'Estatus refrescado correctamente.',
                  paymentComplementDocumentId: 7001,
                  paymentComplementStatus: 'Stamped',
                  lastKnownExternalStatus: 'VIGENTE'
                }
              ]
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
    expect(fixture.nativeElement.textContent).toContain('Timbrado (1)');
    expect(fixture.nativeElement.textContent).toContain('Bloqueado (0)');
  });

  it('applies a quick view and reloads the external tray', async () => {
    const fixture = await configure();
    const api = TestBed.inject(PaymentComplementsApiService) as unknown as { searchExternalBaseDocuments: ReturnType<typeof vi.fn> };

    await fixture.componentInstance['applyQuickView']('Stamped');

    expect(api.searchExternalBaseDocuments).toHaveBeenLastCalledWith(expect.objectContaining({
      page: 1,
      quickView: 'Stamped'
    }));
  });

  it('loads page 2 preserving filters and clears local selection', async () => {
    const fixture = await configure();
    const api = TestBed.inject(PaymentComplementsApiService) as unknown as { searchExternalBaseDocuments: ReturnType<typeof vi.fn> };

    fixture.componentInstance['receiverRfc'] = 'BBB010101BBB';
    fixture.componentInstance['toggleSelection'](123, true);
    await fixture.componentInstance['goToPage'](2);
    fixture.detectChanges();

    expect(api.searchExternalBaseDocuments).toHaveBeenLastCalledWith(expect.objectContaining({
      page: 2,
      receiverRfc: 'BBB010101BBB'
    }));
    expect(fixture.componentInstance['selectedCount']()).toBe(0);
    expect(fixture.nativeElement.textContent).toContain('Página 2 de 2');
  });

  it('disables previous pagination on the first page', async () => {
    const fixture = await configure();
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('.pagination button')) as HTMLButtonElement[];

    expect(buttons.find((button) => button.textContent?.includes('Anterior'))?.disabled).toBe(true);
    expect(buttons.find((button) => button.textContent?.includes('Siguiente'))?.disabled).toBe(false);
  });

  it('disables next pagination on the last page', async () => {
    const fixture = await configure();

    await fixture.componentInstance['goToPage'](2);
    fixture.detectChanges();

    const buttons = Array.from(fixture.nativeElement.querySelectorAll('.pagination button')) as HTMLButtonElement[];
    expect(buttons.find((button) => button.textContent?.includes('Anterior'))?.disabled).toBe(false);
    expect(buttons.find((button) => button.textContent?.includes('Siguiente'))?.disabled).toBe(true);
  });

  it('hides pagination controls when there is a single page', async () => {
    const fixture = await configure();

    fixture.componentInstance['totalPages'].set(1);
    fixture.componentInstance['page'].set(1);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.pagination')).toBeNull();
  });

  it('resets to page 1 and clears selection when filters change', async () => {
    const fixture = await configure();
    const api = TestBed.inject(PaymentComplementsApiService) as unknown as { searchExternalBaseDocuments: ReturnType<typeof vi.fn> };

    await fixture.componentInstance['goToPage'](2);
    fixture.componentInstance['toggleSelection'](123, true);
    fixture.componentInstance['query'] = 'UUID-EXT-123';
    await fixture.componentInstance['applyFilters']();

    expect(api.searchExternalBaseDocuments).toHaveBeenLastCalledWith(expect.objectContaining({
      page: 1,
      query: 'UUID-EXT-123'
    }));
    expect(fixture.componentInstance['selectedCount']()).toBe(0);
  });

  it('opens external detail context', async () => {
    const fixture = await configure();

    await fixture.componentInstance['openDetail'](123);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Detalle operativo del CFDI importado');
    expect(fixture.nativeElement.textContent).toContain('Emisor externo');
    expect(fixture.nativeElement.textContent).toContain('Timeline operativo');
    expect(fixture.nativeElement.textContent).toContain('XML externo importado');
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

  it('executes bulk refresh from selected external documents', async () => {
    const fixture = await configure();
    const api = TestBed.inject(PaymentComplementsApiService) as unknown as Record<string, ReturnType<typeof vi.fn>>;

    fixture.componentInstance['toggleSelection'](123, true);
    await fixture.componentInstance['refreshSelectedDocuments']();
    fixture.detectChanges();

    expect(api['bulkRefreshExternalBaseDocuments']).toHaveBeenCalledWith(expect.objectContaining({
      mode: 'Selected',
      documents: [{ sourceType: 'External', sourceId: 123 }]
    }));
    expect(fixture.nativeElement.textContent).toContain('Resultado del refresh masivo');
  });

  it('executes filtered bulk refresh without selected document ids', async () => {
    const fixture = await configure();
    const api = TestBed.inject(PaymentComplementsApiService) as unknown as Record<string, ReturnType<typeof vi.fn>>;

    fixture.componentInstance['toggleSelection'](123, true);
    await fixture.componentInstance['refreshFilteredDocuments']();

    expect(api['bulkRefreshExternalBaseDocuments']).toHaveBeenCalledWith(expect.objectContaining({
      mode: 'Filtered'
    }));
    const calls = api['bulkRefreshExternalBaseDocuments'].mock.calls;
    expect(calls[calls.length - 1][0]).not.toHaveProperty('documents');
  });
});
