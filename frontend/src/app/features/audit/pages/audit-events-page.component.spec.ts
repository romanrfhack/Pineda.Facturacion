import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AuditEventsPageComponent } from './audit-events-page.component';
import { AuditApiService } from '../infrastructure/audit-api.service';
import { FeedbackService } from '../../../core/ui/feedback.service';

describe('AuditEventsPageComponent', () => {
  it('renders list rows and detail safely', async () => {
    await TestBed.configureTestingModule({
      imports: [AuditEventsPageComponent],
      providers: [
        {
          provide: AuditApiService,
          useValue: {
            list: vi.fn().mockReturnValue(of({
              page: 1,
              pageSize: 25,
              totalCount: 1,
              items: [
                {
                  id: 1,
                  occurredAtUtc: '2026-03-20T10:00:00Z',
                  actorUsername: 'admin',
                  actionType: 'FiscalDocument.Stamp',
                  entityType: 'FiscalDocument',
                  entityId: '50',
                  outcome: 'Stamped',
                  correlationId: 'corr-001',
                  requestSummaryJson: '{"billingDocumentId":30}',
                  responseSummaryJson: '{"fiscalDocumentId":50}',
                  errorMessage: null,
                  ipAddress: '127.0.0.1',
                  userAgent: 'Vitest',
                  createdAtUtc: '2026-03-20T10:00:00Z'
                }
              ]
            }))
          }
        },
        {
          provide: FeedbackService,
          useValue: { show: vi.fn() }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(AuditEventsPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('FiscalDocument.Stamp');
    expect(fixture.nativeElement.textContent).toContain('corr-001');
    expect(fixture.nativeElement.textContent).toContain('Request summary');
  });

  it('shows error state when loading fails', async () => {
    await TestBed.configureTestingModule({
      imports: [AuditEventsPageComponent],
      providers: [
        {
          provide: AuditApiService,
          useValue: {
            list: vi.fn().mockReturnValue(throwError(() => ({ error: { errorMessage: 'Forbidden' } })))
          }
        },
        {
          provide: FeedbackService,
          useValue: { show: vi.fn() }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(AuditEventsPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Forbidden');
  });
});
