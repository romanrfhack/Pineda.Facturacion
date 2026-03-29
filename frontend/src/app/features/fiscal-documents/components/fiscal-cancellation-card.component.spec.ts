import { TestBed } from '@angular/core/testing';
import { FiscalCancellationCardComponent } from './fiscal-cancellation-card.component';

describe('FiscalCancellationCardComponent', () => {
  async function configure(overrides?: Partial<{
    status: string;
    cancellationReasonCode: string;
    replacementUuid: string | null;
    providerName: string;
    providerTrackingId: string | null;
    providerCode: string | null;
    providerMessage: string | null;
    errorCode: string | null;
    errorMessage: string | null;
    supportMessage: string | null;
    rawResponseSummaryJson: string | null;
    requestedAtUtc: string;
    cancelledAtUtc: string | null;
  }>) {
    await TestBed.configureTestingModule({
      imports: [FiscalCancellationCardComponent]
    }).compileComponents();

    const fixture = TestBed.createComponent(FiscalCancellationCardComponent);
    fixture.componentRef.setInput('cancellation', {
      fiscalDocumentId: 40,
      status: 'Rejected',
      cancellationReasonCode: '03',
      replacementUuid: null,
      providerName: 'FacturaloPlus',
      providerTrackingId: null,
      providerCode: '203',
      providerMessage: 'Solicitud rechazada',
      errorCode: '203',
      errorMessage: 'Provider rejected the cancellation request.',
      supportMessage: 'ProviderCode=203 | ProviderMessage=Solicitud rechazada',
      rawResponseSummaryJson: '{"httpStatusCode":400}',
      requestedAtUtc: '2026-03-29T12:00:00Z',
      cancelledAtUtc: null,
      ...overrides
    });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('does not show contradictory pending-cancelled text for rejected cancellations', async () => {
    const fixture = await configure();

    expect(fixture.nativeElement.textContent).toContain('Cancelación rechazada');
    expect(fixture.nativeElement.textContent).toContain('Resultado');
    expect(fixture.nativeElement.textContent).toContain('Rechazado por el PAC');
    expect(fixture.nativeElement.textContent).not.toContain('CanceladoPendiente');
  });

  it('shows support diagnostics and raw summary when available', async () => {
    const fixture = await configure();

    expect(fixture.nativeElement.textContent).toContain('Código PAC');
    expect(fixture.nativeElement.textContent).toContain('203');
    expect(fixture.nativeElement.textContent).toContain('Solicitud rechazada');
    expect(fixture.nativeElement.textContent).toContain('Resumen técnico PAC');
  });
});
