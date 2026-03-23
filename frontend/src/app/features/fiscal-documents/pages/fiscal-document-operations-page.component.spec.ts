import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { FiscalDocumentOperationsPageComponent } from './fiscal-document-operations-page.component';
import { FiscalDocumentsApiService } from '../infrastructure/fiscal-documents-api.service';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { PermissionService } from '../../../core/auth/permission.service';
import { ProductFiscalProfilesApiService } from '../../catalogs/infrastructure/product-fiscal-profiles-api.service';

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
      getBillingDocumentById: vi.fn().mockReturnValue(of({
        billingDocumentId: 30,
        salesOrderId: 20,
        legacyOrderId: 'LEG-1001',
        status: 'Draft',
        documentType: 'I',
        currencyCode: 'MXN',
        total: 100,
        createdAtUtc: '2026-03-20T12:00:00Z',
        fiscalDocumentId: null,
        fiscalDocumentStatus: null
      })),
      searchBillingDocuments: vi.fn().mockReturnValue(of([])),
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
    routeOptions?: { id?: string | null; billingDocumentId?: string | null },
    productApiOverrides?: Partial<Record<keyof ProductFiscalProfilesApiService, unknown>>
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
          provide: ProductFiscalProfilesApiService,
          useValue: {
            create: vi.fn().mockReturnValue(of({
              outcome: 'Created',
              isSuccess: true,
              id: 15
            })),
            ...productApiOverrides
          }
        },
        {
          provide: Router,
          useValue: { navigate: vi.fn().mockResolvedValue(true) }
        },
        {
          provide: PermissionService,
          useValue: {
            canStampFiscal: vi.fn().mockReturnValue(true),
            canCancelFiscal: vi.fn().mockReturnValue(true),
            canWriteMasterData: vi.fn().mockReturnValue(true)
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

  it('loads billing document context from query params', async () => {
    const getBillingDocumentById = vi.fn().mockReturnValue(of({
      billingDocumentId: 30,
      salesOrderId: 20,
      legacyOrderId: 'LEG-1001',
      status: 'Draft',
      documentType: 'I',
      currencyCode: 'MXN',
      total: 100,
      createdAtUtc: '2026-03-20T12:00:00Z',
      fiscalDocumentId: null,
      fiscalDocumentStatus: null
    }));

    const fixture = await configure(
      { getBillingDocumentById },
      { id: null, billingDocumentId: '30' }
    );

    expect(getBillingDocumentById).toHaveBeenCalledWith(30);
    expect(fixture.nativeElement.textContent).toContain('Documento #30');
    expect(fixture.nativeElement.textContent).toContain('LEG-1001');
  });

  it('searches and loads an existing billing document from the selector', async () => {
    const searchBillingDocuments = vi.fn().mockReturnValue(of([
      {
        billingDocumentId: 31,
        salesOrderId: 21,
        legacyOrderId: 'LEG-2002',
        status: 'Draft',
        documentType: 'I',
        currencyCode: 'MXN',
        total: 150,
        createdAtUtc: '2026-03-20T12:00:00Z',
        fiscalDocumentId: null,
        fiscalDocumentStatus: null
      }
    ]));
    const getBillingDocumentById = vi.fn().mockReturnValue(of({
      billingDocumentId: 31,
      salesOrderId: 21,
      legacyOrderId: 'LEG-2002',
      status: 'Draft',
      documentType: 'I',
      currencyCode: 'MXN',
      total: 150,
      createdAtUtc: '2026-03-20T12:00:00Z',
      fiscalDocumentId: null,
      fiscalDocumentStatus: null
    }));

    const fixture = await configure(
      { searchBillingDocuments, getBillingDocumentById },
      { id: null }
    );

    fixture.componentInstance['billingDocumentQuery'] = 'LEG-2002';
    await fixture.componentInstance['searchBillingDocuments']();
    fixture.detectChanges();

    expect(searchBillingDocuments).toHaveBeenCalledWith('LEG-2002');
    expect(fixture.nativeElement.textContent).toContain('Documento #31');

    await fixture.componentInstance['selectBillingDocument']({
      billingDocumentId: 31,
      salesOrderId: 21,
      legacyOrderId: 'LEG-2002',
      status: 'Draft',
      documentType: 'I',
      currencyCode: 'MXN',
      total: 150,
      createdAtUtc: '2026-03-20T12:00:00Z',
      fiscalDocumentId: null,
      fiscalDocumentStatus: null
    });
    fixture.detectChanges();

    expect(getBillingDocumentById).toHaveBeenCalledWith(31);
    expect(fixture.nativeElement.textContent).toContain('Documento seleccionado');
  });

  it('opens the recovery form automatically when preparation fails due to missing product fiscal profile via HttpErrorResponse', async () => {
    const prepareFiscalDocument = vi.fn().mockReturnValue(throwError(() => ({
      status: 400,
      error: {
        outcome: 'MissingProductFiscalProfile',
        isSuccess: false,
        errorMessage: "No active product fiscal profile exists for item line '1' and internal code 'MTE-4259'.",
        billingDocumentId: 30,
        fiscalDocumentId: null,
        status: null
      }
    })));
    const feedback = { show: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [FiscalDocumentOperationsPageComponent],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: convertToParamMap({ billingDocumentId: '30' }),
              paramMap: convertToParamMap({})
            }
          }
        },
        { provide: FiscalDocumentsApiService, useValue: createApi({ prepareFiscalDocument }) },
        { provide: ProductFiscalProfilesApiService, useValue: { create: vi.fn().mockReturnValue(of({ outcome: 'Created', isSuccess: true, id: 15 })) } },
        { provide: FeedbackService, useValue: feedback },
        { provide: Router, useValue: { navigate: vi.fn().mockResolvedValue(true) } },
        {
          provide: PermissionService,
          useValue: {
            canStampFiscal: vi.fn().mockReturnValue(true),
            canCancelFiscal: vi.fn().mockReturnValue(true),
            canWriteMasterData: vi.fn().mockReturnValue(true)
          }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(FiscalDocumentOperationsPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.componentInstance['selectedReceiverId'] = 9;
    await fixture.componentInstance['prepare']();
    fixture.detectChanges();

    expect(prepareFiscalDocument).toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Falta el perfil fiscal del producto MTE-4259');
    expect(fixture.nativeElement.textContent).toContain('Agregar producto fiscal');
    expect(fixture.nativeElement.textContent).toContain('Guardar y reintentar');
    expect(feedback.show).toHaveBeenCalledWith('warning', 'Falta el perfil fiscal del producto MTE-4259. Debes darlo de alta para continuar.');
  });

  it('creates the missing product fiscal profile and retries preparation', async () => {
    const prepareFiscalDocument = vi
      .fn()
      .mockReturnValueOnce(throwError(() => ({
        status: 400,
        error: {
          outcome: 'MissingProductFiscalProfile',
          isSuccess: false,
          errorMessage: "No active product fiscal profile exists for item line '1' and internal code 'MTE-4259'.",
          billingDocumentId: 30,
          fiscalDocumentId: null,
          status: null
        }
      })))
      .mockReturnValueOnce(of({
        outcome: 'Prepared',
        isSuccess: true,
        errorMessage: null,
        billingDocumentId: 30,
        fiscalDocumentId: 40,
        status: 'ReadyForStamping'
      }));
    const create = vi.fn().mockReturnValue(of({ outcome: 'Created', isSuccess: true, id: 15 }));
    const feedback = { show: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [FiscalDocumentOperationsPageComponent],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: convertToParamMap({ billingDocumentId: '30' }),
              paramMap: convertToParamMap({})
            }
          }
        },
        { provide: FiscalDocumentsApiService, useValue: createApi({ prepareFiscalDocument }) },
        { provide: ProductFiscalProfilesApiService, useValue: { create } },
        { provide: FeedbackService, useValue: feedback },
        { provide: Router, useValue: { navigate: vi.fn().mockResolvedValue(true) } },
        {
          provide: PermissionService,
          useValue: {
            canStampFiscal: vi.fn().mockReturnValue(true),
            canCancelFiscal: vi.fn().mockReturnValue(true),
            canWriteMasterData: vi.fn().mockReturnValue(true)
          }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(FiscalDocumentOperationsPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.componentInstance['selectedReceiverId'] = 9;

    await fixture.componentInstance['prepare']();
    await fixture.componentInstance['saveMissingProductProfile']({
      internalCode: 'MTE-4259',
      description: 'MTE-4259',
      satProductServiceCode: '01010101',
      satUnitCode: 'H87',
      taxObjectCode: '02',
      vatRate: 0.16,
      defaultUnitText: 'PIEZA',
      isActive: true
    });

    expect(create).toHaveBeenCalledWith({
      internalCode: 'MTE-4259',
      description: 'MTE-4259',
      satProductServiceCode: '01010101',
      satUnitCode: 'H87',
      taxObjectCode: '02',
      vatRate: 0.16,
      defaultUnitText: 'PIEZA',
      isActive: true
    });
    expect(prepareFiscalDocument).toHaveBeenCalledTimes(2);
    expect(feedback.show).toHaveBeenCalledWith('success', 'Perfil fiscal del producto MTE-4259 creado.');
    expect(feedback.show).toHaveBeenCalledWith('success', 'Documento fiscal preparado.');
  });

  it('keeps a visible recovery action after the user cancels the auto-opened form', async () => {
    const prepareFiscalDocument = vi.fn().mockReturnValue(throwError(() => ({
      status: 400,
      error: {
        outcome: 'MissingProductFiscalProfile',
        isSuccess: false,
        errorMessage: "No active product fiscal profile exists for item line '1' and internal code 'MTE-4259'.",
        billingDocumentId: 30,
        fiscalDocumentId: null,
        status: null
      }
    })));

    const fixture = await configure(
      { prepareFiscalDocument },
      { id: null, billingDocumentId: '30' }
    );

    fixture.componentInstance['selectedReceiverId'] = 9;
    await fixture.componentInstance['prepare']();
    fixture.componentInstance['closeMissingProductProfileForm']();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Falta el perfil fiscal del producto MTE-4259');
    expect(fixture.nativeElement.textContent).toContain('Agregar producto fiscal');
    expect(fixture.nativeElement.textContent).not.toContain('Guardar y reintentar');
  });

  it('opens recovery again for the next missing product after a successful save', async () => {
    const prepareFiscalDocument = vi
      .fn()
      .mockReturnValueOnce(throwError(() => ({
        status: 400,
        error: {
          outcome: 'MissingProductFiscalProfile',
          isSuccess: false,
          errorMessage: "No active product fiscal profile exists for item line '1' and internal code 'MTE-4259'.",
          billingDocumentId: 30,
          fiscalDocumentId: null,
          status: null
        }
      })))
      .mockReturnValueOnce(throwError(() => ({
        status: 400,
        error: {
          outcome: 'MissingProductFiscalProfile',
          isSuccess: false,
          errorMessage: "No active product fiscal profile exists for item line '2' and internal code 'PS-317'.",
          billingDocumentId: 30,
          fiscalDocumentId: null,
          status: null
        }
      })));
    const create = vi.fn().mockReturnValue(of({ outcome: 'Created', isSuccess: true, id: 15 }));

    const fixture = await configure(
      { prepareFiscalDocument },
      { id: null, billingDocumentId: '30' },
      { create }
    );

    fixture.componentInstance['selectedReceiverId'] = 9;
    await fixture.componentInstance['prepare']();
    await fixture.componentInstance['saveMissingProductProfile']({
      internalCode: 'MTE-4259',
      description: 'MTE-4259',
      satProductServiceCode: '01010101',
      satUnitCode: 'H87',
      taxObjectCode: '02',
      vatRate: 0.16,
      defaultUnitText: 'PIEZA',
      isActive: true
    });
    fixture.detectChanges();

    expect(prepareFiscalDocument).toHaveBeenCalledTimes(2);
    expect(fixture.nativeElement.textContent).toContain('Falta el perfil fiscal del producto PS-317');
    expect(fixture.nativeElement.textContent).toContain('Guardar y reintentar');
  });
});
