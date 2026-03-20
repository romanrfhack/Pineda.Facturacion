import { TestBed } from '@angular/core/testing';
import { FiscalStampEvidenceCardComponent } from './fiscal-stamp-evidence-card.component';

describe('FiscalStampEvidenceCardComponent', () => {
  it('renders safe evidence summary and emits actions', async () => {
    await TestBed.configureTestingModule({
      imports: [FiscalStampEvidenceCardComponent]
    }).compileComponents();

    const fixture = TestBed.createComponent(FiscalStampEvidenceCardComponent);
    fixture.componentRef.setInput('stamp', {
      id: 11,
      fiscalDocumentId: 40,
      providerName: 'FacturaloPlus',
      providerOperation: 'stamp',
      providerTrackingId: 'TRACK-1',
      status: 'Stamped',
      uuid: 'UUID-123',
      stampedAtUtc: '2026-03-20T12:00:00Z',
      providerCode: '200',
      providerMessage: 'Stamped',
      errorCode: null,
      errorMessage: null,
      xmlHash: 'HASH-1',
      qrCodeTextOrUrl: 'https://sat.example/qr',
      originalString: '||1.1|UUID-123||',
      createdAtUtc: '2026-03-20T12:00:00Z',
      updatedAtUtc: '2026-03-20T12:00:00Z'
    });
    fixture.detectChanges();

    const detailSpy = vi.fn();
    const xmlSpy = vi.fn();
    fixture.componentInstance.detailsRequested.subscribe(detailSpy);
    fixture.componentInstance.xmlRequested.subscribe(xmlSpy);

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('UUID-123');
    expect(text).toContain('FacturaloPlus');
    expect(text).not.toContain('xmlContent');

    fixture.nativeElement.querySelectorAll('button')[0].click();
    fixture.nativeElement.querySelectorAll('button')[1].click();

    expect(detailSpy).toHaveBeenCalledTimes(1);
    expect(xmlSpy).toHaveBeenCalledTimes(1);
  });
});
