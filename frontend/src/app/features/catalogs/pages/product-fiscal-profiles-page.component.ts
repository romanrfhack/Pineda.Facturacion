import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { PermissionService } from '../../../core/auth/permission.service';
import { FeedbackService } from '../../../core/ui/feedback.service';
import { extractApiErrorMessage } from '../../../core/http/api-error-message';
import { ProductFiscalProfileFormComponent } from '../components/product-fiscal-profile-form.component';
import { ProductFiscalProfilesApiService } from '../infrastructure/product-fiscal-profiles-api.service';
import { ProductFiscalProfile, ProductFiscalProfileSearchItem, UpsertProductFiscalProfileRequest } from '../models/catalogs.models';

@Component({
  selector: 'app-product-fiscal-profiles-page',
  imports: [FormsModule, ProductFiscalProfileFormComponent],
  template: `
    <section class="page">
      <header>
        <p class="eyebrow">Catalogs / Product fiscal profiles</p>
        <h2>Product SAT mappings</h2>
      </header>

      <section class="card">
        <div class="toolbar">
          <label>
            <span>Search products</span>
            <input [(ngModel)]="query" name="query" placeholder="Internal code or description" />
          </label>
          <div class="actions">
            <button type="button" (click)="search()" [disabled]="loadingList()">{{ loadingList() ? 'Searching...' : 'Search' }}</button>
            @if (permissionService.canWriteMasterData()) {
              <button type="button" class="secondary" (click)="startCreate()">New profile</button>
            }
          </div>
        </div>

        @if (listError()) {
          <p class="error">{{ listError() }}</p>
        } @else if (!profiles().length) {
          <p class="helper">Search by internal code or description to inspect fiscal mappings.</p>
        } @else {
          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Internal code</th>
                  <th>Description</th>
                  <th>Prod/Serv</th>
                  <th>Unit</th>
                  <th>Tax object</th>
                  <th>VAT</th>
                  <th>Status</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (profile of profiles(); track profile.id) {
                  <tr>
                    <td>{{ profile.internalCode }}</td>
                    <td>{{ profile.description }}</td>
                    <td>{{ profile.satProductServiceCode }}</td>
                    <td>{{ profile.satUnitCode }}</td>
                    <td>{{ profile.taxObjectCode }}</td>
                    <td>{{ profile.vatRate }}</td>
                    <td>{{ profile.isActive ? 'Active' : 'Inactive' }}</td>
                    <td><button type="button" class="link" (click)="selectProfile(profile)">Inspect</button></td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </section>

      <section class="card">
        <h3>{{ selectedProfile() ? 'Product fiscal profile details' : 'New product fiscal profile' }}</h3>
        <app-product-fiscal-profile-form
          [profile]="selectedProfile()"
          [readOnly]="!permissionService.canWriteMasterData()"
          [submitLabel]="selectedProfile() ? 'Update product fiscal profile' : 'Create product fiscal profile'"
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
export class ProductFiscalProfilesPageComponent {
  private readonly api = inject(ProductFiscalProfilesApiService);
  private readonly feedbackService = inject(FeedbackService);
  protected readonly permissionService = inject(PermissionService);

  protected query = '';
  protected readonly loadingList = signal(false);
  protected readonly listError = signal<string | null>(null);
  protected readonly formError = signal<string | null>(null);
  protected readonly profiles = signal<ProductFiscalProfileSearchItem[]>([]);
  protected readonly selectedProfile = signal<ProductFiscalProfile | null>(null);

  protected async search(): Promise<void> {
    this.loadingList.set(true);
    this.listError.set(null);
    try {
      this.profiles.set(await firstValueFrom(this.api.search(this.query.trim())));
    } catch (error) {
      this.listError.set(extractApiErrorMessage(error));
    } finally {
      this.loadingList.set(false);
    }
  }

  protected startCreate(): void {
    this.formError.set(null);
    this.selectedProfile.set(null);
  }

  protected async selectProfile(profile: ProductFiscalProfileSearchItem): Promise<void> {
    this.formError.set(null);
    try {
      this.selectedProfile.set(await firstValueFrom(this.api.getByCode(profile.internalCode)));
    } catch (error) {
      this.feedbackService.show('error', extractApiErrorMessage(error));
    }
  }

  protected async save(request: UpsertProductFiscalProfileRequest): Promise<void> {
    if (!this.permissionService.canWriteMasterData()) {
      return;
    }

    this.formError.set(null);
    try {
      const selected = this.selectedProfile();
      if (selected) {
        await firstValueFrom(this.api.update(selected.id, request));
        this.feedbackService.show('success', 'Product fiscal profile updated.');
      } else {
        await firstValueFrom(this.api.create(request));
        this.feedbackService.show('success', 'Product fiscal profile created.');
      }

      await this.search();
      const refreshed = await firstValueFrom(this.api.getByCode(request.internalCode));
      this.selectedProfile.set(refreshed);
    } catch (error) {
      this.formError.set(extractApiErrorMessage(error));
    }
  }
}
