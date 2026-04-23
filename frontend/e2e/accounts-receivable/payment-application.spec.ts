import { expect, test } from '@playwright/test';
import { LoginPage } from '../support/login-page';
import {
  mockAccountsReceivableJourney,
  mockAccountsReceivablePaymentRemainderJourney,
} from '../support/mock-operational-scenarios';

async function openAccountsReceivablePaymentForm(
  page: import('@playwright/test').Page,
): Promise<void> {
  const loginPage = new LoginPage(page);
  await loginPage.open();
  await loginPage.signIn('operator', 'Secret123!');
  await expect(page).toHaveURL(/\/app\/orders$/);

  await page.goto('/app/accounts-receivable?fiscalDocumentId=401');
  await page.getByRole('button', { name: 'Crear cuenta por cobrar' }).click();

  await expect(page.getByText('Cuenta #601 · CFDI A-401')).toBeVisible();
  await expect(page.locator('app-accounts-receivable-card').getByText('100.00 MXN')).toBeVisible();
  await page.getByRole('link', { name: 'Nuevo pago' }).click();

  await expect(page).toHaveURL(/\/app\/accounts-receivable\/new-payment\?invoiceId=601$/);
  await expect(page.locator('app-payment-create-form')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Aplicar remanente seleccionado' })).toHaveCount(0);
}

test('operator creates an AR invoice then records and applies a payment', async ({ page }) => {
  await mockAccountsReceivableJourney(page);
  await openAccountsReceivablePaymentForm(page);

  await page.locator('app-payment-create-form').getByTestId('payment-create-amount').fill('40');
  await page.locator('app-payment-create-form').getByLabel('Referencia').fill('PAY-701');
  await page.getByRole('button', { name: 'Crear pago' }).click();

  await expect(page).toHaveURL(/\/app\/accounts-receivable\?.*paymentId=701/);
  await expect(page.getByText('Pago #701')).toBeVisible();
  await expect(page.getByText('Remanente disponible 40 MXN')).toBeVisible();
  await expect(page.locator('app-payment-create-form')).toHaveCount(0);

  await page.getByRole('textbox', { name: 'Monto a aplicar a esta cuenta MXN' }).fill('40.00');
  await page.getByRole('button', { name: 'Aplicar pago a esta cuenta' }).click();

  await expect(page.getByText('Remanente disponible 0 MXN')).toBeVisible();
  await expect(page.getByRole('cell', { name: '601', exact: true })).toBeVisible();
  await expect(page.getByRole('cell', { name: '40', exact: true })).toBeVisible();
  await expect(page.getByRole('cell', { name: '60', exact: true })).toBeVisible();
});

test('operator can continue when the payment amount matches the operational outstanding balance', async ({
  page,
}) => {
  await mockAccountsReceivableJourney(page);
  await openAccountsReceivablePaymentForm(page);

  const paymentForm = page.locator('app-payment-create-form');
  await paymentForm.getByTestId('payment-create-amount').fill('100');
  await paymentForm.getByLabel('Referencia').fill('PAY-701-EXACT');

  await expect(paymentForm.getByTestId('payment-create-amount-info')).toHaveCount(0);
  await expect(paymentForm.getByTestId('payment-create-submit')).toBeEnabled();

  await paymentForm.getByTestId('payment-create-submit').click();

  await expect(page).toHaveURL(/\/app\/accounts-receivable\?.*paymentId=701/);
  await expect(page.getByText('Pago #701')).toBeVisible();
  await expect(page.getByText('Remanente disponible 100 MXN')).toBeVisible();
  await expect(page.locator('app-payment-create-form')).toHaveCount(0);
});

test('operator can register the full received amount even when it exceeds the current operational outstanding balance', async ({
  page,
}) => {
  await mockAccountsReceivableJourney(page);
  await openAccountsReceivablePaymentForm(page);

  const paymentForm = page.locator('app-payment-create-form');
  await paymentForm.getByTestId('payment-create-amount').fill('100.01');

  await expect(paymentForm.getByTestId('payment-create-amount-info')).toBeVisible();
  await expect(paymentForm.getByTestId('payment-create-amount-info')).toContainText('0.01 MXN');
  await expect(paymentForm.getByTestId('payment-create-submit')).toBeEnabled();

  await paymentForm.getByTestId('payment-create-submit').click();

  await expect(page).toHaveURL(/\/app\/accounts-receivable\?.*paymentId=701/);
  await expect(page.getByText('Pago #701')).toBeVisible();
  await expect(page.getByText('Remanente disponible 100.01 MXN')).toBeVisible();
  await expect(page.locator('app-payment-create-form')).toHaveCount(0);
});

test('operator can reopen a payment by paymentId and apply its remainder to another invoice from the same receiver', async ({
  page,
}) => {
  await mockAccountsReceivablePaymentRemainderJourney(page);

  const loginPage = new LoginPage(page);
  await loginPage.open();
  await loginPage.signIn('operator', 'Secret123!');
  await expect(page).toHaveURL(/\/app\/orders$/);

  await page.goto('/app/accounts-receivable?paymentId=702');

  await expect(page.getByText('Pago #702')).toBeVisible();
  await expect(page.getByText('Remanente disponible 60 MXN')).toBeVisible();
  await expect(
    page.getByText('Aplicar remanente a otras facturas del mismo receptor'),
  ).toBeVisible();
  await expect(page.getByText('Aplicar pago a esta cuenta')).toHaveCount(0);

  const remainderForm = page.locator('app-payment-remainder-application-form');
  await expect(remainderForm.getByText('#602')).toBeVisible();
  await remainderForm.locator('input').first().fill('60.00');
  await remainderForm.getByRole('button', { name: 'Aplicar remanente seleccionado' }).click();

  await expect(page.getByText('Remanente disponible 0 MXN')).toBeVisible();
  const appliedRow = page.locator('table.applications tbody tr').filter({ hasText: '602' });
  await expect(appliedRow).toBeVisible();
  await expect(appliedRow).toContainText('60');
});
