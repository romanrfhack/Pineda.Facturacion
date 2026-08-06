import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { GlobalLoaderService } from './global-loader.service';

@Component({
  selector: 'app-pineda-loader-overlay',
  template: `
    @if (loader.active()) {
      <div class="loader-backdrop" data-testid="global-loader" aria-busy="true">
        <section class="loader-card" role="status" aria-live="polite" aria-atomic="true">
          <div class="loader-accent" aria-hidden="true"></div>

          <div class="mechanism" aria-hidden="true">
            <span class="rotor"></span>
            <span class="inner-ring"></span>
            <span class="orbit"><i></i></span>
            <span class="bolt bolt-top"></span>
            <span class="bolt bolt-right"></span>
            <span class="bolt bolt-bottom"></span>
            <span class="bolt bolt-left"></span>

            <div class="hub">
              <svg viewBox="0 0 112 78" focusable="false">
                <path d="M8 62 37 13h34l14 24H67L59 24H45L22 62Z" />
                <path d="M38 62 61 25l23 37H67l-7-13-8 13Z" />
                <path d="M69 13h17l19 33H88Z" />
              </svg>
              <strong>PINEDA</strong>
            </div>
          </div>

          <p class="eyebrow">{{ loader.current().eyebrow }}</p>
          <h2>{{ loader.current().message }}</h2>
          <p class="detail">{{ loader.current().detail }}</p>

          <div class="progress-track" aria-hidden="true"><span></span></div>
          <p class="safety-note">No cierres ni actualices esta pantalla.</p>
        </section>
      </div>
    }
  `,
  styles: [`
    :host { display:contents; }
    .loader-backdrop { position:fixed; inset:0; z-index:1000; display:grid; place-items:center; padding:1rem; background:rgba(3, 23, 53, 0.58); backdrop-filter:blur(6px); animation:backdrop-in 180ms ease-out; }
    .loader-card { position:relative; isolation:isolate; width:min(92vw, 430px); overflow:hidden; padding:2rem 2rem 1.45rem; border:1px solid rgba(255,255,255,0.7); border-radius:1.75rem; background:linear-gradient(155deg, rgba(255,255,255,0.98), rgba(239,247,255,0.97)); box-shadow:0 28px 80px rgba(0,31,78,0.32), 0 2px 8px rgba(0,31,78,0.16); text-align:center; animation:card-in 240ms cubic-bezier(.2,.8,.2,1); }
    .loader-card::before { content:''; position:absolute; z-index:-1; width:15rem; height:15rem; top:-9rem; right:-7rem; border-radius:50%; background:rgba(9,118,242,0.12); }
    .loader-card::after { content:''; position:absolute; z-index:-1; width:11rem; height:11rem; bottom:-7rem; left:-5rem; border-radius:50%; background:rgba(249,153,51,0.12); }
    .loader-accent { position:absolute; inset:0 auto 0 0; width:0.35rem; background:linear-gradient(#0976f2, #f99933); }
    .mechanism { position:relative; width:9.9rem; height:9.9rem; margin:0 auto 1.35rem; display:grid; place-items:center; }
    .rotor, .inner-ring, .orbit { position:absolute; inset:0; border-radius:50%; }
    .rotor { background:conic-gradient(from 20deg, #0976f2 0 21%, transparent 21% 29%, #0976f2 29% 51%, transparent 51% 61%, #f99933 61% 70%, transparent 70% 80%, rgba(9,118,242,0.48) 80% 100%); -webkit-mask:radial-gradient(farthest-side, transparent calc(100% - 0.67rem), #000 0); mask:radial-gradient(farthest-side, transparent calc(100% - 0.67rem), #000 0); filter:drop-shadow(0 0.35rem 0.55rem rgba(9,118,242,0.22)); animation:spin 2.15s linear infinite; }
    .inner-ring { inset:0.95rem; border:2px dashed rgba(9,118,242,0.36); animation:spin-reverse 5.2s linear infinite; }
    .orbit { inset:0.24rem; animation:spin 1.65s linear infinite; }
    .orbit i { position:absolute; top:0.15rem; left:50%; width:0.78rem; height:0.78rem; margin-left:-0.39rem; border:0.16rem solid #fff; border-radius:50%; background:#f99933; box-shadow:0 0 0 0.28rem rgba(249,153,51,0.16), 0 0.25rem 0.6rem rgba(111,60,0,0.28); }
    .hub { position:relative; z-index:2; width:6.65rem; height:6.65rem; display:grid; place-items:center; align-content:center; gap:0.1rem; border:0.32rem solid #fff; border-radius:50%; background:linear-gradient(145deg, #1385ff, #0064ee); box-shadow:0 0.7rem 1.45rem rgba(0,75,177,0.28), inset 0 0.2rem 0.35rem rgba(255,255,255,0.28); animation:hub-pulse 1.8s ease-in-out infinite; }
    .hub svg { width:4.45rem; height:3.1rem; overflow:visible; }
    .hub path { fill:#fff; }
    .hub strong { color:#fff; font-size:0.72rem; letter-spacing:0.12em; line-height:1; }
    .bolt { position:absolute; z-index:1; width:0.55rem; height:0.55rem; border:2px solid rgba(9,118,242,0.28); border-radius:0.16rem; background:#fff; transform:rotate(45deg); }
    .bolt-top { top:1.05rem; left:1.05rem; }
    .bolt-right { top:1.05rem; right:1.05rem; }
    .bolt-bottom { right:1.05rem; bottom:1.05rem; }
    .bolt-left { left:1.05rem; bottom:1.05rem; }
    .eyebrow { margin:0 0 0.45rem; color:#0870df; font-size:0.72rem; font-weight:750; letter-spacing:0.14em; text-transform:uppercase; }
    h2 { margin:0; color:#082b57; font-size:clamp(1.22rem, 4vw, 1.55rem); line-height:1.2; }
    .detail { max-width:22rem; margin:0.65rem auto 0; color:#47627f; font-size:0.95rem; line-height:1.5; }
    .progress-track { position:relative; height:0.34rem; margin:1.35rem 0 0.8rem; overflow:hidden; border-radius:999px; background:rgba(9,118,242,0.12); }
    .progress-track span { position:absolute; inset:0 auto 0 -45%; width:45%; border-radius:inherit; background:linear-gradient(90deg, transparent, #f99933 35%, #0976f2 75%, transparent); animation:progress-sweep 1.25s ease-in-out infinite; }
    .safety-note { margin:0; color:#6a7d91; font-size:0.78rem; }
    @keyframes spin { to { transform:rotate(360deg); } }
    @keyframes spin-reverse { to { transform:rotate(-360deg); } }
    @keyframes hub-pulse { 0%,100% { transform:scale(0.98); } 50% { transform:scale(1.025); } }
    @keyframes progress-sweep { 0% { left:-45%; } 100% { left:100%; } }
    @keyframes backdrop-in { from { opacity:0; } }
    @keyframes card-in { from { opacity:0; transform:translateY(0.7rem) scale(0.97); } }
    @media (max-width:520px) { .loader-card { padding:1.65rem 1.25rem 1.25rem; border-radius:1.4rem; } .mechanism { transform:scale(0.9); margin-block:-0.35rem 0.9rem; } }
    @media (prefers-reduced-motion:reduce) { .loader-backdrop, .loader-card, .rotor, .inner-ring, .orbit, .hub, .progress-track span { animation:none; } .progress-track span { left:25%; width:50%; } }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PinedaLoaderOverlayComponent {
  protected readonly loader = inject(GlobalLoaderService);
}
