import { expect, test } from '@playwright/test';
import { LoginPage } from './support/login-page';
import { mockFiscalPreparationFlow } from './support/mock-backend';

test('fiscal document preparation guides SAT capture and submits only SAT codes', async ({ page }) => {
  const backend = await mockFiscalPreparationFlow(page);

  const loginPage = new LoginPage(page);
  await loginPage.open();
  await loginPage.signIn('supervisor', 'Secret123!');
  await expect(page).toHaveURL(/\/app\/orders$/);

  await expect(page.getByText('LEG-7001')).toBeVisible();
  await page.getByRole('button', { name: 'Importar orden' }).first().click();
  await expect(page.getByText('Orden legada LEG-7001')).toBeVisible();

  await page.getByRole('button', { name: 'Crear documento de facturación' }).click();
  await expect(page).toHaveURL(/\/app\/fiscal-documents\?billingDocumentId=30$/);

  const prepareButton = page.getByRole('button', { name: 'Preparar documento fiscal' });
  const paymentMethodSelect = page.locator('select[name="paymentMethodSat"]');
  const paymentFormSelect = page.locator('select[name="paymentFormSat"]');
  const paymentConditionInput = page.getByRole('textbox', { name: 'Condición de pago' });
  const creditSaleCheckbox = page.getByRole('checkbox', { name: 'Venta a crédito' });
  const creditDaysInput = page.getByRole('spinbutton', { name: 'Días de crédito' });

  await expect(prepareButton).toBeDisabled();

  await page.getByRole('textbox', { name: 'Buscar receptor' }).fill('BBB010101BBB');
  await page.getByRole('button', { name: 'BBB010101BBB Receiver One Código postal 02000' }).click();
  await expect(page.getByText('Receptor seleccionado')).toBeVisible();

  await expect(paymentMethodSelect).toHaveValue('PPD');
  await expect(paymentFormSelect).toHaveValue('99');
  await expect(paymentConditionInput).toHaveValue('Crédito a 7 días');
  await expect(prepareButton).toBeEnabled();

  await creditSaleCheckbox.uncheck();
  await expect(paymentMethodSelect).toHaveValue('PUE');
  await expect(paymentConditionInput).toHaveValue('Contado');

  await paymentMethodSelect.selectOption('PUE');
  await expect(paymentFormSelect.locator('option')).toHaveCount(3);
  await expect(paymentFormSelect.locator('option[value="99"]')).toHaveCount(0);
  await paymentFormSelect.selectOption('03');
  await paymentConditionInput.fill('Contado');
  await expect(prepareButton).toBeEnabled();

  await creditSaleCheckbox.check();
  await expect(paymentMethodSelect).toHaveValue('PPD');
  await expect(paymentFormSelect).toHaveValue('99');
  await expect(paymentConditionInput).toHaveValue('Crédito a 7 días');

  await creditDaysInput.fill('21');
  await expect(paymentConditionInput).toHaveValue('Crédito a 21 días');
  await expect(paymentFormSelect.locator('option')).toHaveCount(2);
  await expect(paymentFormSelect.locator('option[value="99"]')).toHaveCount(1);

  await creditSaleCheckbox.uncheck();
  await expect(paymentMethodSelect).toHaveValue('PUE');
  await expect(paymentConditionInput).toHaveValue('Contado');
  await expect(paymentFormSelect.locator('option[value="99"]')).toHaveCount(0);

  await paymentFormSelect.selectOption('28');
  await paymentConditionInput.fill('Contado');
  await expect(prepareButton).toBeEnabled();

  await prepareButton.click();
  await expect(page.locator('app-fiscal-document-card')).toContainText('Listo para timbrar');

  expect(backend.getLastPreparePayload()).toEqual(expect.objectContaining({
    fiscalReceiverId: 9,
    issuerProfileId: 1,
    paymentMethodSat: 'PUE',
    paymentFormSat: '28',
    paymentCondition: 'Contado',
    isCreditSale: false,
    creditDays: 21
  }));
});
