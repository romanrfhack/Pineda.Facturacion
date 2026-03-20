import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FiscalCancellationResponse } from '../models/fiscal-documents.models';
import { StatusBadgeComponent } from '../../../shared/components/status-badge.component';

@Component({
  selector: 'app-fiscal-cancellation-card',
  imports: [DatePipe, StatusBadgeComponent],
  template: `
    <section class="panel">
      <div class="header">
        <h3>Cancellation</h3>
        <app-status-badge [label]="cancellation().status" [tone]="cancellation().status === 'Cancelled' ? 'success' : 'danger'" />
      </div>
      <dl class="grid">
        <div><dt>Reason</dt><dd>{{ cancellation().cancellationReasonCode }}</dd></div>
        <div><dt>Replacement UUID</dt><dd>{{ cancellation().replacementUuid || 'N/A' }}</dd></div>
        <div><dt>Requested</dt><dd>{{ cancellation().requestedAtUtc | date: 'medium' }}</dd></div>
        <div><dt>Cancelled</dt><dd>{{ cancellation().cancelledAtUtc ? (cancellation().cancelledAtUtc | date: 'medium') : 'Pending' }}</dd></div>
      </dl>
      @if (cancellation().providerMessage || cancellation().errorMessage) {
        <p class="message">{{ cancellation().providerMessage || cancellation().errorMessage }}</p>
      }
    </section>
  `,
  styles: [`.panel { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; } .header { display:flex; justify-content:space-between; gap:1rem; } .grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:0.75rem; margin-top:0.75rem; } dt { font-size:0.82rem; color:#666; } dd { margin:0.2rem 0 0; font-weight:600; } .message { margin-top:0.75rem; color:#7a2020; }`],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FiscalCancellationCardComponent {
  readonly cancellation = input.required<FiscalCancellationResponse>();
}
