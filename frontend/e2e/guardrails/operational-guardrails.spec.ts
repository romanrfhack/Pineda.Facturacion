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
  await expect(page.getByText('UUID-FISCAL-RO')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Stamp' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Cancel' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Refresh status' })).toHaveCount(0);
});

test('supervisor sees provider unavailable feedback when invoice stamp fails', async ({ page }) => {
  await mockStampUnavailableFiscalDocument(page);

  const loginPage = new LoginPage(page);
  await loginPage.open();
  await loginPage.signIn('supervisor', 'Secret123!');

  await page.goto('/app/fiscal-documents/406');
  await expect(page.getByText('No stamp evidence is available yet')).toBeVisible();
  await page.getByRole('button', { name: 'Stamp' }).click();
  await expect(page.getByText('PAC provider is unavailable. Retry after checking status.')).toBeVisible();
});

test('mixed receivers validation is shown during payment complement preparation', async ({ page }) => {
  await mockMixedReceiversComplementFailure(page);

  const loginPage = new LoginPage(page);
  await loginPage.open();
  await loginPage.signIn('supervisor', 'Secret123!');

  await page.goto('/app/payment-complements?paymentId=703', { waitUntil: 'domcontentloaded' });
  await page.getByRole('button', { name: 'Prepare payment complement' }).click();
  await expect(page.getByText('Applied invoices belong to different receivers.')).toBeVisible();
});
