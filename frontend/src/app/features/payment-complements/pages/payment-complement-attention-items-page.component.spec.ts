import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { PaymentComplementsApiService } from '../infrastructure/payment-complements-api.service';
import { PaymentComplementAttentionItemsPageComponent } from './payment-complement-attention-items-page.component';

describe('PaymentComplementAttentionItemsPageComponent', () => {
  async function configure(
    searchAttentionItems = vi.fn((filters: { page: number }) => of({
      page: filters.page,
      pageSize: 25,
      totalCount: 26,
      totalPages: 2,
      summaryCounts: {
        infoCount: 0,
        warningCount: 1,
        errorCount: 1,
        criticalCount: 1,
        blockedCount: 1,
        alertCounts: [
          { code: 'CancelledBaseDocument', count: 1 },
          { code: 'SatValidationUnavailable', count: 1 }
        ],
        nextRecommendedActionCounts: [
          { code: 'Blocked', count: 1 },
          { code: 'RefreshRepStatus', count: 1 }
        ],
        quickViewCounts: []
      },
      items: [
        {
          sourceType: 'Internal',
          sourceId: 501,
          fiscalDocumentId: 501,
          billingDocumentId: 401,
          uuid: 'UUID-INT-501',
          series: 'INT',
          folio: '501',
          issuedAtUtc: '2026-04-01T09:00:00Z',
          receiverRfc: 'BBB010101BBB',
          receiverLegalName: 'Cliente bloqueado',
          currencyCode: 'MXN',
          total: 116,
          outstandingBalance: 116,
          operationalStatus: 'Blocked',
          isBlocked: true,
          primaryReasonCode: 'FiscalDocumentCancelled',
          primaryReasonMessage: 'El CFDI está cancelado.',
          nextRecommendedAction: 'Blocked',
          availableActions: ['ViewDetail'],
          attentionSeverity: 'critical',
          attentionAlerts: [
            {
              alertCode: 'CancelledBaseDocument',
              severity: 'critical',
              title: 'Documento base cancelado',
              message: 'El CFDI está cancelado.',
              hookKey: 'rep.cancelled-base-document'
            }
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
          importedAtUtc: '2026-04-01T11:00:00Z',
          issuerRfc: 'AAA010101AAA',
          receiverRfc: 'CCC010101CCC',
          receiverLegalName: 'Cliente externo',
          currencyCode: 'MXN',
          total: 232,
          outstandingBalance: 232,
          operationalStatus: 'Blocked',
          isBlocked: true,
          primaryReasonCode: 'ValidationUnavailable',
          primaryReasonMessage: 'No fue posible validar el CFDI externo en SAT.',
          nextRecommendedAction: 'RefreshRepStatus',
          availableActions: ['ViewDetail', 'RefreshRepStatus'],
          attentionSeverity: 'warning',
          attentionAlerts: [
            {
              alertCode: 'SatValidationUnavailable',
              severity: 'warning',
              title: 'Validación SAT no disponible',
              message: 'No fue posible validar el CFDI externo en SAT.',
              hookKey: 'rep.sat-validation-unavailable'
            }
          ]
        }
      ]
    }))
  ) {
    await TestBed.configureTestingModule({
      imports: [PaymentComplementAttentionItemsPageComponent],
      providers: [
        {
          provide: PaymentComplementsApiService,
          useValue: {
            searchAttentionItems,
            getInternalBaseDocumentByFiscalDocumentId: vi.fn().mockReturnValue(of({
              summary: {
                fiscalDocumentId: 501,
                uuid: 'UUID-INT-501',
                series: 'INT',
                folio: '501',
                receiverRfc: 'BBB010101BBB',
                receiverLegalName: 'Cliente bloqueado',
                issuedAtUtc: '2026-04-01T09:00:00Z',
                paymentMethodSat: 'PPD',
                paymentFormSat: '99',
                currencyCode: 'MXN',
                total: 116,
                paidTotal: 0,
                outstandingBalance: 116,
                fiscalStatus: 'Cancelled',
                repOperationalStatus: 'Blocked',
                isEligible: false,
                isBlocked: true,
                eligibilityReason: 'El CFDI está cancelado.',
                eligibility: {
                  status: 'Blocked',
                  primaryReasonCode: 'FiscalDocumentCancelled',
                  primaryReasonMessage: 'El CFDI está cancelado.',
                  evaluatedAtUtc: '2026-04-01T10:00:00Z',
                  secondarySignals: []
                },
                registeredPaymentCount: 0,
                paymentComplementCount: 0,
                stampedPaymentComplementCount: 0,
                nextRecommendedAction: 'Blocked',
                availableActions: ['ViewDetail'],
                alerts: [
                  { code: 'CancelledBaseDocument', severity: 'critical', message: 'El CFDI está cancelado.' }
                ]
              },
              operationalState: null,
              timeline: [
                {
                  eventType: 'RepCancellationRejected',
                  occurredAtUtc: '2026-04-01T10:05:00Z',
                  sourceType: 'PaymentComplementCancellation',
                  severity: 'error',
                  title: 'Cancelación de REP rechazada',
                  description: 'La cancelación fue rechazada.',
                  status: 'Rejected',
                  referenceId: 7001,
                  referenceUuid: 'UUID-PC-1',
                  metadata: {}
                }
              ],
              paymentHistory: [],
              paymentApplications: [],
              issuedReps: []
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
                receiverLegalName: 'Cliente externo',
                currencyCode: 'MXN',
                exchangeRate: 1,
                subtotal: 200,
                total: 232,
                paidTotal: 0,
                outstandingBalance: 232,
                paymentMethodSat: 'PPD',
                paymentFormSat: '99',
                validationStatus: 'Blocked',
                reasonCode: 'ValidationUnavailable',
                reasonMessage: 'SAT no disponible',
                satStatus: 'Unavailable',
                sourceFileName: 'external.xml',
                xmlHash: 'HASH-901',
                importedAtUtc: '2026-04-01T11:00:00Z',
                registeredPaymentCount: 0,
                paymentComplementCount: 0,
                stampedPaymentComplementCount: 0,
                operationalStatus: 'Blocked',
                isEligible: false,
                isBlocked: true,
                primaryReasonCode: 'ValidationUnavailable',
                primaryReasonMessage: 'No fue posible validar el CFDI externo en SAT.',
                nextRecommendedAction: 'RefreshRepStatus',
                availableActions: ['ViewDetail', 'RefreshRepStatus'],
                alerts: [
                  { code: 'SatValidationUnavailable', severity: 'warning', message: 'No fue posible validar el CFDI externo en SAT.' }
                ]
              },
              timeline: [
                {
                  eventType: 'SatValidationUnavailable',
                  occurredAtUtc: '2026-04-01T11:05:00Z',
                  sourceType: 'SatValidation',
                  severity: 'warning',
                  title: 'Validación SAT no disponible',
                  description: 'No fue posible validar el CFDI externo en SAT.',
                  status: 'Unavailable',
                  referenceId: 901,
                  referenceUuid: 'UUID-EXT-901',
                  metadata: {}
                }
              ],
              paymentHistory: [],
              paymentApplications: [],
              issuedReps: []
            }))
          }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(PaymentComplementAttentionItemsPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('renders the attention list with notifiable alerts', async () => {
    const fixture = await configure();

    expect(fixture.nativeElement.textContent).toContain('Documentos REP que requieren atención');
    expect(fixture.nativeElement.textContent).toContain('UUID-INT-501');
    expect(fixture.nativeElement.textContent).toContain('Documento base cancelado');
    expect(fixture.nativeElement.textContent).toContain('Validación SAT no disponible');
    expect(fixture.nativeElement.textContent).toContain('Hook rep.cancelled-base-document');
  });

  it('renders an empty operational state without critical or cancelled chips', async () => {
    const fixture = await configure(vi.fn((filters: { page: number }) => of({
      page: filters.page,
      pageSize: 25,
      totalCount: 0,
      totalPages: 0,
      summaryCounts: {
        infoCount: 0,
        warningCount: 0,
        errorCount: 0,
        criticalCount: 0,
        blockedCount: 0,
        alertCounts: [],
        nextRecommendedActionCounts: [],
        quickViewCounts: []
      },
      items: []
    })));

    expect(fixture.nativeElement.textContent).toContain('No hay documentos que requieran atención.');
    expect(fixture.nativeElement.querySelector('.summary-strip')).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('Críticas 13');
    expect(fixture.nativeElement.textContent).not.toContain('Documento base cancelado (13)');
  });

  it('applies filters through the attention endpoint', async () => {
    const fixture = await configure();
    const api = TestBed.inject(PaymentComplementsApiService) as unknown as { searchAttentionItems: ReturnType<typeof vi.fn> };

    fixture.componentInstance['sourceType'] = 'External';
    fixture.componentInstance['alertCodeFilter'] = 'SatValidationUnavailable';
    await fixture.componentInstance['applyFilters']();

    expect(api.searchAttentionItems).toHaveBeenLastCalledWith(expect.objectContaining({
      page: 1,
      sourceType: 'External',
      alertCode: 'SatValidationUnavailable'
    }));
  });

  it('includes cancelled base documents only when the audit filter is selected', async () => {
    const fixture = await configure();
    const api = TestBed.inject(PaymentComplementsApiService) as unknown as { searchAttentionItems: ReturnType<typeof vi.fn> };

    fixture.componentInstance['includeCancelledBaseDocuments'] = true;
    await fixture.componentInstance['applyFilters']();
    fixture.detectChanges();

    expect(api.searchAttentionItems).toHaveBeenLastCalledWith(expect.objectContaining({
      page: 1,
      includeCancelledBaseDocuments: true
    }));
    expect(fixture.nativeElement.textContent).toContain('Documentos base cancelados.');
    expect(fixture.nativeElement.textContent).toContain('no son elegibles para complemento de pago');
  });

  it('uses the audit flag when filtering by cancelled base document alert', async () => {
    const fixture = await configure();
    const api = TestBed.inject(PaymentComplementsApiService) as unknown as { searchAttentionItems: ReturnType<typeof vi.fn> };

    fixture.componentInstance['alertCodeFilter'] = 'CancelledBaseDocument';
    await fixture.componentInstance['applyFilters']();

    expect(api.searchAttentionItems).toHaveBeenLastCalledWith(expect.objectContaining({
      alertCode: 'CancelledBaseDocument',
      includeCancelledBaseDocuments: true
    }));
  });

  it('does not render REP operation buttons for cancelled base documents in the attention list', async () => {
    const fixture = await configure();
    const buttonText = (Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[])
      .map((button) => button.textContent ?? '')
      .join(' ');

    expect(fixture.nativeElement.textContent).toContain('Documento base cancelado');
    expect(buttonText).not.toContain('Preparar REP');
    expect(buttonText).not.toContain('Timbrar REP');
  });

  it('loads page 2 preserving attention filters', async () => {
    const fixture = await configure();
    const api = TestBed.inject(PaymentComplementsApiService) as unknown as { searchAttentionItems: ReturnType<typeof vi.fn> };

    fixture.componentInstance['severityFilter'] = 'warning';
    await fixture.componentInstance['goToPage'](2);
    fixture.detectChanges();

    expect(api.searchAttentionItems).toHaveBeenLastCalledWith(expect.objectContaining({
      page: 2,
      severity: 'warning'
    }));
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

  it('resets to page 1 when attention filters change', async () => {
    const fixture = await configure();
    const api = TestBed.inject(PaymentComplementsApiService) as unknown as { searchAttentionItems: ReturnType<typeof vi.fn> };

    await fixture.componentInstance['goToPage'](2);
    fixture.componentInstance['query'] = 'UUID-EXT-901';
    await fixture.componentInstance['applyFilters']();

    expect(api.searchAttentionItems).toHaveBeenLastCalledWith(expect.objectContaining({
      page: 1,
      query: 'UUID-EXT-901'
    }));
  });

  it('opens detail for an attention item', async () => {
    const fixture = await configure();
    const item = fixture.componentInstance['items']()[0];

    await fixture.componentInstance['openDetail'](item);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Detalle del documento afectado');
    expect(fixture.nativeElement.textContent).toContain('Timeline reciente');
    expect(fixture.nativeElement.textContent).toContain('Cancelación de REP rechazada');
  });
});
