import { expect, test } from '@playwright/test';
import { LoginPage } from './support/login-page';
import { mockHappyPathBackend } from './support/mock-backend';

test('login then import order then create billing then open fiscal preparation', async ({ page }) => {
  await mockHappyPathBackend(page);

  const loginPage = new LoginPage(page);
  await loginPage.open();
  await loginPage.signIn('supervisor', 'Secret123!');
  await expect(page).toHaveURL(/\/app\/orders$/);

  await page.getByLabel('Legacy order id').fill('LEG-7001');
  await page.getByRole('button', { name: 'Import order' }).click();
  await expect(page.getByText('Legacy order LEG-7001')).toBeVisible();

  await page.getByRole('button', { name: 'Create billing document' }).click();
  await expect(page.getByRole('link', { name: 'Continue to fiscal preparation' })).toBeVisible();

  await page.getByRole('link', { name: 'Continue to fiscal preparation' }).click();
  await expect(page).toHaveURL(/\/app\/fiscal-documents/);
  await page.getByRole('button', { name: 'Search' }).click();
  await page.locator('select[name="selectedReceiverId"]').selectOption({ label: 'BBB010101BBB · Receiver One' });
  await page.getByRole('button', { name: 'Prepare fiscal document' }).click();
  await expect(page.getByText('Fiscal document #40')).toBeVisible();
});
