import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { getDisplayLabel } from '../../../shared/ui/display-labels';
import { PaymentComplementsApiService } from '../infrastructure/payment-complements-api.service';
import {
  ExternalRepBaseDocumentDetailResponse,
  InternalRepBaseDocumentDetailResponse,
  RepBaseDocumentBulkRefreshResponse,
  RepOperationalAlertResponse,
  RepOperationalSummaryCountsResponse,
  RepBaseDocumentItemResponse
} from '../models/payment-complements.models';

@Component({
  selector: 'app-payment-complement-unified-base-documents-page',
  imports: [FormsModule, DecimalPipe],
  template: `
    <section class="page">
      <section class="card">
        <header>
          <p class="eyebrow">Unificada</p>
          <h3>Bandeja base REP interna y externa</h3>
          <p class="helper">La bandeja unificada compone documentos internos y externos bajo un contrato común. La operación fina sigue viviendo en sus vistas dedicadas, pero aquí ya se concentran seguimiento, alertas y acción recomendada.</p>
        </header>

        <form class="filters" (ngSubmit)="applyFilters()">
          <label><span>Desde</span><input [(ngModel)]="fromDate" name="fromDate" type="date" /></label>
          <label><span>Hasta</span><input [(ngModel)]="toDate" name="toDate" type="date" /></label>
          <label><span>RFC receptor</span><input [(ngModel)]="receiverRfc" name="receiverRfc" /></label>
          <label class="wide"><span>Búsqueda general</span><input [(ngModel)]="query" name="query" placeholder="UUID, RFC, nombre, serie o folio" /></label>
          <label>
            <span>Origen</span>
            <select [(ngModel)]="sourceType" name="sourceType">
              <option value="">Todos</option>
              <option value="Internal">Internal</option>
              <option value="External">External</option>
            </select>
          </label>
          <label>
            <span>Validación externa</span>
            <select [(ngModel)]="validationStatus" name="validationStatus">
              <option value="">Todas</option>
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
          <p class="helper">Cargando bandeja unificada...</p>
        } @else if (errorMessage()) {
          <p class="error">{{ errorMessage() }}</p>
        } @else if (!items().length) {
          <p class="helper">No se encontraron documentos base REP con los filtros actuales.</p>
        } @else {
          <p class="helper">Mostrando {{ items().length }} de {{ totalCount() }} documentos base.</p>

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
            <section class="summary-card">
              <h4>Resultado del refresh masivo</h4>
              <p class="helper">
                Modo {{ getDisplayLabel(result.mode) }} · solicitados {{ result.totalRequested }} · procesados {{ result.totalAttempted }} · actualizados {{ result.refreshedCount }} · sin cambios {{ result.noChangesCount }} · bloqueados {{ result.blockedCount }} · fallidos {{ result.failedCount }}
              </p>
              @if (result.items.length) {
                <div class="table-wrap">
                  <table>
                    <thead>
                      <tr>
                        <th>Origen</th>
                        <th>Documento</th>
                        <th>Resultado</th>
                        <th>REP</th>
                        <th>Estado externo</th>
                        <th>Mensaje</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (item of result.items; track item.sourceType + '-' + item.sourceId) {
                        <tr>
                          <td>{{ getDisplayLabel(item.sourceType) }}</td>
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
                  <th>Origen</th>
                  <th>Emisión</th>
                  <th>Serie/Folio</th>
                  <th>UUID</th>
                  <th>Receptor</th>
                  <th>Total</th>
                  <th>Saldo</th>
                  <th>REP</th>
                  <th>Validación</th>
                  <th>SAT</th>
                  <th>Operativo</th>
                  <th>Acción</th>
                </tr>
              </thead>
              <tbody>
                @for (item of items(); track item.sourceType + '-' + item.sourceId) {
                  <tr>
                    <td>
                      <input type="checkbox" [checked]="isSelected(item)" (change)="toggleSelection(item, $any($event.target).checked)" [disabled]="bulkRefreshing()" />
                    </td>
                    <td><span class="source-pill" [class.source-internal]="item.sourceType === 'Internal'" [class.source-external]="item.sourceType === 'External'">{{ getDisplayLabel(item.sourceType) }}</span></td>
                    <td>{{ formatUtc(item.issuedAtUtc) }}</td>
                    <td>{{ buildSeriesFolio(item.series, item.folio) }}</td>
                    <td>{{ item.uuid || '—' }}</td>
                    <td>{{ item.receiverRfc }} · {{ item.receiverLegalName || '—' }}</td>
                    <td>{{ item.total | number:'1.2-2' }}</td>
                    <td>{{ item.outstandingBalance != null ? (item.outstandingBalance | number:'1.2-2') : '—' }}</td>
                    <td>{{ item.repCount ?? '—' }}</td>
                    <td>{{ item.validationStatus ? getDisplayLabel(item.validationStatus) : '—' }}</td>
                    <td>{{ item.satStatus ? getDisplayLabel(item.satStatus) : '—' }}</td>
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
                            <span class="alert-chip" [class.alert-critical]="alert.severity === 'critical'" [class.alert-error]="alert.severity === 'error'" [class.alert-warning]="alert.severity === 'warning'" [class.alert-info]="alert.severity === 'info'">
                              {{ getDisplayLabel(alert.code) }}
                            </span>
                          }
                        </div>
                      }
                    </td>
                    <td><button type="button" class="secondary small" (click)="openDetail(item)">Ver detalle</button></td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </section>

      @if (showDetailModal()) {
        <section class="modal-backdrop" (click)="closeDetail()">
          <section class="modal-card" (click)="$event.stopPropagation()">
            <div class="modal-header">
              <div>
                <p class="eyebrow">{{ selectedItem()?.sourceType === 'External' ? 'Externo' : 'Interno' }}</p>
                <h3>Detalle operativo del documento base</h3>
              </div>
              <button type="button" class="secondary" (click)="closeDetail()">Cerrar</button>
            </div>

            @if (loadingDetail()) {
              <p class="helper">Cargando detalle...</p>
            } @else if (detailError()) {
              <p class="error">{{ detailError() }}</p>
            } @else if (selectedItem()?.sourceType === 'External' && selectedExternalDetail(); as externalDetail) {
              <section class="summary-grid">
                <article class="summary-card">
                  <h4>Resumen fiscal externo</h4>
                  <dl>
                    <div><dt>UUID</dt><dd>{{ externalDetail.summary.uuid }}</dd></div>
                    <div><dt>Serie/Folio</dt><dd>{{ buildSeriesFolio(externalDetail.summary.series, externalDetail.summary.folio) }}</dd></div>
                    <div><dt>Emisión</dt><dd>{{ formatUtc(externalDetail.summary.issuedAtUtc) }}</dd></div>
                    <div><dt>Emisor</dt><dd>{{ externalDetail.summary.issuerRfc }}</dd></div>
                    <div><dt>Receptor</dt><dd>{{ externalDetail.summary.receiverRfc }}</dd></div>
                    <div><dt>Total</dt><dd>{{ externalDetail.summary.total | number:'1.2-2' }}</dd></div>
                    <div><dt>Método/Forma</dt><dd>{{ externalDetail.summary.paymentMethodSat }} / {{ externalDetail.summary.paymentFormSat }}</dd></div>
                  </dl>
                </article>

                <article class="summary-card">
                  <h4>Estado externo</h4>
                  <dl>
                    <div><dt>Validación</dt><dd>{{ getDisplayLabel(externalDetail.summary.validationStatus) }}</dd></div>
                    <div><dt>SAT</dt><dd>{{ getDisplayLabel(externalDetail.summary.satStatus) }}</dd></div>
                    <div><dt>Operativo</dt><dd>{{ getDisplayLabel(externalDetail.summary.operationalStatus) }}</dd></div>
                    <div><dt>Motivo</dt><dd>{{ externalDetail.summary.primaryReasonMessage }}</dd></div>
                    <div><dt>Acción recomendada</dt><dd>{{ getRecommendedActionLabel(externalDetail.summary.nextRecommendedAction) }}</dd></div>
                    <div><dt>Importado</dt><dd>{{ formatUtc(externalDetail.summary.importedAtUtc) }}</dd></div>
                  </dl>
                  @if (getAlerts(externalDetail.summary).length) {
                    <ul class="alert-list">
                      @for (alert of getAlerts(externalDetail.summary); track alert.code + '-' + alert.message) {
                        <li class="alert-item" [class.alert-critical]="alert.severity === 'critical'" [class.alert-error]="alert.severity === 'error'" [class.alert-warning]="alert.severity === 'warning'" [class.alert-info]="alert.severity === 'info'">
                          <strong>{{ getDisplayLabel(alert.code) }}</strong>
                          <p>{{ alert.message }}</p>
                        </li>
                      }
                    </ul>
                  }
                  <p class="helper">La operación REP externa vive en la pestaña Externos; esta vista unificada conserva un contexto común de seguimiento.</p>
                </article>
              </section>
            } @else if (selectedItem()?.sourceType === 'Internal' && selectedInternalDetail(); as internalDetail) {
              <section class="summary-grid">
                <article class="summary-card">
                  <h4>Resumen fiscal interno</h4>
                  <dl>
                    <div><dt>FiscalDocumentId</dt><dd>{{ internalDetail.summary.fiscalDocumentId }}</dd></div>
                    <div><dt>UUID</dt><dd>{{ internalDetail.summary.uuid || '—' }}</dd></div>
                    <div><dt>Serie/Folio</dt><dd>{{ buildSeriesFolio(internalDetail.summary.series, internalDetail.summary.folio) }}</dd></div>
                    <div><dt>Emisión</dt><dd>{{ formatUtc(internalDetail.summary.issuedAtUtc) }}</dd></div>
                    <div><dt>Receptor</dt><dd>{{ internalDetail.summary.receiverRfc }} · {{ internalDetail.summary.receiverLegalName }}</dd></div>
                    <div><dt>Total</dt><dd>{{ internalDetail.summary.total | number:'1.2-2' }}</dd></div>
                    <div><dt>Pagado</dt><dd>{{ internalDetail.summary.paidTotal | number:'1.2-2' }}</dd></div>
                    <div><dt>Saldo</dt><dd>{{ internalDetail.summary.outstandingBalance | number:'1.2-2' }}</dd></div>
                  </dl>
                </article>

                <article class="summary-card">
                  <h4>Estado interno</h4>
                  <dl>
                    <div><dt>REP</dt><dd>{{ getDisplayLabel(internalDetail.summary.repOperationalStatus) }}</dd></div>
                    <div><dt>Motivo</dt><dd>{{ internalDetail.summary.eligibility.primaryReasonMessage }}</dd></div>
                    <div><dt>Acción recomendada</dt><dd>{{ getRecommendedActionLabel(internalDetail.summary.nextRecommendedAction) }}</dd></div>
                    <div><dt>Pagos</dt><dd>{{ internalDetail.summary.registeredPaymentCount }}</dd></div>
                    <div><dt>REP emitidos</dt><dd>{{ internalDetail.summary.stampedPaymentComplementCount }}</dd></div>
                    <div><dt>REP pendiente</dt><dd>{{ internalDetail.operationalState?.repPendingFlag ? 'Sí' : 'No' }}</dd></div>
                  </dl>
                  @if (getAlerts(internalDetail.summary).length) {
                    <ul class="alert-list">
                      @for (alert of getAlerts(internalDetail.summary); track alert.code + '-' + alert.message) {
                        <li class="alert-item" [class.alert-critical]="alert.severity === 'critical'" [class.alert-error]="alert.severity === 'error'" [class.alert-warning]="alert.severity === 'warning'" [class.alert-info]="alert.severity === 'info'">
                          <strong>{{ getDisplayLabel(alert.code) }}</strong>
                          <p>{{ alert.message }}</p>
                        </li>
                      }
                    </ul>
                  }
                  <p class="helper">Para operar pagos y REP internos usa la pestaña Internos, que conserva el flujo completo de operación.</p>
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
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    .helper { margin:0; color:#5f6b76; }
    .filters { display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:0.85rem; }
    .filters label { display:grid; gap:0.35rem; }
    .filters .wide { grid-column:1 / -1; }
    input, select { font:inherit; padding:0.65rem 0.75rem; border:1px solid #d8d1c2; border-radius:0.7rem; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    button.secondary { background:#d8c49b; color:#182533; }
    button.small { padding:0.5rem 0.75rem; font-size:0.9rem; }
    .actions, .bulk-toolbar { display:flex; gap:0.75rem; flex-wrap:wrap; align-items:center; }
    .selection-toggle { display:inline-flex; align-items:center; gap:0.5rem; }
    .table-wrap { overflow:auto; }
    table { width:100%; border-collapse:collapse; min-width:1120px; }
    th, td { padding:0.75rem; border-bottom:1px solid #ece5d7; vertical-align:top; text-align:left; }
    .source-pill, .status-pill { display:inline-flex; align-items:center; border-radius:999px; padding:0.25rem 0.6rem; font-size:0.82rem; font-weight:600; }
    .source-internal { background:#eef1f4; color:#182533; }
    .source-external { background:#fff6e5; color:#8a5a00; }
    .status-eligible { background:#eef8f1; color:#24573a; }
    .status-blocked { background:#fff6e5; color:#8a5a00; }
    .status-neutral { background:#eef1f4; color:#425466; }
    .quick-filters { display:flex; flex-wrap:wrap; gap:0.75rem; align-items:center; }
    .alert-chip-list { display:flex; flex-wrap:wrap; gap:0.35rem; margin-top:0.45rem; }
    .alert-chip { display:inline-flex; align-items:center; padding:0.2rem 0.55rem; border-radius:0.8rem; font-size:0.75rem; font-weight:700; }
    .severity-pill { display:inline-flex; align-items:center; border-radius:999px; padding:0.2rem 0.55rem; margin-left:0.35rem; font-size:0.74rem; font-weight:700; }
    .severity-info { background:#eef1f4; color:#425466; }
    .severity-warning { background:#fff3dd; color:#8a5a00; }
    .severity-error { background:#fde8e8; color:#8a1f1f; }
    .severity-critical { background:#f8d7d7; color:#6f1111; }
    .alert-warning { background:#fff3dd; color:#8a5a00; }
    .alert-error { background:#fde8e8; color:#8a1f1f; }
    .alert-critical { background:#fdeaea; color:#8a1f1f; }
    .alert-info { background:#eef1f4; color:#425466; }
    .quick-chip { border:1px solid #d8d1c2; }
    .quick-chip.quick-chip-active { outline:2px solid #182533; }
    .row-reason { display:block; color:#5f6b76; margin-top:0.35rem; }
    .error { margin:0; color:#7a2020; }
    .modal-backdrop { position:fixed; inset:0; background:rgba(8, 15, 25, 0.4); display:grid; place-items:center; padding:1rem; z-index:20; }
    .modal-card { width:min(960px, 100%); max-height:90vh; overflow:auto; background:#fff; border-radius:1rem; padding:1rem; display:grid; gap:1rem; }
    .modal-header { display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; }
    .summary-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(280px, 1fr)); gap:1rem; }
    .summary-card { border:1px solid #ece5d7; border-radius:0.9rem; padding:1rem; display:grid; gap:0.75rem; }
    .alert-list { list-style:none; padding:0; margin:0; display:grid; gap:0.65rem; }
    .alert-item { border:1px solid #ece5d7; border-radius:0.8rem; padding:0.75rem; }
    .alert-item p { margin:0.2rem 0 0; color:#425466; }
    dl { display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:0.75rem; margin:0; }
    dt { font-size:0.8rem; color:#5f6b76; }
    dd { margin:0.15rem 0 0; font-weight:600; color:#182533; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PaymentComplementUnifiedBaseDocumentsPageComponent {
  private readonly api = inject(PaymentComplementsApiService);

  protected readonly items = signal<RepBaseDocumentItemResponse[]>([]);
  protected readonly selectedKeys = signal<string[]>([]);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly totalCount = signal(0);
  protected readonly summaryCounts = signal<RepOperationalSummaryCountsResponse>(createEmptySummaryCounts());
  protected readonly bulkRefreshing = signal(false);
  protected readonly bulkRefreshError = signal<string | null>(null);
  protected readonly bulkRefreshResult = signal<RepBaseDocumentBulkRefreshResponse | null>(null);
  protected readonly showDetailModal = signal(false);
  protected readonly loadingDetail = signal(false);
  protected readonly detailError = signal<string | null>(null);
  protected readonly selectedItem = signal<RepBaseDocumentItemResponse | null>(null);
  protected readonly selectedInternalDetail = signal<InternalRepBaseDocumentDetailResponse | null>(null);
  protected readonly selectedExternalDetail = signal<ExternalRepBaseDocumentDetailResponse | null>(null);

  protected fromDate = '';
  protected toDate = '';
  protected receiverRfc = '';
  protected query = '';
  protected sourceType = '';
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

  constructor() {
    void this.applyFilters();
  }

  protected async applyFilters(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);

    try {
      const response = await firstValueFrom(this.api.searchBaseDocuments({
        page: 1,
        pageSize: 25,
        fromDate: this.fromDate || null,
        toDate: this.toDate || null,
        receiverRfc: this.receiverRfc || null,
        query: this.query || null,
        sourceType: this.sourceType || null,
        validationStatus: this.validationStatus || null,
        eligible: parseBoolean(this.eligibleFilter),
        blocked: parseBoolean(this.blockedFilter),
        alertCode: this.alertCodeFilter || null,
        severity: this.severityFilter || null,
        nextRecommendedAction: this.nextRecommendedActionFilter || null,
        quickView: this.quickViewFilter || null
      }));

      this.items.set(response.items);
      this.selectedKeys.set([]);
      this.summaryCounts.set(response.summaryCounts ?? createEmptySummaryCounts());
      this.totalCount.set(response.totalCount);
    } catch (error) {
      this.errorMessage.set(extractApiErrorMessage(error, 'No fue posible consultar la bandeja REP unificada.'));
      this.items.set([]);
      this.summaryCounts.set(createEmptySummaryCounts());
      this.totalCount.set(0);
    } finally {
      this.loading.set(false);
    }
  }

  protected clearFilters(): void {
    this.fromDate = '';
    this.toDate = '';
    this.receiverRfc = '';
    this.query = '';
    this.sourceType = '';
    this.validationStatus = '';
    this.eligibleFilter = '';
    this.blockedFilter = '';
    this.alertCodeFilter = '';
    this.severityFilter = '';
    this.nextRecommendedActionFilter = '';
    this.quickViewFilter = '';
    void this.applyFilters();
  }

  protected async openDetail(item: RepBaseDocumentItemResponse): Promise<void> {
    this.showDetailModal.set(true);
    this.loadingDetail.set(true);
    this.detailError.set(null);
    this.selectedItem.set(item);
    this.selectedInternalDetail.set(null);
    this.selectedExternalDetail.set(null);

    try {
      if (item.sourceType === 'External' && item.externalRepBaseDocumentId) {
        this.selectedExternalDetail.set(await firstValueFrom(this.api.getExternalBaseDocumentById(item.externalRepBaseDocumentId)));
      } else if (item.sourceType === 'Internal' && item.fiscalDocumentId) {
        this.selectedInternalDetail.set(await firstValueFrom(this.api.getInternalBaseDocumentByFiscalDocumentId(item.fiscalDocumentId)));
      } else {
        this.detailError.set('El documento seleccionado no tiene un identificador válido para cargar detalle.');
      }
    } catch (error) {
      this.detailError.set(extractApiErrorMessage(error, 'No fue posible cargar el detalle del documento base.'));
    } finally {
      this.loadingDetail.set(false);
    }
  }

  protected closeDetail(): void {
    this.showDetailModal.set(false);
    this.loadingDetail.set(false);
    this.detailError.set(null);
    this.selectedItem.set(null);
    this.selectedInternalDetail.set(null);
    this.selectedExternalDetail.set(null);
  }

  protected readonly getDisplayLabel = getDisplayLabel;

  protected formatUtc(value: string | null | undefined): string {
    if (!value) {
      return '—';
    }

    return new Intl.DateTimeFormat('es-MX', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'UTC' }).format(new Date(value));
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

  protected isSelected(item: RepBaseDocumentItemResponse): boolean {
    return this.selectedKeys().includes(this.buildSelectionKey(item.sourceType, item.sourceId));
  }

  protected selectedCount(): number {
    return this.selectedKeys().length;
  }

  protected allVisibleSelected(): boolean {
    const currentItems = this.items();
    return currentItems.length > 0 && currentItems.every((item) => this.isSelected(item));
  }

  protected toggleSelection(item: RepBaseDocumentItemResponse, checked: boolean): void {
    const key = this.buildSelectionKey(item.sourceType, item.sourceId);

    if (checked) {
      this.selectedKeys.set([...new Set([...this.selectedKeys(), key])]);
      return;
    }

    this.selectedKeys.set(this.selectedKeys().filter((value) => value !== key));
  }

  protected toggleSelectAll(checked: boolean): void {
    if (checked) {
      this.selectedKeys.set(this.items().map((item) => this.buildSelectionKey(item.sourceType, item.sourceId)));
      return;
    }

    this.selectedKeys.set([]);
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

  private buildSelectionKey(sourceType: string, sourceId: number): string {
    return `${sourceType}:${sourceId}`;
  }

  private async executeBulkRefresh(mode: string): Promise<void> {
    this.bulkRefreshing.set(true);
    this.bulkRefreshError.set(null);

    try {
      const result = await firstValueFrom(this.api.bulkRefreshBaseDocuments({
        mode,
        documents: this.items()
          .filter((item) => this.selectedKeys().includes(this.buildSelectionKey(item.sourceType, item.sourceId)))
          .map((item) => ({ sourceType: item.sourceType, sourceId: item.sourceId })),
        fromDate: this.fromDate || null,
        toDate: this.toDate || null,
        receiverRfc: this.receiverRfc || null,
        query: this.query || null,
        sourceType: this.sourceType || null,
        validationStatus: this.validationStatus || null,
        eligible: parseBoolean(this.eligibleFilter),
        blocked: parseBoolean(this.blockedFilter),
        alertCode: this.alertCodeFilter || null,
        severity: this.severityFilter || null,
        nextRecommendedAction: this.nextRecommendedActionFilter || null,
        quickView: this.quickViewFilter || null
      }));

      this.bulkRefreshResult.set(result);
      this.selectedKeys.set([]);

      const selected = this.selectedItem();
      if (selected && result.items.some((item) => item.attempted && item.sourceType === selected.sourceType && item.sourceId === selected.sourceId)) {
        await this.openDetail(selected);
      }

      await this.applyFilters();
    } catch (error) {
      this.bulkRefreshError.set(extractApiErrorMessage(error, 'No fue posible ejecutar el refresh masivo unificado.'));
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
