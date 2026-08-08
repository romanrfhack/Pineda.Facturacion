import { ComponentFixture, TestBed } from '@angular/core/testing';
import { GlobalLoaderService } from './global-loader.service';
import { PinedaLoaderOverlayComponent } from './pineda-loader-overlay.component';

describe('PinedaLoaderOverlayComponent', () => {
  let fixture: ComponentFixture<PinedaLoaderOverlayComponent>;
  let service: GlobalLoaderService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PinedaLoaderOverlayComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(PinedaLoaderOverlayComponent);
    service = TestBed.inject(GlobalLoaderService);
    fixture.detectChanges();
  });

  it('does not render the blocking overlay while no operation is active', () => {
    expect(fixture.nativeElement.querySelector('[data-testid="global-loader"]')).toBeNull();
  });

  it('renders the branded overlay with contextual operation text', () => {
    const handle = service.begin({
      message: 'Timbrando CFDI',
      detail: 'Esperando respuesta del proveedor.'
    });

    fixture.detectChanges();

    const overlay = fixture.nativeElement.querySelector('[data-testid="global-loader"]') as HTMLElement;
    expect(overlay).not.toBeNull();
    expect(overlay.getAttribute('aria-busy')).toBe('true');
    expect(overlay.textContent).toContain('Auto Refacciones Pineda');
    expect(overlay.textContent).toContain('Timbrando CFDI');
    expect(overlay.textContent).toContain('Esperando respuesta del proveedor.');

    handle.close();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="global-loader"]')).toBeNull();
  });
});
