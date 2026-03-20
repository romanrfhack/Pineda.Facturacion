import { ChangeDetectionStrategy, Component, OnChanges, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ProductFiscalProfile, UpsertProductFiscalProfileRequest } from '../models/catalogs.models';

@Component({
  selector: 'app-product-fiscal-profile-form',
  imports: [FormsModule],
  template: `
    <form class="form-grid" (ngSubmit)="submitForm()">
      <label>
        <span>Internal code</span>
        <input [(ngModel)]="draft.internalCode" name="internalCode" required />
      </label>

      <label>
        <span>Description</span>
        <input [(ngModel)]="draft.description" name="description" required />
      </label>

      <label>
        <span>SAT product/service code</span>
        <input [(ngModel)]="draft.satProductServiceCode" name="satProductServiceCode" required />
      </label>

      <label>
        <span>SAT unit code</span>
        <input [(ngModel)]="draft.satUnitCode" name="satUnitCode" required />
      </label>

      <label>
        <span>Tax object code</span>
        <input [(ngModel)]="draft.taxObjectCode" name="taxObjectCode" required />
      </label>

      <label>
        <span>VAT rate</span>
        <input [(ngModel)]="draft.vatRate" name="vatRate" type="number" min="0" step="0.0001" required />
      </label>

      <label>
        <span>Default unit text</span>
        <input [(ngModel)]="draft.defaultUnitText" name="defaultUnitText" />
      </label>

      <label class="checkbox">
        <input [(ngModel)]="draft.isActive" name="isActive" type="checkbox" />
        <span>Active</span>
      </label>

      @if (errorMessage()) {
        <p class="error">{{ errorMessage() }}</p>
      }

      <button type="submit" [disabled]="readOnly()">{{ submitLabel() }}</button>
    </form>
  `,
  styles: [`
    .form-grid { display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:1rem; align-items:end; }
    label { display:grid; gap:0.35rem; }
    input, button { font:inherit; }
    input { border:1px solid #c9d1da; border-radius:0.8rem; padding:0.75rem 0.9rem; }
    .checkbox { display:flex; align-items:center; gap:0.5rem; }
    .checkbox input { width:auto; }
    button { border:none; border-radius:0.8rem; padding:0.75rem 1rem; background:#182533; color:#fff; cursor:pointer; }
    button:disabled { opacity:0.6; cursor:not-allowed; }
    .error { color:#7a2020; margin:0; grid-column:1 / -1; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProductFiscalProfileFormComponent implements OnChanges {
  readonly profile = input<ProductFiscalProfile | null>(null);
  readonly submitLabel = input('Save product fiscal profile');
  readonly readOnly = input(false);
  readonly errorMessage = input<string | null>(null);
  readonly submitted = output<UpsertProductFiscalProfileRequest>();

  protected draft: UpsertProductFiscalProfileRequest = emptyProductProfile();

  ngOnChanges(): void {
    const profile = this.profile();
    this.draft = profile
      ? {
          internalCode: profile.internalCode,
          description: profile.description,
          satProductServiceCode: profile.satProductServiceCode,
          satUnitCode: profile.satUnitCode,
          taxObjectCode: profile.taxObjectCode,
          vatRate: profile.vatRate,
          defaultUnitText: profile.defaultUnitText,
          isActive: profile.isActive
        }
      : emptyProductProfile();
  }

  protected submitForm(): void {
    this.submitted.emit({
      ...this.draft,
      internalCode: this.draft.internalCode.trim(),
      description: this.draft.description.trim(),
      satProductServiceCode: this.draft.satProductServiceCode.trim(),
      satUnitCode: this.draft.satUnitCode.trim(),
      taxObjectCode: this.draft.taxObjectCode.trim(),
      defaultUnitText: this.draft.defaultUnitText?.trim() || null
    });
  }
}

function emptyProductProfile(): UpsertProductFiscalProfileRequest {
  return {
    internalCode: '',
    description: '',
    satProductServiceCode: '',
    satUnitCode: '',
    taxObjectCode: '',
    vatRate: 0,
    defaultUnitText: '',
    isActive: true
  };
}
