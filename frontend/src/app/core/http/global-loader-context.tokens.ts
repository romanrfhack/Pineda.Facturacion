import { HttpContext, HttpContextToken } from '@angular/common/http';
import { GlobalLoaderOptions } from '../ui/global-loader.service';

export interface GlobalHttpLoaderOptions extends GlobalLoaderOptions {
  delayMs?: number;
  minimumVisibleMs?: number;
}

/**
 * Excluye explícitamente una petición que sería cubierta por el interceptor global.
 * Úsalo solo para polling, precargas, sincronizaciones silenciosas o trabajo en segundo plano.
 * Las operaciones iniciadas directamente por el usuario normalmente no deben usar este token.
 */
export const SKIP_GLOBAL_LOADER = new HttpContextToken<boolean>(() => false);

/**
 * Activa/configura el loader para una petición HTTP concreta.
 * Es el mecanismo recomendado para GET perceptibles por el usuario: búsquedas paginadas,
 * navegación a detalle, cargas de workspace o consultas cuyo resultado bloquea la siguiente acción.
 * No debe aplicarse a autocompletados o búsquedas disparadas por cada tecla.
 */
export const GLOBAL_LOADER_OPTIONS = new HttpContextToken<GlobalHttpLoaderOptions | null>(() => null);

/**
 * Helper estándar para hacer opt-in al loader global desde servicios API.
 * Mantén mensajes específicos de la operación y deja los tiempos por defecto salvo necesidad demostrable.
 * Ver docs/FRONTEND_GLOBAL_LOADER.md antes de agregar una nueva integración.
 */
export function createGlobalLoaderContext(options: GlobalHttpLoaderOptions): HttpContext {
  return new HttpContext().set(GLOBAL_LOADER_OPTIONS, options);
}
