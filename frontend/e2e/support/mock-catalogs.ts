import type { Page } from '@playwright/test';

export async function mockCatalogReceiversBackend(page: Page): Promise<void> {
  let receivers = [
    {
      id: 9,
      rfc: 'BBB010101BBB',
      legalName: 'Receiver One',
      postalCode: '02000',
      fiscalRegimeCode: '601',
      cfdiUseCodeDefault: 'G03',
      isActive: true,
      countryCode: 'MX',
      foreignTaxRegistration: null,
      email: 'receiver@example.com',
      phone: '555-0101',
      searchAlias: 'Receiver One',
      createdAtUtc: new Date().toISOString(),
      updatedAtUtc: new Date().toISOString()
    }
  ];

  await page.route('**/api/auth/login', async (route) => {
    await route.fulfill({
      json: {
        outcome: 'Authenticated',
        isSuccess: true,
        token: 'catalog-token',
        expiresAtUtc: new Date().toISOString(),
        user: {
          id: 1,
          username: 'supervisor',
          displayName: 'Supervisor',
          roles: ['FiscalSupervisor'],
          isAuthenticated: true
        }
      }
    });
  });

  await page.route('**/api/auth/me', async (route) => {
    await route.fulfill({
      json: {
        id: 1,
        username: 'supervisor',
        displayName: 'Supervisor',
        roles: ['FiscalSupervisor'],
        isAuthenticated: true
      }
    });
  });

  await page.route('**/api/fiscal/receivers/search**', async (route) => {
    const url = new URL(route.request().url());
    const query = (url.searchParams.get('q') || '').toUpperCase();
    const filtered = receivers.filter((receiver) =>
      !query
      || receiver.rfc.includes(query)
      || receiver.legalName.toUpperCase().includes(query)
    );

    await route.fulfill({
      json: filtered.map((receiver) => ({
        id: receiver.id,
        rfc: receiver.rfc,
        legalName: receiver.legalName,
        postalCode: receiver.postalCode,
        fiscalRegimeCode: receiver.fiscalRegimeCode,
        cfdiUseCodeDefault: receiver.cfdiUseCodeDefault,
        isActive: receiver.isActive
      }))
    });
  });

  await page.route('**/api/fiscal/receivers/by-rfc/**', async (route) => {
    const rfc = decodeURIComponent(route.request().url().split('/by-rfc/')[1]);
    const receiver = receivers.find((item) => item.rfc === rfc);

    if (!receiver) {
      await route.fulfill({ status: 404, json: {} });
      return;
    }

    await route.fulfill({ json: receiver });
  });

  await page.route('**/api/fiscal/receivers/', async (route) => {
    if (route.request().method() !== 'POST') {
      await route.fallback();
      return;
    }

    const payload = route.request().postDataJSON() as Record<string, unknown>;
    const created = {
      id: 15,
      rfc: String(payload['rfc']),
      legalName: String(payload['legalName']),
      postalCode: String(payload['postalCode']),
      fiscalRegimeCode: String(payload['fiscalRegimeCode']),
      cfdiUseCodeDefault: String(payload['cfdiUseCodeDefault']),
      isActive: Boolean(payload['isActive']),
      countryCode: payload['countryCode'] as string | null,
      foreignTaxRegistration: payload['foreignTaxRegistration'] as string | null,
      email: payload['email'] as string | null,
      phone: payload['phone'] as string | null,
      searchAlias: payload['searchAlias'] as string | null,
      createdAtUtc: new Date().toISOString(),
      updatedAtUtc: new Date().toISOString()
    };

    receivers = [created, ...receivers];

    await route.fulfill({
      json: {
        outcome: 'Created',
        isSuccess: true,
        id: created.id
      }
    });
  });
}
