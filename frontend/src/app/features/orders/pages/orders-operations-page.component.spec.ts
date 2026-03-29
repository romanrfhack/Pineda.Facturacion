import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { OrdersOperationsPageComponent } from './orders-operations-page.component';
import { OrdersApiService } from '../infrastructure/orders-api.service';
import { FeedbackService } from '../../../core/ui/feedback.service';

describe('OrdersOperationsPageComponent', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-03-23T12:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  function createApi() {
    return {
      searchLegacyOrders: vi.fn().mockReturnValue(of({
        isSuccess: true,
        items: [
          {
            legacyOrderId: 'LEG-1001',
            orderDateUtc: '2026-03-23T10:00:00Z',
            customerName: 'Cliente Uno',
            total: 116,
            legacyOrderType: 'F',
            isImported: false,
            salesOrderId: null,
            billingDocumentId: null,
            billingDocumentStatus: null,
            fiscalDocumentId: null,
            fiscalDocumentStatus: null,
            importStatus: null
          }
        ],
        totalCount: 1,
        totalPages: 1,
        page: 1,
        pageSize: 10
      })),
      importLegacyOrder: vi.fn().mockReturnValue(of({
        outcome: 'Imported',
        isSuccess: true,
        isIdempotent: false,
        sourceSystem: 'legacy',
        sourceTable: 'pedidos',
        legacyOrderId: 'LEG-1001',
        sourceHash: 'hash',
        legacyImportRecordId: 10,
        salesOrderId: 20,
        importStatus: 'Imported'
      })),
      createBillingDocument: vi.fn().mockReturnValue(of({
        outcome: 'Created',
        isSuccess: true,
        salesOrderId: 20,
        billingDocumentId: 30,
        billingDocumentStatus: 'Draft'
      }))
    };
  }

  async function configure(apiOverrides?: Partial<ReturnType<typeof createApi>>) {
    const api = { ...createApi(), ...apiOverrides };
    const feedback = { show: vi.fn() };
    const router = { navigate: vi.fn().mockResolvedValue(true) };

    await TestBed.configureTestingModule({
      imports: [OrdersOperationsPageComponent],
      providers: [
        { provide: OrdersApiService, useValue: api },
        { provide: FeedbackService, useValue: feedback },
        { provide: Router, useValue: router }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(OrdersOperationsPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, api, feedback, router };
  }

  it('loads today orders on init', async () => {
    const { api } = await configure();
    expect(api.searchLegacyOrders).toHaveBeenCalledWith({
      fromDate: '2026-03-23',
      toDate: '2026-03-23',
      customerQuery: '',
      page: 1,
      pageSize: 10
    });
  });

  it('applies customer filter to the legacy orders search without importing automatically', async () => {
    const { fixture, api } = await configure();

    fixture.componentInstance['customerQuery'].set('Cliente Uno');
    await fixture.componentInstance['searchCurrentRange']();

    expect(api.searchLegacyOrders).toHaveBeenLastCalledWith({
      fromDate: '2026-03-23',
      toDate: '2026-03-23',
      customerQuery: 'Cliente Uno',
      page: 1,
      pageSize: 10
    });
    expect(api.importLegacyOrder).toHaveBeenCalledTimes(0);
  });

  it('shows custom range validation and avoids invalid search', async () => {
    const { fixture, api } = await configure();

    fixture.componentInstance['setQuickRange']('custom');
    fixture.componentInstance['fromDate'].set('2026-03-24');
    fixture.componentInstance['toDate'].set('2026-03-23');
    fixture.detectChanges();

    await fixture.componentInstance['searchCustomRange']();

    expect(fixture.nativeElement.textContent).toContain('La fecha inicial no puede ser mayor que la fecha final.');
    expect(api.searchLegacyOrders).toHaveBeenCalledTimes(1);
  });

  it('imports an order from the table and marks it as imported', async () => {
    const { fixture, feedback } = await configure();
    const order = fixture.componentInstance['ordersPage']()!.items[0];

    await fixture.componentInstance['importOrderFromList'](order);
    fixture.detectChanges();

    expect(feedback.show).toHaveBeenCalledWith('success', 'La orden legada se importó correctamente. Puedes continuar con el documento de facturación.');
    expect(fixture.componentInstance['ordersPage']()!.items[0].isImported).toBe(true);
    expect(fixture.componentInstance['importedOrder']()?.salesOrderId).toBe(20);
  });

  it('continues with an imported order without billing document by selecting it locally', async () => {
    const { fixture, feedback } = await configure({
      searchLegacyOrders: vi.fn().mockReturnValue(of({
        isSuccess: true,
        items: [
          {
            legacyOrderId: 'LEG-2001',
            orderDateUtc: '2026-03-23T08:00:00Z',
            customerName: 'Cliente Importado',
            total: 116,
            legacyOrderType: 'F',
            isImported: true,
            salesOrderId: 22,
            billingDocumentId: null,
            billingDocumentStatus: null,
            fiscalDocumentId: null,
            fiscalDocumentStatus: null,
            importStatus: 'Imported'
          }
        ],
        totalCount: 1,
        totalPages: 1,
        page: 1,
        pageSize: 10
      }))
    });

    const order = fixture.componentInstance['ordersPage']()!.items[0];
    await fixture.componentInstance['continueOrder'](order);

    expect(fixture.componentInstance['importedOrder']()?.salesOrderId).toBe(22);
    expect(feedback.show).toHaveBeenCalledWith('info', 'La orden ya está importada. Puedes crear el documento de facturación para continuar.');
  });

  it('navigates to fiscal documents after creating a billing document', async () => {
    const { fixture, router, feedback } = await configure();

    fixture.componentInstance['importedOrder'].set({
      outcome: 'Imported',
      isSuccess: true,
      isIdempotent: false,
      sourceSystem: 'legacy',
      sourceTable: 'pedidos',
      legacyOrderId: 'LEG-1001',
      sourceHash: 'hash',
      legacyImportRecordId: 10,
      salesOrderId: 20,
      importStatus: 'Imported'
    });

    await fixture.componentInstance['createBillingDocument']();

    expect(feedback.show).toHaveBeenCalledWith('success', 'Documento de facturación creado.');
    expect(router.navigate).toHaveBeenCalledWith(['/app/fiscal-documents'], { queryParams: { billingDocumentId: 30 } });
  });

  it('reuses the existing billing document returned by a conflict response', async () => {
    const { fixture, router, feedback } = await configure({
      createBillingDocument: vi.fn().mockReturnValue(throwError(() =>
        new HttpErrorResponse({
          status: 409,
          error: {
            outcome: 'Conflict',
            isSuccess: false,
            salesOrderId: 20,
            billingDocumentId: 31,
            billingDocumentStatus: 'Draft',
            errorMessage: 'Sales order already has a billing document'
          }
        })))
    });

    fixture.componentInstance['importedOrder'].set({
      outcome: 'Imported',
      isSuccess: true,
      isIdempotent: false,
      sourceSystem: 'legacy',
      sourceTable: 'pedidos',
      legacyOrderId: 'LEG-1001',
      sourceHash: 'hash',
      legacyImportRecordId: 10,
      salesOrderId: 20,
      importStatus: 'Imported'
    });

    await fixture.componentInstance['createBillingDocument']();

    expect(feedback.show).toHaveBeenCalledWith('info', 'La orden ya cuenta con un documento de facturación. Se abrirá el documento existente.');
    expect(router.navigate).toHaveBeenCalledWith(['/app/fiscal-documents'], { queryParams: { billingDocumentId: 31 } });
    expect(fixture.componentInstance['billingDocument']()?.outcome).toBe('Conflict');
  });
});
