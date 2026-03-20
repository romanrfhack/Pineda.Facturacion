import { expect, test } from '@playwright/test';
import { LoginPage } from './support/login-page';
import { mockCatalogReceiversBackend } from './support/mock-catalogs';

test('login then create receiver and find it in search results', async ({ page }) => {
  await mockCatalogReceiversBackend(page);

  const loginPage = new LoginPage(page);
  await loginPage.open();
  await loginPage.signIn('supervisor', 'Secret123!');
  await expect(page).toHaveURL(/\/app\/orders$/);

  await page.getByRole('link', { name: 'Catalogs' }).click();
  await expect(page).toHaveURL(/\/app\/catalogs$/);
  await page.getByRole('link', { name: 'Fiscal receivers' }).click();
  await expect(page).toHaveURL(/\/app\/catalogs\/receivers$/);

  await page.getByRole('button', { name: 'New receiver' }).click();
  await page.getByRole('textbox', { name: 'RFC' }).fill('CCC010101CCC');
  await page.getByRole('textbox', { name: 'Legal name' }).fill('Receiver Catalog UI');
  await page.getByRole('textbox', { name: 'Fiscal regime code' }).fill('601');
  await page.getByRole('textbox', { name: 'Default CFDI use' }).fill('G03');
  await page.getByRole('textbox', { name: 'Postal code' }).fill('03100');
  await page.getByRole('button', { name: 'Create receiver' }).click();

  await page.getByRole('textbox', { name: 'Search receivers' }).fill('CCC010101CCC');
  await page.getByRole('button', { name: 'Search' }).click();

  await expect(page.getByText('CCC010101CCC')).toBeVisible();
  await expect(page.getByText('Receiver Catalog UI')).toBeVisible();
});
