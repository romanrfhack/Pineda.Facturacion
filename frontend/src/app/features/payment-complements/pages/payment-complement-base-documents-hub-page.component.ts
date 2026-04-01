import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { PaymentComplementBaseDocumentsPageComponent } from './payment-complement-base-documents-page.component';
import { PaymentComplementExternalBaseDocumentsPageComponent } from './payment-complement-external-base-documents-page.component';
import { PaymentComplementUnifiedBaseDocumentsPageComponent } from './payment-complement-unified-base-documents-page.component';

@Component({
  selector: 'app-payment-complement-base-documents-hub-page',
  imports: [
    PaymentComplementBaseDocumentsPageComponent,
    PaymentComplementExternalBaseDocumentsPageComponent,
    PaymentComplementUnifiedBaseDocumentsPageComponent
  ],
  template: `
    <section class="page">
      <header class="hero">
        <div>
          <p class="eyebrow">Complementos de pago</p>
          <h2>Administración REP interna y externa</h2>
          <p class="helper">La experiencia se unifica por documento base, pero la operación completa sigue disponible sólo para CFDI internos. Los CFDI externos quedan en seguimiento y preparación para Fase 4.</p>
        </div>
      </header>

      <nav class="tabs" aria-label="Bandejas REP">
        <button type="button" [class.active]="activeTab() === 'unified'" (click)="activeTab.set('unified')">Unificada</button>
        <button type="button" [class.active]="activeTab() === 'internal'" (click)="activeTab.set('internal')">Internos</button>
        <button type="button" [class.active]="activeTab() === 'external'" (click)="activeTab.set('external')">Externos</button>
      </nav>

      @switch (activeTab()) {
        @case ('internal') {
          <app-payment-complement-base-documents-page />
        }
        @case ('external') {
          <app-payment-complement-external-base-documents-page />
        }
        @default {
          <app-payment-complement-unified-base-documents-page />
        }
      }
    </section>
  `,
  styles: [`
    .page { display:grid; gap:1rem; }
    .hero { border:1px solid #d8d1c2; border-radius:1rem; background:#fff; padding:1rem; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    .helper { margin:0; color:#5f6b76; }
    .tabs { display:flex; flex-wrap:wrap; gap:0.75rem; }
    .tabs button { border:1px solid #d8d1c2; background:#fff; color:#182533; border-radius:999px; padding:0.65rem 1rem; cursor:pointer; }
    .tabs button.active { background:#182533; color:#fff; border-color:#182533; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PaymentComplementBaseDocumentsHubPageComponent {
  protected readonly activeTab = signal<'unified' | 'internal' | 'external'>('unified');
}
