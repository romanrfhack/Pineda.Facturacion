using Pineda.Facturacion.Application.Abstractions.Legacy;
using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.Orders;

public sealed class OrderDebtSummaryDocumentFactory
{
    private readonly ILegacyOrderReader _legacyOrderReader;
    private readonly IFiscalReceiverRepository _receiverRepository;
    private readonly IIssuerProfileRepository _issuerProfileRepository;
    private readonly IImportedLegacyOrderLookupRepository _importedLegacyOrderLookupRepository;
    private readonly TimeProvider _timeProvider;

    public OrderDebtSummaryDocumentFactory(
        ILegacyOrderReader legacyOrderReader,
        IFiscalReceiverRepository receiverRepository,
        IIssuerProfileRepository issuerProfileRepository,
        IImportedLegacyOrderLookupRepository importedLegacyOrderLookupRepository,
        TimeProvider timeProvider)
    {
        _legacyOrderReader = legacyOrderReader;
        _receiverRepository = receiverRepository;
        _issuerProfileRepository = issuerProfileRepository;
        _importedLegacyOrderLookupRepository = importedLegacyOrderLookupRepository;
        _timeProvider = timeProvider;
    }

    public async Task<OrderDebtSummaryDocumentBuildResult> BuildDocumentAsync(
        OrderDebtSummaryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.ReceiverId <= 0)
        {
            return ValidationFailure("Selecciona un receptor válido para continuar.");
        }

        if (!OrderDebtSummaryComposer.TryParseFormat(command.Format, out var format))
        {
            return ValidationFailure($"El formato '{command.Format}' no está soportado para este resumen.");
        }

        var receiver = await _receiverRepository.GetByIdAsync(command.ReceiverId, cancellationToken);
        if (receiver is null)
        {
            return new OrderDebtSummaryDocumentBuildResult
            {
                Outcome = OrderDebtSummaryOutcome.NotFound,
                ErrorMessage = "El receptor seleccionado no existe o no está activo."
            };
        }

        var requestedOrderIds = command.LegacyOrderIds
            .Where(orderId => !string.IsNullOrWhiteSpace(orderId))
            .Select(orderId => orderId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (requestedOrderIds.Length == 0)
        {
            return ValidationFailure("Selecciona al menos una orden para continuar.");
        }

        var legacyOrders = new List<Pineda.Facturacion.Application.Models.Legacy.LegacyOrderReadModel>(requestedOrderIds.Length);
        var missingOrderIds = new List<string>();
        foreach (var legacyOrderId in requestedOrderIds)
        {
            var legacyOrder = await _legacyOrderReader.GetByIdAsync(legacyOrderId, cancellationToken);
            if (legacyOrder is null)
            {
                missingOrderIds.Add(legacyOrderId);
                continue;
            }

            legacyOrders.Add(legacyOrder);
        }

        if (missingOrderIds.Count > 0)
        {
            return ValidationFailure($"Algunas órdenes seleccionadas ya no están disponibles: {string.Join(", ", missingOrderIds)}.");
        }

        var lookup = await _importedLegacyOrderLookupRepository.GetByLegacyOrderIdsAsync(requestedOrderIds, cancellationToken);
        var orders = legacyOrders
            .OrderBy(order => Array.IndexOf(requestedOrderIds, order.LegacyOrderId))
            .Select(order =>
            {
                lookup.TryGetValue(order.LegacyOrderId, out var importedLookup);
                return OrderDebtSummaryComposer.MapOrder(order, importedLookup);
            })
            .ToArray();

        var invalidRecipients = FindInvalidRecipients(command.To)
            .Concat(FindInvalidRecipients(command.Cc))
            .Concat(FindInvalidRecipients(command.Bcc))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (invalidRecipients.Length > 0)
        {
            return ValidationFailure($"Correo inválido: {string.Join(", ", invalidRecipients)}.");
        }

        var to = OrderDebtSummaryComposer.NormalizeRecipients(command.To);
        if (to.Count == 0 && OrderDebtSummaryComposer.IsValidEmailAddress(receiver.Email))
        {
            to = [receiver.Email!.Trim()];
        }

        if (to.Count == 0)
        {
            return ValidationFailure("Captura al menos un correo destinatario válido para continuar.");
        }

        var subject = command.Subject?.Trim();
        if (string.IsNullOrWhiteSpace(subject))
        {
            return ValidationFailure("Captura el asunto del correo.");
        }

        var message = command.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return ValidationFailure("Captura el mensaje inicial.");
        }

        var issuer = await _issuerProfileRepository.GetActiveAsync(cancellationToken);
        var document = new OrderDebtSummaryDocument
        {
            ReceiverId = receiver.Id,
            Format = format,
            Receiver = new OrderDebtSummaryParty
            {
                Id = receiver.Id,
                LegalName = receiver.LegalName,
                Rfc = receiver.Rfc,
                Email = receiver.Email,
                FiscalRegimeCode = receiver.FiscalRegimeCode,
                PostalCode = receiver.PostalCode
            },
            Issuer = new OrderDebtSummaryParty
            {
                Id = issuer?.Id,
                LegalName = issuer?.LegalName ?? "Empresa emisora",
                Rfc = issuer?.Rfc ?? string.Empty,
                FiscalRegimeCode = issuer?.FiscalRegimeCode,
                PostalCode = issuer?.PostalCode
            },
            Orders = orders,
            Selection = OrderDebtSummaryComposer.BuildSelectionSummary(orders),
            To = to,
            Cc = OrderDebtSummaryComposer.NormalizeRecipients(command.Cc),
            Bcc = OrderDebtSummaryComposer.NormalizeRecipients(command.Bcc),
            Subject = subject,
            Message = message,
            Options = command.Options,
            GeneratedAtUtc = _timeProvider.GetUtcNow().UtcDateTime
        };

        return new OrderDebtSummaryDocumentBuildResult
        {
            Outcome = OrderDebtSummaryOutcome.Found,
            IsSuccess = true,
            Document = document
        };
    }

    private static OrderDebtSummaryDocumentBuildResult ValidationFailure(string errorMessage)
    {
        return new OrderDebtSummaryDocumentBuildResult
        {
            Outcome = OrderDebtSummaryOutcome.ValidationFailed,
            ErrorMessage = errorMessage
        };
    }

    private static IReadOnlyList<string> FindInvalidRecipients(IEnumerable<string>? recipients)
    {
        if (recipients is null)
        {
            return [];
        }

        var invalid = new List<string>();
        foreach (var recipient in recipients)
        {
            var candidate = recipient?.Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (!OrderDebtSummaryComposer.IsValidEmailAddress(candidate))
            {
                invalid.Add(candidate);
            }
        }

        return invalid;
    }
}

public sealed class OrderDebtSummaryDocumentBuildResult
{
    public OrderDebtSummaryOutcome Outcome { get; init; }

    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public OrderDebtSummaryDocument? Document { get; init; }
}
