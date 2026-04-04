import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter, Router } from '@angular/router';
import { AppShellComponent } from './app-shell.component';
import { AppRole, CurrentUser } from '../auth/models';
import { SessionService } from '../auth/session.service';
import { PermissionService } from '../auth/permission.service';

@Component({
  standalone: true,
  template: '<p>Dummy</p>'
})
class DummyPageComponent {}

class SessionServiceStub {
  readonly currentUser = signal<CurrentUser>({
    id: 1,
    username: 'operator',
    displayName: 'Operador Demo',
    roles: [AppRole.Admin],
    isAuthenticated: true
  });

  readonly roles = signal<AppRole[]>([AppRole.Admin]);
  readonly logout = vi.fn().mockResolvedValue(undefined);
}

class PermissionServiceStub {
  hasAnyRole = vi.fn().mockReturnValue(true);
}

describe('AppShellComponent', () => {
  let router: Router;

  beforeEach(async () => {
    localStorage.clear();

    await TestBed.configureTestingModule({
      imports: [AppShellComponent],
      providers: [
        provideRouter([
          { path: 'app/orders', component: DummyPageComponent },
          { path: 'app/accounts-receivable', component: DummyPageComponent },
          { path: '**', redirectTo: 'app/orders' }
        ]),
        { provide: SessionService, useClass: SessionServiceStub },
        { provide: PermissionService, useClass: PermissionServiceStub }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
  });

  it('opens and closes the mobile drawer, then closes after navigation', async () => {
    setViewportWidth(520);
    await router.navigateByUrl('/app/orders');

    const fixture = TestBed.createComponent(AppShellComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('.sidebar.mobile-open'))).toBeNull();

    clickButton(fixture, 'Abrir menú de navegación');
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('.sidebar.mobile-open'))).not.toBeNull();
    expect(fixture.debugElement.query(By.css('.backdrop'))).not.toBeNull();

    await router.navigateByUrl('/app/accounts-receivable');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('.sidebar.mobile-open'))).toBeNull();
  });

  it('closes the mobile drawer when clicking the backdrop', async () => {
    setViewportWidth(520);
    await router.navigateByUrl('/app/orders');

    const fixture = TestBed.createComponent(AppShellComponent);
    fixture.detectChanges();
    await fixture.whenStable();

    clickButton(fixture, 'Abrir menú de navegación');
    fixture.detectChanges();

    const backdrop = fixture.debugElement.query(By.css('.backdrop')).nativeElement as HTMLButtonElement;
    backdrop.click();
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('.sidebar.mobile-open'))).toBeNull();
  });

  it('toggles the desktop sidebar collapsed state and persists it', async () => {
    setViewportWidth(1280);
    await router.navigateByUrl('/app/orders');

    const fixture = TestBed.createComponent(AppShellComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('.shell.desktop-collapsed'))).toBeNull();

    clickButton(fixture, 'Colapsar menú lateral');
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('.shell.desktop-collapsed'))).not.toBeNull();
    expect(localStorage.getItem('app-shell-sidebar-collapsed')).toBe('true');

    const firstLink = fixture.debugElement.query(By.css('nav a')).nativeElement as HTMLAnchorElement;
    expect(firstLink.title).toContain('Órdenes');
  });

  function clickButton(fixture: ReturnType<typeof TestBed.createComponent<AppShellComponent>>, ariaLabel: string): void {
    const button = fixture.debugElement
      .queryAll(By.css('button'))
      .find((item) => item.attributes['aria-label'] === ariaLabel);

    expect(button).toBeDefined();
    (button!.nativeElement as HTMLButtonElement).click();
  }

  function setViewportWidth(width: number): void {
    Object.defineProperty(window, 'innerWidth', {
      configurable: true,
      writable: true,
      value: width
    });

    window.dispatchEvent(new Event('resize'));
  }
});
