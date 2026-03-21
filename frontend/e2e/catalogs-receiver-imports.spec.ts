import { expect, test } from '@playwright/test';
import { LoginPage } from './support/login-page';
import { mockCatalogReceiverImportsBackend } from './support/mock-catalogs';

test('login then apply all eligible receiver import rows', async ({ page }) => {
  await mockCatalogReceiverImportsBackend(page);

  const loginPage = new LoginPage(page);
  await loginPage.open();
  await loginPage.signIn('supervisor', 'Secret123!');

  await page.goto('/app/catalogs/imports/receivers');
  await page.getByLabel('Cargar lote por id').fill('22');
  await page.getByRole('button', { name: 'Cargar lote' }).click();

  await expect(page.getByText('Las filas elegibles son las válidas con acción sugerida Crear o Actualizar. Actualmente hay 2.')).toBeVisible();
  await expect(page.getByLabel('Aplicar todas las filas elegibles')).toBeChecked();
  await expect(page.getByLabel('Números de fila')).toHaveCount(0);

  const applyRequest = page.waitForRequest('**/api/fiscal/imports/receivers/batches/22/apply');
  const applyResponse = page.waitForResponse('**/api/fiscal/imports/receivers/batches/22/apply');

  page.once('dialog', (dialog) => dialog.accept());
  await page.getByRole('button', { name: 'Aplicar lote de receptores' }).click();

  const request = await applyRequest;
  await applyResponse;

  expect(request.postDataJSON()).toEqual({
    applyMode: 0,
    stopOnFirstError: false
  });
});
