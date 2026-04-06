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
  RepAttentionItemResponse,
  RepOperationalAlertResponse,
  RepOperationalSummaryCountsResponse
} from '../models/payment-complements.models';

@Component({
  selector: 'app-payment-complement-attention-items-page',
  imports: [FormsModule, DecimalPipe],
  template: `
    <section class="page">
      <section class="card">
        <header>
          <p class="eyebrow">Atención</p>
          <h3>Documentos REP que requieren atención</h3>
          <p class="helper">Esta vista concentra alertas notificables y accionables del sprint REP. Es la base operativa para seguimiento hoy y para hooks reales en una siguiente iteración.</p>
        </header>

        <form class="filters" (ngSubmit)="applyFilters()">
          <label><span>Desde</span><input [(ngModel)]="fromDate" name="fromDate" type="date" /></label>
          <label><span>Hasta</span><input [(ngModel)]="toDate" name="toDate" type="date" /></label>
          <label><span>RFC receptor</span><input [(ngModel)]="receiverRfc" name="receiverRfc" /></label>
          <label class="wide"><span>Búsqueda general</span><input [(ngModel)]="query" name="query" placeholder="UUID, RFC, receptor, serie o folio" /></label>
          <label>
            <span>Origen</span>
            <select [(ngModel)]="sourceType" name="sourceType">
              <option value="">Todos</option>
              <option value="Internal">Internal</option>
              <option value="External">External</option>
            </select>
          </label>
          <label>
            <span>Alerta</span>
            <select [(ngModel)]="alertCodeFilter" name="alertCodeFilter">
              <option value="">Todas</option>
              @for (option of attentionAlertOptions; track option) {
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

        @if (summaryCounts().alertCounts.length) {
          <div class="summary-strip">
            <span class="summary-chip critical">Críticas {{ summaryCounts().criticalCount }}</span>
            <span class="summary-chip error">Error {{ summaryCounts().errorCount }}</span>
            @for (item of summaryCounts().alertCounts; track item.code) {
              <span class="summary-chip neutral">{{ getDisplayLabel(item.code) }} ({{ item.count }})</span>
            }
          </div>
        }
      </section>

      <section class="card">
        @if (loading()) {
          <p class="helper">Cargando documentos que requieren atención...</p>
        } @else if (errorMessage()) {
          <p class="error">{{ errorMessage() }}</p>
        } @else if (!items().length) {
          <p class="helper">No hay documentos REP que requieran atención con los filtros actuales.</p>
        } @else {
          <p class="helper">Mostrando {{ items().length }} de {{ totalCount() }} documentos que requieren atención.</p>

          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Origen</th>
                  <th>Documento</th>
                  <th>Receptor</th>
                  <th>Alertas notificables</th>
                  <th>Motivo</th>
                  <th>Acción recomendada</th>
                  <th>Detalle</th>
                </tr>
              </thead>
              <tbody>
                @for (item of items(); track item.sourceType + '-' + item.sourceId) {
                  <tr>
                    <td><span class="source-pill" [class.source-internal]="item.sourceType === 'Internal'" [class.source-external]="item.sourceType === 'External'">{{ getDisplayLabel(item.sourceType) }}</span></td>
                    <td>
                      <strong>{{ item.uuid || buildSeriesFolio(item.series, item.folio) }}</strong>
                      <small class="helper">#{{ item.sourceId }} · {{ formatUtc(item.importedAtUtc || item.issuedAtUtc) }}</small>
                    </td>
                    <td>{{ item.receiverRfc }} · {{ item.receiverLegalName || '—' }}</td>
                    <td>
                      <div class="alert-stack">
                        @for (alert of item.attentionAlerts; track alert.hookKey) {
                          <div class="alert-row">
                            <span class="severity-pill" [class.severity-warning]="alert.severity === 'warning'" [class.severity-error]="alert.severity === 'error'" [class.severity-critical]="alert.severity === 'critical'" [class.severity-info]="alert.severity === 'info'">
                              {{ getDisplayLabel(alert.severity) }}
                            </span>
                            <div>
                              <strong>{{ alert.title }}</strong>
                              <p>{{ alert.message }}</p>
                              <small>Hook {{ alert.hookKey }}</small>
                            </div>
                          </div>
                        }
                      </div>
                    </td>
                    <td>{{ item.primaryReasonMessage }}</td>
                    <td>{{ getRecommendedActionLabel(item.nextRecommendedAction) }}</td>
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
                <p class="eyebrow">Atención</p>
                <h3>Detalle del documento afectado</h3>
              </div>
              <button type="button" class="secondary" (click)="closeDetail()">Cerrar</button>
            </div>

            @if (loadingDetail()) {
              <p class="helper">Cargando detalle...</p>
            } @else if (detailError()) {
              <p class="error">{{ detailError() }}</p>
            } @else if (selectedItem(); as item) {
              <section class="summary-grid">
                <article class="summary-card">
                  <h4>Contexto</h4>
                  <dl>
                    <div><dt>Origen</dt><dd>{{ getDisplayLabel(item.sourceType) }}</dd></div>
                    <div><dt>Documento</dt><dd>{{ item.uuid || buildSeriesFolio(item.series, item.folio) }}</dd></div>
                    <div><dt>Receptor</dt><dd>{{ item.receiverRfc }} · {{ item.receiverLegalName }}</dd></div>
                    <div><dt>Emisión</dt><dd>{{ formatUtc(item.issuedAtUtc) }}</dd></div>
                    <div><dt>Total</dt><dd>{{ item.total | number:'1.2-2' }}</dd></div>
                    <div><dt>Saldo</dt><dd>{{ item.outstandingBalance != null ? (item.outstandingBalance | number:'1.2-2') : '—' }}</dd></div>
                    <div><dt>Operativo</dt><dd>{{ getDisplayLabel(item.operationalStatus) }}</dd></div>
                    <div><dt>Acción recomendada</dt><dd>{{ getRecommendedActionLabel(item.nextRecommendedAction) }}</dd></div>
                  </dl>
                </article>

                <article class="summary-card">
                  <h4>Alertas notificables</h4>
                  <ul class="alert-list">
                    @for (alert of item.attentionAlerts; track alert.hookKey) {
                      <li class="alert-item" [class.alert-critical]="alert.severity === 'critical'" [class.alert-error]="alert.severity === 'error'" [class.alert-warning]="alert.severity === 'warning'" [class.alert-info]="alert.severity === 'info'">
                        <strong>{{ alert.title }}</strong>
                        <p>{{ alert.message }}</p>
                        <small>Hook {{ alert.hookKey }}</small>
                      </li>
                    }
                  </ul>
                </article>

                @if (selectedExternalDetail(); as externalDetail) {
                  <article class="summary-card">
                    <h4>Timeline reciente</h4>
                    @if (!(externalDetail.timeline ?? []).length) {
                      <p class="helper">Sin eventos cronológicos recientes.</p>
                    } @else {
                      <ul class="timeline-list">
                        @for (event of (externalDetail.timeline ?? []).slice(-3); track event.eventType + '-' + event.occurredAtUtc) {
                          <li><strong>{{ event.title }}</strong><p>{{ formatUtc(event.occurredAtUtc) }} · {{ event.description }}</p></li>
                        }
                      </ul>
                    }
                  </article>
                } @else if (selectedInternalDetail(); as internalDetail) {
                  <article class="summary-card">
                    <h4>Timeline reciente</h4>
                    @if (!(internalDetail.timeline ?? []).length) {
                      <p class="helper">Sin eventos cronológicos recientes.</p>
                    } @else {
                      <ul class="timeline-list">
                        @for (event of (internalDetail.timeline ?? []).slice(-3); track event.eventType + '-' + event.occurredAtUtc) {
                          <li><strong>{{ event.title }}</strong><p>{{ formatUtc(event.occurredAtUtc) }} · {{ event.description }}</p></li>
                        }
                      </ul>
                    }
                  </article>
                }
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
    .helper { margin:0; color:#5f6b76; display:block; }
    .filters { display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:0.85rem; }
    .filters label { display:grid; gap:0.35rem; }
    .filters .wide { grid-column:1 / -1; }
    input, select { font:inherit; padding:0.65rem 0.75rem; border:1px solid #d8d1c2; border-radius:0.7rem; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    button.secondary { background:#d8c49b; color:#182533; }
    button.small { padding:0.5rem 0.75rem; font-size:0.9rem; }
    .actions, .summary-strip { display:flex; gap:0.75rem; flex-wrap:wrap; align-items:center; }
    .summary-chip { display:inline-flex; align-items:center; padding:0.25rem 0.65rem; border-radius:999px; font-size:0.8rem; font-weight:700; }
    .summary-chip.critical { background:#fdeaea; color:#8a1f1f; }
    .summary-chip.error { background:#fde8e8; color:#8a1f1f; }
    .summary-chip.neutral { background:#eef1f4; color:#425466; }
    .table-wrap { overflow:auto; }
    table { width:100%; border-collapse:collapse; min-width:1080px; }
    th, td { padding:0.75rem; border-bottom:1px solid #ece5d7; vertical-align:top; text-align:left; }
    .source-pill { display:inline-flex; align-items:center; border-radius:999px; padding:0.25rem 0.6rem; font-size:0.82rem; font-weight:600; }
    .source-internal { background:#eef1f4; color:#182533; }
    .source-external { background:#fff6e5; color:#8a5a00; }
    .alert-stack { display:grid; gap:0.65rem; }
    .alert-row { display:grid; grid-template-columns:auto 1fr; gap:0.65rem; align-items:flex-start; }
    .alert-row p, .alert-row small { margin:0.15rem 0 0; color:#425466; display:block; }
    .severity-pill { display:inline-flex; align-items:center; border-radius:999px; padding:0.2rem 0.55rem; font-size:0.74rem; font-weight:700; }
    .severity-info { background:#eef1f4; color:#425466; }
    .severity-warning { background:#fff3dd; color:#8a5a00; }
    .severity-error { background:#fde8e8; color:#8a1f1f; }
    .severity-critical { background:#f8d7d7; color:#6f1111; }
    .error { margin:0; color:#7a2020; }
    .modal-backdrop { position:fixed; inset:0; background:rgba(8, 15, 25, 0.4); display:grid; place-items:center; padding:1rem; z-index:20; }
    .modal-card { width:min(960px, 100%); max-height:90vh; overflow:auto; background:#fff; border-radius:1rem; padding:1rem; display:grid; gap:1rem; }
    .modal-header { display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; }
    .summary-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(280px, 1fr)); gap:1rem; }
    .summary-card { border:1px solid #ece5d7; border-radius:0.9rem; padding:1rem; display:grid; gap:0.75rem; }
    .alert-list, .timeline-list { list-style:none; padding:0; margin:0; display:grid; gap:0.65rem; }
    .alert-item { border:1px solid #ece5d7; border-radius:0.8rem; padding:0.75rem; }
    .alert-item p, .alert-item small, .timeline-list p { margin:0.2rem 0 0; color:#425466; }
    .alert-warning { background:#fff3dd; color:#8a5a00; }
    .alert-error { background:#fde8e8; color:#8a1f1f; }
    .alert-critical { background:#fdeaea; color:#8a1f1f; }
    .alert-info { background:#eef1f4; color:#425466; }
    dl { display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:0.75rem; margin:0; }
    dt { font-size:0.8rem; color:#5f6b76; }
    dd { margin:0.15rem 0 0; font-weight:600; color:#182533; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PaymentComplementAttentionItemsPageComponent {
  private readonly api = inject(PaymentComplementsApiService);

  protected readonly items = signal<RepAttentionItemResponse[]>([]);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly totalCount = signal(0);
  protected readonly summaryCounts = signal<RepOperationalSummaryCountsResponse>(createEmptySummaryCounts());
  protected readonly showDetailModal = signal(false);
  protected readonly loadingDetail = signal(false);
  protected readonly detailError = signal<string | null>(null);
  protected readonly selectedItem = signal<RepAttentionItemResponse | null>(null);
  protected readonly selectedInternalDetail = signal<InternalRepBaseDocumentDetailResponse | null>(null);
  protected readonly selectedExternalDetail = signal<ExternalRepBaseDocumentDetailResponse | null>(null);

  protected fromDate = '';
  protected toDate = '';
  protected receiverRfc = '';
  protected query = '';
  protected sourceType = '';
  protected alertCodeFilter = '';
  protected severityFilter = '';
  protected nextRecommendedActionFilter = '';

  protected readonly attentionAlertOptions = [
    'RepStampingRejected',
    'RepCancellationRejected',
    'SatValidationUnavailable',
    'BlockedOperation',
    'CancelledBaseDocument'
  ];
  protected readonly severityOptions = ['warning', 'error', 'critical'];
  protected readonly recommendedActionOptions = ['RegisterPayment', 'PrepareRep', 'StampRep', 'RefreshRepStatus', 'CancelRep', 'ViewDetail', 'Blocked', 'NoAction'];
  protected readonly getDisplayLabel = getDisplayLabel;

  constructor() {
    void this.applyFilters();
  }

  protected async applyFilters(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);

    try {
      const response = await firstValueFrom(this.api.searchAttentionItems({
        page: 1,
        pageSize: 25,
        fromDate: this.fromDate || null,
        toDate: this.toDate || null,
        receiverRfc: this.receiverRfc || null,
        query: this.query || null,
        sourceType: this.sourceType || null,
        alertCode: this.alertCodeFilter || null,
        severity: this.severityFilter || null,
        nextRecommendedAction: this.nextRecommendedActionFilter || null
      }));

      this.items.set(response.items);
      this.totalCount.set(response.totalCount);
      this.summaryCounts.set(response.summaryCounts ?? createEmptySummaryCounts());
    } catch (error) {
      this.errorMessage.set(extractApiErrorMessage(error, 'No fue posible consultar los documentos REP que requieren atención.'));
      this.items.set([]);
      this.totalCount.set(0);
      this.summaryCounts.set(createEmptySummaryCounts());
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
    this.alertCodeFilter = '';
    this.severityFilter = '';
    this.nextRecommendedActionFilter = '';
    void this.applyFilters();
  }

  protected async openDetail(item: RepAttentionItemResponse): Promise<void> {
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
      this.detailError.set(extractApiErrorMessage(error, 'No fue posible cargar el detalle del documento que requiere atención.'));
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

  protected formatUtc(value: string | null | undefined): string {
    if (!value) {
      return '—';
    }

    return new Intl.DateTimeFormat('es-MX', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'UTC' }).format(new Date(value));
  }

  protected buildSeriesFolio(series: string | null | undefined, folio: string | null | undefined): string {
    return [series, folio].filter(Boolean).join('-') || '—';
  }

  protected getRecommendedActionLabel(action?: string | null): string {
    return action ? getDisplayLabel(action) : 'Sin acción disponible';
  }
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
