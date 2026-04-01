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
          <p class="helper">La bandeja unificada compone documentos internos y externos bajo un contrato común. Internos siguen siendo operables; externos quedan visibles para seguimiento y futura operación.</p>
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

          <div class="actions wide">
            <button type="submit" [disabled]="loading()">{{ loading() ? 'Buscando...' : 'Buscar' }}</button>
            <button type="button" class="secondary" (click)="clearFilters()" [disabled]="loading()">Limpiar filtros</button>
          </div>
        </form>
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
          <div class="table-wrap">
            <table>
              <thead>
                <tr>
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
                      <small class="row-reason">{{ item.primaryReasonMessage }}</small>
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
                    <div><dt>UUID</dt><dd>{{ externalDetail.uuid }}</dd></div>
                    <div><dt>Serie/Folio</dt><dd>{{ buildSeriesFolio(externalDetail.series, externalDetail.folio) }}</dd></div>
                    <div><dt>Emisión</dt><dd>{{ formatUtc(externalDetail.issuedAtUtc) }}</dd></div>
                    <div><dt>Emisor</dt><dd>{{ externalDetail.issuerRfc }}</dd></div>
                    <div><dt>Receptor</dt><dd>{{ externalDetail.receiverRfc }}</dd></div>
                    <div><dt>Total</dt><dd>{{ externalDetail.total | number:'1.2-2' }}</dd></div>
                    <div><dt>Método/Forma</dt><dd>{{ externalDetail.paymentMethodSat }} / {{ externalDetail.paymentFormSat }}</dd></div>
                  </dl>
                </article>

                <article class="summary-card">
                  <h4>Estado externo</h4>
                  <dl>
                    <div><dt>Validación</dt><dd>{{ getDisplayLabel(externalDetail.validationStatus) }}</dd></div>
                    <div><dt>SAT</dt><dd>{{ getDisplayLabel(externalDetail.satStatus) }}</dd></div>
                    <div><dt>Operativo</dt><dd>{{ getDisplayLabel(externalDetail.operationalStatus) }}</dd></div>
                    <div><dt>Motivo</dt><dd>{{ externalDetail.primaryReasonMessage }}</dd></div>
                    <div><dt>Importado</dt><dd>{{ formatUtc(externalDetail.importedAtUtc) }}</dd></div>
                  </dl>
                  <p class="helper">En la bandeja unificada los externos sólo están disponibles para administración y seguimiento. La operación REP se habilita en Fase 4.</p>
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
                    <div><dt>Pagos</dt><dd>{{ internalDetail.summary.registeredPaymentCount }}</dd></div>
                    <div><dt>REP emitidos</dt><dd>{{ internalDetail.summary.stampedPaymentComplementCount }}</dd></div>
                    <div><dt>REP pendiente</dt><dd>{{ internalDetail.operationalState?.repPendingFlag ? 'Sí' : 'No' }}</dd></div>
                  </dl>
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
    .actions { display:flex; gap:0.75rem; flex-wrap:wrap; }
    .table-wrap { overflow:auto; }
    table { width:100%; border-collapse:collapse; min-width:1120px; }
    th, td { padding:0.75rem; border-bottom:1px solid #ece5d7; vertical-align:top; text-align:left; }
    .source-pill, .status-pill { display:inline-flex; align-items:center; border-radius:999px; padding:0.25rem 0.6rem; font-size:0.82rem; font-weight:600; }
    .source-internal { background:#eef1f4; color:#182533; }
    .source-external { background:#fff6e5; color:#8a5a00; }
    .status-eligible { background:#eef8f1; color:#24573a; }
    .status-blocked { background:#fff6e5; color:#8a5a00; }
    .status-neutral { background:#eef1f4; color:#425466; }
    .row-reason { display:block; color:#5f6b76; margin-top:0.35rem; }
    .error { margin:0; color:#7a2020; }
    .modal-backdrop { position:fixed; inset:0; background:rgba(8, 15, 25, 0.4); display:grid; place-items:center; padding:1rem; z-index:20; }
    .modal-card { width:min(960px, 100%); max-height:90vh; overflow:auto; background:#fff; border-radius:1rem; padding:1rem; display:grid; gap:1rem; }
    .modal-header { display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; }
    .summary-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(280px, 1fr)); gap:1rem; }
    .summary-card { border:1px solid #ece5d7; border-radius:0.9rem; padding:1rem; display:grid; gap:0.75rem; }
    dl { display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:0.75rem; margin:0; }
    dt { font-size:0.8rem; color:#5f6b76; }
    dd { margin:0.15rem 0 0; font-weight:600; color:#182533; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PaymentComplementUnifiedBaseDocumentsPageComponent {
  private readonly api = inject(PaymentComplementsApiService);

  protected readonly items = signal<RepBaseDocumentItemResponse[]>([]);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly totalCount = signal(0);
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
        blocked: parseBoolean(this.blockedFilter)
      }));

      this.items.set(response.items);
      this.totalCount.set(response.totalCount);
    } catch (error) {
      this.errorMessage.set(extractApiErrorMessage(error, 'No fue posible consultar la bandeja REP unificada.'));
      this.items.set([]);
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
