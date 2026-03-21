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
  await page.getByRole('button', { name: 'Crear cuenta por cobrar' }).click();

  await expect(page.locator('app-accounts-receivable-card').getByText('Abierto')).toBeVisible();
  await expect(page.getByText('Pendiente100.00')).toBeVisible();

  await page.getByLabel('Monto').fill('40');
  await page.getByLabel('Referencia').fill('PAY-701');
  await page.getByRole('button', { name: 'Crear pago' }).click();

  await expect(page.getByText('Pago #701')).toBeVisible();
  await expect(page.getByText('Remanente 40 MXN')).toBeVisible();

  await page.getByLabel('Id de cuenta por cobrar').fill('601');
  await page.getByLabel('Monto aplicado').fill('40');
  await page.getByRole('button', { name: 'Aplicar pago' }).click();

  await expect(page.locator('app-accounts-receivable-card').getByText('Parcialmente pagado')).toBeVisible();
  await expect(page.getByText('Remanente 0 MXN')).toBeVisible();
  await expect(page.getByRole('cell', { name: '601', exact: true })).toBeVisible();
  await expect(page.getByRole('cell', { name: '40', exact: true })).toBeVisible();
  await expect(page.getByRole('cell', { name: '60', exact: true })).toBeVisible();
});
