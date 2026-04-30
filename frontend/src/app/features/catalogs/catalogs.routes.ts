import { Routes } from '@angular/router';
import { AppRole } from '../../core/auth/models';
import { roleGuard } from '../../core/auth/role.guard';

export const CATALOG_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/catalogs-home-page.component').then((m) => m.CatalogsHomePageComponent)
  },
  {
    path: 'issuer-profile',
    loadComponent: () => import('./pages/issuer-profile-page.component').then((m) => m.IssuerProfilePageComponent)
  },
  {
    path: 'receivers',
    loadComponent: () => import('./pages/fiscal-receivers-page.component').then((m) => m.FiscalReceiversPageComponent)
  },
  {
    path: 'product-fiscal-profiles',
    loadComponent: () => import('./pages/product-fiscal-profiles-page.component').then((m) => m.ProductFiscalProfilesPageComponent)
  },
  {
    path: 'imports/receivers',
    loadComponent: () => import('./pages/receiver-imports-page.component').then((m) => m.ReceiverImportsPageComponent)
  },
  {
    path: 'imports/products',
    loadComponent: () => import('./pages/product-imports-page.component').then((m) => m.ProductImportsPageComponent)
  },
  {
    path: 'imports/products/legacy-mappings',
    canMatch: [roleGuard([AppRole.Admin, AppRole.FiscalSupervisor])],
    loadComponent: () => import('./pages/legacy-product-mapping-imports-page.component').then((m) => m.LegacyProductMappingImportsPageComponent)
  },
  {
    path: 'imports/sat',
    loadComponent: () => import('./pages/sat-catalog-import-page.component').then((m) => m.SatCatalogImportPageComponent)
  }
];
