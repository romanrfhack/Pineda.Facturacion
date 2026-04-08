import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { PermissionService } from '../../../core/auth/permission.service';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { FiscalImportsApiService } from '../infrastructure/fiscal-imports-api.service';
import { OfficialSatCatalogImportExecution, OfficialSatCatalogImportResponse } from '../models/catalogs.models';

type SatCatalogImportUiState = 'idle' | 'calculatingChecksum' | 'readyToImport' | 'importing' | 'imported' | 'alreadyImported' | 'error';

const MAX_UPLOAD_SIZE_BYTES = 128 * 1024 * 1024;

@Component({
  selector: 'app-sat-catalog-import-page',
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">Catálogos / Importar catálogo SAT</p>
        <h2>Carga oficial del catálogo SAT</h2>
        <p class="helper">Este flujo está separado de la importación de perfiles fiscales de producto y usa el archivo oficial completo del SAT.</p>
      </header>

      <section class="card">
        @if (permissionService.canWriteMasterData()) {
          <div class="upload-grid">
            <label>
              <span>Archivo oficial SAT (.xls, .xlsx)</span>
              <input type="file" accept=".xls,.xlsx" (change)="onFileSelected($event)" />
            </label>
            <div class="status-box">
              <span class="status-label">Estado</span>
              <strong>{{ getStateLabel(importState()) }}</strong>
              <span class="helper">Límite actual: {{ maxUploadSizeLabel }}</span>
            </div>
            <button type="button" (click)="importCatalog()" [disabled]="importState() !== 'readyToImport'">
              {{ importState() === 'importing' ? 'Importando...' : 'Importar catálogo SAT' }}
            </button>
          </div>
        } @else {
          <p class="helper">Tu rol puede consultar catálogos, pero no subir ni importar el archivo oficial SAT.</p>
        }

        @if (errorMessage()) {
          <p class="error">{{ errorMessage() }}</p>
        }
      </section>

      @if (selectedFile()) {
        <section class="card">
          <h3>Resumen previo</h3>
          <div class="summary-grid">
            <div>
              <span class="summary-label">Vas a subir este archivo</span>
              <strong>{{ selectedFile()!.name }}</strong>
            </div>
            <div>
              <span class="summary-label">Tamaño</span>
              <strong>{{ formatFileSize(selectedFile()!.size) }}</strong>
            </div>
            <div>
              <span class="summary-label">Checksum SHA-256</span>
              <strong>{{ localChecksum() || (importState() === 'calculatingChecksum' ? 'Calculando...' : 'Pendiente') }}</strong>
            </div>
            <div>
              <span class="summary-label">Versión CFDI</span>
              <strong>4.0</strong>
            </div>
          </div>
        </section>
      }

      @if (importResult()) {
        <section class="card">
          <h3>Resultado de importación</h3>
          <div class="summary-grid">
            <div>
              <span class="summary-label">Resultado</span>
              <strong>{{ getOutcomeLabel(importResult()!.outcome) }}</strong>
            </div>
            <div>
              <span class="summary-label">Archivo usado por servidor</span>
              <strong>{{ importResult()!.sourceFileName }}</strong>
            </div>
            <div>
              <span class="summary-label">Checksum final usado por servidor</span>
              <strong>{{ importResult()!.sourceChecksum }}</strong>
            </div>
            <div>
              <span class="summary-label">Versión CFDI usada por servidor</span>
              <strong>{{ importResult()!.sourceVersion }}</strong>
            </div>
            <div>
              <span class="summary-label">Correlation id</span>
              <strong>{{ importResult()!.correlationId }}</strong>
            </div>
            <div>
              <span class="summary-label">Checksum preview vs servidor</span>
              <strong>{{ getChecksumMatchLabel(importResult()!.clientChecksumMatchesServer) }}</strong>
            </div>
          </div>

          @if (importResult()!.errorMessage) {
            <p class="error">{{ importResult()!.errorMessage }}</p>
          }

          <div class="result-grid">
            <article class="result-card">
              <h4>Productos y servicios</h4>
              <p><strong>{{ getExecutionStatusLabel(importResult()!.productServices) }}</strong></p>
              <p>Filas: {{ importResult()!.productServices.totalRows }}</p>
              <p>Insertadas: {{ importResult()!.productServices.insertedRows }}</p>
              <p>Actualizadas: {{ importResult()!.productServices.updatedRows }}</p>
              <p>Desactivadas: {{ importResult()!.productServices.deactivatedRows }}</p>
              @if (importResult()!.productServices.errorMessage) {
                <p class="error">{{ importResult()!.productServices.errorMessage }}</p>
              }
            </article>

            <article class="result-card">
              <h4>Claves de unidad</h4>
              <p><strong>{{ getExecutionStatusLabel(importResult()!.units) }}</strong></p>
              <p>Filas: {{ importResult()!.units.totalRows }}</p>
              <p>Insertadas: {{ importResult()!.units.insertedRows }}</p>
              <p>Actualizadas: {{ importResult()!.units.updatedRows }}</p>
              <p>Desactivadas: {{ importResult()!.units.deactivatedRows }}</p>
              @if (importResult()!.units.errorMessage) {
                <p class="error">{{ importResult()!.units.errorMessage }}</p>
              }
            </article>
          </div>
        </section>
      }
    </section>
  `,
  styles: [`
    .page { display:grid; gap:1rem; }
    .card { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    .helper { margin:0; color:#5f6b76; }
    .error { margin:0; color:#7a2020; }
    .upload-grid, .summary-grid, .result-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:1rem; align-items:end; }
    .summary-grid { align-items:start; }
    label, .status-box, .result-card { display:grid; gap:0.35rem; }
    input, button { font:inherit; }
    input { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; }
    button { border:none; border-radius:0.8rem; padding:0.85rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    button:disabled { cursor:not-allowed; opacity:0.65; }
    .status-box, .result-card { border:1px solid #ece5d7; border-radius:0.85rem; padding:0.85rem 1rem; background:#fbf8f2; }
    .status-label, .summary-label { font-size:0.82rem; text-transform:uppercase; letter-spacing:0.08em; color:#8a6a32; }
    h2, h3, h4, p { margin:0; }
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
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly importState = signal<SatCatalogImportUiState>('idle');
  protected readonly maxUploadSizeLabel = `${Math.round(MAX_UPLOAD_SIZE_BYTES / 1024 / 1024)} MB`;

  protected async onFileSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;

    this.importResult.set(null);
    this.localChecksum.set(null);
    this.errorMessage.set(null);

    if (!file) {
      this.selectedFile.set(null);
      this.importState.set('idle');
      return;
    }

    if (!isSupportedSatCatalogWorkbook(file.name)) {
      this.selectedFile.set(null);
      this.importState.set('error');
      this.errorMessage.set('Selecciona un archivo .xls o .xlsx del catálogo SAT.');
      return;
    }

    if (file.size > MAX_UPLOAD_SIZE_BYTES) {
      this.selectedFile.set(null);
      this.importState.set('error');
      this.errorMessage.set(`El archivo supera el límite permitido de ${this.maxUploadSizeLabel}.`);
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
      this.errorMessage.set('No fue posible calcular el checksum SHA-256 del archivo.');
    }
  }

  protected async importCatalog(): Promise<void> {
    const file = this.selectedFile();
    const checksum = this.localChecksum();
    if (!file || !checksum || !this.permissionService.canWriteMasterData()) {
      return;
    }

    this.importState.set('importing');
    this.errorMessage.set(null);

    try {
      const result = await firstValueFrom(this.api.importOfficialSatCatalog(file, checksum));
      this.importResult.set(result);
      const state: SatCatalogImportUiState = result.outcome === 'AlreadyImported'
        ? 'alreadyImported'
        : result.isSuccess
          ? 'imported'
          : 'error';
      this.importState.set(state);

      if (state === 'alreadyImported') {
        this.feedbackService.show('info', 'El archivo SAT ya había sido importado.');
      } else if (state === 'imported') {
        this.feedbackService.show('success', 'Catálogo SAT importado correctamente.');
      }
    } catch (error) {
      this.importState.set('error');
      this.errorMessage.set(extractApiErrorMessage(error, 'No fue posible importar el catálogo SAT.'));
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
