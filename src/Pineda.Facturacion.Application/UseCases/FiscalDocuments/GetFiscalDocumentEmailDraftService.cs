using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class GetFiscalDocumentEmailDraftService
{
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IFiscalStampRepository _fiscalStampRepository;
    private readonly IFiscalReceiverRepository _fiscalReceiverRepository;

    public GetFiscalDocumentEmailDraftService(
        IFiscalDocumentRepository fiscalDocumentRepository,
        IFiscalStampRepository fiscalStampRepository,
        IFiscalReceiverRepository fiscalReceiverRepository)
    {
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _fiscalStampRepository = fiscalStampRepository;
        _fiscalReceiverRepository = fiscalReceiverRepository;
    }

    public async Task<GetFiscalDocumentEmailDraftResult> ExecuteAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
    {
        var fiscalDocument = await _fiscalDocumentRepository.GetByIdAsync(fiscalDocumentId, cancellationToken);
        if (fiscalDocument is null)
        {
            return new GetFiscalDocumentEmailDraftResult
            {
                Outcome = GetFiscalDocumentEmailDraftOutcome.NotFound,
                ErrorMessage = $"Fiscal document '{fiscalDocumentId}' was not found."
            };
        }

        var fiscalStamp = await _fiscalStampRepository.GetByFiscalDocumentIdAsync(fiscalDocumentId, cancellationToken);
        if (fiscalStamp is null
            || fiscalStamp.Status != FiscalStampStatus.Succeeded
            || string.IsNullOrWhiteSpace(fiscalStamp.XmlContent)
            || string.IsNullOrWhiteSpace(fiscalStamp.Uuid))
        {
            return new GetFiscalDocumentEmailDraftResult
            {
                Outcome = GetFiscalDocumentEmailDraftOutcome.NotStamped,
                ErrorMessage = "Fiscal document must be stamped successfully before emailing it."
            };
        }

        var fiscalReceiver = await _fiscalReceiverRepository.GetByIdAsync(fiscalDocument.FiscalReceiverId, cancellationToken);
        var documentLabel = string.IsNullOrWhiteSpace(fiscalDocument.Series) && string.IsNullOrWhiteSpace(fiscalDocument.Folio)
            ? $"CFDI {fiscalStamp.Uuid}"
            : $"CFDI {fiscalDocument.Series}{fiscalDocument.Folio}";

        return new GetFiscalDocumentEmailDraftResult
        {
            Outcome = GetFiscalDocumentEmailDraftOutcome.Found,
            IsSuccess = true,
            DefaultRecipientEmail = NormalizeDraftRecipientEmail(fiscalReceiver?.Email),
            SuggestedSubject = $"{documentLabel} timbrado",
            SuggestedBody = $"Adjuntamos el CFDI timbrado {documentLabel} en formatos XML y PDF."
        };
    }

    private static string? NormalizeDraftRecipientEmail(string? value)
    {
        var normalizedValue = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            return null;
        }

        return EmailRecipientParser.FindInvalidRecipients([normalizedValue]).Count == 0
            ? EmailRecipientParser.JoinNormalizedRecipients([normalizedValue])
            : normalizedValue;
    }
}
