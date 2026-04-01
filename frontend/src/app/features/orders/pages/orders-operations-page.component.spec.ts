import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { OrdersOperationsPageComponent } from './orders-operations-page.component';
import { OrdersApiService } from '../infrastructure/orders-api.service';
import { FeedbackService } from '../../../core/ui/feedback.service';

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
        { provide: Router, useValue: router },
        { provide: ActivatedRoute, useValue: {} }
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
