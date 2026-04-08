import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { PermissionService } from '../../../core/auth/permission.service';
import { FeedbackLevel, FeedbackService } from '../../../core/ui/feedback.service';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { StatusBadgeComponent, StatusBadgeTone } from '../../../shared/components/status-badge.component';
import { FiscalImportsApiService } from '../infrastructure/fiscal-imports-api.service';
import { OfficialSatCatalogImportExecution, OfficialSatCatalogImportResponse } from '../models/catalogs.models';

type SatCatalogImportUiState = 'idle' | 'calculatingChecksum' | 'readyToImport' | 'importing' | 'imported' | 'alreadyImported' | 'error';

const MAX_UPLOAD_SIZE_BYTES = 128 * 1024 * 1024;

@Component({
  selector: 'app-sat-catalog-import-page',
  imports: [StatusBadgeComponent],
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">Catálogos / Importar catálogo SAT</p>
        <h2>Carga oficial del catálogo SAT</h2>
        <p class="helper">Este flujo está separado de la importación de perfiles fiscales de producto y usa el archivo oficial completo del SAT.</p>
      </header>

      <section class="card card-spacious">
        @if (permissionService.canWriteMasterData()) {
          <div class="upload-layout">
            <div class="upload-panel">
              <div class="section-intro">
                <div>
                  <h3>Archivo oficial</h3>
                  <p class="helper">Selecciona el workbook completo del SAT, calcula el checksum localmente y ejecuta la carga canónica cuando el archivo esté listo.</p>
                </div>
              </div>

              <label class="field">
                <span>Archivo oficial SAT (.xls, .xlsx)</span>
                <input type="file" accept=".xls,.xlsx" (change)="onFileSelected($event)" />
              </label>

              <div class="field-footnote">
                <span class="helper">Límite actual: {{ maxUploadSizeLabel }}</span>
                @if (selectedFile()) {
                  <span class="helper">Archivo seleccionado: {{ selectedFile()!.name }}</span>
                }
              </div>
            </div>

            <div class="control-stack">
              <article class="status-panel">
                <div class="status-header">
                  <div>
                    <p class="status-label">Estado</p>
                    <strong>{{ getStateLabel(importState()) }}</strong>
                  </div>
                  <app-status-badge [label]="getStateLabel(importState())" [tone]="getStateTone(importState())" />
                </div>
                <p class="helper">{{ getStateDescription(importState()) }}</p>
                <div class="status-meta">
                  <span class="summary-label">Checksum local</span>
                  <strong [class.mono]="!!localChecksum()">
                    {{ localChecksum() || (importState() === 'calculatingChecksum' ? 'Calculando...' : 'Pendiente') }}
                  </strong>
                </div>
              </article>

              <button type="button" class="primary-action" (click)="importCatalog()" [disabled]="importState() !== 'readyToImport'">
                {{ importState() === 'importing' ? 'Importando...' : 'Importar catálogo SAT' }}
              </button>
            </div>
          </div>
        } @else {
          <p class="helper">Tu rol puede consultar catálogos, pero no subir ni importar el archivo oficial SAT.</p>
        }
      </section>

      @if (selectedFile()) {
        <section class="card">
          <div class="section-intro">
            <div>
              <h3>Resumen previo</h3>
              <p class="helper">Verifica el archivo y los metadatos locales antes de ejecutar la importación.</p>
            </div>
          </div>

          <dl class="data-grid">
            <div class="data-item data-item-wide">
              <dt>Archivo seleccionado</dt>
              <dd>{{ selectedFile()!.name }}</dd>
            </div>
            <div class="data-item">
              <dt>Tamaño</dt>
              <dd>{{ formatFileSize(selectedFile()!.size) }}</dd>
            </div>
            <div class="data-item data-item-wide">
              <dt>Checksum SHA-256</dt>
              <dd class="mono">{{ localChecksum() || (importState() === 'calculatingChecksum' ? 'Calculando...' : 'Pendiente') }}</dd>
            </div>
            <div class="data-item">
              <dt>Versión CFDI</dt>
              <dd>4.0</dd>
            </div>
          </dl>
        </section>
      }

      @if (importResult()) {
        <section class="card">
          <div class="section-intro result-header">
            <div>
              <h3>Resultado de importación</h3>
              <p class="helper">Resultado consolidado de la carga ejecutada por el servidor.</p>
            </div>
            <app-status-badge [label]="getOutcomeLabel(importResult()!.outcome)" [tone]="getOutcomeTone(importResult()!.outcome)" />
          </div>

          @if (getPersistentServerDetail(importResult()!); as persistentServerDetail) {
            <p class="detail-note error-note">{{ persistentServerDetail }}</p>
          }

          <dl class="data-grid">
            <div class="data-item">
              <dt>Resultado</dt>
              <dd>{{ getOutcomeLabel(importResult()!.outcome) }}</dd>
            </div>
            <div class="data-item data-item-wide">
              <dt>Archivo usado por servidor</dt>
              <dd>{{ importResult()!.sourceFileName }}</dd>
            </div>
            <div class="data-item data-item-wide">
              <dt>Checksum final usado por servidor</dt>
              <dd class="mono">{{ importResult()!.sourceChecksum }}</dd>
            </div>
            <div class="data-item">
              <dt>Versión CFDI usada por servidor</dt>
              <dd>{{ importResult()!.sourceVersion }}</dd>
            </div>
            <div class="data-item data-item-wide">
              <dt>Correlation id</dt>
              <dd class="mono">{{ importResult()!.correlationId }}</dd>
            </div>
            <div class="data-item">
              <dt>Checksum preview vs servidor</dt>
              <dd>{{ getChecksumMatchLabel(importResult()!.clientChecksumMatchesServer) }}</dd>
            </div>
          </dl>

          <div class="result-grid">
            <article class="result-card">
              <div class="result-card-header">
                <h4>Productos y servicios</h4>
                <app-status-badge [label]="getExecutionStatusLabel(importResult()!.productServices)" [tone]="getExecutionTone(importResult()!.productServices)" />
              </div>

              <dl class="result-details">
                <div>
                  <dt>Filas</dt>
                  <dd>{{ importResult()!.productServices.totalRows }}</dd>
                </div>
                <div>
                  <dt>Insertadas</dt>
                  <dd>{{ importResult()!.productServices.insertedRows }}</dd>
                </div>
                <div>
                  <dt>Actualizadas</dt>
                  <dd>{{ importResult()!.productServices.updatedRows }}</dd>
                </div>
                <div>
                  <dt>Desactivadas</dt>
                  <dd>{{ importResult()!.productServices.deactivatedRows }}</dd>
                </div>
              </dl>

              @if (importResult()!.productServices.errorMessage) {
                <p class="detail-note error-note">{{ importResult()!.productServices.errorMessage }}</p>
              }
            </article>

            <article class="result-card">
              <div class="result-card-header">
                <h4>Claves de unidad</h4>
                <app-status-badge [label]="getExecutionStatusLabel(importResult()!.units)" [tone]="getExecutionTone(importResult()!.units)" />
              </div>

              <dl class="result-details">
                <div>
                  <dt>Filas</dt>
                  <dd>{{ importResult()!.units.totalRows }}</dd>
                </div>
                <div>
                  <dt>Insertadas</dt>
                  <dd>{{ importResult()!.units.insertedRows }}</dd>
                </div>
                <div>
                  <dt>Actualizadas</dt>
                  <dd>{{ importResult()!.units.updatedRows }}</dd>
                </div>
                <div>
                  <dt>Desactivadas</dt>
                  <dd>{{ importResult()!.units.deactivatedRows }}</dd>
                </div>
              </dl>

              @if (importResult()!.units.errorMessage) {
                <p class="detail-note error-note">{{ importResult()!.units.errorMessage }}</p>
              }
            </article>
          </div>
        </section>
      }
    </section>
  `,
  styles: [`
    .page { display:grid; gap:1rem; }
    header { display:grid; gap:0.3rem; }
    .card { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; }
    .card-spacious { padding:1.1rem 1.15rem; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    h2 { margin:0.25rem 0 0; }
    h3 { margin:0; }
    h4, p { margin:0; }
    .helper { margin:0; color:#5f6b76; }
    .upload-layout { display:grid; grid-template-columns:minmax(0, 1.65fr) minmax(280px, 1fr); gap:1rem 1.25rem; align-items:start; }
    .upload-panel, .control-stack, .field, .section-intro, .result-card { display:grid; gap:0.75rem; }
    .control-stack { align-content:start; align-items:stretch; }
    .field { gap:0.45rem; }
    .field-footnote { display:flex; flex-wrap:wrap; gap:0.5rem 1rem; }
    input, button { font:inherit; }
    input, button { width:100%; }
    input { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; }
    button { border:none; border-radius:0.8rem; padding:0.85rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    button:disabled { cursor:not-allowed; opacity:0.65; }
    .primary-action { min-height:3.15rem; }
    .status-panel, .result-card, .data-item, .detail-note { border:1px solid #ece5d7; border-radius:0.9rem; background:#fbf8f2; }
    .status-panel, .result-card { padding:0.95rem 1rem; }
    .status-header, .result-card-header { display:flex; justify-content:space-between; gap:0.75rem; align-items:flex-start; }
    .status-meta { display:grid; gap:0.3rem; padding-top:0.15rem; }
    .status-label, .summary-label { font-size:0.82rem; text-transform:uppercase; letter-spacing:0.08em; color:#8a6a32; }
    .data-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:0.9rem 1rem; margin:0; }
    .data-item { display:grid; gap:0.4rem; padding:0.9rem 1rem; }
    .data-item-wide { grid-column:span 2; }
    .result-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(280px, 1fr)); gap:1rem; align-items:start; }
    .result-details { display:grid; grid-template-columns:repeat(2, minmax(0, 1fr)); gap:0.85rem 1rem; margin:0; }
    .result-details div { display:grid; gap:0.25rem; }
    dt { font-size:0.8rem; color:#5f6b76; }
    dd { margin:0; font-weight:600; color:#182533; overflow-wrap:anywhere; word-break:break-word; }
    .mono { font-family:"SFMono-Regular", Consolas, "Liberation Mono", Menlo, monospace; font-weight:500; }
    .result-header { grid-template-columns:minmax(0, 1fr) auto; align-items:start; }
    .detail-note { margin:0; padding:0.8rem 0.9rem; color:#5c4a1f; overflow-wrap:anywhere; word-break:break-word; }
    .error-note { color:#7a2020; background:#fff5f5; border-color:#efcaca; }
    @media (max-width: 960px) {
      .upload-layout { grid-template-columns:1fr; }
    }
    @media (max-width: 720px) {
      .data-item-wide { grid-column:auto; }
      .result-header { grid-template-columns:1fr; }
      .status-header, .result-card-header { flex-direction:column; align-items:flex-start; }
      .result-details { grid-template-columns:1fr; }
    }
    @media (max-width: 560px) {
      .field-footnote { flex-direction:column; gap:0.35rem; }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SatCatalogImportPageComponent {
  private readonly api = inject(FiscalImportsApiService);
  private readonly feedbackService = inject(FeedbackService);
  protected readonly permissionService = inject(PermissionService);

  protected readonly selectedFile = signal<File | null>(null);
  protected readonly localChecksum = signal<string | null>(null);
  protected readonly importResult = signal<OfficialSatCatalogImportResponse | null>(null);
  protected readonly importState = signal<SatCatalogImportUiState>('idle');
  protected readonly maxUploadSizeLabel = `${Math.round(MAX_UPLOAD_SIZE_BYTES / 1024 / 1024)} MB`;

  protected async onFileSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;

    this.importResult.set(null);
    this.localChecksum.set(null);

    if (!file) {
      this.selectedFile.set(null);
      this.importState.set('idle');
      return;
    }

    if (!isSupportedSatCatalogWorkbook(file.name)) {
      this.selectedFile.set(null);
      this.importState.set('error');
      this.feedbackService.show('error', 'Selecciona un archivo .xls o .xlsx del catálogo SAT.');
      return;
    }

    if (file.size > MAX_UPLOAD_SIZE_BYTES) {
      this.selectedFile.set(null);
      this.importState.set('error');
      this.feedbackService.show('error', `El archivo supera el límite permitido de ${this.maxUploadSizeLabel}.`);
      return;
    }

    this.selectedFile.set(file);
    this.importState.set('calculatingChecksum');

    try {
      const checksum = await computeSha256(file);
      if (this.selectedFile() !== file) {
        return;
      }

      this.localChecksum.set(checksum);
      this.importState.set('readyToImport');
    } catch {
      this.importState.set('error');
      this.feedbackService.show('error', 'No fue posible calcular el checksum SHA-256 del archivo.');
    }
  }

  protected async importCatalog(): Promise<void> {
    const file = this.selectedFile();
    const checksum = this.localChecksum();
    if (!file || !checksum || !this.permissionService.canWriteMasterData()) {
      return;
    }

    this.importState.set('importing');
    this.importResult.set(null);

    try {
      const result = await firstValueFrom(this.api.importOfficialSatCatalog(file, checksum));
      this.importResult.set(result);
      const state: SatCatalogImportUiState = result.outcome === 'AlreadyImported'
        ? 'alreadyImported'
        : result.isSuccess
          ? 'imported'
          : 'error';
      this.importState.set(state);
      const feedback = this.buildOutcomeFeedback(result);
      this.feedbackService.show(feedback.level, feedback.message);
    } catch (error) {
      this.importState.set('error');
      this.feedbackService.show('error', extractApiErrorMessage(error, 'No fue posible importar el catálogo SAT.'));
    }
  }

  protected getStateLabel(state: SatCatalogImportUiState): string {
    switch (state) {
      case 'calculatingChecksum':
        return 'Calculando checksum';
      case 'readyToImport':
        return 'Listo para importar';
      case 'importing':
        return 'Importando';
      case 'imported':
        return 'Importado';
      case 'alreadyImported':
        return 'Ya importado';
      case 'error':
        return 'Error';
      case 'idle':
      default:
        return 'Selecciona un archivo';
    }
  }

  protected getStateTone(state: SatCatalogImportUiState): StatusBadgeTone {
    switch (state) {
      case 'readyToImport':
      case 'imported':
        return 'success';
      case 'calculatingChecksum':
      case 'importing':
        return 'info';
      case 'alreadyImported':
        return 'warning';
      case 'error':
        return 'danger';
      case 'idle':
      default:
        return 'neutral';
    }
  }

  protected getStateDescription(state: SatCatalogImportUiState): string {
    switch (state) {
      case 'calculatingChecksum':
        return 'Se está calculando el checksum local antes de habilitar la carga.';
      case 'readyToImport':
        return 'El archivo ya fue validado localmente y puede enviarse al servidor.';
      case 'importing':
        return 'La importación está en curso. Mantén esta pantalla abierta hasta recibir el resultado.';
      case 'imported':
        return 'La última importación terminó correctamente.';
      case 'alreadyImported':
        return 'El archivo ya existía en el histórico del servidor.';
      case 'error':
        return 'La última acción no se completó. Revisa la notificación mostrada.';
      case 'idle':
      default:
        return 'Selecciona un archivo válido para preparar la importación.';
    }
  }

  protected getOutcomeLabel(outcome: string): string {
    switch (outcome) {
      case 'AlreadyImported':
        return 'Ya importado';
      case 'Completed':
        return 'Importado';
      case 'PartiallyCompleted':
        return 'Importado con incidencias';
      case 'Failed':
        return 'Fallido';
      case 'ValidationFailed':
        return 'Validación fallida';
      default:
        return outcome;
    }
  }

  protected getOutcomeTone(outcome: string): StatusBadgeTone {
    switch (outcome) {
      case 'Completed':
        return 'success';
      case 'AlreadyImported':
      case 'PartiallyCompleted':
        return 'warning';
      case 'Failed':
      case 'ValidationFailed':
        return 'danger';
      default:
        return 'neutral';
    }
  }

  protected getPersistentServerDetail(result: OfficialSatCatalogImportResponse): string | null {
    const message = normalizeMessage(result.errorMessage);
    if (!message) {
      return null;
    }

    const summaryMessage = normalizeMessage(this.buildOutcomeFeedback(result).message);
    if (summaryMessage && areEquivalentMessages(message, summaryMessage)) {
      return null;
    }

    const executionMessages = [result.productServices.errorMessage, result.units.errorMessage]
      .map((value) => normalizeMessage(value))
      .filter((value): value is string => value !== null);

    if (executionMessages.some((value) => areEquivalentMessages(message, value))) {
      return null;
    }

    return message;
  }

  protected getExecutionStatusLabel(result: OfficialSatCatalogImportExecution): string {
    if (result.wasAlreadyImported || result.status === 'alreadyImported') {
      return 'Ya importado';
    }

    switch (result.status) {
      case 'completed':
        return 'Importado';
      case 'failed':
        return 'Fallido';
      case 'processing':
        return 'Procesando';
      default:
        return result.status;
    }
  }

  protected getExecutionTone(result: OfficialSatCatalogImportExecution): StatusBadgeTone {
    if (result.wasAlreadyImported || result.status === 'alreadyImported') {
      return 'warning';
    }

    switch (result.status) {
      case 'completed':
        return 'success';
      case 'failed':
        return 'danger';
      case 'processing':
        return 'info';
      default:
        return 'neutral';
    }
  }

  protected getChecksumMatchLabel(value: boolean | null | undefined): string {
    if (value == null) {
      return 'No enviado por cliente';
    }

    return value ? 'Coincide' : 'No coincide';
  }

  protected formatFileSize(bytes: number): string {
    if (bytes < 1024 * 1024) {
      return `${Math.round(bytes / 1024)} KB`;
    }

    return `${(bytes / 1024 / 1024).toFixed(2)} MB`;
  }

  private buildOutcomeFeedback(result: OfficialSatCatalogImportResponse): { level: FeedbackLevel; message: string } {
    switch (result.outcome) {
      case 'AlreadyImported':
        return {
          level: 'info',
          message: 'El archivo SAT ya había sido importado.'
        };
      case 'Completed':
        return {
          level: 'success',
          message: 'Catálogo SAT importado correctamente.'
        };
      case 'PartiallyCompleted':
        return {
          level: 'warning',
          message: 'Catálogo SAT importado con incidencias. Revisa el resultado.'
        };
      case 'ValidationFailed':
        return {
          level: 'error',
          message: 'La importación del catálogo SAT fue rechazada por validación. Revisa el resultado.'
        };
      case 'Failed':
      default:
        return {
          level: 'error',
          message: 'No fue posible importar el catálogo SAT.'
        };
    }
  }
}

async function computeSha256(file: File): Promise<string> {
  if (!globalThis.crypto?.subtle) {
    throw new Error('Web Crypto API is not available.');
  }

  const buffer = await file.arrayBuffer();
  const digest = await globalThis.crypto.subtle.digest('SHA-256', buffer);
  const hash = Array.from(new Uint8Array(digest))
    .map((value) => value.toString(16).padStart(2, '0'))
    .join('');

  return `sha256:${hash}`;
}

function isSupportedSatCatalogWorkbook(fileName: string): boolean {
  const normalized = fileName.trim().toLowerCase();
  return normalized.endsWith('.xls') || normalized.endsWith('.xlsx');
}

function normalizeMessage(value?: string | null): string | null {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
}

function areEquivalentMessages(left: string, right: string): boolean {
  return normalizeComparableMessage(left) === normalizeComparableMessage(right);
}

function normalizeComparableMessage(value: string): string {
  return value
    .trim()
    .replace(/\s+/g, ' ')
    .replace(/[.:;!?]+$/g, '')
    .toLocaleLowerCase();
}
