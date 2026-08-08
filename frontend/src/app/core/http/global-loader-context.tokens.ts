import { HttpContextToken } from '@angular/common/http';
import { GlobalLoaderOptions } from '../ui/global-loader.service';

export interface GlobalHttpLoaderOptions extends GlobalLoaderOptions {
  delayMs?: number;
  minimumVisibleMs?: number;
}

export const SKIP_GLOBAL_LOADER = new HttpContextToken<boolean>(() => false);

export const GLOBAL_LOADER_OPTIONS = new HttpContextToken<GlobalHttpLoaderOptions | null>(() => null);
