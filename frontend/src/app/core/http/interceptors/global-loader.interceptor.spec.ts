import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { GlobalLoaderService } from '../../ui/global-loader.service';
import {
  GLOBAL_LOADER_OPTIONS,
  SKIP_GLOBAL_LOADER
} from '../global-loader-context.tokens';
import { globalLoaderInterceptor } from './global-loader.interceptor';

describe('globalLoaderInterceptor', () => {
  let httpClient: HttpClient;
  let httpTesting: HttpTestingController;
  let loader: GlobalLoaderService;

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([globalLoaderInterceptor])),
        provideHttpClientTesting()
      ]
    });

    httpClient = TestBed.inject(HttpClient);
    httpTesting = TestBed.inject(HttpTestingController);
    loader = TestBed.inject(GlobalLoaderService);
  });

  afterEach(() => {
    httpTesting.verify();
    loader.clear();
    vi.useRealTimers();
  });

  it('does not flash the loader for backend requests that finish before the delay', async () => {
    httpClient.get('/api/orders').subscribe();
    httpTesting.expectOne('/api/orders').flush([]);

    await vi.advanceTimersByTimeAsync(1_000);

    expect(loader.active()).toBe(false);
  });

  it('shows a contextual loader for a slow request and respects the minimum visible time', async () => {
    httpClient.get('/api/orders').subscribe();
    const request = httpTesting.expectOne('/api/orders');

    await vi.advanceTimersByTimeAsync(249);
    expect(loader.active()).toBe(false);

    await vi.advanceTimersByTimeAsync(1);
    expect(loader.active()).toBe(true);
    expect(loader.current().message).toBe('Consultando información');

    request.flush([]);

    await vi.advanceTimersByTimeAsync(319);
    expect(loader.active()).toBe(true);

    await vi.advanceTimersByTimeAsync(1);
    expect(loader.active()).toBe(false);
  });

  it('uses operation-specific text for stamping endpoints', async () => {
    httpClient.post('/api/fiscal-documents/42/stamp', {}).subscribe();
    const request = httpTesting.expectOne('/api/fiscal-documents/42/stamp');

    await vi.advanceTimersByTimeAsync(250);

    expect(loader.current()).toEqual({
      eyebrow: 'Auto Refacciones Pineda',
      message: 'Timbrando CFDI',
      detail: 'Validamos la información y esperamos la respuesta del proveedor de certificación.'
    });

    request.flush({});
    await vi.advanceTimersByTimeAsync(320);
  });

  it('allows a request to opt out of the global loader', async () => {
    const context = new HttpContext().set(SKIP_GLOBAL_LOADER, true);

    httpClient.get('/api/background-refresh', { context }).subscribe();
    const request = httpTesting.expectOne('/api/background-refresh');

    await vi.advanceTimersByTimeAsync(1_000);
    expect(loader.active()).toBe(false);

    request.flush({});
  });

  it('supports custom text and timing through HttpContext', async () => {
    const context = new HttpContext().set(GLOBAL_LOADER_OPTIONS, {
      message: 'Procesando venta',
      detail: 'Estamos registrando el movimiento y actualizando el inventario.',
      delayMs: 0,
      minimumVisibleMs: 0
    });

    httpClient.post('/api/sales', {}, { context }).subscribe();
    const request = httpTesting.expectOne('/api/sales');

    await vi.advanceTimersByTimeAsync(0);

    expect(loader.active()).toBe(true);
    expect(loader.current().message).toBe('Procesando venta');

    request.flush({});

    expect(loader.active()).toBe(false);
  });

  it('does not intercept non-backend HttpClient requests', async () => {
    httpClient.get('/assets/catalog.json').subscribe();
    const request = httpTesting.expectOne('/assets/catalog.json');

    await vi.advanceTimersByTimeAsync(1_000);
    expect(loader.active()).toBe(false);

    request.flush({});
  });
});
