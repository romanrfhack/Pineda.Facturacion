import {
  AccountsReceivableInvoiceResponse,
  AccountsReceivablePortfolioItemResponse,
} from '../models/accounts-receivable.models';

const DUE_SOON_WINDOW_DAYS = 7;
const MILLISECONDS_PER_DAY = 24 * 60 * 60 * 1000;

export const RECEIVABLE_WORKSPACE_FILTERS = {
  pending: 'pending',
  overdue: 'overdue',
  currentOrDueSoon: 'currentOrDueSoon',
  openInvoices: 'openInvoices',
} as const;

export type ReceivableWorkspaceFilter =
  (typeof RECEIVABLE_WORKSPACE_FILTERS)[keyof typeof RECEIVABLE_WORKSPACE_FILTERS];

export type ReceivableCollectionStatus = 'paid' | 'overdue' | 'dueToday' | 'dueSoon' | 'current';

export interface ReceivableCollectionStatusDetail {
  readonly status: ReceivableCollectionStatus;
  readonly dayDelta: number | null;
  readonly badgeLabel: string;
  readonly dueDateHint: string | null;
}

export interface ReceivableInvoiceCollectionSummary {
  pendingBalanceTotal: number;
  overdueBalanceTotal: number;
  currentOrDueSoonBalanceTotal: number;
  openInvoicesCount: number;
  overdueInvoicesCount: number;
  currentOrDueSoonCount: number;
  paidInvoicesCount: number;
}

type ReceivableInvoiceLike = Pick<
  AccountsReceivablePortfolioItemResponse | AccountsReceivableInvoiceResponse,
  'dueAtUtc' | 'outstandingBalance'
>;

export function getReceivableCollectionStatus(
  invoice: ReceivableInvoiceLike,
  today: Date = new Date(),
): ReceivableCollectionStatus {
  if (invoice.outstandingBalance <= 0) {
    return 'paid';
  }

  const dayDelta = getInvoiceDayDelta(invoice, today);

  if (dayDelta === null) {
    return 'current';
  }

  if (dayDelta < 0) {
    return 'overdue';
  }

  if (dayDelta === 0) {
    return 'dueToday';
  }

  if (dayDelta <= DUE_SOON_WINDOW_DAYS) {
    return 'dueSoon';
  }

  return 'current';
}

export function describeReceivableCollectionStatus(
  invoice: ReceivableInvoiceLike,
  today: Date = new Date(),
): ReceivableCollectionStatusDetail {
  const status = getReceivableCollectionStatus(invoice, today);
  const dayDelta = status === 'paid' ? null : getInvoiceDayDelta(invoice, today);

  switch (status) {
    case 'overdue': {
      const daysPastDue = Math.abs(dayDelta ?? 0);
      return {
        status,
        dayDelta,
        badgeLabel: `Vencida · ${formatDaysPastDue(daysPastDue)}`,
        dueDateHint: capitalize(formatDaysPastDue(daysPastDue)),
      };
    }
    case 'dueToday':
      return {
        status,
        dayDelta,
        badgeLabel: 'Vence hoy',
        dueDateHint: 'Hoy',
      };
    case 'dueSoon': {
      const daysUntilDue = dayDelta ?? 0;
      return {
        status,
        dayDelta,
        badgeLabel: formatDaysUntilDue(daysUntilDue),
        dueDateHint: formatShortDaysUntilDue(daysUntilDue),
      };
    }
    case 'paid':
      return {
        status,
        dayDelta,
        badgeLabel: 'Pagada',
        dueDateHint: null,
      };
    case 'current':
    default:
      return {
        status: 'current',
        dayDelta,
        badgeLabel: 'Vigente',
        dueDateHint: null,
      };
  }
}

export function sortReceivableInvoices<T extends ReceivableInvoiceLike>(
  invoices: readonly T[],
  today: Date = new Date(),
): T[] {
  return invoices
    .map((invoice, index) => ({
      invoice,
      index,
      sortKey: buildSortKey(invoice, today),
    }))
    .sort((left, right) => {
      if (left.sortKey.priority !== right.sortKey.priority) {
        return left.sortKey.priority - right.sortKey.priority;
      }

      if (left.sortKey.dueDayKey !== right.sortKey.dueDayKey) {
        return left.sortKey.dueDayKey - right.sortKey.dueDayKey;
      }

      return left.index - right.index;
    })
    .map(({ invoice }) => invoice);
}

export function filterReceivableInvoices<T extends ReceivableInvoiceLike>(
  invoices: readonly T[],
  activeFilter: ReceivableWorkspaceFilter,
  today: Date = new Date(),
): T[] {
  return invoices.filter((invoice) => {
    const status = getReceivableCollectionStatus(invoice, today);

    switch (activeFilter) {
      case RECEIVABLE_WORKSPACE_FILTERS.overdue:
        return status === 'overdue';
      case RECEIVABLE_WORKSPACE_FILTERS.currentOrDueSoon:
        return status === 'dueToday' || status === 'dueSoon' || status === 'current';
      case RECEIVABLE_WORKSPACE_FILTERS.pending:
      case RECEIVABLE_WORKSPACE_FILTERS.openInvoices:
      default:
        return status !== 'paid';
    }
  });
}

