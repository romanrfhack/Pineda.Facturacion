import { expect, test } from '@playwright/test';
import { LoginPage } from '../support/login-page';
import { mockAccountsReceivableJourney } from '../support/mock-operational-scenarios';

test('operator creates an AR invoice then records and applies a payment', async ({ page }) => {
  await mockAccountsReceivableJourney(page);

  const loginPage = new LoginPage(page);
  await loginPage.open();
  await loginPage.signIn('operator', 'Secret123!');
  await expect(page).toHaveURL(/\/app\/orders$/);

  await page.goto('/app/accounts-receivable?fiscalDocumentId=401');
  await page.getByRole('button', { name: 'Create AR invoice' }).click();

  await expect(page.locator('app-accounts-receivable-card').getByText('Open')).toBeVisible();
  await expect(page.getByText('Outstanding100.00')).toBeVisible();

  await page.getByLabel('Amount').fill('40');
  await page.getByLabel('Reference').fill('PAY-701');
  await page.getByRole('button', { name: 'Create payment' }).click();

  await expect(page.getByText('Payment #701')).toBeVisible();
  await expect(page.getByText('Remaining 40 MXN')).toBeVisible();

  await page.getByLabel('AR invoice id').fill('601');
  await page.getByLabel('Applied amount').fill('40');
  await page.getByRole('button', { name: 'Apply payment' }).click();

  await expect(page.locator('app-accounts-receivable-card').getByText('Partiallypaid')).toBeVisible();
  await expect(page.getByText('Remaining 0 MXN')).toBeVisible();
  await expect(page.getByRole('cell', { name: '601', exact: true })).toBeVisible();
  await expect(page.getByRole('cell', { name: '40', exact: true })).toBeVisible();
  await expect(page.getByRole('cell', { name: '60', exact: true })).toBeVisible();
});
