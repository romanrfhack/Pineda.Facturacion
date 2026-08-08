import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { GlobalLoaderHandle, GlobalLoaderOptions, GlobalLoaderService } from '../../../core/ui/global-loader.service';

interface LoaderPreviewScenario extends GlobalLoaderOptions {
  title: string;
  description: string;
  durationMs: number;
}

const PREVIEW_SCENARIOS: readonly LoaderPreviewScenario[] = [
  {
    title: 'Consultar órdenes',
    description: 'Ejemplo para búsquedas, filtros y carga de información.',
    message: 'Consultando órdenes',
    detail: 'Estamos localizando y organizando las órdenes disponibles.',
    durationMs: 3_200
  },
  {
    title: 'Generar documento',
    description: 'Ejemplo para PDF, XML, reportes y exportaciones.',
    message: 'Generando documento',
    detail: 'Estamos preparando el archivo con la información más reciente.',
    durationMs: 3_600
  },
  {
    title: 'Timbrar CFDI',
    description: 'Ejemplo para operaciones críticas que no deben interrumpirse.',
    message: 'Timbrando CFDI',
    detail: 'Validamos la información y esperamos la respuesta del proveedor de certificación.',
    durationMs: 4_200
  }
];

@Component({
  selector: 'app-loader-preview-page',
  template: `
    <section class="preview-page">
      <header>
        <p class="eyebrow">Laboratorio visual</p>
        <h1>Loader global Pineda</h1>
        <p class="intro">
          Esta pantalla permite revisar el diseño y los mensajes utilizados por las operaciones del sistema.
        </p>
      </header>

      <div class="design-summary">
        <div>
          <span class="summary-number">01</span>
          <strong>Identidad Pineda</strong>
          <p>Azul institucional, acento naranja y emblema mecánico animado.</p>
        </div>
        <div>
          <span class="summary-number">02</span>
          <strong>Bloqueo seguro</strong>
          <p>Evita dobles clics y acciones simultáneas mientras termina el proceso.</p>
        </div>
        <div>
          <span class="summary-number">03</span>
          <strong>Mensaje contextual</strong>
          <p>La leyenda puede cambiar según la operación que se esté ejecutando.</p>
        </div>
      </div>

      <div class="scenario-grid">
        @for (scenario of scenarios; track scenario.title) {
          <article>
            <div class="scenario-icon" aria-hidden="true">{{ $index + 1 }}</div>
            <h2>{{ scenario.title }}</h2>
            <p>{{ scenario.description }}</p>
            <button type="button" (click)="showPreview(scenario)">Ver animación</button>
          </article>
        }
      </div>

      <aside>
        <strong>Integración actual:</strong>
        las operaciones de escritura y los documentos de procesamiento prolongado activan automáticamente el loader.
        También se habilitó de forma selectiva en consultas perceptibles de Órdenes, CFDI emitidos, Cartera,
        workspaces de receptor, Pagos, bandejas REP y Auditoría; autocompletados y lecturas silenciosas permanecen sin bloqueo global.
      </aside>
    </section>
  `,
  styles: [`
    :host { display:block; }
    .preview-page { max-width:1120px; margin:0 auto; padding:0.5rem 0 2rem; color:#102c4e; }
    header { padding:2rem; border:1px solid #d9e9fb; border-radius:1.5rem; background:linear-gradient(130deg, #fff 0%, #edf6ff 72%, #fff4e9 100%); box-shadow:0 14px 36px rgba(0,65,139,0.08); }
    .eyebrow { margin:0 0 0.5rem; color:#0870df; font-size:0.76rem; font-weight:750; letter-spacing:0.14em; text-transform:uppercase; }
    h1 { margin:0; font-size:clamp(1.8rem, 4vw, 2.65rem); line-height:1.1; }
    .intro { max-width:45rem; margin:0.8rem 0 0; color:#536b85; line-height:1.6; }
    .design-summary { display:grid; grid-template-columns:repeat(3, 1fr); gap:1rem; margin:1.25rem 0; }
    .design-summary div { padding:1.2rem; border:1px solid #dfe8f1; border-radius:1rem; background:#fff; }
    .summary-number { display:block; margin-bottom:0.7rem; color:#f28c22; font-size:0.78rem; font-weight:800; letter-spacing:0.12em; }
    .design-summary strong { font-size:1rem; }
    .design-summary p { margin:0.45rem 0 0; color:#60748a; font-size:0.9rem; line-height:1.45; }
    .scenario-grid { display:grid; grid-template-columns:repeat(3, 1fr); gap:1rem; }
    article { display:flex; min-height:245px; flex-direction:column; align-items:flex-start; padding:1.3rem; border:1px solid #dce7f3; border-radius:1.1rem; background:#fff; box-shadow:0 10px 24px rgba(0,49,105,0.06); }
    .scenario-icon { width:2.4rem; height:2.4rem; display:grid; place-items:center; border-radius:0.8rem; background:#0976f2; color:#fff; font-size:0.82rem; font-weight:800; box-shadow:0 0.45rem 0.9rem rgba(9,118,242,0.2); }
    article h2 { margin:1rem 0 0.45rem; font-size:1.15rem; }
    article p { margin:0 0 1rem; color:#60748a; font-size:0.92rem; line-height:1.5; }
    button { width:100%; margin-top:auto; padding:0.78rem 1rem; border:0; border-radius:0.8rem; background:linear-gradient(135deg, #0976f2, #0064ee); color:#fff; cursor:pointer; font-weight:700; box-shadow:0 0.5rem 1rem rgba(9,118,242,0.18); }
    button:hover { filter:brightness(1.04); transform:translateY(-1px); }
    button:focus-visible { outline:3px solid rgba(249,153,51,0.45); outline-offset:3px; }
    aside { margin-top:1.25rem; padding:1rem 1.15rem; border-left:0.3rem solid #f99933; border-radius:0.25rem 0.8rem 0.8rem 0.25rem; background:#fff8ef; color:#68491f; line-height:1.5; }
    @media (max-width:900px) { .design-summary, .scenario-grid { grid-template-columns:1fr; } article { min-height:auto; } }
    @media (max-width:520px) { header { padding:1.35rem; } }
    @media (prefers-reduced-motion:reduce) { button:hover { transform:none; } }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LoaderPreviewPageComponent {
  private readonly loader = inject(GlobalLoaderService);
  private readonly destroyRef = inject(DestroyRef);
  private activeHandle: GlobalLoaderHandle | null = null;
  private closeTimer: ReturnType<typeof setTimeout> | null = null;

  protected readonly scenarios = PREVIEW_SCENARIOS;

  constructor() {
    this.destroyRef.onDestroy(() => this.closePreview());
  }

  protected showPreview(scenario: LoaderPreviewScenario): void {
    this.closePreview();
    this.activeHandle = this.loader.begin(scenario);
    this.closeTimer = setTimeout(() => this.closePreview(), scenario.durationMs);
  }

  private closePreview(): void {
    if (this.closeTimer) {
      clearTimeout(this.closeTimer);
      this.closeTimer = null;
    }

    this.activeHandle?.close();
    this.activeHandle = null;
  }
}
