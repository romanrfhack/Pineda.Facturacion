import { DecimalPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { FiscalDocumentsApiService } from '../infrastructure/fiscal-documents-api.service';
import {
  BillingDocumentAssociatedOrderResponse,
  BillingDocumentLookupItemResponse,
  BillingDocumentLookupResponse,
  FiscalDocumentResponse,
} from '../models/fiscal-documents.models';

interface FiscalDocumentSourceOrderViewModel {
  salesOrderId: number;
  legacyOrderId: string;
  customerName: string;
  isPrimary: boolean;
  originalTotal: number | null;
  includedTotal: number;
  items: BillingDocumentLookupItemResponse[];
}

@Component({
  selector: 'app-fiscal-document-source-orders',
  imports: [DecimalPipe],
  template: `
    <section class="source-orders" aria-labelledby="source-orders-title">
      <div class="section-header">
        <div>
          <p class="eyebrow">Origen operativo</p>
          <h4 id="source-orders-title">Órdenes incluidas en el CFDI</h4>
          <p class="helper">
            Consulta qué órdenes importadas aportaron partidas a esta factura y el importe incluido de cada una.
          </p>
        </div>

        @if (sourceOrders().length) {
          <span class="summary-pill">
            {{ sourceOrders().length }} {{ sourceOrders().length === 1 ? 'orden' : 'órdenes' }}
            · {{ includedTotal() | number: '1.2-2' }} {{ fiscalDocument().currencyCode }}
          </span>
        }
      </div>

      @if (loading()) {
        <section class="status-panel" aria-live="polite">
          Cargando las órdenes incluidas en el CFDI...
        </section>
      } @else if (errorMessage()) {
        <section class="status-panel status-panel-warning" role="alert">
          <p>{{ errorMessage() }}</p>
          <button type="button" class="retry-button" (click)="retry()">Reintentar</button>
        </section>
      } @else if (sourceOrders().length) {
        <div class="order-list">
          @for (order of sourceOrders(); track order.salesOrderId) {
            <details class="order-card">
              <summary>
                <div class="order-identity">
                  <div class="order-title-row">
                    <strong>Orden {{ order.legacyOrderId || ('#' + order.salesOrderId) }}</strong>
                    <span class="order-tag" [class.primary]="order.isPrimary">
                      {{ order.isPrimary ? 'Principal' : 'Adicional' }}
                    </span>
                  </div>
                  <span class="order-customer">{{ order.customerName || 'Cliente no disponible' }}</span>
                  <span class="order-reference">Registro interno #{{ order.salesOrderId }}</span>
                </div>

                <div class="order-metrics">
                  <span>
                    <small>Partidas incluidas</small>
                    <strong>{{ order.items.length }}</strong>
                  </span>
                  <span>
                    <small>Importe incluido</small>
                    <strong>{{ order.includedTotal | number: '1.2-2' }} {{ fiscalDocument().currencyCode }}</strong>
                  </span>
                  @if (order.originalTotal !== null) {
                    <span>
                      <small>Total original de la orden</small>
                      <strong>{{ order.originalTotal | number: '1.2-2' }} {{ fiscalDocument().currencyCode }}</strong>
                    </span>
                  }
                </div>
              </summary>

              <div class="order-body">
                <div class="table-wrap">
                  <table>
                    <thead>
                      <tr>
                        <th>Partida origen</th>
                        <th>Código</th>
                        <th>Descripción</th>
                        <th>Cantidad</th>
                        <th>Importe incluido</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (item of order.items; track item.billingDocumentItemId) {
                        <tr>
                          <td>{{ item.sourceSalesOrderLineNumber }}</td>
                          <td>{{ item.productInternalCode || '—' }}</td>
                          <td>{{ item.description }}</td>
                          <td>{{ item.quantity | number: '1.2-2' }}</td>
                          <td>{{ item.total | number: '1.2-2' }} {{ fiscalDocument().currencyCode }}</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>
            </details>
          }
        </div>
      } @else if (billingDocument()) {
        <section class="status-panel">
          No se encontraron órdenes de origen asociadas a las partidas de este CFDI.
        </section>
      }
    </section>
  `,
  styles: [
    `
      .source-orders {
        border-top: 1px solid #ece3d3;
        margin-top: 1rem;
        padding-top: 1rem;
      }
      .section-header {
        display: flex;
        justify-content: space-between;
        gap: 1rem;
        align-items: flex-start;
      }
      .eyebrow {
        margin: 0;
        text-transform: uppercase;
        letter-spacing: 0.12em;
        font-size: 0.72rem;
        color: #8a6a32;
      }
      h4 {
        margin: 0.25rem 0 0;
        font-size: 1rem;
      }
      .helper {
        margin: 0.35rem 0 0;
        color: #5f6b76;
        font-size: 0.86rem;
      }
      .summary-pill {
        flex: 0 0 auto;
        border-radius: 999px;
        background: #f4ead4;
        color: #59451f;
        padding: 0.45rem 0.75rem;
        font-size: 0.82rem;
        font-weight: 700;
        white-space: nowrap;
      }
      .status-panel {
        margin-top: 0.8rem;
        border: 1px solid #ddd3c1;
        border-radius: 0.8rem;
        background: #faf7f0;
        padding: 0.8rem;
        color: #5f6b76;
      }
      .status-panel-warning {
        border-color: #d9b56d;
        background: #fff8e8;
        color: #6b4b12;
      }
      .status-panel p {
        margin: 0;
      }
      .retry-button {
        margin-top: 0.65rem;
        border: none;
        border-radius: 0.65rem;
        background: #d8c49b;
        color: #182533;
        padding: 0.55rem 0.8rem;
        font-weight: 700;
        cursor: pointer;
      }
      .order-list {
        display: grid;
        gap: 0.65rem;
        margin-top: 0.85rem;
      }
      .order-card {
        border: 1px solid #ddd3c1;
        border-radius: 0.85rem;
        background: #fffdf9;
        overflow: hidden;
      }
      summary {
        display: grid;
        grid-template-columns: minmax(220px, 1fr) minmax(300px, auto);
        gap: 1rem;
        align-items: center;
        padding: 0.8rem 0.9rem;
        cursor: pointer;
        list-style-position: inside;
      }
      summary:hover {
        background: #faf6ed;
      }
      summary:focus-visible {
        outline: 3px solid rgba(138, 106, 50, 0.28);
        outline-offset: -3px;
      }
      .order-identity {
        display: grid;
        gap: 0.2rem;
        min-width: 0;
      }
      .order-title-row {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: 0.5rem;
      }
      .order-tag {
        border-radius: 999px;
        background: #edf0f2;
        color: #52606d;
        padding: 0.2rem 0.5rem;
        font-size: 0.7rem;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.04em;
      }
      .order-tag.primary {
        background: #e7f4e9;
        color: #28643a;
      }
      .order-customer {
        color: #344250;
        font-size: 0.86rem;
        overflow-wrap: anywhere;
      }
      .order-reference {
        color: #7a858f;
        font-size: 0.74rem;
      }
      .order-metrics {
        display: flex;
        flex-wrap: wrap;
        justify-content: flex-end;
        gap: 0.6rem 1rem;
      }
      .order-metrics span {
        display: grid;
        gap: 0.15rem;
        min-width: 95px;
      }
      .order-metrics small {
        color: #6b7680;
        font-size: 0.7rem;
      }
      .order-metrics strong {
        font-size: 0.84rem;
        color: #182533;
      }
      .order-body {
        border-top: 1px solid #ece3d3;
        padding: 0.15rem 0.9rem 0.9rem;
      }
      .table-wrap {
        overflow-x: auto;
      }
      table {
        width: 100%;
        min-width: 680px;
        border-collapse: collapse;
      }
      th,
      td {
        text-align: left;
        padding: 0.55rem 0.5rem;
        border-top: 1px solid #ece3d3;
        vertical-align: top;
      }
      th {
        color: #5f6b76;
        font-size: 0.75rem;
      }
      td {
        font-size: 0.82rem;
      }
      @media (max-width: 820px) {
        .section-header {
          flex-direction: column;
        }
        .summary-pill {
          white-space: normal;
        }
        summary {
          grid-template-columns: 1fr;
        }
        .order-metrics {
          justify-content: flex-start;
        }
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FiscalDocumentSourceOrdersComponent {
  private readonly api = inject(FiscalDocumentsApiService);
  private requestId = 0;

  readonly fiscalDocument = input.required<FiscalDocumentResponse>();

  protected readonly billingDocument = signal<BillingDocumentLookupResponse | null>(null);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly sourceOrders = computed(() =>
    buildSourceOrders(this.billingDocument(), this.fiscalDocument()),
  );
  protected readonly includedTotal = computed(() =>
    this.sourceOrders().reduce((total, order) => total + order.includedTotal, 0),
  );

  private readonly loadEffect = effect(() => {
    const billingDocumentId = this.fiscalDocument().billingDocumentId;
    untracked(() => void this.loadBillingDocument(billingDocumentId));
  });

  protected retry(): void {
    void this.loadBillingDocument(this.fiscalDocument().billingDocumentId);
  }

  private async loadBillingDocument(billingDocumentId: number): Promise<void> {
    const currentRequestId = ++this.requestId;
    this.billingDocument.set(null);
    this.errorMessage.set(null);
    this.loading.set(true);

    try {
      const billingDocument = await firstValueFrom(
        this.api.getBillingDocumentById(billingDocumentId),
      );
      if (currentRequestId !== this.requestId) {
        return;
      }

      this.billingDocument.set(billingDocument);
    } catch (error) {
      if (currentRequestId !== this.requestId) {
        return;
      }

      this.errorMessage.set(
        extractApiErrorMessage(
          error,
          'No fue posible cargar las órdenes incluidas en el CFDI.',
        ),
      );
    } finally {
      if (currentRequestId === this.requestId) {
        this.loading.set(false);
      }
    }
  }
}

function buildSourceOrders(
  billingDocument: BillingDocumentLookupResponse | null,
  fiscalDocument: FiscalDocumentResponse,
): FiscalDocumentSourceOrderViewModel[] {
  if (!billingDocument) {
    return [];
  }

  const fiscalBillingItemIds = new Set(
    fiscalDocument.items
      .map((item) => item.billingDocumentItemId)
      .filter((itemId): itemId is number => typeof itemId === 'number'),
  );
  const billingItems = billingDocument.items ?? [];
  const includedItems = fiscalBillingItemIds.size
    ? billingItems.filter((item) => fiscalBillingItemIds.has(item.billingDocumentItemId))
    : billingItems;
  const orderMetadata = new Map<number, BillingDocumentAssociatedOrderResponse>(
    (billingDocument.associatedOrders ?? []).map((order) => [order.salesOrderId, order]),
  );
  const itemsByOrder = new Map<number, BillingDocumentLookupItemResponse[]>();

  for (const item of includedItems) {
    const currentItems = itemsByOrder.get(item.salesOrderId) ?? [];
    currentItems.push(item);
    itemsByOrder.set(item.salesOrderId, currentItems);
  }

  return [...itemsByOrder.entries()]
    .map(([salesOrderId, items]) => {
      const metadata = orderMetadata.get(salesOrderId);
      const sortedItems = [...items].sort(
        (left, right) => left.lineNumber - right.lineNumber,
      );

      return {
        salesOrderId,
        legacyOrderId: metadata?.legacyOrderId ?? sortedItems[0]?.sourceLegacyOrderId ?? '',
        customerName: metadata?.customerName ?? '',
        isPrimary: metadata?.isPrimary ?? salesOrderId === billingDocument.salesOrderId,
        originalTotal: metadata?.total ?? null,
        includedTotal: sortedItems.reduce((total, item) => total + item.total, 0),
        items: sortedItems,
      };
    })
    .sort(
      (left, right) =>
        Number(right.isPrimary) - Number(left.isPrimary) ||
        (left.items[0]?.lineNumber ?? 0) - (right.items[0]?.lineNumber ?? 0),
    );
}
