import {
  CancelFiscalDocumentRequest,
  CancelFiscalDocumentResponse,
  FiscalCancellationResponse,
  FiscalDocumentResponse
} from '../models/fiscal-documents.models';

export const cancellationReasonOptions = [
  {
    code: '01',
    description: 'Comprobante emitido con errores con relación',
    helpText: 'Usa este motivo cuando el CFDI será sustituido por otro comprobante relacionado.'
  },
  {
    code: '02',
    description: 'Comprobante emitido con errores sin relación',
    helpText: 'Usa este motivo cuando el CFDI tiene errores y no será sustituido por un nuevo comprobante relacionado.'
  },
  {
    code: '03',
    description: 'No se llevó a cabo la operación',
    helpText: 'Usa este motivo cuando la operación comercial finalmente no ocurrió.'
  },
  {
    code: '04',
    description: 'Operación nominativa relacionada en una factura global',
    helpText: 'Usa este motivo cuando la operación ya quedó incluida en una factura global nominativa.'
  }
] as const;

export function canCancelFiscalDocumentStatus(status: string | null | undefined): boolean {
  return status === 'Stamped' || status === 'CancellationRejected';
}

export function normalizeSatCode(value: string | null | undefined): string {
  return value?.trim().toUpperCase() ?? '';
}

export function normalizeOptionalText(value: string | null | undefined): string | null {
  const trimmed = value?.trim() ?? '';
  return trimmed.length > 0 ? trimmed : null;
}

export function buildCancellationConfirmationMessage(request: CancelFiscalDocumentRequest): string {
  const reason = cancellationReasonOptions.find((option) => option.code === request.cancellationReasonCode);
  if (request.cancellationReasonCode === '01' && request.replacementUuid) {
    return `¿Confirmas la cancelación SAT con motivo ${request.cancellationReasonCode} - ${reason?.description ?? 'Motivo SAT'} y UUID de sustitución ${request.replacementUuid}?`;
  }

  return `¿Confirmas la cancelación SAT con motivo ${request.cancellationReasonCode} - ${reason?.description ?? 'Motivo SAT'}?`;
}

export function buildCancellationRequest(
  reasonCode: string | null | undefined,
  replacementUuid: string | null | undefined
): CancelFiscalDocumentRequest | null {
  const normalizedReasonCode = normalizeSatCode(reasonCode);
  if (!normalizedReasonCode) {
    return null;
  }

  return {
    cancellationReasonCode: normalizedReasonCode,
    replacementUuid: normalizedReasonCode === '01'
      ? normalizeOptionalText(replacementUuid)
      : undefined
  };
}

export function getCancellationValidationError(
  reasonCode: string | null | undefined,
  replacementUuid: string | null | undefined
): string | null {
  const normalizedReasonCode = normalizeSatCode(reasonCode);
  if (!normalizedReasonCode || !cancellationReasonOptions.some((option) => option.code === normalizedReasonCode)) {
    return 'Selecciona un motivo de cancelación SAT válido.';
  }

  if (normalizedReasonCode === '01' && !normalizeOptionalText(replacementUuid)) {
    return 'El motivo 01 requiere capturar el UUID de sustitución.';
  }

  return null;
}

export function reconcileCancellationAfterOperation(
  currentDocument: FiscalDocumentResponse | null,
  currentCancellation: FiscalCancellationResponse | null,
  response: CancelFiscalDocumentResponse,
  request: CancelFiscalDocumentRequest,
  requestedAtUtc: string = new Date().toISOString()
): {
  nextDocument: FiscalDocumentResponse | null;
  nextCancellation: FiscalCancellationResponse;
} {
  const nextDocument = currentDocument && response.fiscalDocumentStatus
    ? { ...currentDocument, status: response.fiscalDocumentStatus }
    : currentDocument;

  const nextStatus = response.isSuccess
    ? 'Cancelled'
    : response.cancellationStatus ?? currentCancellation?.status ?? 'Rejected';

  return {
    nextDocument,
    nextCancellation: {
      fiscalDocumentId: response.fiscalDocumentId,
      status: nextStatus,
      cancellationReasonCode: request.cancellationReasonCode,
      replacementUuid: request.replacementUuid ?? null,
      providerName: response.providerName ?? currentCancellation?.providerName ?? 'FacturaloPlus',
      providerTrackingId: response.providerTrackingId ?? currentCancellation?.providerTrackingId ?? null,
      providerCode: response.providerCode ?? currentCancellation?.providerCode ?? null,
      providerMessage: response.providerMessage ?? currentCancellation?.providerMessage ?? null,
      errorCode: response.errorCode ?? currentCancellation?.errorCode ?? null,
      errorMessage: response.errorMessage ?? currentCancellation?.errorMessage ?? null,
      supportMessage: response.supportMessage ?? currentCancellation?.supportMessage ?? null,
      rawResponseSummaryJson: response.rawResponseSummaryJson ?? currentCancellation?.rawResponseSummaryJson ?? null,
      requestedAtUtc: currentCancellation?.requestedAtUtc ?? requestedAtUtc,
      cancelledAtUtc: response.cancelledAtUtc ?? currentCancellation?.cancelledAtUtc ?? null
    }
  };
}

export function shouldKeepCurrentCancelledCancellation(
  currentCancellation: FiscalCancellationResponse | null,
  fetchedCancellation: FiscalCancellationResponse
): boolean {
  return currentCancellation?.status === 'Cancelled' && fetchedCancellation.status !== 'Cancelled';
}
