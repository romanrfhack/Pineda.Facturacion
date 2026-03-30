import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { FiscalDocumentOperationsPageComponent } from './fiscal-document-operations-page.component';
import { FiscalDocumentsApiService } from '../infrastructure/fiscal-documents-api.service';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { PermissionService } from '../../../core/auth/permission.service';
import { ProductFiscalProfilesApiService } from '../../catalogs/infrastructure/product-fiscal-profiles-api.service';
import { FiscalReceiversApiService } from '../../catalogs/infrastructure/fiscal-receivers-api.service';
import { OrdersApiService } from '../../orders/infrastructure/orders-api.service';

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
        fiscalDocumentStatus: null,
        associatedOrders: [
          {
            salesOrderId: 20,
            legacyOrderId: 'LEG-1001-ORD-LEG-1001',
            customerName: 'Receiver One',
            total: 100,
            isPrimary: true
          }
        ],
        items: [
          {
            billingDocumentItemId: 501,
            salesOrderId: 20,
            salesOrderItemId: 601,
            sourceSalesOrderLineNumber: 1,
            sourceLegacyOrderId: 'LEG-1001-ORD-LEG-1001',
            lineNumber: 1,
            productInternalCode: 'MTE-4259',
            description: 'FILTRO DE ACEITE',
            quantity: 1,
            total: 100
          }
        ]
      })),
      searchBillingDocuments: vi.fn().mockReturnValue(of([])),
      prepareFiscalDocument: vi.fn(),
      addSalesOrderToBillingDocument: vi.fn().mockReturnValue(of({
        outcome: 'Updated',
        isSuccess: true,
        billingDocumentId: 30,
        billingDocumentStatus: 'Draft',
        salesOrderId: 20,
        fiscalDocumentId: 40,
        fiscalDocumentStatus: 'ReadyForStamping',
        associatedOrderCount: 2,
        total: 200
      })),
      removeSalesOrderFromBillingDocument: vi.fn().mockReturnValue(of({
        outcome: 'Updated',
        isSuccess: true,
        billingDocumentId: 30,
        billingDocumentStatus: 'Draft',
        salesOrderId: 20,
        fiscalDocumentId: 40,
        fiscalDocumentStatus: 'ReadyForStamping',
        associatedOrderCount: 1,
        total: 100
      })),
      removeBillingDocumentItem: vi.fn().mockReturnValue(of({
        outcome: 'Removed',
        isSuccess: true,
        billingDocumentId: 30,
        billingDocumentStatus: 'Draft',
        billingDocumentItemId: 501,
        fiscalDocumentId: 40,
        fiscalDocumentStatus: 'ReadyForStamping',
        removalId: 88,
        includedItemCount: 1,
        total: 100
      })),
      listPendingBillingItems: vi.fn().mockReturnValue(of([])),
      assignPendingBillingItems: vi.fn().mockReturnValue(of({
        outcome: 'Assigned',
        isSuccess: true,
        billingDocumentId: 30,
        fiscalDocumentId: 40,
        fiscalDocumentStatus: 'ReadyForStamping',
        assignedCount: 1,
        includedItemCount: 2,
        total: 158
      })),
      getFiscalDocumentById: vi.fn().mockReturnValue(of({
        id: 40,
        billingDocumentId: 30,
        issuerProfileId: 1,
        fiscalReceiverId: 9,
        status: 'Stamped',
        cfdiVersion: '4.0',
        documentType: 'I',
        series: 'A',
        folio: '31787',
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
      getStampPdf: vi.fn().mockReturnValue(of(new Blob(['%PDF-1.4'], { type: 'application/pdf' }))),
      getEmailDraft: vi.fn().mockReturnValue(of({
        outcome: 'Found',
        isSuccess: true,
        defaultRecipientEmail: 'cliente@example.com',
        suggestedSubject: 'CFDI timbrado A8',
        suggestedBody: 'Adjuntamos XML y PDF.'
      })),
      sendByEmail: vi.fn().mockReturnValue(of({
        outcome: 'Sent',
        isSuccess: true,
        fiscalDocumentId: 40,
        recipients: ['cliente@example.com'],
        sentAtUtc: '2026-03-24T12:10:00Z'
      })),
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
    productApiOverrides?: Partial<Record<keyof ProductFiscalProfilesApiService, unknown>>,
    receiverApiOverrides?: Partial<Record<keyof FiscalReceiversApiService, unknown>>,
    ordersApiOverrides?: Partial<Record<keyof OrdersApiService, unknown>>
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
          provide: OrdersApiService,
          useValue: {
            importLegacyOrder: vi.fn().mockReturnValue(of({
              outcome: 'Imported',
              isSuccess: true,
              isIdempotent: false,
              sourceSystem: 'legacy',
              sourceTable: 'pedidos',
              legacyOrderId: 'LEG-2002',
              sourceHash: 'hash',
              salesOrderId: 21,
              importStatus: 'Imported'
            })),
            ...ordersApiOverrides
          }
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
          provide: FiscalReceiversApiService,
          useValue: {
            search: vi.fn().mockReturnValue(of([])),
            getByRfc: vi.fn().mockReturnValue(of({
              id: 9,
              rfc: 'BBB010101BBB',
              legalName: 'Receiver One',
              postalCode: '02000',
              fiscalRegimeCode: '601',
              cfdiUseCodeDefault: 'G03',
              countryCode: 'MX',
              foreignTaxRegistration: null,
              email: 'cliente@example.com',
              phone: null,
              searchAlias: null,
              isActive: true,
              createdAtUtc: '2026-03-20T12:00:00Z',
              updatedAtUtc: '2026-03-20T12:00:00Z'
            })),
            getSatCatalog: vi.fn().mockReturnValue(of({
              regimenFiscal: [
                { code: '601', description: 'General de Ley Personas Morales' }
              ],
              usoCfdi: [
                { code: 'G03', description: 'Gastos en general' }
              ],
              paymentMethods: [
                { code: 'PUE', description: 'Pago en una sola exhibición' },
                { code: 'PPD', description: 'Pago en parcialidades o diferido' }
              ],
              paymentForms: [
                { code: '03', description: 'Transferencia electrónica de fondos' },
                { code: '28', description: 'Tarjeta de débito' },
                { code: '99', description: 'Por definir' }
              ],
              byRegimenFiscal: [
                {
                  code: '601',
                  description: 'General de Ley Personas Morales',
                  allowedUsoCfdi: [{ code: 'G03', description: 'Gastos en general' }]
                }
              ]
            })),
            create: vi.fn().mockReturnValue(of({ outcome: 'Created', isSuccess: true, id: 9 })),
            update: vi.fn(),
            ...receiverApiOverrides
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

  it('shows PDF and email actions only for stamped documents', async () => {
    const fixture = await configure();

    expect(fixture.nativeElement.textContent).toContain('Ver PDF');
    expect(fixture.nativeElement.textContent).toContain('Descargar PDF');
    expect(fixture.nativeElement.textContent).toContain('Enviar por correo');
  });

  it('downloads the stamped PDF with RFC plus series-folio plus receiver RFC', async () => {
    const fixture = await configure();
    const createObjectUrlSpy = vi.spyOn(window.URL, 'createObjectURL').mockReturnValue('blob:pdf');
    const revokeObjectUrlSpy = vi.spyOn(window.URL, 'revokeObjectURL').mockImplementation(() => undefined);
    const clickSpy = vi.fn();
    const originalCreateElement = document.createElement.bind(document);
    const createdAnchors: HTMLAnchorElement[] = [];
    const createElementSpy = vi.spyOn(document, 'createElement').mockImplementation(((tagName: string) => {
      if (tagName === 'a') {
        const anchor = {
          href: '',
          download: '',
          click: clickSpy
        } as unknown as HTMLAnchorElement;
        createdAnchors.push(anchor);
        return anchor;
      }

      return originalCreateElement(tagName);
    }) as typeof document.createElement);

    await fixture.componentInstance['handleStampPdf'](true);

    expect(createObjectUrlSpy).toHaveBeenCalled();
    expect(clickSpy).toHaveBeenCalled();
    expect(createdAnchors.at(0)?.download).toBe('AAA010101AAA_A31787_BBB010101BBB.pdf');

    createElementSpy.mockRestore();
    revokeObjectUrlSpy.mockRestore();
    createObjectUrlSpy.mockRestore();
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

  it('opens the email composer with preloaded recipient and suggested content', async () => {
    const fixture = await configure();

    await fixture.componentInstance['openEmailComposer']();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Enviar CFDI por correo');
    expect(fixture.componentInstance['emailRecipientsInput']).toBe('cliente@example.com');
    expect(fixture.componentInstance['emailSubject']).toBe('CFDI timbrado A8');
    expect(fixture.componentInstance['emailBody']).toContain('XML y PDF');
  });

  it('sends the stamped CFDI by email and shows confirmation', async () => {
    const sendByEmail = vi.fn().mockReturnValue(of({
      outcome: 'Sent',
      isSuccess: true,
      fiscalDocumentId: 40,
      recipients: ['cliente@example.com'],
      sentAtUtc: '2026-03-24T12:10:00Z'
    }));
    const fixture = await configure({ sendByEmail });
    await fixture.componentInstance['openEmailComposer']();
    fixture.componentInstance['emailRecipientsInput'] = 'cliente@example.com';
    fixture.componentInstance['emailSubject'] = 'CFDI timbrado A8';
    fixture.componentInstance['emailBody'] = 'Adjuntamos XML y PDF.';

    await fixture.componentInstance['sendEmail']();

    expect(sendByEmail).toHaveBeenCalledWith(40, {
      recipients: ['cliente@example.com'],
      subject: 'CFDI timbrado A8',
      body: 'Adjuntamos XML y PDF.'
    });
    expect(fixture.componentInstance['lastOperationMessage']()).toContain('CFDI enviado correctamente');
  });

  it('keeps the email composer open when sending fails', async () => {
    const fixture = await configure({
      sendByEmail: vi.fn().mockReturnValue(throwError(() => ({ error: { errorMessage: 'SMTP no disponible.' } })))
    });

    await fixture.componentInstance['openEmailComposer']();
    fixture.componentInstance['emailRecipientsInput'] = 'cliente@example.com';
    await fixture.componentInstance['sendEmail']();
    fixture.detectChanges();

    expect(fixture.componentInstance['showEmailComposer']()).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('SMTP no disponible.');
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

    await fixture.componentInstance['selectReceiver']({
      id: 9,
      rfc: 'BBB010101BBB',
      legalName: 'Receiver One',
      postalCode: '02000',
      fiscalRegimeCode: '601',
      cfdiUseCodeDefault: 'G03',
      isActive: true
    });
    await fixture.whenStable();
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
    expect(fixture.nativeElement.textContent).toContain('Agregar receptor');
  });

  it('renders SAT payment catalogs and disables prepare until the capture is valid', async () => {
    const fixture = await configure(undefined, { id: null, billingDocumentId: '30' });
    fixture.componentInstance['selectedReceiverId'] = 9;
    fixture.componentInstance['selectedReceiver'].set({
      id: 9,
      rfc: 'BBB010101BBB',
      legalName: 'Receiver One',
      postalCode: '02000',
      fiscalRegimeCode: '601',
      cfdiUseCodeDefault: 'G03',
      countryCode: 'MX',
      foreignTaxRegistration: null,
      email: 'cliente@example.com',
      phone: null,
      searchAlias: null,
      isActive: true,
      createdAtUtc: '2026-03-20T12:00:00Z',
      updatedAtUtc: '2026-03-20T12:00:00Z'
    });
    fixture.componentInstance['paymentMethodSat'] = '';
    fixture.componentInstance['paymentFormSat'] = '';
    fixture.componentInstance['paymentCondition'] = '';
    fixture.detectChanges();

    const submitButton = Array.from(fixture.nativeElement.querySelectorAll('button[type="submit"]') as NodeListOf<HTMLButtonElement>)
      .find((button: HTMLButtonElement) => button.textContent?.includes('Preparar documento fiscal')) as HTMLButtonElement;
    expect(fixture.nativeElement.textContent).toContain('PUE - Pago en una sola exhibición');
    expect(fixture.nativeElement.textContent).toContain('99 - Por definir');
    expect(submitButton.disabled).toBe(true);

    fixture.componentInstance['onCreditSaleChange'](false);
    fixture.componentInstance['onPaymentMethodChange']('PUE');
    fixture.componentInstance['onPaymentFormChange']('03');
    fixture.componentInstance['onPaymentConditionChange']('Contado');
    fixture.detectChanges();

    expect(fixture.componentInstance['canPrepareFiscalDocument']()).toBe(true);
  });

  it('forces payment form 99 and suggests credit condition when credit sale is enabled', async () => {
    const fixture = await configure(undefined, { id: null, billingDocumentId: '30' });

    fixture.componentInstance['onCreditDaysChange'](21);
    fixture.componentInstance['onCreditSaleChange'](true);

    expect(fixture.componentInstance['paymentMethodSat']).toBe('PPD');
    expect(fixture.componentInstance['paymentFormSat']).toBe('99');
    expect(fixture.componentInstance['paymentCondition']).toBe('Crédito a 21 días');
    expect(fixture.componentInstance['availablePaymentFormOptions']()).toEqual([
      { code: '99', description: 'Por definir' }
    ]);
  });

  it('sends only SAT codes plus payment condition text when preparing', async () => {
    const prepareFiscalDocument = vi.fn().mockReturnValue(of({
      outcome: 'Created',
      isSuccess: true,
      fiscalDocumentId: 40
    }));
    const fixture = await configure(
      { prepareFiscalDocument },
      { id: null, billingDocumentId: '30' }
    );

    fixture.componentInstance['selectedReceiverId'] = 9;
    fixture.componentInstance['onCreditSaleChange'](false);
    fixture.componentInstance['onPaymentMethodChange']('PUE');
    fixture.componentInstance['onPaymentFormChange']('03');
    fixture.componentInstance['onPaymentConditionChange']('Contado');

    await fixture.componentInstance['prepare']();

    expect(prepareFiscalDocument).toHaveBeenCalledWith(30, expect.objectContaining({
      fiscalReceiverId: 9,
      paymentMethodSat: 'PUE',
      paymentFormSat: '03',
      paymentCondition: 'Contado'
    }));
  });

  it('shows SAT cancellation reasons with code plus description in the cancellation dialog', async () => {
    const fixture = await configure();

    fixture.componentInstance['openCancelDialog']();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('01 - Comprobante emitido con errores con relación');
    expect(fixture.nativeElement.textContent).toContain('02 - Comprobante emitido con errores sin relación');
    expect(fixture.nativeElement.textContent).toContain('03 - No se llevó a cabo la operación');
    expect(fixture.nativeElement.textContent).toContain('04 - Operación nominativa relacionada en una factura global');
  });

  it('requires replacementUuid only for reason 01 and clears it when switching to another reason', async () => {
    const fixture = await configure();

    fixture.componentInstance['openCancelDialog']();
    fixture.componentInstance['onCancellationReasonChange']('01');
    fixture.detectChanges();

    expect(fixture.componentInstance['requiresCancellationReplacementUuid']()).toBe(true);
    expect(fixture.componentInstance['getCancellationValidationError']()).toContain('UUID de sustitución');

    fixture.componentInstance['onCancellationReplacementUuidChange']('UUID-SUSTITUTO');
    expect(fixture.componentInstance['getCancellationValidationError']()).toBeNull();

    fixture.componentInstance['onCancellationReasonChange']('02');
    fixture.detectChanges();

    expect(fixture.componentInstance['requiresCancellationReplacementUuid']()).toBe(false);
    expect(fixture.componentInstance['cancellationReplacementUuid']).toBe('');
    expect(fixture.componentInstance['getCancellationValidationError']()).toBeNull();
  });

  it('submits cancellation with SAT reason and replacementUuid only when reason 01 applies', async () => {
    const cancelFiscalDocument = vi.fn().mockReturnValue(of({
      outcome: 'Cancelled',
      isSuccess: true,
      fiscalDocumentId: 40,
      fiscalDocumentStatus: 'Cancelled',
      fiscalCancellationId: 90,
      cancellationStatus: 'Cancelled',
      providerName: 'FacturaloPlus',
      providerTrackingId: 'TRACK-CANCEL-1',
      cancelledAtUtc: '2026-03-28T12:00:00Z'
    }));
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const fixture = await configure({ cancelFiscalDocument });

    fixture.componentInstance['openCancelDialog']();
    fixture.componentInstance['onCancellationReasonChange']('01');
    fixture.componentInstance['onCancellationReplacementUuidChange']('UUID-SUSTITUTO');

    await fixture.componentInstance['cancel']();

    expect(confirmSpy).toHaveBeenCalled();
    expect(cancelFiscalDocument).toHaveBeenCalledWith(40, {
      cancellationReasonCode: '01',
      replacementUuid: 'UUID-SUSTITUTO'
    });
    expect(fixture.componentInstance['showCancelDialog']()).toBe(false);

    confirmSpy.mockRestore();
  });

  it('reconciles the local cancellation card immediately after a successful cancellation', async () => {
    const cancelFiscalDocument = vi.fn().mockReturnValue(of({
      outcome: 'Cancelled',
      isSuccess: true,
      fiscalDocumentId: 40,
      fiscalDocumentStatus: 'Cancelled',
      fiscalCancellationId: 90,
      cancellationStatus: 'Cancelled',
      providerName: 'FacturaloPlus',
      providerTrackingId: 'TRACK-CANCEL-201',
      providerCode: '201',
      providerMessage: 'Solicitud de cancelación de UUID exitosa. - ',
      errorCode: null,
      rawResponseSummaryJson: '{"httpStatusCode":200}',
      supportMessage: 'ProviderCode=201 | ProviderMessage=Solicitud de cancelación de UUID exitosa. - ',
      cancelledAtUtc: '2026-03-29T18:40:00Z'
    }));
    const getCancellation = vi.fn().mockReturnValue(of({
      fiscalDocumentId: 40,
      status: 'Rejected',
      cancellationReasonCode: '03',
      replacementUuid: null,
      providerName: 'FacturaloPlus',
      providerTrackingId: null,
      providerCode: '404',
      providerMessage: 'Viejo rechazo',
      errorCode: '404',
      errorMessage: 'Old error',
      supportMessage: 'old',
      rawResponseSummaryJson: '{"old":true}',
      requestedAtUtc: '2026-03-29T12:00:00Z',
      cancelledAtUtc: null
    }));
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const fixture = await configure({ cancelFiscalDocument, getCancellation });

    fixture.componentInstance['openCancelDialog']();
    fixture.componentInstance['onCancellationReasonChange']('03');

    await fixture.componentInstance['cancel']();

    expect(fixture.componentInstance['fiscalDocument']()?.status).toBe('Cancelled');
    expect(fixture.componentInstance['cancellation']()?.status).toBe('Cancelled');
    expect(fixture.componentInstance['cancellation']()?.providerCode).toBe('201');
    expect(fixture.componentInstance['lastOperationMessage']()).toContain('Cancelación exitosa');
    expect(fixture.componentInstance['canCancelCurrentFiscalDocument']()).toBe(false);
    confirmSpy.mockRestore();
  });

  it('allows reopening cancellation when the document is in CancellationRejected', async () => {
    const fixture = await configure({
      getFiscalDocumentById: vi.fn().mockReturnValue(of({
        id: 40,
        billingDocumentId: 30,
        issuerProfileId: 1,
        fiscalReceiverId: 9,
        status: 'CancellationRejected',
        cfdiVersion: '4.0',
        documentType: 'I',
        series: 'A',
        folio: '31787',
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
      getCancellation: vi.fn().mockReturnValue(of({
        fiscalDocumentId: 40,
        status: 'Rejected',
        cancellationReasonCode: '03',
        replacementUuid: null,
        providerName: 'FacturaloPlus',
        providerTrackingId: null,
        providerCode: '203',
        providerMessage: 'Solicitud rechazada',
        errorCode: '203',
        errorMessage: 'Provider rejected the cancellation request.',
        supportMessage: 'ProviderCode=203 | ProviderMessage=Solicitud rechazada',
        rawResponseSummaryJson: '{"httpStatusCode":400}',
        requestedAtUtc: '2026-03-29T12:00:00Z',
        cancelledAtUtc: null
      }))
    });

    expect(fixture.componentInstance['canCancelCurrentFiscalDocument']()).toBe(true);

    fixture.componentInstance['openCancelDialog']();
    fixture.detectChanges();

    expect(fixture.componentInstance['showCancelDialog']()).toBe(true);
  });

  it('shows PAC diagnostic details after a rejected cancellation attempt', async () => {
    const cancelFiscalDocument = vi.fn().mockReturnValue(of({
      outcome: 'ProviderRejected',
      isSuccess: false,
      errorMessage: 'Provider rejected the cancellation request.',
      fiscalDocumentId: 40,
      fiscalDocumentStatus: 'CancellationRejected',
      fiscalCancellationId: 90,
      cancellationStatus: 'Rejected',
      providerName: 'FacturaloPlus',
      providerTrackingId: null,
      providerCode: '203',
      providerMessage: 'Solicitud rechazada',
      errorCode: '203',
      rawResponseSummaryJson: '{"httpStatusCode":400}',
      supportMessage: 'ProviderCode=203 | ProviderMessage=Solicitud rechazada',
      cancelledAtUtc: null
    }));
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const fixture = await configure({ cancelFiscalDocument });

    fixture.componentInstance['openCancelDialog']();
    fixture.componentInstance['onCancellationReasonChange']('03');

    await fixture.componentInstance['cancel']();

    expect(fixture.componentInstance['lastOperationMessage']()).toContain('Solicitud rechazada');
    confirmSpy.mockRestore();
  });

  it('prefers the operational SAT message when refreshing status', async () => {
    const refreshStatus = vi.fn().mockReturnValue(of({
      outcome: 'Refreshed',
      isSuccess: true,
      fiscalDocumentId: 40,
      fiscalDocumentStatus: 'CancellationRequested',
      uuid: 'UUID-123',
      lastKnownExternalStatus: 'Vigente',
      providerCode: 'S - Comprobante obtenido satisfactoriamente.',
      providerMessage: 'Estado=Vigente | EstatusCancelacion=En proceso',
      operationalStatus: 'CancellationPending',
      operationalMessage: 'La cancelación fue solicitada y sigue en proceso en SAT.',
      supportMessage: 'CodigoEstatus=S - Comprobante obtenido satisfactoriamente. | Estado=Vigente | EstatusCancelacion=En proceso',
      checkedAtUtc: '2026-03-29T10:00:00Z'
    }));
    const fixture = await configure({ refreshStatus });

    await fixture.componentInstance['refreshStatus']();

    expect(refreshStatus).toHaveBeenCalledWith(40);
    expect(fixture.componentInstance['lastOperationMessage']()).toContain('sigue en proceso en SAT');
  });

  it('disables refresh-status when the document has no stamped UUID evidence', async () => {
    const fixture = await configure({
      getFiscalDocumentById: vi.fn().mockReturnValue(of({
        id: 40,
        billingDocumentId: 30,
        issuerProfileId: 1,
        fiscalReceiverId: 9,
        status: 'Prepared',
        cfdiVersion: '4.0',
        documentType: 'I',
        series: 'A',
        folio: '31787',
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
      getStamp: vi.fn().mockReturnValue(throwError(() => ({ status: 404 })))
    });

    fixture.detectChanges();

    expect(fixture.componentInstance['canRefreshCurrentFiscalDocument']()).toBe(false);
    expect(fixture.nativeElement.textContent).toContain('Actualizar estatus solo está disponible para CFDI timbrados con UUID.');
  });

  it('renders dynamic special billing fields for the selected receiver', async () => {
    const fixture = await configure(
      undefined,
      { id: null, billingDocumentId: '30' },
      undefined,
      {
        getByRfc: vi.fn().mockReturnValue(of({
          id: 9,
          rfc: 'BBB010101BBB',
          legalName: 'Receiver One',
          postalCode: '02000',
          fiscalRegimeCode: '601',
          cfdiUseCodeDefault: 'G03',
          countryCode: 'MX',
          foreignTaxRegistration: null,
          email: 'cliente@example.com',
          phone: null,
          searchAlias: null,
          isActive: true,
          createdAtUtc: '2026-03-20T12:00:00Z',
          updatedAtUtc: '2026-03-20T12:00:00Z',
          specialFields: [
            {
              id: 31,
              code: 'AGENTE',
              label: 'Agente',
              dataType: 'text',
              maxLength: 80,
              helpText: 'Nombre del agente',
              isRequired: true,
              isActive: true,
              displayOrder: 1
            },
            {
              id: 32,
              code: 'ORDEN_TRABAJO',
              label: 'Orden de trabajo',
              dataType: 'text',
              maxLength: 30,
              helpText: null,
              isRequired: false,
              isActive: true,
              displayOrder: 2
            }
          ]
        }))
      }
    );

    await fixture.componentInstance['selectReceiver']({
      id: 9,
      rfc: 'BBB010101BBB',
      legalName: 'Receiver One',
      postalCode: '02000',
      fiscalRegimeCode: '601',
      cfdiUseCodeDefault: 'G03',
      isActive: true
    });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Campos especiales de facturación');
    expect(fixture.nativeElement.textContent).toContain('Agente');
    expect(fixture.nativeElement.textContent).toContain('Orden de trabajo');
  });

  it('blocks prepare when a required special billing field is missing', async () => {
    const prepareFiscalDocument = vi.fn();
    const fixture = await configure(
      { prepareFiscalDocument },
      { id: null, billingDocumentId: '30' },
      undefined,
      {
        getByRfc: vi.fn().mockReturnValue(of({
          id: 9,
          rfc: 'BBB010101BBB',
          legalName: 'Receiver One',
          postalCode: '02000',
          fiscalRegimeCode: '601',
          cfdiUseCodeDefault: 'G03',
          countryCode: 'MX',
          foreignTaxRegistration: null,
          email: 'cliente@example.com',
          phone: null,
          searchAlias: null,
          isActive: true,
          createdAtUtc: '2026-03-20T12:00:00Z',
          updatedAtUtc: '2026-03-20T12:00:00Z',
          specialFields: [
            {
              id: 31,
              code: 'AGENTE',
              label: 'Agente',
              dataType: 'text',
              maxLength: 80,
              helpText: null,
              isRequired: true,
              isActive: true,
              displayOrder: 1
            }
          ]
        }))
      }
    );
    const feedback = TestBed.inject(FeedbackService) as unknown as { show: ReturnType<typeof vi.fn> };

    await fixture.componentInstance['selectReceiver']({
      id: 9,
      rfc: 'BBB010101BBB',
      legalName: 'Receiver One',
      postalCode: '02000',
      fiscalRegimeCode: '601',
      cfdiUseCodeDefault: 'G03',
      isActive: true
    });
    await fixture.whenStable();

    await fixture.componentInstance['prepare']();

    expect(prepareFiscalDocument).not.toHaveBeenCalled();
    expect(feedback.show).toHaveBeenCalledWith('error', "El campo especial 'Agente' es requerido.");
  });

  it('opens the receiver creation modal with the searched RFC preloaded', async () => {
    const fixture = await configure(undefined, { id: null, billingDocumentId: '30' });

    fixture.componentInstance['receiverQuery'].set('XAXX010101000');
    fixture.componentInstance['receiverSearchTouched'].set(true);
    fixture.componentInstance['receiverResults'].set([]);
    fixture.componentInstance['openReceiverCreateModal']();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Nuevo receptor');
    expect(fixture.componentInstance['receiverCreateDraft']()?.rfc).toBe('XAXX010101000');
  });

  it('creates a receiver from the modal and selects it automatically', async () => {
    const create = vi.fn().mockReturnValue(of({ outcome: 'Created', isSuccess: true, id: 12 }));
    const getByRfc = vi.fn().mockReturnValue(of({
      id: 12,
      rfc: 'XAXX010101000',
      legalName: 'Publico General',
      postalCode: '01000',
      fiscalRegimeCode: '616',
      cfdiUseCodeDefault: 'S01',
      countryCode: 'MX',
      foreignTaxRegistration: null,
      email: null,
      phone: null,
      searchAlias: null,
      isActive: true,
      createdAtUtc: '2026-03-20T12:00:00Z',
      updatedAtUtc: '2026-03-20T12:00:00Z'
    }));
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
        { provide: FiscalDocumentsApiService, useValue: createApi() },
        { provide: ProductFiscalProfilesApiService, useValue: { create: vi.fn().mockReturnValue(of({ outcome: 'Created', isSuccess: true, id: 15 })) } },
        { provide: FiscalReceiversApiService, useValue: { search: vi.fn(), create, getByRfc, update: vi.fn() } },
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

    fixture.componentInstance['receiverQuery'].set('XAXX010101000');
    fixture.componentInstance['openReceiverCreateModal']();
    await fixture.componentInstance['saveReceiver']({
      rfc: 'XAXX010101000',
      legalName: 'Publico General',
      fiscalRegimeCode: '616',
      cfdiUseCodeDefault: 'S01',
      postalCode: '01000',
      countryCode: 'MX',
      foreignTaxRegistration: null,
      email: null,
      phone: null,
      searchAlias: null,
      isActive: true,
      specialFields: []
    });
    fixture.detectChanges();

    expect(create).toHaveBeenCalled();
    expect(getByRfc).toHaveBeenCalledWith('XAXX010101000');
    expect(fixture.componentInstance['selectedReceiverId']).toBe(12);
    expect(fixture.componentInstance['showReceiverCreateModal']()).toBe(false);
    expect(fixture.nativeElement.textContent).toContain('XAXX010101000 · Publico General');
    expect(feedback.show).toHaveBeenCalledWith('success', 'Receptor creado y seleccionado.');
  });

  it('keeps the receiver creation modal open and preserves data when creation fails', async () => {
    const fixture = await configure(
      undefined,
      { id: null, billingDocumentId: '30' },
      undefined,
      {
        create: vi.fn().mockReturnValue(throwError(() => ({ error: { errorMessage: 'RFC ya existe.' } })))
      }
    );

    fixture.componentInstance['receiverQuery'].set('XAXX010101000');
    fixture.componentInstance['openReceiverCreateModal']();
    await fixture.componentInstance['saveReceiver']({
      rfc: 'XAXX010101000',
      legalName: 'Publico General',
      fiscalRegimeCode: '616',
      cfdiUseCodeDefault: 'S01',
      postalCode: '01000',
      countryCode: 'MX',
      foreignTaxRegistration: null,
      email: 'cliente@example.com',
      phone: null,
      searchAlias: null,
      isActive: true,
      specialFields: []
    });
    fixture.detectChanges();

    expect(fixture.componentInstance['showReceiverCreateModal']()).toBe(true);
    expect(fixture.componentInstance['receiverCreateDraft']()?.rfc).toBe('XAXX010101000');
    expect(fixture.nativeElement.textContent).toContain('RFC ya existe.');
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
        {
          provide: FiscalReceiversApiService,
          useValue: {
            search: vi.fn().mockReturnValue(of([])),
            getByRfc: vi.fn(),
            getSatCatalog: vi.fn().mockReturnValue(of({
              regimenFiscal: [{ code: '601', description: 'General de Ley Personas Morales' }],
              usoCfdi: [{ code: 'G03', description: 'Gastos en general' }],
              paymentMethods: [
                { code: 'PUE', description: 'Pago en una sola exhibición' },
                { code: 'PPD', description: 'Pago en parcialidades o diferido' }
              ],
              paymentForms: [
                { code: '03', description: 'Transferencia electrónica de fondos' },
                { code: '99', description: 'Por definir' }
              ],
              byRegimenFiscal: [
                {
                  code: '601',
                  description: 'General de Ley Personas Morales',
                  allowedUsoCfdi: [{ code: 'G03', description: 'Gastos en general' }]
                }
              ]
            })),
            create: vi.fn(),
            update: vi.fn()
          }
        },
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
    expect(fixture.componentInstance['missingProductFiscalProfile']()?.description).toBe('FILTRO DE ACEITE');
    expect(fixture.componentInstance['missingProductFiscalProfile']()?.draft.description).toBe('FILTRO DE ACEITE');
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
        {
          provide: FiscalReceiversApiService,
          useValue: {
            search: vi.fn().mockReturnValue(of([])),
            getByRfc: vi.fn(),
            getSatCatalog: vi.fn().mockReturnValue(of({
              regimenFiscal: [{ code: '601', description: 'General de Ley Personas Morales' }],
              usoCfdi: [{ code: 'G03', description: 'Gastos en general' }],
              paymentMethods: [
                { code: 'PUE', description: 'Pago en una sola exhibición' },
                { code: 'PPD', description: 'Pago en parcialidades o diferido' }
              ],
              paymentForms: [
                { code: '03', description: 'Transferencia electrónica de fondos' },
                { code: '99', description: 'Por definir' }
              ],
              byRegimenFiscal: [
                {
                  code: '601',
                  description: 'General de Ley Personas Morales',
                  allowedUsoCfdi: [{ code: 'G03', description: 'Gastos en general' }]
                }
              ]
            })),
            create: vi.fn(),
            update: vi.fn()
          }
        },
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
      description: 'FILTRO DE ACEITE',
      satProductServiceCode: '01010101',
      satUnitCode: 'H87',
      taxObjectCode: '02',
      vatRate: 0.16,
      defaultUnitText: 'PIEZA',
      isActive: true
    });

    expect(create).toHaveBeenCalledWith({
      internalCode: 'MTE-4259',
      description: 'FILTRO DE ACEITE',
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
      description: 'FILTRO DE ACEITE',
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

  it('falls back to internal code when the billing document item description is not available', async () => {
    const prepareFiscalDocument = vi.fn().mockReturnValue(throwError(() => ({
      status: 400,
      error: {
        outcome: 'MissingProductFiscalProfile',
        isSuccess: false,
        errorMessage: "No active product fiscal profile exists for item line '1' and internal code 'GP-149'.",
        billingDocumentId: 30,
        fiscalDocumentId: null,
        status: null
      }
    })));

    const fixture = await configure({
      prepareFiscalDocument,
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
        fiscalDocumentStatus: null,
        items: []
      }))
    }, { id: null, billingDocumentId: '30' });

    fixture.componentInstance['selectedReceiverId'] = 9;
    await fixture.componentInstance['prepare']();

    expect(fixture.componentInstance['missingProductFiscalProfile']()?.description).toBe('GP-149');
    expect(fixture.componentInstance['missingProductFiscalProfile']()?.draft.description).toBe('GP-149');
  });

  it('adds another legacy order to the current billing document before stamping', async () => {
    const importLegacyOrder = vi.fn().mockReturnValue(of({
      outcome: 'Imported',
      isSuccess: true,
      isIdempotent: false,
      sourceSystem: 'legacy',
      sourceTable: 'pedidos',
      legacyOrderId: 'LEG-2002',
      sourceHash: 'hash',
      salesOrderId: 21,
      importStatus: 'Imported'
    }));
    const addSalesOrderToBillingDocument = vi.fn().mockReturnValue(of({
      outcome: 'Updated',
      isSuccess: true,
      billingDocumentId: 30,
      billingDocumentStatus: 'Draft',
      salesOrderId: 21,
      fiscalDocumentId: 40,
      fiscalDocumentStatus: 'ReadyForStamping',
      associatedOrderCount: 2,
      total: 200
    }));
    const fixture = await configure({
      addSalesOrderToBillingDocument,
      getFiscalDocumentById: vi.fn().mockReturnValue(of({
        id: 40,
        billingDocumentId: 30,
        issuerProfileId: 1,
        fiscalReceiverId: 9,
        status: 'ReadyForStamping',
        cfdiVersion: '4.0',
        documentType: 'I',
        series: 'A',
        folio: '31787',
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
        taxTotal: 16,
        total: 116,
        items: []
      }))
    }, undefined, undefined, undefined, { importLegacyOrder });

    fixture.componentInstance['additionalLegacyOrderId'] = 'LEG-2002';
    await fixture.componentInstance['addLegacyOrderToBillingDocument']();

    expect(importLegacyOrder).toHaveBeenCalledWith('LEG-2002');
    expect(addSalesOrderToBillingDocument).toHaveBeenCalledWith(30, 21);
  });

  it('removes an associated order from the current billing document before stamping', async () => {
    const removeSalesOrderFromBillingDocument = vi.fn().mockReturnValue(of({
      outcome: 'Updated',
      isSuccess: true,
      billingDocumentId: 30,
      billingDocumentStatus: 'Draft',
      salesOrderId: 21,
      fiscalDocumentId: 40,
      fiscalDocumentStatus: 'ReadyForStamping',
      associatedOrderCount: 1,
      total: 116
    }));
    const getBillingDocumentById = vi.fn().mockReturnValue(of({
      billingDocumentId: 30,
      salesOrderId: 20,
      legacyOrderId: 'LEG-1001-ORD-LEG-1001',
      status: 'Draft',
      documentType: 'I',
      currencyCode: 'MXN',
      total: 174,
      createdAtUtc: '2026-03-20T12:00:00Z',
      fiscalDocumentId: 40,
      fiscalDocumentStatus: 'ReadyForStamping',
      associatedOrders: [
        { salesOrderId: 20, legacyOrderId: 'LEG-1001-ORD-LEG-1001', customerName: 'Receiver One', total: 116, isPrimary: true },
        { salesOrderId: 21, legacyOrderId: 'LEG-2002-ORD-LEG-2002', customerName: 'Receiver One', total: 58, isPrimary: false }
      ],
      items: []
    }));
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const fixture = await configure({
      removeSalesOrderFromBillingDocument,
      getBillingDocumentById,
      getFiscalDocumentById: vi.fn().mockReturnValue(of({
        id: 40,
        billingDocumentId: 30,
        issuerProfileId: 1,
        fiscalReceiverId: 9,
        status: 'ReadyForStamping',
        cfdiVersion: '4.0',
        documentType: 'I',
        series: 'A',
        folio: '31787',
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
        subtotal: 150,
        discountTotal: 0,
        taxTotal: 24,
        total: 174,
        items: []
      }))
    });

    await fixture.componentInstance['removeAssociatedOrder'](21);

    expect(removeSalesOrderFromBillingDocument).toHaveBeenCalledWith(30, 21);
    confirmSpy.mockRestore();
  });

  it('removes a complete billing line with reason and disposition before stamping', async () => {
    const removeBillingDocumentItem = vi.fn().mockReturnValue(of({
      outcome: 'Removed',
      isSuccess: true,
      billingDocumentId: 30,
      billingDocumentStatus: 'Draft',
      billingDocumentItemId: 501,
      fiscalDocumentId: 40,
      fiscalDocumentStatus: 'ReadyForStamping',
      removalId: 91,
      includedItemCount: 1,
      total: 58
    }));
    const fixture = await configure({
      removeBillingDocumentItem,
      getFiscalDocumentById: vi.fn().mockReturnValue(of({
        id: 40,
        billingDocumentId: 30,
        issuerProfileId: 1,
        fiscalReceiverId: 9,
        status: 'ReadyForStamping',
        cfdiVersion: '4.0',
        documentType: 'I',
        series: 'A',
        folio: '31787',
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
        taxTotal: 16,
        total: 116,
        items: []
      }))
    });

    fixture.componentInstance['openRemoveBillingItemDialog']({
      billingDocumentItemId: 501,
      salesOrderId: 20,
      salesOrderItemId: 601,
      sourceSalesOrderLineNumber: 1,
      sourceLegacyOrderId: 'LEG-1001-ORD-LEG-1001',
      lineNumber: 1,
      productInternalCode: 'MTE-4259',
      description: 'FILTRO DE ACEITE',
      quantity: 1,
      total: 116
    });
    fixture.componentInstance['onBillingItemRemovalReasonChange']('WrongDocument');
    fixture.componentInstance['onBillingItemRemovalDispositionChange']('PendingBilling');
    fixture.componentInstance['onBillingItemRemovalObservationsChange']('Se facturará aparte');

    await fixture.componentInstance['confirmRemoveBillingItem']();

    expect(removeBillingDocumentItem).toHaveBeenCalledWith(30, 501, {
      removalReason: 'WrongDocument',
      observations: 'Se facturará aparte',
      removalDisposition: 'PendingBilling'
    });
  });

  it('enables removal confirmation when product reason and disposition are selected', async () => {
    const fixture = await configure({
      getFiscalDocumentById: vi.fn().mockReturnValue(of({
        id: 40,
        billingDocumentId: 30,
        issuerProfileId: 1,
        fiscalReceiverId: 9,
        status: 'ReadyForStamping',
        cfdiVersion: '4.0',
        documentType: 'I',
        series: 'A',
        folio: '31787',
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
        taxTotal: 16,
        total: 116,
        items: []
      }))
    });

    fixture.componentInstance['openRemoveBillingItemDialog']({
      billingDocumentItemId: 501,
      salesOrderId: 20,
      salesOrderItemId: 601,
      sourceSalesOrderLineNumber: 1,
      sourceLegacyOrderId: 'LEG-1001-ORD-LEG-1001',
      lineNumber: 1,
      productInternalCode: 'MTE-4259',
      description: 'FILTRO DE ACEITE',
      quantity: 1,
      total: 116
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Quitar producto completo');
    expect(fixture.nativeElement.textContent).toContain('Selecciona el motivo base del producto removido.');

    fixture.componentInstance['onBillingItemRemovalReasonChange']('WrongDocument');
    fixture.componentInstance['onBillingItemRemovalDispositionChange']('PendingBilling');
    fixture.detectChanges();

    await fixture.whenStable();
    fixture.detectChanges();

    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[];
    const confirmButton = buttons.find((button) => button.textContent?.includes('Confirmar remoción'));

    expect(confirmButton).toBeTruthy();
    expect(fixture.componentInstance['billingItemRemovalValidationError']()).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('Selecciona el motivo base del producto removido.');
  });

  it('assigns selected pending billing items to the current editable document', async () => {
    const assignPendingBillingItems = vi.fn().mockReturnValue(of({
      outcome: 'Assigned',
      isSuccess: true,
      billingDocumentId: 30,
      fiscalDocumentId: 40,
      fiscalDocumentStatus: 'ReadyForStamping',
      assignedCount: 2,
      includedItemCount: 3,
      total: 174
    }));

    const fixture = await configure(
      {
        assignPendingBillingItems,
        listPendingBillingItems: vi.fn().mockReturnValue(of([
          {
            removalId: 91,
            billingDocumentId: 29,
            fiscalDocumentId: 39,
            salesOrderId: 19,
            salesOrderItemId: 601,
            sourceLegacyOrderId: 'LEG-5001-ORD-LEG-5001',
            customerName: 'Receiver One',
            sourceSalesOrderLineNumber: 2,
            productInternalCode: 'MTE-4259',
            description: 'FILTRO DE ACEITE',
            quantityRemoved: 1,
            removalReason: 'WrongDocument',
            observations: 'Se facturará aparte',
            removalDisposition: 'PendingBilling',
            removedAtUtc: '2026-03-30T12:00:00Z'
          },
          {
            removalId: 92,
            billingDocumentId: 28,
            fiscalDocumentId: 38,
            salesOrderId: 18,
            salesOrderItemId: 602,
            sourceLegacyOrderId: 'LEG-5002-ORD-LEG-5002',
            customerName: 'Receiver One',
            sourceSalesOrderLineNumber: 3,
            productInternalCode: 'PS-317',
            description: 'PASTILLA DE FRENO',
            quantityRemoved: 1,
            removalReason: 'CommercialValidationPending',
            observations: null,
            removalDisposition: 'PendingBilling',
            removedAtUtc: '2026-03-30T13:00:00Z'
          }
        ]))
      },
      { id: null, billingDocumentId: '30' }
    );

    await fixture.componentInstance['loadBillingDocumentContext'](30);
    fixture.detectChanges();

    fixture.componentInstance['togglePendingBillingSelection'](91, true);
    fixture.componentInstance['togglePendingBillingSelection'](92, true);

    await fixture.componentInstance['assignSelectedPendingBillingItems']();

    expect(assignPendingBillingItems).toHaveBeenCalledWith(30, { removalIds: [91, 92] });
  });
});
