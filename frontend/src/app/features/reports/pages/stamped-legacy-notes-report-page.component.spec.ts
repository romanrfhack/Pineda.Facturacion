import { HttpHeaders, HttpResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { ReportsApiService } from '../infrastructure/reports-api.service';
import { StampedLegacyNotesReportPageComponent } from './stamped-legacy-notes-report-page.component';

describe('StampedLegacyNotesReportPageComponent', () => {
  it('renders filters and loads grid results with current filters', async () => {
    const api = {
      searchStampedLegacyNotes: vi.fn().mockReturnValue(of({
        page: 1,
        pageSize: 50,
        totalCount: 1,
        totalPages: 1,
        items: [
          {
            stampedAtUtc: '2026-05-04T15:00:00Z',
            stampedAtLocalText: '2026-05-04 09:00:00',
            legacyOrderId: '1171335',
            legacyOrderNumber: 'REF-1171335',
            billingDocumentId: 200,
            fiscalDocumentId: 300,
            series: 'A',
            folio: '100',
            uuid: 'UUID-1',
            fiscalStatus: 'Stamped',
            cancellationStatus: null,
            receiverName: 'Cliente Fiscal',
            receiverRfc: 'AAA010101AAA',
            cfdiTotal: 116,
            noteAmountInCfdi: 116,
            currencyCode: 'MXN',
            itemCount: 1
          }
        ]
      })),
      exportStampedLegacyNotes: vi.fn()
    };

    await configure(api);
    const fixture = TestBed.createComponent(StampedLegacyNotesReportPageComponent);
    fixture.detectChanges();

    const inputs = fixture.nativeElement.querySelectorAll('input') as NodeListOf<HTMLInputElement>;
    setInput(inputs[0], '2026-05-01');
    setInput(inputs[1], '2026-05-04');
    setInput(inputs[6], '1171335');
    fixture.detectChanges();

    clickButton(fixture.nativeElement, 'Buscar');
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.searchStampedLegacyNotes).toHaveBeenCalledWith(expect.objectContaining({
      fromDate: '2026-05-01',
      toDate: '2026-05-04',
      legacyOrderId: '1171335',
      page: 1,
      pageSize: 50
    }));
    expect(fixture.nativeElement.textContent).toContain('1171335');
    expect(fixture.nativeElement.textContent).toContain('Cliente Fiscal');
    expect(fixture.nativeElement.textContent).toContain('Mostrando 1 de 1 registros.');
  });

  it('shows empty state and backend errors', async () => {
    const api = {
      searchStampedLegacyNotes: vi.fn()
        .mockReturnValueOnce(of({ page: 1, pageSize: 50, totalCount: 0, totalPages: 0, items: [] }))
        .mockReturnValueOnce(throwError(() => ({ error: { errorMessage: 'Backend error' } }))),
      exportStampedLegacyNotes: vi.fn()
    };

    await configure(api);
    const fixture = TestBed.createComponent(StampedLegacyNotesReportPageComponent);
    fixture.detectChanges();

    clickButton(fixture.nativeElement, 'Buscar');
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No se encontraron notas timbradas');

    clickButton(fixture.nativeElement, 'Buscar');
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Backend error');
  });

  it('prevents search and export when dates are invalid', async () => {
    const feedbackService = { show: vi.fn() };
    const api = {
      searchStampedLegacyNotes: vi.fn(),
      exportStampedLegacyNotes: vi.fn()
    };

    await configure(api, feedbackService);
    const fixture = TestBed.createComponent(StampedLegacyNotesReportPageComponent);
    fixture.detectChanges();

    const inputs = fixture.nativeElement.querySelectorAll('input') as NodeListOf<HTMLInputElement>;
    setInput(inputs[0], '2026-05-05');
    setInput(inputs[1], '2026-05-04');
    fixture.detectChanges();

    clickButton(fixture.nativeElement, 'Buscar');
    clickButton(fixture.nativeElement, 'Exportar Excel');
    await fixture.whenStable();

    expect(api.searchStampedLegacyNotes).not.toHaveBeenCalled();
    expect(api.exportStampedLegacyNotes).not.toHaveBeenCalled();
    expect(feedbackService.show).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('La fecha inicial no puede ser mayor a la fecha final.');
  });

  it('exports xlsx with the current filters', async () => {
    if (!URL.createObjectURL) {
      Object.defineProperty(URL, 'createObjectURL', { value: vi.fn(), configurable: true });
    }

    if (!URL.revokeObjectURL) {
      Object.defineProperty(URL, 'revokeObjectURL', { value: vi.fn(), configurable: true });
    }

    const createObjectUrl = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:url');
    const revokeObjectUrl = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    const clickedDownloads: string[] = [];
    const originalCreateElement = document.createElement.bind(document);
    vi.spyOn(document, 'createElement').mockImplementation((tagName: string) => {
      const element = originalCreateElement(tagName);
      if (tagName.toLowerCase() === 'a') {
        vi.spyOn(element, 'click').mockImplementation(() => {
          clickedDownloads.push((element as HTMLAnchorElement).download);
        });
      }

      return element;
    });

    const api = {
      searchStampedLegacyNotes: vi.fn(),
      exportStampedLegacyNotes: vi.fn().mockReturnValue(of(new HttpResponse({
        body: new Blob(['xlsx'], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }),
        headers: new HttpHeaders({ 'content-disposition': 'attachment; filename="reporte.xlsx"' })
      })))
    };

    await configure(api);
    const fixture = TestBed.createComponent(StampedLegacyNotesReportPageComponent);
    fixture.detectChanges();

    const inputs = fixture.nativeElement.querySelectorAll('input') as NodeListOf<HTMLInputElement>;
    setInput(inputs[0], '2026-05-01');
    setInput(inputs[1], '2026-05-04');
    setInput(inputs[6], '1171335');
    fixture.detectChanges();

    clickButton(fixture.nativeElement, 'Exportar Excel');
    await fixture.whenStable();

    expect(api.exportStampedLegacyNotes).toHaveBeenCalledWith(expect.objectContaining({
      fromDate: '2026-05-01',
      toDate: '2026-05-04',
      legacyOrderId: '1171335'
    }));
    expect(clickedDownloads).toContain('reporte.xlsx');

    createObjectUrl.mockRestore();
    revokeObjectUrl.mockRestore();
    vi.restoreAllMocks();
  });
});

async function configure(api: unknown, feedbackService: { show: ReturnType<typeof vi.fn> } = { show: vi.fn() }): Promise<void> {
  await TestBed.configureTestingModule({
    imports: [StampedLegacyNotesReportPageComponent],
    providers: [
      { provide: ReportsApiService, useValue: api },
      { provide: FeedbackService, useValue: feedbackService }
    ]
  }).compileComponents();
}

function setInput(input: HTMLInputElement, value: string): void {
  input.value = value;
  input.dispatchEvent(new Event('input', { bubbles: true }));
}

function clickButton(root: HTMLElement, label: string): void {
  const button = Array.from(root.querySelectorAll('button')).find((candidate) => candidate.textContent?.includes(label));
  if (!(button instanceof HTMLButtonElement)) {
    throw new Error(`Button not found: ${label}`);
  }

  button.click();
}
