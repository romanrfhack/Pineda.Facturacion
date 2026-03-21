import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PermissionService } from '../../../core/auth/permission.service';

@Component({
  selector: 'app-catalogs-home-page',
  imports: [RouterLink],
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">Catálogos fiscales</p>
        <h2>Datos maestros y operaciones de importación</h2>
        <p class="helper">Usa estas pantallas para administrar emisor, receptores, perfiles fiscales de producto y flujos de staging de importación.</p>
      </header>

      <div class="grid">
        <a routerLink="/app/catalogs/issuer-profile" class="card">
          <h3>Perfil del emisor</h3>
          <p>Consulta el perfil activo del emisor y actualiza campos operativos seguros.</p>
        </a>

        <a routerLink="/app/catalogs/receivers" class="card">
          <h3>Receptores fiscales</h3>
          <p>Busca, crea y actualiza datos maestros fiscales de receptores.</p>
        </a>

        <a routerLink="/app/catalogs/product-fiscal-profiles" class="card">
          <h3>Perfiles fiscales de producto</h3>
          <p>Mantén los mapeos SAT usados durante la preparación del snapshot fiscal.</p>
        </a>

        <a routerLink="/app/catalogs/imports/receivers" class="card">
          <h3>Importaciones de receptores</h3>
          <p>Consulta la vista previa y aplica lotes de staging de receptores con resultados por fila.</p>
        </a>

        <a routerLink="/app/catalogs/imports/products" class="card">
          <h3>Importaciones de productos</h3>
          <p>Consulta la vista previa y aplica lotes de perfiles fiscales de producto, incluyendo faltantes de enriquecimiento.</p>
        </a>
      </div>

      @if (!permissionService.canWriteMasterData()) {
        <p class="helper">Tu rol actual tiene acceso de solo lectura en el área de catálogos. Las acciones de escritura y aplicación permanecen deshabilitadas.</p>
      }
    </section>
  `,
  styles: [`
    .page { display:grid; gap:1rem; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    h2 { margin:0.3rem 0 0; }
    .helper { margin:0; color:#5f6b76; }
    .grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:1rem; }
    .card { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; color:inherit; text-decoration:none; display:grid; gap:0.5rem; }
    .card h3, .card p { margin:0; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CatalogsHomePageComponent {
  protected readonly permissionService = inject(PermissionService);
}
