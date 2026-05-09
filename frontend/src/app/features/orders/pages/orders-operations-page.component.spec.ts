import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { OrdersOperationsPageComponent } from './orders-operations-page.component';
import { OrdersApiService } from '../infrastructure/orders-api.service';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { FiscalReceiversApiService } from '../../catalogs/infrastructure/fiscal-receivers-api.service';
import { LegacyOrderListItem, SearchLegacyOrdersResponse } from '../models/orders.models';

describe('OrdersOperationsPageComponent', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-03-23T12:00:00Z'));
    vi.stubGlobal('confirm', vi.fn().mockReturnValue(true));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
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
        importStatus: 'Imported',
        currentRevisionNumber: 1
      })),
      listLegacyOrderImportRevisions: vi.fn().mockReturnValue(of({
        isSuccess: true,
        legacyOrderId: 'LEG-1001',
        currentRevisionNumber: 1,
        revisions: [
          {
            legacyOrderId: 'LEG-1001',
            revisionNumber: 1,
            previousRevisionNumber: null,
            actionType: 'Imported',
            outcome: 'Imported',
            sourceHash: 'hash',
            previousSourceHash: null,
            appliedAtUtc: '2026-03-23T10:00:00Z',
            isCurrent: true,
            actorUserId: 99,
            actorUsername: 'tester',
            salesOrderId: 20,
            billingDocumentId: null,
            fiscalDocumentId: null,
            eligibilityStatus: 'Allowed',
            eligibilityReasonCode: 'None',
            eligibilityReasonMessage: 'Initial legacy import created the first tracked revision.',
            changeSummary: {
              addedLines: 1,
              removedLines: 0,
              modifiedLines: 0,
              unchangedLines: 0,
              oldSubtotal: 0,
              newSubtotal: 100,
              oldTotal: 0,
              newTotal: 116
            },
            snapshotJson: '{}',
            diffJson: '{}'
          }
        ]
      })),
      previewLegacyOrderImport: vi.fn().mockReturnValue(of({
        isSuccess: true,
        legacyOrderId: 'LEG-1001',
        existingSalesOrderId: 20,
        existingSalesOrderStatus: 'SnapshotCreated',
        existingBillingDocumentId: 30,
        existingBillingDocumentStatus: 'Draft',
        existingFiscalDocumentId: null,
        existingFiscalDocumentStatus: null,
        fiscalUuid: null,
        existingSourceHash: 'old-hash',
        currentSourceHash: 'new-hash',
        currentRevisionNumber: 1,
        hasChanges: true,
        changedOrderFields: [],
        changeSummary: {
          addedLines: 0,
          removedLines: 0,
          modifiedLines: 1,
          unchangedLines: 0,
          oldSubtotal: 100,
          newSubtotal: 150,
          oldTotal: 116,
          newTotal: 174
        },
        lineChanges: [
          {
            changeType: 'Modified',
            matchKey: 'A-1#1',
            oldLine: {
              lineNumber: 1,
              legacyArticleId: 'A-1',
              sku: 'A-1',
              description: 'Articulo demo',
              unitCode: 'H87',
              unitName: 'Pieza',
              quantity: 1,
              unitPrice: 100,
              discountAmount: 0,
              taxAmount: 16,
              lineTotal: 100
            },
            newLine: {
              lineNumber: 1,
              legacyArticleId: 'A-1',
              sku: 'A-1',
              description: 'Articulo demo',
              unitCode: 'H87',
              unitName: 'Pieza',
              quantity: 2,
              unitPrice: 75,
              discountAmount: 0,
              taxAmount: 24,
              lineTotal: 150
            },
            changedFields: ['quantity', 'unitPrice', 'lineTotal']
          }
        ],
        reimportEligibility: {
          status: 'Allowed',
          reasonCode: 'None',
          reasonMessage: 'Preview completed. No protected state blocks controlled reimport.'
        },
        allowedActions: ['view_existing_sales_order', 'preview_reimport', 'reimport_not_available']
      })),
      reimportLegacyOrder: vi.fn().mockReturnValue(of({
        outcome: 'Reimported',
        isSuccess: true,
        legacyOrderId: 'LEG-1001',
        legacyImportRecordId: 10,
        salesOrderId: 20,
        salesOrderStatus: 'SnapshotCreated',
        billingDocumentId: 30,
        billingDocumentStatus: 'Draft',
        fiscalDocumentId: null,
        fiscalDocumentStatus: null,
        fiscalUuid: null,
        previousSourceHash: 'old-hash',
        newSourceHash: 'new-hash',
        currentRevisionNumber: 2,
        reimportApplied: true,
        reimportMode: 'ReplaceExistingImport',
        reimportEligibility: {
          status: 'Allowed',
          reasonCode: 'None',
          reasonMessage: 'Preview completed. No protected state blocks controlled reimport.'
        },
        allowedActions: ['view_existing_sales_order', 'view_existing_billing_document', 'preview_reimport'],
        warnings: []
      })),
      createBillingDocument: vi.fn().mockReturnValue(of({
        outcome: 'Created',
        isSuccess: true,
        salesOrderId: 20,
        billingDocumentId: 30,
        billingDocumentStatus: 'Draft'
      })),
      createBulkBillingDocument: vi.fn().mockReturnValue(of({
        outcome: 'Created',
        isSuccess: true,
        billingDocumentId: 30,
        billingDocumentStatus: 'Draft',
        selectedOrderCount: 1,
        importedOrderCount: 1,
        associatedOrderCount: 1,
        legacyOrderIds: ['LEG-1001'],
        orderErrors: []
      }))
    };
  }

  type OrdersApiMock = ReturnType<typeof createApi>;
  type OrdersApiOverrides = Partial<OrdersApiMock>;
  type ConfigureOptions = {
    apiOverrides?: OrdersApiOverrides;
    queryParams?: Record<string, string>;
  };

  async function configure(
    options?: OrdersApiOverrides | ConfigureOptions
  ) {
    const normalizedOptions: ConfigureOptions = isConfigureOptions(options)
      ? options
      : { apiOverrides: options };
    const api = { ...createApi(), ...normalizedOptions.apiOverrides };
    const feedback = { show: vi.fn() };
    const router = { navigate: vi.fn().mockResolvedValue(true) };
    const fiscalReceiversApi = {
      search: vi.fn().mockReturnValue(of([])),
      getByRfc: vi.fn().mockReturnValue(of({
        id: 77,
        rfc: 'AAA010101AAA',
        legalName: 'Cliente Uno',
        postalCode: '01000',
        fiscalRegimeCode: '601',
        cfdiUseCodeDefault: 'G03',
        isActive: true,
        email: 'cliente@example.com',
        createdAtUtc: '2026-03-23T10:00:00Z',
        updatedAtUtc: '2026-03-23T10:00:00Z'
      }))
    };

    await TestBed.configureTestingModule({
      imports: [OrdersOperationsPageComponent],
      providers: [
        { provide: OrdersApiService, useValue: api },
        { provide: FeedbackService, useValue: feedback },
        { provide: FiscalReceiversApiService, useValue: fiscalReceiversApi },
        { provide: Router, useValue: router },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: convertToParamMap(normalizedOptions.queryParams ?? {})
            }
          }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(OrdersOperationsPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, api, feedback, router };
  }

  function isConfigureOptions(value: OrdersApiOverrides | ConfigureOptions | undefined): value is ConfigureOptions {
    return !!value && ('apiOverrides' in value || 'queryParams' in value);
  }

  function createLegacyOrder(overrides: Partial<LegacyOrderListItem> = {}): LegacyOrderListItem {
    return {
      legacyOrderId: overrides.legacyOrderId ?? 'LEG-1001',
      orderDateUtc: overrides.orderDateUtc ?? '2026-03-23T10:00:00Z',
      customerName: overrides.customerName ?? 'Cliente Uno',
      customerLegacyId: overrides.customerLegacyId ?? 'C-1',
      customerRfc: overrides.customerRfc ?? 'AAA010101AAA',
      total: 'total' in overrides ? overrides.total as number : 116,
      currencyCode: overrides.currencyCode,
      legacyOrderType: overrides.legacyOrderType ?? 'F',
      isImported: overrides.isImported ?? false,
      salesOrderId: overrides.salesOrderId ?? null,
      billingDocumentId: overrides.billingDocumentId ?? null,
      billingDocumentStatus: overrides.billingDocumentStatus ?? null,
      fiscalDocumentId: overrides.fiscalDocumentId ?? null,
      fiscalDocumentStatus: overrides.fiscalDocumentStatus ?? null,
      importStatus: overrides.importStatus ?? null
    };
  }

  function createSearchResponse(
    items: LegacyOrderListItem[],
    overrides: Partial<SearchLegacyOrdersResponse> = {},
  ): SearchLegacyOrdersResponse {
    return {
      isSuccess: overrides.isSuccess ?? true,
      errorMessage: overrides.errorMessage ?? null,
      items,
      totalCount: overrides.totalCount ?? items.length,
      totalPages: overrides.totalPages ?? 1,
      page: overrides.page ?? 1,
      pageSize: overrides.pageSize ?? 10
    };
  }

  it('loads orders without a default period filter on init', async () => {
    const { fixture, api } = await configure();
    expect(fixture.componentInstance['quickRange']()).toBe('');
    expect(api.searchLegacyOrders).toHaveBeenCalledWith({
      legacyOrderId: '',
      customerQuery: '',
      page: 1,
      pageSize: 10
    });
  });

  it('starts without bulk selection or mass actions', async () => {
    const { fixture } = await configure();

    expect(fixture.componentInstance['selectedOrdersCount']()).toBe(0);
    expect(fixture.componentInstance['selectedOrdersSelectionSummary']().totalsByCurrency).toEqual([
      { currencyCode: 'MXN', amount: 0 }
    ]);
    expect(fixture.nativeElement.textContent).toContain('0 órdenes seleccionadas');
    expect(fixture.nativeElement.textContent).toContain('Total seleccionado:');
    expect(fixture.nativeElement.textContent).not.toContain('Limpiar selección');
  });

  it('selects one eligible order and shows the bulk counter', async () => {
    const { fixture } = await configure();
    const order = fixture.componentInstance['ordersPage']()!.items[0];

    fixture.componentInstance['toggleOrderSelection'](order, true);
    fixture.detectChanges();

    expect(fixture.componentInstance['selectedOrdersCount']()).toBe(1);
    expect(fixture.componentInstance['selectedOrdersSelectionSummary']().totalsByCurrency).toEqual([
      { currencyCode: 'MXN', amount: 116 }
    ]);
    expect(fixture.nativeElement.textContent).toContain('1 orden seleccionada');
    expect(fixture.nativeElement.textContent).toContain('Total seleccionado:');
    expect(fixture.nativeElement.textContent).toContain('Crear documento de facturación');
  });

  it('updates the selected total when selecting and deselecting orders', async () => {
    const { fixture } = await configure({
      searchLegacyOrders: vi.fn().mockReturnValue(of({
        isSuccess: true,
        items: [
          createLegacyOrder({ legacyOrderId: 'LEG-1001', total: 116 }),
          createLegacyOrder({ legacyOrderId: 'LEG-1002', total: 58 })
        ],
        totalCount: 2,
        totalPages: 1,
        page: 1,
        pageSize: 10
      }))
    });
    const [firstOrder, secondOrder] = fixture.componentInstance['ordersPage']()!.items;

    fixture.componentInstance['toggleOrderSelection'](firstOrder, true);
    fixture.componentInstance['toggleOrderSelection'](secondOrder, true);

    expect(fixture.componentInstance['selectedOrdersCount']()).toBe(2);
    expect(fixture.componentInstance['selectedOrdersSelectionSummary']().totalsByCurrency).toEqual([
      { currencyCode: 'MXN', amount: 174 }
    ]);

    fixture.componentInstance['toggleOrderSelection'](firstOrder, false);

    expect(fixture.componentInstance['selectedOrdersCount']()).toBe(1);
    expect(fixture.componentInstance['selectedOrdersSelectionSummary']().totalsByCurrency).toEqual([
      { currencyCode: 'MXN', amount: 58 }
    ]);
  });

  it('resets the selected total when clearing the selection', async () => {
    const { fixture } = await configure();
    const order = fixture.componentInstance['ordersPage']()!.items[0];

    fixture.componentInstance['toggleOrderSelection'](order, true);
    fixture.componentInstance['clearBulkSelection']();

    expect(fixture.componentInstance['selectedOrdersCount']()).toBe(0);
    expect(fixture.componentInstance['selectedOrdersSelectionSummary']().totalsByCurrency).toEqual([
      { currencyCode: 'MXN', amount: 0 }
    ]);
  });

  it('groups selected totals by currency when currency is available', async () => {
    const { fixture } = await configure({
      searchLegacyOrders: vi.fn().mockReturnValue(of({
        isSuccess: true,
        items: [
          createLegacyOrder({ legacyOrderId: 'LEG-1001', total: 8500, currencyCode: 'MXN' }),
          createLegacyOrder({ legacyOrderId: 'LEG-1002', total: 320, currencyCode: 'USD' }),
          createLegacyOrder({ legacyOrderId: 'LEG-1003', total: 1500, currencyCode: 'MXN' })
        ],
        totalCount: 3,
        totalPages: 1,
        page: 1,
        pageSize: 10
      }))
    });

    fixture.componentInstance['toggleVisibleSelection'](true);

    expect(fixture.componentInstance['selectedOrdersCount']()).toBe(3);
    expect(fixture.componentInstance['selectedOrdersSelectionSummary']().totalsByCurrency).toEqual([
      { currencyCode: 'MXN', amount: 10000 },
      { currencyCode: 'USD', amount: 320 }
    ]);
  });

  it('treats an invalid selected order total as zero', async () => {
    const { fixture } = await configure({
      searchLegacyOrders: vi.fn().mockReturnValue(of({
        isSuccess: true,
        items: [
          createLegacyOrder({ legacyOrderId: 'LEG-1001', total: null as unknown as number }),
          createLegacyOrder({ legacyOrderId: 'LEG-1002', total: 58 })
        ],
        totalCount: 2,
        totalPages: 1,
        page: 1,
        pageSize: 10
      }))
    });

    fixture.componentInstance['toggleVisibleSelection'](true);

    expect(fixture.componentInstance['selectedOrdersSelectionSummary']().totalsByCurrency).toEqual([
      { currencyCode: 'MXN', amount: 58 }
    ]);
  });

  it('allows selecting visible orders already associated to billing and blocks only the mass billing action', async () => {
    const { fixture } = await configure({
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
          },
          {
            legacyOrderId: 'LEG-1002',
            orderDateUtc: '2026-03-23T09:00:00Z',
            customerName: 'Cliente Uno',
            total: 58,
            legacyOrderType: 'F',
            isImported: true,
            salesOrderId: 22,
            billingDocumentId: 44,
            billingDocumentStatus: 'Draft',
            fiscalDocumentId: null,
            fiscalDocumentStatus: null,
            importStatus: 'Imported'
          }
        ],
        totalCount: 2,
        totalPages: 1,
        page: 1,
        pageSize: 10
      }))
    });

    fixture.componentInstance['toggleVisibleSelection'](true);
    fixture.detectChanges();

    expect(fixture.componentInstance['selectedOrdersCount']()).toBe(2);
    expect(fixture.componentInstance['selectedOrdersHaveBillingConflicts']()).toBe(true);
    expect(fixture.componentInstance['canCreateBillingFromSelection']()).toBe(false);
    expect(fixture.nativeElement.textContent).toContain('Solo resumen');
    expect(fixture.nativeElement.textContent).toContain('La selección contiene órdenes ya asociadas a facturación. Puedes enviarlas en resumen, pero para crear un documento de facturación debes quitarlas de la selección.');
  });

  it('offers selecting all filtered orders and sends the filter snapshot on confirmation', async () => {
    const createBulkBillingDocument = vi.fn().mockReturnValue(of({
      outcome: 'Created',
      isSuccess: true,
      billingDocumentId: 45,
      billingDocumentStatus: 'Draft',
      selectedOrderCount: 12,
      importedOrderCount: 12,
      associatedOrderCount: 12,
      legacyOrderIds: ['LEG-1001', 'LEG-1002'],
      orderErrors: []
    }));
    const firstPageOrders = Array.from({ length: 10 }, (_, index) =>
      createLegacyOrder({ legacyOrderId: `LEG-10${index}`, total: 116 + index }));
    const secondPageOrders = [
      createLegacyOrder({ legacyOrderId: 'LEG-2001', total: 58 }),
      createLegacyOrder({ legacyOrderId: 'LEG-2002', total: 75 })
    ];
    const searchLegacyOrders = vi
      .fn()
      .mockReturnValueOnce(of(createSearchResponse(firstPageOrders, { totalCount: 12, totalPages: 2 })))
      .mockReturnValueOnce(of(createSearchResponse(firstPageOrders, { totalCount: 12, totalPages: 2 })))
      .mockReturnValueOnce(of(createSearchResponse(firstPageOrders, { totalCount: 12, totalPages: 2 })))
      .mockReturnValueOnce(of(createSearchResponse(secondPageOrders, { totalCount: 12, totalPages: 2, page: 2 })));
    const { fixture, api, router, feedback } = await configure({
      apiOverrides: {
        searchLegacyOrders,
        createBulkBillingDocument
      }
    });

    fixture.componentInstance['customerQuery'].set('Cliente Uno');
    await fixture.componentInstance['searchCurrentRange']();
    fixture.componentInstance['toggleVisibleSelection'](true);
    await fixture.componentInstance['selectAllFilteredOrders']();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('12 órdenes seleccionadas según los filtros actuales');

    fixture.componentInstance['openBulkCreateModal']();
    await fixture.componentInstance['confirmBulkCreateBillingDocument']();

    expect(createBulkBillingDocument).toHaveBeenCalledWith({
      documentType: 'I',
      selectionMode: 'Filtered',
      filters: {
        customerQuery: 'Cliente Uno'
      }
    });
    expect(feedback.show).toHaveBeenCalledWith('success', 'Documento de facturación creado con 12 órdenes.');
    expect(router.navigate).toHaveBeenCalledWith(['/app/fiscal-documents'], { queryParams: { billingDocumentId: 45 } });
    expect(api.createBulkBillingDocument).toHaveBeenCalledTimes(1);
  });

  it('calculates the selected total for all filtered orders across pages', async () => {
    const firstPageOrders = Array.from({ length: 10 }, (_, index) =>
      createLegacyOrder({ legacyOrderId: `LEG-10${index}`, total: 10 }));
    const secondPageOrders = [
      createLegacyOrder({ legacyOrderId: 'LEG-2001', total: 20 }),
      createLegacyOrder({ legacyOrderId: 'LEG-2002', total: 30 })
    ];
    const searchLegacyOrders = vi
      .fn()
      .mockReturnValueOnce(of(createSearchResponse(firstPageOrders, { totalCount: 12, totalPages: 2 })))
      .mockReturnValueOnce(of(createSearchResponse(firstPageOrders, { totalCount: 12, totalPages: 2 })))
      .mockReturnValueOnce(of(createSearchResponse(firstPageOrders, { totalCount: 12, totalPages: 2 })))
      .mockReturnValueOnce(of(createSearchResponse(secondPageOrders, { totalCount: 12, totalPages: 2, page: 2 })));
    const { fixture } = await configure({ searchLegacyOrders });

    fixture.componentInstance['customerQuery'].set('Cliente Uno');
    await fixture.componentInstance['searchCurrentRange']();
    fixture.componentInstance['toggleVisibleSelection'](true);
    await fixture.componentInstance['selectAllFilteredOrders']();

    expect(fixture.componentInstance['selectedOrdersCount']()).toBe(12);
    expect(fixture.componentInstance['selectedOrdersTotalsReady']()).toBe(true);
    expect(fixture.componentInstance['selectedOrdersSelectionSummary']().totalsByCurrency).toEqual([
      { currencyCode: 'MXN', amount: 150 }
    ]);
  });

  it('clears the current selection when filters are applied again', async () => {
    const { fixture } = await configure();
    const order = fixture.componentInstance['ordersPage']()!.items[0];

    fixture.componentInstance['toggleOrderSelection'](order, true);
    fixture.componentInstance['setCustomerQuery']('Cliente Uno');
    await fixture.componentInstance['searchCurrentRange']();

    expect(fixture.componentInstance['selectedOrdersCount']()).toBe(0);
  });

  it('clears the current selection when editing customerQuery', async () => {
    const { fixture } = await configure();
    const order = fixture.componentInstance['ordersPage']()!.items[0];

    fixture.componentInstance['toggleOrderSelection'](order, true);
    fixture.componentInstance['setCustomerQuery']('Cliente Uno');

    expect(fixture.componentInstance['selectedOrdersCount']()).toBe(0);
  });

  it('clears the current selection when editing legacyOrderId', async () => {
    const { fixture } = await configure();
    const order = fixture.componentInstance['ordersPage']()!.items[0];

    fixture.componentInstance['toggleOrderSelection'](order, true);
    fixture.componentInstance['setLegacyOrderIdFilter']('117-5479');

    expect(fixture.componentInstance['selectedOrdersCount']()).toBe(0);
  });

  it('clears the current selection when editing fromDate', async () => {
    const { fixture } = await configure();
    const order = fixture.componentInstance['ordersPage']()!.items[0];

    fixture.componentInstance['setQuickRange']('custom');
    fixture.componentInstance['toggleOrderSelection'](order, true);
    fixture.componentInstance['setFromDate']('2026-03-20');

    expect(fixture.componentInstance['selectedOrdersCount']()).toBe(0);
  });

  it('clears the current selection when editing toDate', async () => {
    const { fixture } = await configure();
    const order = fixture.componentInstance['ordersPage']()!.items[0];

    fixture.componentInstance['setQuickRange']('custom');
    fixture.componentInstance['toggleOrderSelection'](order, true);
    fixture.componentInstance['setToDate']('2026-03-21');

    expect(fixture.componentInstance['selectedOrdersCount']()).toBe(0);
  });

  it('clears the current selection when changing quickRange', async () => {
    const { fixture } = await configure();
    const order = fixture.componentInstance['ordersPage']()!.items[0];

    fixture.componentInstance['toggleOrderSelection'](order, true);
    fixture.componentInstance['setQuickRange']('today');

    expect(fixture.componentInstance['selectedOrdersCount']()).toBe(0);
  });

  it('keeps the current selection when changing page without changing filters', async () => {
    const searchLegacyOrders = vi
      .fn()
      .mockReturnValueOnce(of({
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
        totalCount: 2,
        totalPages: 2,
        page: 1,
        pageSize: 10
      }))
      .mockReturnValueOnce(of({
        isSuccess: true,
        items: [
          {
            legacyOrderId: 'LEG-1002',
            orderDateUtc: '2026-03-22T10:00:00Z',
            customerName: 'Cliente Dos',
            total: 58,
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
        totalCount: 2,
        totalPages: 2,
        page: 2,
        pageSize: 10
      }));

    const { fixture } = await configure({
      searchLegacyOrders
    });
    const order = fixture.componentInstance['ordersPage']()!.items[0];

    fixture.componentInstance['toggleOrderSelection'](order, true);
    await fixture.componentInstance['changePage'](1);

    expect(fixture.componentInstance['selectedOrdersCount']()).toBe(1);
    expect(fixture.componentInstance['selectedLegacyOrderIds']()).toEqual(['LEG-1001']);
  });

  it('blocks selecting all filtered orders when there are no active filters', async () => {
    const { fixture } = await configure({
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
        totalCount: 12,
        totalPages: 2,
        page: 1,
        pageSize: 10
      }))
    });
    const order = fixture.componentInstance['ordersPage']()!.items[0];

    fixture.componentInstance['toggleOrderSelection'](order, true);
    fixture.componentInstance['selectAllFilteredOrders']();

    expect(fixture.componentInstance['bulkActionError']()).toContain('Aplica al menos un filtro');
  });

  it('shows a clear error when filtered selection exceeds the 50-order limit', async () => {
    const { fixture } = await configure({
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
        totalCount: 51,
        totalPages: 6,
        page: 1,
        pageSize: 10
      }))
    });

    fixture.componentInstance['setCustomerQuery']('Cliente Uno');
    await fixture.componentInstance['searchCurrentRange']();
    fixture.componentInstance['toggleVisibleSelection'](true);
    fixture.componentInstance['selectAllFilteredOrders']();

    expect(fixture.componentInstance['bulkActionError']()).toContain('hasta 50 órdenes');
  });

  it('does not clear selection during initial query param hydration', async () => {
    const clearBulkSelectionSpy = vi.spyOn(OrdersOperationsPageComponent.prototype as any, 'clearBulkSelection');

    await configure({
      queryParams: {
        quickRange: 'today',
        customerQuery: 'Cliente Uno',
        legacyOrderId: '1175479'
      }
    });

    expect(clearBulkSelectionSpy).not.toHaveBeenCalled();
    clearBulkSelectionSpy.mockRestore();
  });

  it('does not call the bulk endpoint when the confirmation modal is cancelled', async () => {
    const { fixture, api } = await configure();
    const order = fixture.componentInstance['ordersPage']()!.items[0];

    fixture.componentInstance['toggleOrderSelection'](order, true);
    fixture.componentInstance['openBulkCreateModal']();
    fixture.componentInstance['closeBulkCreateModal']();
    fixture.detectChanges();

    expect(fixture.componentInstance['showBulkCreateModal']()).toBe(false);
    expect(api.createBulkBillingDocument).not.toHaveBeenCalled();
  });

  it('renders backend incompatibility errors for the bulk flow', async () => {
    const { fixture } = await configure({
      createBulkBillingDocument: vi.fn().mockReturnValue(throwError(() =>
        new HttpErrorResponse({
          status: 400,
          error: {
            outcome: 'ValidationFailed',
            isSuccess: false,
            errorMessage: 'One or more selected legacy orders are not compatible for a single billing document.',
            selectedOrderCount: 2,
            importedOrderCount: 0,
            associatedOrderCount: 0,
            legacyOrderIds: ['LEG-1001', 'LEG-1002'],
            orderErrors: [
              {
                legacyOrderId: 'LEG-1002',
                errorCode: 'DifferentCustomer',
                errorMessage: "Legacy order 'LEG-1002' belongs to a different customer than Legacy order 'LEG-1001'."
              }
            ]
          }
        })))
    });
    const order = fixture.componentInstance['ordersPage']()!.items[0];

    fixture.componentInstance['toggleOrderSelection'](order, true);
    fixture.componentInstance['openBulkCreateModal']();
    await fixture.componentInstance['confirmBulkCreateBillingDocument']();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Órdenes no compatibles');
    expect(fixture.nativeElement.textContent).toContain("Legacy order 'LEG-1002' belongs to a different customer");
  });

  it('hydrates an explicit quick range from the URL', async () => {
    const { fixture, api } = await configure({
      queryParams: { quickRange: 'today' }
    });

    expect(fixture.componentInstance['quickRange']()).toBe('today');
    expect(api.searchLegacyOrders).toHaveBeenCalledWith({
      fromDate: '2026-03-23',
      toDate: '2026-03-23',
      legacyOrderId: '',
      customerQuery: '',
      page: 1,
      pageSize: 10
    });
  });

  it('hydrates explicit date params from the URL as a custom range', async () => {
    const { fixture, api } = await configure({
      queryParams: {
        fromDate: '2026-03-20',
        toDate: '2026-03-21'
      }
    });

    expect(fixture.componentInstance['quickRange']()).toBe('custom');
    expect(api.searchLegacyOrders).toHaveBeenCalledWith({
      fromDate: '2026-03-20',
      toDate: '2026-03-21',
      legacyOrderId: '',
      customerQuery: '',
      page: 1,
      pageSize: 10
    });
  });

  it('does not trigger a search when only fromDate is present in query params', async () => {
    const { fixture, api } = await configure({
      queryParams: {
        fromDate: '2026-03-20'
      }
    });

    expect(fixture.componentInstance['quickRange']()).toBe('custom');
    expect(fixture.componentInstance['fromDate']()).toBe('2026-03-20');
    expect(fixture.componentInstance['toDate']()).toBe('');
    expect(api.searchLegacyOrders).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Captura una fecha inicial y una fecha final válidas.');
  });

  it('does not trigger a search when only toDate is present in query params', async () => {
    const { fixture, api } = await configure({
      queryParams: {
        toDate: '2026-03-21'
      }
    });

    expect(fixture.componentInstance['quickRange']()).toBe('custom');
    expect(fixture.componentInstance['fromDate']()).toBe('');
    expect(fixture.componentInstance['toDate']()).toBe('2026-03-21');
    expect(api.searchLegacyOrders).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Captura una fecha inicial y una fecha final válidas.');
  });

  it('applies customer filter to the legacy orders search without importing automatically', async () => {
    const { fixture, api } = await configure();

    fixture.componentInstance['setCustomerQuery']('Cliente Uno');
    await fixture.componentInstance['searchCurrentRange']();

    expect(api.searchLegacyOrders).toHaveBeenLastCalledWith({
      legacyOrderId: '',
      customerQuery: 'Cliente Uno',
      page: 1,
      pageSize: 10
    });
    expect(api.importLegacyOrder).toHaveBeenCalledTimes(0);
  });

  it('applies the exact legacy order filter to the legacy orders search', async () => {
    const { fixture, api } = await configure();

    fixture.componentInstance['setLegacyOrderIdFilter']('117-5479');
    await fixture.componentInstance['searchCurrentRange']();

    expect(api.searchLegacyOrders).toHaveBeenLastCalledWith({
      legacyOrderId: '1175479',
      customerQuery: '',
      page: 1,
      pageSize: 10
    });
    expect(api.importLegacyOrder).toHaveBeenCalledTimes(0);
  });

  it('applies today only when selected explicitly', async () => {
    const { fixture, api } = await configure();

    fixture.componentInstance['setQuickRange']('today');

    expect(api.searchLegacyOrders).toHaveBeenLastCalledWith({
      fromDate: '2026-03-23',
      toDate: '2026-03-23',
      legacyOrderId: '',
      customerQuery: '',
      page: 1,
      pageSize: 10
    });
  });

  it('applies the last 7 days when another quick range is selected', async () => {
    const { fixture, api } = await configure();

    fixture.componentInstance['setQuickRange']('last7');

    expect(api.searchLegacyOrders).toHaveBeenLastCalledWith({
      fromDate: '2026-03-17',
      toDate: '2026-03-23',
      legacyOrderId: '',
      customerQuery: '',
      page: 1,
      pageSize: 10
    });
  });

  it('clears date filters when the quick range returns to neutral and preserves other filters', async () => {
    const { fixture, api } = await configure();

    fixture.componentInstance['setLegacyOrderIdFilter']('117-5479');
    fixture.componentInstance['setCustomerQuery']('Cliente Uno');
    fixture.componentInstance['setQuickRange']('today');
    fixture.componentInstance['setQuickRange']('');

    expect(api.searchLegacyOrders).toHaveBeenLastCalledWith({
      legacyOrderId: '1175479',
      customerQuery: 'Cliente Uno',
      page: 1,
      pageSize: 10
    });
  });

  it('shows custom range validation and avoids invalid search', async () => {
    const { fixture, api } = await configure();

    fixture.componentInstance['setQuickRange']('custom');
    fixture.componentInstance['setFromDate']('2026-03-24');
    fixture.componentInstance['setToDate']('2026-03-23');
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

  it('renders the enriched legacy hash conflict and does not attempt reimport beyond the failed call', async () => {
    const createBillingDocument = vi.fn();
    const { fixture, api } = await configure({
      importLegacyOrder: vi.fn().mockReturnValue(throwError(() =>
        new HttpErrorResponse({
          status: 409,
          error: {
            outcome: 'Conflict',
            isSuccess: false,
            errorCode: 'LegacyOrderAlreadyImportedWithDifferentSourceHash',
            errorMessage: "Legacy order 'LEG-1001' was already imported with a different source hash.",
            sourceSystem: 'legacy',
            sourceTable: 'pedidos',
            legacyOrderId: 'LEG-1001',
            sourceHash: 'current-hash',
            existingSalesOrderId: 20,
            existingSalesOrderStatus: 'SnapshotCreated',
            existingBillingDocumentId: 31,
            existingBillingDocumentStatus: 'Draft',
            existingFiscalDocumentId: 41,
            existingFiscalDocumentStatus: 'Stamped',
            fiscalUuid: 'UUID-LEG-1001',
            importedAtUtc: '2026-04-01T12:00:00Z',
            existingSourceHash: 'old-hash',
            currentSourceHash: 'current-hash',
            allowedActions: ['view_existing_sales_order', 'view_existing_billing_document', 'view_existing_fiscal_document', 'reimport_not_available']
          }
        }))),
      createBillingDocument
    });

    fixture.componentInstance['legacyOrderId'] = 'LEG-1001';
    await fixture.componentInstance['importOrderManually']();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('La orden legacy LEG-1001 cambió después de la importación');
    expect(fixture.nativeElement.textContent).toContain('UUID-LEG-1001');
    expect(fixture.nativeElement.textContent).toContain('old-hash');
    expect(fixture.nativeElement.textContent).toContain('current-hash');
    expect(api.importLegacyOrder).toHaveBeenCalledTimes(1);
    expect(createBillingDocument).not.toHaveBeenCalled();
  });

  it('opens the existing billing or fiscal document from the enriched conflict actions', async () => {
    const { fixture, router } = await configure({
      importLegacyOrder: vi.fn().mockReturnValue(throwError(() =>
        new HttpErrorResponse({
          status: 409,
          error: {
            outcome: 'Conflict',
            isSuccess: false,
            errorCode: 'LegacyOrderAlreadyImportedWithDifferentSourceHash',
            errorMessage: "Legacy order 'LEG-1001' was already imported with a different source hash.",
            sourceSystem: 'legacy',
            sourceTable: 'pedidos',
            legacyOrderId: 'LEG-1001',
            sourceHash: 'current-hash',
            existingSalesOrderId: 20,
            existingBillingDocumentId: 31,
            existingBillingDocumentStatus: 'Draft',
            existingFiscalDocumentId: 41,
            existingFiscalDocumentStatus: 'Stamped',
            allowedActions: ['view_existing_sales_order', 'view_existing_billing_document', 'view_existing_fiscal_document', 'reimport_not_available']
          }
        })))
    });

    fixture.componentInstance['legacyOrderId'] = 'LEG-1001';
    await fixture.componentInstance['importOrderManually']();
    fixture.detectChanges();

    const conflict = fixture.componentInstance['importConflict']();
    expect(conflict).not.toBeNull();

    await fixture.componentInstance['openExistingBillingDocumentConflict'](conflict!);
    expect(router.navigate).toHaveBeenCalledWith(['/app/fiscal-documents'], { queryParams: { billingDocumentId: 31 } });

    await fixture.componentInstance['openExistingFiscalDocumentConflict'](conflict!);
    expect(router.navigate).toHaveBeenCalledWith(['/app/fiscal-documents', 41], { queryParams: { billingDocumentId: 31 } });
  });

  it('renders the import preview summary and detail without triggering reimport', async () => {
    const { fixture, api } = await configure({
      importLegacyOrder: vi.fn().mockReturnValue(throwError(() =>
        new HttpErrorResponse({
          status: 409,
          error: {
            outcome: 'Conflict',
            isSuccess: false,
            errorCode: 'LegacyOrderAlreadyImportedWithDifferentSourceHash',
            errorMessage: "Legacy order 'LEG-1001' was already imported with a different source hash.",
            sourceSystem: 'legacy',
            sourceTable: 'pedidos',
            legacyOrderId: 'LEG-1001',
            sourceHash: 'current-hash',
            existingSalesOrderId: 20,
            allowedActions: ['preview_reimport', 'reimport_not_available']
          }
        })))
    });

    fixture.componentInstance['legacyOrderId'] = 'LEG-1001';
    await fixture.componentInstance['importOrderManually']();
    await fixture.componentInstance['loadImportPreview']('LEG-1001');
    fixture.detectChanges();

    expect(api.previewLegacyOrderImport).toHaveBeenCalledWith('LEG-1001');
    expect(fixture.nativeElement.textContent).toContain('Comparación segura de snapshot vs Legacy actual');
    expect(fixture.nativeElement.textContent).toContain('Líneas modificadas');
    expect(fixture.nativeElement.textContent).toContain('Campos modificados: quantity, unitPrice, lineTotal');
    expect(fixture.nativeElement.textContent).toContain('Elegibilidad: Allowed');
    expect(fixture.nativeElement.textContent).toContain('Reimportar');
    expect(api.importLegacyOrder).toHaveBeenCalledTimes(1);
  });

  it('renders the revision history and marks the current revision', async () => {
    const { fixture, api } = await configure();

    fixture.componentInstance['selectedLegacyOrderId'].set('LEG-1001');
    await fixture.componentInstance['loadRevisionHistory']('LEG-1001');
    fixture.detectChanges();

    expect(api.listLegacyOrderImportRevisions).toHaveBeenCalledWith('LEG-1001');
    expect(fixture.nativeElement.textContent).toContain('Historial de importaciones Legacy');
    expect(fixture.nativeElement.textContent).toContain('Revisión 1');
    expect(fixture.nativeElement.textContent).toContain('Actual');
  });

  it('renders blocked preview eligibility clearly', async () => {
    const { fixture } = await configure({
      importLegacyOrder: vi.fn().mockReturnValue(throwError(() =>
        new HttpErrorResponse({
          status: 409,
          error: {
            outcome: 'Conflict',
            isSuccess: false,
            errorCode: 'LegacyOrderAlreadyImportedWithDifferentSourceHash',
            errorMessage: "Legacy order 'LEG-1001' was already imported with a different source hash.",
            sourceSystem: 'legacy',
            sourceTable: 'pedidos',
            legacyOrderId: 'LEG-1001',
            sourceHash: 'current-hash',
            existingSalesOrderId: 20,
            allowedActions: ['preview_reimport', 'reimport_not_available']
          }
        }))),
      previewLegacyOrderImport: vi.fn().mockReturnValue(of({
        isSuccess: true,
        legacyOrderId: 'LEG-1001',
        existingSourceHash: 'old-hash',
        currentSourceHash: 'new-hash',
        currentRevisionNumber: 2,
        hasChanges: true,
        changedOrderFields: [],
        changeSummary: {
          addedLines: 0,
          removedLines: 0,
          modifiedLines: 1,
          unchangedLines: 0,
          oldSubtotal: 100,
          newSubtotal: 150,
          oldTotal: 116,
          newTotal: 174
        },
        lineChanges: [],
        reimportEligibility: {
          status: 'BlockedByStampedFiscalDocument',
          reasonCode: 'FiscalDocumentStamped',
          reasonMessage: 'Reimport is blocked because the related fiscal document is already stamped.'
        },
        allowedActions: ['preview_reimport', 'reimport_not_available']
      }))
    });

    fixture.componentInstance['legacyOrderId'] = 'LEG-1001';
    await fixture.componentInstance['importOrderManually']();
    await fixture.componentInstance['loadImportPreview']('LEG-1001');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('BlockedByStampedFiscalDocument');
    expect(fixture.nativeElement.textContent).toContain('related fiscal document is already stamped');
    expect(fixture.nativeElement.textContent).not.toContain('Reimportar');
  });

  it('renders no-changes preview clearly', async () => {
    const { fixture } = await configure({
      importLegacyOrder: vi.fn().mockReturnValue(throwError(() =>
        new HttpErrorResponse({
          status: 409,
          error: {
            outcome: 'Conflict',
            isSuccess: false,
            errorCode: 'LegacyOrderAlreadyImportedWithDifferentSourceHash',
            errorMessage: "Legacy order 'LEG-1001' was already imported with a different source hash.",
            sourceSystem: 'legacy',
            sourceTable: 'pedidos',
            legacyOrderId: 'LEG-1001',
            sourceHash: 'same-hash',
            existingSalesOrderId: 20,
            allowedActions: ['preview_reimport', 'reimport_not_available']
          }
        }))),
      previewLegacyOrderImport: vi.fn().mockReturnValue(of({
        isSuccess: true,
        legacyOrderId: 'LEG-1001',
        existingSourceHash: 'same-hash',
        currentSourceHash: 'same-hash',
        currentRevisionNumber: 2,
        hasChanges: false,
        changedOrderFields: [],
        changeSummary: {
          addedLines: 0,
          removedLines: 0,
          modifiedLines: 0,
          unchangedLines: 1,
          oldSubtotal: 100,
          newSubtotal: 100,
          oldTotal: 116,
          newTotal: 116
        },
        lineChanges: [],
        reimportEligibility: {
          status: 'NotNeededNoChanges',
          reasonCode: 'NoChangesDetected',
          reasonMessage: 'Reimport preview shows no changes between the current legacy order and the existing snapshot.'
        },
        allowedActions: ['preview_reimport', 'reimport_not_available']
      }))
    });

    fixture.componentInstance['legacyOrderId'] = 'LEG-1001';
    await fixture.componentInstance['importOrderManually']();
    await fixture.componentInstance['loadImportPreview']('LEG-1001');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No se detectaron cambios entre el snapshot existente y el estado actual de Legacy.');
    expect(fixture.nativeElement.textContent).toContain('NotNeededNoChanges');
    expect(fixture.nativeElement.textContent).not.toContain('Reimportar');
  });

  it('reimports from preview after explicit confirmation', async () => {
    const { fixture, api, feedback } = await configure({
      importLegacyOrder: vi.fn().mockReturnValue(throwError(() =>
        new HttpErrorResponse({
          status: 409,
          error: {
            outcome: 'Conflict',
            isSuccess: false,
            errorCode: 'LegacyOrderAlreadyImportedWithDifferentSourceHash',
            errorMessage: "Legacy order 'LEG-1001' was already imported with a different source hash.",
            sourceSystem: 'legacy',
            sourceTable: 'pedidos',
            legacyOrderId: 'LEG-1001',
            sourceHash: 'new-hash',
            existingSalesOrderId: 20,
            existingSourceHash: 'old-hash',
            currentSourceHash: 'new-hash',
            allowedActions: ['preview_reimport', 'reimport_not_available']
          }
        })))
    });

    fixture.componentInstance['legacyOrderId'] = 'LEG-1001';
    await fixture.componentInstance['importOrderManually']();
    await fixture.componentInstance['loadImportPreview']('LEG-1001');
    await fixture.componentInstance['executeReimport']('LEG-1001', fixture.componentInstance['importPreview']()!);
    fixture.detectChanges();

    expect(window.confirm).toHaveBeenCalled();
    expect(api.reimportLegacyOrder).toHaveBeenCalledWith('LEG-1001', {
      expectedExistingSourceHash: 'old-hash',
      expectedCurrentSourceHash: 'new-hash',
      confirmationMode: 'ReplaceExistingImport'
    });
    expect(feedback.show).toHaveBeenCalledWith('success', 'La importación existente fue reemplazada con el estado actual de Legacy.');
    expect(fixture.componentInstance['importConflict']()).toBeNull();
    expect(fixture.componentInstance['importedOrder']()?.outcome).toBe('Reimported');
  });

  it('does not reimport when the user cancels confirmation', async () => {
    vi.mocked(window.confirm).mockReturnValue(false);
    const { fixture, api } = await configure({
      importLegacyOrder: vi.fn().mockReturnValue(throwError(() =>
        new HttpErrorResponse({
          status: 409,
          error: {
            outcome: 'Conflict',
            isSuccess: false,
            errorCode: 'LegacyOrderAlreadyImportedWithDifferentSourceHash',
            errorMessage: "Legacy order 'LEG-1001' was already imported with a different source hash.",
            sourceSystem: 'legacy',
            sourceTable: 'pedidos',
            legacyOrderId: 'LEG-1001',
            sourceHash: 'new-hash',
            existingSalesOrderId: 20,
            existingSourceHash: 'old-hash',
            currentSourceHash: 'new-hash',
            allowedActions: ['preview_reimport', 'reimport_not_available']
          }
        })))
    });

    fixture.componentInstance['legacyOrderId'] = 'LEG-1001';
    await fixture.componentInstance['importOrderManually']();
    await fixture.componentInstance['loadImportPreview']('LEG-1001');
    await fixture.componentInstance['executeReimport']('LEG-1001', fixture.componentInstance['importPreview']()!);

    expect(api.reimportLegacyOrder).not.toHaveBeenCalled();
  });

  it('renders preview-expired reimport failure clearly', async () => {
    const { fixture } = await configure({
      importLegacyOrder: vi.fn().mockReturnValue(throwError(() =>
        new HttpErrorResponse({
          status: 409,
          error: {
            outcome: 'Conflict',
            isSuccess: false,
            errorCode: 'LegacyOrderAlreadyImportedWithDifferentSourceHash',
            errorMessage: "Legacy order 'LEG-1001' was already imported with a different source hash.",
            sourceSystem: 'legacy',
            sourceTable: 'pedidos',
            legacyOrderId: 'LEG-1001',
            sourceHash: 'new-hash',
            existingSalesOrderId: 20,
            existingSourceHash: 'old-hash',
            currentSourceHash: 'new-hash',
            allowedActions: ['preview_reimport', 'reimport_not_available']
          }
        }))),
      reimportLegacyOrder: vi.fn().mockReturnValue(throwError(() =>
        new HttpErrorResponse({
          status: 409,
          error: {
            outcome: 'Conflict',
            isSuccess: false,
            errorCode: 'ReimportPreviewExpired',
            errorMessage: 'Reimport preview is no longer current. Refresh the preview and confirm the new hashes before retrying.'
          }
        })))
    });

    fixture.componentInstance['legacyOrderId'] = 'LEG-1001';
    await fixture.componentInstance['importOrderManually']();
    await fixture.componentInstance['loadImportPreview']('LEG-1001');
    await fixture.componentInstance['executeReimport']('LEG-1001', fixture.componentInstance['importPreview']()!);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Refresh the preview and confirm the new hashes before retrying');
  });
});
