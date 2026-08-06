import { TestBed } from '@angular/core/testing';
import { GlobalLoaderService } from './global-loader.service';

describe('GlobalLoaderService', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [GlobalLoaderService]
    });
  });

  it('stays active until every concurrent handle is closed', () => {
    const service = TestBed.inject(GlobalLoaderService);
    const first = service.begin({ message: 'Primera operación' });
    const second = service.begin({ message: 'Segunda operación' });

    expect(service.active()).toBe(true);
    expect(service.current().message).toBe('Segunda operación');

    second.close();

    expect(service.active()).toBe(true);
    expect(service.current().message).toBe('Primera operación');

    first.close();

    expect(service.active()).toBe(false);
  });

  it('treats closing the same handle more than once as an idempotent operation', () => {
    const service = TestBed.inject(GlobalLoaderService);
    const handle = service.begin();

    handle.close();
    handle.close();

    expect(service.active()).toBe(false);
  });

  it('normalizes empty custom text to the default loader content', () => {
    const service = TestBed.inject(GlobalLoaderService);
    const handle = service.begin({ eyebrow: ' ', message: '', detail: '   ' });

    expect(service.current()).toEqual({
      eyebrow: 'Auto Refacciones Pineda',
      message: 'Procesando información',
      detail: 'Espera un momento; estamos preparando todo para continuar.'
    });

    handle.close();
  });

  it('closes the loader after a tracked operation succeeds or fails', async () => {
    const service = TestBed.inject(GlobalLoaderService);

    await expect(service.track(async () => 42, { message: 'Operación exitosa' })).resolves.toBe(42);
    expect(service.active()).toBe(false);

    await expect(
      service.track(async () => {
        throw new Error('Fallo controlado');
      }, { message: 'Operación fallida' })
    ).rejects.toThrow('Fallo controlado');
    expect(service.active()).toBe(false);
  });
});
