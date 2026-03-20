import { expect, test } from '@playwright/test';
import { LoginPage } from '../support/login-page';
import { mockInvoiceStampingJourney } from '../support/mock-operational-scenarios';

test('supervisor completes invoice stamping journey and sees stamp evidence', async ({ page }) => {
  await mockInvoiceStampingJourney(page);

  const loginPage = new LoginPage(page);
  await loginPage.open();
  await loginPage.signIn('supervisor', 'Secret123!');
  await expect(page).toHaveURL(/\/app\/orders$/);

  await page.getByLabel('Legacy order id').fill('LEG-7101');
  await page.getByRole('button', { name: 'Import order' }).click();
  await expect(page.getByText('Legacy order LEG-7101')).toBeVisible();

  await page.getByRole('button', { name: 'Create billing document' }).click();
  await page.getByRole('link', { name: 'Continue to fiscal preparation' }).click();

  await expect(page).toHaveURL(/\/app\/fiscal-documents\?billingDocumentId=301$/);
  await page.getByRole('button', { name: 'Search' }).click();
  await page.locator('select[name="selectedReceiverId"]').selectOption({ label: 'BBB010101BBB · Receiver One' });
  await page.getByRole('button', { name: 'Prepare fiscal document' }).click();

  await expect(page.locator('app-fiscal-document-card')).toContainText('Readyforstamping');
  await expect(page.getByText('No stamp evidence is available yet')).toBeVisible();

  await page.getByRole('button', { name: 'Stamp' }).click();

  await expect(page.locator('app-fiscal-document-card').getByText('Stamped')).toBeVisible();
  await expect(page.getByText('UUID-FISCAL-7101')).toBeVisible();
  await expect(page.getByText('FacturaloPlus')).toBeVisible();
  await expect(page.getByText('TRACK-FISCAL-7101')).toBeVisible();
});
