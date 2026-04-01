import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { getDisplayLabel } from '../../../shared/ui/display-labels';
import { ExternalRepBaseDocumentImportCardComponent } from '../components/external-rep-base-document-import-card.component';
import { PaymentComplementsApiService } from '../infrastructure/payment-complements-api.service';
import { ExternalRepBaseDocumentDetailResponse, ExternalRepBaseDocumentItemResponse } from '../models/payment-complements.models';

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
          <p class="helper">Estos CFDI todavía no reciben pagos ni REP desde la plataforma. En 3B sólo quedan visibles para seguimiento, validación y futura operación.</p>
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

          <div class="actions wide">
            <button type="submit" [disabled]="loading()">{{ loading() ? 'Buscando...' : 'Buscar' }}</button>
            <button type="button" class="secondary" (click)="clearFilters()" [disabled]="loading()">Limpiar filtros</button>
          </div>
        </form>
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
          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Emisión</th>
                  <th>Serie/Folio</th>
                  <th>UUID</th>
                  <th>Emisor</th>
                  <th>Receptor</th>
                  <th>Total</th>
                  <th>Método/Forma</th>
                  <th>Validación</th>
                  <th>SAT</th>
                  <th>Operativo</th>
                  <th>Importado</th>
                  <th>Acción</th>
                </tr>
              </thead>
              <tbody>
                @for (item of items(); track item.externalRepBaseDocumentId) {
                  <tr>
                    <td>{{ formatUtc(item.issuedAtUtc) }}</td>
                    <td>{{ buildSeriesFolio(item.series, item.folio) }}</td>
                    <td>{{ item.uuid }}</td>
                    <td>{{ item.issuerRfc }}</td>
                    <td>{{ item.receiverRfc }}</td>
                    <td>{{ item.total | number:'1.2-2' }}</td>
                    <td>{{ item.paymentMethodSat }} / {{ item.paymentFormSat }}</td>
                    <td>{{ getDisplayLabel(item.validationStatus) }}</td>
                    <td>{{ getDisplayLabel(item.satStatus) }}</td>
                    <td>
                      <span class="status-pill" [class.status-eligible]="item.isEligible" [class.status-blocked]="item.isBlocked" [class.status-neutral]="!item.isEligible && !item.isBlocked">
                        {{ getDisplayLabel(item.operationalStatus) }}
                      </span>
                      <small class="row-reason">{{ item.primaryReasonMessage }}</small>
                    </td>
                    <td>{{ formatUtc(item.importedAtUtc) }}</td>
                    <td><button type="button" class="secondary small" (click)="openDetail(item.externalRepBaseDocumentId)">Ver detalle</button></td>
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
                <p class="eyebrow">Externo</p>
                <h3>Detalle de CFDI importado</h3>
              </div>
              <button type="button" class="secondary" (click)="closeDetail()">Cerrar</button>
            </div>

            @if (loadingDetail()) {
              <p class="helper">Cargando detalle...</p>
            } @else if (detailError()) {
              <p class="error">{{ detailError() }}</p>
            } @else if (selectedDetail(); as detail) {
              <section class="summary-grid">
                <article class="summary-card">
                  <h4>Resumen fiscal</h4>
                  <dl>
                    <div><dt>UUID</dt><dd>{{ detail.uuid }}</dd></div>
                    <div><dt>Serie/Folio</dt><dd>{{ buildSeriesFolio(detail.series, detail.folio) }}</dd></div>
                    <div><dt>Emisión</dt><dd>{{ formatUtc(detail.issuedAtUtc) }}</dd></div>
                    <div><dt>Emisor</dt><dd>{{ detail.issuerRfc }} · {{ detail.issuerLegalName || '—' }}</dd></div>
                    <div><dt>Receptor</dt><dd>{{ detail.receiverRfc }} · {{ detail.receiverLegalName || '—' }}</dd></div>
                    <div><dt>Moneda</dt><dd>{{ detail.currencyCode }}</dd></div>
                    <div><dt>Total</dt><dd>{{ detail.total | number:'1.2-2' }}</dd></div>
                    <div><dt>Método/Forma</dt><dd>{{ detail.paymentMethodSat }} / {{ detail.paymentFormSat }}</dd></div>
                  </dl>
                </article>

                <article class="summary-card">
                  <h4>Seguimiento operativo</h4>
                  <dl>
                    <div><dt>Validación</dt><dd>{{ getDisplayLabel(detail.validationStatus) }}</dd></div>
                    <div><dt>SAT</dt><dd>{{ getDisplayLabel(detail.satStatus) }}</dd></div>
                    <div><dt>Estado operativo</dt><dd>{{ getDisplayLabel(detail.operationalStatus) }}</dd></div>
                    <div><dt>Motivo</dt><dd>{{ detail.primaryReasonMessage }}</dd></div>
                    <div><dt>Código</dt><dd>{{ detail.primaryReasonCode }}</dd></div>
                    <div><dt>Importado</dt><dd>{{ formatUtc(detail.importedAtUtc) }}</dd></div>
                    <div><dt>Usuario</dt><dd>{{ detail.importedByUsername || '—' }}</dd></div>
                    <div><dt>Archivo</dt><dd>{{ detail.sourceFileName }}</dd></div>
                  </dl>
                  <p class="helper">En esta fase sólo hay seguimiento y validación. Los pagos y el REP sobre CFDI externos quedan para Fase 4.</p>
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
    table { width:100%; border-collapse:collapse; min-width:1100px; }
    th, td { padding:0.75rem; border-bottom:1px solid #ece5d7; vertical-align:top; text-align:left; }
    .status-pill { display:inline-flex; align-items:center; border-radius:999px; padding:0.25rem 0.6rem; font-size:0.82rem; font-weight:600; }
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
export class PaymentComplementExternalBaseDocumentsPageComponent {
  private readonly api = inject(PaymentComplementsApiService);

  protected readonly items = signal<ExternalRepBaseDocumentItemResponse[]>([]);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly totalCount = signal(0);
  protected readonly totalPages = signal(0);
  protected readonly showDetailModal = signal(false);
  protected readonly loadingDetail = signal(false);
  protected readonly detailError = signal<string | null>(null);
  protected readonly selectedDetail = signal<ExternalRepBaseDocumentDetailResponse | null>(null);

  protected fromDate = '';
  protected toDate = '';
  protected receiverRfc = '';
  protected query = '';
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
      const response = await firstValueFrom(this.api.searchExternalBaseDocuments({
        page: 1,
        pageSize: 25,
        fromDate: this.fromDate || null,
        toDate: this.toDate || null,
        receiverRfc: this.receiverRfc || null,
        query: this.query || null,
        validationStatus: this.validationStatus || null,
        eligible: parseBoolean(this.eligibleFilter),
        blocked: parseBoolean(this.blockedFilter)
      }));

      this.items.set(response.items);
      this.totalCount.set(response.totalCount);
      this.totalPages.set(response.totalPages);
    } catch (error) {
      this.errorMessage.set(extractApiErrorMessage(error, 'No fue posible consultar los CFDI externos importados.'));
      this.items.set([]);
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
    void this.applyFilters();
  }

  protected async openDetail(externalRepBaseDocumentId: number): Promise<void> {
    this.showDetailModal.set(true);
    this.loadingDetail.set(true);
    this.detailError.set(null);
    this.selectedDetail.set(null);

    try {
      const detail = await firstValueFrom(this.api.getExternalBaseDocumentById(externalRepBaseDocumentId));
      this.selectedDetail.set(detail);
    } catch (error) {
      this.detailError.set(extractApiErrorMessage(error, 'No fue posible cargar el detalle del CFDI externo.'));
    } finally {
      this.loadingDetail.set(false);
    }
  }

  protected closeDetail(): void {
    this.showDetailModal.set(false);
    this.loadingDetail.set(false);
    this.detailError.set(null);
    this.selectedDetail.set(null);
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
