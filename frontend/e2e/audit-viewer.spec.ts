import { expect, test } from '@playwright/test';
import { LoginPage } from './support/login-page';
import { mockAuditBackend } from './support/mock-audit';

test('login then open audit viewer apply filter and inspect detail', async ({ page }) => {
  await mockAuditBackend(page);

  const loginPage = new LoginPage(page);
  await loginPage.open();
  await loginPage.signIn('auditor', 'Secret123!');
  await expect(page).toHaveURL(/\/app\/audit$/);
  await expect(page.getByRole('cell', { name: 'FiscalDocument.Stamp' })).toBeVisible();

  await page.getByRole('textbox', { name: 'Actor' }).fill('admin');
  await page.getByRole('button', { name: 'Apply filters' }).click();

  await expect(page.getByRole('cell', { name: 'corr-audit-001' })).toBeVisible();
  await page.getByRole('button', { name: 'Details' }).first().click();
  await expect(page.getByText('Request summary')).toBeVisible();
});
