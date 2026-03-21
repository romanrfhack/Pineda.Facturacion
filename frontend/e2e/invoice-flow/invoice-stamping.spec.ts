import { expect, test } from '@playwright/test';
import { LoginPage } from '../support/login-page';
import { mockInvoiceStampingJourney } from '../support/mock-operational-scenarios';

test('supervisor completes invoice stamping journey and sees stamp evidence', async ({ page }) => {
  await mockInvoiceStampingJourney(page);

  const loginPage = new LoginPage(page);
  await loginPage.open();
  await loginPage.signIn('supervisor', 'Secret123!');
  await expect(page).toHaveURL(/\/app\/orders$/);

  await page.getByLabel('Id de orden legada').fill('LEG-7101');
  await page.getByRole('button', { name: 'Importar orden' }).click();
  await expect(page.getByText('Orden legada LEG-7101')).toBeVisible();

  await page.getByRole('button', { name: 'Crear documento de facturación' }).click();
  await page.getByRole('link', { name: 'Continuar a preparación fiscal' }).click();

  await expect(page).toHaveURL(/\/app\/fiscal-documents\?billingDocumentId=301$/);
  await page.getByRole('textbox', { name: 'Buscar receptor' }).fill('BBB010101BBB');
  await expect(page.getByRole('button', { name: 'BBB010101BBB Receiver One Código postal 02000' })).toBeVisible();
  await page.getByRole('button', { name: 'BBB010101BBB Receiver One Código postal 02000' }).click();
  await page.getByRole('button', { name: 'Preparar documento fiscal' }).click();

  await expect(page.locator('app-fiscal-document-card')).toContainText('Listo para timbrar');
  await expect(page.getByText('Aún no hay evidencia de timbrado disponible')).toBeVisible();

  await page.getByRole('button', { name: 'Timbrar' }).click();

  await expect(page.locator('app-fiscal-document-card').getByText('Timbrado')).toBeVisible();
  await expect(page.getByText('UUID-FISCAL-7101')).toBeVisible();
  await expect(page.getByText('FacturaloPlus')).toBeVisible();
  await expect(page.getByText('TRACK-FISCAL-7101')).toBeVisible();
});
