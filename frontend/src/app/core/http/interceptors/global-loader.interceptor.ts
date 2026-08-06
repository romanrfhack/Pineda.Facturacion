import { HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { finalize } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  GlobalLoaderHandle,
  GlobalLoaderOptions,
  GlobalLoaderService
} from '../../ui/global-loader.service';
import {
  GLOBAL_LOADER_OPTIONS,
  GlobalHttpLoaderOptions,
  SKIP_GLOBAL_LOADER
} from '../global-loader-context.tokens';

const DEFAULT_DELAY_MS = 250;
const DEFAULT_MINIMUM_VISIBLE_MS = 320;

export const globalLoaderInterceptor: HttpInterceptorFn = (request, next) => {
  if (!isBackendRequest(request.url) || request.context.get(SKIP_GLOBAL_LOADER)) {
    return next(request);
  }

  const loader = inject(GlobalLoaderService);
  const configuredOptions = request.context.get(GLOBAL_LOADER_OPTIONS);
  const loaderOptions = configuredOptions ?? resolveDefaultLoaderOptions(request);
  const delayMs = normalizeDuration(configuredOptions?.delayMs, DEFAULT_DELAY_MS);
  const minimumVisibleMs = normalizeDuration(
    configuredOptions?.minimumVisibleMs,
    DEFAULT_MINIMUM_VISIBLE_MS
  );

  let completed = false;
  let handle: GlobalLoaderHandle | null = null;
  let shownAt = 0;

  const showTimer = setTimeout(() => {
    if (completed) {
      return;
    }

    shownAt = Date.now();
    handle = loader.begin(loaderOptions);
  }, delayMs);

  return next(request).pipe(
    finalize(() => {
      completed = true;
      clearTimeout(showTimer);

      const activeHandle = handle;
      handle = null;

      if (!activeHandle) {
        return;
      }

      const elapsedMs = Date.now() - shownAt;
      const remainingMs = Math.max(0, minimumVisibleMs - elapsedMs);

      if (remainingMs === 0) {
        activeHandle.close();
        return;
      }

      setTimeout(() => activeHandle.close(), remainingMs);
    })
  );
};

function isBackendRequest(url: string): boolean {
  const baseUrl = environment.apiBaseUrl.replace(/\/+$/, '');
  if (!baseUrl) {
    return false;
  }

  return url === baseUrl || url.startsWith(`${baseUrl}/`);
}

function resolveDefaultLoaderOptions(request: HttpRequest<unknown>): GlobalLoaderOptions {
  const url = request.url.toLowerCase();

  if (containsAny(url, ['/stamp', '/stamping', '/timbr'])) {
    return {
      message: 'Timbrando CFDI',
      detail: 'Validamos la información y esperamos la respuesta del proveedor de certificación.'
    };
  }

  if (containsAny(url, ['/cancel', '/cancellation', '/cancelacion'])) {
    return {
      message: 'Procesando cancelación',
      detail: 'Enviamos la solicitud y actualizamos el estado fiscal del comprobante.'
    };
  }

  if (url.includes('/pdf')) {
    return {
      message: 'Generando PDF',
      detail: 'Estamos preparando el documento para su visualización o descarga.'
    };
  }

  if (url.includes('/xml')) {
    return {
      message: 'Preparando XML',
      detail: 'Estamos recuperando el comprobante fiscal solicitado.'
    };
  }

  if (containsAny(url, ['/report', '/summary'])) {
    return {
      message: 'Generando reporte',
      detail: 'Procesamos la información y preparamos el resultado solicitado.'
    };
  }

  if (containsAny(url, ['/import', '/sync', '/refresh'])) {
    return {
      message: 'Sincronizando información',
      detail: 'Estamos actualizando los datos necesarios para continuar.'
    };
  }

  switch (request.method.toUpperCase()) {
    case 'GET':
      return {
        message: 'Consultando información',
        detail: 'Estamos recuperando y organizando los datos solicitados.'
      };
    case 'POST':
      return {
        message: 'Procesando operación',
        detail: 'Validamos la información y aplicamos los cambios solicitados.'
      };
    case 'PUT':
    case 'PATCH':
      return {
        message: 'Guardando cambios',
        detail: 'Estamos actualizando la información del sistema.'
      };
    case 'DELETE':
      return {
        message: 'Eliminando información',
        detail: 'Validamos la operación antes de aplicar el cambio.'
      };
    default:
      return {
        message: 'Procesando información',
        detail: 'Espera un momento; estamos preparando todo para continuar.'
      };
  }
}

function containsAny(value: string, fragments: readonly string[]): boolean {
  return fragments.some((fragment) => value.includes(fragment));
}

function normalizeDuration(value: number | undefined, fallback: number): number {
  return Number.isFinite(value) ? Math.max(0, value ?? fallback) : fallback;
}
