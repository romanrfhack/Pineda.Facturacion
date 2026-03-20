import type { Page } from '@playwright/test';

export async function mockFiscalEvidenceBackend(page: Page): Promise<void> {
  await page.route('**/api/auth/login', async (route) => {
    await route.fulfill({
      json: {
        outcome: 'Authenticated',
        isSuccess: true,
        token: 'test-token',
        expiresAtUtc: new Date().toISOString(),
        user: {
          id: 2,
          username: 'auditor',
          displayName: 'Audit Reader',
          roles: ['Auditor'],
          isAuthenticated: true
        }
      }
    });
  });

  await page.route('**/api/auth/me', async (route) => {
    await route.fulfill({
      json: {
        id: 2,
        username: 'auditor',
        displayName: 'Audit Reader',
        roles: ['Auditor'],
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

  await page.route('**/api/fiscal-documents/40', async (route) => {
    await route.fulfill({
      json: {
        id: 40,
        billingDocumentId: 30,
        issuerProfileId: 1,
        fiscalReceiverId: 9,
        status: 'Stamped',
        cfdiVersion: '4.0',
        documentType: 'I',
        issuedAtUtc: '2026-03-20T12:00:00Z',
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
        taxTotal: 0,
        total: 100,
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
            taxTotal: 0,
            total: 100,
            satProductServiceCode: '01010101',
            satUnitCode: 'H87',
            taxObjectCode: '02',
            vatRate: 0,
            unitText: 'Pieza'
          }
        ]
      }
    });
  });

  await page.route('**/api/fiscal-documents/40/stamp', async (route) => {
    await route.fulfill({
      json: {
        id: 11,
        fiscalDocumentId: 40,
        providerName: 'FacturaloPlus',
        providerOperation: 'stamp',
        providerTrackingId: 'TRACK-FISCAL-1',
        status: 'Stamped',
        uuid: 'UUID-FISCAL-1',
        stampedAtUtc: '2026-03-20T12:00:00Z',
        providerCode: '200',
        providerMessage: 'Stamped successfully',
        errorCode: null,
        errorMessage: null,
        xmlHash: 'XML-HASH-FISCAL',
        qrCodeTextOrUrl: 'https://sat.example/qr/fiscal',
        originalString: '||1.1|UUID-FISCAL-1||',
        createdAtUtc: '2026-03-20T12:00:00Z',
        updatedAtUtc: '2026-03-20T12:00:00Z'
      }
    });
  });

  await page.route('**/api/fiscal-documents/40/stamp/xml', async (route) => {
    await route.fulfill({
      contentType: 'application/xml',
      body: '<cfdi:Comprobante Version="4.0"><cfdi:Complemento /></cfdi:Comprobante>'
    });
  });

  await page.route('**/api/fiscal-documents/40/cancellation', async (route) => {
    await route.fulfill({ status: 404, json: { errorMessage: 'Not found' } });
  });
}
