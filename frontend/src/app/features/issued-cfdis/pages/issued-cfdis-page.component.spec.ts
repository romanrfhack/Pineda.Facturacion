import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { IssuedCfdisPageComponent } from './issued-cfdis-page.component';
import { FiscalDocumentsApiService } from '../../fiscal-documents/infrastructure/fiscal-documents-api.service';
import { FeedbackService } from '../../../core/ui/feedback.service';

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
        { provide: FeedbackService, useValue: { show: vi.fn() } }
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

    expect(fixture.nativeElement.textContent).toContain('UUID-123');
    expect(fixture.nativeElement.textContent).toContain('BBB010101BBB');
    expect(fixture.nativeElement.textContent).toContain('Receiver One');
    expect(fixture.nativeElement.textContent).toContain('Ver detalle');
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

  it('opens the email composer and sends without re-timbrar', async () => {
    const sendByEmail = vi.fn().mockReturnValue(of({
      outcome: 'Sent',
      isSuccess: true,
      fiscalDocumentId: 40,
      recipients: ['cliente@example.com'],
      sentAtUtc: '2026-03-24T12:10:00Z'
    }));
    const fixture = await configure({ sendByEmail });

    await fixture.componentInstance['openEmailComposer'](fixture.componentInstance['items']()[0]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Reenviar CFDI por correo');
    fixture.componentInstance['emailRecipientsInput'] = 'cliente@example.com';
    await fixture.componentInstance['sendEmail']();

    expect(sendByEmail).toHaveBeenCalledWith(40, expect.objectContaining({
      recipients: ['cliente@example.com']
    }));
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
