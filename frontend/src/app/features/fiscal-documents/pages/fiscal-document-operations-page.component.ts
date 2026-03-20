import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { FiscalDocumentsApiService } from '../infrastructure/fiscal-documents-api.service';
import {
  FiscalCancellationResponse,
  FiscalDocumentResponse,
  FiscalReceiverSearchResponse,
  FiscalStampResponse,
  IssuerProfileResponse
} from '../models/fiscal-documents.models';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { PermissionService } from '../../../core/auth/permission.service';
import { FiscalDocumentCardComponent } from '../components/fiscal-document-card.component';
import { FiscalStampEvidenceCardComponent } from '../components/fiscal-stamp-evidence-card.component';
import { FiscalCancellationCardComponent } from '../components/fiscal-cancellation-card.component';
import { FiscalStampEvidenceDetailComponent } from '../components/fiscal-stamp-evidence-detail.component';
import { XmlViewerPanelComponent } from '../../../shared/components/xml-viewer-panel.component';

@Component({
  selector: 'app-fiscal-document-operations-page',
  imports: [FormsModule, RouterLink, FiscalDocumentCardComponent, FiscalStampEvidenceCardComponent, FiscalCancellationCardComponent, FiscalStampEvidenceDetailComponent, XmlViewerPanelComponent],
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">Fiscal document operations</p>
        <h2>Prepare, stamp, inspect, cancel, and refresh status</h2>
      </header>

      @if (!fiscalDocument()) {
        <section class="card">
          <h3>Prepare fiscal document</h3>
          <p class="helper">Billing document id: <strong>{{ billingDocumentId() || 'Missing' }}</strong></p>

          <form class="form-grid" (ngSubmit)="prepare()">
            <label>
              <span>Receiver search</span>
              <div class="row">
                <input [(ngModel)]="receiverQuery" name="receiverQuery" placeholder="RFC or legal name" />
                <button type="button" class="secondary" (click)="searchReceivers()">Search</button>
              </div>
            </label>

            <label>
              <span>Receiver</span>
              <select [(ngModel)]="selectedReceiverId" name="selectedReceiverId" required>
                <option [ngValue]="null">Select receiver</option>
                @for (receiver of receiverResults(); track receiver.id) {
                  <option [ngValue]="receiver.id">{{ receiver.rfc }} · {{ receiver.legalName }}</option>
                }
              </select>
            </label>

            <label>
              <span>Active issuer</span>
              <input [value]="activeIssuerLabel()" disabled />
            </label>

            <label>
              <span>Payment method SAT</span>
              <input [(ngModel)]="paymentMethodSat" name="paymentMethodSat" required />
            </label>

            <label>
              <span>Payment form SAT</span>
              <input [(ngModel)]="paymentFormSat" name="paymentFormSat" required />
            </label>

            <label>
              <span>Payment condition</span>
              <input [(ngModel)]="paymentCondition" name="paymentCondition" />
            </label>

            <label class="checkbox">
              <input [(ngModel)]="isCreditSale" name="isCreditSale" type="checkbox" />
              <span>Credit sale</span>
            </label>

            <label>
              <span>Credit days</span>
              <input [(ngModel)]="creditDays" name="creditDays" type="number" min="1" />
            </label>

            <button type="submit" [disabled]="loadingPrepare()"> {{ loadingPrepare() ? 'Preparing...' : 'Prepare fiscal document' }} </button>
          </form>
        </section>
      }

      @if (fiscalDocument(); as currentDocument) {
        <app-fiscal-document-card [document]="currentDocument" />

        <section class="card actions">
          <h3>Operations</h3>
          <div class="button-row">
            @if (permissionService.canStampFiscal()) {
              <button type="button" (click)="stamp()" [disabled]="loadingOperation() || currentDocument.status === 'Stamped'">Stamp</button>
            }
            @if (permissionService.canCancelFiscal()) {
              <button type="button" class="danger" (click)="cancel()" [disabled]="loadingOperation() || currentDocument.status !== 'Stamped'">Cancel</button>
            }
            @if (permissionService.canCancelFiscal()) {
              <button type="button" class="secondary" (click)="refreshStatus()" [disabled]="loadingOperation()">Refresh status</button>
            }
            <a [routerLink]="['/app/accounts-receivable']" [queryParams]="{ fiscalDocumentId: currentDocument.id }">Open AR and payments</a>
          </div>

          @if (lastOperationMessage()) {
            <p class="helper">{{ lastOperationMessage() }}</p>
          }
        </section>
      }

      @if (stampEvidence(); as currentStamp) {
        <app-fiscal-stamp-evidence-card
          [stamp]="currentStamp"
          (detailsRequested)="toggleStampDetail()"
          (xmlRequested)="openStampXml()"
        />
        @if (showStampDetail()) {
          <app-fiscal-stamp-evidence-detail [stamp]="currentStamp" />
        }
      } @else if (fiscalDocument()) {
        <section class="card">
          <h3>Stamp evidence</h3>
          <p class="helper">No stamp evidence is available yet. Stamp the fiscal document first to view persisted metadata and XML.</p>
        </section>
      }

      @if (showStampXmlPanel()) {
        <app-xml-viewer-panel
          title="Fiscal document XML"
          [loading]="loadingStampXml()"
          [xmlContent]="stampXmlContent()"
          [errorMessage]="stampXmlError()"
          (close)="closeStampXml()"
        />
      }

      @if (cancellation(); as currentCancellation) {
        <app-fiscal-cancellation-card [cancellation]="currentCancellation" />
      }
    </section>
  `,
  styles: [`
    .page { display:grid; gap:1rem; }
    .card { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    h2 { margin:0.3rem 0 0; }
    .helper { color:#5f6b76; }
    .form-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:1rem; align-items:end; }
    label { display:grid; gap:0.35rem; }
    input, select, button { font:inherit; }
    input, select { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; }
    .row { display:flex; gap:0.75rem; }
    .checkbox { display:flex; align-items:center; gap:0.5rem; }
    .checkbox input { width:auto; }
    .button-row { display:flex; flex-wrap:wrap; gap:0.75rem; align-items:center; }
    button, a { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; text-decoration:none; display:inline-flex; }
    button.secondary { background:#d8c49b; color:#182533; }
    button.danger { background:#7a2020; }
    button:disabled { opacity:0.6; cursor:wait; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FiscalDocumentOperationsPageComponent {
  private readonly api = inject(FiscalDocumentsApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly feedbackService = inject(FeedbackService);
  protected readonly permissionService = inject(PermissionService);

  protected readonly billingDocumentId = signal<number | null>(parseNumber(this.route.snapshot.queryParamMap.get('billingDocumentId')));
  protected readonly fiscalDocumentId = signal<number | null>(parseNumber(this.route.snapshot.paramMap.get('id')));
  protected readonly loadingPrepare = signal(false);
  protected readonly loadingOperation = signal(false);
  protected readonly activeIssuer = signal<IssuerProfileResponse | null>(null);
  protected readonly receiverResults = signal<FiscalReceiverSearchResponse[]>([]);
  protected readonly fiscalDocument = signal<FiscalDocumentResponse | null>(null);
  protected readonly stampEvidence = signal<FiscalStampResponse | null>(null);
  protected readonly cancellation = signal<FiscalCancellationResponse | null>(null);
  protected readonly lastOperationMessage = signal<string | null>(null);
  protected readonly showStampDetail = signal(false);
  protected readonly showStampXmlPanel = signal(false);
  protected readonly loadingStampXml = signal(false);
  protected readonly stampXmlContent = signal<string | null>(null);
  protected readonly stampXmlError = signal<string | null>(null);

  protected receiverQuery = '';
  protected selectedReceiverId: number | null = null;
  protected paymentMethodSat = 'PPD';
  protected paymentFormSat = '99';
  protected paymentCondition = 'CREDITO';
  protected isCreditSale = true;
  protected creditDays = 7;

  protected readonly activeIssuerLabel = computed(() => {
    const issuer = this.activeIssuer();
    return issuer ? `${issuer.rfc} · ${issuer.legalName}` : 'Loading active issuer...';
  });

  constructor() {
    void this.loadIssuer();
    if (this.fiscalDocumentId()) {
      void this.loadFiscalDocument(this.fiscalDocumentId()!);
    }
  }

  protected async searchReceivers(): Promise<void> {
    this.receiverResults.set(await firstValueFrom(this.api.searchReceivers(this.receiverQuery || '')));
  }

  protected async prepare(): Promise<void> {
    const billingDocumentId = this.billingDocumentId();
    if (!billingDocumentId || !this.selectedReceiverId) {
      this.feedbackService.show('error', 'Select a receiver and open this page from a billing document.');
      return;
    }

    this.loadingPrepare.set(true);
    try {
      const response = await firstValueFrom(this.api.prepareFiscalDocument(billingDocumentId, {
        fiscalReceiverId: this.selectedReceiverId,
        issuerProfileId: this.activeIssuer()?.id ?? null,
        paymentMethodSat: this.paymentMethodSat,
        paymentFormSat: this.paymentFormSat,
        paymentCondition: this.paymentCondition,
        isCreditSale: this.isCreditSale,
        creditDays: this.creditDays
      }));

      if (!response.fiscalDocumentId) {
        this.feedbackService.show('error', response.errorMessage || 'Fiscal document could not be prepared.');
        return;
      }

      await this.loadFiscalDocument(response.fiscalDocumentId);
      this.feedbackService.show('success', 'Fiscal document prepared.');
    } catch (error) {
      this.feedbackService.show('error', extractErrorMessage(error));
    } finally {
      this.loadingPrepare.set(false);
    }
  }

  protected async stamp(): Promise<void> {
    const fiscalDocumentId = this.fiscalDocumentId();
    if (!fiscalDocumentId) {
      return;
    }

    await this.runOperation(async () => {
      const response = await firstValueFrom(this.api.stampFiscalDocument(fiscalDocumentId, { retryRejected: false }));
      this.lastOperationMessage.set(response.errorMessage || `Stamp outcome: ${response.outcome}`);
      await this.loadFiscalDocument(fiscalDocumentId);
      await this.loadStamp(fiscalDocumentId);
    });
  }

  protected async cancel(): Promise<void> {
    const fiscalDocumentId = this.fiscalDocumentId();
    if (!fiscalDocumentId || !window.confirm('Cancel this stamped fiscal document? This action is operationally sensitive.')) {
      return;
    }

    await this.runOperation(async () => {
      const response = await firstValueFrom(this.api.cancelFiscalDocument(fiscalDocumentId, { cancellationReasonCode: '02' }));
      this.lastOperationMessage.set(response.errorMessage || `Cancellation outcome: ${response.outcome}`);
      await this.loadFiscalDocument(fiscalDocumentId);
      await this.loadCancellation(fiscalDocumentId);
    });
  }

  protected async refreshStatus(): Promise<void> {
    const fiscalDocumentId = this.fiscalDocumentId();
    if (!fiscalDocumentId) {
      return;
    }

    await this.runOperation(async () => {
      const response = await firstValueFrom(this.api.refreshStatus(fiscalDocumentId));
      this.lastOperationMessage.set(response.providerMessage || response.errorMessage || `Latest external status: ${response.lastKnownExternalStatus ?? 'Unknown'}`);
      await this.loadFiscalDocument(fiscalDocumentId);
      await this.loadStamp(fiscalDocumentId);
      await this.loadCancellation(fiscalDocumentId, false);
    });
  }

  protected toggleStampDetail(): void {
    this.showStampDetail.update((value) => !value);
  }

  protected async openStampXml(): Promise<void> {
    const fiscalDocumentId = this.fiscalDocumentId();
    if (!fiscalDocumentId) {
      return;
    }

    this.showStampXmlPanel.set(true);
    this.loadingStampXml.set(true);
    this.stampXmlError.set(null);
    this.stampXmlContent.set(null);

    try {
      this.stampXmlContent.set(await firstValueFrom(this.api.getStampXml(fiscalDocumentId)));
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

  private async loadIssuer(): Promise<void> {
    try {
      this.activeIssuer.set(await firstValueFrom(this.api.getActiveIssuer()));
    } catch {
      this.feedbackService.show('warning', 'Active issuer profile could not be loaded.');
    }
  }

  private async loadFiscalDocument(fiscalDocumentId: number): Promise<void> {
    this.fiscalDocumentId.set(fiscalDocumentId);
    this.showStampDetail.set(false);
    this.closeStampXml();
    const document = await firstValueFrom(this.api.getFiscalDocumentById(fiscalDocumentId));
    this.fiscalDocument.set(document);
    await this.loadStamp(fiscalDocumentId, false);
    await this.loadCancellation(fiscalDocumentId, false);
  }

  private async loadStamp(fiscalDocumentId: number, notifyOnMissing = false): Promise<void> {
    try {
      this.stampEvidence.set(await firstValueFrom(this.api.getStamp(fiscalDocumentId)));
    } catch {
      this.stampEvidence.set(null);
      if (notifyOnMissing) {
        this.feedbackService.show('info', 'No stamp evidence is available yet.');
      }
    }
  }

  private async loadCancellation(fiscalDocumentId: number, notifyOnMissing = false): Promise<void> {
    try {
      this.cancellation.set(await firstValueFrom(this.api.getCancellation(fiscalDocumentId)));
    } catch {
      this.cancellation.set(null);
      if (notifyOnMissing) {
        this.feedbackService.show('info', 'No cancellation evidence is available yet.');
      }
    }
  }

  private async runOperation(operation: () => Promise<void>): Promise<void> {
    this.loadingOperation.set(true);
    try {
      await operation();
    } catch (error) {
      this.feedbackService.show('error', extractErrorMessage(error));
    } finally {
      this.loadingOperation.set(false);
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
