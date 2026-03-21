import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { PermissionService } from '../../../core/auth/permission.service';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { ImportBatchSummaryCardComponent } from '../components/import-batch-summary-card.component';
import { FiscalImportsApiService } from '../infrastructure/fiscal-imports-api.service';
import { ApplyImportBatchResponse, ImportApplyMode, ImportBatchSummary, ReceiverImportRow } from '../models/catalogs.models';
import { getDisplayLabel } from '../../../shared/ui/display-labels';
import { countEligibleImportRows, ImportApplySelectionMode, resolveSelectedRowNumbers } from '../application/import-apply';

@Component({
  selector: 'app-receiver-imports-page',
  imports: [FormsModule, ImportBatchSummaryCardComponent],
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">Catálogos / Importaciones de receptores</p>
        <h2>Vista previa y aplicación de lotes de receptores</h2>
      </header>

      <section class="card">
        <div class="toolbar">
          <label>
            <span>Cargar lote por id</span>
            <input [(ngModel)]="batchIdInput" name="batchIdInput" type="number" min="1" />
          </label>
          <div class="actions">
            <button type="button" class="secondary" (click)="loadBatch()">Cargar lote</button>
          </div>
        </div>

        @if (permissionService.canWriteMasterData()) {
          <div class="upload-grid">
            <label>
              <span>Vista previa de archivo .xlsx</span>
              <input type="file" accept=".xlsx" (change)="onFileSelected($event)" />
            </label>
            <button type="button" (click)="preview()" [disabled]="previewing() || !selectedFile()">
              {{ previewing() ? 'Generando vista previa...' : 'Vista previa de importación de receptores' }}
            </button>
          </div>
        } @else {
          <p class="helper">Tu rol puede consultar lotes, pero no generar vistas previas ni aplicar importaciones.</p>
        }

        @if (errorMessage()) {
          <p class="error">{{ errorMessage() }}</p>
        }
      </section>

      <app-import-batch-summary-card [summary]="summary()" />

      @if (summary()) {
        <section class="card">
          <h3>Aplicar lote</h3>
          <p class="helper">Las filas elegibles son las válidas con acción sugerida Crear o Actualizar. Actualmente hay {{ eligibleRowsCount() }}.</p>
          <div class="form-grid">
            <label>
              <span>Modo de aplicación</span>
              <select [(ngModel)]="applyMode" name="applyMode" [disabled]="!permissionService.canWriteMasterData()">
                <option value="CreateOnly">Solo crear</option>
                <option value="CreateAndUpdate">Crear y actualizar</option>
              </select>
            </label>

            <fieldset class="selection-mode" [disabled]="!permissionService.canWriteMasterData()">
              <legend>Filas a aplicar</legend>
              <label class="radio-option">
                <input type="radio" name="receiverSelectionMode" [value]="'allEligible'" [(ngModel)]="applySelectionMode" />
                <span>Aplicar todas las filas elegibles</span>
              </label>
              <label class="radio-option">
                <input type="radio" name="receiverSelectionMode" [value]="'specificRows'" [(ngModel)]="applySelectionMode" />
                <span>Aplicar filas específicas</span>
              </label>
            </fieldset>

            @if (applySelectionMode === 'specificRows') {
              <label>
                <span>Números de fila</span>
                <input [(ngModel)]="selectedRowsText" name="selectedRowsText" placeholder="1,2,7" [disabled]="!permissionService.canWriteMasterData()" />
              </label>
            }

            <label class="checkbox">
              <input [(ngModel)]="stopOnFirstError" name="stopOnFirstError" type="checkbox" [disabled]="!permissionService.canWriteMasterData()" />
              <span>Detener en el primer error</span>
            </label>

            <button type="button" (click)="apply()" [disabled]="applying() || !permissionService.canWriteMasterData()">
              {{ applying() ? 'Aplicando...' : 'Aplicar lote de receptores' }}
            </button>
          </div>

          @if (applyResult()) {
            <p class="helper">
              Aplicadas {{ applyResult()!.appliedRows }}, omitidas {{ applyResult()!.skippedRows }}, con error {{ applyResult()!.failedRows }}, ya aplicadas {{ applyResult()!.alreadyAppliedRows }}.
            </p>
          }
        </section>

        <section class="card">
          <h3>Filas del lote</h3>
          @if (!rows().length) {
            <p class="helper">Aún no hay datos de filas cargados.</p>
          } @else {
            <div class="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Fila</th>
                    <th>Estatus</th>
                    <th>Acción sugerida</th>
                    <th>RFC</th>
                    <th>Razón social</th>
                    <th>Errores de validación</th>
                    <th>Id existente</th>
                    <th>Estatus de aplicación</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of rows(); track row.rowNumber) {
                    <tr>
                      <td>{{ row.rowNumber }}</td>
                      <td>{{ getDisplayLabel(row.status) }}</td>
                      <td>{{ getDisplayLabel(row.suggestedAction) }}</td>
                      <td>{{ row.normalizedRfc || 'N/D' }}</td>
                      <td>{{ row.normalizedLegalName || 'N/D' }}</td>
                      <td>{{ row.validationErrors.join(', ') || 'Ninguno' }}</td>
                      <td>{{ row.existingMasterEntityId || 'N/D' }}</td>
                      <td>{{ getDisplayLabel(row.applyStatus) }}</td>
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
    .card { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    .toolbar, .upload-grid, .form-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:1rem; align-items:end; }
    label { display:grid; gap:0.35rem; }
    input, select, button { font:inherit; }
    input, select { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; }
    fieldset { border:1px solid #d8d1c2; border-radius:0.8rem; padding:0.75rem 1rem; margin:0; }
    legend { padding:0 0.35rem; }
    .checkbox { display:flex; align-items:center; gap:0.5rem; }
    .checkbox input { width:auto; }
    .radio-option { display:flex; align-items:center; gap:0.5rem; }
    .radio-option input { width:auto; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    button.secondary { background:#d8c49b; color:#182533; }
    .helper { margin:0; color:#5f6b76; }
    .error { margin:0; color:#7a2020; }
    .table-wrap { overflow:auto; }
    table { width:100%; border-collapse:collapse; }
    th, td { text-align:left; padding:0.75rem 0.5rem; border-bottom:1px solid #ece5d7; vertical-align:top; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReceiverImportsPageComponent {
  private readonly api = inject(FiscalImportsApiService);
  private readonly feedbackService = inject(FeedbackService);
  protected readonly permissionService = inject(PermissionService);

  protected batchIdInput: number | null = null;
  protected readonly selectedFile = signal<File | null>(null);
  protected readonly summary = signal<ImportBatchSummary | null>(null);
  protected readonly rows = signal<ReceiverImportRow[]>([]);
  protected readonly applyResult = signal<ApplyImportBatchResponse | null>(null);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly previewing = signal(false);
  protected readonly applying = signal(false);
  protected readonly eligibleRowsCount = signal(0);

  protected applyMode: ImportApplyMode = 'CreateOnly';
  protected applySelectionMode: ImportApplySelectionMode = 'allEligible';
  protected selectedRowsText = '';
  protected stopOnFirstError = false;
  protected readonly getDisplayLabel = getDisplayLabel;

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile.set(input.files?.[0] ?? null);
  }

  protected async preview(): Promise<void> {
    const file = this.selectedFile();
    if (!file) {
      return;
    }

    this.previewing.set(true);
    this.errorMessage.set(null);
    try {
      const summary = await firstValueFrom(this.api.previewReceivers(file));
      this.summary.set(summary);
      this.batchIdInput = summary.batchId ?? null;
      this.applyResult.set(null);
      if (summary.batchId) {
        await this.loadRows(summary.batchId);
      }
      this.feedbackService.show('success', 'Vista previa de importación de receptores creada.');
    } catch (error) {
      this.errorMessage.set(extractApiErrorMessage(error));
    } finally {
      this.previewing.set(false);
    }
  }

  protected async loadBatch(resetApplyResult = true): Promise<void> {
    if (!this.batchIdInput) {
      return;
    }

    this.errorMessage.set(null);
    try {
      this.summary.set(await firstValueFrom(this.api.getReceiverBatch(this.batchIdInput)));
      await this.loadRows(this.batchIdInput);
      if (resetApplyResult) {
        this.applyResult.set(null);
      }
    } catch (error) {
      this.errorMessage.set(extractApiErrorMessage(error));
    }
  }

  protected async apply(): Promise<void> {
    const batchId = this.summary()?.batchId;
    if (!batchId || !this.permissionService.canWriteMasterData()) {
      return;
    }

    if (!window.confirm('¿Aplicar este lote de receptores a los datos maestros?')) {
      return;
    }

    this.applying.set(true);
    this.errorMessage.set(null);
    try {
      const selectedRowsResolution = resolveSelectedRowNumbers(this.applySelectionMode, this.selectedRowsText);
      if (selectedRowsResolution.errorMessage) {
        this.errorMessage.set(selectedRowsResolution.errorMessage);
        return;
      }

      const result = await firstValueFrom(this.api.applyReceiverBatch(batchId, {
        applyMode: this.applyMode,
        selectedRowNumbers: selectedRowsResolution.selectedRowNumbers,
        stopOnFirstError: this.stopOnFirstError
      }));
      this.applyResult.set(result);
      this.feedbackService.show('success', 'Lote aplicado correctamente.');
      await this.loadBatch(false);
    } catch (error) {
      this.errorMessage.set(extractApiErrorMessage(error, 'No fue posible aplicar el lote.'));
    } finally {
      this.applying.set(false);
    }
  }

  private async loadRows(batchId: number): Promise<void> {
    const rows = await firstValueFrom(this.api.listReceiverRows(batchId));
    this.rows.set(rows);
    this.eligibleRowsCount.set(countEligibleImportRows(rows));
  }
}
