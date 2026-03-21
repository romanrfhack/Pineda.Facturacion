import { expect, test } from '@playwright/test';
import { mockSession } from './support/mock-operational-scenarios';

test('repeated billing-document creation reuses the existing document and continues to fiscal documents', async ({ page }) => {
  await mockSession(page, {
    username: 'supervisor',
    displayName: 'Supervisor',
    role: 'FiscalSupervisor'
  });

  await page.route('**/api/orders/LEG-7201/import', async (route) => {
    await route.fulfill({
      json: {
        outcome: 'Imported',
        isSuccess: true,
        isIdempotent: false,
        sourceSystem: 'legacy',
        sourceTable: 'pedidos',
        legacyOrderId: 'LEG-7201',
        sourceHash: 'hash-7201',
        legacyImportRecordId: 121,
        salesOrderId: 221,
        importStatus: 'Imported'
      }
    });
  });

  await page.route('**/api/sales-orders/221/billing-documents', async (route) => {
    await route.fulfill({
      status: 409,
      json: {
        outcome: 'Conflict',
        isSuccess: false,
        salesOrderId: 221,
        billingDocumentId: 321,
        billingDocumentStatus: 'Draft',
        errorMessage: 'Sales order already has a billing document'
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

  await page.route('**/api/billing-documents/321', async (route) => {
    await route.fulfill({
      json: {
        billingDocumentId: 321,
        salesOrderId: 221,
        legacyOrderId: 'LEG-7201',
        status: 'Draft',
        documentType: 'I',
        currencyCode: 'MXN',
        total: 100,
        createdAtUtc: '2026-03-20T12:00:00Z',
        fiscalDocumentId: null,
        fiscalDocumentStatus: null
      }
    });
  });

  await page.goto('/login');
  await page.getByLabel('Usuario').fill('supervisor');
  await page.getByLabel('Contraseña').fill('Secret123!');
  await page.getByRole('button', { name: 'Iniciar sesión' }).click();

  await page.getByLabel('Id de orden legada').fill('LEG-7201');
  await page.getByRole('button', { name: 'Importar orden' }).click();
  await page.getByRole('button', { name: 'Crear documento de facturación' }).click();

  await expect(page).toHaveURL(/\/app\/fiscal-documents\?billingDocumentId=321$/);
  await expect(page.getByText('Documento seleccionado')).toBeVisible();
  await expect(page.getByText('Documento #321')).toBeVisible();
});
