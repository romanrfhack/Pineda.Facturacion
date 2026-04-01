import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { PaymentComplementsApiService } from '../infrastructure/payment-complements-api.service';
import { ExternalRepBaseDocumentImportResponse } from '../models/payment-complements.models';

@Component({
  selector: 'app-external-rep-base-document-import-card',
  imports: [DecimalPipe],
  template: `
    <section class="card">
      <header class="header">
        <div>
          <p class="eyebrow">Facturas externas</p>
          <h2>Importar CFDI externo por XML</h2>
          <p class="helper">Esta fase valida y persiste el snapshot fiscal externo. No registra pagos ni emite REP todavía sobre estas facturas.</p>
        </div>
      </header>

      <div class="form">
        <label class="file-input">
          <span>Archivo XML</span>
          <input type="file" accept=".xml,application/xml,text/xml" (change)="onFileSelected($event)" [disabled]="submitting()" />
        </label>

        <div class="file-summary">
          <strong>{{ selectedFileName() || 'Sin archivo seleccionado' }}</strong>
          <span class="helper">Se validará tipo de comprobante, UUID, PPD, forma 99, moneda y estatus SAT cuando esté disponible.</span>
        </div>

        @if (errorMessage()) {
          <p class="error">{{ errorMessage() }}</p>
        }

        <div class="actions">
          <button type="button" (click)="submit()" [disabled]="!selectedFile() || submitting()">
            {{ submitting() ? 'Importando...' : 'Importar XML' }}
          </button>
          <button type="button" class="secondary" (click)="clear()" [disabled]="submitting()">Limpiar</button>
        </div>
      </div>

      @if (result(); as result) {
        <section class="result-card" [class.result-accepted]="result.validationStatus === 'Accepted'" [class.result-blocked]="result.validationStatus === 'Blocked'" [class.result-rejected]="result.validationStatus === 'Rejected'">
          <div class="result-head">
            <strong>{{ result.validationStatus }}</strong>
            <span>{{ result.reasonCode }}</span>
          </div>
          <p class="result-message">{{ result.reasonMessage }}</p>

          <dl>
            <div><dt>Outcome</dt><dd>{{ result.outcome }}</dd></div>
            <div><dt>ExternalRepBaseDocumentId</dt><dd>{{ result.externalRepBaseDocumentId ?? '—' }}</dd></div>
            <div><dt>UUID</dt><dd>{{ result.uuid || '—' }}</dd></div>
            <div><dt>RFC emisor</dt><dd>{{ result.issuerRfc || '—' }}</dd></div>
            <div><dt>RFC receptor</dt><dd>{{ result.receiverRfc || '—' }}</dd></div>
            <div><dt>Método / Forma</dt><dd>{{ result.paymentMethodSat || '—' }} / {{ result.paymentFormSat || '—' }}</dd></div>
            <div><dt>Moneda</dt><dd>{{ result.currencyCode || '—' }}</dd></div>
            <div><dt>Total</dt><dd>{{ result.total != null ? (result.total | number:'1.2-2') : '—' }}</dd></div>
            <div><dt>Duplicado</dt><dd>{{ result.isDuplicate ? 'Sí' : 'No' }}</dd></div>
          </dl>
        </section>
      }
    </section>
  `,
  styles: [`
    .card { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; display:grid; gap:1rem; }
    .header { display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    .helper { margin:0; color:#5f6b76; }
    .form { display:grid; gap:0.85rem; }
    .file-input { display:grid; gap:0.35rem; }
    input[type="file"] { font:inherit; }
    .file-summary { display:grid; gap:0.25rem; }
    .actions { display:flex; flex-wrap:wrap; gap:0.75rem; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    button.secondary { background:#d8c49b; color:#182533; }
    button:disabled { opacity:0.6; cursor:not-allowed; }
    .error { margin:0; color:#7a2020; }
    .result-card { border:1px solid #ece5d7; border-radius:0.9rem; padding:1rem; display:grid; gap:0.75rem; }
    .result-accepted { background:#eef8f1; }
    .result-blocked { background:#fff6e5; }
    .result-rejected { background:#fdeaea; }
    .result-head { display:flex; justify-content:space-between; gap:1rem; align-items:center; }
    .result-message { margin:0; font-weight:600; color:#182533; }
    dl { display:grid; grid-template-columns:repeat(auto-fit, minmax(200px, 1fr)); gap:0.75rem; margin:0; }
    dt { font-size:0.8rem; color:#5f6b76; }
    dd { margin:0.15rem 0 0; font-weight:600; color:#182533; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ExternalRepBaseDocumentImportCardComponent {
  private readonly api = inject(PaymentComplementsApiService);
  private readonly feedbackService = inject(FeedbackService);

  protected readonly selectedFile = signal<File | null>(null);
  protected readonly selectedFileName = signal<string>('');
  protected readonly submitting = signal(false);
  protected readonly result = signal<ExternalRepBaseDocumentImportResponse | null>(null);
  protected readonly errorMessage = signal<string | null>(null);

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    const file = input?.files?.[0] ?? null;
    this.selectedFile.set(file);
    this.selectedFileName.set(file?.name ?? '');
    this.result.set(null);
    this.errorMessage.set(null);
  }

  protected async submit(): Promise<void> {
    const file = this.selectedFile();
    if (!file) {
      this.errorMessage.set('Selecciona un archivo XML antes de importar.');
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    try {
      const response = await firstValueFrom(this.api.importExternalBaseDocumentXml(file));
      this.result.set(response);

      const level = response.validationStatus === 'Accepted'
        ? 'success'
        : response.validationStatus === 'Blocked'
          ? 'warning'
          : 'error';
      this.feedbackService.show(level, response.reasonMessage);
    } catch (error) {
      const structuredError = tryExtractStructuredImportError(error);
      if (structuredError) {
        this.result.set(structuredError);
        this.errorMessage.set(null);

        const level = structuredError.validationStatus === 'Accepted'
          ? 'success'
          : structuredError.validationStatus === 'Blocked' || structuredError.outcome === 'Duplicate'
            ? 'warning'
            : 'error';
        this.feedbackService.show(level, structuredError.reasonMessage);
        return;
      }

      const message = extractApiErrorMessage(error, 'No fue posible importar el XML externo.');
      this.errorMessage.set(message);
      this.result.set(null);
      this.feedbackService.show('error', message);
    } finally {
      this.submitting.set(false);
    }
  }

  protected clear(): void {
    this.selectedFile.set(null);
    this.selectedFileName.set('');
    this.submitting.set(false);
    this.result.set(null);
    this.errorMessage.set(null);
  }
}

function tryExtractStructuredImportError(error: unknown): ExternalRepBaseDocumentImportResponse | null {
  if (typeof error !== 'object' || !error || !('error' in error)) {
    return null;
  }

  const payload = (error as { error?: Partial<ExternalRepBaseDocumentImportResponse> }).error;
  if (!payload?.reasonCode || !payload.reasonMessage || !payload.validationStatus || !payload.outcome) {
    return null;
  }

  return {
    outcome: payload.outcome,
    isSuccess: payload.isSuccess ?? false,
    externalRepBaseDocumentId: payload.externalRepBaseDocumentId ?? null,
    validationStatus: payload.validationStatus,
    reasonCode: payload.reasonCode,
    reasonMessage: payload.reasonMessage,
    errorMessage: payload.errorMessage ?? payload.reasonMessage,
    uuid: payload.uuid ?? null,
    issuerRfc: payload.issuerRfc ?? null,
    receiverRfc: payload.receiverRfc ?? null,
    paymentMethodSat: payload.paymentMethodSat ?? null,
    paymentFormSat: payload.paymentFormSat ?? null,
    currencyCode: payload.currencyCode ?? null,
    total: payload.total ?? null,
    isDuplicate: payload.isDuplicate ?? false
  };
}
