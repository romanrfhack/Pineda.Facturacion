import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PermissionService } from '../../../core/auth/permission.service';

@Component({
  selector: 'app-catalogs-home-page',
  imports: [RouterLink],
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">Fiscal catalogs</p>
        <h2>Master data and import operations</h2>
        <p class="helper">Use these screens to manage issuer, receivers, product fiscal profiles, and import staging flows.</p>
      </header>

      <div class="grid">
        <a routerLink="/app/catalogs/issuer-profile" class="card">
          <h3>Issuer profile</h3>
          <p>Inspect the active issuer profile and update safe operational fields.</p>
        </a>

        <a routerLink="/app/catalogs/receivers" class="card">
          <h3>Fiscal receivers</h3>
          <p>Search, create, and update receiver fiscal master data.</p>
        </a>

        <a routerLink="/app/catalogs/product-fiscal-profiles" class="card">
          <h3>Product fiscal profiles</h3>
          <p>Maintain SAT mappings used during fiscal snapshot preparation.</p>
        </a>

        <a routerLink="/app/catalogs/imports/receivers" class="card">
          <h3>Receiver imports</h3>
          <p>Preview and apply receiver staging batches with row-level outcomes.</p>
        </a>

        <a routerLink="/app/catalogs/imports/products" class="card">
          <h3>Product imports</h3>
          <p>Preview and apply product fiscal profile batches, including enrichment gaps.</p>
        </a>
      </div>

      @if (!permissionService.canWriteMasterData()) {
        <p class="helper">Your current role has read-only access in the catalogs area. Write and apply actions stay disabled.</p>
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
