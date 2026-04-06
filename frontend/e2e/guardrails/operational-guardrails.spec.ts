import { expect, test } from '@playwright/test';
import { LoginPage } from '../support/login-page';
import {
  mockMixedReceiversComplementFailure,
  mockOperatorReadOnlyFiscalDocument,
  mockStampUnavailableFiscalDocument
} from '../support/mock-operational-scenarios';

test('operator sees read-only fiscal document actions', async ({ page }) => {
  await mockOperatorReadOnlyFiscalDocument(page);

  const loginPage = new LoginPage(page);
  await loginPage.open();
  await loginPage.signIn('operator', 'Secret123!');

  await page.goto('/app/fiscal-documents/405');
  await expect(page.getByText('UUID-FISCAL-RO', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Timbrar' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Cancelar' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Actualizar estatus' })).toHaveCount(0);
});

test('supervisor sees provider unavailable feedback when invoice stamp fails', async ({ page }) => {
  await mockStampUnavailableFiscalDocument(page);

  const loginPage = new LoginPage(page);
  await loginPage.open();
  await loginPage.signIn('supervisor', 'Secret123!');

  await page.goto('/app/fiscal-documents/406', { waitUntil: 'commit' });
  await expect(page.getByText('Aún no hay evidencia de timbrado disponible')).toBeVisible();
  await page.getByRole('button', { name: 'Timbrar' }).click();
  await expect(page.getByText('PAC no disponible. Intenta de nuevo después de verificar el estatus.')).toBeVisible();
});

test('mixed receivers validation is shown during payment complement preparation', async ({ page }) => {
  await mockMixedReceiversComplementFailure(page);

  const loginPage = new LoginPage(page);
  await loginPage.open();
  await loginPage.signIn('supervisor', 'Secret123!');

  await page.goto('/app/payment-complements?paymentId=703', { waitUntil: 'domcontentloaded' });
  await page.getByRole('button', { name: 'Preparar complemento de pago' }).click();
  await expect(page.getByText('Las facturas aplicadas pertenecen a receptores distintos.')).toBeVisible();
});
