import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { SendReceivablesSummaryResponse } from '../models/accounts-receivable.models';
import { SendReceivablesSummaryModalComponent } from './send-receivables-summary-modal.component';

@Component({
  selector: 'app-send-receivables-summary-button',
  imports: [SendReceivablesSummaryModalComponent],
  template: `
    <button type="button" class="secondary" (click)="openModal()" [disabled]="disabled()">
      Enviar resumen de adeudos
    </button>
    <app-send-receivables-summary-modal
      [open]="modalOpen()"
      [receiverId]="receiverId()"
      [currentSelection]="currentSelection()"
      (closed)="modalOpen.set(false)"
      (sent)="handleSent($event)"
    />
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SendReceivablesSummaryButtonComponent {
  readonly receiverId = input.required<number>();
  readonly currentSelection = input<readonly number[]>([]);
  readonly disabled = input(false);
  readonly summarySent = output<SendReceivablesSummaryResponse>();

  protected readonly modalOpen = signal(false);

  protected openModal(): void {
    this.modalOpen.set(true);
  }

  protected handleSent(response: SendReceivablesSummaryResponse): void {
    this.modalOpen.set(false);
    this.summarySent.emit(response);
  }
}
