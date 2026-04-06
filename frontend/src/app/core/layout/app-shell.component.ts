import { ChangeDetectionStrategy, Component, HostListener, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs';
import { SessionService } from '../auth/session.service';
import { PermissionService } from '../auth/permission.service';
import { NAVIGATION_ITEMS } from './navigation.config';
import { FeedbackBannerComponent } from '../ui/feedback-banner.component';
import { getRoleDisplayLabel } from '../../shared/ui/display-labels';

const MOBILE_BREAKPOINT = 768;
const SIDEBAR_COLLAPSED_STORAGE_KEY = 'app-shell-sidebar-collapsed';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FeedbackBannerComponent],
  template: `
    <div class="shell" [class.mobile]="isMobile()" [class.desktop-collapsed]="isDesktopCollapsed()">
      @if (isMobile() && isMobileMenuOpen()) {
        <button class="backdrop" type="button" (click)="closeMobileMenu()" aria-label="Cerrar menú lateral"></button>
      }

      <aside class="sidebar" [class.mobile-open]="isMobileMenuOpen()" [attr.aria-hidden]="isMobile() && !isMobileMenuOpen()">
        <div class="sidebar-header">
          <div class="brand">
            <p class="eyebrow">Pineda Facturacion</p>
            <h1>{{ isDesktopCollapsed() ? 'PF' : 'Consola operativa' }}</h1>
          </div>
          @if (!isMobile()) {
            <button
              type="button"
              class="icon-button sidebar-toggle"
              (click)="toggleDesktopSidebar()"
              [attr.aria-label]="isDesktopCollapsed() ? 'Expandir menú lateral' : 'Colapsar menú lateral'">
              <span aria-hidden="true">{{ isDesktopCollapsed() ? '>>' : '<<' }}</span>
            </button>
          }
        </div>

        <nav>
          @for (item of navigation(); track item.route) {
            <a
              [routerLink]="item.route"
              routerLinkActive="active"
              [routerLinkActiveOptions]="{ exact: false }"
              (click)="handleNavigationSelection()"
              [attr.title]="isDesktopCollapsed() ? item.label : null"
              [attr.aria-label]="item.label">
              <span class="nav-icon" aria-hidden="true">{{ item.iconText }}</span>
              @if (!isDesktopCollapsed()) {
                <span class="nav-label">{{ item.label }}</span>
              } @else {
                <span class="nav-tooltip">{{ item.label }}</span>
              }
            </a>
          }
        </nav>

        <div class="user-card">
          @if (!isDesktopCollapsed()) {
            <p class="user-name">{{ sessionService.currentUser().displayName || sessionService.currentUser().username }}</p>
            <p class="user-meta">{{ roleLabels() || 'Sin roles' }}</p>
          } @else {
            <p class="user-name compact">{{ compactUserName() }}</p>
          }
          <button type="button" (click)="logout()">Cerrar sesión</button>
        </div>
      </aside>

      <main class="content">
        @if (isMobile()) {
          <header class="topbar mobile-topbar">
            <div class="topbar-actions">
              <button
                type="button"
                class="icon-button"
                (click)="toggleMobileMenu()"
                aria-label="Abrir menú de navegación">
                <span aria-hidden="true">|||</span>
              </button>
              <div class="topbar-copy">
                <p class="eyebrow">Pineda Facturacion</p>
                <strong>Consola operativa</strong>
              </div>
            </div>
          </header>
        }
        <app-feedback-banner />
        <router-outlet />
      </main>
    </div>
  `,
  styles: [`
    :host { display:block; min-height:100vh; }
    .shell { min-height:100vh; display:grid; grid-template-columns:280px 1fr; background:linear-gradient(180deg, #f4f0e8 0%, #fffdf8 100%); color:#1f1f1f; position:relative; }
    .shell.desktop-collapsed { grid-template-columns:88px 1fr; }
    .sidebar { padding:1.5rem; background:#1d2a39; color:#f7f2e8; display:flex; flex-direction:column; gap:1.5rem; transition:transform 160ms ease, width 160ms ease, padding 160ms ease; z-index:20; }
    .sidebar-header { display:flex; align-items:flex-start; justify-content:space-between; gap:0.75rem; }
    .brand h1 { margin:0.25rem 0 0; font-size:1.5rem; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#d4c29c; }
    nav { display:grid; gap:0.5rem; }
    nav a { color:inherit; text-decoration:none; padding:0.75rem 0.9rem; border-radius:0.75rem; background:rgba(255,255,255,0.04); display:flex; align-items:center; gap:0.85rem; position:relative; }
    nav a.active { background:#d4c29c; color:#1d2a39; font-weight:600; }
    .nav-icon { width:2rem; height:2rem; border-radius:0.7rem; display:inline-flex; align-items:center; justify-content:center; background:rgba(255,255,255,0.1); font-size:0.72rem; font-weight:700; letter-spacing:0.08em; flex:0 0 auto; }
    nav a.active .nav-icon { background:rgba(29, 42, 57, 0.12); }
    .nav-label { white-space:nowrap; }
    .nav-tooltip { position:absolute; left:calc(100% + 0.75rem); top:50%; transform:translateY(-50%); background:#fffdf8; color:#1d2a39; padding:0.45rem 0.65rem; border-radius:0.6rem; border:1px solid #d8d1c2; box-shadow:0 10px 24px rgba(0,0,0,0.12); opacity:0; pointer-events:none; white-space:nowrap; transition:opacity 120ms ease; }
    .shell.desktop-collapsed nav a:hover .nav-tooltip,
    .shell.desktop-collapsed nav a:focus-visible .nav-tooltip { opacity:1; }
    .user-card { margin-top:auto; padding:1rem; border:1px solid rgba(255,255,255,0.12); border-radius:1rem; background:rgba(255,255,255,0.04); }
    .user-name, .user-meta { margin:0 0 0.4rem; }
    .user-name.compact { text-align:center; font-size:0.82rem; letter-spacing:0.08em; }
    .user-meta { color:#c4cfda; font-size:0.9rem; }
    button { margin-top:0.5rem; width:100%; padding:0.7rem 0.9rem; border:none; border-radius:0.75rem; background:#f7f2e8; color:#1d2a39; cursor:pointer; font:inherit; }
    .content { padding:1.5rem; min-width:0; }
    .topbar { display:flex; align-items:center; justify-content:space-between; gap:1rem; margin-bottom:1rem; padding:0.75rem 1rem; border:1px solid #e8decd; border-radius:1rem; background:rgba(255,253,248,0.84); backdrop-filter:blur(10px); }
    .mobile-topbar { position:sticky; top:1rem; z-index:5; }
    .topbar-actions { display:flex; align-items:center; gap:0.85rem; min-width:0; }
    .topbar-copy { display:grid; gap:0.1rem; }
    .topbar-copy strong { color:#1d2a39; }
    .icon-button { margin:0; width:2.8rem; min-width:2.8rem; padding:0; aspect-ratio:1; display:inline-flex; align-items:center; justify-content:center; background:#1d2a39; color:#f7f2e8; }
    .sidebar-toggle { flex:0 0 auto; background:rgba(255,255,255,0.12); }
    .backdrop { position:fixed; inset:0; background:rgba(10, 16, 24, 0.45); border:none; padding:0; margin:0; border-radius:0; z-index:15; }
    .shell.desktop-collapsed .sidebar { padding-inline:1rem; }
    .shell.desktop-collapsed .brand .eyebrow,
    .shell.desktop-collapsed .user-meta { display:none; }
    .shell.desktop-collapsed nav a { justify-content:center; padding-inline:0.75rem; }
    .shell.desktop-collapsed .user-card { padding:0.85rem 0.6rem; }
    .shell.desktop-collapsed .user-card button { padding-inline:0.5rem; font-size:0.82rem; }
    @media (max-width: 767px) {
      .shell,
      .shell.desktop-collapsed { grid-template-columns:1fr; }
      .sidebar { position:fixed; top:0; left:0; bottom:0; width:min(84vw, 320px); max-width:320px; transform:translateX(-100%); box-shadow:0 20px 40px rgba(0,0,0,0.24); }
      .sidebar.mobile-open { transform:translateX(0); }
      .content { padding:1rem; }
    }
    @media (min-width: 768px) {
      .backdrop { display:none; }
      .sidebar { position:sticky; top:0; min-height:100vh; }
      .topbar { display:none; }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppShellComponent {
  protected readonly sessionService = inject(SessionService);
  private readonly permissionService = inject(PermissionService);
  private readonly router = inject(Router);
  protected readonly isMobile = signal(this.readIsMobileViewport());
  protected readonly isMobileMenuOpen = signal(false);
  private readonly isDesktopSidebarCollapsed = signal(this.readDesktopSidebarCollapsedPreference());
  protected readonly navigation = computed(() =>
    NAVIGATION_ITEMS.filter((item) => this.permissionService.hasAnyRole(item.roles))
  );
  protected readonly roleLabels = computed(() => this.sessionService.roles().map((role) => getRoleDisplayLabel(role)).join(', '));
  protected readonly isDesktopCollapsed = computed(() => !this.isMobile() && this.isDesktopSidebarCollapsed());
  protected readonly compactUserName = computed(() => {
    const user = this.sessionService.currentUser();
    const source = user.displayName || user.username || 'US';
    return source
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase() ?? '')
      .join('');
  });

  constructor() {
    this.router.events
      .pipe(filter((event) => event instanceof NavigationEnd))
      .subscribe(() => {
        this.closeMobileMenu();
      });
  }

  protected logout(): void {
    void this.sessionService.logout();
  }

  protected toggleMobileMenu(): void {
    if (!this.isMobile()) {
      return;
    }

    this.isMobileMenuOpen.update((value) => !value);
  }

  protected closeMobileMenu(): void {
    this.isMobileMenuOpen.set(false);
  }

  protected handleNavigationSelection(): void {
    if (this.isMobile()) {
      this.closeMobileMenu();
    }
  }

  protected toggleDesktopSidebar(): void {
    if (this.isMobile()) {
      return;
    }

    const nextValue = !this.isDesktopSidebarCollapsed();
    this.isDesktopSidebarCollapsed.set(nextValue);

    if (typeof localStorage !== 'undefined') {
      localStorage.setItem(SIDEBAR_COLLAPSED_STORAGE_KEY, String(nextValue));
    }
  }

  @HostListener('window:resize')
  protected handleViewportResize(): void {
    const mobile = this.readIsMobileViewport();
    this.isMobile.set(mobile);

    if (!mobile) {
      this.closeMobileMenu();
    }
  }

  private readIsMobileViewport(): boolean {
    return typeof window !== 'undefined'
      ? window.innerWidth < MOBILE_BREAKPOINT
      : false;
  }

  private readDesktopSidebarCollapsedPreference(): boolean {
    if (typeof localStorage === 'undefined') {
      return false;
    }

    return localStorage.getItem(SIDEBAR_COLLAPSED_STORAGE_KEY) == 'true';
  }
}
