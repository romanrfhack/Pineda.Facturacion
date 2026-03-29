import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { IssuedCfdisPageComponent } from './issued-cfdis-page.component';
import { FiscalDocumentsApiService } from '../../fiscal-documents/infrastructure/fiscal-documents-api.service';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { PermissionService } from '../../../core/auth/permission.service';

describe('IssuedCfdisPageComponent', () => {
  function createApi(overrides?: Partial<Record<keyof FiscalDocumentsApiService, unknown>>) {
    return {
      searchIssued: vi.fn().mockReturnValue(of({
        page: 1,
        pageSize: 25,
        totalCount: 1,
        totalPages: 1,
        items: [
          {
            fiscalDocumentId: 40,
            billingDocumentId: 30,
            status: 'Stamped',
            issuedAtUtc: '2026-03-24T12:00:00Z',
            stampedAtUtc: '2026-03-24T12:05:00Z',
            issuerRfc: 'AAA010101AAA',
            issuerLegalName: 'Issuer SA',
            series: 'A',
            folio: '31787',
            uuid: 'UUID-123',
            receiverRfc: 'BBB010101BBB',
            receiverLegalName: 'Receiver One',
            receiverCfdiUseCode: 'G03',
            paymentMethodSat: 'PPD',
            paymentFormSat: '99',
            documentType: 'I',
            total: 116
          }
        ]
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
        issuedAtUtc: '2026-03-24T12:00:00Z',
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
        items: [],
        specialFields: [
          {
            id: 1,
            fiscalReceiverSpecialFieldDefinitionId: 31,
            fieldCode: 'AGENTE',
            fieldLabelSnapshot: 'Agente',
            dataType: 'text',
            value: 'Juan Pérez',
            displayOrder: 1
          },
          {
            id: 2,
            fiscalReceiverSpecialFieldDefinitionId: 32,
            fieldCode: 'ORDEN_TRABAJO',
            fieldLabelSnapshot: 'Orden de trabajo',
            dataType: 'text',
            value: 'OT-45678',
            displayOrder: 2
          }
        ]
      })),
      getStamp: vi.fn().mockReturnValue(of({
        id: 11,
        fiscalDocumentId: 40,
        providerName: 'FacturaloPlus',
        providerOperation: 'stamp',
        providerTrackingId: 'TRACK-1-ABCDEF1234567890-ABCDEFGHIJKLMNOPQRSTUVWXYZ-1234567890',
        status: 'Stamped',
        uuid: 'UUID-123',
        stampedAtUtc: '2026-03-24T12:05:00Z',
        providerCode: '200',
        providerMessage: 'Stamped',
        errorCode: null,
        errorMessage: null,
        xmlHash: 'HASH-1',
        qrCodeTextOrUrl: 'https://sat.example/qr',
        originalString: '||1.1|UUID-123||',
        createdAtUtc: '2026-03-24T12:05:00Z',
        updatedAtUtc: '2026-03-24T12:05:00Z'
      })),
      getCancellation: vi.fn().mockReturnValue(throwError(() => ({ status: 404 }))),
      getStampXml: vi.fn().mockReturnValue(of('<cfdi:Comprobante />')),
      getStampXmlFile: vi.fn().mockReturnValue(of(new Blob(['<cfdi:Comprobante />'], { type: 'application/xml' }))),
      getStampPdf: vi.fn().mockReturnValue(of(new Blob(['%PDF-1.4'], { type: 'application/pdf' }))),
      getEmailDraft: vi.fn().mockReturnValue(of({
        outcome: 'Found',
        isSuccess: true,
        defaultRecipientEmail: 'cliente@example.com',
        suggestedSubject: 'CFDI A31787',
        suggestedBody: 'Adjuntamos CFDI.'
      })),
      sendByEmail: vi.fn().mockReturnValue(of({
        outcome: 'Sent',
        isSuccess: true,
        fiscalDocumentId: 40,
        recipients: ['cliente@example.com'],
        sentAtUtc: '2026-03-24T12:10:00Z'
      })),
      ...overrides
    };
  }

  async function configure(apiOverrides?: Partial<Record<keyof FiscalDocumentsApiService, unknown>>) {
    await TestBed.configureTestingModule({
      imports: [IssuedCfdisPageComponent],
      providers: [
        { provide: FiscalDocumentsApiService, useValue: createApi(apiOverrides) },
        { provide: FeedbackService, useValue: { show: vi.fn() } },
        { provide: PermissionService, useValue: { canCancelFiscal: () => true } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(IssuedCfdisPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    await Promise.resolve();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('loads and renders the issued CFDI grid', async () => {
    const fixture = await configure();
    await fixture.componentInstance['load']();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('UUID-123');
    expect(fixture.nativeElement.textContent).toContain('BBB010101BBB');
    expect(fixture.nativeElement.textContent).toContain('Receiver One');
    expect(fixture.nativeElement.textContent).toContain('Ver detalle');
    expect(fixture.nativeElement.textContent).not.toContain('Descargar PDF');
    expect(fixture.nativeElement.textContent).not.toContain('Reenviar por correo');
  });

  it('applies filters through the paged issued-search endpoint', async () => {
    const searchIssued = vi.fn().mockReturnValue(of({
      page: 1,
      pageSize: 25,
      totalCount: 0,
      totalPages: 0,
      items: []
    }));
    const fixture = await configure({ searchIssued });

    fixture.componentInstance['receiverRfc'] = 'BBB010101BBB';
    fixture.componentInstance['status'] = 'Stamped';
    await fixture.componentInstance['applyFilters']();

    expect(searchIssued).toHaveBeenCalledWith(expect.objectContaining({
      page: 1,
      pageSize: 25,
      receiverRfc: 'BBB010101BBB',
      status: 'Stamped'
    }));
  });

  it('opens the detail in a modal and hides the fixed summary sections from the page', async () => {
    const fixture = await configure();

    expect(fixture.nativeElement.textContent).not.toContain('Detalle de CFDI emitido');

    await fixture.componentInstance['openDetailModal'](fixture.componentInstance['items']()[0]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Detalle de CFDI emitido');
    expect(fixture.nativeElement.textContent).toContain('Snapshot fiscal');
    expect(fixture.nativeElement.textContent).toContain('Evidencia de timbrado');
    expect(fixture.nativeElement.textContent).toContain('Campos especiales de facturación');
    expect(fixture.nativeElement.textContent).toContain('Agente');
    expect(fixture.nativeElement.textContent).toContain('Juan Pérez');
    expect(fixture.nativeElement.textContent).toContain('Orden de trabajo');
    expect(fixture.nativeElement.textContent).toContain('OT-45678');
    expect(fixture.nativeElement.textContent).toContain('TRACK-1-ABCDEF1234567890-ABCDEFGHIJKLMNOPQRSTUVWXYZ-1234567890');

    fixture.componentInstance['closeDetailModal']();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).not.toContain('Detalle de CFDI emitido');
  });

  it('keeps the detail modal stable when the document has no special billing fields', async () => {
    const getFiscalDocumentById = vi.fn().mockReturnValue(of({
      id: 40,
      billingDocumentId: 30,
      issuerProfileId: 1,
      fiscalReceiverId: 9,
      status: 'Stamped',
      cfdiVersion: '4.0',
      documentType: 'I',
      series: 'A',
      folio: '31787',
      issuedAtUtc: '2026-03-24T12:00:00Z',
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
      items: [],
      specialFields: []
    }));
    const fixture = await configure({ getFiscalDocumentById });

    await fixture.componentInstance['openDetailModal'](fixture.componentInstance['items']()[0]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Detalle de CFDI emitido');
    expect(fixture.nativeElement.textContent).not.toContain('Campos especiales de facturación');
  });

  it('opens the email composer from the detail modal and sends without re-timbrar', async () => {
    const sendByEmail = vi.fn().mockReturnValue(of({
      outcome: 'Sent',
      isSuccess: true,
      fiscalDocumentId: 40,
      recipients: ['cliente@example.com'],
      sentAtUtc: '2026-03-24T12:10:00Z'
    }));
    const fixture = await configure({ sendByEmail });

    await fixture.componentInstance['openDetailModal'](fixture.componentInstance['items']()[0]);
    await fixture.componentInstance['openEmailComposerForSelected']();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Reenviar CFDI por correo');
    fixture.componentInstance['emailRecipientsInput'] = 'cliente@example.com';
    await fixture.componentInstance['sendEmail']();

    expect(sendByEmail).toHaveBeenCalledWith(40, expect.objectContaining({
      recipients: ['cliente@example.com']
    }));
  });

  it('cancels an issued CFDI from the detail modal using the shared SAT flow', async () => {
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
      cancelledAtUtc: '2026-03-29T18:40:00Z'
    }));
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const fixture = await configure({ cancelFiscalDocument });

    await fixture.componentInstance['openDetailModal'](fixture.componentInstance['items']()[0]);
    fixture.componentInstance['openCancelDialog']();
    fixture.componentInstance['onCancellationReasonChange']('03');

    await fixture.componentInstance['cancel']();

    expect(cancelFiscalDocument).toHaveBeenCalledWith(40, { cancellationReasonCode: '03', replacementUuid: undefined });
    expect(fixture.componentInstance['selectedDocument']()?.status).toBe('Cancelled');
    expect(fixture.componentInstance['selectedCancellation']()?.status).toBe('Cancelled');
    expect(fixture.componentInstance['selectedCancellation']()?.providerCode).toBe('201');
    expect(fixture.componentInstance['canCancelSelectedDocument']()).toBe(false);
    expect(fixture.componentInstance['lastOperationMessage']()).toContain('Cancelación exitosa');

    confirmSpy.mockRestore();
  });

  it('refreshes SAT status from the issued CFDI detail modal', async () => {
    const refreshStatus = vi.fn().mockReturnValue(of({
      outcome: 'Refreshed',
      isSuccess: true,
      fiscalDocumentId: 40,
      fiscalDocumentStatus: 'CancellationRequested',
      uuid: 'UUID-123',
      lastKnownExternalStatus: 'Vigente',
      providerCode: 'S',
      providerMessage: 'CodigoEstatus=S - Comprobante obtenido satisfactoriamente. | Estado=Vigente | EstatusCancelacion=En proceso',
      operationalStatus: 'CancellationPending',
      operationalMessage: 'La cancelación fue solicitada y sigue en proceso en SAT.',
      supportMessage: 'CodigoEstatus=S - Comprobante obtenido satisfactoriamente. | Estado=Vigente | EsCancelable=Cancelable con aceptación | EstatusCancelacion=En proceso',
      checkedAtUtc: '2026-03-29T10:00:00Z'
    }));
    const getFiscalDocumentById = vi.fn().mockReturnValue(of({
      id: 40,
      billingDocumentId: 30,
      issuerProfileId: 1,
      fiscalReceiverId: 9,
      status: 'CancellationRequested',
      cfdiVersion: '4.0',
      documentType: 'I',
      series: 'A',
      folio: '31787',
      issuedAtUtc: '2026-03-24T12:00:00Z',
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
      items: [],
      specialFields: []
    }));
    const getCancellation = vi.fn().mockReturnValue(of({
      fiscalDocumentId: 40,
      status: 'Requested',
      cancellationReasonCode: '03',
      replacementUuid: null,
      providerName: 'FacturaloPlus',
      providerTrackingId: 'TRACK-CANCEL-201',
      providerCode: 'S',
      providerMessage: 'CodigoEstatus=S - Comprobante obtenido satisfactoriamente. | Estado=Vigente | EstatusCancelacion=En proceso',
      errorCode: null,
      errorMessage: null,
      supportMessage: 'CodigoEstatus=S - Comprobante obtenido satisfactoriamente. | Estado=Vigente | EstatusCancelacion=En proceso',
      rawResponseSummaryJson: '{"Estado":"Vigente","EstatusCancelacion":"En proceso"}',
      requestedAtUtc: '2026-03-29T12:00:00Z',
      cancelledAtUtc: null
    }));
    const fixture = await configure({ refreshStatus, getFiscalDocumentById, getCancellation });

    await fixture.componentInstance['openDetailModal'](fixture.componentInstance['items']()[0]);
    await fixture.componentInstance['refreshStatus']();

    expect(refreshStatus).toHaveBeenCalledWith(40);
    expect(fixture.componentInstance['lastOperationMessage']()).toContain('sigue en proceso en SAT');
    expect(fixture.componentInstance['selectedDocument']()?.status).toBe('CancellationRequested');
    expect(fixture.componentInstance['selectedCancellation']()?.status).toBe('Requested');
  });

  it('shows filter validation when the date range is invalid', async () => {
    const fixture = await configure();

    fixture.componentInstance['fromDate'] = '2026-03-24';
    fixture.componentInstance['toDate'] = '2026-03-23';
    await fixture.componentInstance['applyFilters']();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('La fecha inicial no puede ser mayor a la fecha final.');
  });
});
