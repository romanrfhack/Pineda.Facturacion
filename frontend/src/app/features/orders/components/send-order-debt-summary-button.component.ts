import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { OrdersApiService } from '../infrastructure/orders-api.service';
import { LegacyOrderListItem, SendOrderDebtSummaryResponse } from '../models/orders.models';
import { OrderDebtSummaryEligibilityItemResponse } from '../models/order-debt-summary-eligibility.models';
import { SendOrderDebtSummaryModalComponent } from './send-order-debt-summary-modal.component';

@Component({
  selector: 'app-send-order-debt-summary-button',
  imports: [SendOrderDebtSummaryModalComponent],
  template: `
    <section class="summary-action">
      <button type="button" class="secondary" (click)="checkSelection()" [disabled]="disabled() || checking()">
        {{ checking() ? 'Validando adeudos...' : 'Enviar resumen de adeudos' }}
      </button>

      @if (errorMessage()) {
        <section class="eligibility-panel eligibility-error">{{ errorMessage() }}</section>
      }

      @if (blockedItems().length) {
        <section class="eligibility-panel eligibility-warning">
          <strong>
            {{ blockedItems().length === 1
              ? '1 orden no puede incluirse en el resumen.'
              : blockedItems().length + ' órdenes no pueden incluirse en el resumen.' }}
          </strong>
          <ul>
            @for (item of blockedItems(); track item.legacyOrderId) {
              <li>
                <strong>Orden {{ item.legacyOrderId }}</strong>
                <span>{{ item.message }}</span>
                @if (item.requiresReview) {
                  <small>Revisión requerida · {{ item.reasonCode }}</small>
                }
              </li>
            }
          </ul>

          @if (eligibleOrders().length) {
            <button
              type="button"
              class="continue-action"
              (click)="continueWithEligibleOrders()"
              [disabled]="checking()">
              {{ checking()
                ? 'Revalidando...'
                : 'Continuar con ' + eligibleOrders().length + (eligibleOrders().length === 1 ? ' orden elegible' : ' órdenes elegibles') }}
            </button>
          }
        </section>
      } @else if (evaluated() && adjustedReceivableItems().length) {
        <section class="eligibility-panel eligibility-info">
          El resumen utilizará el saldo vigente de Cuentas por Cobrar y contabilizará una sola vez los CFDI que agrupen varias órdenes.
        </section>
      }
    </section>

    <app-send-order-debt-summary-modal
      [open]="modalOpen()"
      [selectedOrders]="eligibleOrders()"
      (closed)="modalOpen.set(false)"
      (sent)="handleSent($event)"
    />
  `,
  styles: [
    `
      :host {
        display: block;
        max-width: min(680px, 100%);
      }

      .summary-action {
        display: grid;
        gap: 0.55rem;
      }

      button {
        border: none;
        border-radius: 0.8rem;
        padding: 0.75rem 1rem;
        background: #182533;
        color: #fff;
        cursor: pointer;
        text-decoration: none;
        display: inline-flex;
        align-items: center;
        justify-content: center;
        width: fit-content;
      }

      button.secondary {
        background: #eef1f4;
        color: #182533;
      }

      button.continue-action {
        background: #182533;
        color: #fff;
      }

      button:disabled {
        opacity: 0.58;
        cursor: not-allowed;
      }

      button:focus-visible {
        outline: 2px solid #8a6a32;
        outline-offset: 2px;
      }

      .eligibility-panel {
        border: 1px solid #d8d1c2;
        border-radius: 0.85rem;
        padding: 0.8rem 0.9rem;
        display: grid;
        gap: 0.6rem;
        font-size: 0.86rem;
        line-height: 1.4;
      }

      .eligibility-warning {
        border-color: #e6c981;
        background: #fff8ea;
        color: #4d3a16;
      }

      .eligibility-error {
        border-color: #e3b4b4;
        background: #fff1f1;
        color: #7a2020;
      }

      .eligibility-info {
        border-color: #b9d7e8;
        background: #f0f8fc;
        color: #174f78;
      }

      ul {
        margin: 0;
        padding-left: 1.15rem;
        display: grid;
        gap: 0.55rem;
      }

      li {
        padding-left: 0.15rem;
      }

      li strong,
      li span,
      li small {
        display: block;
      }

      li small {
        margin-top: 0.15rem;
        opacity: 0.8;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SendOrderDebtSummaryButtonComponent {
  private readonly ordersApi = inject(OrdersApiService);

  readonly selectedOrders = input<readonly LegacyOrderListItem[]>([]);
  readonly disabled = input(false);
  readonly summarySent = output<SendOrderDebtSummaryResponse>();

  protected readonly modalOpen = signal(false);
  protected readonly checking = signal(false);
  protected readonly evaluated = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly eligibleOrders = signal<readonly LegacyOrderListItem[]>([]);
  protected readonly blockedItems = signal<readonly OrderDebtSummaryEligibilityItemResponse[]>([]);
  protected readonly eligibilityItems = signal<readonly OrderDebtSummaryEligibilityItemResponse[]>([]);
  protected readonly adjustedReceivableItems = computed(() =>
    this.eligibilityItems().filter(item =>
      item.canInclude
      && (item.classification === 'OpenReceivable' || item.classification === 'PartiallyPaidReceivable')),
  );
  private readonly selectionKey = computed(() =>
    this.selectedOrders()
      .map(order => order.legacyOrderId)
      .sort((left, right) => left.localeCompare(right))
      .join('|'),
  );

  private eligibilityRequestId = 0;

  private readonly resetEffect = effect(() => {
    this.selectionKey();
    untracked(() => this.resetEligibilityState());
  });

  protected async checkSelection(): Promise<void> {
    await this.evaluateSelection(false);
  }

  protected async continueWithEligibleOrders(): Promise<void> {
    await this.evaluateSelection(true);
  }

  protected handleSent(response: SendOrderDebtSummaryResponse): void {
    this.modalOpen.set(false);
    this.resetEligibilityState();
    this.summarySent.emit(response);
  }

  private async evaluateSelection(openWhenSomeAreBlocked: boolean): Promise<void> {
    const selectedOrders = this.selectedOrders();
    if (this.checking() || selectedOrders.length === 0) {
      return;
    }

    const requestId = ++this.eligibilityRequestId;
    this.checking.set(true);
    this.errorMessage.set(null);
    this.modalOpen.set(false);

    try {
      const response = await firstValueFrom(this.ordersApi.evaluateOrderDebtSummaryEligibility({
        legacyOrderIds: selectedOrders.map(order => order.legacyOrderId),
      }));
      if (requestId !== this.eligibilityRequestId) {
        return;
      }

      if (!response.success) {
        this.errorMessage.set(response.errorMessage || 'No fue posible validar el estado de las órdenes seleccionadas.');
        this.evaluated.set(true);
        return;
      }

      const itemsByOrderId = new Map(
        response.items.map(item => [item.legacyOrderId.toUpperCase(), item] as const),
      );
      const eligibleOrders = selectedOrders
        .map(order => {
          const eligibility = itemsByOrderId.get(order.legacyOrderId.toUpperCase());
          if (!eligibility?.canInclude) {
            return null;
          }

          const displayStatus = eligibility.displayStatus?.trim();
          return {
            ...order,
            total: eligibility.amountDueContribution,
            currencyCode: eligibility.currencyCode || order.currencyCode,
            billingDocumentId: eligibility.billingDocumentId ?? order.billingDocumentId,
            fiscalDocumentId: eligibility.fiscalDocumentId ?? order.fiscalDocumentId,
            billingDocumentStatus: !eligibility.fiscalDocumentId && eligibility.billingDocumentId && displayStatus
              ? displayStatus
              : order.billingDocumentStatus,
            fiscalDocumentStatus: eligibility.fiscalDocumentId && displayStatus
              ? displayStatus
              : order.fiscalDocumentStatus,
          } satisfies LegacyOrderListItem;
        })
        .filter((order): order is LegacyOrderListItem => order !== null);
      const blockedItems = response.items.filter(item => !item.canInclude);

      this.eligibilityItems.set(response.items);
      this.eligibleOrders.set(eligibleOrders);
      this.blockedItems.set(blockedItems);
      this.evaluated.set(true);

      if (eligibleOrders.length === 0) {
        this.errorMessage.set('Ninguna de las órdenes seleccionadas es elegible para el resumen de adeudos.');
        return;
      }

      if (blockedItems.length === 0 || openWhenSomeAreBlocked) {
        this.modalOpen.set(true);
      }
    } catch (error) {
      if (requestId === this.eligibilityRequestId) {
        this.errorMessage.set(extractApiErrorMessage(error));
        this.evaluated.set(true);
      }
    } finally {
      if (requestId === this.eligibilityRequestId) {
        this.checking.set(false);
      }
    }
  }

  private resetEligibilityState(): void {
    this.eligibilityRequestId += 1;
    this.checking.set(false);
    this.evaluated.set(false);
    this.errorMessage.set(null);
    this.eligibleOrders.set([]);
    this.blockedItems.set([]);
    this.eligibilityItems.set([]);
    this.modalOpen.set(false);
  }
}
