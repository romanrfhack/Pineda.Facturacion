import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FiscalCancellationResponse } from '../models/fiscal-documents.models';
import { StatusBadgeComponent } from '../../../shared/components/status-badge.component';
import { getDisplayLabel } from '../../../shared/ui/display-labels';

@Component({
  selector: 'app-fiscal-cancellation-card',
  imports: [DatePipe, StatusBadgeComponent],
  template: `
    <section class="panel">
      <div class="header">
        <h3>Cancelación</h3>
        <app-status-badge [label]="statusBadgeLabel()" [tone]="statusBadgeTone()" />
      </div>
      <dl class="grid">
        <div><dt>Motivo</dt><dd>{{ cancellation().cancellationReasonCode }} - {{ cancellationReasonLabel() }}</dd></div>
        <div><dt>UUID de reemplazo</dt><dd>{{ cancellation().replacementUuid || 'N/D' }}</dd></div>
        <div><dt>Solicitado</dt><dd>{{ cancellation().requestedAtUtc | date: 'medium' }}</dd></div>
        <div><dt>{{ resolutionLabel() }}</dt><dd>{{ resolutionValue() }}</dd></div>
        <div><dt>PAC</dt><dd>{{ cancellation().providerName }}</dd></div>
        <div><dt>Código PAC</dt><dd>{{ cancellation().providerCode || 'N/D' }}</dd></div>
        <div><dt>Tracking</dt><dd>{{ cancellation().providerTrackingId || 'N/D' }}</dd></div>
        <div><dt>Error PAC</dt><dd>{{ cancellation().errorCode || 'N/D' }}</dd></div>
      </dl>
      @if (cancellation().providerMessage || cancellation().errorMessage || cancellation().supportMessage) {
        <div class="message-block">
          @if (cancellation().providerMessage) {
            <p class="message"><strong>Mensaje PAC:</strong> {{ cancellation().providerMessage }}</p>
          }
          @if (cancellation().errorMessage) {
            <p class="message"><strong>Error:</strong> {{ cancellation().errorMessage }}</p>
          }
          @if (cancellation().supportMessage) {
            <p class="message support"><strong>Soporte:</strong> {{ cancellation().supportMessage }}</p>
          }
        </div>
      }
      @if (cancellation().rawResponseSummaryJson) {
        <details class="raw-details">
          <summary>Resumen técnico PAC</summary>
          <pre>{{ cancellation().rawResponseSummaryJson }}</pre>
        </details>
      }
    </section>
  `,
  styles: [`.panel { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; } .header { display:flex; justify-content:space-between; gap:1rem; } .grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:0.75rem; margin-top:0.75rem; } dt { font-size:0.82rem; color:#666; } dd { margin:0.2rem 0 0; font-weight:600; } .message-block { display:grid; gap:0.4rem; margin-top:0.75rem; } .message { margin:0; color:#7a2020; } .support { color:#5f6b76; } .raw-details { margin-top:0.75rem; } pre { margin:0.5rem 0 0; white-space:pre-wrap; word-break:break-word; background:#f7f7f7; border-radius:0.75rem; padding:0.75rem; font-size:0.85rem; }`],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FiscalCancellationCardComponent {
  readonly cancellation = input.required<FiscalCancellationResponse>();
  protected readonly statusBadgeLabel = computed(() => {
    const status = this.cancellation().status;
    return status === 'Rejected' ? 'CancellationRejected' : status;
  });
  protected readonly statusBadgeTone = computed<'success' | 'warning' | 'danger'>(() => {
    switch (this.cancellation().status) {
      case 'Cancelled':
        return 'success';
      case 'Requested':
        return 'warning';
      default:
        return 'danger';
    }
  });
  protected readonly cancellationReasonLabel = computed(() => {
    const code = this.cancellation().cancellationReasonCode;
    switch (code) {
      case '01':
        return 'Comprobante emitido con errores con relación';
      case '02':
        return 'Comprobante emitido con errores sin relación';
      case '03':
        return 'No se llevó a cabo la operación';
      case '04':
        return 'Operación nominativa relacionada en una factura global';
      default:
        return getDisplayLabel(code);
    }
  });
  protected readonly resolutionLabel = computed(() => {
    switch (this.cancellation().status) {
      case 'Cancelled':
        return 'Cancelado';
      case 'Rejected':
        return 'Resultado';
      case 'Unavailable':
        return 'Último intento';
      default:
        return 'Estado';
    }
  });
  protected readonly resolutionValue = computed(() => {
    const cancellation = this.cancellation();
    switch (cancellation.status) {
      case 'Cancelled':
        return cancellation.cancelledAtUtc ? new DatePipe('es-MX').transform(cancellation.cancelledAtUtc, 'medium') ?? 'Cancelado' : 'Cancelado';
      case 'Rejected':
        return 'Rechazado por el PAC';
      case 'Unavailable':
        return 'No completado';
      default:
        return 'Pendiente';
    }
  });
}
