import type { Page } from '@playwright/test';

export async function mockAuditBackend(page: Page): Promise<void> {
  await page.route('**/api/auth/login', async (route) => {
    await route.fulfill({
      json: {
        outcome: 'Authenticated',
        isSuccess: true,
        token: 'audit-token',
        expiresAtUtc: new Date().toISOString(),
        user: {
          id: 1,
          username: 'auditor',
          displayName: 'Auditor',
          roles: ['Auditor'],
          isAuthenticated: true
        }
      }
    });
  });

  await page.route('**/api/auth/me', async (route) => {
    await route.fulfill({
      json: {
        id: 1,
        username: 'auditor',
        displayName: 'Auditor',
        roles: ['Auditor'],
        isAuthenticated: true
      }
    });
  });

  await page.route('**/api/audit-events**', async (route) => {
    const url = new URL(route.request().url());
    const actor = url.searchParams.get('actorUsername');

    const items = [
      {
        id: 1,
        occurredAtUtc: '2026-03-20T10:00:00Z',
        actorUserId: 1,
        actorUsername: 'admin',
        actionType: 'FiscalDocument.Stamp',
        entityType: 'FiscalDocument',
        entityId: '50',
        outcome: 'Stamped',
        correlationId: 'corr-audit-001',
        requestSummaryJson: '{"billingDocumentId":30}',
        responseSummaryJson: '{"fiscalDocumentId":50}',
        errorMessage: null,
        ipAddress: '127.0.0.1',
        userAgent: 'Playwright',
        createdAtUtc: '2026-03-20T10:00:00Z'
      },
      {
        id: 2,
        occurredAtUtc: '2026-03-20T11:00:00Z',
        actorUserId: 2,
        actorUsername: 'supervisor',
        actionType: 'FiscalReceiver.Update',
        entityType: 'FiscalReceiver',
        entityId: '9',
        outcome: 'Updated',
        correlationId: 'corr-audit-002',
        requestSummaryJson: '{"rfc":"BBB010101BBB"}',
        responseSummaryJson: '{"id":9}',
        errorMessage: null,
        ipAddress: '127.0.0.1',
        userAgent: 'Playwright',
        createdAtUtc: '2026-03-20T11:00:00Z'
      }
    ].filter((item) => !actor || item.actorUsername?.includes(actor));

    await route.fulfill({
      json: {
        page: 1,
        pageSize: 25,
        totalCount: items.length,
        items
      }
    });
  });
}
