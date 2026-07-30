import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { FiscalReceiversApiService } from '../../catalogs/infrastructure/fiscal-receivers-api.service';
import { FiscalReceiver } from '../../catalogs/models/catalogs.models';
import { OrdersApiService } from '../infrastructure/orders-api.service';
import { LegacyOrderListItem } from '../models/orders.models';
import { SendOrderDebtSummaryModalComponent } from './send-order-debt-summary-modal.component';

describe('SendOrderDebtSummaryModalComponent', () => {
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

  async function configure(selectedOrders: LegacyOrderListItem[]) {
    const ordersApi = {
      previewOrderDebtSummary: vi.fn().mockReturnValue(of({
        outcome: 'Found',
        success: true,
        html: '',
        pdfBase64: 'JVBERi10ZXN0',
        pdfFileName: 'Resumen-adeudos-Cliente-Uno-2026-07-30.pdf',
        summary: {
          orderCount: selectedOrders.length,
          total: null,
          totalsByCurrency: [
            { currencyCode: 'MXN', orderCount: 1, total: 116 },
            { currencyCode: 'USD', orderCount: 1, total: 50 },
          ],
        },
        finalSummary: {
          to: ['cliente@example.com'],
          cc: [],
          bcc: [],
          subject: 'Resumen',
          orderCount: selectedOrders.length,
          format: 'Html',
          totalsByCurrency: [
            { currencyCode: 'MXN', orderCount: 1, total: 116 },
            { currencyCode: 'USD', orderCount: 1, total: 50 },
          ],
        },
      })),
      sendOrderDebtSummary: vi.fn(),
    };
    const fiscalReceiversApi = {
      search: vi.fn().mockReturnValue(of([])),
      getByRfc: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [SendOrderDebtSummaryModalComponent],
      providers: [
        { provide: OrdersApiService, useValue: ordersApi },
        { provide: FiscalReceiversApiService, useValue: fiscalReceiversApi },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(SendOrderDebtSummaryModalComponent);
    fixture.componentRef.setInput('open', true);
    fixture.componentRef.setInput('selectedOrders', selectedOrders);
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      selectedReceiver: { set(value: FiscalReceiver): void };
      step: { set(value: number): void };
      toInput: string;
      subject: string;
      message: string;
      next(): Promise<void>;
    };

    component.selectedReceiver.set(createReceiver('AAA010101AAA'));
    component.toInput = 'cliente@example.com';
    component.subject = 'Resumen';
    component.message = 'Mensaje';

    return { fixture, component, ordersApi, fiscalReceiversApi };
  }

  it('blocks preview when selected orders belong to different RFCs', async () => {
    const { fixture, component, ordersApi } = await configure([
      createOrder({ legacyOrderId: 'LEG-1001', customerRfc: 'AAA010101AAA', customerLegacyId: 'C-1' }),
      createOrder({ legacyOrderId: 'LEG-1002', customerRfc: 'BBB010101BBB', customerLegacyId: 'C-2' }),
    ]);

    component.step.set(2);
    await component.next();
    fixture.detectChanges();

    expect(ordersApi.previewOrderDebtSummary).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('No se puede enviar el resumen porque la selección contiene órdenes de distintos clientes');
  }, 15000);

  it('blocks preview when receiver RFC does not match selected orders RFC', async () => {
    const { fixture, component, ordersApi } = await configure([
      createOrder({ legacyOrderId: 'LEG-1001', customerRfc: 'AAA010101AAA', customerLegacyId: 'C-1' }),
    ]);
    component.selectedReceiver.set(createReceiver('XYZ010101XYZ'));

    component.step.set(2);
    await component.next();
    fixture.detectChanges();

    expect(ordersApi.previewOrderDebtSummary).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('El RFC del receptor seleccionado no coincide con el RFC de las órdenes seleccionadas.');
  });

  it('shows totals by currency without a mixed global total in preview', async () => {
    const { fixture, component, ordersApi } = await configure([
      createOrder({ legacyOrderId: 'LEG-1001', customerRfc: 'AAA010101AAA', customerLegacyId: 'C-1', currencyCode: 'MXN', total: 116 }),
      createOrder({ legacyOrderId: 'LEG-1002', customerRfc: 'AAA010101AAA', customerLegacyId: 'C-1', currencyCode: 'USD', total: 50 }),
    ]);

    component.step.set(2);
    await component.next();
    fixture.detectChanges();

    expect(ordersApi.previewOrderDebtSummary).toHaveBeenCalledOnce();
    expect(fixture.nativeElement.textContent).toContain('Totales por moneda');
    expect(fixture.nativeElement.textContent).toContain('116.00 MXN');
    expect(fixture.nativeElement.textContent).toContain('50.00 USD');
    expect(fixture.nativeElement.textContent).not.toContain('166.00');
  });

  it('preloads multiple receiver emails using semicolon format', async () => {
    const { fixture, component, fiscalReceiversApi } = await configure([
      createOrder({ legacyOrderId: 'LEG-1001', customerRfc: 'AAA010101AAA', customerLegacyId: 'C-1' }),
    ]);

    fiscalReceiversApi.getByRfc.mockReturnValue(
      of(createReceiver('AAA010101AAA', 'cliente@example.com, cobranza@example.com')),
    );

    await fixture.componentInstance['selectReceiver']({
      id: 77,
      rfc: 'AAA010101AAA',
      legalName: 'Cliente Uno',
      postalCode: '01000',
      fiscalRegimeCode: '601',
      cfdiUseCodeDefault: 'G03',
      isActive: true,
    });

    expect(component.toInput).toBe('cliente@example.com; cobranza@example.com');
  });

  it('blocks the order debt summary send when bcc contains invalid recipients', async () => {
    const { fixture, component, ordersApi } = await configure([
      createOrder({ legacyOrderId: 'LEG-1001', customerRfc: 'AAA010101AAA', customerLegacyId: 'C-1' }),
    ]);

    fixture.componentInstance['preview'].set({
      outcome: 'Found',
      success: true,
      html: '',
      summary: {
        orderCount: 1,
        total: 116,
        totalsByCurrency: [{ currencyCode: 'MXN', orderCount: 1, total: 116 }],
      },
      finalSummary: {
        to: ['cliente@example.com'],
        cc: [],
        bcc: [],
        subject: 'Resumen',
        orderCount: 1,
        format: 'Html',
        totalsByCurrency: [{ currencyCode: 'MXN', orderCount: 1, total: 116 }],
      },
    });
    component.toInput = 'cliente@example.com';
    fixture.componentInstance['bccInput'] = 'cobranza@example.com; invalido';

    await fixture.componentInstance['send']();
    fixture.detectChanges();

    expect(ordersApi.sendOrderDebtSummary).not.toHaveBeenCalled();
    expect(fixture.componentInstance['errorMessage']()).toBe('Correo inválido: invalido');
  });

  it('shares the generated PDF through the native file share dialog', async () => {
    const nativeShare = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(window.navigator, 'share', {
      configurable: true,
      value: nativeShare,
    });
    Object.defineProperty(window.navigator, 'canShare', {
      configurable: true,
      value: vi.fn().mockReturnValue(true),
    });

    const { fixture, component } = await configure([
      createOrder({ legacyOrderId: 'LEG-1001', customerRfc: 'AAA010101AAA', customerLegacyId: 'C-1' }),
    ]);

    component.step.set(2);
    await component.next();
    await fixture.componentInstance['sharePdf']();

    expect(nativeShare).toHaveBeenCalledOnce();
    const shareData = nativeShare.mock.calls[0][0] as ShareData;
    expect(shareData.files).toHaveLength(1);
    expect(shareData.files?.[0].name).toBe('Resumen-adeudos-Cliente-Uno-2026-07-30.pdf');
    expect(shareData.files?.[0].type).toBe('application/pdf');
  });

  it('downloads the PDF and opens WhatsApp when file sharing is unavailable', async () => {
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
      value: vi.fn().mockReturnValue('blob:order-summary'),
    });
    Object.defineProperty(window.URL, 'revokeObjectURL', {
      configurable: true,
      value: vi.fn(),
    });
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
    const open = vi.spyOn(window, 'open').mockReturnValue({} as Window);

    const { fixture, component } = await configure([
      createOrder({ legacyOrderId: 'LEG-1001', customerRfc: 'AAA010101AAA', customerLegacyId: 'C-1' }),
    ]);

    component.step.set(2);
    await component.next();
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

function createOrder(overrides: Partial<LegacyOrderListItem>): LegacyOrderListItem {
  return {
    legacyOrderId: 'LEG-1001',
    orderDateUtc: '2026-05-08T00:00:00Z',
    customerName: 'Cliente Uno',
    customerLegacyId: 'C-1',
    customerRfc: 'AAA010101AAA',
    total: 116,
    currencyCode: 'MXN',
    legacyOrderType: 'Nota',
    isImported: false,
    salesOrderId: null,
    billingDocumentId: null,
    billingDocumentStatus: null,
    fiscalDocumentId: null,
    fiscalDocumentStatus: null,
    importStatus: null,
    ...overrides,
  };
}

function createReceiver(rfc: string, email = 'cliente@example.com'): FiscalReceiver {
  return {
    id: 77,
    rfc,
    legalName: 'Cliente Uno',
    postalCode: '01000',
    fiscalRegimeCode: '601',
    cfdiUseCodeDefault: 'G03',
    isActive: true,
    email,
    createdAtUtc: '2026-05-08T00:00:00Z',
    updatedAtUtc: '2026-05-08T00:00:00Z',
  };
}
