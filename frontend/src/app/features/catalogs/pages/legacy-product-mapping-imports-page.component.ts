import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { PermissionService } from '../../../core/auth/permission.service';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { FeedbackLevel, FeedbackService } from '../../../core/ui/feedback.service';
import { StatusBadgeComponent, StatusBadgeTone } from '../../../shared/components/status-badge.component';
import { getDisplayLabel } from '../../../shared/ui/display-labels';
import { FiscalImportsApiService } from '../infrastructure/fiscal-imports-api.service';
import { LegacyProductMappingImportBatchSummary, LegacyProductMappingImportResponse } from '../models/catalogs.models';

@Component({
  selector: 'app-legacy-product-mapping-imports-page',
  imports: [FormsModule, StatusBadgeComponent],
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">Catálogos / Importaciones de productos</p>
        <h2>Importar mappings SAT de productos</h2>
        <p class="helper">
          Carga archivos CSV provenientes del sistema anterior para relacionar descripciones y códigos internos de productos con claves SAT válidas.
          Estos mappings serán usados por el sistema para sugerir o resolver automáticamente perfiles fiscales antes del timbrado.
        </p>
      </header>

      @if (!permissionService.canWriteMasterData()) {
        <section class="card">
          <p class="error">No tienes permiso para importar mappings SAT.</p>
        </section>
      } @else {
        <section class="card info-card">
          <div>
            <h3>Columnas esperadas</h3>
            <p class="helper">El CSV debe incluir estas columnas con los nombres del sistema anterior.</p>
          </div>
          <ul class="columns-list">
            <li>Id</li>
            <li>Descripción</li>
            <li>Clave Producto/Servicio</li>
            <li>Clave Unidad</li>
            <li>No. Catálogo Interno</li>
            <li>Código EAN</li>
            <li>Código SKU</li>
          </ul>
          <p class="note">El CSV solo aporta Clave Producto/Servicio SAT y Clave Unidad SAT. Objeto de impuesto, IVA y texto de unidad siguen resolviéndose con las reglas actuales del sistema.</p>
          <p class="note">El código SAT 01010101 no se asigna automáticamente; solo puede usarse si el usuario lo elige explícitamente.</p>
        </section>

        <section class="card">
          <div class="section-intro">
            <div>
              <h3>Cargar archivo</h3>
              <p class="helper">La validación completa del contenido la ejecuta el backend. Esta pantalla solo valida que se seleccione un archivo .csv.</p>
            </div>
          </div>

          <div class="form-grid">
            <label>
              <span>Archivo CSV</span>
              <input type="file" accept=".csv,text/csv" (change)="onFileSelected($event)" [disabled]="importing()" />
            </label>
            <label>
              <span>Fuente</span>
              <input [(ngModel)]="sourceName" name="sourceName" [disabled]="importing()" placeholder="Sistema anterior" />
            </label>
            <button type="button" (click)="importFile()" [disabled]="importing()">
              {{ importing() ? 'Importando...' : 'Importar archivo' }}
            </button>
          </div>

          @if (selectedFile()) {
            <p class="helper">Archivo seleccionado: {{ selectedFile()!.name }}</p>
          }

          @if (errorMessage()) {
            <p class="error">{{ errorMessage() }}</p>
          }
        </section>

        @if (importResult()) {
          <section class="card">
            <div class="section-intro result-header">
              <div>
                <h3>Resultado de importación</h3>
                <p class="helper">Resumen devuelto por el backend para el último archivo enviado.</p>
              </div>
              <app-status-badge [label]="getStatusLabel(importResult()!.status || importResult()!.outcome)" [tone]="getResultTone(importResult()!)" />
            </div>

            @if (importResult()!.errorMessage) {
              <p class="detail-note error-note">{{ importResult()!.errorMessage }}</p>
            }

            <dl class="data-grid">
              <div class="data-item">
                <dt>Total</dt>
                <dd>{{ importResult()!.totalRows }}</dd>
              </div>
              <div class="data-item">
                <dt>Válidos</dt>
                <dd>{{ importResult()!.validRows }}</dd>
              </div>
              <div class="data-item">
                <dt>Inválidos</dt>
                <dd>{{ importResult()!.invalidRows }}</dd>
              </div>
              <div class="data-item">
                <dt>Ambiguos</dt>
                <dd>{{ importResult()!.ambiguousRows }}</dd>
              </div>
              <div class="data-item">
                <dt>Omitidos</dt>
                <dd>{{ importResult()!.skippedRows }}</dd>
              </div>
              <div class="data-item">
                <dt>Estado</dt>
                <dd>{{ getStatusLabel(importResult()!.status) }}</dd>
              </div>
              <div class="data-item">
                <dt>Batch</dt>
                <dd>{{ importResult()!.batchId ?? 'N/D' }}</dd>
              </div>
              <div class="data-item data-item-wide">
                <dt>Archivo</dt>
                <dd>{{ importResult()!.fileName || 'N/D' }}</dd>
              </div>
              <div class="data-item">
                <dt>Fuente</dt>
                <dd>{{ importResult()!.sourceName || 'N/D' }}</dd>
              </div>
              <div class="data-item">
                <dt>Importado</dt>
                <dd>{{ formatDateTime(importResult()!.importedAtUtc) }}</dd>
              </div>
            </dl>
          </section>
        }

        <section class="card">
          <div class="section-intro result-header">
            <div>
              <h3>Historial de importaciones</h3>
              <p class="helper">Últimos batches registrados de mappings SAT históricos.</p>
            </div>
            <button type="button" class="secondary compact" (click)="loadHistory()" [disabled]="historyLoading()">
              {{ historyLoading() ? 'Actualizando...' : 'Actualizar' }}
            </button>
          </div>

          @if (historyError()) {
            <p class="error">{{ historyError() }}</p>
          }

          @if (!historyLoading() && !history().length) {
            <p class="helper">Aún no hay importaciones registradas.</p>
          } @else {
            <div class="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Fecha</th>
                    <th>Archivo</th>
                    <th>Fuente</th>
                    <th>Total</th>
                    <th>Válidos</th>
                    <th>Inválidos</th>
                    <th>Ambiguos</th>
                    <th>Omitidos</th>
                    <th>Estado</th>
                    <th>Usuario</th>
                  </tr>
                </thead>
                <tbody>
                  @for (batch of history(); track batch.id) {
                    <tr>
                      <td>{{ formatDateTime(batch.importedAtUtc) }}</td>
                      <td>{{ batch.fileName }}</td>
                      <td>{{ batch.sourceName || 'N/D' }}</td>
                      <td>{{ batch.totalRows }}</td>
                      <td>{{ batch.validRows }}</td>
                      <td>{{ batch.invalidRows }}</td>
                      <td>{{ batch.ambiguousRows }}</td>
                      <td>{{ batch.skippedRows }}</td>
                      <td>
                        <app-status-badge [label]="getStatusLabel(batch.status)" [tone]="getStatusTone(batch.status)" />
                      </td>
                      <td>{{ batch.importedByUser || 'N/D' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </section>
      }
    </section>
  `,
  styles: [`
    .page { display:grid; gap:1rem; }
    header, .section-intro, .info-card { display:grid; gap:0.45rem; }
    .card { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    h2 { margin:0.25rem 0 0; }
    h3, p { margin:0; }
    .helper { margin:0; color:#5f6b76; }
    .columns-list { margin:0; padding-left:1.2rem; columns:2; color:#182533; }
    .columns-list li { break-inside:avoid; padding:0.12rem 0; }
    .note, .detail-note { border:1px solid #ece5d7; border-radius:0.9rem; background:#fbf8f2; padding:0.8rem 0.9rem; color:#5c4a1f; }
    .error, .error-note { color:#7a2020; }
    .error-note { background:#fff5f5; border-color:#efcaca; }
    .form-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:1rem; align-items:end; }
    label { display:grid; gap:0.35rem; }
    input, button { font:inherit; }
    input { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    button.secondary { background:#d8c49b; color:#182533; }
    button.compact { width:auto; justify-self:end; }
    button:disabled, input:disabled { cursor:not-allowed; opacity:0.65; }
    .result-header { grid-template-columns:minmax(0, 1fr) auto; align-items:start; }
    .data-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(160px, 1fr)); gap:0.9rem 1rem; margin:0; }
    .data-item { display:grid; gap:0.4rem; padding:0.9rem 1rem; border:1px solid #ece5d7; border-radius:0.9rem; background:#fbf8f2; }
    .data-item-wide { grid-column:span 2; }
    dt { font-size:0.8rem; color:#5f6b76; }
    dd { margin:0; font-weight:600; color:#182533; overflow-wrap:anywhere; word-break:break-word; }
    .table-wrap { overflow:auto; }
    table { width:100%; border-collapse:collapse; }
    th, td { text-align:left; padding:0.75rem 0.5rem; border-bottom:1px solid #ece5d7; vertical-align:top; white-space:nowrap; }
    @media (max-width: 720px) {
      .columns-list { columns:1; }
      .result-header { grid-template-columns:1fr; }
      button.compact { justify-self:start; }
      .data-item-wide { grid-column:auto; }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LegacyProductMappingImportsPageComponent implements OnInit {
  private readonly api = inject(FiscalImportsApiService);
  private readonly feedbackService = inject(FeedbackService);
  protected readonly permissionService = inject(PermissionService);

  protected sourceName = 'Sistema anterior';
  protected readonly selectedFile = signal<File | null>(null);
  protected readonly importResult = signal<LegacyProductMappingImportResponse | null>(null);
  protected readonly history = signal<LegacyProductMappingImportBatchSummary[]>([]);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly historyError = signal<string | null>(null);
  protected readonly importing = signal(false);
  protected readonly historyLoading = signal(false);

  ngOnInit(): void {
    if (this.permissionService.canWriteMasterData()) {
      void this.loadHistory();
    }
  }

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.errorMessage.set(null);
    this.importResult.set(null);

    if (!file) {
      this.selectedFile.set(null);
      return;
    }

    if (!isCsvFile(file.name)) {
      this.selectedFile.set(null);
      this.errorMessage.set('El archivo debe tener extensión .csv.');
      return;
    }

    this.selectedFile.set(file);
  }

  protected async importFile(): Promise<void> {
    if (this.importing()) {
      return;
    }

    if (!this.permissionService.canWriteMasterData()) {
      this.errorMessage.set('No tienes permiso para importar mappings SAT.');
      return;
    }

    const file = this.selectedFile();
    if (!file) {
      this.errorMessage.set('Selecciona un archivo CSV para continuar.');
      return;
    }

    if (!isCsvFile(file.name)) {
      this.errorMessage.set('El archivo debe tener extensión .csv.');
      return;
    }

    this.importing.set(true);
    this.errorMessage.set(null);
    this.importResult.set(null);

    try {
      const result = await firstValueFrom(this.api.importLegacyProductMappingsCsv(file, this.sourceName));
      this.importResult.set(result);
      const feedback = buildImportFeedback(result);
      this.feedbackService.show(feedback.level, feedback.message);
      await this.loadHistory();
    } catch (error) {
      this.errorMessage.set(extractApiErrorMessage(error, 'No se pudo importar el archivo. Verifica el formato del CSV.'));
    } finally {
      this.importing.set(false);
    }
  }

  protected async loadHistory(): Promise<void> {
    if (!this.permissionService.canWriteMasterData() || this.historyLoading()) {
      return;
    }

    this.historyLoading.set(true);
    this.historyError.set(null);

    try {
      this.history.set(await firstValueFrom(this.api.listLegacyProductMappingBatches()));
    } catch (error) {
      this.historyError.set(extractApiErrorMessage(error, 'No se pudo cargar el historial de importaciones.'));
    } finally {
      this.historyLoading.set(false);
    }
  }

  protected getStatusLabel(value: string | null | undefined): string {
    return getDisplayLabel(value);
  }

  protected getStatusTone(status: string): StatusBadgeTone {
    switch (status) {
      case 'Validated':
      case 'Completed':
        return 'success';
      case 'Uploaded':
      case 'Parsed':
        return 'info';
      case 'Failed':
      case 'ValidationFailed':
        return 'danger';
      default:
        return 'neutral';
    }
  }

  protected getResultTone(result: LegacyProductMappingImportResponse): StatusBadgeTone {
    if (!result.isSuccess || result.status === 'Failed') {
      return 'danger';
    }

    if (result.wasAlreadyImported || result.invalidRows > 0 || result.ambiguousRows > 0 || result.skippedRows > 0) {
      return 'warning';
    }

    return 'success';
  }

  protected formatDateTime(value: string | null | undefined): string {
    if (!value) {
      return 'N/D';
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return value;
    }

    return date.toLocaleString('es-MX', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit'
    });
  }
}

function isCsvFile(fileName: string): boolean {
  return fileName.trim().toLowerCase().endsWith('.csv');
}

function buildImportFeedback(result: LegacyProductMappingImportResponse): { level: FeedbackLevel; message: string } {
  if (result.wasAlreadyImported) {
    return {
      level: 'info',
      message: 'El archivo ya había sido importado.'
    };
  }

  if (!result.isSuccess || result.status === 'Failed') {
    return {
      level: 'error',
      message: 'No se pudo importar el archivo. Verifica el formato del CSV.'
    };
  }

  if (result.invalidRows > 0 || result.ambiguousRows > 0 || result.skippedRows > 0) {
    return {
      level: 'warning',
      message: 'La importación terminó con advertencias. Revisa el resumen.'
    };
  }

  return {
    level: 'success',
    message: 'Mappings SAT importados correctamente.'
  };
}
