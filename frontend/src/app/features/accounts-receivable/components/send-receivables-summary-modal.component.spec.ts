import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AccountsReceivableApiService } from '../infrastructure/accounts-receivable-api.service';
import { SendReceivablesSummaryModalComponent } from './send-receivables-summary-modal.component';

describe('SendReceivablesSummaryModalComponent', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    Object.defineProperty(window.navigator, 'share', {
      configurable: true,
      value: undefined,
    });
    Object.defineProperty(window.navigator, 'canShare', {
      configurable: true,
      value: undefined,
    });
  });

  async function configure() {
    const api = {
      getReceivablesSummaryCandidates: vi.fn().mockReturnValue(
        of({
          receiver: {
            id: 77,
            legalName: 'Cliente Uno',
            rfc: 'AAA010101AAA',
            email: 'cliente@example.com; cobranza@example.com',
            fiscalRegimeCode: '601',
            postalCode: '01000',
          },
          issuer: {
            id: 1,
            legalName: 'Emisor Uno',
            rfc: 'III010101III',
            email: null,
            fiscalRegimeCode: '601',
            postalCode: '01000',
          },
          defaultTo: ['cliente@example.com', 'cobranza@example.com'],
          defaultSubject: 'Resumen de adeudos pendientes - Cliente Uno',
          defaultMessage: 'Mensaje',
          invoices: [
            {
              accountsReceivableInvoiceId: 201,
              fiscalDocumentId: 301,
              fiscalSeries: 'A',
              fiscalFolio: '201',
              fiscalUuid: 'UUID-201',
              issuedAtUtc: '2026-07-01T00:00:00Z',
              dueAtUtc: '2026-07-10T00:00:00Z',
              daysPastDue: 0,
              currencyCode: 'MXN',
              total: 100,
              paidTotal: 0,
              outstandingBalance: 100,
              status: 'Open',
              isOverdue: false,
              documentLink: null,
            },
          ],
        }),
      ),
      previewReceivablesSummary: vi.fn().mockReturnValue(of(createPreviewResponse())),
      sendReceivablesSummary: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [SendReceivablesSummaryModalComponent],
      providers: [{ provide: AccountsReceivableApiService, useValue: api }],
    }).compileComponents();

    const fixture = TestBed.createComponent(SendReceivablesSummaryModalComponent);
    fixture.componentRef.setInput('open', true);
    fixture.componentRef.setInput('receiverId', 77);
    fixture.componentRef.setInput('currentSelection', []);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    return { fixture, api };
  }

  it('loads multiple default recipients from the catalog using semicolon format', async () => {
    const { fixture } = await configure();

    expect(fixture.componentInstance['toInput']).toBe('cliente@example.com; cobranza@example.com');
  }, 15000);

  it('blocks the receivables summary send when cc contains invalid recipients', async () => {
    const { fixture, api } = await configure();

    fixture.componentInstance['preview'].set({
      outcome: 'Found',
      success: true,
      html: '',
      summary: {
        invoiceCount: 1,
        outstandingBalance: 100,
        overdueBalance: 0,
        currentBalance: 100,
        totalsByCurrency: [
          {
            currencyCode: 'MXN',
            invoiceCount: 1,
            total: 100,
            paidTotal: 0,
            outstandingBalance: 100,
            overdueBalance: 0,
            currentBalance: 100,
          },
        ],
      },
      finalSummary: {
        to: ['cliente@example.com'],
        cc: [],
        bcc: [],
        subject: 'Resumen de adeudos pendientes - Cliente Uno',
        invoiceCount: 1,
        format: 'Html',
        attachedPdf: false,
        totalsByCurrency: [
          {
            currencyCode: 'MXN',
            invoiceCount: 1,
            total: 100,
            paidTotal: 0,
            outstandingBalance: 100,
            overdueBalance: 0,
            currentBalance: 100,
          },
        ],
      },
    });
    fixture.componentInstance['toInput'] = 'cliente@example.com';
    fixture.componentInstance['ccInput'] = 'cobranza@example.com; invalido';

    await fixture.componentInstance['send']();
    fixture.detectChanges();

    expect(api.sendReceivablesSummary).not.toHaveBeenCalled();
    expect(fixture.componentInstance['errorMessage']()).toBe('Correo inválido: invalido');
  });

  it('generates the preview without an email recipient for WhatsApp or printing', async () => {
    const { fixture, api } = await configure();

    fixture.componentInstance['toInput'] = '';
    fixture.componentInstance['step'].set(2);
    await fixture.componentInstance['next']();

    expect(api.previewReceivablesSummary).toHaveBeenCalledWith(
      77,
      expect.objectContaining({ to: [] }),
    );
    expect(fixture.componentInstance['step']()).toBe(3);
    expect(fixture.componentInstance['preview']()?.success).toBe(true);
    expect(fixture.componentInstance['errorMessage']()).toBeNull();
  });

  it('shares the digital PDF through the native file share dialog', async () => {
    const nativeShare = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(window.navigator, 'share', {
      configurable: true,
      value: nativeShare,
    });
    Object.defineProperty(window.navigator, 'canShare', {
      configurable: true,
      value: vi.fn().mockReturnValue(true),
    });
    const { fixture } = await configure();
    fixture.componentInstance['preview'].set(createPreviewResponse());

    await fixture.componentInstance['sharePdf']();

    expect(nativeShare).toHaveBeenCalledOnce();
    const shareData = nativeShare.mock.calls[0][0] as ShareData;
    expect(shareData.files).toHaveLength(1);
    expect(shareData.files?.[0].name).toBe('resumen_adeudos_AAA010101AAA_2026-07-30.pdf');
    expect(shareData.files?.[0].type).toBe('application/pdf');
  });

  it('downloads the low-ink print PDF instead of the digital version', async () => {
    Object.defineProperty(window.URL, 'createObjectURL', {
      configurable: true,
      value: vi.fn().mockReturnValue('blob:receivables-print-summary'),
    });
    Object.defineProperty(window.URL, 'revokeObjectURL', {
      configurable: true,
      value: vi.fn(),
    });
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
    const { fixture } = await configure();
    fixture.componentInstance['preview'].set(createPreviewResponse());

    fixture.componentInstance['downloadPrintPdf']();

    expect(click).toHaveBeenCalledOnce();
    const file = vi.mocked(window.URL.createObjectURL).mock.calls[0][0] as File;
    expect(file.name).toBe('resumen_adeudos_impresion_AAA010101AAA_2026-07-30.pdf');
    expect(fixture.componentInstance['shareStatusMessage']()).toContain('impresión');
  });

  it('downloads the digital PDF and opens WhatsApp when native sharing is unavailable', async () => {
    Object.defineProperty(window.navigator, 'share', {
      configurable: true,
      value: vi.fn(),
    });
    Object.defineProperty(window.navigator, 'canShare', {
      configurable: true,
      value: vi.fn().mockReturnValue(false),
    });
    Object.defineProperty(window.URL, 'createObjectURL', {
      configurable: true,
      value: vi.fn().mockReturnValue('blob:receivables-digital-summary'),
    });
    Object.defineProperty(window.URL, 'revokeObjectURL', {
      configurable: true,
      value: vi.fn(),
    });
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
    const open = vi.spyOn(window, 'open').mockReturnValue({} as Window);
    const { fixture } = await configure();
    fixture.componentInstance['preview'].set(createPreviewResponse());

    await fixture.componentInstance['sharePdf']();

    expect(click).toHaveBeenCalledOnce();
    expect(open).toHaveBeenCalledWith(
      expect.stringContaining('https://wa.me/?text='),
      '_blank',
      'noopener,noreferrer',
    );
    expect(fixture.componentInstance['shareStatusMessage']()).toContain('adjunta manualmente');
  });
});

