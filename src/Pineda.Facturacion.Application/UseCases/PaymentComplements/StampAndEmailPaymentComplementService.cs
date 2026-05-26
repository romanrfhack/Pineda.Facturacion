namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class StampAndEmailPaymentComplementService
{
    private readonly StampPaymentComplementService _stampPaymentComplementService;
    private readonly GetPaymentComplementEmailDraftService _getPaymentComplementEmailDraftService;
    private readonly SendPaymentComplementEmailService _sendPaymentComplementEmailService;

    public StampAndEmailPaymentComplementService(
        StampPaymentComplementService stampPaymentComplementService,
        GetPaymentComplementEmailDraftService getPaymentComplementEmailDraftService,
        SendPaymentComplementEmailService sendPaymentComplementEmailService)
    {
        _stampPaymentComplementService = stampPaymentComplementService;
        _getPaymentComplementEmailDraftService = getPaymentComplementEmailDraftService;
        _sendPaymentComplementEmailService = sendPaymentComplementEmailService;
    }

    public async Task<StampAndEmailPaymentComplementResult> ExecuteAsync(
        StampAndEmailPaymentComplementCommand command,
        CancellationToken cancellationToken = default)
    {
        var stampResult = await _stampPaymentComplementService.ExecuteAsync(
            new StampPaymentComplementCommand
            {
                PaymentComplementId = command.PaymentComplementId,
                RetryRejected = command.RetryRejected
            },
            cancellationToken);

        var result = MapStampResult(stampResult);
        if (!result.Stamped)
        {
            return result;
        }

        var draftResult = await _getPaymentComplementEmailDraftService.ExecuteAsync(command.PaymentComplementId, cancellationToken);
        var defaultRecipientEmail = draftResult.DefaultRecipientEmail?.Trim();
        if (string.IsNullOrWhiteSpace(defaultRecipientEmail))
        {
            result.Email.Status = StampAndEmailPaymentComplementEmailStatus.Missing;
            result.Email.Message = "El receptor no tiene un email registrado.";
            result.WarningMessages.Add("El complemento de pago se timbró correctamente, pero el receptor no tiene un email registrado.");
            return result;
        }

        var normalizedRecipients = SendPaymentComplementEmailService.NormalizeRecipients([defaultRecipientEmail]);
        if (normalizedRecipients.Count == 0)
        {
            result.Email.Status = StampAndEmailPaymentComplementEmailStatus.Invalid;
            result.Email.InvalidRecipients = [defaultRecipientEmail];
            result.Email.Message = "El email registrado del receptor no es válido.";
            result.WarningMessages.Add("El complemento de pago se timbró correctamente, pero el email registrado del receptor no es válido.");
            return result;
        }

        result.Email.Attempted = true;
        result.Email.Recipients = normalizedRecipients;

        try
        {
            var sendResult = await _sendPaymentComplementEmailService.ExecuteAsync(
                new SendPaymentComplementEmailCommand
                {
                    PaymentComplementId = command.PaymentComplementId,
                    Recipients = normalizedRecipients,
                    Subject = draftResult.Subject,
                    Body = draftResult.Body
                },
                cancellationToken);

            result.Email.Sent = sendResult.IsSuccess;
            result.Email.Status = sendResult.IsSuccess
                ? StampAndEmailPaymentComplementEmailStatus.Sent
                : StampAndEmailPaymentComplementEmailStatus.Failed;
            result.Email.Recipients = sendResult.Recipients;
            result.Email.SentAtUtc = sendResult.SentAtUtc;
            result.Email.Message = sendResult.IsSuccess
                ? $"El correo fue enviado automáticamente a: {string.Join(", ", sendResult.Recipients)}."
                : sendResult.ErrorMessage ?? sendResult.SupportMessage ?? "No fue posible enviar el complemento de pago por correo.";

            if (!sendResult.IsSuccess)
            {
                result.WarningMessages.Add("El complemento de pago se timbró correctamente, pero no fue posible enviar el correo.");
            }
        }
        catch (Exception exception)
        {
            result.Email.Sent = false;
            result.Email.Status = StampAndEmailPaymentComplementEmailStatus.Failed;
            result.Email.Message = $"No fue posible enviar el complemento de pago por correo por un error inesperado del servidor. {exception.Message}";
            result.WarningMessages.Add("El complemento de pago se timbró correctamente, pero no fue posible enviar el correo.");
        }

        return result;
    }

    private static StampAndEmailPaymentComplementResult MapStampResult(StampPaymentComplementResult stampResult)
    {
        return new StampAndEmailPaymentComplementResult
        {
            PaymentComplementId = stampResult.PaymentComplementId,
            Stamped = stampResult.Outcome == StampPaymentComplementOutcome.Stamped,
            Status = stampResult.Status,
            StampOutcome = stampResult.Outcome,
            IsSuccess = stampResult.IsSuccess,
            ErrorMessage = stampResult.ErrorMessage,
            ProviderName = stampResult.ProviderName,
            ProviderTrackingId = stampResult.ProviderTrackingId,
            ProviderCode = stampResult.ProviderCode,
            ProviderMessage = stampResult.ProviderMessage,
            ErrorCode = stampResult.ErrorCode,
            SupportMessage = stampResult.SupportMessage,
            RawResponseSummaryJson = stampResult.RawResponseSummaryJson,
            PaymentComplementStampId = stampResult.PaymentComplementStampId,
            Uuid = stampResult.Uuid,
            StampedAtUtc = stampResult.StampedAtUtc
        };
    }
}
