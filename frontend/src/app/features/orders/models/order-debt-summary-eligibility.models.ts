export interface OrderDebtSummaryEligibilityRequest {
  legacyOrderIds: string[];
}

export interface OrderDebtSummaryEligibilityResponse {
  success: boolean;
  errorMessage?: string | null;
  requestedOrderCount: number;
  eligibleOrderCount: number;
  blockedOrderCount: number;
  missingOrderIds: string[];
  items: OrderDebtSummaryEligibilityItemResponse[];
}

export interface OrderDebtSummaryEligibilityItemResponse {
  legacyOrderId: string;
  canInclude: boolean;
  requiresReview: boolean;
  classification: string;
  reasonCode: string;
  message: string;
  displayStatus: string;
  reportGroupKey: string;
  currencyCode: string;
  amountDue: number;
  amountDueContribution: number;
  billingDocumentId?: number | null;
  fiscalDocumentId?: number | null;
  fiscalUuid?: string | null;
  accountsReceivableInvoiceId?: number | null;
  accountsReceivableStatus?: string | null;
  invoiceTotal?: number | null;
  paidTotal?: number | null;
  outstandingBalance?: number | null;
  relatedLegacyOrderIds: string[];
}
