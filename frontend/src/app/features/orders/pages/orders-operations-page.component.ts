import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { OrdersApiService } from '../infrastructure/orders-api.service';
import {
  CreateBillingDocumentResponse,
  CreateBulkBillingDocumentFiltersRequest,
  CreateBulkBillingDocumentOrderError,
  CreateBulkBillingDocumentResponse,
  ImportLegacyOrderResponse,
  ImportLegacyOrderAllowedAction,
  ImportLegacyOrderPreviewResponse,
  ImportLegacyOrderRevisionHistoryResponse,
  LegacyOrderListItem,
  ReimportLegacyOrderResponse,
  SearchLegacyOrdersResponse
} from '../models/orders.models';
import { BillingDocumentCardComponent } from '../components/billing-document-card.component';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { StatusBadgeComponent } from '../../../shared/components/status-badge.component';
import { extractImportLegacyOrderConflict, ImportLegacyOrderConflictViewModel } from '../application/import-legacy-order-conflict';
import { adaptImportLegacyOrderPreview, ImportLegacyOrderPreviewViewModel } from '../application/import-legacy-order-preview';
import { DEFAULT_ORDER_CURRENCY, normalizeOrderCurrency, summarizeOrderSelection } from '../application/order-selection-summary';

type QuickRange = '' | 'today' | 'yesterday' | 'last7' | 'custom';
type PresetQuickRange = Exclude<QuickRange, '' | 'custom'>;
type BulkSelectionMode = 'explicit' | 'filtered';

