import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { SessionService } from '../../../core/auth/session.service';
import { FeedbackService } from '../../../core/ui/feedback.service';

@Component({
  selector: 'app-login-page',
  imports: [FormsModule],
  template: `
    <section class="login-page">
      <form class="login-card" (ngSubmit)="submit()">
        <p class="eyebrow">Acceso administrativo</p>
        <h1>Iniciar sesión</h1>
        <p class="hint">Usa una cuenta local con el rol requerido para la operación fiscal que necesitas realizar.</p>

        <label>
          <span>Usuario</span>
          <input [(ngModel)]="username" name="username" autocomplete="username" required />
        </label>

        <label>
          <span>Contraseña</span>
          <input [(ngModel)]="password" name="password" type="password" autocomplete="current-password" required />
        </label>

        @if (errorMessage()) {
          <div class="error">{{ errorMessage() }}</div>
        }

        <button type="submit" [disabled]="sessionService.loggingIn()">
          {{ sessionService.loggingIn() ? 'Ingresando...' : 'Iniciar sesión' }}
        </button>
      </form>
    </section>
  `,
  styles: [`
    .login-page { min-height:100vh; display:grid; place-items:center; background:
      radial-gradient(circle at top left, rgba(212,194,156,0.45), transparent 32%),
      linear-gradient(160deg, #182533 0%, #213348 48%, #f4efe4 48%, #f7f4ed 100%);
      padding:1.5rem; }
    .login-card { width:min(100%, 420px); background:rgba(255,255,255,0.94); border:1px solid rgba(24,37,51,0.08); border-radius:1.25rem; padding:1.5rem; display:grid; gap:1rem; box-shadow:0 20px 70px rgba(24,37,51,0.18); }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.14em; font-size:0.72rem; color:#8a6a32; }
    h1 { margin:0; }
    .hint { margin:0; color:#586574; }
    label { display:grid; gap:0.35rem; }
    input { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; font:inherit; }
    .error { background:#fff0f0; color:#7a2020; border:1px solid #ebb1b1; padding:0.75rem 0.9rem; border-radius:0.8rem; }
    button { padding:0.8rem 1rem; border:none; border-radius:0.9rem; background:#182533; color:#fff; font:inherit; cursor:pointer; }
    button:disabled { opacity:0.65; cursor:wait; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LoginPageComponent {
  protected readonly sessionService = inject(SessionService);
  private readonly router = inject(Router);
  private readonly feedbackService = inject(FeedbackService);

  protected username = '';
  protected password = '';
  protected readonly errorMessage = signal<string | null>(null);

  protected async submit(): Promise<void> {
    this.errorMessage.set(null);
    const response = await this.sessionService.login({
      username: this.username.trim(),
      password: this.password
    });

    if (!response.isSuccess) {
      this.errorMessage.set(response.errorMessage || 'Credenciales inválidas.');
      return;
    }

    this.feedbackService.show('success', 'Sesión iniciada.');
    await this.router.navigate([this.sessionService.getDefaultAppRoute()]);
  }
}
