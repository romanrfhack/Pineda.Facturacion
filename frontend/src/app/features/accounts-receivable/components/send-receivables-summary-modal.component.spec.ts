import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AccountsReceivableApiService } from '../infrastructure/accounts-receivable-api.service';
import { SendReceivablesSummaryModalComponent } from './send-receivables-summary-modal.component';

describe('SendReceivablesSummaryModalComponent', () => {
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
      previewReceivablesSummary: vi.fn(),
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
});
