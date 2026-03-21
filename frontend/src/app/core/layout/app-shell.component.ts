import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { SessionService } from '../auth/session.service';
import { PermissionService } from '../auth/permission.service';
import { NAVIGATION_ITEMS } from './navigation.config';
import { FeedbackBannerComponent } from '../ui/feedback-banner.component';
import { getRoleDisplayLabel } from '../../shared/ui/display-labels';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FeedbackBannerComponent],
  template: `
    <div class="shell">
      <aside class="sidebar">
        <div class="brand">
          <p class="eyebrow">Pineda Facturacion</p>
          <h1>Consola operativa</h1>
        </div>

        <nav>
          @for (item of navigation(); track item.route) {
            <a [routerLink]="item.route" routerLinkActive="active">{{ item.label }}</a>
          }
        </nav>

        <div class="user-card">
          <p class="user-name">{{ sessionService.currentUser().displayName || sessionService.currentUser().username }}</p>
          <p class="user-meta">{{ roleLabels() || 'Sin roles' }}</p>
          <button type="button" (click)="logout()">Cerrar sesión</button>
        </div>
      </aside>

      <main class="content">
        <app-feedback-banner />
        <router-outlet />
      </main>
    </div>
  `,
  styles: [`
    :host { display:block; min-height:100vh; }
    .shell { min-height:100vh; display:grid; grid-template-columns:280px 1fr; background:linear-gradient(180deg, #f4f0e8 0%, #fffdf8 100%); color:#1f1f1f; }
    .sidebar { padding:1.5rem; background:#1d2a39; color:#f7f2e8; display:flex; flex-direction:column; gap:1.5rem; }
    .brand h1 { margin:0.25rem 0 0; font-size:1.5rem; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#d4c29c; }
    nav { display:grid; gap:0.5rem; }
    nav a { color:inherit; text-decoration:none; padding:0.75rem 0.9rem; border-radius:0.75rem; background:rgba(255,255,255,0.04); }
    nav a.active { background:#d4c29c; color:#1d2a39; font-weight:600; }
    .user-card { margin-top:auto; padding:1rem; border:1px solid rgba(255,255,255,0.12); border-radius:1rem; background:rgba(255,255,255,0.04); }
    .user-name, .user-meta { margin:0 0 0.4rem; }
    .user-meta { color:#c4cfda; font-size:0.9rem; }
    button { margin-top:0.5rem; width:100%; padding:0.7rem 0.9rem; border:none; border-radius:0.75rem; background:#f7f2e8; color:#1d2a39; cursor:pointer; font:inherit; }
    .content { padding:1.5rem; }
    @media (max-width: 960px) {
      .shell { grid-template-columns:1fr; }
      .sidebar { position:sticky; top:0; z-index:10; }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppShellComponent {
  protected readonly sessionService = inject(SessionService);
  private readonly permissionService = inject(PermissionService);
  protected readonly navigation = computed(() =>
    NAVIGATION_ITEMS.filter((item) => this.permissionService.hasAnyRole(item.roles))
  );
  protected readonly roleLabels = computed(() => this.sessionService.roles().map((role) => getRoleDisplayLabel(role)).join(', '));

  protected logout(): void {
    void this.sessionService.logout();
  }
}
