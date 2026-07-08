import { CurrencyPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  SimpleChanges,
  computed,
  input,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  AccountsReceivablePaymentApplicationResponse,
  AccountsReceivablePaymentResponse,
  AccountsReceivablePortfolioItemResponse,
  ReassignAccountsReceivablePaymentApplicationRowRequest,
  ReassignAccountsReceivablePaymentApplicationsRequest,
} from '../models/accounts-receivable.models';

interface PaymentApplicationReassignRow {
  readonly rowId: number;
  readonly accountsReceivableInvoiceId: number | null;
  readonly amount: number;
  readonly amountText: string;
}

interface PaymentApplicationReassignInvoiceOption {
  readonly id: number;
  readonly label: string;
  readonly fiscalUuid?: string | null;
  readonly issuedAtUtc?: string | null;
  readonly dueAtUtc?: string | null;
  readonly outstandingBalance: number;
  readonly status: string;
  readonly fiscalReceiverId?: number | null;
  readonly currencyCode?: string | null;
}

@Component({
  selector: 'app-payment-application-reassign-modal',
  imports: [CurrencyPipe, FormsModule],
  template: `
    <section class="modal-backdrop" (click)="cancelWhenIdle()">
      <section
        class="modal-card"
        role="dialog"
        aria-modal="true"
        aria-label="Reasignar pago"
        (click)="$event.stopPropagation()"
      >
        <header class="modal-header">
          <div>
            <p class="eyebrow">Cuentas por cobrar</p>
            <h3>Reasignar pago #{{ payment().id }}</h3>
            <p class="helper">
              Pago {{ payment().amount | currency: 'MXN' : 'symbol' : '1.2-2' }} ·
              {{ payment().applications.length }} aplicacion(es) actuales
            </p>
          </div>
          <button
            type="button"
            class="icon-button secondary"
            aria-label="Cerrar modal"
            (click)="cancelWhenIdle()"
            [disabled]="submitting()"
          >
            x
          </button>
        </header>

        <section class="warning-panel">
          Esta operación reemplazará la distribución actual del pago. No se permite si ya existe
          REP asociado.
        </section>

        <section class="modal-section">
          <div class="section-head compact">
            <h4>Distribución actual</h4>
          </div>
          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Factura</th>
                  <th>Importe aplicado</th>
                  <th>Saldo previo</th>
                  <th>Saldo nuevo</th>
                </tr>
              </thead>
              <tbody>
                @for (application of currentApplications(); track application.id) {
                  <tr>
                    <td>
                      <div>{{ applicationInvoiceLabel(application.accountsReceivableInvoiceId) }}</div>
                      <div class="subtle">CxC #{{ application.accountsReceivableInvoiceId }}</div>
                    </td>
                    <td>
                      {{ application.appliedAmount | currency: 'MXN' : 'symbol' : '1.2-2' }}
                    </td>
                    <td>
                      {{ application.previousBalance | currency: 'MXN' : 'symbol' : '1.2-2' }}
                    </td>
                    <td>{{ application.newBalance | currency: 'MXN' : 'symbol' : '1.2-2' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>

        <section class="summary-grid">
          <div>
            <span>Total del pago</span>
            <strong>{{ payment().amount | currency: 'MXN' : 'symbol' : '1.2-2' }}</strong>
          </div>
          <div>
            <span>Total asignado</span>
            <strong>{{ totalAssigned() | currency: 'MXN' : 'symbol' : '1.2-2' }}</strong>
          </div>
          <div>
            <span>Diferencia / remanente</span>
            <strong>{{ remainingAfterReassign() | currency: 'MXN' : 'symbol' : '1.2-2' }}</strong>
          </div>
        </section>

        <section class="modal-section">
          <div class="section-head compact">
            <h4>Nueva distribución</h4>
            <button
              type="button"
              class="secondary inline-action-button"
              (click)="addRow()"
              [disabled]="submitting() || !hasUnusedInvoiceOptions()"
            >
              Agregar factura
            </button>
          </div>

          @if (!rows().length) {
            <p class="helper">Agrega al menos una factura para reasignar el pago.</p>
          } @else {
            <div class="table-wrap">
              <table class="reassign-table">
                <thead>
                  <tr>
                    <th>Factura</th>
                    <th>Monto a aplicar</th>
                    <th>Máximo aplicable</th>
                    <th>Saldo nuevo estimado</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of rows(); track row.rowId) {
                    <tr>
                      <td data-label="Factura">
                        <select
                          [ngModel]="row.accountsReceivableInvoiceId"
                          (ngModelChange)="updateRowInvoice(row.rowId, $event)"
                          [disabled]="submitting()"
                        >
                          <option [ngValue]="null">Selecciona factura</option>
                          @for (option of invoiceOptionsForRow(row.rowId); track option.id) {
                            <option [ngValue]="option.id">
                              {{ option.label }} · saldo
                              {{ maxApplicableForInvoice(option.id) | currency: 'MXN' : 'symbol' : '1.2-2' }}
                            </option>
                          }
                        </select>
                        @if (row.accountsReceivableInvoiceId) {
                          <div class="subtle">
                            {{ invoiceDetail(row.accountsReceivableInvoiceId) }}
                          </div>
                        }
                      </td>
                      <td data-label="Monto a aplicar">
                        <input
                          type="text"
                          inputmode="decimal"
                          autocomplete="off"
                          [ngModel]="row.amountText"
                          (ngModelChange)="updateRowAmount(row.rowId, $event)"
                          (blur)="normalizeRowAmount(row.rowId)"
                          [disabled]="submitting()"
                        />
                      </td>
                      <td data-label="Máximo aplicable">
                        {{
                          row.accountsReceivableInvoiceId
                            ? (maxApplicableForInvoice(row.accountsReceivableInvoiceId)
                              | currency: 'MXN' : 'symbol' : '1.2-2')
                            : 'N/D'
                        }}
                      </td>
                      <td data-label="Saldo nuevo estimado">
                        {{
                          row.accountsReceivableInvoiceId
                            ? (estimatedNewBalance(row) | currency: 'MXN' : 'symbol' : '1.2-2')
                            : 'N/D'
                        }}
                      </td>
                      <td data-label="Acción">
                        <button
                          type="button"
                          class="secondary inline-action-button"
                          (click)="removeRow(row.rowId)"
                          [disabled]="submitting()"
                        >
                          Quitar
                        </button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </section>

        <label class="reason-field">
          <span>Motivo</span>
          <textarea
            rows="4"
            maxlength="1000"
            [ngModel]="reasonText()"
            (ngModelChange)="updateReason($event)"
            [disabled]="submitting()"
          ></textarea>
          <small>{{ reasonLength() }}/1000</small>
        </label>

        @if (errorMessage()) {
          <section class="error-panel">{{ errorMessage() }}</section>
        } @else if (validationMessage()) {
          <section class="error-panel">{{ validationMessage() }}</section>
        }

        <div class="actions">
          <button type="button" class="secondary" (click)="cancelWhenIdle()" [disabled]="submitting()">
            Cancelar
          </button>
          <button type="button" (click)="confirmSubmit()" [disabled]="!canSubmit()">
            {{ submitting() ? 'Reasignando...' : 'Confirmar reasignación' }}
          </button>
        </div>
      </section>
    </section>
  `,
  styles: [
    `
      .modal-backdrop {
        position: fixed;
        inset: 0;
        background: rgba(24, 37, 51, 0.52);
        display: grid;
        place-items: center;
        padding: 1rem;
        z-index: 70;
      }
      .modal-card {
        width: min(1040px, 100%);
        max-height: min(92vh, 860px);
        overflow: auto;
        border: 1px solid #d8d1c2;
        border-radius: 1rem;
        background: #fff;
        padding: 1rem;
        display: grid;
        gap: 1rem;
        box-shadow: 0 24px 60px rgba(24, 37, 51, 0.24);
      }
      .modal-header,
      .section-head,
      .actions {
        display: flex;
        justify-content: space-between;
        gap: 1rem;
        align-items: flex-start;
      }
      .section-head.compact {
        align-items: center;
      }
      .eyebrow {
        margin: 0;
        text-transform: uppercase;
        letter-spacing: 0.12em;
        font-size: 0.72rem;
        color: #8a6a32;
      }
      h3,
      h4 {
        margin: 0;
      }
      .helper {
        margin: 0.35rem 0 0;
        color: #5f6b76;
      }
      .warning-panel,
      .error-panel {
        border-radius: 0.85rem;
        padding: 0.8rem 0.9rem;
        line-height: 1.45;
      }
      .warning-panel {
        background: #fff8ea;
        border: 1px solid #ecd9aa;
        color: #66460f;
      }
      .error-panel {
        background: #fff2f0;
        border: 1px solid #f0c7c1;
        color: #8a2d2d;
      }
      .modal-section {
        display: grid;
        gap: 0.75rem;
      }
      .summary-grid {
        display: grid;
        gap: 0.75rem;
        grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
        padding: 0.9rem;
        border: 1px solid #ece3d3;
        border-radius: 0.9rem;
        background: #fffdf8;
      }
      .summary-grid div {
        display: grid;
        gap: 0.25rem;
      }
      .summary-grid span,
      label span {
        font-size: 0.82rem;
        color: #495766;
      }
      .summary-grid strong {
        color: #182533;
      }
      .table-wrap {
        overflow: auto;
      }
      table {
        width: 100%;
        border-collapse: collapse;
      }
      th,
      td {
        text-align: left;
        padding: 0.7rem;
        border-top: 1px solid #ece3d3;
        vertical-align: top;
      }
      .subtle {
        color: #71808f;
        font-size: 0.84rem;
        margin-top: 0.2rem;
      }
      label,
      .reason-field {
        display: grid;
        gap: 0.35rem;
      }
      input,
      select,
      textarea {
        width: 100%;
        min-width: 0;
        box-sizing: border-box;
        border: 1px solid #d8d1c2;
        border-radius: 0.75rem;
        padding: 0.7rem 0.85rem;
        background: #fffdf8;
        font: inherit;
      }
      textarea {
        resize: vertical;
      }
      small {
        justify-self: end;
        color: #71808f;
      }
      button {
        border: none;
        border-radius: 0.8rem;
        padding: 0.75rem 1rem;
        background: #182533;
        color: #fff;
        cursor: pointer;
        font: inherit;
        display: inline-flex;
        justify-content: center;
      }
      button.secondary {
        background: #eef1f4;
        color: #182533;
      }
      button:disabled {
        opacity: 0.6;
        cursor: not-allowed;
      }
      .icon-button {
        width: 2.5rem;
        height: 2.5rem;
        align-items: center;
        padding: 0;
        font-size: 1.5rem;
        line-height: 1;
      }
      .inline-action-button {
        padding: 0.58rem 0.8rem;
      }
      .actions {
        justify-content: flex-end;
        flex-wrap: wrap;
      }
      @media (max-width: 720px) {
        .modal-card {
          max-height: 94vh;
          padding: 0.85rem;
        }
        .modal-header,
        .section-head,
        .actions {
          align-items: stretch;
          flex-direction: column;
        }
        .reassign-table thead {
          display: none;
        }
        .reassign-table,
        .reassign-table tbody,
        .reassign-table tr,
        .reassign-table td {
          display: block;
          width: 100%;
        }
        .reassign-table tbody {
          display: grid;
          gap: 0.75rem;
        }
        .reassign-table tr {
          border: 1px solid #ece3d3;
          border-radius: 0.85rem;
          overflow: hidden;
        }
        .reassign-table td {
          display: grid;
          grid-template-columns: minmax(6rem, 8rem) minmax(0, 1fr);
          gap: 0.5rem;
          padding: 0.75rem;
        }
        .reassign-table td::before {
          content: attr(data-label);
          color: #5f6b76;
          font-size: 0.78rem;
          font-weight: 600;
        }
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PaymentApplicationReassignModalComponent {
  readonly payment = input.required<AccountsReceivablePaymentResponse>();
  readonly fiscalReceiverId = input<number | null>(null);
  readonly candidateInvoices = input<AccountsReceivablePortfolioItemResponse[]>([]);
  readonly submitting = input(false);
  readonly errorMessage = input<string | null>(null);
  readonly submitted = output<ReassignAccountsReceivablePaymentApplicationsRequest>();
  readonly cancelled = output<void>();

  protected readonly rows = signal<PaymentApplicationReassignRow[]>([]);
  protected readonly reasonText = signal('');
  protected readonly currentApplications = computed(() => this.payment().applications ?? []);
  protected readonly reasonLength = computed(() => this.reasonText().length);
  protected readonly currentAppliedByInvoice = computed(() =>
    this.sumCurrentApplicationsByInvoice((application) => application.appliedAmount),
  );
  protected readonly currentPreviousBalanceByInvoice = computed(() =>
    this.maxCurrentApplicationsByInvoice((application) => application.previousBalance),
  );
  protected readonly invoiceOptions = computed(() => this.buildInvoiceOptions());
  protected readonly totalAssigned = computed(() =>
    this.roundMoney(this.rows().reduce((sum, row) => sum + row.amount, 0)),
  );
  protected readonly remainingAfterReassign = computed(() =>
    this.roundMoney(this.payment().amount - this.totalAssigned()),
  );
  protected readonly validationMessages = computed(() => this.buildValidationMessages());
  protected readonly validationMessage = computed(() => this.validationMessages()[0] ?? null);
  protected readonly canSubmit = computed(
    () => !this.submitting() && this.validationMessages().length === 0,
  );

  private nextRowId = 0;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['payment']) {
      this.resetFromPayment();
    }
  }

  protected updateReason(value: string): void {
    this.reasonText.set(String(value ?? '').slice(0, 1000));
  }

  protected addRow(): void {
    const selectedIds = new Set(
      this.rows()
        .map((row) => row.accountsReceivableInvoiceId)
        .filter((id): id is number => id !== null),
    );
    const firstUnusedOption = this.invoiceOptions().find((option) => !selectedIds.has(option.id));

    this.rows.update((current) => [
      ...current,
      {
        rowId: ++this.nextRowId,
        accountsReceivableInvoiceId: firstUnusedOption?.id ?? null,
        amount: 0,
        amountText: this.formatEditableAmount(0),
      },
    ]);
  }

  protected removeRow(rowId: number): void {
    this.rows.update((current) => current.filter((row) => row.rowId !== rowId));
  }

  protected updateRowInvoice(rowId: number, invoiceId: number | null): void {
    const parsedInvoiceId = invoiceId === null ? null : Number(invoiceId);
    this.rows.update((current) =>
      current.map((row) =>
        row.rowId === rowId
          ? {
              ...row,
              accountsReceivableInvoiceId:
                parsedInvoiceId !== null && Number.isFinite(parsedInvoiceId)
                  ? parsedInvoiceId
                  : null,
            }
          : row,
      ),
    );
  }

  protected updateRowAmount(rowId: number, value: string | number): void {
    const sanitized = String(value ?? '')
      .replace(/,/g, '')
      .trim();
    const parsed = Number(sanitized);
    const amount = Number.isFinite(parsed) ? this.roundMoney(parsed) : 0;

    this.rows.update((current) =>
      current.map((row) =>
        row.rowId === rowId
          ? {
              ...row,
              amount,
              amountText: sanitized,
            }
          : row,
      ),
    );
  }

  protected normalizeRowAmount(rowId: number): void {
    this.rows.update((current) =>
      current.map((row) =>
        row.rowId === rowId
          ? {
              ...row,
              amount: Math.max(row.amount, 0),
              amountText: this.formatEditableAmount(Math.max(row.amount, 0)),
            }
          : row,
      ),
    );
  }

  protected invoiceOptionsForRow(rowId: number): PaymentApplicationReassignInvoiceOption[] {
    const currentRow = this.rows().find((row) => row.rowId === rowId);
    const selectedByOtherRows = new Set(
      this.rows()
        .filter((row) => row.rowId !== rowId)
        .map((row) => row.accountsReceivableInvoiceId)
        .filter((id): id is number => id !== null),
    );

    return this.invoiceOptions().filter(
      (option) =>
        option.id === currentRow?.accountsReceivableInvoiceId ||
        !selectedByOtherRows.has(option.id),
    );
  }

  protected hasUnusedInvoiceOptions(): boolean {
    const selectedIds = new Set(
      this.rows()
        .map((row) => row.accountsReceivableInvoiceId)
        .filter((id): id is number => id !== null),
    );

    return this.invoiceOptions().some((option) => !selectedIds.has(option.id));
  }

  protected maxApplicableForInvoice(invoiceId: number): number {
    const option = this.invoiceOptions().find((item) => item.id === invoiceId);
    const currentApplied = this.currentAppliedByInvoice()[invoiceId] ?? 0;
    const currentPreviousBalance = this.currentPreviousBalanceByInvoice()[invoiceId] ?? 0;
    const balanceAfterReversal = this.roundMoney((option?.outstandingBalance ?? 0) + currentApplied);

    return this.roundMoney(Math.max(balanceAfterReversal, currentPreviousBalance));
  }

  protected estimatedNewBalance(row: PaymentApplicationReassignRow): number {
    if (row.accountsReceivableInvoiceId === null) {
      return 0;
    }

    return this.roundMoney(this.maxApplicableForInvoice(row.accountsReceivableInvoiceId) - row.amount);
  }

  protected applicationInvoiceLabel(invoiceId: number): string {
    return this.invoiceOptions().find((option) => option.id === invoiceId)?.label ?? `CxC #${invoiceId}`;
  }

  protected invoiceDetail(invoiceId: number): string {
    const option = this.invoiceOptions().find((item) => item.id === invoiceId);
    if (!option) {
      return 'Factura no disponible en el workspace';
    }

    const dates = [option.issuedAtUtc, option.dueAtUtc]
      .filter((value): value is string => !!value)
      .map((value) => this.formatCalendarDate(value));
    const dateText = dates.length ? dates.join(' / ') : 'Sin fechas';
    return `${option.status} · ${dateText}`;
  }

  protected confirmSubmit(): void {
    if (!this.canSubmit()) {
      return;
    }

    const applications: ReassignAccountsReceivablePaymentApplicationRowRequest[] = [];
    for (const row of this.rows()) {
      if (row.accountsReceivableInvoiceId === null) {
        return;
      }

      applications.push({
        accountsReceivableInvoiceId: row.accountsReceivableInvoiceId,
        appliedAmount: this.roundMoney(row.amount),
      });
    }

    this.submitted.emit({
      reason: this.reasonText().trim(),
      applications,
    });
  }

  protected cancelWhenIdle(): void {
    if (this.submitting()) {
      return;
    }

    this.cancelled.emit();
  }

  private resetFromPayment(): void {
    this.nextRowId = 0;
    this.reasonText.set('');
    this.rows.set(
      this.currentApplications().map((application) => ({
        rowId: ++this.nextRowId,
        accountsReceivableInvoiceId: application.accountsReceivableInvoiceId,
        amount: this.roundMoney(application.appliedAmount),
        amountText: this.formatEditableAmount(application.appliedAmount),
      })),
    );
  }

  private buildInvoiceOptions(): PaymentApplicationReassignInvoiceOption[] {
    const options = new Map<number, PaymentApplicationReassignInvoiceOption>();
    const currentAppliedInvoiceIds = new Set(
      this.currentApplications().map((application) => application.accountsReceivableInvoiceId),
    );

    for (const invoice of this.candidateInvoices()) {
      const invoiceId = invoice.accountsReceivableInvoiceId;
      const isCurrentlyApplied = currentAppliedInvoiceIds.has(invoiceId);

      if (!this.isSelectableInvoice(invoice, isCurrentlyApplied)) {
        continue;
      }

      options.set(invoiceId, {
        id: invoiceId,
        label: this.formatFiscalLabel(invoice),
        fiscalUuid: invoice.fiscalUuid,
        issuedAtUtc: invoice.issuedAtUtc,
        dueAtUtc: invoice.dueAtUtc,
        outstandingBalance: invoice.outstandingBalance,
        status: invoice.status,
        fiscalReceiverId: invoice.fiscalReceiverId,
        currencyCode: invoice.currencyCode,
      });
    }

    for (const application of this.currentApplications()) {
      if (options.has(application.accountsReceivableInvoiceId)) {
        continue;
      }

      options.set(application.accountsReceivableInvoiceId, {
        id: application.accountsReceivableInvoiceId,
        label: `CxC #${application.accountsReceivableInvoiceId}`,
        outstandingBalance: application.newBalance,
        status: application.newBalance > 0 ? 'PartiallyPaid' : 'Paid',
        fiscalReceiverId: this.fiscalReceiverId(),
        currencyCode: this.payment().currencyCode,
      });
    }

    return [...options.values()].sort((left, right) => {
      const leftCurrent = currentAppliedInvoiceIds.has(left.id) ? 0 : 1;
      const rightCurrent = currentAppliedInvoiceIds.has(right.id) ? 0 : 1;
      if (leftCurrent !== rightCurrent) {
        return leftCurrent - rightCurrent;
      }

      return left.label.localeCompare(right.label, 'es-MX');
    });
  }

  private isSelectableInvoice(
    invoice: AccountsReceivablePortfolioItemResponse,
    isCurrentlyApplied: boolean,
  ): boolean {
    const receiverId = this.fiscalReceiverId();
    const sameReceiver = receiverId === null || invoice.fiscalReceiverId === receiverId;
    const currencyIsMxn = !invoice.currencyCode || invoice.currencyCode === 'MXN';
    const isCancelled = invoice.status === 'Cancelled';
    const isOpen = invoice.outstandingBalance > 0 && invoice.status !== 'Paid';

    return sameReceiver && currencyIsMxn && !isCancelled && (isOpen || isCurrentlyApplied);
  }

  private buildValidationMessages(): string[] {
    const messages: string[] = [];
    const trimmedReason = this.reasonText().trim();
    const rows = this.rows();
    const selectedInvoiceIds = rows
      .map((row) => row.accountsReceivableInvoiceId)
      .filter((id): id is number => id !== null);
    const duplicateInvoiceIds = selectedInvoiceIds.filter(
      (id, index) => selectedInvoiceIds.indexOf(id) !== index,
    );

    if (trimmedReason.length < 10) {
      messages.push('Captura un motivo de al menos 10 caracteres.');
    }
    if (trimmedReason.length > 1000) {
      messages.push('El motivo no puede exceder 1000 caracteres.');
    }
    if (!rows.length) {
      messages.push('Agrega al menos una factura.');
    }
    if (rows.some((row) => row.accountsReceivableInvoiceId === null)) {
      messages.push('Selecciona una factura en cada fila.');
    }
    if (rows.some((row) => row.amount <= 0)) {
      messages.push('Cada importe debe ser mayor a cero.');
    }
    if (duplicateInvoiceIds.length > 0) {
      messages.push('No repitas facturas en la nueva distribución.');
    }
    if (this.totalAssigned() <= 0) {
      messages.push('El total asignado debe ser mayor a cero.');
    }
    if (this.totalAssigned() > this.payment().amount) {
      messages.push('El total asignado no puede exceder el importe del pago.');
    }
    if (
      rows.some(
        (row) =>
          row.accountsReceivableInvoiceId !== null &&
          row.amount > this.maxApplicableForInvoice(row.accountsReceivableInvoiceId),
      )
    ) {
      messages.push(
        'El importe por factura no puede exceder el saldo disponible después de revertir la aplicación actual.',
      );
    }

    return messages;
  }

  private sumCurrentApplicationsByInvoice(
    selector: (application: AccountsReceivablePaymentApplicationResponse) => number,
  ): Record<number, number> {
    const values: Record<number, number> = {};
    for (const application of this.currentApplications()) {
      values[application.accountsReceivableInvoiceId] = this.roundMoney(
        (values[application.accountsReceivableInvoiceId] ?? 0) + selector(application),
      );
    }

    return values;
  }

  private maxCurrentApplicationsByInvoice(
    selector: (application: AccountsReceivablePaymentApplicationResponse) => number,
  ): Record<number, number> {
    const values: Record<number, number> = {};
    for (const application of this.currentApplications()) {
      values[application.accountsReceivableInvoiceId] = Math.max(
        values[application.accountsReceivableInvoiceId] ?? 0,
        selector(application),
      );
    }

    return values;
  }

  private formatFiscalLabel(item: AccountsReceivablePortfolioItemResponse): string {
    const series = item.fiscalSeries?.trim();
    const folio = item.fiscalFolio?.trim();
    if (series || folio) {
      return [series, folio].filter(Boolean).join('-');
    }

    return item.fiscalDocumentId
      ? `CFDI #${item.fiscalDocumentId}`
      : `CxC #${item.accountsReceivableInvoiceId}`;
  }

  private formatCalendarDate(value: string): string {
    return value.slice(0, 10);
  }

  private roundMoney(value: number): number {
    return Math.round((value + Number.EPSILON) * 100) / 100;
  }

  private formatEditableAmount(value: number): string {
    return this.roundMoney(value).toFixed(2);
  }
}
