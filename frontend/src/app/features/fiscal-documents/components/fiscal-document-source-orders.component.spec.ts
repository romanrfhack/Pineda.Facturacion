import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { FiscalDocumentsApiService } from '../infrastructure/fiscal-documents-api.service';
import { FiscalDocumentSourceOrdersComponent } from './fiscal-document-source-orders.component';

function createFiscalDocument() {
  return {
    id: 40,
    billingDocumentId: 30,
    issuerProfileId: 1,
    fiscalReceiverId: 9,
    status: 'Stamped',
    cfdiVersion: '4.0',
    documentType: 'I',
    series: 'A',
    folio: '31787',
    issuedAtUtc: '2026-03-24T12:00:00Z',
    currencyCode: 'MXN',
    exchangeRate: 1,
    paymentMethodSat: 'PUE',
    paymentFormSat: '03',
    paymentCondition: 'CONTADO',
    isCreditSale: false,
    creditDays: null,
    issuerRfc: 'AAA010101AAA',
    issuerLegalName: 'Issuer SA',
    issuerFiscalRegimeCode: '601',
    issuerPostalCode: '01000',
    pacEnvironment: 'Production',
    hasCertificateReference: true,
    hasPrivateKeyReference: true,
    hasPrivateKeyPasswordReference: true,
    receiverRfc: 'BBB010101BBB',
    receiverLegalName: 'Receiver One',
    receiverFiscalRegimeCode: '601',
    receiverCfdiUseCode: 'G03',
    receiverPostalCode: '02000',
    receiverCountryCode: 'MX',
    receiverForeignTaxRegistration: null,
    subtotal: 350,
    discountTotal: 0,
    taxTotal: 56,
    total: 406,
    specialFields: [],
    items: [
      {
        id: 1,
        fiscalDocumentId: 40,
        lineNumber: 1,
        billingDocumentItemId: 101,
        internalCode: 'A-100',
        description: 'Artículo principal',
        quantity: 1,
        unitPrice: 100,
        discountAmount: 0,
        subtotal: 100,
        taxTotal: 16,
        total: 116,
        satProductServiceCode: '25170000',
        satUnitCode: 'H87',
        taxObjectCode: '02',
        vatRate: 0.16,
      },
      {
        id: 2,
        fiscalDocumentId: 40,
        lineNumber: 2,
        billingDocumentItemId: 102,
        internalCode: 'A-200',
        description: 'Artículo secundario',
        quantity: 1,
        unitPrice: 50,
        discountAmount: 0,
        subtotal: 50,
        taxTotal: 8,
        total: 58,
        satProductServiceCode: '25170000',
        satUnitCode: 'H87',
        taxObjectCode: '02',
        vatRate: 0.16,
      },
      {
        id: 3,
        fiscalDocumentId: 40,
        lineNumber: 3,
        billingDocumentItemId: 103,
        internalCode: 'B-100',
        description: 'Artículo de orden adicional',
        quantity: 2,
        unitPrice: 100,
        discountAmount: 0,
        subtotal: 200,
        taxTotal: 32,
        total: 232,
        satProductServiceCode: '25170000',
        satUnitCode: 'H87',
        taxObjectCode: '02',
        vatRate: 0.16,
      },
    ],
  };
}

