import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { PermissionService } from '../../../core/auth/permission.service';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import {
  StatusBadgeComponent,
  StatusBadgeTone,
} from '../../../shared/components/status-badge.component';
import { PendingFiscalDocumentsApiService } from '../infrastructure/pending-fiscal-documents-api.service';
import {
  PendingFiscalDocumentListItemResponse,
  PendingFiscalDocumentSort,
  PendingFiscalDocumentWorkFilter,
} from '../models/pending-fiscal-documents.models';

@Component({
  selector: 'app-pending-fiscal-documents-page',
  imports: [FormsModule, DecimalPipe, RouterLink, StatusBadgeComponent],
  template: `
    <section class="page">
      <header class="page-header">
        <div>
          <p class="eyebrow">Documentos fiscales</p>
          <h2>Documentos abiertos pendientes de timbrar</h2>
          <p class="intro">
            Localiza documentos que todavía pueden recibir órdenes, continuar su preparación o
            corregirse antes del timbrado.
          </p>
        </div>
        @if (!permissionService.isReadOnlyAuditor()) {
          <a class="button-link secondary" routerLink="/app/fiscal-documents">
            Búsqueda manual
          </a>
        } @else {
          <span class="read-only-label">Vista de solo consulta</span>
        }
      </header>

      <section class="summary-card" aria-live="polite">
        <div>
          <span>Trabajo abierto</span>
          <strong>{{ totalCount() }}</strong>
          <small>documento(s) pendiente(s)</small>
        </div>
        <p>
          Los CFDI timbrados, cancelados y las solicitudes de timbrado en proceso no se muestran en
          esta bandeja.
        </p>
      </section>

      <section class="card">
        <form class="filters" (ngSubmit)="applyFilters()">
          <label class="search-field">
            <span>Buscar documento</span>
            <input
              [(ngModel)]="query"
              name="query"
              placeholder="Cliente, RFC, orden, documento de facturación o fiscal"
            />
          </label>

          <label>
            <span>Estado de trabajo</span>
            <select [(ngModel)]="workStatus" name="workStatus">
              <option value="All">Todos los abiertos</option>
              <option value="PendingPreparation">Pendientes de preparar</option>
              <option value="ReadyForStamping">Listos para timbrar</option>
              <option value="RequiresAttention">Requieren atención</option>
            </select>
          </label>

          <label>
            <span>Ordenar por</span>
            <select [(ngModel)]="sort" name="sort">
              <option value="LastActivityDesc">Prioridad y último movimiento</option>
              <option value="OldestFirst">Más antiguos primero</option>
              <option value="TotalDesc">Mayor importe</option>
            </select>
          </label>

          <div class="filter-actions">
            <button type="submit" [disabled]="loading()">
              {{ loading() ? 'Consultando...' : 'Buscar' }}
            </button>
            <button type="button" class="secondary" (click)="clearFilters()" [disabled]="loading()">
              Limpiar
            </button>
            <button type="button" class="secondary" (click)="reload()" [disabled]="loading()">
              Actualizar
            </button>
          </div>
        </form>
      </section>

      <section class="card inbox-card">
        @if (loading()) {
          <div class="empty-state">
            <strong>Cargando documentos abiertos...</strong>
            <span>Se está consultando el estado vigente de facturación y timbrado.</span>
          </div>
        } @else if (errorMessage()) {
          <div class="empty-state error-state">
            <strong>No fue posible cargar la bandeja</strong>
            <span>{{ errorMessage() }}</span>
            <button type="button" (click)="reload()">Reintentar</button>
          </div>
        } @else if (!items().length) {
          <div class="empty-state">
            <strong>No hay documentos abiertos con estos filtros</strong>
            <span>Prueba una búsqueda distinta o limpia los filtros para consultar toda la bandeja.</span>
          </div>
        } @else {
          <div class="toolbar">
            <p>
              Mostrando <strong>{{ items().length }}</strong> de
              <strong>{{ totalCount() }}</strong> documento(s).
            </p>
            <label class="page-size">
              <span>Filas</span>
              <select
                [ngModel]="pageSize()"
                (ngModelChange)="changePageSize($event)"
                name="pageSize"
              >
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
                  <th>Documento</th>
                  <th>Cliente / receptor</th>
                  <th>Órdenes incluidas</th>
                  <th>Último movimiento</th>
                  <th>Antigüedad</th>
                  <th>Total actual</th>
                  <th>Estado</th>
                  <th>Acción</th>
                </tr>
              </thead>
              <tbody>
                @for (item of items(); track item.billingDocumentId) {
                  <tr [class.attention-row]="item.requiresAttention">
                    <td>
                      <strong>Facturación #{{ item.billingDocumentId }}</strong>
                      @if (item.fiscalDocumentId) {
                        <span>Fiscal #{{ item.fiscalDocumentId }}</span>
                        @if (buildFiscalReference(item)) {
                          <small>{{ buildFiscalReference(item) }}</small>
                        }
                      } @else {
                        <span>Sin documento fiscal preparado</span>
                      }
                    </td>
                    <td>
                      <strong>{{ item.receiverName || 'Cliente sin nombre' }}</strong>
                      <span>{{ item.receiverRfc || 'RFC no disponible' }}</span>
                    </td>
                    <td>
                      <strong>
                        {{ item.associatedOrderCount }} orden(es) · {{ item.itemCount }} producto(s)
                      </strong>
                      <span>{{ formatOrderReferences(item) }}</span>
                      @if (item.associatedOrderCount > item.orderReferences.length) {
                        <small>
                          +{{ item.associatedOrderCount - item.orderReferences.length }} orden(es) más
                        </small>
                      }
                    </td>
                    <td>
                      <strong>{{ formatDateTime(item.lastActivityAtUtc) }}</strong>
                      <span>Creado {{ formatDate(item.createdAtUtc) }}</span>
                      @if (item.fiscalPreparedAtUtc) {
                        <small>Fiscal preparado {{ formatDate(item.fiscalPreparedAtUtc) }}</small>
                      }
                    </td>
                    <td>
                      <span class="age-pill" [class]="ageClass(item.lastActivityAtUtc)">
                        {{ activityAgeLabel(item.lastActivityAtUtc) }}
                      </span>
                    </td>
                    <td>
                      <strong>{{ item.total | number: '1.2-2' }} {{ item.currencyCode }}</strong>
                    </td>
                    <td>
                      <app-status-badge
                        [label]="item.workStatusLabel"
                        [tone]="workStatusTone(item)"
                      />
                      @if (item.paymentMethodSat) {
                        <small>
                          {{ item.paymentMethodSat }}
                          @if (item.paymentFormSat) {
                            · {{ item.paymentFormSat }}
                          }
                        </small>
                      }
                    </td>
                    <td>
                      @if (permissionService.isReadOnlyAuditor()) {
                        <span class="read-only-label">Solo consulta</span>
                      } @else if (item.fiscalDocumentId) {
                        <a
                          class="button-link small"
                          [routerLink]="['/app/fiscal-documents', item.fiscalDocumentId]"
                        >
                          Continuar
                        </a>
                      } @else {
                        <a
                          class="button-link small"
                          [routerLink]="['/app/fiscal-documents']"
                          [queryParams]="{ billingDocumentId: item.billingDocumentId }"
                        >
                          Continuar
                        </a>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <div class="pagination">
            <button
              type="button"
              class="secondary"
              (click)="goToPage(page() - 1)"
              [disabled]="page() <= 1 || loading()"
            >
              Anterior
            </button>
            <span>Página {{ page() }} de {{ totalPages() || 1 }}</span>
            <button
              type="button"
              class="secondary"
              (click)="goToPage(page() + 1)"
              [disabled]="page() >= totalPages() || loading()"
            >
              Siguiente
            </button>
          </div>
        }
      </section>
    </section>
  `,
  styles: [
    `
      .page {
        display: grid;
        gap: 1rem;
      }

      .page-header {
        display: flex;
        justify-content: space-between;
        gap: 1.5rem;
        align-items: flex-start;
      }

      .eyebrow {
        margin: 0;
        text-transform: uppercase;
        letter-spacing: 0.12em;
        font-size: 0.72rem;
        color: #8a6a32;
      }

      h2 {
        margin: 0.3rem 0 0;
        color: #182533;
      }

      .intro {
        max-width: 760px;
        margin: 0.55rem 0 0;
        color: #5f6b76;
        line-height: 1.55;
      }

      .card,
      .summary-card {
        border: 1px solid #d8d1c2;
        border-radius: 1rem;
        background: #fff;
      }

      .card {
        padding: 1rem;
      }

      .summary-card {
        display: grid;
        grid-template-columns: minmax(180px, 0.3fr) minmax(280px, 1fr);
        gap: 1.25rem;
        align-items: center;
        padding: 1rem 1.15rem;
        background: linear-gradient(135deg, #182533 0%, #253c52 100%);
        color: #fff;
      }

      .summary-card div {
        display: grid;
        grid-template-columns: auto 1fr;
        column-gap: 0.75rem;
        align-items: baseline;
      }

      .summary-card span,
      .summary-card small {
        grid-column: 1 / -1;
        color: #d8c49b;
      }

      .summary-card strong {
        font-size: 2rem;
      }

      .summary-card p {
        margin: 0;
        color: #e8edf2;
        line-height: 1.5;
      }

      .filters {
        display: grid;
        grid-template-columns: minmax(260px, 1.4fr) repeat(2, minmax(190px, 0.7fr));
        gap: 0.85rem;
        align-items: end;
      }

      label {
        display: grid;
        gap: 0.35rem;
      }

      label span {
        font-size: 0.82rem;
        color: #465565;
      }

      input,
      select,
      button,
      a {
        font: inherit;
      }

      input,
      select {
        width: 100%;
        box-sizing: border-box;
        border: 1px solid #c9d1da;
        border-radius: 0.8rem;
        padding: 0.72rem 0.85rem;
        background: #fff;
        color: #182533;
      }

      .filter-actions {
        grid-column: 1 / -1;
        display: flex;
        flex-wrap: wrap;
        gap: 0.65rem;
      }

      button,
      .button-link {
        border: none;
        border-radius: 0.8rem;
        padding: 0.72rem 1rem;
        background: #182533;
        color: #fff;
        cursor: pointer;
        text-decoration: none;
        display: inline-flex;
        align-items: center;
        justify-content: center;
        width: fit-content;
      }

      button.secondary,
      .button-link.secondary {
        background: #d8c49b;
        color: #182533;
      }

      button.small,
      .button-link.small {
        padding: 0.48rem 0.75rem;
        font-size: 0.88rem;
      }

      button:disabled {
        opacity: 0.58;
        cursor: wait;
      }

      .read-only-label {
        display: inline-flex;
        width: fit-content;
        border-radius: 999px;
        padding: 0.36rem 0.62rem;
        background: #eef1f4;
        color: #536170;
        font-size: 0.78rem;
        font-weight: 600;
      }

      .inbox-card {
        min-width: 0;
      }

      .toolbar,
      .pagination {
        display: flex;
        justify-content: space-between;
        gap: 1rem;
        align-items: center;
      }

      .toolbar {
        margin-bottom: 0.75rem;
      }

      .toolbar p {
        margin: 0;
        color: #5f6b76;
      }

      .page-size {
        display: flex;
        align-items: center;
        gap: 0.5rem;
      }

      .page-size select {
        width: auto;
      }

      .table-wrap {
        overflow: auto;
        border: 1px solid #ece5d7;
        border-radius: 0.9rem;
      }

      table {
        width: 100%;
        min-width: 1120px;
        border-collapse: collapse;
      }

      th,
      td {
        padding: 0.8rem 0.7rem;
        border-bottom: 1px solid #ece5d7;
        text-align: left;
        vertical-align: top;
      }

      th {
        background: #f6f2e9;
        color: #51452f;
        font-size: 0.78rem;
        text-transform: uppercase;
        letter-spacing: 0.04em;
      }

      td {
        color: #253443;
        font-size: 0.9rem;
      }

      td strong,
      td span,
      td small {
        display: block;
      }

      td span,
      td small {
        margin-top: 0.18rem;
        color: #5f6b76;
      }

      tr.attention-row td {
        background: #fff9ed;
      }

      tr:last-child td {
        border-bottom: none;
      }

      .age-pill {
        display: inline-flex;
        width: fit-content;
        border-radius: 999px;
        padding: 0.32rem 0.58rem;
        background: #eef1f4;
        color: #344353;
        font-size: 0.78rem;
        font-weight: 600;
      }

      .age-pill.age-warning {
        background: #fff1cf;
        color: #74510f;
      }

      .age-pill.age-critical {
        background: #fde1df;
        color: #84251f;
      }

      .pagination {
        margin-top: 0.9rem;
      }

      .empty-state {
        min-height: 180px;
        display: grid;
        place-items: center;
        align-content: center;
        gap: 0.45rem;
        text-align: center;
        color: #5f6b76;
      }

      .empty-state strong {
        color: #182533;
      }

      .error-state span {
        color: #7a2020;
      }

      @media (max-width: 900px) {
        .page-header,
        .toolbar,
        .pagination {
          flex-direction: column;
          align-items: stretch;
        }

        .summary-card,
        .filters {
          grid-template-columns: 1fr;
        }

        .filter-actions {
          grid-column: auto;
        }
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PendingFiscalDocumentsPageComponent {
  private readonly api = inject(PendingFiscalDocumentsApiService);
  protected readonly permissionService = inject(PermissionService);

  protected query = '';
  protected workStatus: PendingFiscalDocumentWorkFilter = 'All';
  protected sort: PendingFiscalDocumentSort = 'LastActivityDesc';

  protected readonly page = signal(1);
  protected readonly pageSize = signal(25);
  protected readonly totalCount = signal(0);
  protected readonly totalPages = signal(0);
  protected readonly items = signal<PendingFiscalDocumentListItemResponse[]>([]);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  constructor() {
    void this.load();
  }

  protected async applyFilters(): Promise<void> {
    this.page.set(1);
    await this.load();
  }

  protected async clearFilters(): Promise<void> {
    this.query = '';
    this.workStatus = 'All';
    this.sort = 'LastActivityDesc';
    this.page.set(1);
    this.pageSize.set(25);
    await this.load();
  }

  protected async reload(): Promise<void> {
    await this.load();
  }

  protected async changePageSize(value: number): Promise<void> {
    this.pageSize.set(Number(value) || 25);
    this.page.set(1);
    await this.load();
  }

  protected async goToPage(page: number): Promise<void> {
    if (page < 1 || page > this.totalPages() || page === this.page()) {
      return;
    }

    this.page.set(page);
    await this.load();
  }

  protected buildFiscalReference(item: PendingFiscalDocumentListItemResponse): string {
    const series = item.series?.trim() ?? '';
    const folio = item.folio?.trim() ?? '';
    return [series, folio].filter((value) => !!value).join('-');
  }

  protected formatOrderReferences(item: PendingFiscalDocumentListItemResponse): string {
    return item.orderReferences.length
      ? item.orderReferences.join(', ')
      : 'Sin referencias de orden disponibles';
  }

  protected formatDateTime(value: string): string {
    return new Date(value).toLocaleString('es-MX', {
      dateStyle: 'short',
      timeStyle: 'short',
    });
  }

  protected formatDate(value: string): string {
    return new Date(value).toLocaleDateString('es-MX');
  }

  protected activityAgeLabel(value: string): string {
    const days = this.activityAgeDays(value);
    if (days <= 0) {
      return 'Hoy';
    }

    if (days === 1) {
      return 'Hace 1 día';
    }

    return `Hace ${days} días`;
  }

  protected ageClass(value: string): string {
    const days = this.activityAgeDays(value);
    if (days >= 30) {
      return 'age-critical';
    }

    if (days >= 7) {
      return 'age-warning';
    }

    return '';
  }

  protected workStatusTone(item: PendingFiscalDocumentListItemResponse): StatusBadgeTone {
    switch (item.workStatus) {
      case 'ReadyForStamping':
        return 'success';
      case 'StampingRejected':
        return 'danger';
      case 'NeedsRegeneration':
        return 'warning';
      case 'PendingPreparation':
      case 'InPreparation':
        return 'info';
      default:
        return 'neutral';
    }
  }

  private activityAgeDays(value: string): number {
    const timestamp = new Date(value).getTime();
    if (!Number.isFinite(timestamp)) {
      return 0;
    }

    return Math.max(0, Math.floor((Date.now() - timestamp) / 86_400_000));
  }

  private async load(): Promise<void> {
    if (this.loading()) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    try {
      const response = await firstValueFrom(
        this.api.search({
          page: this.page(),
          pageSize: this.pageSize(),
          query: this.query || null,
          workStatus: this.workStatus,
          sort: this.sort,
        }),
      );
      this.items.set(response.items);
      this.page.set(response.page);
      this.pageSize.set(response.pageSize);
      this.totalCount.set(response.totalCount);
      this.totalPages.set(response.totalPages);
    } catch (error) {
      this.items.set([]);
      this.totalCount.set(0);
      this.totalPages.set(0);
      this.errorMessage.set(
        extractApiErrorMessage(error, 'No fue posible cargar los documentos pendientes de timbrar.'),
      );
    } finally {
      this.loading.set(false);
    }
  }
}
