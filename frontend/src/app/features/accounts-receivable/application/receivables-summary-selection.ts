import {
  ReceivablesSummaryCandidateResponse,
  ReceivablesSummaryScope,
  ReceivablesSummaryTotalByCurrencyResponse,
} from '../models/accounts-receivable.models';

export interface ReceivablesSummarySelectionViewModel {
  invoiceCount: number;
  outstandingBalance: number;
  overdueBalance: number;
  currentBalance: number;
  totalsByCurrency: ReceivablesSummaryTotalByCurrencyResponse[];
}

export function selectReceivablesSummaryInvoices(
  invoices: readonly ReceivablesSummaryCandidateResponse[],
  scope: ReceivablesSummaryScope,
  selectedInvoiceIds: readonly number[],
): ReceivablesSummaryCandidateResponse[] {
  const selectedIds = new Set(selectedInvoiceIds);

  switch (scope) {
    case 'all_pending':
      return [...invoices];
    case 'overdue':
      return invoices.filter((invoice) => invoice.isOverdue);
    case 'manual':
    case 'current_selection':
      return invoices.filter((invoice) => selectedIds.has(invoice.accountsReceivableInvoiceId));
    default:
      return [];
  }
}

export function summarizeReceivablesSummaryInvoices(
  invoices: readonly ReceivablesSummaryCandidateResponse[],
): ReceivablesSummarySelectionViewModel {
  const totalsByCurrency = new Map<string, ReceivablesSummaryTotalByCurrencyResponse>();

  for (const invoice of invoices) {
    const currencyCode = normalizeCurrency(invoice.currencyCode);
    const existing =
      totalsByCurrency.get(currencyCode) ??
      {
        currencyCode,
        invoiceCount: 0,
        total: 0,
        paidTotal: 0,
        outstandingBalance: 0,
        overdueBalance: 0,
        currentBalance: 0,
      };

    existing.invoiceCount += 1;
    existing.total += invoice.total;
    existing.paidTotal += invoice.paidTotal;
    existing.outstandingBalance += invoice.outstandingBalance;
    if (invoice.isOverdue) {
      existing.overdueBalance += invoice.outstandingBalance;
    } else {
      existing.currentBalance += invoice.outstandingBalance;
    }

    totalsByCurrency.set(currencyCode, existing);
  }

  const totals = [...totalsByCurrency.values()]
    .map((total) => ({
      ...total,
      total: roundMoney(total.total),
      paidTotal: roundMoney(total.paidTotal),
      outstandingBalance: roundMoney(total.outstandingBalance),
      overdueBalance: roundMoney(total.overdueBalance),
      currentBalance: roundMoney(total.currentBalance),
    }))
    .sort((left, right) => left.currencyCode.localeCompare(right.currencyCode));

  return {
    invoiceCount: invoices.length,
    outstandingBalance: roundMoney(totals.reduce((sum, item) => sum + item.outstandingBalance, 0)),
    overdueBalance: roundMoney(totals.reduce((sum, item) => sum + item.overdueBalance, 0)),
    currentBalance: roundMoney(totals.reduce((sum, item) => sum + item.currentBalance, 0)),
    totalsByCurrency: totals,
  };
}

export function formatReceivablesSummaryMoney(
  amount: number,
  currencyCode: string | null | undefined,
): string {
  return `${roundMoney(amount).toLocaleString('es-MX', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })} ${normalizeCurrency(currencyCode)}`;
}

function normalizeCurrency(currencyCode: string | null | undefined): string {
  return (currencyCode || 'MXN').trim().toUpperCase();
}

function roundMoney(value: number): number {
  return Math.round((value + Number.EPSILON) * 100) / 100;
}