function createBillingDocument() {
  return {
    billingDocumentId: 30,
    salesOrderId: 201,
    legacyOrderId: 'ORD-1001',
    status: 'Issued',
    documentType: 'I',
    currencyCode: 'MXN',
    total: 406,
    createdAtUtc: '2026-03-24T11:00:00Z',
    fiscalDocumentId: 40,
    fiscalDocumentStatus: 'Stamped',
    associatedOrders: [
      {
        salesOrderId: 201,
        legacyOrderId: 'ORD-1001',
        customerName: 'Cliente Uno',
        total: 174,
        isPrimary: true,
      },
      {
        salesOrderId: 202,
        legacyOrderId: 'ORD-1002',
        customerName: 'Cliente Uno',
        total: 300,
        isPrimary: false,
      },
      {
        salesOrderId: 203,
        legacyOrderId: 'ORD-SIN-PARTIDAS',
        customerName: 'Cliente Uno',
        total: 99,
        isPrimary: false,
      },
    ],
    items: [
      {
        billingDocumentItemId: 101,
        salesOrderId: 201,
        salesOrderItemId: 1001,
        sourceBillingDocumentItemRemovalId: null,
        sourceSalesOrderLineNumber: 1,
        sourceLegacyOrderId: 'ORD-1001',
        lineNumber: 1,
        productInternalCode: 'A-100',
        description: 'Artículo principal',
        quantity: 1,
        total: 116,
      },
      {
        billingDocumentItemId: 102,
        salesOrderId: 201,
        salesOrderItemId: 1002,
        sourceBillingDocumentItemRemovalId: null,
        sourceSalesOrderLineNumber: 2,
        sourceLegacyOrderId: 'ORD-1001',
        lineNumber: 2,
        productInternalCode: 'A-200',
        description: 'Artículo secundario',
        quantity: 1,
        total: 58,
      },
      {
        billingDocumentItemId: 103,
        salesOrderId: 202,
        salesOrderItemId: 2001,
        sourceBillingDocumentItemRemovalId: null,
        sourceSalesOrderLineNumber: 4,
        sourceLegacyOrderId: 'ORD-1002',
        lineNumber: 3,
        productInternalCode: 'B-100',
        description: 'Artículo de orden adicional',
        quantity: 2,
        total: 232,
      },
      {
        billingDocumentItemId: 104,
        salesOrderId: 203,
        salesOrderItemId: 3001,
        sourceBillingDocumentItemRemovalId: null,
        sourceSalesOrderLineNumber: 1,
        sourceLegacyOrderId: 'ORD-SIN-PARTIDAS',
        lineNumber: 4,
        productInternalCode: 'X-100',
        description: 'Partida que no pertenece al snapshot fiscal',
        quantity: 1,
        total: 99,
      },
    ],
    removedItems: [],
  };
}

async function configure(getBillingDocumentById = vi.fn().mockReturnValue(of(createBillingDocument()))) {
  await TestBed.configureTestingModule({
    imports: [FiscalDocumentSourceOrdersComponent],
    providers: [
      {
        provide: FiscalDocumentsApiService,
        useValue: { getBillingDocumentById },
      },
    ],
  }).compileComponents();

  const fixture = TestBed.createComponent(FiscalDocumentSourceOrdersComponent);
  fixture.componentRef.setInput('fiscalDocument', createFiscalDocument());
  fixture.detectChanges();
  await fixture.whenStable();
  await Promise.resolve();
  fixture.detectChanges();

  return { fixture, getBillingDocumentById };
}

describe('FiscalDocumentSourceOrdersComponent', () => {
  it('groups the fiscal snapshot items by their imported source order', async () => {
    const { fixture, getBillingDocumentById } = await configure();
    const text = fixture.nativeElement.textContent as string;
    const orderCards = fixture.nativeElement.querySelectorAll('.order-card');

    expect(getBillingDocumentById).toHaveBeenCalledWith(30);
    expect(orderCards).toHaveLength(2);
    expect(text).toContain('Órdenes incluidas en el CFDI');
    expect(text).toContain('ORD-1001');
    expect(text).toContain('ORD-1002');
    expect(text).toContain('Cliente Uno');
    expect(text).toContain('174.00 MXN');
    expect(text).toContain('232.00 MXN');
    expect(text).toContain('Artículo de orden adicional');
  });

  it('excludes billing items and associated orders that are not part of the fiscal snapshot', async () => {
    const { fixture } = await configure();
    const text = fixture.nativeElement.textContent as string;

    expect(text).not.toContain('ORD-SIN-PARTIDAS');
    expect(text).not.toContain('Partida que no pertenece al snapshot fiscal');
  });

  it('keeps the CFDI detail usable when the order lookup fails and supports retry', async () => {
    const getBillingDocumentById = vi
      .fn()
      .mockReturnValueOnce(throwError(() => ({ status: 500 })))
      .mockReturnValueOnce(of(createBillingDocument()));
    const { fixture } = await configure(getBillingDocumentById);

    expect(fixture.nativeElement.textContent).toContain(
      'No fue posible cargar las órdenes incluidas en el CFDI.',
    );

    const retryButton = fixture.nativeElement.querySelector('.retry-button') as HTMLButtonElement;
    retryButton.click();
    fixture.detectChanges();
    await fixture.whenStable();
    await Promise.resolve();
    fixture.detectChanges();

    expect(getBillingDocumentById).toHaveBeenCalledTimes(2);
    expect(fixture.nativeElement.textContent).toContain('ORD-1001');
  });
});
