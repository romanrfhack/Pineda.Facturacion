import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { FiscalReceiversApiService } from '../../catalogs/infrastructure/fiscal-receivers-api.service';
import { FiscalReceiver } from '../../catalogs/models/catalogs.models';
import { OrdersApiService } from '../infrastructure/orders-api.service';
import { LegacyOrderListItem } from '../models/orders.models';
import { SendOrderDebtSummaryModalComponent } from './send-order-debt-summary-modal.component';

describe('SendOrderDebtSummaryModalComponent', () => {
  async function configure(selectedOrders: LegacyOrderListItem[]) {
    const ordersApi = {
      previewOrderDebtSummary: vi.fn().mockReturnValue(of({
        outcome: 'Found',
        success: true,
        html: '',
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

    return { fixture, component, ordersApi };
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
  });

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

function createReceiver(rfc: string): FiscalReceiver {
  return {
    id: 77,
    rfc,
    legalName: 'Cliente Uno',
    postalCode: '01000',
    fiscalRegimeCode: '601',
    cfdiUseCodeDefault: 'G03',
    isActive: true,
    email: 'cliente@example.com',
    createdAtUtc: '2026-05-08T00:00:00Z',
    updatedAtUtc: '2026-05-08T00:00:00Z',
  };
}
