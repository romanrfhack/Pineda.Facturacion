import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { firstValueFrom } from 'rxjs';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { AccountsReceivableApiService } from '../infrastructure/accounts-receivable-api.service';
import {
  ReceivablesSummaryCandidateResponse,
  ReceivablesSummaryCandidatesResponse,
  ReceivablesSummaryFormat,
  ReceivablesSummaryIncludeOptionsRequest,
  ReceivablesSummaryPreviewResponse,
  ReceivablesSummaryRequest,
  ReceivablesSummaryScope,
  SendReceivablesSummaryResponse,
} from '../models/accounts-receivable.models';
import {
  formatReceivablesSummaryMoney,
  selectReceivablesSummaryInvoices,
  summarizeReceivablesSummaryInvoices,
} from '../application/receivables-summary-selection';
import {
  findInvalidEmailRecipients,
  formatEmailRecipientsInput,
  joinEmailRecipients,
  parseEmailRecipients,
} from '../../../shared/utils/email-recipients';

type SummaryStep = 1 | 2 | 3;

@Component({
  selector: 'app-send-receivables-summary-modal',
  imports: [FormsModule, CurrencyPipe, DatePipe],
  template: `
    @if (open()) {
      <section class="modal-backdrop" (click)="requestClose()">
        <section
          class="modal-card receivables-summary-modal"
          role="dialog"
          aria-modal="true"
          aria-label="Enviar resumen de adeudos"
          (click)="$event.stopPropagation()"
        >
          <header class="modal-header">
            <div>
              <p class="eyebrow">Cuentas por cobrar</p>
              <h3>Enviar resumen de adeudos</h3>
              @if (candidateResponse(); as candidates) {
                <p class="helper">
                  {{ candidates.receiver.legalName }} · {{ candidates.receiver.rfc }}
                </p>
              }
            </div>
            <button type="button" class="secondary compact" (click)="requestClose()" [disabled]="sending()">
              Cerrar
            </button>
          </header>

          <nav class="stepper" aria-label="Pasos del resumen de adeudos">
            <button type="button" [class.active]="step() === 1" (click)="goToStep(1)" [disabled]="sending()">1. Adeudos</button>
            <button type="button" [class.active]="step() === 2" (click)="goToStep(2)" [disabled]="!canLeaveSelection() || sending()">2. Envío</button>
            <button type="button" [class.active]="step() === 3" (click)="goToStep(3)" [disabled]="!preview() || sending()">3. Vista previa</button>
          </nav>

          @if (loadingCandidates()) {
            <p class="helper">Cargando facturas elegibles...</p>
          } @else if (candidateError()) {
            <section class="status-panel status-panel-warning">
              {{ candidateError() }}
            </section>
          } @else if (candidateResponse(); as candidates) {
            @if (!candidates.invoices.length) {
              <section class="status-panel status-panel-warning">
                No existen facturas pendientes elegibles para este receptor.
              </section>
            }

            @if (errorMessage()) {
              <section class="status-panel status-panel-warning">{{ errorMessage() }}</section>
            }

            @if (step() === 1) {
              <section class="modal-step">
                <div class="option-grid">
                  <label class="choice-card">
                    <input type="radio" name="summaryScope" [checked]="scope() === 'all_pending'" (change)="setScope('all_pending')" />
                    <span>Todas las facturas pendientes</span>
                  </label>
                  <label class="choice-card">
                    <input type="radio" name="summaryScope" [checked]="scope() === 'overdue'" (change)="setScope('overdue')" />
                    <span>Solo facturas vencidas</span>
                  </label>
                  <label class="choice-card">
                    <input type="radio" name="summaryScope" [checked]="scope() === 'manual'" (change)="setScope('manual')" />
                    <span>Facturas seleccionadas manualmente</span>
                  </label>
                  <label class="choice-card" [class.disabled]="!currentSelectionEligibleCount()">
                    <input
                      type="radio"
                      name="summaryScope"
                      [checked]="scope() === 'current_selection'"
                      (change)="setScope('current_selection')"
                      [disabled]="!currentSelectionEligibleCount()"
                    />
                    <span>Usar selección actual ({{ currentSelectionEligibleCount() }})</span>
                  </label>
                </div>

                <section class="summary-grid compact-summary">
                  <article class="summary-card">
                    <strong>Facturas seleccionadas</strong>
                    <div class="summary-card-value">{{ selectionSummary().invoiceCount }}</div>
                  </article>
                  <article class="summary-card">
                    <strong>Saldo total</strong>
                    <div class="summary-card-value">{{ formatTotals('outstandingBalance') }}</div>
                  </article>
                  <article class="summary-card">
                    <strong>Saldo vencido</strong>
                    <div class="summary-card-value">{{ formatTotals('overdueBalance') }}</div>
                  </article>
                  <article class="summary-card">
                    <strong>Saldo por vencer</strong>
                    <div class="summary-card-value">{{ formatTotals('currentBalance') }}</div>
                  </article>
                </section>

                @if (selectionSummary().totalsByCurrency.length > 1) {
                  <div class="currency-strip">
                    @for (total of selectionSummary().totalsByCurrency; track total.currencyCode) {
                      <span>{{ total.currencyCode }} · {{ total.invoiceCount }} factura(s) · {{ total.outstandingBalance | currency: total.currencyCode : 'symbol' : '1.2-2' }}</span>
                    }
                  </div>
                }

                <div class="table-wrap">
                  <table class="portfolio receiver-workspace-table">
                    <thead>
                      <tr>
                        <th></th>
                        <th>Factura</th>
                        <th>Emisión</th>
                        <th>Vencimiento</th>
                        <th>Moneda</th>
                        <th>Total</th>
                        <th>Pagado</th>
                        <th>Saldo</th>
                        <th>Estado</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (invoice of candidates.invoices; track invoice.accountsReceivableInvoiceId) {
                        <tr [class.is-overdue]="invoice.isOverdue">
                          <td data-label="Seleccionar">
                            <input
                              type="checkbox"
                              [checked]="isInvoiceSelected(invoice.accountsReceivableInvoiceId)"
                              (change)="toggleInvoice(invoice.accountsReceivableInvoiceId, $any($event.target).checked)"
                            />
                          </td>
                          <td data-label="Factura">
                            <strong>{{ formatInvoiceLabel(invoice) }}</strong>
                            <div class="subtle">{{ invoice.fiscalUuid || 'UUID pendiente' }}</div>
                          </td>
                          <td data-label="Emisión">{{ invoice.issuedAtUtc | date: 'yyyy-MM-dd' }}</td>
                          <td data-label="Vencimiento">
                            {{ invoice.dueAtUtc ? (invoice.dueAtUtc | date: 'yyyy-MM-dd') : 'Sin fecha de vencimiento' }}
                            @if (invoice.isOverdue) {
                              <div class="subtle">{{ invoice.daysPastDue }} día(s) vencida</div>
                            }
                          </td>
                          <td data-label="Moneda">{{ invoice.currencyCode }}</td>
                          <td data-label="Total">{{ invoice.total | currency: invoice.currencyCode : 'symbol' : '1.2-2' }}</td>
                          <td data-label="Pagado">{{ invoice.paidTotal | currency: invoice.currencyCode : 'symbol' : '1.2-2' }}</td>
                          <td data-label="Saldo">{{ invoice.outstandingBalance | currency: invoice.currencyCode : 'symbol' : '1.2-2' }}</td>
                          <td data-label="Estado">
                            <span class="badge" [attr.data-status]="invoice.status">{{ invoice.status }}</span>
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </section>
            }

            @if (step() === 2) {
              <section class="modal-step">
                <p class="helper delivery-helper">
                  El correo es opcional para compartir por WhatsApp o descargar. Solo será obligatorio si eliges enviar por correo.
                </p>
                <div class="filter-grid">
                  <label class="wide-field">
                    <span>Para</span>
                    <input type="text" [(ngModel)]="toInput" placeholder="correo@cliente.com" />
                  </label>
                  <label>
                    <span>CC</span>
                    <input type="text" [(ngModel)]="ccInput" placeholder="opcional" />
                  </label>
                  <label>
                    <span>CCO</span>
                    <input type="text" [(ngModel)]="bccInput" placeholder="opcional" />
                  </label>
                  <label class="wide-field">
                    <span>Asunto</span>
                    <input type="text" [(ngModel)]="subject" />
                  </label>
                  <label class="wide-field">
                    <span>Mensaje inicial</span>
                    <textarea rows="4" [(ngModel)]="message"></textarea>
                  </label>
                  <label>
                    <span>Formato</span>
                    <select [(ngModel)]="format">
                      <option value="html">Correo HTML con resumen</option>
                      <option value="html_with_pdf">Correo HTML + PDF adjunto</option>
                      <option value="pdf">PDF adjunto con mensaje breve</option>
                    </select>
                  </label>
                </div>

                <section class="checkbox-grid">
                  <label><input type="checkbox" [(ngModel)]="includeOptions.invoiceTable" /> Incluir tabla de facturas</label>
                  <label><input type="checkbox" [(ngModel)]="includeOptions.totalsByCurrency" /> Incluir totales por moneda</label>
                  <label><input type="checkbox" [(ngModel)]="includeOptions.highlightOverdue" /> Resaltar vencidas</label>
                  <label><input type="checkbox" [(ngModel)]="includeOptions.paymentInstructions" /> Instrucciones de pago</label>
                  <label><input type="checkbox" [(ngModel)]="includeOptions.receiverFiscalData" /> Datos fiscales del receptor</label>
                  <label><input type="checkbox" [(ngModel)]="includeOptions.issuerData" /> Datos del emisor</label>
                  <label><input type="checkbox" [(ngModel)]="includeOptions.invoiceLinks" /> Enlaces a comprobantes</label>
                </section>
              </section>
            }

            @if (step() === 3) {
              <section class="modal-step">
                @if (previewing()) {
                  <p class="helper">Generando vista previa...</p>
                } @else if (preview(); as currentPreview) {
                  <section class="final-summary">
                    <article><strong>Destinatarios</strong><span>{{ currentPreview.finalSummary?.to?.join(', ') || 'Sin destinatario de correo' }}</span></article>
                    <article><strong>Facturas</strong><span>{{ currentPreview.finalSummary?.invoiceCount ?? selectionSummary().invoiceCount }}</span></article>
                    <article><strong>Total</strong><span>{{ formatTotals('outstandingBalance') }}</span></article>
                    <article><strong>Formato</strong><span>{{ formatLabel(currentPreview.finalSummary?.format || format) }}</span></article>
                  </section>

                  <section class="pdf-actions-panel">
                    <div class="actions compact-actions">
                      <button
                        type="button"
                        class="whatsapp-action"
                        (click)="sharePdf()"
                        [disabled]="pdfAction() !== null || !currentPreview.pdfBase64"
                      >
                        {{ pdfAction() === 'share' ? 'Abriendo opciones...' : 'Compartir por WhatsApp' }}
                      </button>
                      <button
                        type="button"
                        class="secondary"
                        (click)="downloadPrintPdf()"
                        [disabled]="pdfAction() !== null || !currentPreview.printPdfBase64"
                      >
                        {{ pdfAction() === 'download' ? 'Descargando...' : 'Descargar para imprimir' }}
                      </button>
                    </div>
                    <p class="helper">
                      WhatsApp usa la versión digital del resumen. La descarga usa una versión tamaño carta, con fondo blanco y bajo consumo de tinta.
                    </p>
                    <p class="helper">
                      En celular, selecciona WhatsApp y después el contacto. Si el navegador no permite compartir archivos,
                      el PDF se descargará y WhatsApp se abrirá para que lo adjuntes manualmente.
                    </p>
                  </section>

                  @if (currentPreview.pdfErrorMessage) {
                    <section class="status-panel status-panel-warning">{{ currentPreview.pdfErrorMessage }}</section>
                  }
                  @if (currentPreview.printPdfErrorMessage) {
                    <section class="status-panel status-panel-warning">{{ currentPreview.printPdfErrorMessage }}</section>
                  }
                  @if (shareStatusMessage()) {
                    <section class="status-panel status-panel-success">{{ shareStatusMessage() }}</section>
                  }

                  <section class="email-preview" [innerHTML]="previewHtml()"></section>
                } @else {
                  <p class="helper">Genera la vista previa para confirmar el envío.</p>
                }
              </section>
            }
          }

          <footer class="modal-footer">
            <button type="button" class="secondary" (click)="back()" [disabled]="step() === 1 || sending() || previewing()">Atrás</button>
            @if (step() < 3) {
              <button type="button" (click)="next()" [disabled]="loadingCandidates() || previewing() || sending()">
                {{ step() === 2 ? 'Generar vista previa' : 'Siguiente' }}
              </button>
            } @else {
              <button type="button" (click)="send()" [disabled]="sending() || previewing() || !preview()?.success">
                {{ sending() ? 'Enviando...' : 'Enviar por correo' }}
              </button>
            }
          </footer>
        </section>
      </section>
    }
  `,
  styles: [
    `
      .modal-backdrop { position:fixed; inset:0; background:rgba(24,37,51,.52); display:grid; place-items:center; padding:1rem; z-index:70; }
      .modal-card { width:min(1180px, 100%); max-height:calc(100vh - 2rem); overflow:auto; border:1px solid #d8d1c2; border-radius:1rem; background:#fff; padding:1rem; display:grid; gap:1rem; box-shadow:0 24px 60px rgba(24,37,51,.24); }
      .modal-header, .modal-footer, .actions { display:flex; justify-content:space-between; gap:.75rem; align-items:flex-start; flex-wrap:wrap; }
      .modal-footer { justify-content:flex-end; border-top:1px solid #ece3d3; padding-top:1rem; }
      .eyebrow { margin:0; text-transform:uppercase; letter-spacing:.12em; font-size:.72rem; color:#8a6a32; }
      h3 { margin:.25rem 0 0; }
      .helper, .subtle { color:#5f6b76; margin:.25rem 0 0; }
      .stepper { display:grid; grid-template-columns:repeat(3, 1fr); gap:.5rem; }
      .stepper button { background:#eef1f4; color:#182533; }
      .stepper button.active { background:#182533; color:#fff; }
      button, a { border:none; border-radius:.8rem; padding:.75rem 1rem; background:#182533; color:#fff; cursor:pointer; text-decoration:none; display:inline-flex; justify-content:center; }
      button.secondary { background:#eef1f4; color:#182533; }
      button.compact { padding:.55rem .8rem; }
      button:disabled, .choice-card.disabled { opacity:.58; cursor:not-allowed; }
      .modal-step { display:grid; gap:1rem; }
      .option-grid, .checkbox-grid, .final-summary { display:grid; gap:.75rem; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); }
      .choice-card, .checkbox-grid label { border:1px solid #ece3d3; border-radius:.9rem; padding:.8rem; background:#fffdf8; display:flex; gap:.55rem; align-items:center; }
      .summary-grid { display:grid; gap:.75rem; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); }
      .summary-card, .final-summary article { border:1px solid #ece3d3; border-radius:.85rem; padding:.85rem; background:#fffdf8; display:grid; gap:.35rem; }
      .summary-card-value { font-size:1.02rem; font-weight:700; }
      .currency-strip { display:flex; flex-wrap:wrap; gap:.5rem; }
      .currency-strip span, .badge { display:inline-flex; padding:.3rem .55rem; border-radius:999px; font-size:.78rem; background:#eef1f4; color:#243444; }
      .badge[data-status='Open'] { background:#fff2d8; color:#8a5a00; }
      .badge[data-status='PartiallyPaid'] { background:#dff2ea; color:#116149; }
      .table-wrap { overflow:auto; }
      .portfolio { width:100%; border-collapse:collapse; }
      .portfolio th, .portfolio td { text-align:left; padding:.7rem; border-top:1px solid #ece3d3; vertical-align:top; }
      .portfolio tr.is-overdue td { background:#fff7f5; }
      .filter-grid { display:grid; gap:.9rem; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); }
      label { display:grid; gap:.35rem; }
      label span { font-size:.82rem; color:#495766; }
      input, select, textarea { border:1px solid #d8d1c2; border-radius:.75rem; padding:.7rem .85rem; background:#fffdf8; font:inherit; }
      .wide-field { grid-column:1 / -1; }
      .status-panel { border-radius:.9rem; padding:.9rem 1rem; border:1px solid #ecd9aa; background:#fff8ea; color:#4d3a16; }
      .status-panel-success { border-color:#b9ddca; background:#eef9f3; color:#16583f; }
      .email-preview { border:1px solid #d8d1c2; border-radius:1rem; background:#f7f4ed; max-height:480px; overflow:auto; }
      .compact-actions { justify-content:flex-start; }
      .delivery-helper { border-left:3px solid #2d5f8b; padding:.25rem 0 .25rem .75rem; }
      .pdf-actions-panel { display:grid; gap:.35rem; }
      button.whatsapp-action { background:#1f7a55; }
      @media (max-width:720px) {
        .stepper { grid-template-columns:1fr; }
        .modal-header, .modal-footer { flex-direction:column; align-items:stretch; }
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SendReceivablesSummaryModalComponent {
  private readonly api = inject(AccountsReceivableApiService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly open = input(false);
  readonly receiverId = input.required<number>();
  readonly currentSelection = input<readonly number[]>([]);
  readonly closed = output<void>();
  readonly sent = output<SendReceivablesSummaryResponse>();

  protected readonly step = signal<SummaryStep>(1);
  protected readonly loadingCandidates = signal(false);
  protected readonly previewing = signal(false);
  protected readonly sending = signal(false);
  protected readonly candidateResponse = signal<ReceivablesSummaryCandidatesResponse | null>(null);
  protected readonly candidateError = signal<string | null>(null);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly scope = signal<ReceivablesSummaryScope>('all_pending');
  protected readonly manualSelectedIds = signal<number[]>([]);
  protected readonly preview = signal<ReceivablesSummaryPreviewResponse | null>(null);
  protected readonly pdfAction = signal<'download' | 'share' | null>(null);
  protected readonly shareStatusMessage = signal<string | null>(null);
  protected readonly previewHtml = computed<SafeHtml>(() =>
    this.sanitizer.bypassSecurityTrustHtml(this.preview()?.html || ''),
  );

  protected toInput = '';
  protected ccInput = '';
  protected bccInput = '';
  protected subject = '';
  protected message = '';
  protected format: ReceivablesSummaryFormat = 'html';
  protected readonly includeOptions: ReceivablesSummaryIncludeOptionsRequest = {
    invoiceTable: true,
    totalsByCurrency: true,
    highlightOverdue: true,
    paymentInstructions: true,
    receiverFiscalData: true,
    issuerData: true,
    invoiceLinks: true,
  };

  protected readonly currentSelectionEligibleCount = computed(() => {
    const currentSelection = new Set(this.currentSelection());
    return (this.candidateResponse()?.invoices ?? []).filter((invoice) =>
      currentSelection.has(invoice.accountsReceivableInvoiceId),
    ).length;
  });

  protected readonly selectedInvoices = computed(() =>
    selectReceivablesSummaryInvoices(
      this.candidateResponse()?.invoices ?? [],
      this.scope(),
      this.effectiveSelectedIds(),
    ),
  );

  protected readonly selectionSummary = computed(() =>
    summarizeReceivablesSummaryInvoices(this.selectedInvoices()),
  );

  private readonly loadEffect = effect(() => {
    const receiverId = this.receiverId();
    if (this.open() && receiverId) {
      untracked(() => void this.loadCandidates());
    }
  });

  protected setScope(scope: ReceivablesSummaryScope): void {
    this.scope.set(scope);
    this.errorMessage.set(null);
    this.preview.set(null);
  }

  protected isInvoiceSelected(invoiceId: number): boolean {
    return this.selectedInvoices().some((invoice) => invoice.accountsReceivableInvoiceId === invoiceId);
  }

  protected toggleInvoice(invoiceId: number, checked: boolean): void {
    this.scope.set('manual');
    this.preview.set(null);
    const current = new Set(this.manualSelectedIds());
    if (checked) {
      current.add(invoiceId);
    } else {
      current.delete(invoiceId);
    }
    this.manualSelectedIds.set([...current]);
  }

  protected formatInvoiceLabel(invoice: ReceivablesSummaryCandidateResponse): string {
    const series = invoice.fiscalSeries?.trim();
    const folio = invoice.fiscalFolio?.trim();
    if (series || folio) {
      return [series, folio].filter(Boolean).join('-');
    }
    return invoice.fiscalDocumentId ? `CFDI #${invoice.fiscalDocumentId}` : `CxC #${invoice.accountsReceivableInvoiceId}`;
  }

  protected formatTotals(key: 'outstandingBalance' | 'overdueBalance' | 'currentBalance'): string {
    const totals = this.selectionSummary().totalsByCurrency;
    if (!totals.length) {
      return '0.00 MXN';
    }
    return totals.map((total) => formatReceivablesSummaryMoney(total[key], total.currencyCode)).join(' · ');
  }

  protected formatLabel(format: string): string {
    switch (format) {
      case 'HtmlWithPdf':
      case 'html_with_pdf':
        return 'Correo HTML + PDF';
      case 'Pdf':
      case 'pdf':
        return 'PDF adjunto';
      case 'Html':
      case 'html':
      default:
        return 'Correo HTML';
    }
  }

  protected canLeaveSelection(): boolean {
    return this.selectedInvoices().length > 0;
  }

  protected async goToStep(step: SummaryStep): Promise<void> {
    if (step === 2 && !this.validateSelection()) {
      return;
    }
    if (step === 3) {
      if (!this.validateSelection() || !this.validatePreviewConfiguration()) {
        return;
      }
      if (!this.preview()) {
        await this.generatePreview();
      }
    }
    this.step.set(step);
  }

  protected async next(): Promise<void> {
    if (this.step() === 1) {
      if (this.validateSelection()) {
        this.step.set(2);
      }
      return;
    }

    if (this.step() === 2 && this.validatePreviewConfiguration()) {
      await this.generatePreview();
    }
  }

  protected back(): void {
    this.errorMessage.set(null);
    this.step.set(this.step() === 3 ? 2 : 1);
  }

  protected requestClose(): void {
    if (this.sending()) {
      return;
    }
    this.closed.emit();
  }

  protected async send(): Promise<void> {
    if (this.sending() || !this.preview()?.success || !this.validateEmailConfiguration()) {
      return;
    }

    this.sending.set(true);
    this.errorMessage.set(null);
    try {
      const response = await firstValueFrom(
        this.api.sendReceivablesSummary(this.receiverId(), this.buildRequest()),
      );
      if (!response.success) {
        this.errorMessage.set(response.errorMessage || 'No fue posible enviar el resumen.');
        return;
      }
      this.sent.emit(response);
    } catch (error) {
      this.errorMessage.set(
        describeOperationError(error, 'No se pudo enviar el resumen de adeudos.'),
      );
    } finally {
      this.sending.set(false);
    }
  }

  protected downloadPrintPdf(): void {
    const file = this.buildPdfFile('print');
    if (!file || this.pdfAction() !== null) {
      return;
    }

    this.pdfAction.set('download');
    this.errorMessage.set(null);
    this.shareStatusMessage.set(null);
    try {
      this.downloadFile(file);
      this.shareStatusMessage.set('PDF para impresión descargado correctamente.');
    } finally {
      this.pdfAction.set(null);
    }
  }

  protected async sharePdf(): Promise<void> {
    const file = this.buildPdfFile('digital');
    if (!file || this.pdfAction() !== null) {
      return;
    }

    this.pdfAction.set('share');
    this.errorMessage.set(null);
    this.shareStatusMessage.set(null);

    const shareData: ShareData = {
      files: [file],
      title: 'Resumen de adeudos pendientes',
      text: `Resumen de adeudos pendientes de ${this.candidateResponse()?.receiver.legalName || 'cliente'}.`,
    };

    try {
      if (
        typeof navigator.share === 'function'
        && typeof navigator.canShare === 'function'
        && navigator.canShare(shareData)
      ) {
        await navigator.share(shareData);
        this.shareStatusMessage.set('Archivo compartido correctamente.');
        return;
      }

      this.openWhatsAppFallback(file);
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') {
        return;
      }

      this.openWhatsAppFallback(file);
    } finally {
      this.pdfAction.set(null);
    }
  }

  private async loadCandidates(): Promise<void> {
    if (this.loadingCandidates()) {
      return;
    }

    this.resetStateForOpen();
    this.loadingCandidates.set(true);
    try {
      const response = await firstValueFrom(this.api.getReceivablesSummaryCandidates(this.receiverId()));
      this.candidateResponse.set(response);
      this.toInput = response.defaultTo.length
        ? joinEmailRecipients(response.defaultTo)
        : formatEmailRecipientsInput(response.receiver.email);
      this.subject = response.defaultSubject;
      this.message = response.defaultMessage;

      const eligibleCurrentSelection = response.invoices
        .filter((invoice) => this.currentSelection().includes(invoice.accountsReceivableInvoiceId))
        .map((invoice) => invoice.accountsReceivableInvoiceId);
      this.manualSelectedIds.set(eligibleCurrentSelection);
      this.scope.set(eligibleCurrentSelection.length ? 'current_selection' : 'all_pending');
    } catch (error) {
      this.candidateError.set(
        describeOperationError(
          error,
          'No se pudieron cargar las facturas candidatas para el resumen.',
        ),
      );
    } finally {
      this.loadingCandidates.set(false);
    }
  }

  private resetStateForOpen(): void {
    this.step.set(1);
    this.errorMessage.set(null);
    this.candidateError.set(null);
    this.preview.set(null);
    this.pdfAction.set(null);
    this.shareStatusMessage.set(null);
    this.ccInput = '';
    this.bccInput = '';
    this.format = 'html';
  }

  private async generatePreview(): Promise<void> {
    this.previewing.set(true);
    this.errorMessage.set(null);
    this.preview.set(null);
    this.shareStatusMessage.set(null);
    try {
      const response = await firstValueFrom(
        this.api.previewReceivablesSummary(this.receiverId(), this.buildRequest()),
      );
      if (!response.success) {
        this.errorMessage.set(response.errorMessage || 'No fue posible generar la vista previa.');
        return;
      }
      this.preview.set(response);
      this.step.set(3);
    } catch (error) {
      this.errorMessage.set(
        describeOperationError(error, 'No se pudo generar la vista previa del resumen de adeudos.'),
      );
    } finally {
      this.previewing.set(false);
    }
  }

  private validateSelection(): boolean {
    if (!this.candidateResponse()?.invoices.length) {
      this.errorMessage.set('No existen facturas pendientes elegibles para este receptor.');
      return false;
    }
    if (!this.selectedInvoices().length) {
      this.errorMessage.set(
        this.scope() === 'overdue'
          ? 'No existen facturas vencidas para este receptor.'
          : 'Selecciona al menos una factura para continuar.',
      );
      return false;
    }
    this.errorMessage.set(null);
    return true;
  }

  private validateEmailConfiguration(): boolean {
    if (!this.validatePreviewConfiguration()) {
      return false;
    }
    if (!parseEmailRecipients(this.toInput).length) {
      this.errorMessage.set('Captura al menos un correo válido en Para.');
      return false;
    }
    const invalid = [this.toInput, this.ccInput, this.bccInput]
      .flatMap((value) => findInvalidEmailRecipients(value));
    if (invalid.length) {
      this.errorMessage.set(`Correo inválido: ${invalid.join(', ')}`);
      return false;
    }
    this.errorMessage.set(null);
    return true;
  }

  private validatePreviewConfiguration(): boolean {
    if (!this.subject.trim()) {
      this.errorMessage.set('Captura el asunto del correo.');
      return false;
    }
    if (!this.message.trim()) {
      this.errorMessage.set('Captura el mensaje inicial.');
      return false;
    }
    this.errorMessage.set(null);
    return true;
  }

  private buildRequest(): ReceivablesSummaryRequest {
    return {
      receiverId: String(this.receiverId()),
      invoiceIds: this.selectedInvoices().map((invoice) => invoice.accountsReceivableInvoiceId),
      scope: this.scope(),
      to: parseEmailRecipients(this.toInput),
      cc: parseEmailRecipients(this.ccInput),
      bcc: parseEmailRecipients(this.bccInput),
      subject: this.subject.trim(),
      message: this.message.trim(),
      format: this.format,
      includeOptions: { ...this.includeOptions },
    };
  }

  private effectiveSelectedIds(): readonly number[] {
    return this.scope() === 'current_selection' ? this.currentSelection() : this.manualSelectedIds();
  }

  private buildPdfFile(variant: 'digital' | 'print'): File | null {
    const currentPreview = this.preview();
    const base64 = variant === 'print' ? currentPreview?.printPdfBase64 : currentPreview?.pdfBase64;
    const errorMessage = variant === 'print'
      ? currentPreview?.printPdfErrorMessage
      : currentPreview?.pdfErrorMessage;
    if (!base64) {
      this.errorMessage.set(errorMessage || 'El PDF no está disponible en esta vista previa.');
      return null;
    }

    try {
      const bytes = Uint8Array.from(atob(base64), (character) => character.charCodeAt(0));
      const fileName = variant === 'print'
        ? currentPreview?.printPdfFileName || 'resumen-adeudos-impresion.pdf'
        : currentPreview?.pdfFileName || 'resumen-adeudos.pdf';
      return new File([bytes], fileName, { type: 'application/pdf' });
    } catch {
      this.errorMessage.set('No fue posible preparar el archivo PDF.');
      return null;
    }
  }

  private downloadFile(file: File): void {
    const objectUrl = window.URL.createObjectURL(file);
    const link = document.createElement('a');
    link.href = objectUrl;
    link.download = file.name;
    link.click();
    window.URL.revokeObjectURL(objectUrl);
  }

  private openWhatsAppFallback(file: File): void {
    this.downloadFile(file);

    const text = 'Te comparto el resumen de adeudos pendientes. Adjuntaré el PDF descargado en este mensaje.';
    const whatsappWindow = window.open(
      `https://wa.me/?text=${encodeURIComponent(text)}`,
      '_blank',
      'noopener,noreferrer',
    );

    this.shareStatusMessage.set(
      whatsappWindow
        ? 'El PDF se descargó y WhatsApp se abrió. Selecciona el contacto y adjunta manualmente el archivo.'
        : 'El PDF se descargó. Abre WhatsApp, selecciona el contacto y adjunta manualmente el archivo.',
    );
  }
}

function describeOperationError(error: unknown, fallback: string): string {
  const message = extractApiErrorMessage(error, fallback).trim();
  if (!message || message === fallback) {
    return fallback;
  }

  return `${fallback} ${message}`;
}
