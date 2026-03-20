import { TestBed } from '@angular/core/testing';
import { ImportBatchSummaryCardComponent } from './import-batch-summary-card.component';

describe('ImportBatchSummaryCardComponent', () => {
  it('renders the batch counts', async () => {
    await TestBed.configureTestingModule({
      imports: [ImportBatchSummaryCardComponent]
    }).compileComponents();

    const fixture = TestBed.createComponent(ImportBatchSummaryCardComponent);
    fixture.componentRef.setInput('summary', {
      batchId: 12,
      sourceFileName: 'receivers.xlsx',
      status: 'Validated',
      totalRows: 20,
      validRows: 15,
      invalidRows: 3,
      ignoredRows: 2,
      existingMasterMatches: 4,
      duplicateRowsInFile: 1,
      appliedRows: 0,
      applyFailedRows: 0,
      applySkippedRows: 0,
      completedAtUtc: null,
      lastAppliedAtUtc: null,
      errorMessage: null
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Batch: 12');
    expect(fixture.nativeElement.textContent).toContain('Valid rows: 15');
    expect(fixture.nativeElement.textContent).toContain('Duplicate rows: 1');
  });
});
