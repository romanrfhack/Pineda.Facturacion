import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { OrdersOperationsPageComponent } from './orders-operations-page.component';
import { OrdersApiService } from '../infrastructure/orders-api.service';
import { FeedbackService } from '../../../core/ui/feedback.service';

describe('OrdersOperationsPageComponent', () => {
  function createApi() {
    return {
      importLegacyOrder: vi.fn().mockReturnValue(of({
        outcome: 'Imported',
        isSuccess: true,
        isIdempotent: false,
        sourceSystem: 'legacy',
        sourceTable: 'pedidos',
        legacyOrderId: 'LEG-1001',
        sourceHash: 'hash',
        legacyImportRecordId: 10,
        salesOrderId: 20,
        importStatus: 'Imported'
      })),
      createBillingDocument: vi.fn().mockReturnValue(of({
        outcome: 'Created',
        isSuccess: true,
        salesOrderId: 20,
        billingDocumentId: 30,
        billingDocumentStatus: 'Draft'
      }))
    };
  }

  async function configure(apiOverrides?: Partial<ReturnType<typeof createApi>>) {
    const api = { ...createApi(), ...apiOverrides };
    const feedback = { show: vi.fn() };
    const router = { navigate: vi.fn().mockResolvedValue(true) };

    await TestBed.configureTestingModule({
      imports: [OrdersOperationsPageComponent],
      providers: [
        { provide: OrdersApiService, useValue: api },
        { provide: FeedbackService, useValue: feedback },
        { provide: Router, useValue: router }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(OrdersOperationsPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    return { fixture, api, feedback, router };
  }

  it('navigates to fiscal documents after creating a billing document', async () => {
    const { fixture, router, feedback } = await configure();

    fixture.componentInstance['importedOrder'].set({
      outcome: 'Imported',
      isSuccess: true,
      isIdempotent: false,
      sourceSystem: 'legacy',
      sourceTable: 'pedidos',
      legacyOrderId: 'LEG-1001',
      sourceHash: 'hash',
      legacyImportRecordId: 10,
      salesOrderId: 20,
      importStatus: 'Imported'
    });

    await fixture.componentInstance['createBillingDocument']();

    expect(feedback.show).toHaveBeenCalledWith('success', 'Documento de facturación creado.');
    expect(router.navigate).toHaveBeenCalledWith(['/app/fiscal-documents'], { queryParams: { billingDocumentId: 30 } });
  });

  it('reuses the existing billing document returned by a conflict response', async () => {
    const { fixture, router, feedback } = await configure({
      createBillingDocument: vi.fn().mockReturnValue(throwError(() =>
        new HttpErrorResponse({
          status: 409,
          error: {
            outcome: 'Conflict',
            isSuccess: false,
            salesOrderId: 20,
            billingDocumentId: 31,
            billingDocumentStatus: 'Draft',
            errorMessage: 'Sales order already has a billing document'
          }
        })))
    });

    fixture.componentInstance['importedOrder'].set({
      outcome: 'Imported',
      isSuccess: true,
      isIdempotent: false,
      sourceSystem: 'legacy',
      sourceTable: 'pedidos',
      legacyOrderId: 'LEG-1001',
      sourceHash: 'hash',
      legacyImportRecordId: 10,
      salesOrderId: 20,
      importStatus: 'Imported'
    });

    await fixture.componentInstance['createBillingDocument']();

    expect(feedback.show).toHaveBeenCalledWith('info', 'La orden ya cuenta con un documento de facturación. Se abrirá el documento existente.');
    expect(router.navigate).toHaveBeenCalledWith(['/app/fiscal-documents'], { queryParams: { billingDocumentId: 31 } });
    expect(fixture.componentInstance['billingDocument']()?.outcome).toBe('Conflict');
  });
});
