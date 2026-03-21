import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ImportBatchSummary } from '../models/catalogs.models';

@Component({
  selector: 'app-import-batch-summary-card',
  template: `
    @if (summary(); as current) {
      <section class="card">
        <h3>Resumen del lote</h3>
        <div class="summary-grid">
          <p><strong>Lote:</strong> {{ current.batchId }}</p>
          <p><strong>Estatus:</strong> {{ getDisplayLabel(current.status) }}</p>
          <p><strong>Archivo origen:</strong> {{ current.sourceFileName || 'N/D' }}</p>
          <p><strong>Total de filas:</strong> {{ current.totalRows }}</p>
          <p><strong>Filas válidas:</strong> {{ current.validRows }}</p>
          <p><strong>Filas inválidas:</strong> {{ current.invalidRows }}</p>
          <p><strong>Filas ignoradas:</strong> {{ current.ignoredRows }}</p>
          <p><strong>Coincidencias existentes:</strong> {{ current.existingMasterMatches }}</p>
          <p><strong>Filas duplicadas:</strong> {{ current.duplicateRowsInFile }}</p>
          <p><strong>Filas aplicadas:</strong> {{ current.appliedRows }}</p>
          <p><strong>Filas con error al aplicar:</strong> {{ current.applyFailedRows }}</p>
          <p><strong>Filas omitidas al aplicar:</strong> {{ current.applySkippedRows }}</p>
        </div>

        @if (current.errorMessage) {
          <p class="error">{{ current.errorMessage }}</p>
        }
      </section>
    }
  `,
  styles: [`
    .card { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; }
    .summary-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:0.5rem 1rem; }
    h3, p { margin:0; }
    .error { margin-top:0.75rem; color:#7a2020; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ImportBatchSummaryCardComponent {
  readonly summary = input<ImportBatchSummary | null>(null);
  protected readonly getDisplayLabel = getDisplayLabel;
}
import { getDisplayLabel } from '../../../shared/ui/display-labels';
