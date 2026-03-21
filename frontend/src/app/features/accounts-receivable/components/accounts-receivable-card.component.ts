import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { AccountsReceivableInvoiceResponse } from '../models/accounts-receivable.models';
import { StatusBadgeComponent } from '../../../shared/components/status-badge.component';

@Component({
  selector: 'app-accounts-receivable-card',
  imports: [DatePipe, DecimalPipe, StatusBadgeComponent],
  template: `
    <section class="panel">
      <div class="header">
        <div>
          <p class="eyebrow">Cuenta por cobrar</p>
          <h3>#{{ invoice().id }}</h3>
        </div>
        <app-status-badge [label]="invoice().status" [tone]="invoice().status === 'Paid' ? 'success' : invoice().status === 'PartiallyPaid' ? 'warning' : 'neutral'" />
      </div>
      <dl class="grid">
        <div><dt>Total</dt><dd>{{ invoice().total | number: '1.2-2' }} {{ invoice().currencyCode }}</dd></div>
        <div><dt>Pagado</dt><dd>{{ invoice().paidTotal | number: '1.2-2' }}</dd></div>
        <div><dt>Pendiente</dt><dd>{{ invoice().outstandingBalance | number: '1.2-2' }}</dd></div>
        <div><dt>Vencimiento</dt><dd>{{ invoice().dueAtUtc ? (invoice().dueAtUtc | date: 'mediumDate') : 'N/D' }}</dd></div>
      </dl>
    </section>
  `,
  styles: [`.panel { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; } .header { display:flex; justify-content:space-between; gap:1rem; } .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; } .grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:0.75rem; margin-top:0.75rem; } dt { font-size:0.82rem; color:#666; } dd { margin:0.2rem 0 0; font-weight:600; }`],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AccountsReceivableCardComponent {
  readonly invoice = input.required<AccountsReceivableInvoiceResponse>();
}
