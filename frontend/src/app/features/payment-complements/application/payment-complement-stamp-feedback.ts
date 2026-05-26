import { StampAndEmailPaymentComplementEmailResponse } from '../models/payment-complements.models';

export function buildPaymentComplementStampFeedbackMessage(
  email: StampAndEmailPaymentComplementEmailResponse | null | undefined,
  defaultSuccessMessage: string,
): string {
  switch (email?.status) {
    case 'sent':
      return `Complemento de pago timbrado correctamente. El correo fue enviado automáticamente a: ${email.recipients.join(', ')}.`;
    default:
      return defaultSuccessMessage;
  }
}

export function shouldOpenPaymentComplementEmailComposerAfterStamp(
  status: StampAndEmailPaymentComplementEmailResponse['status'],
): status is 'missing' | 'invalid' | 'failed' {
  return status === 'missing' || status === 'invalid' || status === 'failed';
}
