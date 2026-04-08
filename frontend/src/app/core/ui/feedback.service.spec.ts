import { TestBed } from '@angular/core/testing';
import { FeedbackService } from './feedback.service';

describe('FeedbackService', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({
      providers: [FeedbackService]
    });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('auto dismisses success messages after 5 seconds', async () => {
    const service = TestBed.inject(FeedbackService);

    service.show('success', 'Guardado correctamente.');

    expect(service.toasts()).toHaveLength(1);

    await vi.advanceTimersByTimeAsync(4_999);
    expect(service.toasts()).toHaveLength(1);

    await vi.advanceTimersByTimeAsync(1);
    expect(service.toasts()).toHaveLength(0);
  });

  it('auto dismisses error messages after 10 seconds', async () => {
    const service = TestBed.inject(FeedbackService);

    service.show('error', 'No se pudo guardar.');

    expect(service.toasts()).toHaveLength(1);

    await vi.advanceTimersByTimeAsync(9_999);
    expect(service.toasts()).toHaveLength(1);

    await vi.advanceTimersByTimeAsync(1);
    expect(service.toasts()).toHaveLength(0);
  });

  it('stacks the newest toast first and removes it immediately when dismissed manually', async () => {
    const service = TestBed.inject(FeedbackService);

    const firstId = service.show('info', 'Primer mensaje.');
    const secondId = service.show('success', 'Segundo mensaje.');

    expect(service.toasts().map((toast) => toast.id)).toEqual([secondId, firstId]);
    expect(service.message()?.id).toBe(secondId);

    service.dismiss(secondId);

    expect(service.toasts().map((toast) => toast.id)).toEqual([firstId]);

    await vi.advanceTimersByTimeAsync(10_000);
    expect(service.toasts()).toHaveLength(0);
  });
});
