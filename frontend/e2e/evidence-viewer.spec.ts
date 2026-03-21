import { expect, test } from '@playwright/test';
import { LoginPage } from './support/login-page';
import { mockFiscalEvidenceBackend } from './support/mock-evidence';

test('login then inspect stamped fiscal evidence and open XML', async ({ page }) => {
  await mockFiscalEvidenceBackend(page);

  const loginPage = new LoginPage(page);
  await loginPage.open();
  await loginPage.signIn('auditor', 'Secret123!');
  await expect(page).toHaveURL(/\/app\/audit$/);

  await page.goto('/app/fiscal-documents/40');
  await expect(page.locator('app-fiscal-document-card').getByRole('heading', { name: 'Documento fiscal #40' })).toBeVisible();
  await expect(page.getByText('UUID-FISCAL-1')).toBeVisible();

  await page.getByRole('button', { name: 'Ver XML' }).click();
  await expect(page.getByRole('heading', { name: 'XML del documento fiscal' })).toBeVisible();
  await expect(page.locator('pre')).toContainText('cfdi:Comprobante');

  await page.getByRole('button', { name: 'Cerrar', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'XML del documento fiscal' })).not.toBeVisible();
});
