import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FiscalStampResponse } from '../models/fiscal-documents.models';

@Component({
  selector: 'app-fiscal-stamp-evidence-detail',
  imports: [DatePipe],
  template: `
    <section class="panel">
      <div class="header">
        <div>
          <p class="eyebrow">Detalle de timbrado</p>
          <h3>Documento fiscal #{{ stamp().fiscalDocumentId }}</h3>
        </div>
      </div>

      <dl class="details-list">
        <div class="detail-row"><dt>Id de timbre</dt><dd>{{ stamp().id }}</dd></div>
        <div class="detail-row"><dt>Operación del proveedor</dt><dd>{{ stamp().providerOperation || 'N/D' }}</dd></div>
        <div class="detail-row"><dt>Id de tracking</dt><dd class="mono">{{ stamp().providerTrackingId || 'N/D' }}</dd></div>
        <div class="detail-row"><dt>Texto / URL QR</dt><dd class="mono">{{ stamp().qrCodeTextOrUrl || 'N/D' }}</dd></div>
        <div class="detail-row"><dt>Cadena original</dt><dd class="mono">{{ stamp().originalString || 'N/D' }}</dd></div>
        <div class="detail-row"><dt>Última consulta remota</dt><dd>{{ stamp().lastRemoteQueryAtUtc ? (stamp().lastRemoteQueryAtUtc | date: 'medium') : 'Sin consultar' }}</dd></div>
        <div class="detail-row"><dt>Tracking remoto</dt><dd class="mono">{{ stamp().lastRemoteProviderTrackingId || 'N/D' }}</dd></div>
        <div class="detail-row"><dt>Código remoto</dt><dd class="mono">{{ stamp().lastRemoteProviderCode || 'N/D' }}</dd></div>
        <div class="detail-row"><dt>Mensaje remoto</dt><dd>{{ stamp().lastRemoteProviderMessage || 'N/D' }}</dd></div>
        <div class="detail-row"><dt>XML recuperado</dt><dd>{{ stamp().xmlRecoveredFromProviderAtUtc ? (stamp().xmlRecoveredFromProviderAtUtc | date: 'medium') : 'No' }}</dd></div>
        <div class="detail-row"><dt>Creado</dt><dd>{{ stamp().createdAtUtc | date: 'medium' }}</dd></div>
        <div class="detail-row"><dt>Actualizado</dt><dd>{{ stamp().updatedAtUtc | date: 'medium' }}</dd></div>
      </dl>

      @if (stamp().lastRemoteRawResponseSummaryJson) {
        <div class="raw-block">
          <strong>Resumen raw de última consulta remota</strong>
          <pre>{{ stamp().lastRemoteRawResponseSummaryJson }}</pre>
        </div>
      }
    </section>
  `,
  styles: [`
    .panel { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    h3 { margin:0.25rem 0 0; }
    .details-list { display:grid; gap:0.65rem; margin-top:0.75rem; }
    .detail-row { display:grid; grid-template-columns:minmax(140px, 190px) minmax(0, 1fr); gap:0.35rem 0.9rem; align-items:start; }
    dt { font-size:0.82rem; color:#666; }
    dd { margin:0.2rem 0 0; font-weight:600; min-width:0; overflow-wrap:anywhere; word-break:break-word; }
    .mono { font-family:"SFMono-Regular", Consolas, "Liberation Mono", Menlo, monospace; font-weight:500; }
    .raw-block { margin-top:0.9rem; display:grid; gap:0.35rem; }
    pre { margin:0; padding:0.75rem; border-radius:0.75rem; background:#f6f0e4; overflow:auto; font-family:"SFMono-Regular", Consolas, "Liberation Mono", Menlo, monospace; font-size:0.78rem; }
    @media (max-width: 720px) {
      .detail-row { grid-template-columns:1fr; }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FiscalStampEvidenceDetailComponent {
  readonly stamp = input.required<FiscalStampResponse>();
}
