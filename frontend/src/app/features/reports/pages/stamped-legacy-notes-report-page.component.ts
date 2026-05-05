import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { ReportsApiService } from '../infrastructure/reports-api.service';
import { StampedLegacyNoteReportItem, StampedLegacyNotesReportFilters, StampedLegacyNotesReportResponse } from '../models/stamped-legacy-notes-report.models';

@Component({
  selector: 'app-stamped-legacy-notes-report-page',
  template: `
    <section class="page">
      <header class="page-header">
        <div>
          <p class="eyebrow">Reportes</p>
          <h2>Notas timbradas</h2>
          <p class="subtitle">Consulta notas/pedidos legacy timbrados por fecha real de timbrado.</p>
        </div>
        <button type="button" class="secondary" (click)="exportExcel()" [disabled]="exporting() || !!validationError()">
          {{ exporting() ? 'Exportando...' : 'Exportar Excel' }}
        </button>
      </header>

      <section class="card filters-card">
        <div class="filters-grid">
          <label>
            <span>Fecha inicial</span>
            <input type="date" [value]="filters().fromDate" (input)="updateDateFilter('fromDate', $any($event.target).value)" />
          </label>
          <label>
            <span>Fecha final</span>
            <input type="date" [value]="filters().toDate" (input)="updateDateFilter('toDate', $any($event.target).value)" />
          </label>
          <label>
            <span>Receptor/RFC</span>
            <input type="search" [value]="filters().receiverSearch ?? ''" (input)="updateTextFilter('receiverSearch', $any($event.target).value)" placeholder="Cliente o RFC" />
          </label>
          <label>
            <span>UUID</span>
            <input type="search" [value]="filters().uuid ?? ''" (input)="updateTextFilter('uuid', $any($event.target).value)" placeholder="UUID parcial" />
          </label>
          <label>
            <span>Serie</span>
            <input type="search" [value]="filters().series ?? ''" (input)="updateTextFilter('series', $any($event.target).value)" />
          </label>
          <label>
            <span>Folio</span>
            <input type="search" [value]="filters().folio ?? ''" (input)="updateTextFilter('folio', $any($event.target).value)" />
          </label>
          <label>
            <span>noPedido</span>
            <input type="search" [value]="filters().legacyOrderId ?? ''" (input)="updateTextFilter('legacyOrderId', $any($event.target).value)" />
          </label>
          <label>
            <span>refPedido</span>
            <input type="search" [value]="filters().legacyOrderNumber ?? ''" (input)="updateTextFilter('legacyOrderNumber', $any($event.target).value)" />
          </label>
        </div>

        @if (validationError()) {
          <p class="error">{{ validationError() }}</p>
        }

        <div class="actions">
          <button type="button" (click)="search()" [disabled]="loading() || !!validationError()">
            {{ loading() ? 'Buscando...' : 'Buscar' }}
          </button>
          <button type="button" class="secondary" (click)="clear()" [disabled]="loading() || exporting()">Limpiar</button>
        </div>
      </section>

      <section class="card results-card">
        @if (!searched()) {
          <p class="helper">Selecciona un rango de fechas y presiona Buscar.</p>
        } @else if (loading()) {
          <p class="helper">Cargando notas timbradas...</p>
        } @else if (errorMessage()) {
          <p class="error">{{ errorMessage() }}</p>
        } @else if (rows().length === 0) {
          <p class="helper">No se encontraron notas timbradas con los filtros actuales.</p>
        } @else {
          <div class="results-summary">
            <p class="helper">Mostrando {{ rows().length }} de {{ totalCount() }} registros.</p>
            <label class="page-size">
              <span>Filas</span>
              <select [value]="filters().pageSize" (change)="updatePageSize($any($event.target).value)">
                <option value="25">25</option>
                <option value="50">50</option>
                <option value="100">100</option>
                <option value="200">200</option>
              </select>
            </label>
          </div>

          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Fecha timbrado</th>
                  <th>noPedido</th>
                  <th>refPedido</th>
                  <th>Cliente/Receptor</th>
                  <th>RFC receptor</th>
                  <th>Serie</th>
                  <th>Folio</th>
                  <th>UUID</th>
                  <th>Total CFDI</th>
                  <th>Importe nota en CFDI</th>
                  <th>Moneda</th>
                  <th>BillingDocumentId</th>
                  <th>FiscalDocumentId</th>
                  <th>Estatus fiscal</th>
                  <th>Partidas agrupadas</th>
                </tr>
              </thead>
              <tbody>
                @for (row of rows(); track row.fiscalDocumentId + ':' + row.legacyOrderId) {
                  <tr>
                    <td>{{ row.stampedAtLocalText }}</td>
                    <td>{{ row.legacyOrderId }}</td>
                    <td>{{ row.legacyOrderNumber || '—' }}</td>
                    <td>{{ row.receiverName }}</td>
                    <td>{{ row.receiverRfc }}</td>
                    <td>{{ row.series || '—' }}</td>
                    <td>{{ row.folio || '—' }}</td>
                    <td class="uuid">{{ row.uuid }}</td>
                    <td class="numeric">{{ formatMoney(row.cfdiTotal, row.currencyCode) }}</td>
                    <td class="numeric">{{ formatMoney(row.noteAmountInCfdi, row.currencyCode) }}</td>
                    <td>{{ row.currencyCode }}</td>
                    <td>{{ row.billingDocumentId }}</td>
                    <td>{{ row.fiscalDocumentId }}</td>
                    <td>
                      <span class="status">{{ row.fiscalStatus }}</span>
                      @if (row.cancellationStatus) {
                        <span class="muted"> / {{ row.cancellationStatus }}</span>
                      }
                    </td>
                    <td class="numeric">{{ row.itemCount }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <div class="pagination">
            <button type="button" class="secondary" (click)="goToPage(filters().page - 1)" [disabled]="filters().page <= 1 || loading()">Anterior</button>
            <span>Página {{ filters().page }} de {{ totalPages() || 1 }}</span>
            <button type="button" class="secondary" (click)="goToPage(filters().page + 1)" [disabled]="filters().page >= totalPages() || loading()">Siguiente</button>
          </div>
        }
      </section>
    </section>
  `,
  styles: [`
    .page { display:grid; gap:1rem; }
    .page-header { display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    h2 { margin:0.15rem 0; }
    .subtitle, .helper { margin:0; color:#5f6b76; }
    .card { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; }
    .filters-card { display:grid; gap:1rem; }
    .filters-grid { display:grid; grid-template-columns:repeat(4, minmax(0, 1fr)); gap:0.85rem; }
    label { display:grid; gap:0.35rem; font-size:0.88rem; color:#4b5563; }
    input, select { border:1px solid #c9c0af; border-radius:0.7rem; padding:0.62rem 0.7rem; font:inherit; background:#fffdf8; color:#1f2933; }
    button { border:0; border-radius:999px; padding:0.68rem 1rem; background:#284734; color:#fff; font-weight:700; cursor:pointer; }
    button.secondary { border:1px solid #b9ad96; background:#fffaf0; color:#284734; }
    button:disabled { cursor:not-allowed; opacity:0.55; }
    .actions { display:flex; gap:0.75rem; align-items:center; }
    .error { margin:0; color:#7a2020; }
    .results-card { display:grid; gap:0.85rem; }
    .results-summary { display:flex; justify-content:space-between; gap:1rem; align-items:center; }
    .page-size { display:flex; grid-template-columns:auto auto; align-items:center; gap:0.5rem; }
    .table-wrap { overflow:auto; border:1px solid #eadfcb; border-radius:0.85rem; }
    table { width:100%; border-collapse:collapse; min-width:1350px; }
    th, td { padding:0.7rem 0.8rem; border-bottom:1px solid #efe6d6; text-align:left; vertical-align:top; }
    th { background:#f6eedf; color:#3f3527; font-size:0.78rem; text-transform:uppercase; letter-spacing:0.04em; white-space:nowrap; }
    td { color:#24303a; font-size:0.9rem; }
    .uuid { max-width:18rem; word-break:break-all; }
    .numeric { text-align:right; white-space:nowrap; }
    .status { border-radius:999px; background:#e8f0ea; color:#284734; padding:0.18rem 0.5rem; font-size:0.78rem; font-weight:700; }
    .muted { color:#6b7280; font-size:0.82rem; }
    .pagination { display:flex; justify-content:flex-end; align-items:center; gap:0.75rem; color:#4b5563; }
    @media (max-width: 960px) {
      .page-header, .results-summary { display:grid; }
      .filters-grid { grid-template-columns:1fr; }
      .actions, .pagination { flex-wrap:wrap; justify-content:flex-start; }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StampedLegacyNotesReportPageComponent {
  private readonly api = inject(ReportsApiService);
  private readonly feedbackService = inject(FeedbackService);

  protected readonly filters = signal<StampedLegacyNotesReportFilters>(createDefaultFilters());
  protected readonly rows = signal<StampedLegacyNoteReportItem[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly totalPages = signal(0);
  protected readonly loading = signal(false);
  protected readonly exporting = signal(false);
  protected readonly searched = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly validationError = computed(() => validateDateRange(this.filters()));

  protected updateDateFilter(key: 'fromDate' | 'toDate', value: string): void {
    this.filters.update((current) => ({ ...current, [key]: value, page: 1 }));
  }

  protected updateTextFilter(
    key: 'receiverSearch' | 'uuid' | 'series' | 'folio' | 'legacyOrderId' | 'legacyOrderNumber',
    value: string
  ): void {
    this.filters.update((current) => ({ ...current, [key]: value, page: 1 }));
  }

  protected async updatePageSize(value: string): Promise<void> {
    const pageSize = Number(value);
    this.filters.update((current) => ({ ...current, page: 1, pageSize: Number.isFinite(pageSize) ? pageSize : 50 }));
    await this.search(1);
  }

  protected async search(page = 1): Promise<void> {
    const validationError = this.validationError();
    if (validationError) {
      this.feedbackService.show('error', validationError);
      return;
    }

    this.filters.update((current) => ({ ...current, page }));
    this.loading.set(true);
    this.searched.set(true);
    this.errorMessage.set(null);

    try {
      const response = await firstValueFrom(this.api.searchStampedLegacyNotes(this.filters()));
      this.consumeResponse(response);
    } catch (error) {
      const message = extractApiErrorMessage(error, 'No se pudo cargar el reporte de notas timbradas.');
      this.errorMessage.set(message);
      this.feedbackService.show('error', message);
      this.rows.set([]);
      this.totalCount.set(0);
      this.totalPages.set(0);
    } finally {
      this.loading.set(false);
    }
  }

  protected clear(): void {
    this.filters.set(createDefaultFilters());
    this.rows.set([]);
    this.totalCount.set(0);
    this.totalPages.set(0);
    this.errorMessage.set(null);
    this.searched.set(false);
  }

  protected async exportExcel(): Promise<void> {
    const validationError = this.validationError();
    if (validationError) {
      this.feedbackService.show('error', validationError);
      return;
    }

    this.exporting.set(true);
    try {
      const response = await firstValueFrom(this.api.exportStampedLegacyNotes(this.filters()));
      if (!response.body) {
        throw new Error('Empty export response.');
      }

      triggerBlobDownload(response.body, getFileName(response, this.filters()));
    } catch (error) {
      const message = extractApiErrorMessage(error, 'No se pudo exportar el reporte a Excel.');
      this.feedbackService.show('error', message);
    } finally {
      this.exporting.set(false);
    }
  }

  protected async goToPage(page: number): Promise<void> {
    if (page < 1 || page > Math.max(this.totalPages(), 1)) {
      return;
    }

    await this.search(page);
  }

  protected formatMoney(value: number, currencyCode: string): string {
    try {
      return new Intl.NumberFormat('es-MX', {
        style: 'currency',
        currency: currencyCode || 'MXN'
      }).format(value);
    } catch {
      return `${value.toFixed(2)} ${currencyCode || ''}`.trim();
    }
  }

  private consumeResponse(response: StampedLegacyNotesReportResponse): void {
    this.rows.set(response.items);
    this.totalCount.set(response.totalCount);
    this.totalPages.set(response.totalPages);
    this.filters.update((current) => ({
      ...current,
      page: response.page,
      pageSize: response.pageSize
    }));
  }
}

function createDefaultFilters(): StampedLegacyNotesReportFilters {
  const toDate = new Date();
  const fromDate = new Date(toDate);
  fromDate.setDate(toDate.getDate() - 6);

  return {
    fromDate: toDateInputValue(fromDate),
    toDate: toDateInputValue(toDate),
    page: 1,
    pageSize: 50
  };
}

function validateDateRange(filters: StampedLegacyNotesReportFilters): string | null {
  if (!filters.fromDate || !filters.toDate) {
    return 'Selecciona fecha inicial y fecha final.';
  }

  if (filters.fromDate > filters.toDate) {
    return 'La fecha inicial no puede ser mayor a la fecha final.';
  }

  return null;
}

function toDateInputValue(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

function getFileName(response: { headers: { get(name: string): string | null } }, filters: StampedLegacyNotesReportFilters): string {
  const disposition = response.headers.get('content-disposition');
  if (disposition) {
    const utf8Match = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
    if (utf8Match?.[1]) {
      return decodeURIComponent(utf8Match[1].replace(/"/g, ''));
    }

    const fileNameMatch = /filename="?([^"]+)"?/i.exec(disposition);
    if (fileNameMatch?.[1]) {
      return fileNameMatch[1];
    }
  }

  return `reporte-notas-timbradas-${filters.fromDate.replaceAll('-', '')}-${filters.toDate.replaceAll('-', '')}.xlsx`;
}

function triggerBlobDownload(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.rel = 'noopener';
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}
