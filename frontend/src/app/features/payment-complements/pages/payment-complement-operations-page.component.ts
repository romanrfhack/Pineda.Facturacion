import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { PermissionService } from '../../../core/auth/permission.service';
import { AccountsReceivableApiService } from '../../accounts-receivable/infrastructure/accounts-receivable-api.service';
import { PaymentComplementsApiService } from '../infrastructure/payment-complements-api.service';
import {
  PaymentComplementCancellationResponse,
  PaymentComplementDocumentResponse,
  PaymentComplementStampResponse
} from '../models/payment-complements.models';
import { PaymentComplementCardComponent } from '../components/payment-complement-card.component';
import { PaymentComplementStampCardComponent } from '../components/payment-complement-stamp-card.component';
import { PaymentComplementCancellationCardComponent } from '../components/payment-complement-cancellation-card.component';
import { PaymentComplementStampEvidenceDetailComponent } from '../components/payment-complement-stamp-evidence-detail.component';
import { XmlViewerPanelComponent } from '../../../shared/components/xml-viewer-panel.component';
import { getDisplayLabel } from '../../../shared/ui/display-labels';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';

@Component({
  selector: 'app-payment-complement-operations-page',
  imports: [PaymentComplementCardComponent, PaymentComplementStampCardComponent, PaymentComplementCancellationCardComponent, PaymentComplementStampEvidenceDetailComponent, XmlViewerPanelComponent],
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">Complementos de pago</p>
        <h2>Preparar, timbrar, cancelar y actualizar estatus del complemento</h2>
      </header>

      @if (paymentId()) {
        <section class="card">
          <h3>Evento de pago #{{ paymentId() }}</h3>
          <p class="helper">Este flujo puntual se conserva para operación existente sobre un paymentId ya identificado.</p>
          @if (permissionService.canManagePayments()) {
            <button type="button" (click)="prepare()" [disabled]="loading()">Preparar complemento de pago</button>
          }
        </section>
      }

      @if (complement(); as currentComplement) {
        <app-payment-complement-card [complement]="currentComplement" />

        <section class="card actions">
          <div class="button-row">
            @if (permissionService.canStampFiscal()) {
              <button type="button" (click)="stamp()" [disabled]="loading() || !canStamp(currentComplement)">Timbrar</button>
              <button type="button" class="danger" (click)="cancel()" [disabled]="loading() || !canCancel(currentComplement)">Cancelar</button>
              <button type="button" class="secondary" (click)="refreshStatus()" [disabled]="loading() || !canRefreshStatus(currentComplement)">Actualizar estatus</button>
            }
          </div>
          <p class="helper">La nueva bandeja REP interna vive fuera de este flujo. Aquí solo permanece la operación puntual existente por paymentId.</p>
        </section>
      }

      @if (stampEvidence(); as currentStamp) {
        <app-payment-complement-stamp-card
          [stamp]="currentStamp"
          (detailsRequested)="toggleStampDetail()"
          (xmlRequested)="openStampXml()"
        />
        @if (showStampDetail()) {
          <app-payment-complement-stamp-evidence-detail [stamp]="currentStamp" />
        }
      } @else if (complement()) {
        <section class="card">
          <h3>Evidencia de timbrado del complemento</h3>
          <p class="helper">Aún no hay evidencia de timbrado disponible. Primero timbra el complemento de pago para consultar metadatos persistidos y XML.</p>
        </section>
      }

      @if (showStampXmlPanel()) {
        <app-xml-viewer-panel
          title="XML del complemento de pago"
          [loading]="loadingStampXml()"
          [xmlContent]="stampXmlContent()"
          [errorMessage]="stampXmlError()"
          (close)="closeStampXml()"
        />
      }

      @if (cancellation(); as currentCancellation) {
        <app-payment-complement-cancellation-card [cancellation]="currentCancellation" />
      }
    </section>
  `,
  styles: [`
    .page { display:grid; gap:1rem; }
    .card { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    h2 { margin:0.3rem 0 0; }
    .helper { color:#5f6b76; }
    .button-row { display:flex; flex-wrap:wrap; gap:0.75rem; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    button.secondary { background:#d8c49b; color:#182533; }
    button.danger { background:#7a2020; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PaymentComplementOperationsPageComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly arApi = inject(AccountsReceivableApiService);
  private readonly paymentComplementsApi = inject(PaymentComplementsApiService);
  private readonly feedbackService = inject(FeedbackService);
  protected readonly permissionService = inject(PermissionService);

  protected readonly paymentId = signal<number | null>(parseNumber(this.route.snapshot.queryParamMap.get('paymentId')));
  protected readonly complement = signal<PaymentComplementDocumentResponse | null>(null);
  protected readonly stampEvidence = signal<PaymentComplementStampResponse | null>(null);
  protected readonly cancellation = signal<PaymentComplementCancellationResponse | null>(null);
  protected readonly loading = signal(false);
  protected readonly showStampDetail = signal(false);
  protected readonly showStampXmlPanel = signal(false);
  protected readonly loadingStampXml = signal(false);
  protected readonly stampXmlContent = signal<string | null>(null);
  protected readonly stampXmlError = signal<string | null>(null);

  constructor() {
    if (this.paymentId()) {
      void this.loadComplementByPayment(this.paymentId()!);
    }
  }

  protected async prepare(): Promise<void> {
    const paymentId = this.paymentId();
    if (!paymentId) {
      return;
    }

    await this.run(async () => {
      const response = await firstValueFrom(this.arApi.preparePaymentComplement(paymentId));
      if (!resolvePaymentComplementId(response)) {
        this.feedbackService.show('error', response.errorMessage || 'No se pudo preparar el complemento.');
        return;
      }

      await this.loadComplementByPayment(paymentId);
      this.feedbackService.show('success', 'Complemento de pago preparado.');
    });
  }

  protected async stamp(): Promise<void> {
    const complement = this.complement();
    const paymentComplementId = complement ? resolvePaymentComplementId(complement) : null;
    if (!complement || !paymentComplementId) {
      return;
    }

    await this.run(async () => {
      const response = await firstValueFrom(this.paymentComplementsApi.stamp(paymentComplementId));
      this.feedbackService.show(response.isSuccess ? 'success' : 'warning', response.providerMessage || response.supportMessage || response.errorMessage || getDisplayLabel(response.outcome));
      await this.loadComplementByPayment(complement.accountsReceivablePaymentId);
      await this.loadStamp(paymentComplementId);
    });
  }

  protected async cancel(): Promise<void> {
    const complement = this.complement();
    const paymentComplementId = complement ? resolvePaymentComplementId(complement) : null;
    if (!complement || !paymentComplementId || !window.confirm('¿Cancelar este complemento de pago timbrado?')) {
      return;
    }

    await this.run(async () => {
      const response = await firstValueFrom(this.paymentComplementsApi.cancel(paymentComplementId));
      this.feedbackService.show(response.isSuccess ? 'success' : 'warning', response.providerMessage || response.supportMessage || response.errorMessage || getDisplayLabel(response.outcome));
      await this.loadComplementByPayment(complement.accountsReceivablePaymentId);
      await this.loadCancellation(paymentComplementId);
    });
  }

  protected async refreshStatus(): Promise<void> {
    const complement = this.complement();
    const paymentComplementId = complement ? resolvePaymentComplementId(complement) : null;
    if (!complement || !paymentComplementId) {
      return;
    }

    await this.run(async () => {
      const response = await firstValueFrom(this.paymentComplementsApi.refreshStatus(paymentComplementId));
      this.feedbackService.show(
        response.isSuccess ? 'success' : 'warning',
        response.providerMessage || response.supportMessage || response.errorMessage || getDisplayLabel(response.outcome)
      );
      await this.loadComplementByPayment(complement.accountsReceivablePaymentId);
      await this.loadStamp(paymentComplementId);
      await this.loadCancellation(paymentComplementId);
    });
  }

  protected canStamp(complement: PaymentComplementDocumentResponse): boolean {
    return complement.status === 'ReadyForStamping' || complement.status === 'StampingRejected';
  }

  protected canCancel(complement: PaymentComplementDocumentResponse): boolean {
    return complement.status === 'Stamped' || complement.status === 'CancellationRejected';
  }

  protected canRefreshStatus(complement: PaymentComplementDocumentResponse): boolean {
    return complement.status === 'Stamped' || complement.status === 'CancellationRequested' || complement.status === 'CancellationRejected' || complement.status === 'Cancelled';
  }

  protected toggleStampDetail(): void {
    this.showStampDetail.update((value) => !value);
  }

  protected async openStampXml(): Promise<void> {
    const complement = this.complement();
    const paymentComplementId = complement ? resolvePaymentComplementId(complement) : null;
    if (!complement || !paymentComplementId) {
      return;
    }

    this.showStampXmlPanel.set(true);
    this.loadingStampXml.set(true);
    this.stampXmlError.set(null);
    this.stampXmlContent.set(null);

    try {
      this.stampXmlContent.set(await firstValueFrom(this.paymentComplementsApi.getStampXml(paymentComplementId)));
    } catch (error) {
      this.stampXmlError.set(extractErrorMessage(error));
    } finally {
      this.loadingStampXml.set(false);
    }
  }

  protected closeStampXml(): void {
    this.showStampXmlPanel.set(false);
    this.loadingStampXml.set(false);
    this.stampXmlContent.set(null);
    this.stampXmlError.set(null);
  }

  private async loadComplementByPayment(paymentId: number): Promise<void> {
    try {
      const complement = await firstValueFrom(this.arApi.getPaymentComplementByPaymentId(paymentId));
      this.complement.set(complement);
      this.showStampDetail.set(false);
      this.closeStampXml();
      const paymentComplementId = resolvePaymentComplementId(complement);
      if (!paymentComplementId) {
        this.stampEvidence.set(null);
        this.cancellation.set(null);
        return;
      }

      await this.loadStamp(paymentComplementId);
      await this.loadCancellation(paymentComplementId);
    } catch {
      this.complement.set(null);
    }
  }

  private async loadStamp(paymentComplementId: number): Promise<void> {
    try {
      this.stampEvidence.set(await firstValueFrom(this.paymentComplementsApi.getStamp(paymentComplementId)));
    } catch {
      this.stampEvidence.set(null);
    }
  }

  private async loadCancellation(paymentComplementId: number): Promise<void> {
    try {
      this.cancellation.set(await firstValueFrom(this.paymentComplementsApi.getCancellation(paymentComplementId)));
    } catch {
      this.cancellation.set(null);
    }
  }

  private async run(operation: () => Promise<void>): Promise<void> {
    this.loading.set(true);
    try {
      await operation();
    } catch (error) {
      this.feedbackService.show('error', extractErrorMessage(error));
    } finally {
      this.loading.set(false);
    }
  }
}

function parseNumber(value: string | null): number | null {
  if (!value) {
    return null;
  }

  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

function extractErrorMessage(error: unknown): string {
  return extractApiErrorMessage(error);
}

function resolvePaymentComplementId(value: { id?: number | null; paymentComplementId?: number | null; paymentComplementDocumentId?: number | null }): number | null {
  return value.id ?? value.paymentComplementId ?? value.paymentComplementDocumentId ?? null;
}
