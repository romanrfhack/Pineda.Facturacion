namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class StampAndEmailFiscalDocumentService
{
    private readonly StampFiscalDocumentService _stampFiscalDocumentService;
    private readonly GetFiscalDocumentEmailDraftService _getFiscalDocumentEmailDraftService;
    private readonly SendFiscalDocumentEmailService _sendFiscalDocumentEmailService;

    public StampAndEmailFiscalDocumentService(
        StampFiscalDocumentService stampFiscalDocumentService,
        GetFiscalDocumentEmailDraftService getFiscalDocumentEmailDraftService,
        SendFiscalDocumentEmailService sendFiscalDocumentEmailService)
    {
        _stampFiscalDocumentService = stampFiscalDocumentService;
        _getFiscalDocumentEmailDraftService = getFiscalDocumentEmailDraftService;
        _sendFiscalDocumentEmailService = sendFiscalDocumentEmailService;
    }

    public async Task<StampAndEmailFiscalDocumentResult> ExecuteAsync(
        StampAndEmailFiscalDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        var stampResult = await _stampFiscalDocumentService.ExecuteAsync(
            new StampFiscalDocumentCommand
            {
                FiscalDocumentId = command.FiscalDocumentId,
                RetryRejected = command.RetryRejected
            },
            cancellationToken);

        var result = MapStampResult(stampResult);
        if (!result.Stamped)
        {
            return result;
        }

        var draftResult = await _getFiscalDocumentEmailDraftService.ExecuteAsync(command.FiscalDocumentId, cancellationToken);
        var defaultRecipientEmail = draftResult.DefaultRecipientEmail?.Trim();
        if (string.IsNullOrWhiteSpace(defaultRecipientEmail))
        {
            result.EmailStatus = StampAndEmailFiscalDocumentEmailStatus.Missing;
            result.EmailMessage = "El receptor no tiene un email registrado.";
            return result;
        }

        var invalidRecipients = SendFiscalDocumentEmailService.FindInvalidRecipients([defaultRecipientEmail]);
        if (invalidRecipients.Count > 0)
        {
            result.EmailStatus = StampAndEmailFiscalDocumentEmailStatus.Invalid;
            result.InvalidRecipients = invalidRecipients;
            result.EmailMessage = invalidRecipients.Count == 1
                ? $"Correo inválido registrado: {invalidRecipients[0]}."
                : $"Correos inválidos registrados: {string.Join(", ", invalidRecipients)}.";
            return result;
        }

        var normalizedRecipients = SendFiscalDocumentEmailService.NormalizeRecipients([defaultRecipientEmail]);
        if (normalizedRecipients.Count == 0)
        {
            result.EmailStatus = StampAndEmailFiscalDocumentEmailStatus.Invalid;
            result.InvalidRecipients = [defaultRecipientEmail];
            result.EmailMessage = "El email registrado del receptor no es válido.";
            return result;
        }

        result.EmailAttempted = true;
        result.EmailRecipients = normalizedRecipients;

        try
        {
            var sendResult = await _sendFiscalDocumentEmailService.ExecuteAsync(
                new SendFiscalDocumentEmailCommand
                {
                    FiscalDocumentId = command.FiscalDocumentId,
                    Recipients = normalizedRecipients,
                    Subject = draftResult.SuggestedSubject,
                    Body = draftResult.SuggestedBody
                },
                cancellationToken);

            result.EmailSent = sendResult.IsSuccess;
            result.EmailStatus = sendResult.IsSuccess
                ? StampAndEmailFiscalDocumentEmailStatus.Sent
                : StampAndEmailFiscalDocumentEmailStatus.Failed;
            result.EmailRecipients = sendResult.Recipients;
            result.EmailSentAtUtc = sendResult.SentAtUtc;
            result.EmailMessage = sendResult.IsSuccess
                ? $"El correo fue enviado automáticamente a: {string.Join(", ", sendResult.Recipients)}."
                : sendResult.ErrorMessage ?? sendResult.SupportMessage ?? "No fue posible enviar el CFDI por correo.";
        }
        catch (Exception exception)
        {
            result.EmailSent = false;
            result.EmailStatus = StampAndEmailFiscalDocumentEmailStatus.Failed;
            result.EmailMessage = $"No fue posible enviar el CFDI por correo por un error inesperado del servidor. {exception.Message}";
        }

        return result;
    }

    private static StampAndEmailFiscalDocumentResult MapStampResult(StampFiscalDocumentResult stampResult)
    {
        return new StampAndEmailFiscalDocumentResult
        {
            FiscalDocumentId = stampResult.FiscalDocumentId,
            Stamped = stampResult.Outcome == StampFiscalDocumentOutcome.Stamped,
            FiscalDocumentStatus = stampResult.FiscalDocumentStatus,
            StampOutcome = stampResult.Outcome,
            IsSuccess = stampResult.IsSuccess,
            ErrorMessage = stampResult.ErrorMessage,
            ProviderMessage = stampResult.ProviderMessage,
            SupportMessage = stampResult.SupportMessage,
            FiscalStampId = stampResult.FiscalStampId,
            Uuid = stampResult.Uuid,
            StampedAtUtc = stampResult.StampedAtUtc,
            EmailAttempted = false,
            EmailSent = false,
            EmailStatus = StampAndEmailFiscalDocumentEmailStatus.NotAttempted
        };
    }
}
