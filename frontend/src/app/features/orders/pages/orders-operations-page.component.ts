import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { OrdersApiService } from '../infrastructure/orders-api.service';
import {
  CreateBillingDocumentResponse,
  ImportLegacyOrderResponse,
  ImportLegacyOrderAllowedAction,
  LegacyOrderListItem,
  SearchLegacyOrdersResponse
} from '../models/orders.models';
import { BillingDocumentCardComponent } from '../components/billing-document-card.component';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { StatusBadgeComponent } from '../../../shared/components/status-badge.component';
import { extractImportLegacyOrderConflict, ImportLegacyOrderConflictViewModel } from '../application/import-legacy-order-conflict';

type QuickRange = 'today' | 'yesterday' | 'last7' | 'custom';

@Component({
  selector: 'app-orders-operations-page',
  imports: [FormsModule, CurrencyPipe, DatePipe, BillingDocumentCardComponent, StatusBadgeComponent],
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">Órdenes y preparación de facturación</p>
        <h2>Consulta órdenes legadas, impórtalas y continúa el flujo fiscal</h2>
      </header>

      <section class="card">
        <div class="section-header">
          <div>
            <p class="eyebrow">Consulta Legacy</p>
            <h3>Órdenes legadas</h3>
          </div>
          @if (loadingOrders()) {
            <span class="helper">Consultando órdenes...</span>
          }
        </div>

        <form class="filters-grid" (ngSubmit)="searchCustomRange()">
          <label>
            <span>Rango rápido</span>
            <select [ngModel]="quickRange()" name="quickRange" (ngModelChange)="setQuickRange($event)">
              <option value="today">Hoy</option>
              <option value="yesterday">Ayer</option>
              <option value="last7">Últimos 7 días</option>
              <option value="custom">Personalizado</option>
            </select>
          </label>

          @if (quickRange() === 'custom') {
            <label>
              <span>Fecha inicial</span>
              <input [ngModel]="fromDate()" (ngModelChange)="fromDate.set($event)" name="fromDate" type="date" [max]="today()" />
            </label>

            <label>
              <span>Fecha final</span>
              <input [ngModel]="toDate()" (ngModelChange)="toDate.set($event)" name="toDate" type="date" [max]="today()" />
            </label>

            <div class="actions align-end">
              <button type="submit" [disabled]="loadingOrders() || customRangeError() !== null">
                {{ loadingOrders() ? 'Buscando...' : 'Buscar' }}
              </button>
            </div>
          }

          <label>
            <span>Cliente</span>
            <input
              [ngModel]="customerQuery()"
              (ngModelChange)="customerQuery.set($event)"
              name="customerQuery"
              placeholder="Buscar por cliente"
            />
          </label>

          @if (quickRange() !== 'custom') {
            <div class="actions align-end">
              <button type="button" class="secondary" (click)="searchCurrentRange()" [disabled]="loadingOrders()">
                {{ loadingOrders() ? 'Filtrando...' : 'Aplicar filtro' }}
              </button>
            </div>
          }
        </form>

        @if (customRangeError(); as rangeError) {
          <p class="error">{{ rangeError }}</p>
        }

        @if (ordersError(); as ordersError) {
          <p class="error">{{ ordersError }}</p>
        }

        @if (loadingOrders()) {
          <p class="helper">Cargando órdenes legadas...</p>
        } @else if (ordersPage()?.items?.length) {
          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Id orden legacy</th>
                  <th>Fecha</th>
                  <th>Cliente</th>
                  <th>Total</th>
                  <th>Estado</th>
                  <th>Acción</th>
                </tr>
              </thead>
              <tbody>
                @for (order of ordersPage()!.items; track order.legacyOrderId) {
                  <tr [class.selected]="selectedLegacyOrderId() === order.legacyOrderId">
                    <td>{{ order.legacyOrderId }}</td>
                    <td>{{ order.orderDateUtc | date:'dd/MM/yyyy HH:mm' }}</td>
                    <td>{{ order.customerName }}</td>
                    <td>{{ order.total | currency:'MXN':'symbol':'1.2-2' }}</td>
                    <td>
                      <app-status-badge
                        [label]="order.isImported ? 'Importada' : 'No importada'"
                        [tone]="order.isImported ? 'success' : 'warning'" />
                    </td>
                    <td>
                      @if (order.isImported) {
                        <button
                          type="button"
                          class="secondary"
                          (click)="continueOrder(order)"
                          [disabled]="loadingImportOrderId() === order.legacyOrderId || loadingBilling()">
                          Continuar
                        </button>
                      } @else {
                        <button
                          type="button"
                          (click)="importOrderFromList(order)"
                          [disabled]="loadingImportOrderId() === order.legacyOrderId || loadingBilling()">
                          {{ loadingImportOrderId() === order.legacyOrderId ? 'Importando...' : 'Importar orden' }}
                        </button>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <div class="pager">
            <span>Página {{ ordersPage()!.page }} de {{ ordersPage()!.totalPages || 1 }}</span>
            <div class="actions">
              <button type="button" class="secondary" (click)="changePage(-1)" [disabled]="loadingOrders() || ordersPage()!.page <= 1">Anterior</button>
              <button type="button" class="secondary" (click)="changePage(1)" [disabled]="loadingOrders() || ordersPage()!.page >= ordersPage()!.totalPages">Siguiente</button>
            </div>
          </div>
        } @else if (ordersPage()) {
          <p class="helper">No se encontraron órdenes en el rango seleccionado.</p>
        }
      </section>

      <section class="card">
        <div class="section-header">
          <div>
            <p class="eyebrow">Continuidad</p>
            <h3>Importación manual y continuación del flujo</h3>
          </div>
        </div>

        <form class="form-grid" (ngSubmit)="importOrderManually()">
          <label>
            <span>Id de orden legada</span>
            <input [(ngModel)]="legacyOrderId" name="legacyOrderId" placeholder="LEG-1001" required />
          </label>

          <label>
            <span>Tipo de documento de facturación</span>
            <select [(ngModel)]="documentType" name="documentType">
              <option value="I">Factura</option>
            </select>
          </label>

          <div class="actions align-end">
            <button type="submit" [disabled]="loadingImportOrderId() !== null || loadingBilling()">
              {{ loadingImportOrderId() === manualImportKey ? 'Importando...' : 'Importar orden' }}
            </button>
            <button
              type="button"
              class="secondary"
              (click)="createBillingDocument()"
              [disabled]="!importedOrder() || loadingBilling() || loadingImportOrderId() !== null">
              {{ loadingBilling() ? 'Creando...' : billingButtonLabel() }}
            </button>
          </div>
        </form>

        @if (localError()) {
          <p class="error">{{ localError() }}</p>
        } @else if (importConflict(); as conflict) {
          <section class="conflict-panel">
            <div class="section-header">
              <div>
                <p class="eyebrow">Conflicto detectado</p>
                <h3>La orden legacy {{ conflict.legacyOrderId }} cambió después de la importación</h3>
              </div>
              <app-status-badge label="Conflicto" tone="warning" />
            </div>

            <p class="helper">{{ conflict.errorMessage }}</p>

            <dl class="conflict-grid">
              <div><dt>Sales order existente</dt><dd>{{ conflict.existingSalesOrderId ?? 'N/D' }}</dd></div>
              <div><dt>Estatus sales order</dt><dd>{{ conflict.existingSalesOrderStatus ?? 'N/D' }}</dd></div>
              <div><dt>Billing document existente</dt><dd>{{ conflict.existingBillingDocumentId ?? 'N/D' }}</dd></div>
              <div><dt>Estatus billing</dt><dd>{{ conflict.existingBillingDocumentStatus ?? 'N/D' }}</dd></div>
              <div><dt>Fiscal document existente</dt><dd>{{ conflict.existingFiscalDocumentId ?? 'N/D' }}</dd></div>
              <div><dt>Estatus fiscal</dt><dd>{{ conflict.existingFiscalDocumentStatus ?? 'N/D' }}</dd></div>
              <div><dt>UUID fiscal</dt><dd>{{ conflict.fiscalUuid ?? 'N/D' }}</dd></div>
              <div><dt>Importada el</dt><dd>{{ conflict.importedAtUtc ? (conflict.importedAtUtc | date:'dd/MM/yyyy HH:mm') : 'N/D' }}</dd></div>
            </dl>

            <dl class="conflict-grid">
              <div><dt>Hash importado</dt><dd class="mono">{{ conflict.existingSourceHash ?? 'N/D' }}</dd></div>
              <div><dt>Hash actual</dt><dd class="mono">{{ conflict.currentSourceHash ?? 'N/D' }}</dd></div>
            </dl>

            <div class="actions">
              @if (hasAllowedAction(conflict, 'view_existing_sales_order') && conflict.existingSalesOrderId) {
                <button type="button" class="secondary" (click)="openExistingSalesOrderConflict(conflict)">
                  Ver sales order existente
                </button>
              }

              @if (hasAllowedAction(conflict, 'view_existing_billing_document') && conflict.existingBillingDocumentId) {
                <button type="button" class="secondary" (click)="openExistingBillingDocumentConflict(conflict)">
                  Abrir billing document existente
                </button>
              }

              @if (hasAllowedAction(conflict, 'view_existing_fiscal_document') && conflict.existingFiscalDocumentId) {
                <button type="button" class="secondary" (click)="openExistingFiscalDocumentConflict(conflict)">
                  Abrir fiscal document existente
                </button>
              }
            </div>

            <p class="helper">La reimportación real sigue bloqueada en esta fase. Usa las entidades existentes para revisar el caso.</p>
          </section>
        } @else {
          <p class="helper">Puedes seguir usando el flujo manual como respaldo, pero la operación principal ahora parte de la lista paginada de órdenes.</p>
        }
      </section>

      @if (importedOrder(); as importedOrder) {
        <app-billing-document-card [imported]="importedOrder" [billing]="billingDocument()" />
      }
    </section>
  `,
  styles: [`
    .page { display:grid; gap:1rem; }
    .card { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; display:grid; gap:1rem; }
    .section-header { display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    h2, h3 { margin:0.3rem 0 0; }
    .filters-grid, .form-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:1rem; align-items:end; }
    label { display:grid; gap:0.35rem; }
    input, select, button { font:inherit; }
    input, select { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; }
    .actions { display:flex; gap:0.75rem; flex-wrap:wrap; }
    .align-end { align-items:end; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    button.secondary { background:#d8c49b; color:#182533; }
    button:disabled { opacity:0.6; cursor:wait; }
    .error { color:#7a2020; margin:0; }
    .helper { color:#5f6b76; margin:0; }
    .table-wrap { overflow:auto; border:1px solid #eadfcb; border-radius:0.9rem; }
    table { width:100%; border-collapse:collapse; min-width:760px; }
    th, td { padding:0.85rem 0.9rem; border-bottom:1px solid #f1ece1; text-align:left; vertical-align:middle; }
    th { font-size:0.85rem; color:#5f6b76; background:#faf6ee; }
    tr.selected { background:#f7f1e3; }
    .pager { display:flex; justify-content:space-between; gap:1rem; align-items:center; flex-wrap:wrap; }
    .conflict-panel { border:1px solid #e6c981; border-radius:0.9rem; background:#fff8ea; padding:1rem; display:grid; gap:1rem; }
    .conflict-grid { margin:0; display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:0.75rem; }
    .conflict-grid dt { font-size:0.82rem; color:#6d6d6d; }
    .conflict-grid dd { margin:0.2rem 0 0; font-weight:600; }
    .mono { font-family:ui-monospace, SFMono-Regular, SFMono-Regular, Menlo, Consolas, monospace; overflow-wrap:anywhere; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class OrdersOperationsPageComponent implements OnInit {
  protected readonly manualImportKey = '__manual__';

  private readonly ordersApi = inject(OrdersApiService);
  private readonly feedbackService = inject(FeedbackService);
  private readonly router = inject(Router);

  protected legacyOrderId = '';
  protected documentType = 'I';
  protected readonly today = signal(getTodayDateString());
  protected readonly quickRange = signal<QuickRange>('today');
  protected readonly fromDate = signal('');
  protected readonly toDate = signal('');
  protected readonly customerQuery = signal('');
  protected readonly loadingOrders = signal(false);
  protected readonly loadingImportOrderId = signal<string | null>(null);
  protected readonly loadingBilling = signal(false);
  protected readonly ordersError = signal<string | null>(null);
  protected readonly localError = signal<string | null>(null);
  protected readonly ordersPage = signal<SearchLegacyOrdersResponse | null>(null);
  protected readonly importedOrder = signal<ImportLegacyOrderResponse | null>(null);
  protected readonly importConflict = signal<ImportLegacyOrderConflictViewModel | null>(null);
  protected readonly billingDocument = signal<CreateBillingDocumentResponse | null>(null);
  protected readonly selectedLegacyOrderId = signal<string | null>(null);
  protected readonly billingButtonLabel = computed(() =>
    this.billingDocument()?.billingDocumentId ? 'Abrir documento de facturación' : 'Crear documento de facturación');
  protected readonly customRangeError = computed(() => {
    if (this.quickRange() !== 'custom') {
      return null;
    }

    return validateCustomRange(this.fromDate(), this.toDate(), this.today());
  });

  ngOnInit(): void {
    this.applyQuickRange('today');
    void this.searchOrders(1);
  }

  protected setQuickRange(range: QuickRange): void {
    this.quickRange.set(range);
    this.ordersError.set(null);

    if (range === 'custom') {
      this.ordersPage.set(null);
      return;
    }

    this.applyQuickRange(range);
    void this.searchOrders(1);
  }

  protected async searchCustomRange(): Promise<void> {
    if (this.customRangeError()) {
      return;
    }

    await this.searchOrders(1);
  }

  protected async searchCurrentRange(): Promise<void> {
    await this.searchOrders(1);
  }

  protected async changePage(delta: number): Promise<void> {
    const currentPage = this.ordersPage()?.page ?? 1;
    const nextPage = currentPage + delta;

    if (nextPage < 1) {
      return;
    }

    await this.searchOrders(nextPage);
  }

  protected async importOrderManually(): Promise<void> {
    const legacyOrderId = this.legacyOrderId.trim();
    if (!legacyOrderId) {
      this.localError.set('Ingresa un id de orden legada.');
      return;
    }

    await this.importOrderInternal(legacyOrderId, this.manualImportKey);
  }

  protected async importOrderFromList(order: LegacyOrderListItem): Promise<void> {
    await this.importOrderInternal(order.legacyOrderId, order.legacyOrderId);
  }

  protected async continueOrder(order: LegacyOrderListItem): Promise<void> {
    this.localError.set(null);
    this.importConflict.set(null);
    this.selectedLegacyOrderId.set(order.legacyOrderId);
    this.legacyOrderId = order.legacyOrderId;
    this.importedOrder.set(toImportedOrder(order));

    if (order.billingDocumentId) {
      this.billingDocument.set({
        outcome: 'Existing',
        isSuccess: true,
        salesOrderId: order.salesOrderId ?? 0,
        billingDocumentId: order.billingDocumentId,
        billingDocumentStatus: order.billingDocumentStatus ?? null
      });
      await this.openBillingDocument(order.billingDocumentId);
      return;
    }

    this.billingDocument.set(null);
    this.feedbackService.show('info', 'La orden ya está importada. Puedes crear el documento de facturación para continuar.');
  }

  protected async createBillingDocument(): Promise<void> {
    const importedOrder = this.importedOrder();
    if (!importedOrder?.salesOrderId) {
      this.localError.set('Importa o selecciona una orden antes de crear el documento de facturación.');
      return;
    }

    if (this.billingDocument()?.billingDocumentId) {
      await this.openBillingDocument(this.billingDocument()?.billingDocumentId);
      return;
    }

    this.localError.set(null);
    this.loadingBilling.set(true);

    try {
      const response = await firstValueFrom(this.ordersApi.createBillingDocument(importedOrder.salesOrderId, { documentType: this.documentType }));
      this.billingDocument.set(response);
      this.updateOrderInPage(importedOrder.legacyOrderId, (order) => ({
        ...order,
        isImported: true,
        salesOrderId: importedOrder.salesOrderId ?? order.salesOrderId,
        billingDocumentId: response.billingDocumentId ?? order.billingDocumentId,
        billingDocumentStatus: response.billingDocumentStatus ?? order.billingDocumentStatus
      }));
      this.feedbackService.show('success', 'Documento de facturación creado.');
      await this.openBillingDocument(response.billingDocumentId);
    } catch (error) {
      const conflictResponse = extractBillingDocumentResponse(error);
      if (conflictResponse?.billingDocumentId && conflictResponse.outcome === 'Conflict') {
        this.billingDocument.set(conflictResponse);
        this.updateOrderInPage(importedOrder.legacyOrderId, (order) => ({
          ...order,
          isImported: true,
          salesOrderId: importedOrder.salesOrderId ?? order.salesOrderId,
          billingDocumentId: conflictResponse.billingDocumentId ?? order.billingDocumentId,
          billingDocumentStatus: conflictResponse.billingDocumentStatus ?? order.billingDocumentStatus
        }));
        this.feedbackService.show('info', 'La orden ya cuenta con un documento de facturación. Se abrirá el documento existente.');
        await this.openBillingDocument(conflictResponse.billingDocumentId);
      } else {
        this.localError.set(extractErrorMessage(error));
      }
    } finally {
      this.loadingBilling.set(false);
    }
  }

  private async searchOrders(page: number): Promise<void> {
    const range = resolveRange(this.quickRange(), this.fromDate(), this.toDate(), this.today());
    if (!range) {
      return;
    }

    this.ordersError.set(null);
    this.loadingOrders.set(true);

    try {
      const response = await firstValueFrom(this.ordersApi.searchLegacyOrders({
        fromDate: range.fromDate,
        toDate: range.toDate,
        customerQuery: this.customerQuery(),
        page,
        pageSize: 10
      }));
      this.ordersPage.set(response);
    } catch (error) {
      this.ordersError.set(extractErrorMessage(error));
    } finally {
      this.loadingOrders.set(false);
    }
  }

  private async importOrderInternal(legacyOrderId: string, loadingKey: string): Promise<void> {
    this.localError.set(null);
    this.importConflict.set(null);
    this.billingDocument.set(null);
    this.loadingImportOrderId.set(loadingKey);

    try {
      const response = await firstValueFrom(this.ordersApi.importLegacyOrder(legacyOrderId));
      this.legacyOrderId = legacyOrderId;
      this.selectedLegacyOrderId.set(legacyOrderId);
      this.importedOrder.set(response);
      this.updateOrderInPage(legacyOrderId, (order) => ({
        ...order,
        isImported: true,
        salesOrderId: response.salesOrderId ?? order.salesOrderId,
        importStatus: response.importStatus ?? order.importStatus
      }));
      this.feedbackService.show(
        'success',
        response.isIdempotent
          ? 'La orden ya había sido importada. Se reutilizó el snapshot.'
          : 'La orden legada se importó correctamente. Puedes continuar con el documento de facturación.');
    } catch (error) {
      const conflict = extractImportLegacyOrderConflict(error);
      if (conflict) {
        this.legacyOrderId = legacyOrderId;
        this.selectedLegacyOrderId.set(legacyOrderId);
        this.importConflict.set(conflict);
        this.localError.set(null);
        return;
      }

      this.localError.set(extractErrorMessage(error));
    } finally {
      this.loadingImportOrderId.set(null);
    }
  }

  protected hasAllowedAction(conflict: ImportLegacyOrderConflictViewModel, action: ImportLegacyOrderAllowedAction): boolean {
    return conflict.allowedActions.includes(action);
  }

  protected openExistingSalesOrderConflict(conflict: ImportLegacyOrderConflictViewModel): void {
    this.importedOrder.set({
      outcome: 'Conflict',
      isSuccess: false,
      isIdempotent: true,
      errorCode: conflict.errorCode,
      errorMessage: conflict.errorMessage,
      sourceSystem: 'legacy',
      sourceTable: 'pedidos',
      legacyOrderId: conflict.legacyOrderId,
      sourceHash: conflict.currentSourceHash ?? '',
      salesOrderId: conflict.existingSalesOrderId,
      importStatus: conflict.existingSalesOrderStatus,
      existingSalesOrderId: conflict.existingSalesOrderId,
      existingSalesOrderStatus: conflict.existingSalesOrderStatus,
      existingBillingDocumentId: conflict.existingBillingDocumentId,
      existingBillingDocumentStatus: conflict.existingBillingDocumentStatus,
      existingFiscalDocumentId: conflict.existingFiscalDocumentId,
      existingFiscalDocumentStatus: conflict.existingFiscalDocumentStatus,
      fiscalUuid: conflict.fiscalUuid,
      importedAtUtc: conflict.importedAtUtc,
      existingSourceHash: conflict.existingSourceHash,
      currentSourceHash: conflict.currentSourceHash,
      allowedActions: conflict.allowedActions
    });

    if (conflict.existingBillingDocumentId) {
      this.billingDocument.set({
        outcome: 'Conflict',
        isSuccess: false,
        errorMessage: conflict.errorMessage,
        salesOrderId: conflict.existingSalesOrderId ?? 0,
        billingDocumentId: conflict.existingBillingDocumentId,
        billingDocumentStatus: conflict.existingBillingDocumentStatus
      });
    }
  }

  protected async openExistingBillingDocumentConflict(conflict: ImportLegacyOrderConflictViewModel): Promise<void> {
    await this.openBillingDocument(conflict.existingBillingDocumentId);
  }

  protected async openExistingFiscalDocumentConflict(conflict: ImportLegacyOrderConflictViewModel): Promise<void> {
    if (!conflict.existingFiscalDocumentId) {
      return;
    }

    await this.router.navigate(['/app/fiscal-documents', conflict.existingFiscalDocumentId], {
      queryParams: conflict.existingBillingDocumentId ? { billingDocumentId: conflict.existingBillingDocumentId } : {}
    });
  }

  private updateOrderInPage(legacyOrderId: string, updater: (order: LegacyOrderListItem) => LegacyOrderListItem): void {
    const currentPage = this.ordersPage();
    if (!currentPage) {
      return;
    }

    this.ordersPage.set({
      ...currentPage,
      items: currentPage.items.map((order) => order.legacyOrderId === legacyOrderId ? updater(order) : order)
    });
  }

  private applyQuickRange(range: Exclude<QuickRange, 'custom'>): void {
    const today = parseDateInput(this.today());

    switch (range) {
      case 'today':
        this.fromDate.set(this.today());
        this.toDate.set(this.today());
        break;
      case 'yesterday': {
        const yesterday = addDays(today, -1);
        const value = toDateInputValue(yesterday);
        this.fromDate.set(value);
        this.toDate.set(value);
        break;
      }
      case 'last7':
        this.fromDate.set(toDateInputValue(addDays(today, -6)));
        this.toDate.set(this.today());
        break;
    }
  }

  private async openBillingDocument(billingDocumentId?: number | null): Promise<void> {
    if (!billingDocumentId) {
      return;
    }

    await this.router.navigate(['/app/fiscal-documents'], { queryParams: { billingDocumentId } });
  }
}

function extractErrorMessage(error: unknown): string {
  return extractApiErrorMessage(error);
}

function extractBillingDocumentResponse(error: unknown): CreateBillingDocumentResponse | null {
  if (!(error instanceof HttpErrorResponse) || typeof error.error !== 'object' || !error.error) {
    return null;
  }

  const payload = error.error as Partial<CreateBillingDocumentResponse>;
  return typeof payload.billingDocumentId === 'number' && typeof payload.outcome === 'string'
    ? {
        outcome: payload.outcome,
        isSuccess: !!payload.isSuccess,
        errorMessage: payload.errorMessage ?? null,
        salesOrderId: payload.salesOrderId ?? 0,
        billingDocumentId: payload.billingDocumentId,
        billingDocumentStatus: payload.billingDocumentStatus ?? null
      }
    : null;
}

function validateCustomRange(fromDate: string, toDate: string, today: string): string | null {
  if (!fromDate || !toDate) {
    return 'Captura una fecha inicial y una fecha final válidas.';
  }

  if (fromDate > toDate) {
    return 'La fecha inicial no puede ser mayor que la fecha final.';
  }

  if (toDate > today) {
    return 'La fecha final no puede ser mayor al día actual.';
  }

  return null;
}

function resolveRange(range: QuickRange, fromDate: string, toDate: string, today: string): { fromDate: string; toDate: string } | null {
  if (range === 'custom') {
    return validateCustomRange(fromDate, toDate, today) ? null : { fromDate, toDate };
  }

  return { fromDate, toDate };
}

function toImportedOrder(order: LegacyOrderListItem): ImportLegacyOrderResponse {
  return {
    outcome: 'Idempotent',
    isSuccess: true,
    isIdempotent: true,
    errorCode: null,
    sourceSystem: 'legacy',
    sourceTable: 'pedidos',
    legacyOrderId: order.legacyOrderId,
    sourceHash: '',
    salesOrderId: order.salesOrderId ?? null,
    importStatus: order.importStatus ?? 'Imported',
    allowedActions: []
  };
}

function getTodayDateString(): string {
  return toDateInputValue(new Date());
}

function toDateInputValue(value: Date): string {
  return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`;
}

function addDays(value: Date, days: number): Date {
  const result = new Date(value);
  result.setDate(result.getDate() + days);
  return result;
}

function parseDateInput(value: string): Date {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, month - 1, day);
}
