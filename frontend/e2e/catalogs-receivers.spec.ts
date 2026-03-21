import { expect, test } from '@playwright/test';
import { LoginPage } from './support/login-page';
import { mockCatalogReceiversBackend } from './support/mock-catalogs';

test('login then create receiver and find it in search results', async ({ page }) => {
  await mockCatalogReceiversBackend(page);

  const loginPage = new LoginPage(page);
  await loginPage.open();
  await loginPage.signIn('supervisor', 'Secret123!');
  await expect(page).toHaveURL(/\/app\/orders$/);

  await page.getByRole('link', { name: 'Catálogos' }).click();
  await expect(page).toHaveURL(/\/app\/catalogs$/);
  await page.getByRole('link', { name: 'Receptores fiscales' }).click();
  await expect(page).toHaveURL(/\/app\/catalogs\/receivers$/);

  await page.getByRole('button', { name: 'Nuevo receptor' }).click();
  await page.getByRole('textbox', { name: 'RFC' }).fill('CCC010101CCC');
  await page.getByRole('textbox', { name: 'Razón social' }).fill('Receiver Catalog UI');
  await page.getByRole('textbox', { name: 'Código de régimen fiscal' }).fill('601');
  await page.getByRole('textbox', { name: 'Uso CFDI predeterminado' }).fill('G03');
  await page.getByRole('textbox', { name: 'Código postal' }).fill('03100');
  await page.getByRole('button', { name: 'Crear receptor' }).click();

  await page.getByRole('textbox', { name: 'Buscar receptores' }).fill('CCC010101CCC');
  await page.getByRole('button', { name: 'Buscar' }).click();

  await expect(page.getByText('CCC010101CCC')).toBeVisible();
  await expect(page.getByText('Receiver Catalog UI')).toBeVisible();
});
