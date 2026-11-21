import { afterNextRender, ChangeDetectionStrategy, Component, DestroyRef, ElementRef, inject, input, OnDestroy, OnInit, output, signal, viewChild } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { PaymentComplementsApiService } from '../infrastructure/payment-complements-api.service';

@Component({
  selector: 'app-payment-complement-pdf-dialog',
  template: `
    <dialog #dialog aria-labelledby="rep-pdf-title" (cancel)="close($event)">
      <header>
        <div><p class="eyebrow">Evidencia de timbrado</p><h3 id="rep-pdf-title">PDF del complemento de pago #{{ complementId() }}</h3></div>
        <button type="button" class="secondary" autofocus (click)="closed.emit()">Cerrar</button>
      </header>
      <p class="uuid">UUID: {{ uuid() }}</p>
      @if (status() === 'Cancelled' || status() === 'CancellationRequested') {
        <p class="warning">{{ status() === 'Cancelled' ? 'Complemento cancelado. El PDF es evidencia histórica.' : 'Este complemento tiene una cancelación solicitada.' }}</p>
      }
      @if (loading()) { <p role="status">Preparando el PDF timbrado...</p> }
      @if (error()) { <p class="warning" role="alert">{{ error() }}</p><button type="button" (click)="loadPdf()" [disabled]="loading()">Reintentar</button> }
      @if (previewUrl(); as url) {
        <div class="actions">
          <button type="button" (click)="downloadPdf()" [disabled]="sharing()">Descargar PDF</button>
          @if (canShare()) { <button type="button" class="whatsapp" (click)="sharePdf()" [disabled]="sharing()">{{ sharing() ? 'Abriendo opciones...' : 'Compartir por WhatsApp' }}</button> }
        </div>
        <p class="helper">Tamaño carta, fondo blanco y títulos a color. Para imprimir, descarga y abre el PDF.</p>
        @if (canShare()) { <p class="helper">Selecciona WhatsApp y el contacto. Si tu navegador no permite compartir archivos, se descargará el PDF: adjúntalo manualmente en WhatsApp.</p> }
        <iframe [src]="url" title="Vista previa del complemento de pago"></iframe>
      }
      @if (message()) { <p role="status">{{ message() }}</p> }
    </dialog>
  `,
  styles: [`
    dialog { width:min(960px, calc(100vw - 2rem)); max-height:90vh; box-sizing:border-box; border:1px solid #d8d1c2; border-radius:1rem; padding:1.25rem; color:#182533; }
    dialog::backdrop { background:rgb(0 0 0 / 45%); }
    header { display:flex; align-items:flex-start; justify-content:space-between; gap:1rem; }
    h3 { margin:.25rem 0; } .eyebrow { margin:0; font-size:.75rem; color:#8a6a32; text-transform:uppercase; }
    .uuid { overflow-wrap:anywhere; } .helper { color:#5f6b76; font-size:.9rem; } .warning { color:#8a2929; }
    .actions { display:flex; gap:.75rem; flex-wrap:wrap; } button { border:0; border-radius:.7rem; padding:.75rem 1rem; background:#183a56; color:white; cursor:pointer; }
    button.secondary { background:#eee8dc; color:#182533; } button.whatsapp { background:#1f7a55; } button:disabled { opacity:.55; cursor:wait; }
    iframe { width:100%; height:55vh; border:1px solid #d8d1c2; border-radius:.5rem; margin-top:.5rem; background:white; }
    @media (max-width:600px) { dialog { padding:.8rem; } header { flex-wrap:wrap; } iframe { height:45vh; } }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PaymentComplementPdfDialogComponent implements OnInit, OnDestroy {
  readonly complementId = input.required<number>();
  readonly uuid = input.required<string>();
  readonly status = input.required<string>();
  readonly closed = output<void>();
  private readonly api = inject(PaymentComplementsApiService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly destroyRef = inject(DestroyRef);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  protected readonly loading = signal(false);
  protected readonly sharing = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly message = signal<string | null>(null);
  protected readonly previewUrl = signal<SafeResourceUrl | null>(null);
  private file: File | null = null;
  private objectUrl: string | null = null;

  constructor() {
    afterNextRender(() => this.dialog().nativeElement.showModal());
  }

  ngOnInit(): void { void this.loadPdf(); }
  ngOnDestroy(): void { this.releaseUrl(); }

  protected close(event: Event): void { event.preventDefault(); this.closed.emit(); }
  protected canShare(): boolean { return this.status() === 'Stamped' || this.status() === 'CancellationRejected'; }

  protected async loadPdf(): Promise<void> {
    if (this.loading()) return;
    this.loading.set(true);
    this.error.set(null);
    this.message.set(null);
    this.file = null;
    this.previewUrl.set(null);
    this.releaseUrl();
    try {
      const response = await firstValueFrom(this.api.downloadPaymentComplementPdf(this.complementId()).pipe(takeUntilDestroyed(this.destroyRef)));
      if (!response.body?.size || response.body.type.split(';')[0] !== 'application/pdf') {
        throw new Error('La respuesta no contiene un PDF válido.');
      }
      const safeUuid = this.uuid().replace(/[^a-zA-Z0-9-]/g, '_');
      this.file = new File([response.body], `REP_${safeUuid}.pdf`, { type: 'application/pdf' });
      this.objectUrl = URL.createObjectURL(this.file);
      // Only our authenticated PDF blob is trusted, never an API-provided remote URL.
      this.previewUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.objectUrl));
    } catch (error) {
      if (!this.destroyRef.destroyed) {
        let apiError: unknown = error;
        if (typeof error === 'object' && error && 'error' in error && error.error instanceof Blob) {
          try { apiError = { error: JSON.parse(await error.error.text()) }; } catch { /* Use safe fallback below. */ }
        }
        this.error.set(extractApiErrorMessage(apiError, 'No fue posible generar el PDF. Verifica que el complemento esté timbrado e intenta nuevamente.'));
      }
    } finally {
      this.loading.set(false);
    }
  }

  protected downloadPdf(): void {
    if (!this.file || !this.objectUrl) return;
    const link = document.createElement('a');
    link.href = this.objectUrl;
    link.download = this.file.name;
    link.click();
    this.message.set('PDF descargado. Puedes abrirlo para imprimirlo.');
  }

  protected async sharePdf(): Promise<void> {
    if (!this.file || this.sharing() || !this.canShare()) return;
    this.sharing.set(true);
    this.message.set(null);
    const shareData: ShareData = { files: [this.file], title: 'Complemento de pago', text: `Complemento de pago timbrado. UUID: ${this.uuid()}` };
    try {
      // The file is prepared before this click, retaining transient user activation on mobile.
      if (typeof navigator.share === 'function' && typeof navigator.canShare === 'function' && navigator.canShare({ files: [this.file] })) {
        await navigator.share(shareData);
        this.message.set('PDF entregado al selector de aplicaciones. Confirma el envío al contacto en WhatsApp.');
      } else {
        this.shareFallback();
      }
    } catch (error) {
      if (!(typeof error === 'object' && error && 'name' in error && error.name === 'AbortError')) this.shareFallback();
    } finally { this.sharing.set(false); }
  }

  private shareFallback(): void {
    this.downloadPdf();
    const text = `Te comparto el complemento de pago timbrado. UUID: ${this.uuid()}. Adjuntaré el PDF descargado.`;
    window.open(`https://wa.me/?text=${encodeURIComponent(text)}`, '_blank', 'noopener,noreferrer');
    this.message.set('PDF descargado. Selecciona el contacto en WhatsApp y adjunta el archivo manualmente. Si no se abrió WhatsApp, ábrelo desde tu dispositivo.');
  }

  private releaseUrl(): void {
    if (this.objectUrl) URL.revokeObjectURL(this.objectUrl);
    this.objectUrl = null;
  }
}
