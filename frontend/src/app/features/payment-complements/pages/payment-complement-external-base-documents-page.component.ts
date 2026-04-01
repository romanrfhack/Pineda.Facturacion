import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { getDisplayLabel } from '../../../shared/ui/display-labels';
import { ExternalRepBaseDocumentImportCardComponent } from '../components/external-rep-base-document-import-card.component';
import { PaymentComplementsApiService } from '../infrastructure/payment-complements-api.service';
import {
  RepBaseDocumentBulkRefreshResponse,
  ExternalRepBaseDocumentDetailResponse,
  ExternalRepBaseDocumentItemResponse,
  ExternalRepBaseDocumentPaymentComplementResponse,
  RepOperationalAlertResponse,
  RepOperationalSummaryCountsResponse
} from '../models/payment-complements.models';

@Component({
  selector: 'app-payment-complement-external-base-documents-page',
  imports: [FormsModule, DecimalPipe, ExternalRepBaseDocumentImportCardComponent],
  template: `
    <section class="page">
      <app-external-rep-base-document-import-card />

      <section class="card">
        <header>
          <p class="eyebrow">Externos</p>
          <h3>Bandeja de CFDI externos importados</h3>
          <p class="helper">En Fase 4 los CFDI externos aceptados ya pueden registrar pago, preparar REP y timbrarlo desde el mismo documento base.</p>
        </header>

        <form class="filters" (ngSubmit)="applyFilters()">
          <label><span>Desde</span><input [(ngModel)]="fromDate" name="fromDate" type="date" /></label>
          <label><span>Hasta</span><input [(ngModel)]="toDate" name="toDate" type="date" /></label>
          <label><span>RFC receptor</span><input [(ngModel)]="receiverRfc" name="receiverRfc" /></label>
          <label class="wide"><span>Búsqueda general</span><input [(ngModel)]="query" name="query" placeholder="UUID, RFC, nombre, serie o folio" /></label>
          <label>
            <span>Validación</span>
            <select [(ngModel)]="validationStatus" name="validationStatus">
              <option value="">Todos</option>
              <option value="Accepted">Accepted</option>
              <option value="Blocked">Blocked</option>
            </select>
          </label>
          <label>
            <span>Elegible</span>
            <select [(ngModel)]="eligibleFilter" name="eligibleFilter">
              <option value="">Todos</option>
              <option value="true">Sí</option>
              <option value="false">No</option>
            </select>
          </label>
          <label>
            <span>Bloqueado</span>
            <select [(ngModel)]="blockedFilter" name="blockedFilter">
              <option value="">Todos</option>
              <option value="true">Sí</option>
              <option value="false">No</option>
            </select>
          </label>
          <label>
            <span>Alerta</span>
            <select [(ngModel)]="alertCodeFilter" name="alertCodeFilter">
              <option value="">Todas</option>
              @for (option of alertOptions; track option) {
                <option [value]="option">{{ getDisplayLabel(option) }}</option>
              }
            </select>
          </label>
          <label>
            <span>Severidad</span>
            <select [(ngModel)]="severityFilter" name="severityFilter">
              <option value="">Todas</option>
              @for (option of severityOptions; track option) {
                <option [value]="option">{{ getDisplayLabel(option) }}</option>
              }
            </select>
          </label>
          <label>
            <span>Acción recomendada</span>
            <select [(ngModel)]="nextRecommendedActionFilter" name="nextRecommendedActionFilter">
              <option value="">Todas</option>
              @for (option of recommendedActionOptions; track option) {
                <option [value]="option">{{ getDisplayLabel(option) }}</option>
              }
            </select>
          </label>

          <div class="actions wide">
            <button type="submit" [disabled]="loading()">{{ loading() ? 'Buscando...' : 'Buscar' }}</button>
            <button type="button" class="secondary" (click)="clearFilters()" [disabled]="loading()">Limpiar filtros</button>
          </div>
        </form>

        <div class="quick-filters">
          <button type="button" class="secondary small quick-chip" [class.quick-chip-active]="!quickViewFilter" (click)="applyQuickView('')" [disabled]="loading()">
            Todos
          </button>
          @for (quickView of quickViewOptions; track quickView) {
            <button type="button" class="secondary small quick-chip" [class.quick-chip-active]="quickViewFilter === quickView" (click)="applyQuickView(quickView)" [disabled]="loading()">
              {{ getDisplayLabel(quickView) }} ({{ countForQuickView(quickView) }})
            </button>
          }
          @if (quickViewFilter) {
            <button type="button" class="secondary small quick-chip" (click)="applyQuickView('')" [disabled]="loading()">
              Volver a Todos
            </button>
          }
          @if (hasOperationalFilters()) {
            <button type="button" class="secondary small quick-chip" (click)="clearOperationalFilters()" [disabled]="loading()">
              Limpiar operativos
            </button>
          }
        </div>
      </section>

      <section class="card">
        @if (loading()) {
          <p class="helper">Cargando CFDI externos importados...</p>
        } @else if (errorMessage()) {
          <p class="error">{{ errorMessage() }}</p>
        } @else if (!items().length) {
          <p class="helper">No se encontraron CFDI externos importados con los filtros actuales.</p>
        } @else {
          <p class="helper">Mostrando {{ items().length }} de {{ totalCount() }} CFDI externos.</p>

          <div class="bulk-toolbar">
            <label class="selection-toggle">
              <input type="checkbox" [checked]="allVisibleSelected()" (change)="toggleSelectAll($any($event.target).checked)" [disabled]="bulkRefreshing()" />
              <span>Seleccionar visibles</span>
            </label>
            <span class="helper">{{ selectedCount() }} seleccionados</span>
            <button type="button" [disabled]="bulkRefreshing() || !selectedCount()" (click)="refreshSelectedDocuments()">
              {{ bulkRefreshing() ? 'Refrescando...' : 'Refrescar seleccionados' }}
            </button>
            <button type="button" class="secondary" [disabled]="bulkRefreshing() || !items().length" (click)="refreshFilteredDocuments()">
              {{ bulkRefreshing() ? 'Refrescando...' : 'Refrescar filtrados' }}
            </button>
            @if (bulkRefreshResult()) {
              <button type="button" class="secondary small" [disabled]="bulkRefreshing()" (click)="clearBulkRefreshResult()">
                Limpiar resultado
              </button>
            }
          </div>

          @if (bulkRefreshError()) {
            <p class="error">{{ bulkRefreshError() }}</p>
          }

          @if (bulkRefreshResult(); as result) {
            <section class="card inset-card">
              <h4>Resultado del refresh masivo</h4>
              <p class="helper">
                Modo {{ getDisplayLabel(result.mode) }} · solicitados {{ result.totalRequested }} · procesados {{ result.totalAttempted }} · actualizados {{ result.refreshedCount }} · sin cambios {{ result.noChangesCount }} · bloqueados {{ result.blockedCount }} · fallidos {{ result.failedCount }}
              </p>
              @if (result.items.length) {
                <div class="table-wrap">
                  <table>
                    <thead>
                      <tr>
                        <th>Documento</th>
                        <th>Resultado</th>
                        <th>REP</th>
                        <th>Estado externo</th>
                        <th>Mensaje</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (item of result.items; track item.sourceId) {
                        <tr>
                          <td>#{{ item.sourceId }}</td>
                          <td>{{ getDisplayLabel(item.outcome) }}</td>
                          <td>{{ item.paymentComplementDocumentId ? ('#' + item.paymentComplementDocumentId) : '—' }}{{ item.paymentComplementStatus ? (' · ' + getDisplayLabel(item.paymentComplementStatus)) : '' }}</td>
                          <td>{{ item.lastKnownExternalStatus || '—' }}</td>
                          <td>{{ item.message }}</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              }
            </section>
          }

          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>
                    <input type="checkbox" [checked]="allVisibleSelected()" (change)="toggleSelectAll($any($event.target).checked)" [disabled]="bulkRefreshing()" />
                  </th>
                  <th>Emisión</th>
                  <th>Serie/Folio</th>
                  <th>UUID</th>
                  <th>Emisor</th>
                  <th>Receptor</th>
                  <th>Total</th>
                  <th>Pagado</th>
                  <th>Saldo</th>
                  <th>REP emitidos</th>
                  <th>Operativo</th>
                  <th>Acción</th>
                </tr>
              </thead>
              <tbody>
                @for (item of items(); track item.externalRepBaseDocumentId) {
                  <tr>
                    <td>
                      <input type="checkbox" [checked]="isSelected(item.externalRepBaseDocumentId)" (change)="toggleSelection(item.externalRepBaseDocumentId, $any($event.target).checked)" [disabled]="bulkRefreshing()" />
                    </td>
                    <td>{{ formatUtc(item.issuedAtUtc) }}</td>
                    <td>{{ buildSeriesFolio(item.series, item.folio) }}</td>
                    <td>{{ item.uuid }}</td>
                    <td>{{ item.issuerRfc }}</td>
                    <td>{{ item.receiverRfc }}</td>
                    <td>{{ item.total | number:'1.2-2' }}</td>
                    <td>{{ item.paidTotal | number:'1.2-2' }}</td>
                    <td>{{ item.outstandingBalance | number:'1.2-2' }}</td>
                    <td>{{ item.stampedPaymentComplementCount }}</td>
                    <td>
                      <span class="status-pill" [class.status-eligible]="item.isEligible" [class.status-blocked]="item.isBlocked" [class.status-neutral]="!item.isEligible && !item.isBlocked">
                        {{ getDisplayLabel(item.operationalStatus) }}
                      </span>
                      @if (getPrimarySeverity(item); as severity) {
                        <span class="severity-pill" [class.severity-warning]="severity === 'warning'" [class.severity-error]="severity === 'error'" [class.severity-critical]="severity === 'critical'" [class.severity-info]="severity === 'info'">
                          {{ getDisplayLabel(severity) }}
                        </span>
                      }
                      <small class="row-reason">{{ item.primaryReasonMessage }}</small>
                      <small class="row-reason">Siguiente: {{ getRecommendedActionLabel(item.nextRecommendedAction) }}</small>
                      @if (getAlerts(item).length) {
                        <div class="alert-chip-list">
                          @for (alert of visibleAlerts(getAlerts(item)); track alert.code + '-' + alert.message) {
                            <span class="alert-chip" [class.alert-critical]="alert.severity === 'critical'" [class.alert-error]="alert.severity === 'error'" [class.alert-warning]="alert.severity === 'warning'" [class.alert-alert-info]="alert.severity === 'info'">
                              {{ getDisplayLabel(alert.code) }}
                            </span>
                          }
                        </div>
                      }
                    </td>
                    <td>
                      <button type="button" class="secondary small" (click)="openDetail(item.externalRepBaseDocumentId)">
                        Ver detalle
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </section>

      @if (showDetailModal()) {
        <section class="modal-backdrop" (click)="closeDetail()">
          <section class="modal-card detail-modal" (click)="$event.stopPropagation()">
            <div class="modal-header">
              <div>
                <p class="eyebrow">Externo</p>
                <h3>Detalle operativo del CFDI importado</h3>
              </div>
              <div class="modal-actions">
                @if (selectedDetail(); as detail) {
                  @if (canRegisterPayment(detail)) {
                    <button type="button" (click)="openRegisterPaymentForm()">Registrar pago</button>
                  }
                  @if (canPrepareRep(detail)) {
                    <button type="button" [disabled]="preparingRep()" (click)="prepareRep()">
                      {{ preparingRep() ? 'Preparando...' : 'Preparar REP' }}
                    </button>
                  }
                  @if (canStampRep(detail)) {
                    <button type="button" [disabled]="stampingRep()" (click)="stampRep()">
                      {{ stampingRep() ? 'Timbrando...' : 'Timbrar REP' }}
                    </button>
                  }
                }
                <button type="button" class="secondary" (click)="closeDetail()">Cerrar</button>
              </div>
            </div>

            @if (loadingDetail()) {
              <p class="helper">Cargando detalle...</p>
            } @else if (detailError()) {
              <p class="error">{{ detailError() }}</p>
            } @else if (selectedDetail(); as detail) {
              @if (operationMessage()) {
                <p class="success">{{ operationMessage() }}</p>
              }
              @if (operationError()) {
                <p class="error">{{ operationError() }}</p>
              }

              <section class="summary-grid">
                <article class="summary-card">
                  <h4>Resumen fiscal</h4>
                  <dl>
                    <div><dt>ExternalRepBaseDocumentId</dt><dd>{{ detail.summary.externalRepBaseDocumentId }}</dd></div>
                    <div><dt>AR Invoice</dt><dd>{{ detail.summary.accountsReceivableInvoiceId ?? '—' }}</dd></div>
                    <div><dt>UUID</dt><dd>{{ detail.summary.uuid }}</dd></div>
                    <div><dt>Serie/Folio</dt><dd>{{ buildSeriesFolio(detail.summary.series, detail.summary.folio) }}</dd></div>
                    <div><dt>Emisión</dt><dd>{{ formatUtc(detail.summary.issuedAtUtc) }}</dd></div>
                    <div><dt>Emisor</dt><dd>{{ detail.summary.issuerRfc }} · {{ detail.summary.issuerLegalName || '—' }}</dd></div>
                    <div><dt>Receptor</dt><dd>{{ detail.summary.receiverRfc }} · {{ detail.summary.receiverLegalName || '—' }}</dd></div>
                    <div><dt>Método/Forma</dt><dd>{{ detail.summary.paymentMethodSat }} / {{ detail.summary.paymentFormSat }}</dd></div>
                    <div><dt>Moneda</dt><dd>{{ detail.summary.currencyCode }}</dd></div>
                    <div><dt>Total</dt><dd>{{ detail.summary.total | number:'1.2-2' }}</dd></div>
                  </dl>
                </article>

                <article class="summary-card">
                  <h4>Resumen operativo</h4>
                  <dl>
                    <div><dt>Validación</dt><dd>{{ getDisplayLabel(detail.summary.validationStatus) }}</dd></div>
                    <div><dt>SAT</dt><dd>{{ getDisplayLabel(detail.summary.satStatus) }}</dd></div>
                    <div><dt>Estado operativo</dt><dd>{{ getDisplayLabel(detail.summary.operationalStatus) }}</dd></div>
                    <div><dt>Motivo</dt><dd>{{ detail.summary.primaryReasonMessage }}</dd></div>
                    <div><dt>Pagado</dt><dd>{{ detail.summary.paidTotal | number:'1.2-2' }}</dd></div>
                    <div><dt>Saldo</dt><dd>{{ detail.summary.outstandingBalance | number:'1.2-2' }}</dd></div>
                    <div><dt>Pagos registrados</dt><dd>{{ detail.summary.registeredPaymentCount }}</dd></div>
                    <div><dt>REP emitidos</dt><dd>{{ detail.summary.stampedPaymentComplementCount }}</dd></div>
                    <div><dt>Último REP</dt><dd>{{ formatOptionalUtc(detail.summary.lastRepIssuedAtUtc) }}</dd></div>
                  </dl>
                </article>

                <article class="summary-card">
                  <h4>Seguimiento operativo</h4>
                  <dl>
                    <div><dt>Acción recomendada</dt><dd>{{ getRecommendedActionLabel(detail.summary.nextRecommendedAction) }}</dd></div>
                    <div><dt>Operación bloqueada</dt><dd>{{ detail.summary.hasBlockedOperation ? 'Sí' : 'No' }}</dd></div>
                    <div><dt>Pago sin REP timbrado</dt><dd>{{ detail.summary.hasAppliedPaymentsWithoutStampedRep ? 'Sí' : 'No' }}</dd></div>
                    <div><dt>REP pendiente de timbrar</dt><dd>{{ detail.summary.hasPreparedRepPendingStamp ? 'Sí' : 'No' }}</dd></div>
                    <div><dt>REP con error</dt><dd>{{ detail.summary.hasRepWithError ? 'Sí' : 'No' }}</dd></div>
                  </dl>
                  @if (getAlerts(detail.summary).length) {
                    <ul class="alert-list">
                      @for (alert of getAlerts(detail.summary); track alert.code + '-' + alert.message) {
                        <li class="alert-item" [class.alert-critical]="alert.severity === 'critical'" [class.alert-error]="alert.severity === 'error'" [class.alert-warning]="alert.severity === 'warning'" [class.alert-alert-info]="alert.severity === 'info'">
                          <strong>{{ getDisplayLabel(alert.code) }}</strong>
                          <p>{{ alert.message }}</p>
                        </li>
                      }
                    </ul>
                  } @else {
                    <p class="helper">No hay alertas operativas activas para este CFDI externo.</p>
                  }
                </article>
              </section>

              @if (showRegisterPaymentForm()) {
                <section class="card inset-card">
                  <h4>Registrar pago</h4>
                  <form class="payment-form" (ngSubmit)="submitRegisterPayment()">
                    <label><span>Fecha de pago</span><input [(ngModel)]="paymentDate" name="paymentDate" type="date" required /></label>
                    <label><span>Forma SAT</span><input [(ngModel)]="paymentFormSat" name="paymentFormSat" maxlength="10" required /></label>
                    <label><span>Monto</span><input [(ngModel)]="paymentAmount" name="paymentAmount" type="number" min="0.01" step="0.01" required /></label>
                    <label><span>Referencia</span><input [(ngModel)]="paymentReference" name="paymentReference" /></label>
                    <label class="wide"><span>Notas</span><textarea [(ngModel)]="paymentNotes" name="paymentNotes" rows="3"></textarea></label>

                    <div class="actions wide">
                      <button type="submit" [disabled]="submittingPayment()">{{ submittingPayment() ? 'Registrando...' : 'Confirmar pago' }}</button>
                      <button type="button" class="secondary" (click)="cancelRegisterPaymentForm()" [disabled]="submittingPayment()">Cancelar</button>
                    </div>
                  </form>
                </section>
              }

              <section class="history-grid">
                <article class="summary-card">
                  <h4>Pagos registrados</h4>
                  @if (!detail.paymentHistory.length) {
                    <p class="helper">Todavía no hay pagos registrados para este CFDI externo.</p>
                  } @else {
                    <div class="history-list">
                      @for (item of detail.paymentHistory; track item.accountsReceivablePaymentId) {
                        <article class="history-item">
                          <header>
                            <strong>Pago #{{ item.accountsReceivablePaymentId }}</strong>
                            <span>{{ formatUtc(item.paymentDateUtc) }}</span>
                          </header>
                          <p>{{ item.paymentFormSat }} · {{ item.paymentAmount | number:'1.2-2' }} · aplicado {{ item.amountAppliedToDocument | number:'1.2-2' }}</p>
                          <small>{{ item.reference || 'Sin referencia' }} · REP {{ item.paymentComplementStatus || 'pendiente' }}</small>
                        </article>
                      }
                    </div>
                  }
                </article>

                <article class="summary-card">
                  <h4>Aplicaciones</h4>
                  @if (!detail.paymentApplications.length) {
                    <p class="helper">No hay aplicaciones de pago registradas todavía.</p>
                  } @else {
                    <div class="history-list">
                      @for (item of detail.paymentApplications; track item.accountsReceivablePaymentId + '-' + item.applicationSequence) {
                        <article class="history-item">
                          <header>
                            <strong>Aplicación {{ item.applicationSequence }}</strong>
                            <span>{{ formatUtc(item.paymentDateUtc) }}</span>
                          </header>
                          <p>Saldo anterior {{ item.previousBalance | number:'1.2-2' }} · aplicado {{ item.appliedAmount | number:'1.2-2' }} · saldo nuevo {{ item.newBalance | number:'1.2-2' }}</p>
                        </article>
                      }
                    </div>
                  }
                </article>

                <article class="summary-card">
                  <h4>REP emitidos</h4>
                  @if (!detail.issuedReps.length) {
                    <p class="helper">Todavía no hay REP preparados o emitidos para este CFDI externo.</p>
                  } @else {
                    <div class="history-list">
                      @for (item of detail.issuedReps; track item.paymentComplementId) {
                        <article class="history-item">
                          <header>
                            <strong>REP #{{ item.paymentComplementId }}</strong>
                            <span>{{ getDisplayLabel(item.status) }}</span>
                          </header>
                          <p>UUID {{ item.uuid || '—' }} · pago {{ item.accountsReceivablePaymentId }} · parcialidad {{ item.installmentNumber }}</p>
                          <small>{{ formatOptionalUtc(item.stampedAtUtc || item.issuedAtUtc) }} · saldo remanente {{ item.remainingBalance | number:'1.2-2' }}</small>
                          <div class="actions">
                            @if (canRefreshRepItem(item)) {
                              <button type="button" class="secondary small" [disabled]="refreshingRep() || cancellingRep() || preparingRep() || stampingRep()" (click)="refreshRep(item)">
                                {{ refreshingRep() === item.paymentComplementId ? 'Refrescando...' : 'Refrescar' }}
                              </button>
                            }
                            @if (canCancelRepItem(item)) {
                              <button type="button" class="secondary small" [disabled]="refreshingRep() || cancellingRep() || preparingRep() || stampingRep()" (click)="cancelRep(item)">
                                {{ cancellingRep() === item.paymentComplementId ? 'Cancelando...' : 'Cancelar' }}
                              </button>
                            }
                          </div>
                        </article>
                      }
                    </div>
                  }
                </article>
              </section>
            }
          </section>
        </section>
      }
    </section>
  `,
  styles: [`
    .page { display:grid; gap:1rem; }
    .card { border:1px solid #d8d1c2; border-radius:1rem; background:#fff; padding:1rem; display:grid; gap:1rem; }
    .inset-card { border-style:dashed; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    .helper { margin:0; color:#5f6b76; }
    .filters, .payment-form { display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:0.85rem; }
    .filters label, .payment-form label { display:grid; gap:0.35rem; }
    .filters .wide, .payment-form .wide { grid-column:1 / -1; }
    input, select, textarea { font:inherit; padding:0.65rem 0.75rem; border:1px solid #d8d1c2; border-radius:0.7rem; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    button.secondary { background:#d8c49b; color:#182533; }
    button.small { padding:0.5rem 0.75rem; font-size:0.9rem; }
    .actions, .modal-actions, .quick-filters, .bulk-toolbar { display:flex; gap:0.75rem; flex-wrap:wrap; align-items:center; }
    .selection-toggle { display:inline-flex; align-items:center; gap:0.5rem; }
    .table-wrap { overflow:auto; }
    table { width:100%; border-collapse:collapse; min-width:1180px; }
    th, td { padding:0.75rem; border-bottom:1px solid #ece5d7; vertical-align:top; text-align:left; }
    .status-pill { display:inline-flex; align-items:center; border-radius:999px; padding:0.25rem 0.6rem; font-size:0.82rem; font-weight:600; }
    .status-eligible { background:#eef8f1; color:#24573a; }
    .status-blocked { background:#fff6e5; color:#8a5a00; }
    .status-neutral { background:#eef1f4; color:#425466; }
    .severity-pill { display:inline-flex; align-items:center; border-radius:999px; padding:0.2rem 0.55rem; margin-left:0.35rem; font-size:0.74rem; font-weight:700; }
    .severity-info { background:#eef1f4; color:#425466; }
    .severity-warning { background:#fff3dd; color:#8a5a00; }
    .severity-error { background:#fde8e8; color:#8a1f1f; }
    .severity-critical { background:#f8d7d7; color:#6f1111; }
    .alert-chip-list { display:flex; flex-wrap:wrap; gap:0.35rem; margin-top:0.45rem; }
    .alert-chip { display:inline-flex; align-items:center; padding:0.2rem 0.55rem; border-radius:0.8rem; font-size:0.75rem; font-weight:700; }
    .row-reason { display:block; color:#5f6b76; margin-top:0.35rem; }
    .error { margin:0; color:#7a2020; }
    .success { margin:0; color:#24573a; }
    .modal-backdrop { position:fixed; inset:0; background:rgba(8, 15, 25, 0.4); display:grid; place-items:center; padding:1rem; z-index:20; }
    .modal-card { width:min(1180px, 100%); max-height:90vh; overflow:auto; background:#fff; border-radius:1rem; padding:1rem; display:grid; gap:1rem; }
    .detail-modal { align-content:start; }
    .modal-header { display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; }
    .summary-grid, .history-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(280px, 1fr)); gap:1rem; }
    .summary-card { border:1px solid #ece5d7; border-radius:0.9rem; padding:1rem; display:grid; gap:0.75rem; }
    dl { display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:0.75rem; margin:0; }
    dt { font-size:0.8rem; color:#5f6b76; }
    dd { margin:0.15rem 0 0; font-weight:600; color:#182533; }
    .alert-list { list-style:none; padding:0; margin:0; display:grid; gap:0.65rem; }
    .alert-item { border:1px solid #ece5d7; border-radius:0.8rem; padding:0.75rem; }
    .alert-item p { margin:0.2rem 0 0; color:#425466; }
    .alert-warning { background:#fff3dd; color:#8a5a00; }
    .alert-error { background:#fde8e8; color:#8a1f1f; }
    .alert-critical { background:#fdeaea; color:#8a1f1f; }
    .alert-alert-info { background:#eef1f4; color:#425466; }
    .quick-chip { border:1px solid #d8d1c2; }
    .quick-chip.quick-chip-active { outline:2px solid #182533; }
    .history-list { display:grid; gap:0.75rem; }
    .history-item { border:1px solid #ece5d7; border-radius:0.8rem; padding:0.75rem; display:grid; gap:0.35rem; }
    .history-item header { display:flex; justify-content:space-between; gap:1rem; }
    .history-item p, .history-item small { margin:0; color:#425466; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PaymentComplementExternalBaseDocumentsPageComponent {
  private readonly api = inject(PaymentComplementsApiService);

  protected readonly items = signal<ExternalRepBaseDocumentItemResponse[]>([]);
  protected readonly selectedIds = signal<number[]>([]);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly totalCount = signal(0);
  protected readonly totalPages = signal(0);
  protected readonly summaryCounts = signal<RepOperationalSummaryCountsResponse>(createEmptySummaryCounts());
  protected readonly bulkRefreshing = signal(false);
  protected readonly bulkRefreshError = signal<string | null>(null);
  protected readonly bulkRefreshResult = signal<RepBaseDocumentBulkRefreshResponse | null>(null);
  protected readonly showDetailModal = signal(false);
  protected readonly loadingDetail = signal(false);
  protected readonly detailError = signal<string | null>(null);
  protected readonly selectedDetail = signal<ExternalRepBaseDocumentDetailResponse | null>(null);
  protected readonly showRegisterPaymentForm = signal(false);
  protected readonly submittingPayment = signal(false);
  protected readonly preparingRep = signal(false);
  protected readonly stampingRep = signal(false);
  protected readonly refreshingRep = signal<number | null>(null);
  protected readonly cancellingRep = signal<number | null>(null);
  protected readonly operationMessage = signal<string | null>(null);
  protected readonly operationError = signal<string | null>(null);

  protected fromDate = '';
  protected toDate = '';
  protected receiverRfc = '';
  protected query = '';
  protected validationStatus = '';
  protected eligibleFilter = '';
  protected blockedFilter = '';
  protected alertCodeFilter = '';
  protected severityFilter = '';
  protected nextRecommendedActionFilter = '';
  protected quickViewFilter = '';
  protected readonly alertOptions = REP_OPERATIONAL_ALERT_OPTIONS;
  protected readonly severityOptions = REP_OPERATIONAL_SEVERITY_OPTIONS;
  protected readonly recommendedActionOptions = REP_RECOMMENDED_ACTION_OPTIONS;
  protected readonly quickViewOptions = REP_QUICK_VIEW_OPTIONS;

  protected paymentDate = '';
  protected paymentFormSat = '03';
  protected paymentAmount = 0;
  protected paymentReference = '';
  protected paymentNotes = '';

  constructor() {
    void this.applyFilters();
  }

  protected async applyFilters(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);

    try {
      const response = await firstValueFrom(this.api.searchExternalBaseDocuments({
        page: 1,
        pageSize: 25,
        fromDate: this.fromDate || null,
        toDate: this.toDate || null,
        receiverRfc: this.receiverRfc || null,
        query: this.query || null,
        validationStatus: this.validationStatus || null,
        eligible: parseBoolean(this.eligibleFilter),
        blocked: parseBoolean(this.blockedFilter),
        alertCode: this.alertCodeFilter || null,
        severity: this.severityFilter || null,
        nextRecommendedAction: this.nextRecommendedActionFilter || null,
        quickView: this.quickViewFilter || null
      }));

      this.items.set(response.items);
      this.selectedIds.set([]);
      this.summaryCounts.set(response.summaryCounts ?? createEmptySummaryCounts());
      this.totalCount.set(response.totalCount);
      this.totalPages.set(response.totalPages);
    } catch (error) {
      this.errorMessage.set(extractApiErrorMessage(error, 'No fue posible consultar los CFDI externos importados.'));
      this.items.set([]);
      this.summaryCounts.set(createEmptySummaryCounts());
      this.totalCount.set(0);
      this.totalPages.set(0);
    } finally {
      this.loading.set(false);
    }
  }

  protected clearFilters(): void {
    this.fromDate = '';
    this.toDate = '';
    this.receiverRfc = '';
    this.query = '';
    this.validationStatus = '';
    this.eligibleFilter = '';
    this.blockedFilter = '';
    this.alertCodeFilter = '';
    this.severityFilter = '';
    this.nextRecommendedActionFilter = '';
    this.quickViewFilter = '';
    void this.applyFilters();
  }

  protected async openDetail(externalRepBaseDocumentId: number): Promise<void> {
    this.showDetailModal.set(true);
    await this.loadDetail(externalRepBaseDocumentId);
  }

  protected closeDetail(): void {
    this.showDetailModal.set(false);
    this.loadingDetail.set(false);
    this.detailError.set(null);
    this.selectedDetail.set(null);
    this.showRegisterPaymentForm.set(false);
    this.operationMessage.set(null);
    this.operationError.set(null);
  }

  protected openRegisterPaymentForm(): void {
    const detail = this.selectedDetail();
    if (!detail) {
      return;
    }

    this.operationMessage.set(null);
    this.operationError.set(null);
    this.paymentDate = new Date().toISOString().slice(0, 10);
    this.paymentFormSat = '03';
    this.paymentAmount = detail.summary.outstandingBalance;
    this.paymentReference = '';
    this.paymentNotes = '';
    this.showRegisterPaymentForm.set(true);
  }

  protected cancelRegisterPaymentForm(): void {
    this.showRegisterPaymentForm.set(false);
  }

  protected async submitRegisterPayment(): Promise<void> {
    const detail = this.selectedDetail();
    if (!detail) {
      return;
    }

    this.submittingPayment.set(true);
    this.operationMessage.set(null);
    this.operationError.set(null);

    try {
      const response = await firstValueFrom(this.api.registerExternalBaseDocumentPayment(detail.summary.externalRepBaseDocumentId, {
        paymentDate: this.paymentDate,
        paymentFormSat: this.paymentFormSat,
        amount: this.paymentAmount,
        reference: this.paymentReference || null,
        notes: this.paymentNotes || null
      }));

      this.showRegisterPaymentForm.set(false);
      this.operationMessage.set(`Pago registrado y aplicado. Pago #${response.accountsReceivablePaymentId ?? 'n/a'}.`);
      await this.loadDetail(detail.summary.externalRepBaseDocumentId);
      await this.applyFilters();
    } catch (error) {
      this.operationError.set(extractApiErrorMessage(error, 'No fue posible registrar el pago sobre el CFDI externo.'));
    } finally {
      this.submittingPayment.set(false);
    }
  }

  protected async prepareRep(): Promise<void> {
    const detail = this.selectedDetail();
    if (!detail) {
      return;
    }

    this.preparingRep.set(true);
    this.operationMessage.set(null);
    this.operationError.set(null);

    try {
      const response = await firstValueFrom(this.api.prepareExternalBaseDocumentPaymentComplement(
        detail.summary.externalRepBaseDocumentId,
        {}
      ));
      this.operationMessage.set(response.outcome === 'AlreadyPrepared'
        ? 'El REP ya estaba preparado para este CFDI externo.'
        : 'REP preparado correctamente para el CFDI externo.');
      await this.loadDetail(detail.summary.externalRepBaseDocumentId);
      await this.applyFilters();
    } catch (error) {
      this.operationError.set(extractApiErrorMessage(error, 'No fue posible preparar el REP del CFDI externo.'));
    } finally {
      this.preparingRep.set(false);
    }
  }

  protected async stampRep(): Promise<void> {
    const detail = this.selectedDetail();
    if (!detail) {
      return;
    }

    this.stampingRep.set(true);
    this.operationMessage.set(null);
    this.operationError.set(null);

    try {
      const response = await firstValueFrom(this.api.stampExternalBaseDocumentPaymentComplement(
        detail.summary.externalRepBaseDocumentId,
        {}
      ));
      this.operationMessage.set(response.outcome === 'AlreadyStamped'
        ? 'El REP externo ya estaba timbrado.'
        : `REP timbrado correctamente${response.stampUuid ? ` (${response.stampUuid})` : ''}.`);
      await this.loadDetail(detail.summary.externalRepBaseDocumentId);
      await this.applyFilters();
    } catch (error) {
      this.operationError.set(extractApiErrorMessage(error, 'No fue posible timbrar el REP del CFDI externo.'));
    } finally {
      this.stampingRep.set(false);
    }
  }

  protected async refreshRep(item: ExternalRepBaseDocumentPaymentComplementResponse): Promise<void> {
    const detail = this.selectedDetail();
    if (!detail) {
      return;
    }

    this.refreshingRep.set(item.paymentComplementId);
    this.operationMessage.set(null);
    this.operationError.set(null);

    try {
      const response = await firstValueFrom(this.api.refreshExternalBaseDocumentPaymentComplementStatus(
        detail.summary.externalRepBaseDocumentId,
        { paymentComplementDocumentId: item.paymentComplementId }
      ));
      this.operationMessage.set(`REP actualizado${response.lastKnownExternalStatus ? ` (${response.lastKnownExternalStatus})` : ''}.`);
      await this.loadDetail(detail.summary.externalRepBaseDocumentId);
      await this.applyFilters();
    } catch (error) {
      this.operationError.set(extractApiErrorMessage(error, 'No fue posible refrescar el estatus del REP externo.'));
    } finally {
      this.refreshingRep.set(null);
    }
  }

  protected async cancelRep(item: ExternalRepBaseDocumentPaymentComplementResponse): Promise<void> {
    const detail = this.selectedDetail();
    if (!detail) {
      return;
    }

    this.cancellingRep.set(item.paymentComplementId);
    this.operationMessage.set(null);
    this.operationError.set(null);

    try {
      const response = await firstValueFrom(this.api.cancelExternalBaseDocumentPaymentComplement(
        detail.summary.externalRepBaseDocumentId,
        {
          paymentComplementDocumentId: item.paymentComplementId,
          cancellationReasonCode: '02',
          replacementUuid: null
        }
      ));
      this.operationMessage.set(`REP cancelado${response.cancellationStatus ? ` (${getDisplayLabel(response.cancellationStatus)})` : ''}.`);
      await this.loadDetail(detail.summary.externalRepBaseDocumentId);
      await this.applyFilters();
    } catch (error) {
      this.operationError.set(extractApiErrorMessage(error, 'No fue posible cancelar el REP externo.'));
    } finally {
      this.cancellingRep.set(null);
    }
  }

  protected canRegisterPayment(detail: ExternalRepBaseDocumentDetailResponse): boolean {
    return (detail.summary.availableActions ?? []).includes('RegisterPayment');
  }

  protected canPrepareRep(detail: ExternalRepBaseDocumentDetailResponse): boolean {
    return (detail.summary.availableActions ?? []).includes('PrepareRep');
  }

  protected canStampRep(detail: ExternalRepBaseDocumentDetailResponse): boolean {
    return (detail.summary.availableActions ?? []).includes('StampRep');
  }

  protected canRefreshRepItem(item: ExternalRepBaseDocumentPaymentComplementResponse): boolean {
    return ['Stamped', 'CancellationRequested', 'CancellationRejected', 'Cancelled'].includes(item.status);
  }

  protected canCancelRepItem(item: ExternalRepBaseDocumentPaymentComplementResponse): boolean {
    return item.status === 'Stamped' || item.status === 'CancellationRejected';
  }

  protected readonly getDisplayLabel = getDisplayLabel;

  protected formatUtc(value: string | null | undefined): string {
    if (!value) {
      return '—';
    }

    return new Intl.DateTimeFormat('es-MX', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'UTC' }).format(new Date(value));
  }

  protected formatOptionalUtc(value: string | null | undefined): string {
    return value ? this.formatUtc(value) : '—';
  }

  protected buildSeriesFolio(series: string | null | undefined, folio: string | null | undefined): string {
    return [series, folio].filter(Boolean).join('-') || '—';
  }

  protected visibleAlerts(alerts: RepOperationalAlertResponse[]): RepOperationalAlertResponse[] {
    return alerts.slice(0, 3);
  }

  protected getPrimarySeverity(source: { alerts?: RepOperationalAlertResponse[] | null }): string | null {
    return resolvePrimarySeverity(source.alerts ?? []);
  }

  protected getAlerts(source: { alerts?: RepOperationalAlertResponse[] | null }): RepOperationalAlertResponse[] {
    return source.alerts ?? [];
  }

  protected getRecommendedActionLabel(action?: string | null): string {
    return action ? getDisplayLabel(action) : 'Sin acción disponible';
  }

  protected async applyQuickView(quickView: string): Promise<void> {
    this.quickViewFilter = this.quickViewFilter === quickView ? '' : quickView;
    await this.applyFilters();
  }

  protected clearOperationalFilters(): void {
    this.alertCodeFilter = '';
    this.severityFilter = '';
    this.nextRecommendedActionFilter = '';
    this.quickViewFilter = '';
    void this.applyFilters();
  }

  protected countForQuickView(code: string): number {
    return this.summaryCounts().quickViewCounts.find((item) => item.code === code)?.count ?? 0;
  }

  protected hasOperationalFilters(): boolean {
    return Boolean(this.alertCodeFilter || this.severityFilter || this.nextRecommendedActionFilter || this.quickViewFilter);
  }

  protected isSelected(externalRepBaseDocumentId: number): boolean {
    return this.selectedIds().includes(externalRepBaseDocumentId);
  }

  protected selectedCount(): number {
    return this.selectedIds().length;
  }

  protected allVisibleSelected(): boolean {
    const currentItems = this.items();
    return currentItems.length > 0 && currentItems.every((item) => this.isSelected(item.externalRepBaseDocumentId));
  }

  protected toggleSelection(externalRepBaseDocumentId: number, checked: boolean): void {
    if (checked) {
      this.selectedIds.set([...new Set([...this.selectedIds(), externalRepBaseDocumentId])]);
      return;
    }

    this.selectedIds.set(this.selectedIds().filter((id) => id !== externalRepBaseDocumentId));
  }

  protected toggleSelectAll(checked: boolean): void {
    if (checked) {
      this.selectedIds.set(this.items().map((item) => item.externalRepBaseDocumentId));
      return;
    }

    this.selectedIds.set([]);
  }

  protected clearBulkRefreshResult(): void {
    this.bulkRefreshResult.set(null);
    this.bulkRefreshError.set(null);
  }

  protected async refreshSelectedDocuments(): Promise<void> {
    if (!this.selectedCount()) {
      return;
    }

    await this.executeBulkRefresh('Selected');
  }

  protected async refreshFilteredDocuments(): Promise<void> {
    if (!this.items().length) {
      return;
    }

    await this.executeBulkRefresh('Filtered');
  }

  private async loadDetail(externalRepBaseDocumentId: number): Promise<void> {
    this.loadingDetail.set(true);
    this.detailError.set(null);

    try {
      const detail = await firstValueFrom(this.api.getExternalBaseDocumentById(externalRepBaseDocumentId));
      this.selectedDetail.set(detail);
    } catch (error) {
      this.detailError.set(extractApiErrorMessage(error, 'No fue posible cargar el detalle del CFDI externo.'));
      this.selectedDetail.set(null);
    } finally {
      this.loadingDetail.set(false);
    }
  }

  private async executeBulkRefresh(mode: string): Promise<void> {
    this.bulkRefreshing.set(true);
    this.bulkRefreshError.set(null);

    try {
      const result = await firstValueFrom(this.api.bulkRefreshExternalBaseDocuments({
        mode,
        documents: this.selectedIds().map((sourceId) => ({ sourceType: 'External', sourceId })),
        fromDate: this.fromDate || null,
        toDate: this.toDate || null,
        receiverRfc: this.receiverRfc || null,
        query: this.query || null,
        validationStatus: this.validationStatus || null,
        eligible: parseBoolean(this.eligibleFilter),
        blocked: parseBoolean(this.blockedFilter),
        alertCode: this.alertCodeFilter || null,
        severity: this.severityFilter || null,
        nextRecommendedAction: this.nextRecommendedActionFilter || null,
        quickView: this.quickViewFilter || null
      }));

      this.bulkRefreshResult.set(result);
      this.selectedIds.set([]);

      const refreshedIds = new Set(result.items.filter((item) => item.attempted).map((item) => item.sourceId));
      const detail = this.selectedDetail();
      if (detail && refreshedIds.has(detail.summary.externalRepBaseDocumentId)) {
        await this.loadDetail(detail.summary.externalRepBaseDocumentId);
      }

      await this.applyFilters();
      this.operationMessage.set(`Refresh masivo externo ejecutado: ${result.refreshedCount} actualizados, ${result.noChangesCount} sin cambios, ${result.blockedCount} bloqueados, ${result.failedCount} fallidos.`);
    } catch (error) {
      this.bulkRefreshError.set(extractApiErrorMessage(error, 'No fue posible ejecutar el refresh masivo externo.'));
    } finally {
      this.bulkRefreshing.set(false);
    }
  }
}

