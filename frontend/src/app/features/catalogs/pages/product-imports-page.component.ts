import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { PermissionService } from '../../../core/auth/permission.service';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { ImportBatchSummaryCardComponent } from '../components/import-batch-summary-card.component';
import { FiscalImportsApiService } from '../infrastructure/fiscal-imports-api.service';
import { ApplyImportBatchResponse, ImportApplyMode, ImportBatchSummary, ProductImportRow } from '../models/catalogs.models';
import { getDisplayLabel } from '../../../shared/ui/display-labels';

@Component({
  selector: 'app-product-imports-page',
  imports: [FormsModule, ImportBatchSummaryCardComponent],
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">Catálogos / Importaciones de productos</p>
        <h2>Vista previa y aplicación de lotes de perfiles fiscales de producto</h2>
      </header>

      <section class="card">
        <div class="toolbar">
          <label>
            <span>Cargar lote por id</span>
            <input [(ngModel)]="batchIdInput" name="batchIdInput" type="number" min="1" />
          </label>
          <button type="button" class="secondary" (click)="loadBatch()">Cargar lote</button>
        </div>

        @if (permissionService.canWriteMasterData()) {
          <div class="form-grid">
            <label><span>Vista previa de archivo .xlsx</span><input type="file" accept=".xlsx" (change)="onFileSelected($event)" /></label>
            <label><span>Código de objeto de impuesto predeterminado</span><input [(ngModel)]="defaultTaxObjectCode" name="defaultTaxObjectCode" /></label>
            <label><span>Tasa de IVA predeterminada</span><input [(ngModel)]="defaultVatRate" name="defaultVatRate" type="number" min="0" step="0.0001" /></label>
            <label><span>Texto de unidad predeterminado</span><input [(ngModel)]="defaultUnitText" name="defaultUnitText" /></label>
            <button type="button" (click)="preview()" [disabled]="previewing() || !selectedFile()">{{ previewing() ? 'Generando vista previa...' : 'Vista previa de importación de productos' }}</button>
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
          <div class="form-grid">
            <label>
              <span>Modo de aplicación</span>
              <select [(ngModel)]="applyMode" name="applyMode" [disabled]="!permissionService.canWriteMasterData()">
                <option value="CreateOnly">Solo crear</option>
                <option value="CreateAndUpdate">Crear y actualizar</option>
              </select>
            </label>

            <label>
              <span>Números de fila seleccionados</span>
              <input [(ngModel)]="selectedRowsText" name="selectedRowsText" placeholder="1,2,7" [disabled]="!permissionService.canWriteMasterData()" />
            </label>

            <label class="checkbox">
              <input [(ngModel)]="stopOnFirstError" name="stopOnFirstError" type="checkbox" [disabled]="!permissionService.canWriteMasterData()" />
              <span>Detener en el primer error</span>
            </label>

            <button type="button" (click)="apply()" [disabled]="applying() || !permissionService.canWriteMasterData()">
              {{ applying() ? 'Aplicando...' : 'Aplicar lote de productos' }}
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
                    <th>Código interno</th>
                    <th>Descripción</th>
                    <th>Objeto de impuesto</th>
                    <th>IVA</th>
                    <th>Errores de validación</th>
                    <th>Estatus de aplicación</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of rows(); track row.rowNumber) {
                    <tr>
                      <td>{{ row.rowNumber }}</td>
                      <td>{{ getDisplayLabel(row.status) }}</td>
                      <td>{{ getDisplayLabel(row.suggestedAction) }}</td>
                      <td>{{ row.normalizedInternalCode || 'N/D' }}</td>
                      <td>{{ row.normalizedDescription || 'N/D' }}</td>
                      <td>{{ row.normalizedTaxObjectCode || 'Requiere complemento' }}</td>
                      <td>{{ row.normalizedVatRate ?? 'Requiere complemento' }}</td>
                      <td>{{ row.validationErrors.join(', ') || 'Ninguno' }}</td>
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
    .toolbar, .form-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:1rem; align-items:end; }
    label { display:grid; gap:0.35rem; }
    input, select, button { font:inherit; }
    input, select { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; }
    .checkbox { display:flex; align-items:center; gap:0.5rem; }
    .checkbox input { width:auto; }
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
export class ProductImportsPageComponent {
  private readonly api = inject(FiscalImportsApiService);
  private readonly feedbackService = inject(FeedbackService);
  protected readonly permissionService = inject(PermissionService);

  protected batchIdInput: number | null = null;
  protected readonly selectedFile = signal<File | null>(null);
  protected readonly summary = signal<ImportBatchSummary | null>(null);
  protected readonly rows = signal<ProductImportRow[]>([]);
  protected readonly applyResult = signal<ApplyImportBatchResponse | null>(null);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly previewing = signal(false);
  protected readonly applying = signal(false);

  protected applyMode: ImportApplyMode = 'CreateOnly';
  protected selectedRowsText = '';
  protected stopOnFirstError = false;
  protected defaultTaxObjectCode = '';
  protected defaultVatRate: number | null = null;
  protected defaultUnitText = '';
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
      const summary = await firstValueFrom(this.api.previewProducts(file, {
        defaultTaxObjectCode: this.defaultTaxObjectCode.trim() || undefined,
        defaultVatRate: this.defaultVatRate,
        defaultUnitText: this.defaultUnitText.trim() || undefined
      }));
      this.summary.set(summary);
      this.batchIdInput = summary.batchId ?? null;
      this.applyResult.set(null);
      if (summary.batchId) {
        await this.loadRows(summary.batchId);
      }
      this.feedbackService.show('success', 'Vista previa de importación de productos creada.');
    } catch (error) {
      this.errorMessage.set(extractApiErrorMessage(error));
    } finally {
      this.previewing.set(false);
    }
  }

  protected async loadBatch(): Promise<void> {
    if (!this.batchIdInput) {
      return;
    }

    this.errorMessage.set(null);
    try {
      this.summary.set(await firstValueFrom(this.api.getProductBatch(this.batchIdInput)));
      await this.loadRows(this.batchIdInput);
      this.applyResult.set(null);
    } catch (error) {
      this.errorMessage.set(extractApiErrorMessage(error));
    }
  }

  protected async apply(): Promise<void> {
    const batchId = this.summary()?.batchId;
    if (!batchId || !this.permissionService.canWriteMasterData()) {
      return;
    }

    if (!window.confirm('¿Aplicar este lote de productos a los datos maestros?')) {
      return;
    }

    this.applying.set(true);
    this.errorMessage.set(null);
    try {
      const result = await firstValueFrom(this.api.applyProductBatch(batchId, {
        applyMode: this.applyMode,
        selectedRowNumbers: parseSelectedRows(this.selectedRowsText),
        stopOnFirstError: this.stopOnFirstError
      }));
      this.applyResult.set(result);
      this.feedbackService.show('success', 'Lote de importación de productos aplicado.');
      await this.loadBatch();
    } catch (error) {
      this.errorMessage.set(extractApiErrorMessage(error));
    } finally {
      this.applying.set(false);
    }
  }

  private async loadRows(batchId: number): Promise<void> {
    this.rows.set(await firstValueFrom(this.api.listProductRows(batchId)));
  }
}

function parseSelectedRows(value: string): number[] | null {
  const items = value
    .split(',')
    .map((item) => Number(item.trim()))
    .filter((item) => Number.isInteger(item) && item > 0);

  return items.length ? items : null;
}
