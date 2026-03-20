import { TestBed } from '@angular/core/testing';
import { PaymentComplementStampCardComponent } from './payment-complement-stamp-card.component';

describe('PaymentComplementStampCardComponent', () => {
  it('renders safe payment-complement evidence summary', async () => {
    await TestBed.configureTestingModule({
      imports: [PaymentComplementStampCardComponent]
    }).compileComponents();

    const fixture = TestBed.createComponent(PaymentComplementStampCardComponent);
    fixture.componentRef.setInput('stamp', {
      id: 22,
      paymentComplementDocumentId: 60,
      providerName: 'FacturaloPlus',
      providerOperation: 'payment-complement-stamp',
      providerTrackingId: 'TRACK-PC-1',
      status: 'Stamped',
      uuid: 'UUID-PC-1',
      stampedAtUtc: '2026-03-20T12:00:00Z',
      providerCode: '200',
      providerMessage: 'Stamped',
      errorCode: null,
      errorMessage: null,
      xmlHash: 'HASH-PC-1',
      qrCodeTextOrUrl: 'https://sat.example/pc-qr',
      originalString: '||1.1|UUID-PC-1||',
      createdAtUtc: '2026-03-20T12:00:00Z',
      updatedAtUtc: '2026-03-20T12:00:00Z'
    });
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('UUID-PC-1');
    expect(text).toContain('FacturaloPlus');
    expect(text).toContain('HASH-PC-1');
    expect(text).not.toContain('xmlContent');
  });
});
