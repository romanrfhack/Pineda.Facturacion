import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { FiscalDocumentsApiService } from '../../fiscal-documents/infrastructure/fiscal-documents-api.service';
import {
  FiscalCancellationResponse,
  FiscalDocumentEmailDraftResponse,
  FiscalDocumentResponse,
  FiscalStampResponse,
  IssuedFiscalDocumentFilters,
  IssuedFiscalDocumentListItemResponse
} from '../../fiscal-documents/models/fiscal-documents.models';
import { FiscalDocumentCardComponent } from '../../fiscal-documents/components/fiscal-document-card.component';
import { FiscalStampEvidenceCardComponent } from '../../fiscal-documents/components/fiscal-stamp-evidence-card.component';
import { FiscalStampEvidenceDetailComponent } from '../../fiscal-documents/components/fiscal-stamp-evidence-detail.component';
import { FiscalCancellationCardComponent } from '../../fiscal-documents/components/fiscal-cancellation-card.component';
import { XmlViewerPanelComponent } from '../../../shared/components/xml-viewer-panel.component';
import { getDisplayLabel } from '../../../shared/ui/display-labels';
import { buildFiscalDocumentFileName } from '../../fiscal-documents/application/fiscal-document-file-name';

@Component({
  selector: 'app-issued-cfdis-page',
  imports: [FormsModule, DecimalPipe, FiscalDocumentCardComponent, FiscalStampEvidenceCardComponent, FiscalStampEvidenceDetailComponent, FiscalCancellationCardComponent, XmlViewerPanelComponent],
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">CFDI emitidos</p>
        <h2>Bandeja operativa de facturas emitidas</h2>
      </header>

      <section class="card">
        <form class="filters" (ngSubmit)="applyFilters()">
          <label><span>Desde</span><input [(ngModel)]="fromDate" name="fromDate" type="date" /></label>
          <label><span>Hasta</span><input [(ngModel)]="toDate" name="toDate" type="date" /></label>
          <label><span>RFC receptor</span><input [(ngModel)]="receiverRfc" name="receiverRfc" /></label>
          <label><span>Nombre receptor</span><input [(ngModel)]="receiverName" name="receiverName" /></label>
          <label><span>UUID</span><input [(ngModel)]="uuid" name="uuid" /></label>
          <label><span>Serie</span><input [(ngModel)]="series" name="series" /></label>
          <label><span>Folio</span><input [(ngModel)]="folio" name="folio" /></label>
          <label>
            <span>Estado</span>
            <select [(ngModel)]="status" name="status">
              <option value="">Emitidos y cancelados</option>
              <option value="Stamped">Stamped</option>
              <option value="Cancelled">Cancelled</option>
            </select>
          </label>
          <label class="wide"><span>Búsqueda general</span><input [(ngModel)]="query" name="query" placeholder="UUID, RFC, receptor, serie o folio" /></label>

          @if (filtersError()) {
            <p class="error wide">{{ filtersError() }}</p>
          }

          <div class="actions wide">
            <button type="submit" [disabled]="loading()">{{ loading() ? 'Buscando...' : 'Buscar' }}</button>
            <button type="button" class="secondary" (click)="clearFilters()" [disabled]="loading()">Limpiar filtros</button>
          </div>
        </form>
      </section>

      <section class="card">
        @if (loading()) {
          <p class="helper">Cargando CFDI emitidos...</p>
        } @else if (errorMessage()) {
          <p class="error">{{ errorMessage() }}</p>
        } @else if (!items().length) {
          <p class="helper">No se encontraron CFDI emitidos con los filtros actuales.</p>
        } @else {
          <div class="toolbar">
            <p class="helper">Mostrando {{ items().length }} de {{ totalCount() }} CFDI.</p>
            <label class="page-size">
              <span>Tamaño</span>
              <select [ngModel]="pageSize()" (ngModelChange)="changePageSize($event)" name="pageSize">
                <option [ngValue]="10">10</option>
                <option [ngValue]="25">25</option>
                <option [ngValue]="50">50</option>
              </select>
            </label>
          </div>

          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Emisión</th>
                  <th>Timbrado</th>
                  <th>Serie</th>
                  <th>Folio</th>
                  <th>UUID</th>
                  <th>RFC receptor</th>
                  <th>Receptor</th>
                  <th>Total</th>
                  <th>Estado</th>
                  <th>Método</th>
                  <th>Forma</th>
                  <th>Acción</th>
                </tr>
              </thead>
              <tbody>
                @for (item of items(); track item.fiscalDocumentId) {
                  <tr>
                    <td>{{ formatUtc(item.issuedAtUtc) }}</td>
                    <td>{{ item.stampedAtUtc ? formatUtc(item.stampedAtUtc) : '—' }}</td>
                    <td>{{ item.series || '—' }}</td>
                    <td>{{ item.folio || '—' }}</td>
                    <td>{{ item.uuid || '—' }}</td>
                    <td>{{ item.receiverRfc }}</td>
                    <td>{{ item.receiverLegalName }}</td>
                    <td>{{ item.total | number:'1.2-2' }}</td>
                    <td>{{ getDisplayLabel(item.status) }}</td>
                    <td>{{ item.paymentMethodSat }}</td>
                    <td>{{ item.paymentFormSat }}</td>
                    <td><button type="button" class="secondary small" (click)="openDetailModal(item)" [disabled]="actionBusy(item.fiscalDocumentId, 'detail')">Ver detalle</button></td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <div class="pagination">
            <button type="button" class="secondary" (click)="goToPage(page() - 1)" [disabled]="page() <= 1 || loading()">Anterior</button>
            <span>Página {{ page() }} de {{ totalPages() || 1 }}</span>
            <button type="button" class="secondary" (click)="goToPage(page() + 1)" [disabled]="page() >= totalPages() || loading()">Siguiente</button>
          </div>
        }
      </section>

      @if (showDetailModal()) {
        <section class="modal-backdrop" (click)="closeDetailModal()">
          <section class="modal-card detail-modal" (click)="$event.stopPropagation()">
            <div class="modal-header">
              <div>
                <p class="eyebrow">CFDI emitidos</p>
                <h3>Detalle de CFDI emitido</h3>
              </div>
              <button type="button" class="secondary" (click)="closeDetailModal()">Cerrar</button>
            </div>

            @if (selectedDocument(); as currentDocument) {
              <section class="card nested-card">
                <div class="button-row">
                  <button type="button" class="secondary" (click)="openPdfForSelected(false)">Ver PDF</button>
                  <button type="button" class="secondary" (click)="openPdfForSelected(true)">Descargar PDF</button>
                  <button type="button" class="secondary" (click)="openXmlForSelected()">Ver XML</button>
                  <button type="button" class="secondary" (click)="downloadXmlForSelected()">Descargar XML</button>
                  <button type="button" class="secondary" (click)="openEmailComposerForSelected()">Reenviar por correo</button>
                </div>
              </section>

              <app-fiscal-document-card [document]="currentDocument" />
            } @else {
              <section class="card nested-card">
                <p class="helper">Cargando detalle del CFDI...</p>
              </section>
            }

            @if (selectedStamp(); as currentStamp) {
              <app-fiscal-stamp-evidence-card [stamp]="currentStamp" (detailsRequested)="toggleStampDetail()" (xmlRequested)="openXmlForSelected()" />
              @if (showStampDetail()) {
                <app-fiscal-stamp-evidence-detail [stamp]="currentStamp" />
              }
            }

            @if (selectedCancellation(); as cancellation) {
              <app-fiscal-cancellation-card [cancellation]="cancellation" />
            }
          </section>
        </section>
      }

      @if (showStampXmlPanel()) {
        <app-xml-viewer-panel
          title="XML del documento fiscal"
          [loading]="loadingStampXml()"
          [xmlContent]="stampXmlContent()"
          [errorMessage]="stampXmlError()"
          (close)="closeStampXml()"
        />
      }

      @if (showEmailComposer()) {
        <section class="modal-backdrop" (click)="closeEmailComposer()">
          <section class="modal-card" (click)="$event.stopPropagation()">
            <h3>Reenviar CFDI por correo</h3>
            <p class="helper">Se adjuntarán el XML timbrado y el PDF del CFDI ya emitido.</p>

            <form class="filters" (ngSubmit)="sendEmail()">
              <label class="wide">
                <span>Correo(s) destino</span>
                <input [(ngModel)]="emailRecipientsInput" name="emailRecipientsInput" />
              </label>
              <label class="wide">
                <span>Asunto</span>
                <input [(ngModel)]="emailSubject" name="emailSubject" />
              </label>
              <label class="wide">
                <span>Mensaje</span>
                <textarea [(ngModel)]="emailBody" name="emailBody" rows="5"></textarea>
              </label>

              @if (emailRecipientsError()) {
                <p class="error wide">{{ emailRecipientsError() }}</p>
              }

              @if (emailDraftError()) {
                <p class="error wide">{{ emailDraftError() }}</p>
              }

              <div class="actions wide">
                <button type="submit" [disabled]="sendingEmail() || !hasValidEmailRecipients()">{{ sendingEmail() ? 'Enviando...' : 'Enviar CFDI' }}</button>
                <button type="button" class="secondary" (click)="closeEmailComposer()" [disabled]="sendingEmail()">Cancelar</button>
              </div>
            </form>
          </section>
        </section>
      }
    </section>
  `,
  styles: [`
    .page { display:grid; gap:1rem; }
    .card { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    .filters { display:grid; grid-template-columns:repeat(auto-fit, minmax(180px, 1fr)); gap:1rem; align-items:end; }
    .wide { grid-column:1 / -1; }
    label { display:grid; gap:0.35rem; }
    input, select, button, textarea { font:inherit; }
    input, select, textarea { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; }
    .actions, .toolbar, .pagination, .button-row { display:flex; flex-wrap:wrap; gap:0.75rem; align-items:center; }
    .toolbar, .pagination { justify-content:space-between; }
    .table-wrap { overflow:auto; }
    table { width:100%; border-collapse:collapse; }
    th, td { text-align:left; padding:0.75rem 0.5rem; border-bottom:1px solid #ece5d7; vertical-align:top; }
    .helper { margin:0; color:#5f6b76; }
    .error { margin:0; color:#7a2020; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    button.secondary { background:#d8c49b; color:#182533; }
    button.small { padding:0.45rem 0.7rem; font-size:0.88rem; }
    button:disabled { opacity:0.6; cursor:not-allowed; }
    .page-size { min-width:120px; }
    .modal-backdrop { position:fixed; inset:0; background:rgba(24, 37, 51, 0.42); display:grid; place-items:center; padding:1rem; z-index:50; }
    .modal-card { width:min(760px, 100%); max-height:calc(100vh - 2rem); overflow:auto; border:1px solid #d8d1c2; border-radius:1rem; background:#fff; padding:1rem; display:grid; gap:1rem; box-shadow:0 24px 60px rgba(24, 37, 51, 0.24); }
    .detail-modal { width:min(1120px, 100%); align-content:start; }
    .modal-header { display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; }
    .nested-card { padding:0; border:none; background:transparent; }
    @media (max-width: 720px) {
      .toolbar, .pagination { flex-direction:column; align-items:stretch; }
      .modal-header { flex-direction:column; align-items:stretch; }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class IssuedCfdisPageComponent {
  private readonly api = inject(FiscalDocumentsApiService);
  private readonly feedbackService = inject(FeedbackService);
  protected readonly getDisplayLabel = getDisplayLabel;

  protected fromDate = '';
  protected toDate = '';
  protected receiverRfc = '';
  protected receiverName = '';
  protected uuid = '';
  protected series = '';
  protected folio = '';
  protected status = '';
  protected query = '';
  protected readonly page = signal(1);
  protected readonly pageSize = signal(25);
  protected readonly totalCount = signal(0);
  protected readonly totalPages = signal(0);
  protected readonly items = signal<IssuedFiscalDocumentListItemResponse[]>([]);
  protected readonly selectedItem = signal<IssuedFiscalDocumentListItemResponse | null>(null);
  protected readonly selectedDocument = signal<FiscalDocumentResponse | null>(null);
  protected readonly selectedStamp = signal<FiscalStampResponse | null>(null);
  protected readonly selectedCancellation = signal<FiscalCancellationResponse | null>(null);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly filtersError = signal<string | null>(null);
  protected readonly showStampXmlPanel = signal(false);
  protected readonly loadingStampXml = signal(false);
  protected readonly stampXmlContent = signal<string | null>(null);
  protected readonly stampXmlError = signal<string | null>(null);
  protected readonly showEmailComposer = signal(false);
  protected readonly loadingEmailDraft = signal(false);
  protected readonly sendingEmail = signal(false);
  protected readonly emailDraftError = signal<string | null>(null);
  protected readonly emailRecipientsError = signal<string | null>(null);
  protected readonly showStampDetail = signal(false);
  protected readonly actionKey = signal<string | null>(null);
  protected readonly showDetailModal = signal(false);
  private emailFiscalDocumentId: number | null = null;
  protected emailRecipientsInput = '';
  protected emailSubject = '';
  protected emailBody = '';

  constructor() {
    void this.load();
  }

  protected async applyFilters(): Promise<void> {
    if (this.fromDate && this.toDate && this.fromDate > this.toDate) {
      this.filtersError.set('La fecha inicial no puede ser mayor a la fecha final.');
      return;
    }

    this.filtersError.set(null);
    this.page.set(1);
    await this.load();
  }

  protected async clearFilters(): Promise<void> {
    this.fromDate = '';
    this.toDate = '';
    this.receiverRfc = '';
    this.receiverName = '';
    this.uuid = '';
    this.series = '';
    this.folio = '';
    this.status = '';
    this.query = '';
    this.filtersError.set(null);
    this.page.set(1);
    this.pageSize.set(25);
    await this.load();
  }

  protected async goToPage(page: number): Promise<void> {
    if (page < 1 || page > this.totalPages() || page === this.page()) {
      return;
    }

    this.page.set(page);
    await this.load(false);
  }

  protected async changePageSize(value: number): Promise<void> {
    this.pageSize.set(Number(value) || 25);
    this.page.set(1);
    await this.load(false);
  }

  protected async selectItem(item: IssuedFiscalDocumentListItemResponse): Promise<void> {
    this.selectedItem.set(item);
    this.showStampDetail.set(false);
    try {
      this.selectedDocument.set(await firstValueFrom(this.api.getFiscalDocumentById(item.fiscalDocumentId)));
      this.selectedStamp.set(await firstValueFrom(this.api.getStamp(item.fiscalDocumentId)));
    } catch (error) {
      this.feedbackService.show('error', extractApiErrorMessage(error, 'No fue posible cargar el detalle del CFDI.'));
      return;
    }

    try {
      this.selectedCancellation.set(await firstValueFrom(this.api.getCancellation(item.fiscalDocumentId)));
    } catch {
      this.selectedCancellation.set(null);
    }
  }

  protected async openDetailModal(item: IssuedFiscalDocumentListItemResponse): Promise<void> {
    this.actionKey.set(`detail:${item.fiscalDocumentId}`);
    try {
      await this.selectItem(item);
      this.showDetailModal.set(true);
    } finally {
      this.actionKey.set(null);
    }
  }

  protected closeDetailModal(): void {
    this.showDetailModal.set(false);
    this.showStampDetail.set(false);
  }

  protected toggleStampDetail(): void {
    this.showStampDetail.update((value) => !value);
  }

  protected actionBusy(fiscalDocumentId: number, action: string): boolean {
    return this.actionKey() === `${action}:${fiscalDocumentId}`;
  }

  protected async openPdf(item: IssuedFiscalDocumentListItemResponse, download: boolean): Promise<void> {
    await this.handlePdf(item, download);
  }

  protected async openPdfForSelected(download: boolean): Promise<void> {
    const item = this.selectedItem();
    if (!item) {
      return;
    }

    await this.handlePdf(item, download);
  }

  protected async openXml(item: IssuedFiscalDocumentListItemResponse): Promise<void> {
    this.actionKey.set(`xml-view:${item.fiscalDocumentId}`);
    this.showStampXmlPanel.set(true);
    this.loadingStampXml.set(true);
    this.stampXmlError.set(null);
    this.stampXmlContent.set(null);

    try {
      this.stampXmlContent.set(await firstValueFrom(this.api.getStampXml(item.fiscalDocumentId)));
    } catch (error) {
      this.stampXmlError.set(extractApiErrorMessage(error, 'No fue posible cargar el XML del CFDI.'));
    } finally {
      this.loadingStampXml.set(false);
      this.actionKey.set(null);
    }
  }

  protected async openXmlForSelected(): Promise<void> {
    const item = this.selectedItem();
    if (!item) {
      return;
    }

    await this.openXml(item);
  }

  protected closeStampXml(): void {
    this.showStampXmlPanel.set(false);
    this.loadingStampXml.set(false);
    this.stampXmlError.set(null);
    this.stampXmlContent.set(null);
  }

  protected async downloadXml(item: IssuedFiscalDocumentListItemResponse): Promise<void> {
    this.actionKey.set(`xml-download:${item.fiscalDocumentId}`);
    try {
      const blob = await firstValueFrom(this.api.getStampXmlFile(item.fiscalDocumentId));
      triggerBlobDownload(blob, buildFiscalDocumentFileName({
        issuerRfc: item.issuerRfc,
        series: item.series,
        folio: item.folio,
        receiverRfc: item.receiverRfc,
        fallbackToken: item.uuid ?? item.fiscalDocumentId
      }, 'xml'));
    } catch (error) {
      this.feedbackService.show('error', extractApiErrorMessage(error, 'No fue posible descargar el XML del CFDI.'));
    } finally {
      this.actionKey.set(null);
    }
  }

  protected async downloadXmlForSelected(): Promise<void> {
    const item = this.selectedItem();
    if (!item) {
      return;
    }

    await this.downloadXml(item);
  }

  protected async openEmailComposer(item: IssuedFiscalDocumentListItemResponse): Promise<void> {
    if (this.loadingEmailDraft() || this.sendingEmail()) {
      return;
    }

    this.actionKey.set(`email-draft:${item.fiscalDocumentId}`);
    this.loadingEmailDraft.set(true);
    this.emailDraftError.set(null);
    this.emailFiscalDocumentId = item.fiscalDocumentId;

    try {
      const draft = await firstValueFrom(this.api.getEmailDraft(item.fiscalDocumentId));
      this.consumeEmailDraft(draft);
      this.showEmailComposer.set(true);
    } catch (error) {
      this.emailDraftError.set(extractApiErrorMessage(error, 'No fue posible cargar el envío por correo.'));
      this.showEmailComposer.set(true);
    } finally {
      this.loadingEmailDraft.set(false);
      this.actionKey.set(null);
    }
  }

  protected async openEmailComposerForSelected(): Promise<void> {
    const item = this.selectedItem();
    if (!item) {
      return;
    }

    await this.openEmailComposer(item);
  }

  protected closeEmailComposer(): void {
    if (this.sendingEmail()) {
      return;
    }

    this.showEmailComposer.set(false);
    this.emailDraftError.set(null);
    this.emailRecipientsError.set(null);
  }

  protected hasValidEmailRecipients(): boolean {
    const recipients = parseRecipients(this.emailRecipientsInput);
    return recipients.length > 0 && recipients.every(isValidEmail);
  }

  protected async sendEmail(): Promise<void> {
    if (!this.emailFiscalDocumentId || this.sendingEmail()) {
      return;
    }

    const recipients = parseRecipients(this.emailRecipientsInput);
    if (recipients.length === 0 || !recipients.every(isValidEmail)) {
      this.emailRecipientsError.set('Captura al menos un correo válido para continuar.');
      return;
    }

    this.sendingEmail.set(true);
    this.emailRecipientsError.set(null);
    this.emailDraftError.set(null);

    try {
      const response = await firstValueFrom(this.api.sendByEmail(this.emailFiscalDocumentId, {
        recipients,
        subject: this.emailSubject,
        body: this.emailBody
      }));
      this.feedbackService.show('success', `CFDI enviado correctamente a ${response.recipients.join(', ')}.`);
      this.closeEmailComposer();
    } catch (error) {
      this.emailDraftError.set(extractApiErrorMessage(error, 'No fue posible enviar el CFDI por correo.'));
    } finally {
      this.sendingEmail.set(false);
    }
  }

  protected formatUtc(value: string): string {
    return new Date(value).toLocaleString();
  }

  private async load(selectFirst = true): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);

    try {
      const response = await firstValueFrom(this.api.searchIssued(this.buildFilters()));
      this.items.set(response.items);
      this.totalCount.set(response.totalCount);
      this.totalPages.set(response.totalPages);
      this.page.set(response.page);
      this.pageSize.set(response.pageSize);

      const currentSelected = this.selectedItem();
      const nextSelected = response.items.find((item) => item.fiscalDocumentId === currentSelected?.fiscalDocumentId)
        ?? (selectFirst ? response.items[0] ?? null : currentSelected ?? null);

      if (nextSelected) {
        await this.selectItem(nextSelected);
      } else {
        this.selectedItem.set(null);
        this.selectedDocument.set(null);
        this.selectedStamp.set(null);
        this.selectedCancellation.set(null);
      }
    } catch (error) {
      this.items.set([]);
      this.selectedItem.set(null);
      this.selectedDocument.set(null);
      this.selectedStamp.set(null);
      this.selectedCancellation.set(null);
      this.totalCount.set(0);
      this.totalPages.set(0);
      this.errorMessage.set(extractApiErrorMessage(error, 'No fue posible cargar los CFDI emitidos.'));
    } finally {
      this.loading.set(false);
    }
  }

  private buildFilters(): IssuedFiscalDocumentFilters {
    return {
      page: this.page(),
      pageSize: this.pageSize(),
      fromDate: this.fromDate || null,
      toDate: this.toDate || null,
      receiverRfc: this.receiverRfc || null,
      receiverName: this.receiverName || null,
      uuid: this.uuid || null,
      series: this.series || null,
      folio: this.folio || null,
      status: this.status || null,
      query: this.query || null
    };
  }

  private consumeEmailDraft(draft: FiscalDocumentEmailDraftResponse): void {
    this.emailRecipientsInput = draft.defaultRecipientEmail ?? '';
    this.emailSubject = draft.suggestedSubject ?? '';
    this.emailBody = draft.suggestedBody ?? '';
    this.emailRecipientsError.set(null);
  }

  private async handlePdf(item: IssuedFiscalDocumentListItemResponse, download: boolean): Promise<void> {
    this.actionKey.set(`${download ? 'pdf-download' : 'pdf-view'}:${item.fiscalDocumentId}`);
    try {
      const blob = await firstValueFrom(this.api.getStampPdf(item.fiscalDocumentId));
      const objectUrl = window.URL.createObjectURL(blob);

      if (download) {
        const link = document.createElement('a');
        link.href = objectUrl;
        link.download = buildFiscalDocumentFileName({
          issuerRfc: item.issuerRfc,
          series: item.series,
          folio: item.folio,
          receiverRfc: item.receiverRfc,
          fallbackToken: item.uuid ?? item.fiscalDocumentId
        }, 'pdf');
        link.click();
      } else {
        window.open(objectUrl, '_blank', 'noopener,noreferrer');
      }

      window.setTimeout(() => window.URL.revokeObjectURL(objectUrl), 30000);
    } catch (error) {
      this.feedbackService.show('error', extractApiErrorMessage(error, 'No fue posible abrir el PDF del CFDI.'));
    } finally {
      this.actionKey.set(null);
    }
  }
}

function parseRecipients(value: string): string[] {
  return value
    .split(/[,;\n]+/)
    .map((recipient) => recipient.trim())
    .filter((recipient) => recipient.length > 0);
}

function isValidEmail(value: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
}

function triggerBlobDownload(blob: Blob, fileName: string): void {
  const objectUrl = window.URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = objectUrl;
  link.download = fileName;
  link.click();
  window.setTimeout(() => window.URL.revokeObjectURL(objectUrl), 30000);
}