export function summarizeReceivableInvoices<T extends ReceivableInvoiceLike>(
  invoices: readonly T[],
  today: Date = new Date(),
): ReceivableInvoiceCollectionSummary {
  const summary: ReceivableInvoiceCollectionSummary = {
    pendingBalanceTotal: 0,
    overdueBalanceTotal: 0,
    currentOrDueSoonBalanceTotal: 0,
    openInvoicesCount: 0,
    overdueInvoicesCount: 0,
    currentOrDueSoonCount: 0,
    paidInvoicesCount: 0,
  };

  invoices.forEach((invoice) => {
    const status = getReceivableCollectionStatus(invoice, today);
    const outstandingBalance = Math.max(invoice.outstandingBalance, 0);

    switch (status) {
      case 'paid':
        summary.paidInvoicesCount += 1;
        return;
      case 'overdue':
        summary.openInvoicesCount += 1;
        summary.pendingBalanceTotal += outstandingBalance;
        summary.overdueInvoicesCount += 1;
        summary.overdueBalanceTotal += outstandingBalance;
        return;
      case 'dueToday':
      case 'dueSoon':
      case 'current':
      default:
        summary.openInvoicesCount += 1;
        summary.pendingBalanceTotal += outstandingBalance;
        summary.currentOrDueSoonCount += 1;
        summary.currentOrDueSoonBalanceTotal += outstandingBalance;
        return;
    }
  });

  return summary;
}

export function formatReceivableCalendarDate(
  value: Date | string | null | undefined,
  fallback: string,
): string {
  const parts = readCalendarDateParts(value);
  if (!parts) {
    return fallback;
  }

  return `${parts.year}-${padTwoDigits(parts.month)}-${padTwoDigits(parts.day)}`;
}

function buildSortKey(
  invoice: ReceivableInvoiceLike,
  today: Date,
): {
  readonly priority: number;
  readonly dueDayKey: number;
} {
  const detail = describeReceivableCollectionStatus(invoice, today);

  return {
    priority: getStatusPriority(detail.status),
    dueDayKey: getSortableDueDayKey(invoice),
  };
}

function getStatusPriority(status: ReceivableCollectionStatus): number {
  switch (status) {
    case 'overdue':
      return 0;
    case 'dueToday':
      return 1;
    case 'dueSoon':
      return 2;
    case 'current':
      return 3;
    case 'paid':
    default:
      return 4;
  }
}

function getInvoiceDayDelta(invoice: ReceivableInvoiceLike, today: Date): number | null {
  const dueDayKey = toCalendarDayKey(invoice.dueAtUtc);
  if (dueDayKey === null) {
    return null;
  }

  return dueDayKey - toLocalCalendarDayKey(today);
}

function getSortableDueDayKey(invoice: ReceivableInvoiceLike): number {
  return toCalendarDayKey(invoice.dueAtUtc) ?? Number.MAX_SAFE_INTEGER;
}

function toCalendarDayKey(value: Date | string | null | undefined): number | null {
  const parts = readCalendarDateParts(value);
  if (!parts) {
    return null;
  }

  return buildCalendarDayKey(parts.year, parts.month, parts.day);
}

function readCalendarDateParts(value: Date | string | null | undefined): {
  readonly year: number;
  readonly month: number;
  readonly day: number;
} | null {
  if (!value) {
    return null;
  }

  if (typeof value === 'string') {
    const match = value.match(/^(\d{4})-(\d{2})-(\d{2})/);
    if (match) {
      return {
        year: Number(match[1]),
        month: Number(match[2]),
        day: Number(match[3]),
      };
    }
  }

  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) {
    return null;
  }

  return {
    year: date.getFullYear(),
    month: date.getMonth() + 1,
    day: date.getDate(),
  };
}

function toLocalCalendarDayKey(value: Date): number {
  return buildCalendarDayKey(value.getFullYear(), value.getMonth() + 1, value.getDate());
}

function buildCalendarDayKey(year: number, month: number, day: number): number {
  return Math.floor(Date.UTC(year, month - 1, day) / MILLISECONDS_PER_DAY);
}

function padTwoDigits(value: number): string {
  return String(value).padStart(2, '0');
}

function formatDaysPastDue(days: number): string {
  return `hace ${days} ${days === 1 ? 'día' : 'días'}`;
}

function formatDaysUntilDue(days: number): string {
  return `Vence en ${days} ${days === 1 ? 'día' : 'días'}`;
}

function formatShortDaysUntilDue(days: number): string {
  return `En ${days} ${days === 1 ? 'día' : 'días'}`;
}

function capitalize(value: string): string {
  return value.charAt(0).toUpperCase() + value.slice(1);
}
