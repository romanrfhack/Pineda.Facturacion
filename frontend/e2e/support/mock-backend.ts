import type { Page } from '@playwright/test';

export async function mockHappyPathBackend(page: Page): Promise<void> {
  await page.route('**/api/fiscal/receivers/sat-catalogs', async (route) => {
    await route.fulfill({
      json: {
        regimenFiscal: [
          { code: '601', description: 'General de Ley Personas Morales' }
        ],
        usoCfdi: [
          { code: 'G03', description: 'Gastos en general' }
        ],
        paymentMethods: [
          { code: 'PUE', description: 'Pago en una sola exhibición' },
          { code: 'PPD', description: 'Pago en parcialidades o diferido' }
        ],
        paymentForms: [
          { code: '03', description: 'Transferencia electrónica de fondos' },
          { code: '28', description: 'Tarjeta de débito' },
          { code: '99', description: 'Por definir' }
        ],
        byRegimenFiscal: [
          {
            code: '601',
            description: 'General de Ley Personas Morales',
            allowedUsoCfdi: [{ code: 'G03', description: 'Gastos en general' }]
          }
        ]
      }
    });
  });

  await page.route('**/api/auth/login', async (route) => {
    await route.fulfill({
      json: {
        outcome: 'Authenticated',
        isSuccess: true,
        token: 'test-token',
        expiresAtUtc: new Date().toISOString(),
        user: {
          id: 1,
          username: 'supervisor',
          displayName: 'Supervisor',
          roles: ['FiscalSupervisor'],
          isAuthenticated: true
        }
      }
    });
  });

  await page.route('**/api/auth/me', async (route) => {
    await route.fulfill({
      json: {
        id: 1,
        username: 'supervisor',
        displayName: 'Supervisor',
        roles: ['FiscalSupervisor'],
        isAuthenticated: true
      }
    });
  });

  await page.route('**/api/fiscal/issuer-profile/active', async (route) => {
    await route.fulfill({
      json: {
        id: 1,
        legalName: 'Issuer SA',
        rfc: 'AAA010101AAA',
        fiscalRegimeCode: '601',
        postalCode: '01000',
        cfdiVersion: '4.0',
        hasCertificateReference: true,
        hasPrivateKeyReference: true,
        hasPrivateKeyPasswordReference: true,
        pacEnvironment: 'Sandbox',
        isActive: true
      }
    });
  });

  await page.route('**/api/fiscal/receivers/search**', async (route) => {
    await route.fulfill({
      json: [
        {
          id: 9,
          rfc: 'BBB010101BBB',
          legalName: 'Receiver One',
          postalCode: '02000',
          fiscalRegimeCode: '601',
          cfdiUseCodeDefault: 'G03',
          isActive: true
        }
      ]
    });
  });

  await page.route('**/api/fiscal/receivers/by-rfc/BBB010101BBB', async (route) => {
    await route.fulfill({
      json: {
        id: 9,
        rfc: 'BBB010101BBB',
        legalName: 'Receiver One',
        postalCode: '02000',
        fiscalRegimeCode: '601',
        cfdiUseCodeDefault: 'G03',
        countryCode: 'MX',
        foreignTaxRegistration: null,
        email: 'receiver.one@example.com',
        phone: '5550000001',
        searchAlias: 'Receiver One',
        isActive: true,
        specialFields: []
      }
    });
  });

  await page.route('**/api/orders/legacy**', async (route) => {
    await route.fulfill({
      json: {
        isSuccess: true,
        totalCount: 1,
        totalPages: 1,
        page: 1,
        pageSize: 10,
        items: [
          {
            legacyOrderId: 'LEG-7001',
            orderDateUtc: new Date().toISOString(),
            customerName: 'Receiver One',
            total: 116,
            legacyOrderType: 'F',
            isImported: false,
            salesOrderId: null,
            billingDocumentId: null,
            billingDocumentStatus: null,
            fiscalDocumentId: null,
            fiscalDocumentStatus: null,
            importStatus: null
          }
        ]
      }
    });
  });

  await page.route('**/api/orders/LEG-7001/import', async (route) => {
    await route.fulfill({
      json: {
        outcome: 'Imported',
        isSuccess: true,
        isIdempotent: false,
        sourceSystem: 'legacy',
        sourceTable: 'pedidos',
        legacyOrderId: 'LEG-7001',
        sourceHash: 'hash',
        legacyImportRecordId: 10,
        salesOrderId: 20,
        importStatus: 'Imported'
      }
    });
  });

  await page.route('**/api/sales-orders/20/billing-documents', async (route) => {
    await route.fulfill({
      json: {
        outcome: 'Created',
        isSuccess: true,
        salesOrderId: 20,
        billingDocumentId: 30,
        billingDocumentStatus: 'Draft'
      }
    });
  });

  await page.route('**/api/billing-documents/30', async (route) => {
    await route.fulfill({
      json: {
        billingDocumentId: 30,
        salesOrderId: 20,
        legacyOrderId: 'LEG-7001',
        status: 'Draft',
        documentType: 'I',
        currencyCode: 'MXN',
        total: 116,
        createdAtUtc: new Date().toISOString(),
        fiscalDocumentId: null,
        fiscalDocumentStatus: null
      }
    });
  });

  await page.route('**/api/billing-documents/30/fiscal-documents', async (route) => {
    await route.fulfill({
      json: {
        outcome: 'Created',
        isSuccess: true,
        billingDocumentId: 30,
        fiscalDocumentId: 40,
        status: 'ReadyForStamping'
      }
    });
  });

  await page.route('**/api/fiscal-documents/40', async (route) => {
    await route.fulfill({
      json: {
        id: 40,
        billingDocumentId: 30,
        issuerProfileId: 1,
        fiscalReceiverId: 9,
        status: 'ReadyForStamping',
        cfdiVersion: '4.0',
        documentType: 'I',
        issuedAtUtc: new Date().toISOString(),
        currencyCode: 'MXN',
        exchangeRate: 1,
        paymentMethodSat: 'PPD',
        paymentFormSat: '99',
        paymentCondition: 'CREDITO',
        isCreditSale: true,
        creditDays: 7,
        issuerRfc: 'AAA010101AAA',
        issuerLegalName: 'Issuer SA',
        issuerFiscalRegimeCode: '601',
        issuerPostalCode: '01000',
        pacEnvironment: 'Sandbox',
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
        subtotal: 100,
        discountTotal: 0,
        taxTotal: 16,
        total: 116,
        items: [
          {
            id: 400,
            fiscalDocumentId: 40,
            lineNumber: 1,
            billingDocumentItemId: 300,
            internalCode: 'SKU-1',
            description: 'Product SKU-1',
            quantity: 1,
            unitPrice: 100,
            discountAmount: 0,
            subtotal: 100,
            taxTotal: 16,
            total: 116,
            satProductServiceCode: '01010101',
            satUnitCode: 'H87',
            taxObjectCode: '02',
            vatRate: 0.16,
            unitText: 'Pieza'
          }
        ]
      }
    });
  });
}

