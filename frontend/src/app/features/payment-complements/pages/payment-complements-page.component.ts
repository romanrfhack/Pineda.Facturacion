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

@Component({
  selector: 'app-payment-complements-page',
  imports: [PaymentComplementCardComponent, PaymentComplementStampCardComponent, PaymentComplementCancellationCardComponent, PaymentComplementStampEvidenceDetailComponent, XmlViewerPanelComponent],
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">Payment complements</p>
        <h2>Prepare, stamp, cancel, and refresh complement status</h2>
      </header>

      @if (paymentId()) {
        <section class="card">
          <h3>Payment event #{{ paymentId() }}</h3>
          @if (permissionService.canManagePayments()) {
            <button type="button" (click)="prepare()" [disabled]="loading()">Prepare payment complement</button>
          }
        </section>
      }

      @if (complement(); as currentComplement) {
        <app-payment-complement-card [complement]="currentComplement" />

        <section class="card actions">
          <div class="button-row">
            @if (permissionService.canStampFiscal()) {
              <button type="button" (click)="stamp()" [disabled]="loading() || currentComplement.status === 'Stamped'">Stamp</button>
              <button type="button" class="danger" (click)="cancel()" [disabled]="loading() || currentComplement.status !== 'Stamped'">Cancel</button>
              <button type="button" class="secondary" (click)="refreshStatus()" [disabled]="loading()">Refresh status</button>
            }
          </div>
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
          <h3>Complement stamp evidence</h3>
          <p class="helper">No stamp evidence is available yet. Stamp the payment complement first to view persisted metadata and XML.</p>
        </section>
      }

      @if (showStampXmlPanel()) {
        <app-xml-viewer-panel
          title="Payment complement XML"
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
export class PaymentComplementsPageComponent {
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
      if (!response.paymentComplementId) {
        this.feedbackService.show('error', response.errorMessage || 'Complement could not be prepared.');
        return;
      }

      await this.loadComplementByPayment(paymentId);
      this.feedbackService.show('success', 'Payment complement prepared.');
    });
  }

  protected async stamp(): Promise<void> {
    const complement = this.complement();
    if (!complement) {
      return;
    }

    await this.run(async () => {
      const response = await firstValueFrom(this.paymentComplementsApi.stamp(complement.id));
      this.feedbackService.show(response.isSuccess ? 'success' : 'warning', response.errorMessage || response.outcome);
      await this.loadComplementByPayment(complement.accountsReceivablePaymentId);
      await this.loadStamp(complement.id);
    });
  }

  protected async cancel(): Promise<void> {
    const complement = this.complement();
    if (!complement || !window.confirm('Cancel this stamped payment complement?')) {
      return;
    }

    await this.run(async () => {
      const response = await firstValueFrom(this.paymentComplementsApi.cancel(complement.id));
      this.feedbackService.show(response.isSuccess ? 'success' : 'warning', response.errorMessage || response.outcome);
      await this.loadComplementByPayment(complement.accountsReceivablePaymentId);
      await this.loadCancellation(complement.id);
    });
  }

  protected async refreshStatus(): Promise<void> {
    const complement = this.complement();
    if (!complement) {
      return;
    }

    await this.run(async () => {
      const response = await firstValueFrom(this.paymentComplementsApi.refreshStatus(complement.id));
      this.feedbackService.show(response.isSuccess ? 'success' : 'warning', response.providerMessage || response.errorMessage || response.outcome);
      await this.loadComplementByPayment(complement.accountsReceivablePaymentId);
      await this.loadStamp(complement.id);
      await this.loadCancellation(complement.id);
    });
  }

  protected toggleStampDetail(): void {
    this.showStampDetail.update((value) => !value);
  }

  protected async openStampXml(): Promise<void> {
    const complement = this.complement();
    if (!complement) {
      return;
    }

    this.showStampXmlPanel.set(true);
    this.loadingStampXml.set(true);
    this.stampXmlError.set(null);
    this.stampXmlContent.set(null);

    try {
      this.stampXmlContent.set(await firstValueFrom(this.paymentComplementsApi.getStampXml(complement.id)));
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
      await this.loadStamp(complement.id);
      await this.loadCancellation(complement.id);
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
  if (typeof error === 'object' && error && 'error' in error) {
    const payload = (error as { error?: { errorMessage?: string } }).error;
    if (payload?.errorMessage) {
      return payload.errorMessage;
    }
  }

  return 'The operation could not be completed.';
}
