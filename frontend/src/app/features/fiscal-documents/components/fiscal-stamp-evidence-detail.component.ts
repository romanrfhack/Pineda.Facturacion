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
          <p class="eyebrow">Stamp detail</p>
          <h3>Fiscal document #{{ stamp().fiscalDocumentId }}</h3>
        </div>
      </div>

      <dl class="grid">
        <div><dt>Stamp id</dt><dd>{{ stamp().id }}</dd></div>
        <div><dt>Provider operation</dt><dd>{{ stamp().providerOperation || 'N/A' }}</dd></div>
        <div><dt>Tracking id</dt><dd>{{ stamp().providerTrackingId || 'N/A' }}</dd></div>
        <div><dt>QR text / URL</dt><dd>{{ stamp().qrCodeTextOrUrl || 'N/A' }}</dd></div>
        <div><dt>Original string</dt><dd class="mono">{{ stamp().originalString || 'N/A' }}</dd></div>
        <div><dt>Created</dt><dd>{{ stamp().createdAtUtc | date: 'medium' }}</dd></div>
        <div><dt>Updated</dt><dd>{{ stamp().updatedAtUtc | date: 'medium' }}</dd></div>
      </dl>
    </section>
  `,
  styles: [`
    .panel { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    h3 { margin:0.25rem 0 0; }
    .grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:0.75rem; margin-top:0.75rem; }
    dt { font-size:0.82rem; color:#666; }
    dd { margin:0.2rem 0 0; font-weight:600; }
    .mono { font-family:"SFMono-Regular", Consolas, "Liberation Mono", Menlo, monospace; font-weight:500; word-break:break-word; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FiscalStampEvidenceDetailComponent {
  readonly stamp = input.required<FiscalStampResponse>();
}
