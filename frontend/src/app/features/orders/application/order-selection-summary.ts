export const DEFAULT_ORDER_CURRENCY = 'MXN';

export interface OrderSelectionSummaryItem {
  readonly total?: unknown;
  readonly currencyCode?: string | null;
}

export interface OrderSelectionTotalByCurrency {
  readonly currencyCode: string;
  readonly amount: number;
}

export interface OrderSelectionSummary {
  readonly count: number;
  readonly totalsByCurrency: OrderSelectionTotalByCurrency[];
}

export function summarizeOrderSelection(
  orders: readonly OrderSelectionSummaryItem[],
): OrderSelectionSummary {
  const totalsByCurrency = new Map<string, number>();

  for (const order of orders) {
    const currencyCode = normalizeOrderCurrency(order.currencyCode);
    const currentTotal = totalsByCurrency.get(currencyCode) ?? 0;
    totalsByCurrency.set(currencyCode, currentTotal + normalizeOrderTotal(order.total));
  }

  const totals = [...totalsByCurrency.entries()]
    .map(([currencyCode, amount]) => ({
      currencyCode,
      amount: roundMoney(amount),
    }))
    .sort((left, right) => left.currencyCode.localeCompare(right.currencyCode));

  return {
    count: orders.length,
    totalsByCurrency: totals.length > 0
      ? totals
      : [{ currencyCode: DEFAULT_ORDER_CURRENCY, amount: 0 }],
  };
}

export function normalizeOrderCurrency(currencyCode: string | null | undefined): string {
  return (currencyCode || DEFAULT_ORDER_CURRENCY).trim().toUpperCase() || DEFAULT_ORDER_CURRENCY;
}

function normalizeOrderTotal(value: unknown): number {
  if (typeof value === 'number') {
    return Number.isFinite(value) ? value : 0;
  }

  if (typeof value === 'string' && value.trim() !== '') {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  }

  return 0;
}

function roundMoney(value: number): number {
  return Math.round((value + Number.EPSILON) * 100) / 100;
}
