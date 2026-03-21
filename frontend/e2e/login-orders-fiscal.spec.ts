import { expect, test } from '@playwright/test';
import { LoginPage } from './support/login-page';
import { mockHappyPathBackend } from './support/mock-backend';

test('login then import order then create billing then open fiscal preparation', async ({ page }) => {
  await mockHappyPathBackend(page);

  const loginPage = new LoginPage(page);
  await loginPage.open();
  await loginPage.signIn('supervisor', 'Secret123!');
  await expect(page).toHaveURL(/\/app\/orders$/);

  await page.getByLabel('Id de orden legada').fill('LEG-7001');
  await page.getByRole('button', { name: 'Importar orden' }).click();
  await expect(page.getByText('Orden legada LEG-7001')).toBeVisible();

  await page.getByRole('button', { name: 'Crear documento de facturación' }).click();
  await expect(page.getByRole('link', { name: 'Continuar a preparación fiscal' })).toBeVisible();

  await page.getByRole('link', { name: 'Continuar a preparación fiscal' }).click();
  await expect(page).toHaveURL(/\/app\/fiscal-documents/);
  await page.getByRole('button', { name: 'Buscar' }).click();
  await page.locator('select[name="selectedReceiverId"]').selectOption({ label: 'BBB010101BBB · Receiver One' });
  await page.getByRole('button', { name: 'Preparar documento fiscal' }).click();
  await expect(page.getByText('Documento fiscal #40')).toBeVisible();
});
