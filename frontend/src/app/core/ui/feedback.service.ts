import { Injectable, signal } from '@angular/core';

export type FeedbackLevel = 'info' | 'success' | 'warning' | 'error';

export interface FeedbackMessage {
  level: FeedbackLevel;
  text: string;
}

@Injectable({ providedIn: 'root' })
export class FeedbackService {
  readonly message = signal<FeedbackMessage | null>(null);

  show(level: FeedbackLevel, text: string): void {
    this.message.set({ level, text });
  }

  clear(): void {
    this.message.set(null);
  }
}
