import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CreateBillingDocumentResponse, ImportLegacyOrderResponse } from '../models/orders.models';
import { StatusBadgeComponent } from '../../../shared/components/status-badge.component';

@Component({
  selector: 'app-billing-document-card',
  imports: [RouterLink, StatusBadgeComponent],
  template: `
    <section class="card">
      <div class="header">
        <div>
          <p class="eyebrow">Snapshot de venta importado</p>
          <h3>Orden legada {{ imported().legacyOrderId }}</h3>
        </div>
        <app-status-badge [label]="imported().importStatus || imported().outcome" tone="success" />
      </div>

      <dl class="grid">
        <div><dt>Id de orden de venta</dt><dd>{{ imported().salesOrderId }}</dd></div>
        <div><dt>Registro de importación</dt><dd>{{ imported().legacyImportRecordId }}</dd></div>
        <div><dt>Origen</dt><dd>{{ imported().sourceSystem }} / {{ imported().sourceTable }}</dd></div>
        <div><dt>Idempotente</dt><dd>{{ imported().isIdempotent ? 'Sí' : 'No' }}</dd></div>
      </dl>

      @if (billing(); as currentBilling) {
        <div class="secondary">
          <div class="header">
            <div>
              <p class="eyebrow">{{ currentBilling.outcome === 'Conflict' ? 'Documento existente' : 'Documento de facturación' }}</p>
              <h3>#{{ currentBilling.billingDocumentId }}</h3>
            </div>
            <app-status-badge [label]="currentBilling.billingDocumentStatus || currentBilling.outcome" tone="warning" />
          </div>

          @if (currentBilling.outcome === 'Conflict') {
            <p class="helper">La orden ya contaba con este documento. Puedes reutilizarlo para continuar con el flujo fiscal.</p>
          }

          <a [routerLink]="['/app/fiscal-documents']" [queryParams]="{ billingDocumentId: currentBilling.billingDocumentId }">
            {{ currentBilling.outcome === 'Conflict' ? 'Abrir documento existente' : 'Continuar con documento fiscal' }}
          </a>
        </div>
      }
    </section>
  `,
  styles: [`
    .card, .secondary { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; }
    .secondary { margin-top:1rem; background:#fbf8f1; }
    .header { display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    h3 { margin:0.2rem 0 0; }
    .grid { margin:1rem 0 0; display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:0.75rem; }
    dt { font-size:0.82rem; color:#6d6d6d; }
    dd { margin:0.2rem 0 0; font-weight:600; }
    .helper { margin:0.85rem 0 0; color:#5f6b76; }
    a { display:inline-flex; margin-top:1rem; color:#182533; font-weight:600; text-decoration:none; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BillingDocumentCardComponent {
  readonly imported = input.required<ImportLegacyOrderResponse>();
  readonly billing = input<CreateBillingDocumentResponse | null>(null);
}
