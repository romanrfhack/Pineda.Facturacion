import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { PermissionService } from '../../../core/auth/permission.service';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { ImportBatchSummaryCardComponent } from '../components/import-batch-summary-card.component';
import { FiscalImportsApiService } from '../infrastructure/fiscal-imports-api.service';
import { ApplyImportBatchResponse, ImportApplyMode, ImportBatchSummary, ReceiverImportRow } from '../models/catalogs.models';

@Component({
  selector: 'app-receiver-imports-page',
  imports: [FormsModule, ImportBatchSummaryCardComponent],
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">Catalogs / Receiver imports</p>
        <h2>Preview and apply receiver import batches</h2>
      </header>

      <section class="card">
        <div class="toolbar">
          <label>
            <span>Load batch by id</span>
            <input [(ngModel)]="batchIdInput" name="batchIdInput" type="number" min="1" />
          </label>
          <div class="actions">
            <button type="button" class="secondary" (click)="loadBatch()">Load batch</button>
          </div>
        </div>

        @if (permissionService.canWriteMasterData()) {
          <div class="upload-grid">
            <label>
              <span>Preview .xlsx file</span>
              <input type="file" accept=".xlsx" (change)="onFileSelected($event)" />
            </label>
            <button type="button" (click)="preview()" [disabled]="previewing() || !selectedFile()">
              {{ previewing() ? 'Previewing...' : 'Preview receiver import' }}
            </button>
          </div>
        } @else {
          <p class="helper">Your role can inspect batches but cannot preview or apply imports.</p>
        }

        @if (errorMessage()) {
          <p class="error">{{ errorMessage() }}</p>
        }
      </section>

      <app-import-batch-summary-card [summary]="summary()" />

      @if (summary()) {
        <section class="card">
          <h3>Apply batch</h3>
          <div class="form-grid">
            <label>
              <span>Apply mode</span>
              <select [(ngModel)]="applyMode" name="applyMode" [disabled]="!permissionService.canWriteMasterData()">
                <option value="CreateOnly">Create only</option>
                <option value="CreateAndUpdate">Create and update</option>
              </select>
            </label>

            <label>
              <span>Selected row numbers</span>
              <input [(ngModel)]="selectedRowsText" name="selectedRowsText" placeholder="1,2,7" [disabled]="!permissionService.canWriteMasterData()" />
            </label>

            <label class="checkbox">
              <input [(ngModel)]="stopOnFirstError" name="stopOnFirstError" type="checkbox" [disabled]="!permissionService.canWriteMasterData()" />
              <span>Stop on first error</span>
            </label>

            <button type="button" (click)="apply()" [disabled]="applying() || !permissionService.canWriteMasterData()">
              {{ applying() ? 'Applying...' : 'Apply receiver batch' }}
            </button>
          </div>

          @if (applyResult()) {
            <p class="helper">
              Applied {{ applyResult()!.appliedRows }}, skipped {{ applyResult()!.skippedRows }}, failed {{ applyResult()!.failedRows }}, already applied {{ applyResult()!.alreadyAppliedRows }}.
            </p>
          }
        </section>

        <section class="card">
          <h3>Batch rows</h3>
          @if (!rows().length) {
            <p class="helper">No row data loaded yet.</p>
          } @else {
            <div class="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Row</th>
                    <th>Status</th>
                    <th>Suggested action</th>
                    <th>RFC</th>
                    <th>Legal name</th>
                    <th>Validation errors</th>
                    <th>Existing id</th>
                    <th>Apply status</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of rows(); track row.rowNumber) {
                    <tr>
                      <td>{{ row.rowNumber }}</td>
                      <td>{{ row.status }}</td>
                      <td>{{ row.suggestedAction }}</td>
                      <td>{{ row.normalizedRfc || 'N/A' }}</td>
                      <td>{{ row.normalizedLegalName || 'N/A' }}</td>
                      <td>{{ row.validationErrors.join(', ') || 'None' }}</td>
                      <td>{{ row.existingMasterEntityId || 'N/A' }}</td>
                      <td>{{ row.applyStatus }}</td>
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

  protected applyMode: ImportApplyMode = 'CreateOnly';
  protected selectedRowsText = '';
  protected stopOnFirstError = false;

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
      this.feedbackService.show('success', 'Receiver import preview created.');
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
      this.summary.set(await firstValueFrom(this.api.getReceiverBatch(this.batchIdInput)));
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

    if (!window.confirm('Apply this receiver import batch to master data?')) {
      return;
    }

    this.applying.set(true);
    this.errorMessage.set(null);
    try {
      const result = await firstValueFrom(this.api.applyReceiverBatch(batchId, {
        applyMode: this.applyMode,
        selectedRowNumbers: parseSelectedRows(this.selectedRowsText),
        stopOnFirstError: this.stopOnFirstError
      }));
      this.applyResult.set(result);
      this.feedbackService.show('success', 'Receiver import batch applied.');
      await this.loadBatch();
    } catch (error) {
      this.errorMessage.set(extractApiErrorMessage(error));
    } finally {
      this.applying.set(false);
    }
  }

  private async loadRows(batchId: number): Promise<void> {
    this.rows.set(await firstValueFrom(this.api.listReceiverRows(batchId)));
  }
}

function parseSelectedRows(value: string): number[] | null {
  const items = value
    .split(',')
    .map((item) => Number(item.trim()))
    .filter((item) => Number.isInteger(item) && item > 0);

  return items.length ? items : null;
}
