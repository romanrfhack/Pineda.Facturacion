import { expect, test } from '@playwright/test';
import { LoginPage } from './support/login-page';
import { mockHappyPathBackend } from './support/mock-backend';

test('fiscal documents module opens the pending stamping inbox and links to existing detail flows', async ({ page }) => {
  await mockHappyPathBackend(page);
  await page.route('**/api/fiscal-documents/pending-stamping**', async (route) => {
    await route.fulfill({
      json: {
        page: 1,
        pageSize: 25,
        totalCount: 2,
        totalPages: 1,
        items: [
          {
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
            paymentFormSat: '99'
          },
          {
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
            paymentFormSat: null
          }
        ]
      }
    });
  });

  const loginPage = new LoginPage(page);
  await loginPage.open();
  await loginPage.signIn('supervisor', 'Secret123!');

  await page.goto('/app/fiscal-documents/open', { waitUntil: 'commit' });

  await expect(page.getByRole('heading', { name: 'Documentos abiertos pendientes de timbrar' })).toBeVisible();
  await expect(page.getByText('Cliente Uno', { exact: true })).toBeVisible();
  await expect(page.getByText('Listo para timbrar', { exact: true })).toBeVisible();
  await expect(page.getByText('Cliente Dos', { exact: true })).toBeVisible();
  await expect(page.getByText('Pendiente de preparar', { exact: true })).toBeVisible();

  const continueLinks = page.getByRole('link', { name: 'Continuar' });
  await expect(continueLinks).toHaveCount(2);
  await expect(continueLinks.nth(0)).toHaveAttribute('href', '/app/fiscal-documents/900');
  await expect(continueLinks.nth(1)).toHaveAttribute(
    'href',
    '/app/fiscal-documents?billingDocumentId=801'
  );
});
