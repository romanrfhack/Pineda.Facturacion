using Pineda.Facturacion.Application.Abstractions.Legacy;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.Models.Legacy;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.Orders;

public sealed class OrderDebtSummaryDocumentFactory
{
    internal const string MixedCustomersErrorMessage = "No se puede enviar el resumen porque la selección contiene órdenes de distintos clientes. Selecciona únicamente órdenes del mismo cliente.";
    internal const string ReceiverRfcMismatchErrorMessage = "El RFC del receptor seleccionado no coincide con el RFC de las órdenes seleccionadas.";
    internal const string CustomerIdentityUnavailableErrorMessage = "No se puede enviar el resumen porque no hay datos suficientes para validar que todas las órdenes pertenezcan al mismo cliente.";

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

        var legacyOrders = new List<LegacyOrderReadModel>(requestedOrderIds.Length);
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

        var customerValidationError = ValidateCustomerSelection(legacyOrders, receiver);
        if (customerValidationError is not null)
        {
            return ValidationFailure(customerValidationError);
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

        var invalidRecipients = EmailRecipientParser.FindInvalidRecipients(command.To)
            .Concat(EmailRecipientParser.FindInvalidRecipients(command.Cc))
            .Concat(EmailRecipientParser.FindInvalidRecipients(command.Bcc))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (invalidRecipients.Length > 0)
        {
            return ValidationFailure($"Correo inválido: {string.Join(", ", invalidRecipients)}.");
        }

        var to = OrderDebtSummaryComposer.NormalizeRecipients(command.To);
        if (to.Count == 0)
        {
            var defaultInvalidRecipients = EmailRecipientParser.FindInvalidRecipients(string.IsNullOrWhiteSpace(receiver.Email) ? [] : [receiver.Email]);
            if (defaultInvalidRecipients.Count > 0)
            {
                return ValidationFailure($"Correo inválido: {string.Join(", ", defaultInvalidRecipients)}.");
            }

            to = OrderDebtSummaryComposer.NormalizeRecipients(string.IsNullOrWhiteSpace(receiver.Email) ? [] : [receiver.Email]);
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

    private static string? ValidateCustomerSelection(
        IReadOnlyCollection<LegacyOrderReadModel> orders,
        FiscalReceiver receiver)
    {
        var orderRfcs = orders
            .Select(order => NormalizeRfc(order.CustomerRfc))
            .Where(rfc => rfc is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (orderRfcs.Length > 1)
        {
            return MixedCustomersErrorMessage;
        }

        var anyMissingRfc = orders.Any(order => NormalizeRfc(order.CustomerRfc) is null);
        if (anyMissingRfc)
        {
            var legacyCustomerIds = orders
                .Select(order => NormalizeStableCustomerId(order.CustomerLegacyId))
                .ToArray();

            if (legacyCustomerIds.All(customerId => customerId is not null))
            {
                if (legacyCustomerIds.Cast<string>().Distinct(StringComparer.Ordinal).Count() > 1)
                {
                    return MixedCustomersErrorMessage;
                }
            }
            else
            {
                var customerNames = orders
                    .Select(order => NormalizeCustomerName(order.CustomerName))
                    .ToArray();

                if (customerNames.Any(customerName => customerName is null))
                {
                    return CustomerIdentityUnavailableErrorMessage;
                }

                if (customerNames.Cast<string>().Distinct(StringComparer.Ordinal).Count() > 1)
                {
                    return MixedCustomersErrorMessage;
                }
            }
        }

        if (orderRfcs.Length == 1)
        {
            var receiverRfc = NormalizeRfc(receiver.Rfc);
            if (!string.Equals(orderRfcs[0], receiverRfc, StringComparison.Ordinal))
            {
                return ReceiverRfcMismatchErrorMessage;
            }
        }

        return null;
    }

    private static string? NormalizeRfc(string? value)
    {
        var candidate = RemoveWhitespace(value);
        return string.IsNullOrWhiteSpace(candidate) ? null : candidate.ToUpperInvariant();
    }

    private static string? NormalizeStableCustomerId(string? value)
    {
        var candidate = RemoveWhitespace(value);
        if (string.IsNullOrWhiteSpace(candidate) || string.Equals(candidate, "0", StringComparison.Ordinal))
        {
            return null;
        }

        return candidate.ToUpperInvariant();
    }

    private static string? NormalizeCustomerName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = string.Join(
            " ",
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized.ToUpperInvariant();
    }

    private static string RemoveWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value.Where(character => !char.IsWhiteSpace(character)).ToArray());
    }
}

public sealed class OrderDebtSummaryDocumentBuildResult
{
    public OrderDebtSummaryOutcome Outcome { get; init; }

    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public OrderDebtSummaryDocument? Document { get; init; }
}
