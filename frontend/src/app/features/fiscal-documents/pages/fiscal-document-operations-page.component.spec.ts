import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { FiscalDocumentOperationsPageComponent } from './fiscal-document-operations-page.component';
import { FiscalDocumentsApiService } from '../infrastructure/fiscal-documents-api.service';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { PermissionService } from '../../../core/auth/permission.service';

describe('FiscalDocumentOperationsPageComponent', () => {
  function createApi(overrides?: Partial<Record<keyof FiscalDocumentsApiService, unknown>>) {
    return {
      getActiveIssuer: vi.fn().mockReturnValue(of({
        id: 1,
        legalName: 'Issuer SA',
        rfc: 'AAA010101AAA',
        fiscalRegimeCode: '601',
        postalCode: '01000',
        cfdiVersion: '4.0',
        hasCertificateReference: true,
        hasPrivateKeyReference: true,
        hasPrivateKeyPasswordReference: true,
        pacEnvironment: 'Sandbox',
        isActive: true
      })),
      searchReceivers: vi.fn().mockReturnValue(of([])),
      prepareFiscalDocument: vi.fn(),
      getFiscalDocumentById: vi.fn().mockReturnValue(of({
        id: 40,
        billingDocumentId: 30,
        issuerProfileId: 1,
        fiscalReceiverId: 9,
        status: 'Stamped',
        cfdiVersion: '4.0',
        documentType: 'I',
        issuedAtUtc: '2026-03-20T12:00:00Z',
        currencyCode: 'MXN',
        exchangeRate: 1,
        paymentMethodSat: 'PPD',
        paymentFormSat: '99',
        paymentCondition: 'CREDITO',
        isCreditSale: true,
        creditDays: 7,
        issuerRfc: 'AAA010101AAA',
        issuerLegalName: 'Issuer SA',
        issuerFiscalRegimeCode: '601',
        issuerPostalCode: '01000',
        pacEnvironment: 'Sandbox',
        hasCertificateReference: true,
        hasPrivateKeyReference: true,
        hasPrivateKeyPasswordReference: true,
        receiverRfc: 'BBB010101BBB',
        receiverLegalName: 'Receiver One',
        receiverFiscalRegimeCode: '601',
        receiverCfdiUseCode: 'G03',
        receiverPostalCode: '02000',
        receiverCountryCode: 'MX',
        receiverForeignTaxRegistration: null,
        subtotal: 100,
        discountTotal: 0,
        taxTotal: 0,
        total: 100,
        items: []
      })),
      getStamp: vi.fn().mockReturnValue(of({
        id: 11,
        fiscalDocumentId: 40,
        providerName: 'FacturaloPlus',
        providerOperation: 'stamp',
        providerTrackingId: 'TRACK-1',
        status: 'Stamped',
        uuid: 'UUID-123',
        stampedAtUtc: '2026-03-20T12:00:00Z',
        providerCode: '200',
        providerMessage: 'Stamped',
        errorCode: null,
        errorMessage: null,
        xmlHash: 'HASH-1',
        qrCodeTextOrUrl: 'https://sat.example/qr',
        originalString: '||1.1|UUID-123||',
        createdAtUtc: '2026-03-20T12:00:00Z',
        updatedAtUtc: '2026-03-20T12:00:00Z'
      })),
      getStampXml: vi.fn().mockReturnValue(of('<cfdi:Comprobante Version="4.0" />')),
      getCancellation: vi.fn().mockReturnValue(throwError(() => ({ status: 404 }))),
      stampFiscalDocument: vi.fn(),
      cancelFiscalDocument: vi.fn(),
      refreshStatus: vi.fn(),
      ...overrides
    };
  }

  async function configure(
    apiOverrides?: Partial<Record<keyof FiscalDocumentsApiService, unknown>>,
    routeOptions?: { id?: string | null; billingDocumentId?: string | null }
  ) {
    const routeId = routeOptions && 'id' in routeOptions ? routeOptions.id : '40';

    await TestBed.configureTestingModule({
      imports: [FiscalDocumentOperationsPageComponent],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: convertToParamMap(routeOptions?.billingDocumentId ? { billingDocumentId: routeOptions.billingDocumentId } : {}),
              paramMap: convertToParamMap(routeId ? { id: routeId } : {})
            }
          }
        },
        {
          provide: FiscalDocumentsApiService,
          useValue: createApi(apiOverrides)
        },
        {
          provide: FeedbackService,
          useValue: { show: vi.fn() }
        },
        {
          provide: PermissionService,
          useValue: {
            canStampFiscal: vi.fn().mockReturnValue(true),
            canCancelFiscal: vi.fn().mockReturnValue(true)
          }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(FiscalDocumentOperationsPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('shows empty evidence state when the fiscal document is not stamped yet', async () => {
    const fixture = await configure({
      getStamp: vi.fn().mockReturnValue(throwError(() => ({ status: 404 })))
    });

    expect(fixture.nativeElement.textContent).toContain('Aún no hay evidencia de timbrado disponible');
  });

  it('opens and closes the XML viewer on demand', async () => {
    const fixture = await configure();

    await fixture.componentInstance['openStampXml']();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('XML del documento fiscal');
    expect(fixture.nativeElement.textContent).toContain('<cfdi:Comprobante Version="4.0" />');

    fixture.componentInstance['closeStampXml']();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).not.toContain('XML del documento fiscal');
  });

  it('shows an XML error state when the XML endpoint fails', async () => {
    const fixture = await configure({
      getStampXml: vi.fn().mockReturnValue(throwError(() => ({ error: { errorMessage: 'Forbidden' } })))
    });

    await fixture.componentInstance['openStampXml']();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Acceso denegado.');
  });

  it('debounces receiver search and renders suggestions', async () => {
    vi.useFakeTimers();
    const searchReceivers = vi.fn().mockReturnValue(of([
      {
        id: 9,
        rfc: 'BBB010101BBB',
        legalName: 'Receiver One',
        postalCode: '02000',
        fiscalRegimeCode: '601',
        cfdiUseCodeDefault: 'G03',
        isActive: true
      }
    ]));

    const fixture = await configure(
      {
        searchReceivers
      },
      { id: null, billingDocumentId: '30' }
    );

    fixture.componentInstance['onReceiverQueryChange']('BBB');
    await vi.advanceTimersByTimeAsync(249);
    expect(searchReceivers).not.toHaveBeenCalled();

    await vi.advanceTimersByTimeAsync(1);
    await Promise.resolve();
    fixture.detectChanges();

    expect(searchReceivers).toHaveBeenCalledWith('BBB');
    expect(fixture.nativeElement.textContent).toContain('BBB010101BBB');
    expect(fixture.nativeElement.textContent).toContain('Receiver One');
    vi.useRealTimers();
  });

  it('selecting a suggestion sets the receiver and shows the selected summary', async () => {
    const fixture = await configure(undefined, { id: null, billingDocumentId: '30' });

    fixture.componentInstance['selectReceiver']({
      id: 9,
      rfc: 'BBB010101BBB',
      legalName: 'Receiver One',
      postalCode: '02000',
      fiscalRegimeCode: '601',
      cfdiUseCodeDefault: 'G03',
      isActive: true
    });
    fixture.detectChanges();

    expect(fixture.componentInstance['selectedReceiverId']).toBe(9);
    expect(fixture.nativeElement.textContent).toContain('Receptor seleccionado');
    expect(fixture.nativeElement.textContent).toContain('BBB010101BBB · Receiver One');
  });

  it('shows no-results state for receiver autocomplete', async () => {
    const fixture = await configure(undefined, { id: null, billingDocumentId: '30' });

    fixture.componentInstance['receiverQuery'].set('ZZ');
    fixture.componentInstance['receiverSearchTouched'].set(true);
    fixture.componentInstance['receiverResults'].set([]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Sin coincidencias.');
  });

  it('shows autocomplete error state when receiver search fails', async () => {
    vi.useFakeTimers();
    const fixture = await configure(
      {
        searchReceivers: vi.fn().mockReturnValue(throwError(() => ({ error: { errorMessage: 'Forbidden' } })))
      },
      { id: null, billingDocumentId: '30' }
    );

    fixture.componentInstance['onReceiverQueryChange']('BBB');
    await vi.advanceTimersByTimeAsync(250);
    await Promise.resolve();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Acceso denegado.');
    vi.useRealTimers();
  });
});
