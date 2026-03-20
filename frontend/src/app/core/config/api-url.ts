import { environment } from '../../../environments/environment';

export function buildApiUrl(path: string): string {
  const base = environment.apiBaseUrl.replace(/\/+$/, '');
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  return `${base}${normalizedPath}`;
}
