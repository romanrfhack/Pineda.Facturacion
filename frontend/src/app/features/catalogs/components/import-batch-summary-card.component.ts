import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ImportBatchSummary } from '../models/catalogs.models';

@Component({
  selector: 'app-import-batch-summary-card',
  template: `
    @if (summary(); as current) {
      <section class="card">
        <h3>Batch summary</h3>
        <div class="summary-grid">
          <p><strong>Batch:</strong> {{ current.batchId }}</p>
          <p><strong>Status:</strong> {{ current.status }}</p>
          <p><strong>Source file:</strong> {{ current.sourceFileName || 'N/A' }}</p>
          <p><strong>Total rows:</strong> {{ current.totalRows }}</p>
          <p><strong>Valid rows:</strong> {{ current.validRows }}</p>
          <p><strong>Invalid rows:</strong> {{ current.invalidRows }}</p>
          <p><strong>Ignored rows:</strong> {{ current.ignoredRows }}</p>
          <p><strong>Existing master matches:</strong> {{ current.existingMasterMatches }}</p>
          <p><strong>Duplicate rows:</strong> {{ current.duplicateRowsInFile }}</p>
          <p><strong>Applied rows:</strong> {{ current.appliedRows }}</p>
          <p><strong>Apply failed rows:</strong> {{ current.applyFailedRows }}</p>
          <p><strong>Apply skipped rows:</strong> {{ current.applySkippedRows }}</p>
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
}
