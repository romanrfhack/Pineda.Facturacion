import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { DecimalPipe, DatePipe } from '@angular/common';
import { FiscalDocumentResponse } from '../models/fiscal-documents.models';
import { StatusBadgeComponent } from '../../../shared/components/status-badge.component';

@Component({
  selector: 'app-fiscal-document-card',
  imports: [DecimalPipe, DatePipe, StatusBadgeComponent],
  template: `
    <section class="panel">
      <div class="header">
        <div>
          <p class="eyebrow">Snapshot fiscal</p>
          <h3>Documento fiscal #{{ document().id }}</h3>
        </div>
        <app-status-badge [label]="document().status" [tone]="document().status === 'Stamped' ? 'success' : document().status.includes('Rejected') ? 'danger' : 'warning'" />
      </div>

      <dl class="grid">
        <div><dt>Receptor</dt><dd>{{ document().receiverLegalName }} ({{ document().receiverRfc }})</dd></div>
        <div><dt>Emisor</dt><dd>{{ document().issuerLegalName }} ({{ document().issuerRfc }})</dd></div>
        <div><dt>Emitido</dt><dd>{{ document().issuedAtUtc | date: 'medium' }}</dd></div>
        <div><dt>Pago</dt><dd>{{ document().paymentMethodSat }} / {{ document().paymentFormSat }}</dd></div>
        <div><dt>Venta a crédito</dt><dd>{{ document().isCreditSale ? 'Sí' : 'No' }}</dd></div>
        <div><dt>Total</dt><dd>{{ document().total | number: '1.2-2' }} {{ document().currencyCode }}</dd></div>
      </dl>

      <table>
        <thead>
          <tr>
            <th>Partida</th>
            <th>Código</th>
            <th>Descripción</th>
            <th>Cant.</th>
            <th>Total</th>
            <th>SAT</th>
          </tr>
        </thead>
        <tbody>
          @for (item of document().items; track item.id) {
            <tr>
              <td>{{ item.lineNumber }}</td>
              <td>{{ item.internalCode }}</td>
              <td>{{ item.description }}</td>
              <td>{{ item.quantity | number: '1.2-2' }}</td>
              <td>{{ item.total | number: '1.2-2' }}</td>
              <td>{{ item.satProductServiceCode }} / {{ item.satUnitCode }}</td>
            </tr>
          }
        </tbody>
      </table>
    </section>
  `,
  styles: [`
    .panel { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; }
    .header { display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    h3 { margin:0.25rem 0 0; }
    .grid { margin:1rem 0; display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:0.75rem; }
    dt { font-size:0.82rem; color:#666; }
    dd { margin:0.2rem 0 0; font-weight:600; }
    table { width:100%; border-collapse:collapse; }
    th, td { text-align:left; padding:0.6rem; border-top:1px solid #ece3d3; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FiscalDocumentCardComponent {
  readonly document = input.required<FiscalDocumentResponse>();
}
