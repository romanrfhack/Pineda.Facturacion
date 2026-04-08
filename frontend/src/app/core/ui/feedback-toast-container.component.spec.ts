import { TestBed } from '@angular/core/testing';
import { FeedbackToastContainerComponent } from './feedback-toast-container.component';
import { FeedbackService } from './feedback.service';

describe('FeedbackToastContainerComponent', () => {
  beforeEach(async () => {
    vi.useFakeTimers();

    await TestBed.configureTestingModule({
      imports: [FeedbackToastContainerComponent],
      providers: [FeedbackService]
    }).compileComponents();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders toasts in a fixed top-right stack with the newest toast first', () => {
    const fixture = TestBed.createComponent(FeedbackToastContainerComponent);
    const feedbackService = TestBed.inject(FeedbackService);

    feedbackService.show('success', 'Guardado correctamente.');
    feedbackService.show('error', 'No se pudo guardar.');
    fixture.detectChanges();

    const region = fixture.nativeElement.querySelector('.toast-region') as HTMLElement;
    const cards = Array.from(fixture.nativeElement.querySelectorAll('.toast-card')) as HTMLElement[];

    expect(region).not.toBeNull();
    expect(getComputedStyle(region).position).toBe('fixed');
    expect(getComputedStyle(region).top).toBe('1rem');
    expect(getComputedStyle(region).right).toBe('1rem');
    expect(cards).toHaveLength(2);
    expect(fixture.nativeElement.querySelectorAll('.sr-only')).toHaveLength(0);
    expect(cards[0].textContent).toContain('No se pudo guardar.');
    expect(cards[0].getAttribute('role')).toBe('alert');
    expect(cards[1].textContent).toContain('Guardado correctamente.');
    expect(cards[1].getAttribute('role')).toBe('status');
  });

  it('closes a toast immediately when clicking the close button', async () => {
    const fixture = TestBed.createComponent(FeedbackToastContainerComponent);
    const feedbackService = TestBed.inject(FeedbackService);

    feedbackService.show('error', 'Error visible.');
    fixture.detectChanges();

    const closeButton = fixture.nativeElement.querySelector('.toast-close') as HTMLButtonElement;
    closeButton.click();
    fixture.detectChanges();

    await vi.advanceTimersByTimeAsync(200);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.toast-card')).toBeNull();
    expect(feedbackService.toasts()).toHaveLength(0);
  });
});
