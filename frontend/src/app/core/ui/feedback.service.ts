import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';

export type FeedbackLevel = 'info' | 'success' | 'warning' | 'error';

export interface FeedbackMessage {
  id: number;
  level: FeedbackLevel;
  text: string;
  durationMs: number;
}

@Injectable({ providedIn: 'root' })
export class FeedbackService {
  private readonly destroyRef = inject(DestroyRef);
  private readonly toastStore = signal<FeedbackMessage[]>([]);
  private readonly timeouts = new Map<number, ReturnType<typeof setTimeout>>();
  private nextId = 0;

  readonly toasts = this.toastStore.asReadonly();
  readonly message = computed<FeedbackMessage | null>(() => this.toastStore()[0] ?? null);

  constructor() {
    this.destroyRef.onDestroy(() => this.clear());
  }

  show(level: FeedbackLevel, text: string): number {
    const normalizedText = text.trim();
    if (!normalizedText) {
      return -1;
    }

    const toast: FeedbackMessage = {
      id: ++this.nextId,
      level,
      text: normalizedText,
      durationMs: getDuration(level)
    };

    this.toastStore.update((current) => [toast, ...current]);
    this.scheduleAutoDismiss(toast);

    return toast.id;
  }

  clear(): void {
    for (const handle of this.timeouts.values()) {
      clearTimeout(handle);
    }

    this.timeouts.clear();
    this.toastStore.set([]);
  }

  dismiss(id: number): void {
    this.clearTimer(id);
    this.toastStore.update((current) => current.filter((toast) => toast.id !== id));
  }

  private scheduleAutoDismiss(toast: FeedbackMessage): void {
    const handle = setTimeout(() => {
      this.timeouts.delete(toast.id);
      this.toastStore.update((current) => current.filter((item) => item.id !== toast.id));
    }, toast.durationMs);

    this.timeouts.set(toast.id, handle);
  }

  private clearTimer(id: number): void {
    const handle = this.timeouts.get(id);
    if (handle) {
      clearTimeout(handle);
      this.timeouts.delete(id);
    }
  }
}

function getDuration(level: FeedbackLevel): number {
  switch (level) {
    case 'error':
    case 'warning':
      return 10_000;
    case 'info':
    case 'success':
    default:
      return 5_000;
  }
}