function parseBoolean(value: string): boolean | null {
  if (value === 'true') {
    return true;
  }

  if (value === 'false') {
    return false;
  }

  return null;
}

function createEmptySummaryCounts(): RepOperationalSummaryCountsResponse {
  return {
    infoCount: 0,
    warningCount: 0,
    errorCount: 0,
    criticalCount: 0,
    blockedCount: 0,
    alertCounts: [],
    nextRecommendedActionCounts: [],
    quickViewCounts: []
  };
}

function resolvePrimarySeverity(alerts: RepOperationalAlertResponse[]): string | null {
  for (const severity of REP_OPERATIONAL_SEVERITY_PRIORITY) {
    if (alerts.some((alert) => alert.severity === severity)) {
      return severity;
    }
  }

  return null;
}

const REP_OPERATIONAL_ALERT_OPTIONS = [
  'AppliedPaymentsWithoutStampedRep',
  'PreparedRepPendingStamp',
  'RepStampingRejected',
  'RepCancellationRejected',
  'BlockedOperation',
  'CancelledBaseDocument',
  'ValidationBlocked',
  'SatValidationUnavailable',
  'UnsupportedCurrency',
  'DuplicateExternalInvoice',
  'StampedRepAvailable'
];

const REP_OPERATIONAL_SEVERITY_OPTIONS = ['info', 'warning', 'error', 'critical'];
const REP_OPERATIONAL_SEVERITY_PRIORITY = ['critical', 'error', 'warning', 'info'];
const REP_RECOMMENDED_ACTION_OPTIONS = ['RegisterPayment', 'PrepareRep', 'StampRep', 'RefreshRepStatus', 'CancelRep', 'ViewDetail', 'Blocked', 'NoAction'];
const REP_QUICK_VIEW_OPTIONS = ['PendingStamp', 'WithError', 'Blocked', 'AppliedPaymentWithoutStampedRep', 'PendingRefresh', 'Stamped'];
