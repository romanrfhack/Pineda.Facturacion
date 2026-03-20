import { expect, test } from '@playwright/test';
import { LoginPage } from '../support/login-page';
import { mockPaymentComplementJourney } from '../support/mock-operational-scenarios';

test('supervisor prepares and stamps a payment complement from a deterministic payment state', async ({ page }) => {
  await mockPaymentComplementJourney(page);

  const loginPage = new LoginPage(page);
  await loginPage.open();
  await loginPage.signIn('supervisor', 'Secret123!');
  await expect(page).toHaveURL(/\/app\/orders$/);

  await page.goto('/app/payment-complements?paymentId=702');
  await page.getByRole('button', { name: 'Prepare payment complement' }).click();

  await expect(page.getByText('Payment complement snapshot')).toBeVisible();
  await expect(page.getByText('UUID-FISCAL-411')).toBeVisible();
  await expect(page.getByText('Readyforstamping')).toBeVisible();

  await page.getByRole('button', { name: 'Stamp' }).click();

  await expect(page.getByText('UUID-PC-702')).toBeVisible();
  await expect(page.getByText('Stamped successfully')).toBeVisible();
  await expect(page.locator('app-payment-complement-card')).toContainText('Stamped');
});