function createPreviewResponse() {
  return {
    outcome: 'Found',
    success: true,
    html: '<p>Resumen</p>',
    pdfBase64: 'JVBERi10ZXN0',
    pdfFileName: 'resumen_adeudos_AAA010101AAA_2026-07-30.pdf',
    printPdfBase64: 'JVBERi1wcmludA==',
    printPdfFileName: 'resumen_adeudos_impresion_AAA010101AAA_2026-07-30.pdf',
    summary: {
      invoiceCount: 1,
      outstandingBalance: 100,
      overdueBalance: 0,
      currentBalance: 100,
      totalsByCurrency: [
        {
          currencyCode: 'MXN',
          invoiceCount: 1,
          total: 100,
          paidTotal: 0,
          outstandingBalance: 100,
          overdueBalance: 0,
          currentBalance: 100,
        },
      ],
    },
    finalSummary: {
      to: ['cliente@example.com'],
      cc: [],
      bcc: [],
      subject: 'Resumen de adeudos pendientes - Cliente Uno',
      invoiceCount: 1,
      format: 'Html',
      attachedPdf: false,
      totalsByCurrency: [
        {
          currencyCode: 'MXN',
          invoiceCount: 1,
          total: 100,
          paidTotal: 0,
          outstandingBalance: 100,
          overdueBalance: 0,
          currentBalance: 100,
        },
      ],
    },
  };
}
