import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

@Component({
  selector: 'app-xml-viewer-panel',
  template: `
    <section class="panel" aria-live="polite">
      <div class="header">
        <div>
          <p class="eyebrow">XML evidence</p>
          <h3>{{ title() }}</h3>
        </div>
        <button type="button" class="secondary" (click)="close.emit()">Close</button>
      </div>

      @if (loading()) {
        <p class="helper">Loading XML evidence...</p>
      } @else if (errorMessage()) {
        <p class="error">{{ errorMessage() }}</p>
      } @else if (xmlContent()) {
        <pre>{{ xmlContent() }}</pre>
      } @else {
        <p class="helper">No XML evidence is available.</p>
      }
    </section>
  `,
  styles: [`
    .panel { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fffdf8; }
    .header { display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    h3 { margin:0.25rem 0 0; }
    .helper { color:#5f6b76; }
    .error { color:#7a2020; }
    pre { margin:0.75rem 0 0; max-height:28rem; overflow:auto; padding:1rem; border-radius:0.8rem; background:#182533; color:#f7f4ee; white-space:pre-wrap; word-break:break-word; font-family:"SFMono-Regular", Consolas, "Liberation Mono", Menlo, monospace; font-size:0.88rem; line-height:1.4; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#d8c49b; color:#182533; cursor:pointer; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class XmlViewerPanelComponent {
  readonly title = input('Stamped XML');
  readonly loading = input(false);
  readonly xmlContent = input<string | null>(null);
  readonly errorMessage = input<string | null>(null);
  readonly close = output<void>();
}
