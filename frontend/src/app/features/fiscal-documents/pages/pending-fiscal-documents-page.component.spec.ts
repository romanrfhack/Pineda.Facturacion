import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PermissionService } from '../../../core/auth/permission.service';
import { PendingFiscalDocumentListItemResponse } from '../models/pending-fiscal-documents.models';
import { PendingFiscalDocumentsPageComponent } from './pending-fiscal-documents-page.component';

describe('PendingFiscalDocumentsPageComponent', () => {
  const permissionServiceStub = {
    auditor: false,
    isReadOnlyAuditor: () => permissionServiceStub.auditor,
  };

  beforeEach(() => {
    permissionServiceStub.auditor = false;
    TestBed.configureTestingModule({
      imports: [PendingFiscalDocumentsPageComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: PermissionService, useValue: permissionServiceStub },
      ],
    });
  });

  it('renders open fiscal work and routes each row to the correct existing detail flow', async () => {
    const fixture = TestBed.createComponent(PendingFiscalDocumentsPageComponent);
    const httpTesting = TestBed.inject(HttpTestingController);

    fixture.detectChanges();
    flushInbox(httpTesting, [readyItem(), pendingPreparationItem()]);

    await fixture.whenStable();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Documentos abiertos pendientes de timbrar');
    expect(text).toContain('Cliente Uno');
    expect(text).toContain('Listo para timbrar');
    expect(text).toContain('Cliente Dos');
    expect(text).toContain('Pendiente de preparar');

    const continueLinks = Array.from(
      fixture.nativeElement.querySelectorAll('a.button-link.small') as NodeListOf<HTMLAnchorElement>,
    );
    expect(continueLinks).toHaveLength(2);
    expect(continueLinks[0].getAttribute('href')).toBe('/app/fiscal-documents/900');
    expect(continueLinks[1].getAttribute('href')).toBe(
      '/app/fiscal-documents?billingDocumentId=801',
    );
    expect(fixture.nativeElement.textContent).toContain('Búsqueda manual');

    httpTesting.verify();
  });

  it('shows the inbox as read only and hides all operational navigation for auditors', async () => {
    permissionServiceStub.auditor = true;
    const fixture = TestBed.createComponent(PendingFiscalDocumentsPageComponent);
    const httpTesting = TestBed.inject(HttpTestingController);

    fixture.detectChanges();
    flushInbox(httpTesting, [readyItem(), pendingPreparationItem()]);

    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('a.button-link')).toHaveLength(0);
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Vista de solo consulta');
    expect(text).toContain('Solo consulta');
    expect(text).not.toContain('Búsqueda manual');
    httpTesting.verify();
  });
});

function flushInbox(
  httpTesting: HttpTestingController,
  items: PendingFiscalDocumentListItemResponse[],
): void {
  const request = httpTesting.expectOne(
    '/api/fiscal-documents/pending-stamping?page=1&pageSize=25&workStatus=All&sort=LastActivityDesc',
  );
  request.flush({
    page: 1,
    pageSize: 25,
    totalCount: items.length,
    totalPages: items.length ? 1 : 0,
    items,
  });
}

function readyItem(): PendingFiscalDocumentListItemResponse {
  return {
    billingDocumentId: 800,
    fiscalDocumentId: 900,
    billingDocumentStatus: 'Draft',
    fiscalDocumentStatus: 'ReadyForStamping',
    workStatus: 'ReadyForStamping',
    workStatusLabel: 'Listo para timbrar',
    requiresAttention: false,
    documentType: 'I',
    series: 'A',
    folio: '123',
    receiverName: 'Cliente Uno',
    receiverRfc: 'AAA010101AAA',
    currencyCode: 'MXN',
    total: 1160,
    associatedOrderCount: 2,
    itemCount: 5,
    orderReferences: ['LEG-1001', 'LEG-1002'],
    createdAtUtc: '2026-08-01T12:00:00Z',
    lastActivityAtUtc: '2026-08-07T12:00:00Z',
    fiscalPreparedAtUtc: '2026-08-07T11:00:00Z',
    paymentMethodSat: 'PPD',
    paymentFormSat: '99',
  };
}

function pendingPreparationItem(): PendingFiscalDocumentListItemResponse {
  return {
    billingDocumentId: 801,
    fiscalDocumentId: null,
    billingDocumentStatus: 'Draft',
    fiscalDocumentStatus: null,
    workStatus: 'PendingPreparation',
    workStatusLabel: 'Pendiente de preparar',
    requiresAttention: false,
    documentType: 'I',
    series: null,
    folio: null,
    receiverName: 'Cliente Dos',
    receiverRfc: 'BBB010101BBB',
    currencyCode: 'MXN',
    total: 580,
    associatedOrderCount: 1,
    itemCount: 2,
    orderReferences: ['LEG-2001'],
    createdAtUtc: '2026-08-06T12:00:00Z',
    lastActivityAtUtc: '2026-08-06T12:00:00Z',
    fiscalPreparedAtUtc: null,
    paymentMethodSat: null,
    paymentFormSat: null,
  };
}
