import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { LegacyOrderListItem, SendOrderDebtSummaryResponse } from '../models/orders.models';
import { SendOrderDebtSummaryModalComponent } from './send-order-debt-summary-modal.component';

@Component({
  selector: 'app-send-order-debt-summary-button',
  imports: [SendOrderDebtSummaryModalComponent],
  template: `
    <button type="button" class="secondary" (click)="openModal()" [disabled]="disabled()">
      Enviar resumen de adeudos
    </button>
    <app-send-order-debt-summary-modal
      [open]="modalOpen()"
      [selectedOrders]="selectedOrders()"
      (closed)="modalOpen.set(false)"
      (sent)="handleSent($event)"
    />
  `,
  styles: [
    `
      :host {
        display: inline-flex;
        align-items: center;
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
      }

      button.secondary {
        background: #eef1f4;
        color: #182533;
      }

      button:disabled {
        opacity: 0.58;
        cursor: not-allowed;
      }

      button:focus-visible {
        outline: 2px solid #8a6a32;
        outline-offset: 2px;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SendOrderDebtSummaryButtonComponent {
  readonly selectedOrders = input<readonly LegacyOrderListItem[]>([]);
  readonly disabled = input(false);
  readonly summarySent = output<SendOrderDebtSummaryResponse>();

  protected readonly modalOpen = signal(false);

  protected openModal(): void {
    this.modalOpen.set(true);
  }

  protected handleSent(response: SendOrderDebtSummaryResponse): void {
    this.modalOpen.set(false);
    this.summarySent.emit(response);
  }
}
