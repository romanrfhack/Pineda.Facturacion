import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FiscalStampResponse } from '../models/fiscal-documents.models';
import { StatusBadgeComponent } from '../../../shared/components/status-badge.component';

@Component({
  selector: 'app-fiscal-stamp-evidence-card',
  imports: [DatePipe, StatusBadgeComponent],
  template: `
    <section class="panel">
      <div class="header">
        <div>
          <p class="eyebrow">Evidencia de timbrado</p>
          <h3>Documento fiscal #{{ stamp().fiscalDocumentId }}</h3>
        </div>
        <app-status-badge [label]="stamp().status" [tone]="stamp().status === 'Succeeded' || stamp().status === 'Stamped' ? 'success' : 'warning'" />
      </div>
      <dl class="details-list">
        <div class="detail-row"><dt>UUID</dt><dd>{{ stamp().uuid || 'Pendiente' }}</dd></div>
        <div class="detail-row"><dt>Proveedor</dt><dd>{{ stamp().providerName }}</dd></div>
        <div class="detail-row"><dt>Timbrado el</dt><dd>{{ stamp().stampedAtUtc ? (stamp().stampedAtUtc | date: 'medium') : 'Sin timbrar' }}</dd></div>
        <div class="detail-row"><dt>Tracking / hash</dt><dd class="mono">{{ stamp().providerTrackingId || stamp().xmlHash || 'N/D' }}</dd></div>
      </dl>
      @if (stamp().providerMessage || stamp().errorMessage) {
        <p class="message">{{ stamp().providerMessage || stamp().errorMessage }}</p>
      }
      @if (stamp().lastRemoteQueryAtUtc || stamp().xmlRecoveredFromProviderAtUtc) {
        <p class="message">
          @if (stamp().lastRemoteQueryAtUtc) {
            <span>Última consulta remota: {{ stamp().lastRemoteQueryAtUtc | date: 'medium' }}</span>
          }
          @if (stamp().xmlRecoveredFromProviderAtUtc) {
            <span> · XML recuperado: {{ stamp().xmlRecoveredFromProviderAtUtc | date: 'medium' }}</span>
          }
        </p>
      }
      <div class="actions">
        <button type="button" class="secondary" (click)="detailsRequested.emit()">Ver detalle de evidencia</button>
        <button type="button" (click)="xmlRequested.emit()">Ver XML</button>
        <button type="button" class="secondary" (click)="remoteQueryRequested.emit()">Consultar CFDI en PAC</button>
      </div>
    </section>
  `,
  styles: [`
    .panel { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; }
    .header { display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    h3 { margin:0.25rem 0 0; }
    .details-list { display:grid; gap:0.65rem; margin-top:0.9rem; }
    .detail-row { display:grid; grid-template-columns:minmax(130px, 180px) minmax(0, 1fr); gap:0.35rem 0.9rem; align-items:start; }
    dt { font-size:0.82rem; color:#666; }
    dd { margin:0.2rem 0 0; font-weight:600; min-width:0; overflow-wrap:anywhere; word-break:break-word; }
    .mono { font-family:"SFMono-Regular", Consolas, "Liberation Mono", Menlo, monospace; font-weight:500; }
    .message { margin-top:0.75rem; color:#5c4a1f; overflow-wrap:anywhere; word-break:break-word; }
    .actions { margin-top:0.75rem; display:flex; flex-wrap:wrap; gap:0.75rem; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    button.secondary { background:#d8c49b; color:#182533; }
    @media (max-width: 720px) {
      .detail-row { grid-template-columns:1fr; }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FiscalStampEvidenceCardComponent {
  readonly stamp = input.required<FiscalStampResponse>();
  readonly detailsRequested = output<void>();
  readonly xmlRequested = output<void>();
  readonly remoteQueryRequested = output<void>();
}
