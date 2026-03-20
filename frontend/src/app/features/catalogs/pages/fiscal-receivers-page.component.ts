import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { PermissionService } from '../../../core/auth/permission.service';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { FiscalReceiverFormComponent } from '../components/fiscal-receiver-form.component';
import { FiscalReceiversApiService } from '../infrastructure/fiscal-receivers-api.service';
import { FiscalReceiver, FiscalReceiverSearchItem, UpsertFiscalReceiverRequest } from '../models/catalogs.models';

@Component({
  selector: 'app-fiscal-receivers-page',
  imports: [FormsModule, FiscalReceiverFormComponent],
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">Catalogs / Fiscal receivers</p>
        <h2>Receiver master data</h2>
      </header>

      <section class="card">
        <div class="toolbar">
          <label>
            <span>Search receivers</span>
            <input [(ngModel)]="query" name="query" placeholder="RFC or legal name" />
          </label>

          <div class="actions">
            <button type="button" (click)="search()" [disabled]="loadingList()">{{ loadingList() ? 'Searching...' : 'Search' }}</button>
            @if (permissionService.canWriteMasterData()) {
              <button type="button" class="secondary" (click)="startCreate()">New receiver</button>
            }
          </div>
        </div>

        @if (listError()) {
          <p class="error">{{ listError() }}</p>
        } @else if (!receivers().length) {
          <p class="helper">No receivers loaded yet. Search by RFC or legal name.</p>
        } @else {
          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>RFC</th>
                  <th>Legal name</th>
                  <th>Postal code</th>
                  <th>Regime</th>
                  <th>CFDI use</th>
                  <th>Status</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (receiver of receivers(); track receiver.id) {
                  <tr>
                    <td>{{ receiver.rfc }}</td>
                    <td>{{ receiver.legalName }}</td>
                    <td>{{ receiver.postalCode }}</td>
                    <td>{{ receiver.fiscalRegimeCode }}</td>
                    <td>{{ receiver.cfdiUseCodeDefault }}</td>
                    <td>{{ receiver.isActive ? 'Active' : 'Inactive' }}</td>
                    <td><button type="button" class="link" (click)="selectReceiver(receiver)">Inspect</button></td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </section>

      <section class="card">
        <h3>{{ selectedReceiver() ? 'Receiver details' : 'New receiver' }}</h3>
        <app-fiscal-receiver-form
          [receiver]="selectedReceiver()"
          [readOnly]="!permissionService.canWriteMasterData()"
          [submitLabel]="selectedReceiver() ? 'Update receiver' : 'Create receiver'"
          [errorMessage]="formError()"
          (submitted)="save($event)"
        />
      </section>
    </section>
  `,
  styles: [`
    .page { display:grid; gap:1rem; }
    .card { border:1px solid #d8d1c2; border-radius:1rem; padding:1rem; background:#fff; }
    .eyebrow { margin:0; text-transform:uppercase; letter-spacing:0.12em; font-size:0.72rem; color:#8a6a32; }
    .toolbar { display:flex; flex-wrap:wrap; gap:1rem; align-items:end; justify-content:space-between; }
    label { display:grid; gap:0.35rem; min-width:260px; }
    input, button { font:inherit; }
    input { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; }
    .actions { display:flex; gap:0.75rem; flex-wrap:wrap; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    button.secondary { background:#d8c49b; color:#182533; }
    button.link { background:transparent; color:#182533; padding:0; }
    .helper { margin:0; color:#5f6b76; }
    .error { margin:0; color:#7a2020; }
    .table-wrap { overflow:auto; }
    table { width:100%; border-collapse:collapse; }
    th, td { text-align:left; padding:0.75rem 0.5rem; border-bottom:1px solid #ece5d7; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FiscalReceiversPageComponent {
  private readonly api = inject(FiscalReceiversApiService);
  private readonly feedbackService = inject(FeedbackService);
  protected readonly permissionService = inject(PermissionService);

  protected query = '';
  protected readonly loadingList = signal(false);
  protected readonly listError = signal<string | null>(null);
  protected readonly formError = signal<string | null>(null);
  protected readonly receivers = signal<FiscalReceiverSearchItem[]>([]);
  protected readonly selectedReceiver = signal<FiscalReceiver | null>(null);

  protected async search(): Promise<void> {
    this.loadingList.set(true);
    this.listError.set(null);
    try {
      this.receivers.set(await firstValueFrom(this.api.search(this.query.trim())));
    } catch (error) {
      this.listError.set(extractApiErrorMessage(error));
    } finally {
      this.loadingList.set(false);
    }
  }

  protected startCreate(): void {
    this.formError.set(null);
    this.selectedReceiver.set(null);
  }

  protected async selectReceiver(receiver: FiscalReceiverSearchItem): Promise<void> {
    this.formError.set(null);
    try {
      this.selectedReceiver.set(await firstValueFrom(this.api.getByRfc(receiver.rfc)));
    } catch (error) {
      this.feedbackService.show('error', extractApiErrorMessage(error));
    }
  }

  protected async save(request: UpsertFiscalReceiverRequest): Promise<void> {
    if (!this.permissionService.canWriteMasterData()) {
      return;
    }

    this.formError.set(null);
    try {
      const selected = this.selectedReceiver();
      if (selected) {
        await firstValueFrom(this.api.update(selected.id, request));
        this.feedbackService.show('success', 'Receiver updated.');
      } else {
        await firstValueFrom(this.api.create(request));
        this.feedbackService.show('success', 'Receiver created.');
      }

      await this.search();
      const refreshed = await firstValueFrom(this.api.getByRfc(request.rfc));
      this.selectedReceiver.set(refreshed);
    } catch (error) {
      this.formError.set(extractApiErrorMessage(error));
    }
  }
}
