import { TestBed } from '@angular/core/testing';
import { ProductFiscalProfileFormComponent } from './product-fiscal-profile-form.component';

describe('ProductFiscalProfileFormComponent', () => {
  it('renders backend validation message and initial field values', async () => {
    await TestBed.configureTestingModule({
      imports: [ProductFiscalProfileFormComponent]
    }).compileComponents();

    const fixture = TestBed.createComponent(ProductFiscalProfileFormComponent);
    fixture.componentRef.setInput('profile', {
      id: 1,
      internalCode: 'SKU-1',
      description: 'Product Uno',
      satProductServiceCode: '01010101',
      satUnitCode: 'H87',
      taxObjectCode: '02',
      vatRate: 0.16,
      defaultUnitText: 'Pieza',
      isActive: true,
      createdAtUtc: '2026-03-20T00:00:00Z',
      updatedAtUtc: '2026-03-20T00:00:00Z'
    });
    fixture.componentRef.setInput('errorMessage', 'SAT data is required.');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('SAT data is required.');
    expect(fixture.nativeElement.textContent).toContain('Internal code');
    expect(fixture.nativeElement.textContent).toContain('VAT rate');
  });
});