interface OrdersBulkSelectionSummary {
  legacyOrderId: string;
  customerName: string;
  total: number;
  currencyCode: string;
}

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
              <option value="">Sin filtro de periodo</option>
              <option value="today">Hoy</option>
              <option value="yesterday">Ayer</option>
              <option value="last7">Últimos 7 días</option>
              <option value="custom">Personalizado</option>
            </select>
          </label>

          @if (quickRange() === 'custom') {
            <label>
              <span>Fecha inicial</span>
              <input [ngModel]="fromDate()" (ngModelChange)="setFromDate($event)" name="fromDate" type="date" [max]="today()" />
            </label>

            <label>
              <span>Fecha final</span>
              <input [ngModel]="toDate()" (ngModelChange)="setToDate($event)" name="toDate" type="date" [max]="today()" />
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
              (ngModelChange)="setCustomerQuery($event)"
              name="customerQuery"
              placeholder="Buscar por cliente"
            />
          </label>

          <label>
            <span>Orden</span>
            <input
              [ngModel]="legacyOrderIdFilter()"
              (ngModelChange)="setLegacyOrderIdFilter($event)"
              name="legacyOrderIdFilter"
              placeholder="Buscar por orden"
              inputmode="numeric"
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

        @if (bulkActionError(); as bulkActionError) {
          <p class="error">{{ bulkActionError }}</p>
        }

        @if (loadingOrders()) {
          <p class="helper">Cargando órdenes legadas...</p>
        } @else if (ordersPage()?.items?.length) {
          <section class="bulk-toolbar">
            <div>
              <strong class="selection-summary-line">
                <span>
                  @if (selectedOrdersCount() === 0) {
                    0 órdenes seleccionadas
                  } @else if (selectedOrdersCount() === 1) {
                    1 orden seleccionada
                  } @else if (bulkSelectionMode() === 'filtered') {
                    {{ selectedOrdersCount() }} órdenes seleccionadas según los filtros actuales
                  } @else {
                    {{ selectedOrdersCount() }} órdenes seleccionadas
                  }
                </span>
                <span aria-hidden="true">·</span>
                <span>
                  Total seleccionado:
                  @if (selectedOrdersTotalsReady()) {
                    @for (total of selectedOrdersSelectionSummary().totalsByCurrency; track total.currencyCode) {
                      <span>{{ total.amount | currency: total.currencyCode : 'symbol' : '1.2-2' }} {{ total.currencyCode }}</span>
                      @if (!$last) {
                        <span> · </span>
                      }
                    }
                  } @else {
                    <span>Calculando...</span>
                  }
                </span>
              </strong>
              @if (bulkSelectionMode() === 'explicit' && shouldOfferSelectAllFiltered()) {
                <p class="helper">
                  {{ selectedVisibleOrdersCount() }} órdenes de esta página seleccionadas.
                  <button
                    type="button"
                    class="link-button"
                    (click)="selectAllFilteredOrders()"
                    [disabled]="loadingBulkBilling() || loadingSelectionSummary()">
                    Seleccionar las {{ ordersPage()!.totalCount }} órdenes que coinciden con los filtros actuales
                  </button>
                </p>
              }
            </div>

            @if (selectedOrdersCount() > 0) {
              <div class="actions">
                <button
                  type="button"
                  (click)="openBulkCreateModal()"
                  [disabled]="loadingBulkBilling() || loadingOrders() || selectedOrdersCount() === 0">
                  {{ loadingBulkBilling() ? 'Creando...' : 'Crear documento de facturación' }}
                </button>
                <button
                  type="button"
                  class="secondary"
                  (click)="clearBulkSelection()"
                  [disabled]="loadingBulkBilling()">
                  Limpiar selección
                </button>
              </div>
            }
          </section>

          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th class="selection-col">
                    <input
                      type="checkbox"
                      [checked]="allVisibleSelected()"
                      (change)="toggleVisibleSelection($any($event.target).checked)"
                      [disabled]="loadingBulkBilling() || !eligibleVisibleOrders().length" />
                  </th>
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
                  <tr
                    [class.selected]="selectedLegacyOrderId() === order.legacyOrderId"
                    [class.bulk-selected]="isOrderSelected(order)">
                    <td class="selection-col">
                      @if (getOrderSelectionBlockReason(order); as selectionBlockReason) {
                        <div class="selection-disabled">
                          <input type="checkbox" disabled />
                          <span>{{ selectionBlockReason }}</span>
                        </div>
                      } @else {
                        <input
                          type="checkbox"
                          [checked]="isOrderSelected(order)"
                          (change)="toggleOrderSelection(order, $any($event.target).checked)"
                          [disabled]="loadingBulkBilling() || bulkSelectionMode() === 'filtered'" />
                      }
                    </td>
                    <td>{{ order.legacyOrderId }}</td>
                    <td>{{ order.orderDateUtc | date:'dd/MM/yyyy HH:mm' }}</td>
                    <td>{{ order.customerName }}</td>
                    <td>{{ order.total | currency: (order.currencyCode || 'MXN') : 'symbol' : '1.2-2' }}</td>
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
                          [disabled]="loadingImportOrderId() === order.legacyOrderId || loadingBilling() || loadingBulkBilling()">
                          Continuar
                        </button>
                      } @else {
                        <button
                          type="button"
                          (click)="importOrderFromList(order)"
                          [disabled]="loadingImportOrderId() === order.legacyOrderId || loadingBilling() || loadingBulkBilling()">
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
          <p class="helper">No se encontraron órdenes con los filtros actuales.</p>
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
            <button type="submit" [disabled]="loadingImportOrderId() !== null || loadingBilling() || loadingBulkBilling()">
              {{ loadingImportOrderId() === manualImportKey ? 'Importando...' : 'Importar orden' }}
            </button>
            <button
              type="button"
              class="secondary"
              (click)="createBillingDocument()"
              [disabled]="!importedOrder() || loadingBilling() || loadingBulkBilling() || loadingImportOrderId() !== null">
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
              <div><dt>Revisión actual</dt><dd>{{ conflict.currentRevisionNumber ?? 'N/D' }}</dd></div>
            </dl>

            <div class="actions">
              @if (hasAllowedAction(conflict, 'preview_reimport')) {
                <button
                  type="button"
                  class="secondary"
                  (click)="loadImportPreview(conflict.legacyOrderId)"
                  [disabled]="loadingPreview()">
                  {{ loadingPreview() ? 'Generando preview...' : 'Ver preview de cambios' }}
                </button>
              }

              <button
                type="button"
                class="secondary"
                (click)="loadRevisionHistory(conflict.legacyOrderId)"
                [disabled]="loadingRevisions()">
                {{ loadingRevisions() ? 'Cargando historial...' : 'Ver historial de revisiones' }}
              </button>

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

            <p class="helper">El preview es no destructivo. La reimportación solo se habilita cuando el backend confirma estado seguro.</p>

            @if (previewError(); as previewError) {
              <p class="error">{{ previewError }}</p>
            }

            @if (importPreview(); as preview) {
              <section class="preview-panel">
                <div class="section-header">
                  <div>
                    <p class="eyebrow">Preview de reimportación</p>
                    <h3>Comparación segura de snapshot vs Legacy actual</h3>
                  </div>
                  <app-status-badge
                    [label]="preview.eligibilityStatus"
                    [tone]="preview.eligibilityStatus === 'Allowed' ? 'success' : preview.eligibilityStatus === 'NotNeededNoChanges' ? 'neutral' : 'warning'" />
                </div>

                <p class="helper">Comparando contra la revisión actual {{ preview.currentRevisionNumber }}.</p>

                @if (!preview.hasChanges) {
                  <p class="helper">No se detectaron cambios entre el snapshot existente y el estado actual de Legacy.</p>
                } @else {
                  <dl class="conflict-grid">
                    <div><dt>Líneas agregadas</dt><dd>{{ preview.addedLines }}</dd></div>
                    <div><dt>Líneas eliminadas</dt><dd>{{ preview.removedLines }}</dd></div>
                    <div><dt>Líneas modificadas</dt><dd>{{ preview.modifiedLines }}</dd></div>
                    <div><dt>Líneas sin cambio</dt><dd>{{ preview.unchangedLines }}</dd></div>
                    <div><dt>Subtotal anterior</dt><dd>{{ preview.oldSubtotal | currency:'MXN':'symbol':'1.2-2' }}</dd></div>
                    <div><dt>Subtotal nuevo</dt><dd>{{ preview.newSubtotal | currency:'MXN':'symbol':'1.2-2' }}</dd></div>
                    <div><dt>Total anterior</dt><dd>{{ preview.oldTotal | currency:'MXN':'symbol':'1.2-2' }}</dd></div>
                    <div><dt>Total nuevo</dt><dd>{{ preview.newTotal | currency:'MXN':'symbol':'1.2-2' }}</dd></div>
                  </dl>

                  @if (preview.changedOrderFields.length) {
                    <p class="helper">Campos de cabecera con cambio: {{ preview.changedOrderFields.join(', ') }}</p>
                  }

                  @if (preview.lineChanges.length) {
                    <div class="preview-lines">
                      @for (change of preview.lineChanges; track change.matchKey) {
                        <article class="preview-line-card">
                          <div class="section-header">
                            <div>
                              <p class="eyebrow">{{ change.changeType }}</p>
                              <h3>{{ change.matchKey }}</h3>
                            </div>
                          </div>

                          @if (change.changedFields.length) {
                            <p class="helper">Campos modificados: {{ change.changedFields.join(', ') }}</p>
                          }

                          <div class="conflict-grid">
                            <div>
                              <dt>Anterior</dt>
                              <dd>{{ formatPreviewLine(change.oldLine) }}</dd>
                            </div>
                            <div>
                              <dt>Nuevo</dt>
                              <dd>{{ formatPreviewLine(change.newLine) }}</dd>
                            </div>
                          </div>
                        </article>
                      }
                    </div>
                  }
                }

                <p class="helper">
                  Elegibilidad: <strong>{{ preview.eligibilityStatus }}</strong>.
                  {{ preview.eligibilityReasonMessage }}
                </p>

                @if (preview.eligibilityStatus === 'Allowed') {
                  <div class="actions">
                    <button
                      type="button"
                      (click)="executeReimport(conflict.legacyOrderId, preview)"
                      [disabled]="loadingReimport()">
                      {{ loadingReimport() ? 'Reimportando...' : 'Reimportar' }}
                    </button>
                  </div>
                }
              </section>
            }
          </section>
        } @else {
          <p class="helper">Puedes seguir usando el flujo manual como respaldo, pero la operación principal ahora parte de la lista paginada de órdenes.</p>
        }
      </section>

      @if (selectedLegacyOrderId()) {
        <section class="card">
          <div class="section-header">
            <div>
              <p class="eyebrow">Revisiones</p>
              <h3>Historial de importaciones Legacy</h3>
            </div>
            <div class="actions">
              <span class="helper">Revisión actual: {{ currentRevisionNumber() ?? 'N/D' }}</span>
              <button
                type="button"
                class="secondary"
                (click)="loadRevisionHistory(selectedLegacyOrderId()!)"
                [disabled]="loadingRevisions()">
                {{ loadingRevisions() ? 'Cargando...' : 'Actualizar historial' }}
              </button>
            </div>
          </div>

          @if (revisionsError(); as revisionsError) {
            <p class="error">{{ revisionsError }}</p>
          }

          @if (revisionHistory(); as history) {
            <div class="preview-lines">
              @for (revision of history.revisions; track revision.revisionNumber) {
                <article class="preview-line-card">
                  <div class="section-header">
                    <div>
                      <p class="eyebrow">{{ revision.actionType }}</p>
                      <h3>Revisión {{ revision.revisionNumber }}</h3>
                    </div>
                    <app-status-badge [label]="revision.isCurrent ? 'Actual' : 'Histórica'" [tone]="revision.isCurrent ? 'success' : 'neutral'" />
                  </div>

                  <dl class="conflict-grid">
                    <div><dt>Aplicada</dt><dd>{{ revision.appliedAtUtc | date:'dd/MM/yyyy HH:mm' }}</dd></div>
                    <div><dt>Hash</dt><dd class="mono">{{ revision.sourceHash }}</dd></div>
                    <div><dt>Hash previo</dt><dd class="mono">{{ revision.previousSourceHash ?? 'N/D' }}</dd></div>
                    <div><dt>Sales order</dt><dd>{{ revision.salesOrderId ?? 'N/D' }}</dd></div>
                    <div><dt>Billing document</dt><dd>{{ revision.billingDocumentId ?? 'N/D' }}</dd></div>
                    <div><dt>Fiscal document</dt><dd>{{ revision.fiscalDocumentId ?? 'N/D' }}</dd></div>
                  </dl>

                  <dl class="conflict-grid">
                    <div><dt>Agregadas</dt><dd>{{ revision.changeSummary.addedLines }}</dd></div>
                    <div><dt>Eliminadas</dt><dd>{{ revision.changeSummary.removedLines }}</dd></div>
                    <div><dt>Modificadas</dt><dd>{{ revision.changeSummary.modifiedLines }}</dd></div>
                    <div><dt>Sin cambio</dt><dd>{{ revision.changeSummary.unchangedLines }}</dd></div>
                    <div><dt>Total anterior</dt><dd>{{ revision.changeSummary.oldTotal | currency:'MXN':'symbol':'1.2-2' }}</dd></div>
                    <div><dt>Total nuevo</dt><dd>{{ revision.changeSummary.newTotal | currency:'MXN':'symbol':'1.2-2' }}</dd></div>
                  </dl>

                  <p class="helper">{{ revision.eligibilityStatus }}. {{ revision.eligibilityReasonMessage }}</p>
                </article>
              }
            </div>
          } @else {
            <p class="helper">Carga el historial para ver qué revisión quedó activa y qué cambió en cada reimportación.</p>
          }
        </section>
      }

      @if (showBulkCreateModal()) {
        <section class="modal-backdrop" (click)="closeBulkCreateModal()">
          <section class="modal-card" (click)="$event.stopPropagation()">
            <header class="modal-header">
              <div>
                <p class="eyebrow">Creación masiva</p>
                <h3>Crear documento de facturación</h3>
              </div>
              <button
                type="button"
                class="secondary"
                (click)="closeBulkCreateModal()"
                [disabled]="loadingBulkBilling()">
                Cerrar
              </button>
            </header>

            <p class="helper">
              Se creará un documento de facturación con {{ selectedOrdersCount() }}
              @if (selectedOrdersCount() === 1) {
                orden seleccionada.
              } @else {
                órdenes seleccionadas.
              }
              Después podrás eliminar órdenes individualmente desde el documento.
            </p>

            @if (bulkSelectedCustomerName(); as selectedCustomerName) {
              <p class="helper"><strong>Cliente:</strong> {{ selectedCustomerName }}</p>
            }

            @if (selectedOrdersCount() > 0) {
              <p class="helper">
                <strong>Total estimado:</strong>
                @if (selectedOrdersTotalsReady()) {
                  @for (total of selectedOrdersSelectionSummary().totalsByCurrency; track total.currencyCode) {
                    <span>{{ total.amount | currency: total.currencyCode : 'symbol' : '1.2-2' }} {{ total.currencyCode }}</span>
                    @if (!$last) {
                      <span> · </span>
                    }
                  }
                } @else {
                  <span>Calculando...</span>
                }
              </p>
            }

            @if (bulkSelectedSample().length) {
              <section class="preview-panel">
                <div class="section-header">
                  <div>
                    <p class="eyebrow">Muestra</p>
                    <h3>Órdenes incluidas</h3>
                  </div>
                </div>

                <ul class="selection-sample-list">
                  @for (order of bulkSelectedSample(); track order.legacyOrderId) {
                    <li>
                      <strong>{{ order.legacyOrderId }}</strong>
                      <span>{{ order.customerName }} · {{ order.total | currency: order.currencyCode : 'symbol' : '1.2-2' }} {{ order.currencyCode }}</span>
                    </li>
                  }
                </ul>

                @if (bulkSelectionMode() === 'filtered' && selectedOrdersCount() > bulkSelectedSample().length) {
                  <p class="helper">La muestra corresponde a órdenes visibles con los filtros actuales.</p>
                }
              </section>
            }

            @if (bulkCreateModalError(); as bulkCreateModalError) {
              <p class="error">{{ bulkCreateModalError }}</p>
            }

            @if (bulkCreateOrderErrors().length) {
              <section class="conflict-panel">
                <div class="section-header">
                  <div>
                    <p class="eyebrow">Validación</p>
                    <h3>Órdenes no compatibles</h3>
                  </div>
                </div>

                <ul class="selection-sample-list error-list">
                  @for (orderError of bulkCreateOrderErrors(); track orderError.legacyOrderId + orderError.errorCode) {
                    <li>
                      <strong>{{ orderError.legacyOrderId }}</strong>
                      @if (orderError.customerName) {
                        <span>{{ orderError.customerName }}</span>
                      }
                      <span>{{ orderError.errorMessage }}</span>
                    </li>
                  }
                </ul>
              </section>
            }

            <div class="actions">
              <button
                type="button"
                class="secondary"
                (click)="closeBulkCreateModal()"
                [disabled]="loadingBulkBilling()">
                Cancelar
              </button>
              <button
                type="button"
                (click)="confirmBulkCreateBillingDocument()"
                [disabled]="loadingBulkBilling()">
                {{ loadingBulkBilling() ? 'Creando...' : 'Crear documento' }}
              </button>
            </div>
          </section>
        </section>
      }

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
    .bulk-toolbar { border:1px solid #d8c49b; border-radius:0.9rem; background:#faf6ee; padding:1rem; display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; flex-wrap:wrap; }
    .selection-summary-line { display:flex; gap:0.35rem; flex-wrap:wrap; align-items:center; }
    .align-end { align-items:end; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    button.secondary { background:#d8c49b; color:#182533; }
    .link-button { border:none; background:none; padding:0; color:#8a6a32; text-decoration:underline; cursor:pointer; font:inherit; }
    button:disabled { opacity:0.6; cursor:wait; }
    .error { color:#7a2020; margin:0; }
    .helper { color:#5f6b76; margin:0; }
    .table-wrap { overflow:auto; border:1px solid #eadfcb; border-radius:0.9rem; }
    table { width:100%; border-collapse:collapse; min-width:760px; }
    th, td { padding:0.85rem 0.9rem; border-bottom:1px solid #f1ece1; text-align:left; vertical-align:middle; }
    th { font-size:0.85rem; color:#5f6b76; background:#faf6ee; }
    tr.selected { background:#f7f1e3; }
    tr.bulk-selected { background:#fffaf0; }
    .selection-col { width:84px; }
    .selection-disabled { display:grid; gap:0.25rem; font-size:0.78rem; color:#7a2020; }
    .selection-disabled input { width:auto; margin:0; }
    .pager { display:flex; justify-content:space-between; gap:1rem; align-items:center; flex-wrap:wrap; }
    .conflict-panel { border:1px solid #e6c981; border-radius:0.9rem; background:#fff8ea; padding:1rem; display:grid; gap:1rem; }
    .conflict-grid { margin:0; display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:0.75rem; }
    .conflict-grid dt { font-size:0.82rem; color:#6d6d6d; }
    .conflict-grid dd { margin:0.2rem 0 0; font-weight:600; }
    .mono { font-family:ui-monospace, SFMono-Regular, SFMono-Regular, Menlo, Consolas, monospace; overflow-wrap:anywhere; }
    .preview-panel { border:1px solid #d8d1c2; border-radius:0.9rem; background:#fff; padding:1rem; display:grid; gap:1rem; }
    .preview-lines { display:grid; gap:0.75rem; }
    .preview-line-card { border:1px solid #eadfcb; border-radius:0.8rem; padding:0.9rem; background:#faf7f0; display:grid; gap:0.75rem; }
    .selection-sample-list { margin:0; padding-left:1.2rem; display:grid; gap:0.5rem; }
    .selection-sample-list li { display:grid; gap:0.15rem; }
    .error-list li { color:#7a2020; }
    .modal-backdrop { position:fixed; inset:0; background:rgba(24, 37, 51, 0.52); display:grid; place-items:center; padding:1rem; z-index:60; }
    .modal-card { width:min(720px, 100%); max-height:calc(100vh - 2rem); overflow:auto; border:1px solid #d8d1c2; border-radius:1rem; background:#fff; padding:1rem; display:grid; gap:1rem; box-shadow:0 24px 60px rgba(24, 37, 51, 0.24); }
    .modal-header { display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; }
    @media (max-width: 720px) {
      .bulk-toolbar, .pager, .modal-header { flex-direction:column; align-items:stretch; }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class OrdersOperationsPageComponent implements OnInit {
  protected readonly manualImportKey = '__manual__';

  private readonly ordersApi = inject(OrdersApiService);
  private readonly feedbackService = inject(FeedbackService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private hydratingFilters = false;
  private filteredSelectionSummaryRequestId = 0;

  protected legacyOrderId = '';
  protected documentType = 'I';
  protected readonly today = signal(getTodayDateString());
  protected readonly quickRange = signal<QuickRange>('');
  protected readonly fromDate = signal('');
  protected readonly toDate = signal('');
  protected readonly legacyOrderIdFilter = signal('');
  protected readonly customerQuery = signal('');
  protected readonly loadingOrders = signal(false);
  protected readonly loadingImportOrderId = signal<string | null>(null);
  protected readonly loadingBilling = signal(false);
  protected readonly loadingBulkBilling = signal(false);
  protected readonly loadingSelectionSummary = signal(false);
  protected readonly loadingPreview = signal(false);
  protected readonly loadingReimport = signal(false);
  protected readonly loadingRevisions = signal(false);
  protected readonly ordersError = signal<string | null>(null);
  protected readonly localError = signal<string | null>(null);
  protected readonly bulkActionError = signal<string | null>(null);
  protected readonly bulkCreateModalError = signal<string | null>(null);
  protected readonly previewError = signal<string | null>(null);
  protected readonly revisionsError = signal<string | null>(null);
  protected readonly ordersPage = signal<SearchLegacyOrdersResponse | null>(null);
  protected readonly importedOrder = signal<ImportLegacyOrderResponse | null>(null);
  protected readonly importConflict = signal<ImportLegacyOrderConflictViewModel | null>(null);
  protected readonly importPreview = signal<ImportLegacyOrderPreviewViewModel | null>(null);
  protected readonly revisionHistory = signal<ImportLegacyOrderRevisionHistoryResponse | null>(null);
  protected readonly billingDocument = signal<CreateBillingDocumentResponse | null>(null);
  protected readonly selectedLegacyOrderId = signal<string | null>(null);
  protected readonly selectedLegacyOrderIds = signal<string[]>([]);
  protected readonly selectedOrderSummaries = signal<Record<string, OrdersBulkSelectionSummary>>({});
  protected readonly bulkSelectionMode = signal<BulkSelectionMode>('explicit');
  protected readonly bulkSelectionFilters = signal<CreateBulkBillingDocumentFiltersRequest | null>(null);
  protected readonly showBulkCreateModal = signal(false);
  protected readonly bulkCreateOrderErrors = signal<CreateBulkBillingDocumentOrderError[]>([]);
  protected readonly billingButtonLabel = computed(() =>
    this.billingDocument()?.billingDocumentId ? 'Abrir documento de facturación' : 'Crear documento de facturación');
  protected readonly eligibleVisibleOrders = computed(() =>
    this.ordersPage()?.items.filter((order) => this.getOrderSelectionBlockReason(order) === null) ?? []);
  protected readonly selectedOrdersCount = computed(() =>
    this.bulkSelectionMode() === 'filtered'
      ? this.ordersPage()?.totalCount ?? 0
      : this.selectedLegacyOrderIds().length);
  protected readonly selectedVisibleOrdersCount = computed(() =>
    this.eligibleVisibleOrders().filter((order) => this.isOrderSelected(order)).length);
  protected readonly selectedOrderSummaryItems = computed(() => {
    if (this.bulkSelectionMode() === 'filtered') {
      return Object.values(this.selectedOrderSummaries());
    }

    const summaries = this.selectedOrderSummaries();
    return this.selectedLegacyOrderIds().map((legacyOrderId) =>
      summaries[legacyOrderId] ?? {
        legacyOrderId,
        customerName: '',
        total: 0,
        currencyCode: DEFAULT_ORDER_CURRENCY
      });
  });
  protected readonly selectedOrdersSelectionSummary = computed(() =>
    summarizeOrderSelection(this.selectedOrderSummaryItems()));
  protected readonly selectedOrdersTotalsReady = computed(() =>
    this.selectedOrdersCount() === 0
      || this.bulkSelectionMode() !== 'filtered'
      || this.selectedOrderSummaryItems().length >= this.selectedOrdersCount());
  protected readonly bulkSelectedSample = computed(() => {
    if (this.bulkSelectionMode() === 'filtered') {
      return this.eligibleVisibleOrders()
        .map((order) => ({
          legacyOrderId: order.legacyOrderId,
          customerName: order.customerName,
          total: order.total,
          currencyCode: normalizeOrderCurrency(order.currencyCode)
        }))
        .slice(0, 5);
    }

    const summaries = this.selectedOrderSummaries();
    return this.selectedLegacyOrderIds()
      .map((legacyOrderId) => summaries[legacyOrderId])
      .filter((summary): summary is OrdersBulkSelectionSummary => !!summary)
      .slice(0, 5);
  });
  protected readonly bulkSelectedCustomerName = computed(() => {
    if (this.bulkSelectionMode() !== 'explicit') {
      return null;
    }

    const summaries = this.selectedLegacyOrderIds()
      .map((legacyOrderId) => this.selectedOrderSummaries()[legacyOrderId])
      .filter((summary): summary is OrdersBulkSelectionSummary => !!summary);
    if (summaries.length === 0 || summaries.length !== this.selectedLegacyOrderIds().length) {
      return null;
    }

    const uniqueCustomers = Array.from(new Set(summaries.map((summary) => summary.customerName.trim())));
    return uniqueCustomers.length === 1 ? uniqueCustomers[0] : null;
  });
  protected readonly customRangeError = computed(() => {
    if (this.quickRange() !== 'custom') {
      return null;
    }

    return validateCustomRange(this.fromDate(), this.toDate(), this.today());
  });
  protected readonly currentRevisionNumber = computed(() =>
    this.revisionHistory()?.currentRevisionNumber
      ?? this.importPreview()?.currentRevisionNumber
      ?? this.importedOrder()?.currentRevisionNumber
      ?? this.importConflict()?.currentRevisionNumber
      ?? null);

  ngOnInit(): void {
    this.hydrateFiltersFromQueryParams(this.route.snapshot.queryParamMap);
    void this.searchOrders(1);
  }

  protected shouldOfferSelectAllFiltered(): boolean {
    if (this.bulkSelectionMode() !== 'explicit' || !this.allVisibleSelected()) {
      return false;
    }

    const totalCount = this.ordersPage()?.totalCount ?? 0;
    return totalCount > (this.ordersPage()?.items.length ?? 0);
  }

  protected allVisibleSelected(): boolean {
    const eligibleVisible = this.eligibleVisibleOrders();
    return eligibleVisible.length > 0 && eligibleVisible.every((order) => this.isOrderSelected(order));
  }

  protected isOrderSelected(order: LegacyOrderListItem): boolean {
    if (this.getOrderSelectionBlockReason(order)) {
      return false;
    }

    return this.bulkSelectionMode() === 'filtered'
      || this.selectedLegacyOrderIds().includes(order.legacyOrderId);
  }

  protected toggleOrderSelection(order: LegacyOrderListItem, checked: boolean): void {
    if (this.getOrderSelectionBlockReason(order) || this.bulkSelectionMode() === 'filtered') {
      return;
    }

    this.bulkActionError.set(null);
    this.bulkCreateModalError.set(null);
    this.bulkCreateOrderErrors.set([]);

    if (checked) {
      this.selectedLegacyOrderIds.set([...new Set([...this.selectedLegacyOrderIds(), order.legacyOrderId])]);
      this.rememberSelectedOrderSummaries([order]);
      return;
    }

    this.selectedLegacyOrderIds.set(this.selectedLegacyOrderIds().filter((legacyOrderId) => legacyOrderId !== order.legacyOrderId));

    const nextSummaries = { ...this.selectedOrderSummaries() };
    delete nextSummaries[order.legacyOrderId];
    this.selectedOrderSummaries.set(nextSummaries);
  }

  protected toggleVisibleSelection(checked: boolean): void {
    this.bulkActionError.set(null);
    this.bulkCreateModalError.set(null);
    this.bulkCreateOrderErrors.set([]);

    if (this.bulkSelectionMode() === 'filtered') {
      if (!checked) {
        this.clearBulkSelection();
      }
      return;
    }

    const eligibleVisible = this.eligibleVisibleOrders();
    if (checked) {
      this.selectedLegacyOrderIds.set([
        ...new Set([
          ...this.selectedLegacyOrderIds(),
          ...eligibleVisible.map((order) => order.legacyOrderId)
        ])
      ]);
      this.rememberSelectedOrderSummaries(eligibleVisible);
      return;
    }

    const visibleIds = new Set(eligibleVisible.map((order) => order.legacyOrderId));
    this.selectedLegacyOrderIds.set(this.selectedLegacyOrderIds().filter((legacyOrderId) => !visibleIds.has(legacyOrderId)));

    const nextSummaries = { ...this.selectedOrderSummaries() };
    for (const legacyOrderId of visibleIds) {
      delete nextSummaries[legacyOrderId];
    }
    this.selectedOrderSummaries.set(nextSummaries);
  }

  protected async selectAllFilteredOrders(): Promise<void> {
    if (!this.hasEffectiveCurrentFilters()) {
      this.bulkActionError.set('Aplica al menos un filtro antes de seleccionar todas las órdenes filtradas.');
      return;
    }

    const totalCount = this.ordersPage()?.totalCount ?? 0;
    if (totalCount > MAX_BULK_BILLING_ORDERS) {
      this.bulkActionError.set(`Puedes seleccionar hasta ${MAX_BULK_BILLING_ORDERS} órdenes a la vez. Los filtros actuales coinciden con ${totalCount}.`);
      return;
    }

    const filters = this.buildCurrentBulkFilters();
    if (!filters) {
      this.bulkActionError.set('Los filtros actuales no son válidos para una selección masiva.');
      return;
    }

    this.bulkActionError.set(null);
    this.bulkCreateModalError.set(null);
    this.bulkCreateOrderErrors.set([]);
    this.bulkSelectionMode.set('filtered');
    this.bulkSelectionFilters.set(filters);
    this.selectedLegacyOrderIds.set([]);
    this.selectedOrderSummaries.set({});
    this.rememberSelectedOrderSummaries(this.ordersPage()?.items ?? []);
    await this.loadFilteredSelectionSummaries(filters, totalCount);
  }

  protected clearBulkSelection(): void {
    this.filteredSelectionSummaryRequestId += 1;
    this.selectedLegacyOrderIds.set([]);
    this.selectedOrderSummaries.set({});
    this.bulkSelectionMode.set('explicit');
    this.bulkSelectionFilters.set(null);
    this.loadingSelectionSummary.set(false);
    this.showBulkCreateModal.set(false);
    this.bulkActionError.set(null);
    this.bulkCreateModalError.set(null);
    this.bulkCreateOrderErrors.set([]);
  }

  private async loadFilteredSelectionSummaries(
    filters: CreateBulkBillingDocumentFiltersRequest,
    totalCount: number,
  ): Promise<void> {
    if (totalCount <= Object.keys(this.selectedOrderSummaries()).length) {
      return;
    }

    const requestId = ++this.filteredSelectionSummaryRequestId;
    const totalPages = Math.ceil(totalCount / FILTERED_SELECTION_SUMMARY_PAGE_SIZE);
    const selectedOrders: LegacyOrderListItem[] = [];

    this.loadingSelectionSummary.set(true);

    try {
      for (let page = 1; page <= totalPages; page += 1) {
        const response = await firstValueFrom(this.ordersApi.searchLegacyOrders({
          ...filters,
          page,
          pageSize: FILTERED_SELECTION_SUMMARY_PAGE_SIZE
        }));

        if (this.filteredSelectionSummaryRequestId !== requestId || this.bulkSelectionMode() !== 'filtered') {
          return;
        }

        selectedOrders.push(...response.items);
      }

      this.rememberSelectedOrderSummaries(selectedOrders);
    } catch (error) {
      if (this.filteredSelectionSummaryRequestId === requestId && this.bulkSelectionMode() === 'filtered') {
        this.bulkActionError.set(extractErrorMessage(error));
      }
    } finally {
      if (this.filteredSelectionSummaryRequestId === requestId) {
        this.loadingSelectionSummary.set(false);
      }
    }
  }

  protected openBulkCreateModal(): void {
    if (this.selectedOrdersCount() === 0) {
      return;
    }

    if (this.bulkSelectionMode() === 'explicit' && this.selectedLegacyOrderIds().length > MAX_BULK_BILLING_ORDERS) {
      this.bulkActionError.set(`Puedes seleccionar hasta ${MAX_BULK_BILLING_ORDERS} órdenes a la vez.`);
      return;
    }

    this.bulkActionError.set(null);
    this.bulkCreateModalError.set(null);
    this.bulkCreateOrderErrors.set([]);
    this.showBulkCreateModal.set(true);
  }

  protected closeBulkCreateModal(): void {
    if (this.loadingBulkBilling()) {
      return;
    }

    this.showBulkCreateModal.set(false);
  }

  protected async confirmBulkCreateBillingDocument(): Promise<void> {
    if (!this.showBulkCreateModal()) {
      return;
    }

    const request = this.buildBulkCreateRequest();
    if (!request) {
      this.bulkCreateModalError.set('No hay una selección válida para crear el documento.');
      return;
    }

    this.loadingBulkBilling.set(true);
    this.bulkCreateModalError.set(null);
    this.bulkCreateOrderErrors.set([]);

    try {
      const response = await firstValueFrom(this.ordersApi.createBulkBillingDocument(request));
      this.feedbackService.show(
        'success',
        response.associatedOrderCount === 1
          ? 'Documento de facturación creado con 1 orden.'
          : `Documento de facturación creado con ${response.associatedOrderCount} órdenes.`);
      this.showBulkCreateModal.set(false);
      this.clearBulkSelection();
      await this.openBillingDocument(response.billingDocumentId);
    } catch (error) {
      const bulkResponse = extractBulkBillingDocumentResponse(error);
      if (bulkResponse) {
        this.bulkCreateModalError.set(bulkResponse.errorMessage ?? 'No fue posible crear el documento de facturación con la selección actual.');
        this.bulkCreateOrderErrors.set(bulkResponse.orderErrors ?? []);
      } else {
        this.bulkCreateModalError.set(extractErrorMessage(error));
        this.bulkCreateOrderErrors.set([]);
      }
    } finally {
      this.loadingBulkBilling.set(false);
    }
  }

  protected setQuickRange(range: QuickRange): void {
    this.clearBulkSelection();
    this.quickRange.set(range);
    this.ordersError.set(null);

    if (range === '') {
      this.clearDateRange();
      void this.searchOrders(1);
      return;
    }

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

    this.clearBulkSelection();
    await this.searchOrders(1);
  }

  protected async searchCurrentRange(): Promise<void> {
    this.clearBulkSelection();
    await this.searchOrders(1);
  }

  protected setFromDate(value: string): void {
    if (this.fromDate() === value) {
      return;
    }

    this.clearBulkSelectionForFilterChange();
    this.fromDate.set(value);
  }

  protected setToDate(value: string): void {
    if (this.toDate() === value) {
      return;
    }

    this.clearBulkSelectionForFilterChange();
    this.toDate.set(value);
  }

  protected setCustomerQuery(value: string): void {
    if (this.customerQuery() === value) {
      return;
    }

    this.clearBulkSelectionForFilterChange();
    this.customerQuery.set(value);
  }

  protected setLegacyOrderIdFilter(value: string | number | null): void {
    const normalized = normalizeLegacyOrderIdFilter(value);
    if (this.legacyOrderIdFilter() === normalized) {
      return;
    }

    this.clearBulkSelectionForFilterChange();
    this.legacyOrderIdFilter.set(normalized);
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
    this.importPreview.set(null);
    this.previewError.set(null);
    this.revisionHistory.set(null);
    this.revisionsError.set(null);
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
        ...(range.fromDate ? { fromDate: range.fromDate } : {}),
        ...(range.toDate ? { toDate: range.toDate } : {}),
        legacyOrderId: this.legacyOrderIdFilter(),
        customerQuery: this.customerQuery(),
        page,
        pageSize: 10
      }));
      this.ordersPage.set(response);
      this.rememberSelectedOrderSummaries(response.items);
    } catch (error) {
      this.ordersError.set(extractErrorMessage(error));
    } finally {
      this.loadingOrders.set(false);
    }
  }

  private async importOrderInternal(legacyOrderId: string, loadingKey: string): Promise<void> {
    this.localError.set(null);
    this.importConflict.set(null);
    this.importPreview.set(null);
    this.previewError.set(null);
    this.revisionHistory.set(null);
    this.revisionsError.set(null);
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

  protected async loadImportPreview(legacyOrderId: string): Promise<void> {
    this.previewError.set(null);
    this.loadingPreview.set(true);

    try {
      const response = await firstValueFrom(this.ordersApi.previewLegacyOrderImport(legacyOrderId));
      this.importPreview.set(adaptImportLegacyOrderPreview(response));
    } catch (error) {
      this.previewError.set(extractErrorMessage(error));
    } finally {
      this.loadingPreview.set(false);
    }
  }

  protected async executeReimport(legacyOrderId: string, preview: ImportLegacyOrderPreviewViewModel): Promise<void> {
    if (preview.eligibilityStatus !== 'Allowed') {
      return;
    }

    if (!window.confirm(`¿Reemplazar la importación existente de la orden ${legacyOrderId} con el estado actual de Legacy?`)) {
      return;
    }

    this.previewError.set(null);
    this.loadingReimport.set(true);

    try {
      const response = await firstValueFrom(this.ordersApi.reimportLegacyOrder(legacyOrderId, {
        expectedExistingSourceHash: preview.existingSourceHash,
        expectedCurrentSourceHash: preview.currentSourceHash,
        confirmationMode: 'ReplaceExistingImport'
      }));
      this.applyReimportSuccess(response);
      await this.loadRevisionHistory(legacyOrderId);
    } catch (error) {
      this.previewError.set(extractErrorMessage(error));
    } finally {
      this.loadingReimport.set(false);
    }
  }

  private applyReimportSuccess(response: ReimportLegacyOrderResponse): void {
    this.legacyOrderId = response.legacyOrderId;
    this.selectedLegacyOrderId.set(response.legacyOrderId);
    this.importConflict.set(null);
    this.importPreview.set(null);
    this.previewError.set(null);
    this.localError.set(null);
    this.importedOrder.set(toReimportedOrder(response));

    if (response.billingDocumentId) {
      this.billingDocument.set({
        outcome: 'Conflict',
        isSuccess: false,
        errorMessage: null,
        salesOrderId: response.salesOrderId ?? 0,
        billingDocumentId: response.billingDocumentId,
        billingDocumentStatus: response.billingDocumentStatus ?? null
      });
    } else {
      this.billingDocument.set(null);
    }

    this.updateOrderInPage(response.legacyOrderId, (order) => ({
      ...order,
      isImported: true,
      salesOrderId: response.salesOrderId ?? order.salesOrderId,
      billingDocumentId: response.billingDocumentId ?? order.billingDocumentId,
      billingDocumentStatus: response.billingDocumentStatus ?? order.billingDocumentStatus,
      fiscalDocumentId: response.fiscalDocumentId ?? order.fiscalDocumentId,
      fiscalDocumentStatus: response.fiscalDocumentStatus ?? order.fiscalDocumentStatus,
      importStatus: 'Imported'
    }));

    this.feedbackService.show('success', 'La importación existente fue reemplazada con el estado actual de Legacy.');
  }

  protected async loadRevisionHistory(legacyOrderId: string): Promise<void> {
    this.revisionsError.set(null);
    this.loadingRevisions.set(true);

    try {
      const response = await firstValueFrom(this.ordersApi.listLegacyOrderImportRevisions(legacyOrderId));
      this.revisionHistory.set(response);
    } catch (error) {
      this.revisionsError.set(extractErrorMessage(error));
    } finally {
      this.loadingRevisions.set(false);
    }
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
      currentRevisionNumber: conflict.currentRevisionNumber ?? undefined,
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

  protected formatPreviewLine(line?: ImportLegacyOrderPreviewResponse['lineChanges'][number]['oldLine'] | null): string {
    if (!line) {
      return 'N/D';
    }

    return `#${line.lineNumber} · ${line.sku || line.legacyArticleId} · ${line.description} · Cant ${line.quantity} · PU ${line.unitPrice.toFixed(2)} · Total ${line.lineTotal.toFixed(2)}`;
  }

  private updateOrderInPage(legacyOrderId: string, updater: (order: LegacyOrderListItem) => LegacyOrderListItem): void {
    const currentPage = this.ordersPage();
    if (!currentPage) {
      return;
    }

    let updatedSummary: OrdersBulkSelectionSummary | null = null;
    this.ordersPage.set({
      ...currentPage,
      items: currentPage.items.map((order) => {
        if (order.legacyOrderId !== legacyOrderId) {
          return order;
        }

        const updated = updater(order);
        updatedSummary = {
          legacyOrderId: updated.legacyOrderId,
          customerName: updated.customerName,
          total: updated.total,
          currencyCode: normalizeOrderCurrency(updated.currencyCode)
        };
        return updated;
      })
    });

    if (!updatedSummary) {
      return;
    }

    if (this.bulkSelectionMode() === 'filtered' || this.selectedLegacyOrderIds().includes(legacyOrderId)) {
      this.selectedOrderSummaries.set({
        ...this.selectedOrderSummaries(),
        [legacyOrderId]: updatedSummary
      });
    }
  }

  private hydrateFiltersFromQueryParams(params: ParamMap): void {
    this.hydratingFilters = true;
    const quickRange = normalizeQuickRangeParam(params.get('quickRange'));
    const fromDate = normalizeDateInput(params.get('fromDate'));
    const toDate = normalizeDateInput(params.get('toDate'));

    try {
      this.setLegacyOrderIdFilter(params.get('legacyOrderId'));
      this.setCustomerQuery(params.get('customerQuery')?.trim() ?? '');

      if (quickRange === 'custom' || (!quickRange && (fromDate || toDate))) {
        this.quickRange.set('custom');
        this.fromDate.set(fromDate);
        this.toDate.set(toDate);
        return;
      }

      if (quickRange) {
        this.quickRange.set(quickRange);
        this.applyQuickRange(quickRange);
        return;
      }

      this.quickRange.set('');
      this.clearDateRange();
    } finally {
      this.hydratingFilters = false;
    }
  }

  protected getOrderSelectionBlockReason(order: LegacyOrderListItem): string | null {
    if (order.billingDocumentId) {
      return `Ya asociada al documento #${order.billingDocumentId}.`;
    }

    return null;
  }

  private hasEffectiveCurrentFilters(): boolean {
    const filters = this.buildCurrentBulkFilters();
    return !!filters && (
      !!filters.fromDate
      || !!filters.toDate
      || !!filters.legacyOrderId
      || !!filters.customerQuery
    );
  }

  private buildCurrentBulkFilters(): CreateBulkBillingDocumentFiltersRequest | null {
    const range = resolveRange(this.quickRange(), this.fromDate(), this.toDate(), this.today());
    if (!range) {
      return null;
    }

    return {
      ...(range.fromDate ? { fromDate: range.fromDate } : {}),
      ...(range.toDate ? { toDate: range.toDate } : {}),
      ...(this.legacyOrderIdFilter() ? { legacyOrderId: this.legacyOrderIdFilter() } : {}),
      ...(this.customerQuery().trim() ? { customerQuery: this.customerQuery().trim() } : {})
    };
  }

  private buildBulkCreateRequest(): {
    documentType: string;
    selectionMode: 'Explicit' | 'Filtered';
    legacyOrderIds?: string[];
    filters?: CreateBulkBillingDocumentFiltersRequest;
  } | null {
    if (this.bulkSelectionMode() === 'filtered') {
      const filters = this.bulkSelectionFilters() ?? this.buildCurrentBulkFilters();
      if (!filters) {
        return null;
      }

      return {
        documentType: this.documentType,
        selectionMode: 'Filtered',
        filters
      };
    }

    const legacyOrderIds = this.selectedLegacyOrderIds();
    if (legacyOrderIds.length === 0) {
      return null;
    }

    if (legacyOrderIds.length > MAX_BULK_BILLING_ORDERS) {
      this.bulkCreateModalError.set(`Puedes seleccionar hasta ${MAX_BULK_BILLING_ORDERS} órdenes a la vez.`);
      return null;
    }

    return {
      documentType: this.documentType,
      selectionMode: 'Explicit',
      legacyOrderIds
    };
  }

  private rememberSelectedOrderSummaries(orders: LegacyOrderListItem[]): void {
    if (this.bulkSelectionMode() !== 'filtered' && this.selectedLegacyOrderIds().length === 0) {
      return;
    }

    const nextSummaries = { ...this.selectedOrderSummaries() };
    for (const order of orders) {
      if (this.bulkSelectionMode() === 'filtered' || this.selectedLegacyOrderIds().includes(order.legacyOrderId)) {
        nextSummaries[order.legacyOrderId] = {
          legacyOrderId: order.legacyOrderId,
          customerName: order.customerName,
          total: Number.isFinite(order.total) ? order.total : 0,
          currencyCode: normalizeOrderCurrency(order.currencyCode)
        };
      }
    }

    this.selectedOrderSummaries.set(nextSummaries);
  }

  private clearBulkSelectionForFilterChange(): void {
    if (this.hydratingFilters) {
      return;
    }

    this.clearBulkSelection();
  }

  private applyQuickRange(range: PresetQuickRange): void {
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

  private clearDateRange(): void {
    this.fromDate.set('');
    this.toDate.set('');
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

function extractBulkBillingDocumentResponse(error: unknown): CreateBulkBillingDocumentResponse | null {
  if (!(error instanceof HttpErrorResponse) || typeof error.error !== 'object' || !error.error) {
    return null;
  }

  const payload = error.error as Partial<CreateBulkBillingDocumentResponse>;
  return typeof payload.outcome === 'string'
    ? {
        outcome: payload.outcome,
        isSuccess: !!payload.isSuccess,
        errorCode: payload.errorCode ?? null,
        errorMessage: payload.errorMessage ?? null,
        billingDocumentId: payload.billingDocumentId ?? null,
        billingDocumentStatus: payload.billingDocumentStatus ?? null,
        selectedOrderCount: payload.selectedOrderCount ?? 0,
        importedOrderCount: payload.importedOrderCount ?? 0,
        associatedOrderCount: payload.associatedOrderCount ?? 0,
        legacyOrderIds: payload.legacyOrderIds ?? [],
        orderErrors: payload.orderErrors ?? []
      }
    : null;
}

function toReimportedOrder(response: ReimportLegacyOrderResponse): ImportLegacyOrderResponse {
  return {
    outcome: response.outcome,
    isSuccess: response.isSuccess,
    isIdempotent: false,
    errorMessage: response.errorMessage ?? null,
    errorCode: response.errorCode ?? null,
    sourceSystem: 'legacy',
    sourceTable: 'pedidos',
    legacyOrderId: response.legacyOrderId,
    sourceHash: response.newSourceHash,
    legacyImportRecordId: response.legacyImportRecordId ?? null,
    salesOrderId: response.salesOrderId ?? null,
    importStatus: 'Imported',
    currentRevisionNumber: response.currentRevisionNumber,
    existingSalesOrderId: response.salesOrderId ?? null,
    existingSalesOrderStatus: response.salesOrderStatus ?? null,
    existingBillingDocumentId: response.billingDocumentId ?? null,
    existingBillingDocumentStatus: response.billingDocumentStatus ?? null,
    existingFiscalDocumentId: response.fiscalDocumentId ?? null,
    existingFiscalDocumentStatus: response.fiscalDocumentStatus ?? null,
    fiscalUuid: response.fiscalUuid ?? null,
    existingSourceHash: response.previousSourceHash,
    currentSourceHash: response.newSourceHash,
    allowedActions: response.allowedActions
  };
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

function resolveRange(
  range: QuickRange,
  fromDate: string,
  toDate: string,
  today: string
): { fromDate?: string; toDate?: string } | null {
  if (range === '') {
    return {};
  }

  if (range === 'custom') {
    return validateCustomRange(fromDate, toDate, today) ? null : { fromDate, toDate };
  }

  return { fromDate, toDate };
}

function normalizeLegacyOrderIdFilter(value: string | number | null | undefined): string {
  return String(value ?? '').replace(/\D+/g, '');
}

const MAX_BULK_BILLING_ORDERS = 50;
const FILTERED_SELECTION_SUMMARY_PAGE_SIZE = 10;

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
    currentRevisionNumber: undefined,
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

function normalizeQuickRangeParam(value: string | null): QuickRange | null {
  switch (value) {
    case '':
    case 'today':
    case 'yesterday':
    case 'last7':
    case 'custom':
      return value;
    default:
      return null;
  }
}

function normalizeDateInput(value: string | null): string {
  const normalized = value?.trim() ?? '';
  return /^\d{4}-\d{2}-\d{2}$/.test(normalized) ? normalized : '';
}