export async function mockFiscalPreparationFlow(page: Page): Promise<{ getLastPreparePayload: () => Record<string, unknown> | null }> {
  let lastPreparePayload: Record<string, unknown> | null = null;

  await mockHappyPathBackend(page);

  await page.route('**/api/billing-documents/30/fiscal-documents', async (route) => {
    lastPreparePayload = route.request().postDataJSON() as Record<string, unknown>;
    await route.fulfill({
      json: {
        outcome: 'Created',
        isSuccess: true,
        billingDocumentId: 30,
        fiscalDocumentId: 40,
        status: 'ReadyForStamping'
      }
    });
  });

  await page.route('**/api/fiscal-documents/40', async (route) => {
    const paymentMethodSat = typeof lastPreparePayload?.paymentMethodSat === 'string' ? lastPreparePayload.paymentMethodSat : 'PUE';
    const paymentFormSat = typeof lastPreparePayload?.paymentFormSat === 'string' ? lastPreparePayload.paymentFormSat : '03';
    const paymentCondition = typeof lastPreparePayload?.paymentCondition === 'string' ? lastPreparePayload.paymentCondition : 'Contado';
    const isCreditSale = lastPreparePayload?.isCreditSale === true;
    const creditDays = typeof lastPreparePayload?.creditDays === 'number' ? lastPreparePayload.creditDays : null;

    await route.fulfill({
      json: {
        id: 40,
        billingDocumentId: 30,
        issuerProfileId: 1,
        fiscalReceiverId: 9,
        status: 'ReadyForStamping',
        cfdiVersion: '4.0',
        documentType: 'I',
        issuedAtUtc: new Date().toISOString(),
        currencyCode: 'MXN',
        exchangeRate: 1,
        paymentMethodSat,
        paymentFormSat,
        paymentCondition,
        isCreditSale,
        creditDays,
        issuerRfc: 'AAA010101AAA',
        issuerLegalName: 'Issuer SA',
        issuerFiscalRegimeCode: '601',
        issuerPostalCode: '01000',
        pacEnvironment: 'Sandbox',
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
        subtotal: 100,
        discountTotal: 0,
        taxTotal: 16,
        total: 116,
        items: [
          {
            id: 400,
            fiscalDocumentId: 40,
            lineNumber: 1,
            billingDocumentItemId: 300,
            internalCode: 'SKU-1',
            description: 'Product SKU-1',
            quantity: 1,
            unitPrice: 100,
            discountAmount: 0,
            subtotal: 100,
            taxTotal: 16,
            total: 116,
            satProductServiceCode: '01010101',
            satUnitCode: 'H87',
            taxObjectCode: '02',
            vatRate: 0.16,
            unitText: 'Pieza'
          }
        ]
      }
    });
  });

  await page.route('**/api/fiscal-documents/40/stamp', async (route) => {
    await route.fulfill({ status: 404, json: {} });
  });

  await page.route('**/api/fiscal-documents/40/cancellation', async (route) => {
    await route.fulfill({ status: 404, json: {} });
  });

  return {
    getLastPreparePayload: () => lastPreparePayload
  };
}
