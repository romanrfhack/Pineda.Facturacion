import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { DecimalPipe, DatePipe } from '@angular/common';
import { PaymentComplementDocumentResponse } from '../models/payment-complements.models';
import { StatusBadgeComponent } from '../../../shared/components/status-badge.component';

@Component({
  selector: 'app-payment-complement-card',
  imports: [DecimalPipe, DatePipe, StatusBadgeComponent],
  template: `
    <section class="panel">
      <div class="header">
        <div>
          <p class="eyebrow">Payment complement snapshot</p>
          <h3>#{{ complement().id }}</h3>
        </div>
        <app-status-badge [label]="complement().status" [tone]="complement().status === 'Stamped' ? 'success' : complement().status.includes('Rejected') ? 'danger' : 'warning'" />
      </div>

      <dl class="grid">
        <div><dt>Payment id</dt><dd>{{ complement().accountsReceivablePaymentId }}</dd></div>
        <div><dt>Total payments</dt><dd>{{ complement().totalPaymentsAmount | number: '1.2-2' }} {{ complement().currencyCode }}</dd></div>
        <div><dt>Receiver</dt><dd>{{ complement().receiverLegalName }} ({{ complement().receiverRfc }})</dd></div>
        <div><dt>Issued</dt><dd>{{ complement().issuedAtUtc | date: 'medium' }}</dd></div>
      </dl>

      <table>
        <thead>
          <tr><th>UUID</th><th>Installment</th><th>Previous</th><th>Paid</th><th>Remaining</th></tr>
        </thead>
        <tbody>
          @for (row of complement().relatedDocuments; track row.id) {
            <tr>
              <td>{{ row.relatedDocumentUuid }}</td>
              <td>{{ row.installmentNumber }}</td>
              <td>{{ row.previousBalance | number: '1.2-2' }}</td>
              <td>{{ row.paidAmount | number: '1.2-2' }}</td>
              <td>{{ row.remainingBalance | number: '1.2-2' }}</td>
            </tr>
          }
        </tbody>
      </table>
    </section>
  `,
  styles: [`.panel { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; } .header { display:flex; justify-content:space-between; gap:1rem; } .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; } .grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:0.75rem; margin:1rem 0; } dt { font-size:0.82rem; color:#666; } dd { margin:0.2rem 0 0; font-weight:600; } table { width:100%; border-collapse:collapse; } th, td { text-align:left; padding:0.6rem; border-top:1px solid #ece3d3; }`],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PaymentComplementCardComponent {
  readonly complement = input.required<PaymentComplementDocumentResponse>();
}
