import { Injectable, computed, signal } from '@angular/core';

export interface GlobalLoaderOptions {
  eyebrow?: string;
  message?: string;
  detail?: string;
}

export interface GlobalLoaderSnapshot {
  eyebrow: string;
  message: string;
  detail: string;
}

export interface GlobalLoaderHandle {
  readonly id: number;
  close(): void;
}

interface GlobalLoaderRequest extends GlobalLoaderSnapshot {
  id: number;
}

const DEFAULT_LOADER_CONTENT: GlobalLoaderSnapshot = {
  eyebrow: 'Auto Refacciones Pineda',
  message: 'Procesando información',
  detail: 'Espera un momento; estamos preparando todo para continuar.'
};

@Injectable({ providedIn: 'root' })
export class GlobalLoaderService {
  private readonly requestStore = signal<GlobalLoaderRequest[]>([]);
  private nextId = 0;

  readonly active = computed(() => this.requestStore().length > 0);
  readonly current = computed<GlobalLoaderSnapshot>(() => {
    const requests = this.requestStore();
    if (!requests.length) {
      return DEFAULT_LOADER_CONTENT;
    }

    const { eyebrow, message, detail } = requests[requests.length - 1];
    return { eyebrow, message, detail };
  });

  begin(options: GlobalLoaderOptions = {}): GlobalLoaderHandle {
    const request: GlobalLoaderRequest = {
      id: ++this.nextId,
      eyebrow: normalizeText(options.eyebrow, DEFAULT_LOADER_CONTENT.eyebrow),
      message: normalizeText(options.message, DEFAULT_LOADER_CONTENT.message),
      detail: normalizeText(options.detail, DEFAULT_LOADER_CONTENT.detail)
    };

    this.requestStore.update((current) => [...current, request]);

    let closed = false;
    return {
      id: request.id,
      close: () => {
        if (closed) {
          return;
        }

        closed = true;
        this.end(request.id);
      }
    };
  }

  end(id: number): void {
    this.requestStore.update((current) => current.filter((request) => request.id !== id));
  }

  clear(): void {
    this.requestStore.set([]);
  }

  async track<T>(operation: () => Promise<T>, options: GlobalLoaderOptions = {}): Promise<T> {
    const handle = this.begin(options);

    try {
      return await operation();
    } finally {
      handle.close();
    }
  }
}

function normalizeText(value: string | undefined, fallback: string): string {
  const normalized = value?.trim();
  return normalized || fallback;
}
