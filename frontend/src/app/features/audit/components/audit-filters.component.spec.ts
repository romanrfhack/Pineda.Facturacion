import { TestBed } from '@angular/core/testing';
import { AuditFiltersComponent } from './audit-filters.component';

describe('AuditFiltersComponent', () => {
  it('emits cleared when filters are reset', async () => {
    await TestBed.configureTestingModule({
      imports: [AuditFiltersComponent]
    }).compileComponents();

    const fixture = TestBed.createComponent(AuditFiltersComponent);
    const cleared = vi.fn();
    fixture.componentInstance.cleared.subscribe(cleared);
    fixture.detectChanges();

    const clearButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find((button) => button.textContent?.includes('Limpiar')) as HTMLButtonElement;
    clearButton.click();

    expect(cleared).toHaveBeenCalled();
  });
});
