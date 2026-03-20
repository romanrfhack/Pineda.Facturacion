import { Routes } from '@angular/router';

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
  }
];
