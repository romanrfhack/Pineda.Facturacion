import {
  selectReceivablesSummaryInvoices,
  summarizeReceivablesSummaryInvoices,
} from './receivables-summary-selection';
import { ReceivablesSummaryCandidateResponse } from '../models/accounts-receivable.models';

describe('receivables-summary-selection', () => {
  it('selects all pending invoices and keeps partially paid balances', () => {
    const invoices = [
      createInvoice({ accountsReceivableInvoiceId: 1, total: 1000, paidTotal: 250, outstandingBalance: 750 }),
      createInvoice({ accountsReceivableInvoiceId: 2, total: 500, paidTotal: 0, outstandingBalance: 500 }),
    ];

    const selected = selectReceivablesSummaryInvoices(invoices, 'all_pending', []);
    const summary = summarizeReceivablesSummaryInvoices(selected);

    expect(selected.map((invoice) => invoice.accountsReceivableInvoiceId)).toEqual([1, 2]);
    expect(summary.invoiceCount).toBe(2);
    expect(summary.outstandingBalance).toBe(1250);
  });

  it('selects overdue invoices only', () => {
    const invoices = [
      createInvoice({ accountsReceivableInvoiceId: 1, isOverdue: true }),
      createInvoice({ accountsReceivableInvoiceId: 2, isOverdue: false }),
    ];

    const selected = selectReceivablesSummaryInvoices(invoices, 'overdue', []);

    expect(selected.map((invoice) => invoice.accountsReceivableInvoiceId)).toEqual([1]);
  });

  it('selects manual invoice ids and groups totals by currency', () => {
    const invoices = [
      createInvoice({ accountsReceivableInvoiceId: 1, currencyCode: 'MXN', outstandingBalance: 100 }),
      createInvoice({ accountsReceivableInvoiceId: 2, currencyCode: 'USD', outstandingBalance: 50, isOverdue: true }),
      createInvoice({ accountsReceivableInvoiceId: 3, currencyCode: 'MXN', outstandingBalance: 25 }),
    ];

    const selected = selectReceivablesSummaryInvoices(invoices, 'manual', [1, 2]);
    const summary = summarizeReceivablesSummaryInvoices(selected);

    expect(selected.map((invoice) => invoice.accountsReceivableInvoiceId)).toEqual([1, 2]);
    expect(summary.totalsByCurrency).toEqual([
      expect.objectContaining({ currencyCode: 'MXN', outstandingBalance: 100, currentBalance: 100 }),
      expect.objectContaining({ currencyCode: 'USD', outstandingBalance: 50, overdueBalance: 50 }),
    ]);
  });
});

function createInvoice(
  overrides: Partial<ReceivablesSummaryCandidateResponse> = {},
): ReceivablesSummaryCandidateResponse {
  return {
    accountsReceivableInvoiceId: overrides.accountsReceivableInvoiceId ?? 1,
    fiscalDocumentId: overrides.fiscalDocumentId ?? 100,
    fiscalSeries: overrides.fiscalSeries ?? 'A',
    fiscalFolio: overrides.fiscalFolio ?? '100',
    fiscalUuid: overrides.fiscalUuid ?? 'UUID',
    issuedAtUtc: overrides.issuedAtUtc ?? '2026-04-01T00:00:00Z',
    dueAtUtc: overrides.dueAtUtc ?? '2026-04-15T00:00:00Z',
    daysPastDue: overrides.daysPastDue ?? 0,
    currencyCode: overrides.currencyCode ?? 'MXN',
    total: overrides.total ?? 100,
    paidTotal: overrides.paidTotal ?? 0,
    outstandingBalance: overrides.outstandingBalance ?? 100,
    status: overrides.status ?? 'Open',
    isOverdue: overrides.isOverdue ?? false,
    documentLink: overrides.documentLink ?? null,
  };
}
