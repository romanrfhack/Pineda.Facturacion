import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { PaymentComplementStampResponse } from '../models/payment-complements.models';
import { StatusBadgeComponent } from '../../../shared/components/status-badge.component';

@Component({
  selector: 'app-payment-complement-stamp-card',
  imports: [DatePipe, StatusBadgeComponent],
  template: `
    <section class="panel">
      <div class="header">
        <div>
          <p class="eyebrow">Evidencia de timbrado del complemento</p>
          <h3>Complemento de pago #{{ stamp().paymentComplementDocumentId }}</h3>
        </div>
        <app-status-badge [label]="stamp().status" [tone]="stamp().status === 'Succeeded' || stamp().status === 'Stamped' ? 'success' : 'warning'" />
      </div>
      <dl class="grid">
        <div><dt>UUID</dt><dd>{{ stamp().uuid || 'Pendiente' }}</dd></div>
        <div><dt>Proveedor</dt><dd>{{ stamp().providerName }}</dd></div>
        <div><dt>Timbrado el</dt><dd>{{ stamp().stampedAtUtc ? (stamp().stampedAtUtc | date: 'medium') : 'Pendiente' }}</dd></div>
        <div><dt>Hash XML</dt><dd>{{ stamp().xmlHash || 'N/D' }}</dd></div>
      </dl>
      @if (stamp().providerMessage || stamp().errorMessage) {
        <p class="message">{{ stamp().providerMessage || stamp().errorMessage }}</p>
      }
      <div class="actions">
        <button type="button" class="secondary" (click)="detailsRequested.emit()">Ver detalle de evidencia</button>
        <button type="button" (click)="xmlRequested.emit()">Ver XML</button>
      </div>
    </section>
  `,
  styles: [`.panel { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; } .header { display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; } .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; } h3 { margin:0.25rem 0 0; } .grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:0.75rem; margin-top:0.75rem; } dt { font-size:0.82rem; color:#666; } dd { margin:0.2rem 0 0; font-weight:600; } .message { margin-top:0.75rem; color:#5c4a1f; } .actions { margin-top:0.75rem; display:flex; flex-wrap:wrap; gap:0.75rem; } button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; } button.secondary { background:#d8c49b; color:#182533; }`],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PaymentComplementStampCardComponent {
  readonly stamp = input.required<PaymentComplementStampResponse>();
  readonly detailsRequested = output<void>();
  readonly xmlRequested = output<void>();
}
