import { TestBed } from '@angular/core/testing';
import { HttpResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { PaymentComplementsApiService } from '../infrastructure/payment-complements-api.service';
import { PaymentComplementPdfDialogComponent } from './payment-complement-pdf-dialog.component';

describe('PaymentComplementPdfDialogComponent', () => {
  const pdf = new Blob(['%PDF-1.4 test'], { type: 'application/pdf' });
  let download: ReturnType<typeof vi.spyOn>;
  let open: ReturnType<typeof vi.spyOn>;
  let revoke: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value: vi.fn() });
    Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: vi.fn(() => 'blob:rep-pdf') });
    revoke = vi.fn();
    Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: revoke });
    Object.defineProperty(navigator, 'share', { configurable: true, value: undefined });
    Object.defineProperty(navigator, 'canShare', { configurable: true, value: undefined });
    download = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
    open = vi.spyOn(window, 'open').mockReturnValue(null);
  });
  afterEach(() => vi.restoreAllMocks());

  async function setup(status = 'Stamped', response = of(new HttpResponse({ body: pdf }))) {
    const api = { downloadPaymentComplementPdf: vi.fn(() => response) };
    await TestBed.configureTestingModule({ imports: [PaymentComplementPdfDialogComponent], providers: [{ provide: PaymentComplementsApiService, useValue: api }] }).compileComponents();
    const fixture = TestBed.createComponent(PaymentComplementPdfDialogComponent);
    fixture.componentRef.setInput('complementId', 57);
    fixture.componentRef.setInput('uuid', '5f9efa0c-8327-458c-98c9-c123765dfeba');
    fixture.componentRef.setInput('status', status);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, api, button: (text: string) => [...fixture.nativeElement.querySelectorAll('button')].find((b: any) => b.textContent.includes(text)) as HTMLButtonElement };
  }

  it('fetches the complement ID (57), not the payment ID (75), and downloads a named PDF', async () => {
    const { fixture, api, button } = await setup();
    expect(api.downloadPaymentComplementPdf).toHaveBeenCalledWith(57);
    button('Descargar PDF').click();
    expect(download).toHaveBeenCalledOnce();
    const anchor = download.mock.instances[0] as HTMLAnchorElement;
    expect(anchor.download).toBe('REP_5f9efa0c-8327-458c-98c9-c123765dfeba.pdf');
    expect(anchor.href).toBe('blob:rep-pdf');
    fixture.destroy();
    expect(revoke).toHaveBeenCalledWith('blob:rep-pdf');
  });

  it('shares the prepared PDF without fetching again or claiming delivery to a contact', async () => {
    const share = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'share', { configurable: true, value: share });
    Object.defineProperty(navigator, 'canShare', { configurable: true, value: () => true });
    const { fixture, api, button } = await setup();
    button('Compartir por WhatsApp').click();
    await fixture.whenStable(); fixture.detectChanges();
    expect(share).toHaveBeenCalledOnce();
    expect(share.mock.calls[0][0].files[0].type).toBe('application/pdf');
    expect(api.downloadPaymentComplementPdf).toHaveBeenCalledOnce();
    expect(open).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Confirma el envío');
  });

  it('downloads and opens WhatsApp with manual attachment guidance when native sharing is unavailable', async () => {
    const { fixture, button } = await setup();
    button('Compartir por WhatsApp').click();
    await fixture.whenStable(); fixture.detectChanges();
    expect(download).toHaveBeenCalledOnce();
    expect(open).toHaveBeenCalledWith(expect.stringContaining('https://wa.me/?text='), '_blank', 'noopener,noreferrer');
    expect(fixture.nativeElement.textContent).toContain('adjunta el archivo manualmente');
  });

  it('does not download or open WhatsApp after the user cancels the share sheet', async () => {
    Object.defineProperty(navigator, 'canShare', { configurable: true, value: () => true });
    Object.defineProperty(navigator, 'share', { configurable: true, value: vi.fn().mockRejectedValue(new DOMException('Cancelled', 'AbortError')) });
    const { fixture, button } = await setup();
    button('Compartir por WhatsApp').click();
    await fixture.whenStable();
    expect(download).not.toHaveBeenCalled(); expect(open).not.toHaveBeenCalled();
  });

  for (const status of ['Cancelled', 'CancellationRequested']) {
    it(`allows historical download without a share action for ${status}`, async () => {
      const { button } = await setup(status);
      expect(button('Descargar PDF')).toBeTruthy();
      expect(button('Compartir por WhatsApp')).toBeUndefined();
    });
  }

  it('shows the error without enabling download when generation fails', async () => {
    const { fixture, button } = await setup('Stamped', throwError(() => ({ error: { errorMessage: 'XML no disponible' } })));
    expect(fixture.nativeElement.textContent).toContain('XML no disponible');
    expect(button('Descargar PDF')).toBeUndefined();
    expect(button('Reintentar')).toBeTruthy();
  });
});
