import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { getDisplayLabel } from '../../../shared/ui/display-labels';
import { PaymentComplementsApiService } from '../infrastructure/payment-complements-api.service';
import {
  InternalRepBaseDocumentDetailResponse,
  InternalRepBaseDocumentItemResponse
} from '../models/payment-complements.models';

@Component({
  selector: 'app-payment-complement-base-documents-page',
  imports: [FormsModule, DecimalPipe],
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">Complementos de pago</p>
        <h2>Bandeja operativa de CFDI internos para REP</h2>
        <p class="helper">La unidad operativa es el CFDI base. En esta fase la bandeja solo identifica, clasifica y da contexto; todavía no emite REP desde aquí.</p>
      </header>

      <section class="card">
        <form class="filters" (ngSubmit)="applyFilters()">
          <label><span>Desde</span><input [(ngModel)]="fromDate" name="fromDate" type="date" /></label>
          <label><span>Hasta</span><input [(ngModel)]="toDate" name="toDate" type="date" /></label>
          <label><span>RFC receptor</span><input [(ngModel)]="receiverRfc" name="receiverRfc" /></label>
          <label class="wide"><span>Búsqueda general</span><input [(ngModel)]="query" name="query" placeholder="UUID, RFC, receptor, serie o folio" /></label>
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
            <span>Saldo pendiente</span>
            <select [(ngModel)]="outstandingFilter" name="outstandingFilter">
              <option value="">Todos</option>
              <option value="true">Con saldo</option>
              <option value="false">Sin saldo</option>
            </select>
          </label>
          <label>
            <span>REP emitidos</span>
            <select [(ngModel)]="repEmittedFilter" name="repEmittedFilter">
              <option value="">Todos</option>
              <option value="true">Con REP emitidos</option>
              <option value="false">Sin REP emitidos</option>
            </select>
          </label>

          @if (filtersError()) {
            <p class="error wide">{{ filtersError() }}</p>
          }

          <div class="actions wide">
            <button type="submit" [disabled]="loading()">{{ loading() ? 'Buscando...' : 'Buscar' }}</button>
            <button type="button" class="secondary" (click)="clearFilters()" [disabled]="loading()">Limpiar filtros</button>
          </div>
        </form>
      </section>

      <section class="card">
        @if (loading()) {
          <p class="helper">Cargando documentos base REP...</p>
        } @else if (errorMessage()) {
          <p class="error">{{ errorMessage() }}</p>
        } @else if (!items().length) {
          <p class="helper">No se encontraron CFDI internos para la bandeja REP con los filtros actuales.</p>
        } @else {
          <div class="toolbar">
            <p class="helper">Mostrando {{ items().length }} de {{ totalCount() }} CFDI internos.</p>
            <label class="page-size">
              <span>Tamaño</span>
              <select [ngModel]="pageSize()" (ngModelChange)="changePageSize($event)" name="pageSize">
                <option [ngValue]="10">10</option>
                <option [ngValue]="25">25</option>
                <option [ngValue]="50">50</option>
              </select>
            </label>
          </div>

          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Emisión</th>
                  <th>Serie/Folio</th>
                  <th>UUID</th>
                  <th>RFC receptor</th>
                  <th>Receptor</th>
                  <th>Total</th>
                  <th>Pagado</th>
                  <th>Saldo</th>
                  <th>Fiscal</th>
                  <th>REP</th>
                  <th>Pagos</th>
                  <th>REP emitidos</th>
                  <th>Acción</th>
                </tr>
              </thead>
              <tbody>
                @for (item of items(); track item.fiscalDocumentId) {
                  <tr>
                    <td>{{ formatUtc(item.issuedAtUtc) }}</td>
                    <td>{{ buildSeriesFolio(item) }}</td>
                    <td>{{ item.uuid || '—' }}</td>
                    <td>{{ item.receiverRfc }}</td>
                    <td>{{ item.receiverLegalName }}</td>
                    <td>{{ item.total | number:'1.2-2' }}</td>
                    <td>{{ item.paidTotal | number:'1.2-2' }}</td>
                    <td>{{ item.outstandingBalance | number:'1.2-2' }}</td>
                    <td>{{ getDisplayLabel(item.fiscalStatus) }}</td>
                    <td>
                      <span class="status-pill" [class.status-eligible]="item.isEligible" [class.status-blocked]="item.isBlocked" [class.status-ineligible]="!item.isEligible && !item.isBlocked">
                        {{ getDisplayLabel(item.repOperationalStatus) }}
                      </span>
                      <small class="row-reason">{{ item.eligibilityReason }}</small>
                    </td>
                    <td>{{ item.registeredPaymentCount }}</td>
                    <td>{{ item.stampedPaymentComplementCount }}</td>
                    <td><button type="button" class="secondary small" (click)="openDetailModal(item)">Ver contexto</button></td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <div class="pagination">
            <button type="button" class="secondary" (click)="goToPage(page() - 1)" [disabled]="page() <= 1 || loading()">Anterior</button>
            <span>Página {{ page() }} de {{ totalPages() || 1 }}</span>
            <button type="button" class="secondary" (click)="goToPage(page() + 1)" [disabled]="page() >= totalPages() || loading()">Siguiente</button>
          </div>
        }
      </section>

      @if (showDetailModal()) {
        <section class="modal-backdrop" (click)="closeDetailModal()">
          <section class="modal-card detail-modal" (click)="$event.stopPropagation()">
            <div class="modal-header">
              <div>
                <p class="eyebrow">Complementos de pago</p>
                <h3>Contexto del CFDI base</h3>
              </div>
              <button type="button" class="secondary" (click)="closeDetailModal()">Cerrar</button>
            </div>

            @if (loadingDetail()) {
              <p class="helper">Cargando contexto del CFDI...</p>
            } @else if (detailError()) {
              <p class="error">{{ detailError() }}</p>
            } @else if (selectedDetail(); as detail) {
              <section class="summary-grid">
                <article class="summary-card">
                  <h4>Documento base</h4>
                  <dl>
                    <div><dt>FiscalDocumentId</dt><dd>{{ detail.summary.fiscalDocumentId }}</dd></div>
                    <div><dt>BillingDocumentId</dt><dd>{{ detail.summary.billingDocumentId ?? '—' }}</dd></div>
                    <div><dt>SalesOrderId</dt><dd>{{ detail.summary.salesOrderId ?? '—' }}</dd></div>
                    <div><dt>AR Invoice</dt><dd>{{ detail.summary.accountsReceivableInvoiceId ?? '—' }}</dd></div>
                    <div><dt>UUID</dt><dd>{{ detail.summary.uuid || '—' }}</dd></div>
                    <div><dt>Serie/Folio</dt><dd>{{ buildSeriesFolio(detail.summary) }}</dd></div>
                    <div><dt>Receptor</dt><dd>{{ detail.summary.receiverRfc }} · {{ detail.summary.receiverLegalName }}</dd></div>
                    <div><dt>Estado fiscal</dt><dd>{{ getDisplayLabel(detail.summary.fiscalStatus) }}</dd></div>
                    <div><dt>Estado REP</dt><dd>{{ getDisplayLabel(detail.summary.repOperationalStatus) }}</dd></div>
                    <div><dt>Motivo</dt><dd>{{ detail.summary.eligibilityReason }}</dd></div>
                  </dl>
                </article>

                <article class="summary-card">
                  <h4>Saldos y seguimiento</h4>
                  <dl>
                    <div><dt>Método</dt><dd>{{ detail.summary.paymentMethodSat }}</dd></div>
                    <div><dt>Forma</dt><dd>{{ detail.summary.paymentFormSat }}</dd></div>
                    <div><dt>Total</dt><dd>{{ detail.summary.total | number:'1.2-2' }}</dd></div>
                    <div><dt>Pagado</dt><dd>{{ detail.summary.paidTotal | number:'1.2-2' }}</dd></div>
                    <div><dt>Saldo</dt><dd>{{ detail.summary.outstandingBalance | number:'1.2-2' }}</dd></div>
                    <div><dt>Pagos registrados</dt><dd>{{ detail.summary.registeredPaymentCount }}</dd></div>
                    <div><dt>REP ligados</dt><dd>{{ detail.summary.paymentComplementCount }}</dd></div>
                    <div><dt>REP emitidos</dt><dd>{{ detail.summary.stampedPaymentComplementCount }}</dd></div>
                    <div><dt>Estatus CxC</dt><dd>{{ detail.summary.accountsReceivableStatus ? getDisplayLabel(detail.summary.accountsReceivableStatus) : '—' }}</dd></div>
                  </dl>
                </article>
              </section>

              <section class="nested-card">
                <h4>Aplicaciones de pago</h4>
                @if (!detail.paymentApplications.length) {
                  <p class="helper">Todavía no hay pagos aplicados a este CFDI dentro del sistema.</p>
                } @else {
                  <div class="table-wrap">
                    <table>
                      <thead>
                        <tr>
                          <th>PaymentId</th>
                          <th>Fecha</th>
                          <th>Forma</th>
                          <th>Parcialidad</th>
                          <th>Aplicado</th>
                          <th>Saldo anterior</th>
                          <th>Saldo nuevo</th>
                          <th>Referencia</th>
                        </tr>
                      </thead>
                      <tbody>
                        @for (application of detail.paymentApplications; track application.accountsReceivablePaymentId + '-' + application.applicationSequence) {
                          <tr>
                            <td>{{ application.accountsReceivablePaymentId }}</td>
                            <td>{{ formatUtc(application.paymentDateUtc) }}</td>
                            <td>{{ application.paymentFormSat }}</td>
                            <td>{{ application.applicationSequence }}</td>
                            <td>{{ application.appliedAmount | number:'1.2-2' }}</td>
                            <td>{{ application.previousBalance | number:'1.2-2' }}</td>
                            <td>{{ application.newBalance | number:'1.2-2' }}</td>
                            <td>{{ application.reference || '—' }}</td>
                          </tr>
                        }
                      </tbody>
                    </table>
                  </div>
                }
              </section>

              <section class="nested-card">
                <h4>REP relacionados</h4>
                @if (!detail.paymentComplements.length) {
                  <p class="helper">Aún no hay REP ligados a este CFDI base.</p>
                } @else {
                  <div class="table-wrap">
                    <table>
                      <thead>
                        <tr>
                          <th>Complemento</th>
                          <th>PaymentId</th>
                          <th>Estado</th>
                          <th>UUID</th>
                          <th>Fecha pago</th>
                          <th>Emisión</th>
                          <th>Timbrado</th>
                          <th>Monto</th>
                        </tr>
                      </thead>
                      <tbody>
                        @for (complement of detail.paymentComplements; track complement.paymentComplementId) {
                          <tr>
                            <td>{{ complement.paymentComplementId }}</td>
                            <td>{{ complement.accountsReceivablePaymentId }}</td>
                            <td>{{ getDisplayLabel(complement.status) }}</td>
                            <td>{{ complement.uuid || '—' }}</td>
                            <td>{{ formatUtc(complement.paymentDateUtc) }}</td>
                            <td>{{ complement.issuedAtUtc ? formatUtc(complement.issuedAtUtc) : '—' }}</td>
                            <td>{{ complement.stampedAtUtc ? formatUtc(complement.stampedAtUtc) : '—' }}</td>
                            <td>{{ complement.paidAmount | number:'1.2-2' }}</td>
                          </tr>
                        }
                      </tbody>
                    </table>
                  </div>
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
    .card, .summary-card, .nested-card { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    .filters { display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:1rem; align-items:end; }
    .wide { grid-column:1 / -1; }
    label { display:grid; gap:0.35rem; }
    input, select, button { font:inherit; }
    input, select { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    button.secondary { background:#d8c49b; color:#182533; }
    button.small { padding:0.45rem 0.7rem; font-size:0.88rem; }
    button:disabled { opacity:0.6; cursor:not-allowed; }
    .actions, .toolbar, .pagination { display:flex; flex-wrap:wrap; gap:0.75rem; align-items:center; }
    .toolbar, .pagination { justify-content:space-between; }
    .table-wrap { overflow:auto; }
    table { width:100%; border-collapse:collapse; }
    th, td { text-align:left; padding:0.75rem 0.5rem; border-bottom:1px solid #ece5d7; vertical-align:top; }
    .helper { margin:0; color:#5f6b76; }
    .error { margin:0; color:#7a2020; }
    .status-pill { display:inline-flex; align-items:center; border-radius:999px; padding:0.25rem 0.65rem; font-size:0.82rem; font-weight:600; }
    .status-eligible { background:#e5f6eb; color:#1b6b3a; }
    .status-blocked { background:#fdeaea; color:#8a1f1f; }
    .status-ineligible { background:#f4efe4; color:#6f5b22; }
    .row-reason { display:block; margin-top:0.35rem; color:#5f6b76; }
    .modal-backdrop { position:fixed; inset:0; background:rgba(24, 37, 51, 0.42); display:grid; place-items:center; padding:1rem; z-index:50; }
    .modal-card { width:min(1180px, 100%); max-height:calc(100vh - 2rem); overflow:auto; border:1px solid #d8d1c2; border-radius:1rem; background:#fff; padding:1rem; display:grid; gap:1rem; box-shadow:0 24px 60px rgba(24, 37, 51, 0.24); }
    .detail-modal { align-content:start; }
    .modal-header { display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; }
    .summary-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(300px, 1fr)); gap:1rem; }
    dl { display:grid; gap:0.5rem; margin:0; }
    dl div { display:grid; gap:0.15rem; }
    dt { font-size:0.8rem; color:#5f6b76; }
    dd { margin:0; font-weight:600; color:#182533; }
    @media (max-width: 720px) {
      .toolbar, .pagination, .modal-header { flex-direction:column; align-items:stretch; }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PaymentComplementBaseDocumentsPageComponent {
  private readonly api = inject(PaymentComplementsApiService);
  private readonly feedbackService = inject(FeedbackService);
  protected readonly getDisplayLabel = getDisplayLabel;

  protected fromDate = '';
  protected toDate = '';
  protected receiverRfc = '';
  protected query = '';
  protected eligibleFilter = '';
  protected blockedFilter = '';
  protected outstandingFilter = '';
  protected repEmittedFilter = '';
  protected readonly page = signal(1);
  protected readonly pageSize = signal(25);
  protected readonly totalCount = signal(0);
  protected readonly totalPages = signal(0);
  protected readonly items = signal<InternalRepBaseDocumentItemResponse[]>([]);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly filtersError = signal<string | null>(null);
  protected readonly showDetailModal = signal(false);
  protected readonly selectedDetail = signal<InternalRepBaseDocumentDetailResponse | null>(null);
  protected readonly loadingDetail = signal(false);
  protected readonly detailError = signal<string | null>(null);

  constructor() {
    void this.load();
  }

  protected async applyFilters(): Promise<void> {
    if (this.fromDate && this.toDate && this.fromDate > this.toDate) {
      this.filtersError.set('La fecha inicial no puede ser mayor a la fecha final.');
      return;
    }

    this.filtersError.set(null);
    this.page.set(1);
    await this.load();
  }

  protected async clearFilters(): Promise<void> {
    this.fromDate = '';
    this.toDate = '';
    this.receiverRfc = '';
    this.query = '';
    this.eligibleFilter = '';
    this.blockedFilter = '';
    this.outstandingFilter = '';
    this.repEmittedFilter = '';
    this.filtersError.set(null);
    this.page.set(1);
    this.pageSize.set(25);
    await this.load();
  }

  protected async goToPage(page: number): Promise<void> {
    if (page < 1 || page > this.totalPages() || page === this.page()) {
      return;
    }

    this.page.set(page);
    await this.load();
  }

  protected async changePageSize(value: number): Promise<void> {
    this.pageSize.set(Number(value) || 25);
    this.page.set(1);
    await this.load();
  }

  protected async openDetailModal(item: InternalRepBaseDocumentItemResponse): Promise<void> {
    this.showDetailModal.set(true);
    this.loadingDetail.set(true);
    this.detailError.set(null);
    this.selectedDetail.set(null);

    try {
      this.selectedDetail.set(await firstValueFrom(this.api.getInternalBaseDocumentByFiscalDocumentId(item.fiscalDocumentId)));
    } catch (error) {
      const message = extractApiErrorMessage(error, 'No fue posible cargar el contexto del CFDI base.');
      this.detailError.set(message);
      this.feedbackService.show('error', message);
    } finally {
      this.loadingDetail.set(false);
    }
  }

  protected closeDetailModal(): void {
    this.showDetailModal.set(false);
    this.selectedDetail.set(null);
    this.loadingDetail.set(false);
    this.detailError.set(null);
  }

  protected buildSeriesFolio(item: InternalRepBaseDocumentItemResponse): string {
    const series = item.series?.trim();
    const folio = item.folio?.trim();

    if (series && folio) {
      return `${series}-${folio}`;
    }

    return series || folio || '—';
  }

  protected formatUtc(value: string): string {
    return new Date(value).toLocaleString('es-MX', {
      dateStyle: 'short',
      timeStyle: 'short',
      timeZone: 'UTC'
    });
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);

    try {
      const response = await firstValueFrom(this.api.searchInternalBaseDocuments({
        page: this.page(),
        pageSize: this.pageSize(),
        fromDate: this.fromDate || null,
        toDate: this.toDate || null,
        receiverRfc: this.receiverRfc || null,
        query: this.query || null,
        eligible: parseBooleanFilter(this.eligibleFilter),
        blocked: parseBooleanFilter(this.blockedFilter),
        withOutstandingBalance: parseBooleanFilter(this.outstandingFilter),
        hasRepEmitted: parseBooleanFilter(this.repEmittedFilter)
      }));

      this.items.set(response.items);
      this.totalCount.set(response.totalCount);
      this.totalPages.set(response.totalPages);
      this.page.set(response.page);
      this.pageSize.set(response.pageSize);
    } catch (error) {
      this.errorMessage.set(extractApiErrorMessage(error, 'No fue posible cargar la bandeja REP interna.'));
    } finally {
      this.loading.set(false);
    }
  }
}

function parseBooleanFilter(value: string): boolean | null {
  if (value === 'true') {
    return true;
  }

  if (value === 'false') {
    return false;
  }

  return null;
}
