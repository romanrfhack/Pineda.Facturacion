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
  await expect(page.locator('app-fiscal-document-card').getByRole('heading', { name: 'Fiscal document #40' })).toBeVisible();
  await expect(page.getByText('UUID-FISCAL-1')).toBeVisible();

  await page.getByRole('button', { name: 'View XML' }).click();
  await expect(page.getByRole('heading', { name: 'Fiscal document XML' })).toBeVisible();
  await expect(page.getByText('<cfdi:Comprobante Version="4.0">')).toBeVisible();

  await page.getByRole('button', { name: 'Close' }).click();
  await expect(page.getByRole('heading', { name: 'Fiscal document XML' })).not.toBeVisible();
});
