import {
  describeReceivableCollectionStatus,
  filterReceivableInvoices,
  formatReceivableCalendarDate,
  getReceivableCollectionStatus,
  RECEIVABLE_WORKSPACE_FILTERS,
  sortReceivableInvoices,
  summarizeReceivableInvoices,
} from './receivable-workspace-invoices';

describe('receivable-workspace-invoices', () => {
  const today = new Date('2026-04-15T12:00:00Z');

  it('classifies an invoice with pending balance and past due date as overdue', () => {
    const status = getReceivableCollectionStatus(
      createInvoice({
        dueAtUtc: '2026-04-14T00:00:00Z',
      }),
      today,
    );

    expect(status).toBe('overdue');
  });

  it('keeps a fully paid invoice out of overdue even when the due date already passed', () => {
    const status = getReceivableCollectionStatus(
      createInvoice({
        dueAtUtc: '2026-04-10T00:00:00Z',
        outstandingBalance: 0,
      }),
      today,
    );

    expect(status).toBe('paid');
  });

  it('classifies an invoice due on the same calendar day as due today', () => {
    const status = getReceivableCollectionStatus(
      createInvoice({
        dueAtUtc: '2026-04-15T00:00:00Z',
      }),
      today,
    );

    expect(status).toBe('dueToday');
  });

  it('classifies an invoice due tomorrow as due soon and keeps the singular label', () => {
    const detail = describeReceivableCollectionStatus(
      createInvoice({
        dueAtUtc: '2026-04-16T00:00:00Z',
      }),
      today,
    );

    expect(detail.status).toBe('dueSoon');
    expect(detail.badgeLabel).toBe('Vence en 1 día');
    expect(detail.dueDateHint).toBe('En 1 día');
  });

  it('keeps an invoice due in exactly seven days as due soon', () => {
    const detail = describeReceivableCollectionStatus(
      createInvoice({
        dueAtUtc: '2026-04-22T00:00:00Z',
      }),
      today,
    );

    expect(detail.status).toBe('dueSoon');
    expect(detail.badgeLabel).toBe('Vence en 7 días');
  });

  it('classifies an invoice due after seven days as current', () => {
    const status = getReceivableCollectionStatus(
      createInvoice({
        dueAtUtc: '2026-04-23T00:00:00Z',
      }),
      today,
    );

    expect(status).toBe('current');
  });

  it('sorts overdue invoices first and keeps the oldest overdue invoices ahead of the rest', () => {
    const invoices = [
      createInvoice({
        accountsReceivableInvoiceId: 5,
        dueAtUtc: '2026-04-24T00:00:00Z',
      }),
      createInvoice({
        accountsReceivableInvoiceId: 2,
        dueAtUtc: '2026-04-12T00:00:00Z',
      }),
      createInvoice({
        accountsReceivableInvoiceId: 4,
        dueAtUtc: '2026-04-17T00:00:00Z',
      }),
      createInvoice({
        accountsReceivableInvoiceId: 1,
        dueAtUtc: '2026-04-10T00:00:00Z',
      }),
      createInvoice({
        accountsReceivableInvoiceId: 3,
        dueAtUtc: '2026-04-15T00:00:00Z',
      }),
      createInvoice({
        accountsReceivableInvoiceId: 6,
        dueAtUtc: '2026-04-11T00:00:00Z',
        outstandingBalance: 0,
      }),
    ];

    const sorted = sortReceivableInvoices(invoices, today);

    expect(sorted.map((invoice) => invoice.accountsReceivableInvoiceId)).toEqual([
      1, 2, 3, 4, 5, 6,
    ]);
  });

  it('filters overdue invoices only', () => {
    const filtered = filterReceivableInvoices(
      [
        createInvoice({
          accountsReceivableInvoiceId: 1,
          dueAtUtc: '2026-04-14T00:00:00Z',
        }),
        createInvoice({
          accountsReceivableInvoiceId: 2,
          dueAtUtc: '2026-04-16T00:00:00Z',
        }),
        createInvoice({
          accountsReceivableInvoiceId: 3,
          dueAtUtc: '2026-04-10T00:00:00Z',
          outstandingBalance: 0,
        }),
      ],
      RECEIVABLE_WORKSPACE_FILTERS.overdue,
      today,
    );

    expect(filtered.map((invoice) => invoice.accountsReceivableInvoiceId)).toEqual([1]);
  });

  it('filters current, due today and due soon invoices together', () => {
    const filtered = filterReceivableInvoices(
      [
        createInvoice({
          accountsReceivableInvoiceId: 1,
          dueAtUtc: '2026-04-14T00:00:00Z',
        }),
        createInvoice({
          accountsReceivableInvoiceId: 2,
          dueAtUtc: '2026-04-15T00:00:00Z',
        }),
        createInvoice({
          accountsReceivableInvoiceId: 3,
          dueAtUtc: '2026-04-18T00:00:00Z',
        }),
        createInvoice({
          accountsReceivableInvoiceId: 4,
          dueAtUtc: '2026-04-27T00:00:00Z',
        }),
        createInvoice({
          accountsReceivableInvoiceId: 5,
          dueAtUtc: '2026-04-10T00:00:00Z',
          outstandingBalance: 0,
        }),
      ],
      RECEIVABLE_WORKSPACE_FILTERS.currentOrDueSoon,
      today,
    );

    expect(filtered.map((invoice) => invoice.accountsReceivableInvoiceId)).toEqual([2, 3, 4]);
  });

  it('summarizes pending, overdue and current or due soon totals from the same invoice set', () => {
    const summary = summarizeReceivableInvoices(
      [
        createInvoice({
          accountsReceivableInvoiceId: 1,
          dueAtUtc: '2026-04-14T23:59:59-05:00',
          outstandingBalance: 1200,
        }),
        createInvoice({
          accountsReceivableInvoiceId: 2,
          dueAtUtc: '2026-04-15T23:59:59-05:00',
          outstandingBalance: 800,
        }),
        createInvoice({
          accountsReceivableInvoiceId: 3,
          dueAtUtc: '2026-04-22T08:00:00Z',
          outstandingBalance: 700,
        }),
        createInvoice({
          accountsReceivableInvoiceId: 4,
          dueAtUtc: '2026-04-10T00:00:00Z',
          outstandingBalance: 0,
        }),
      ],
      today,
    );

    expect(summary.overdueInvoicesCount).toBe(1);
    expect(summary.overdueBalanceTotal).toBe(1200);
    expect(summary.currentOrDueSoonCount).toBe(2);
    expect(summary.currentOrDueSoonBalanceTotal).toBe(1500);
    expect(summary.openInvoicesCount).toBe(3);
    expect(summary.pendingBalanceTotal).toBe(2700);
    expect(summary.paidInvoicesCount).toBe(1);
  });

  it('formats calendar dates using the literal date portion when the API string includes a timezone offset', () => {
    expect(formatReceivableCalendarDate('2026-04-15T23:59:59-05:00', 'N/D')).toBe('2026-04-15');
  });
});

function createInvoice(
  overrides: Partial<{
    accountsReceivableInvoiceId: number;
    dueAtUtc: string | null;
    outstandingBalance: number;
  }> = {},
) {
  return {
    accountsReceivableInvoiceId: overrides.accountsReceivableInvoiceId ?? 1,
    dueAtUtc: overrides.dueAtUtc ?? '2026-04-20T00:00:00Z',
    outstandingBalance: overrides.outstandingBalance ?? 100,
  };
}
