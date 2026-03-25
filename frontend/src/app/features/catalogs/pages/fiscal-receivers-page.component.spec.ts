import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { FiscalReceiversPageComponent } from './fiscal-receivers-page.component';
import { FiscalReceiversApiService } from '../infrastructure/fiscal-receivers-api.service';
import { PermissionService } from '../../../core/auth/permission.service';
import { FeedbackService } from '../../../core/ui/feedback.service';

describe('FiscalReceiversPageComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FiscalReceiversPageComponent],
      providers: [
        {
          provide: FiscalReceiversApiService,
          useValue: {
            search: vi.fn().mockReturnValue(of([
              {
                id: 1,
                rfc: 'AAA010101AAA',
                legalName: 'Receiver Uno',
                postalCode: '01000',
                fiscalRegimeCode: '601',
                cfdiUseCodeDefault: 'G03',
                isActive: true
              }
            ])),
            getByRfc: vi.fn(),
            getSatCatalog: vi.fn().mockReturnValue(of({
              regimenFiscal: [
                { code: '601', description: 'General de Ley Personas Morales' }
              ],
              usoCfdi: [
                { code: 'G03', description: 'Gastos en general' }
              ],
              byRegimenFiscal: [
                {
                  code: '601',
                  description: 'General de Ley Personas Morales',
                  allowedUsoCfdi: [{ code: 'G03', description: 'Gastos en general' }]
                }
              ]
            })),
            create: vi.fn(),
            update: vi.fn()
          }
        },
        {
          provide: PermissionService,
          useValue: {
            canWriteMasterData: vi.fn().mockReturnValue(true)
          }
        },
        {
          provide: FeedbackService,
          useValue: { show: vi.fn() }
        }
      ]
    }).compileComponents();
  });

  it('renders searched receivers in the list', async () => {
    const fixture = TestBed.createComponent(FiscalReceiversPageComponent);
    fixture.componentInstance['query'] = 'AAA';

    await fixture.componentInstance['search']();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('AAA010101AAA');
    expect(fixture.nativeElement.textContent).toContain('Receiver Uno');
  });
});
