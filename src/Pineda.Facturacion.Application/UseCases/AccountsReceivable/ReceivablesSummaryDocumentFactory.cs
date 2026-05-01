using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class ReceivablesSummaryDocumentFactory
{
    private readonly SearchAccountsReceivablePortfolioService _portfolioService;
    private readonly IFiscalReceiverRepository _receiverRepository;
    private readonly IIssuerProfileRepository _issuerProfileRepository;
    private readonly TimeProvider _timeProvider;

    public ReceivablesSummaryDocumentFactory(
        SearchAccountsReceivablePortfolioService portfolioService,
        IFiscalReceiverRepository receiverRepository,
        IIssuerProfileRepository issuerProfileRepository,
        TimeProvider timeProvider)
    {
        _portfolioService = portfolioService;
        _receiverRepository = receiverRepository;
        _issuerProfileRepository = issuerProfileRepository;
        _timeProvider = timeProvider;
    }

    public async Task<GetReceivablesSummaryCandidatesResult> GetCandidatesAsync(
        long receiverId,
        CancellationToken cancellationToken = default)
    {
        if (receiverId <= 0)
        {
            return new GetReceivablesSummaryCandidatesResult
            {
                Outcome = ReceivablesSummaryOutcome.NotFound,
                ErrorMessage = "Receiver id is required."
            };
        }

        var receiver = await _receiverRepository.GetByIdAsync(receiverId, cancellationToken);
        var issuer = await _issuerProfileRepository.GetActiveAsync(cancellationToken);
        var portfolio = await _portfolioService.ExecuteAsync(
            new SearchAccountsReceivablePortfolioFilter
            {
                FiscalReceiverId = receiverId,
                HasPendingBalance = true
            },
            cancellationToken);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var invoices = portfolio.Items
            .Where(ReceivablesSummaryComposer.IsEligible)
            .Select(item => ReceivablesSummaryComposer.MapCandidate(item, now))
            .OrderByDescending(x => x.IsOverdue)
            .ThenBy(x => x.DueAtUtc ?? DateTime.MaxValue)
            .ThenBy(x => x.AccountsReceivableInvoiceId)
            .ToArray();

        if (receiver is null && invoices.Length == 0)
        {
            return new GetReceivablesSummaryCandidatesResult
            {
                Outcome = ReceivablesSummaryOutcome.NotFound,
                ErrorMessage = $"Receiver '{receiverId}' was not found."
            };
        }

        var receiverParty = new ReceivablesSummaryParty
        {
            Id = receiver?.Id ?? receiverId,
            LegalName = receiver?.LegalName ?? portfolio.Items.FirstOrDefault()?.ReceiverLegalName ?? "Receptor",
            Rfc = receiver?.Rfc ?? portfolio.Items.FirstOrDefault()?.ReceiverRfc ?? string.Empty,
            Email = receiver?.Email,
            FiscalRegimeCode = receiver?.FiscalRegimeCode,
            PostalCode = receiver?.PostalCode
        };

        var issuerParty = new ReceivablesSummaryParty
        {
            Id = issuer?.Id,
            LegalName = issuer?.LegalName ?? "Empresa emisora",
            Rfc = issuer?.Rfc ?? string.Empty,
            FiscalRegimeCode = issuer?.FiscalRegimeCode,
            PostalCode = issuer?.PostalCode
        };
        var issuerLogo = BuildIssuerLogo(issuer);

        return new GetReceivablesSummaryCandidatesResult
        {
            Outcome = ReceivablesSummaryOutcome.Found,
            IsSuccess = true,
            Receiver = receiverParty,
            Issuer = issuerParty,
            IssuerLogo = issuerLogo,
            Invoices = invoices,
            DefaultTo = string.IsNullOrWhiteSpace(receiverParty.Email) ? [] : [receiverParty.Email.Trim()],
            DefaultSubject = ReceivablesSummaryComposer.BuildDefaultSubject(receiverParty.LegalName),
            DefaultMessage = ReceivablesSummaryComposer.BuildDefaultMessage(receiverParty.LegalName)
        };
    }

    public async Task<ReceivablesSummaryDocumentBuildResult> BuildDocumentAsync(
        ReceivablesSummaryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!ReceivablesSummaryComposer.TryParseScope(command.Scope, out var scope))
        {
            return ValidationFailure($"Unknown summary scope '{command.Scope}'.");
        }

        if (!ReceivablesSummaryComposer.TryParseFormat(command.Format, out var format))
        {
            return ValidationFailure($"Unknown summary format '{command.Format}'.");
        }

        var candidatesResult = await GetCandidatesAsync(command.ReceiverId, cancellationToken);
        if (!candidatesResult.IsSuccess)
        {
            return new ReceivablesSummaryDocumentBuildResult
            {
                Outcome = candidatesResult.Outcome,
                ErrorMessage = candidatesResult.ErrorMessage
            };
        }

        var requestedInvoiceIds = command.InvoiceIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        var selectedInvoices = ReceivablesSummaryComposer.SelectInvoices(
                candidatesResult.Invoices,
                scope,
                requestedInvoiceIds)
            .ToArray();

        if ((scope is ReceivablesSummaryScope.Manual or ReceivablesSummaryScope.CurrentSelection)
            && requestedInvoiceIds.Length != selectedInvoices.Length)
        {
            return ValidationFailure("Algunas facturas seleccionadas no son elegibles o ya no pertenecen al receptor.");
        }

        if (selectedInvoices.Length == 0)
        {
            return ValidationFailure(scope == ReceivablesSummaryScope.Overdue
                ? "No existen facturas vencidas elegibles para este receptor."
                : "No hay facturas seleccionadas para enviar.");
        }

        var invalidRecipients = FindInvalidRecipients(command.To)
            .Concat(FindInvalidRecipients(command.Cc))
            .Concat(FindInvalidRecipients(command.Bcc))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (invalidRecipients.Length > 0)
        {
            return ValidationFailure($"Correo inválido: {string.Join(", ", invalidRecipients)}.");
        }

        var to = ReceivablesSummaryComposer.NormalizeRecipients(command.To);
        if (to.Count == 0)
        {
            return ValidationFailure("Captura al menos un correo destinatario válido para continuar.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
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

        var document = new ReceivablesSummaryDocument
        {
            ReceiverId = command.ReceiverId,
            Scope = scope,
            Format = format,
            Receiver = candidatesResult.Receiver,
            Issuer = candidatesResult.Issuer,
            IssuerLogo = candidatesResult.IssuerLogo,
            Invoices = selectedInvoices,
            Selection = ReceivablesSummaryComposer.BuildSelectionSummary(selectedInvoices),
            To = to,
            Cc = ReceivablesSummaryComposer.NormalizeRecipients(command.Cc),
            Bcc = ReceivablesSummaryComposer.NormalizeRecipients(command.Bcc),
            Subject = subject,
            Message = message,
            IncludeOptions = command.IncludeOptions,
            GeneratedAtUtc = now
        };

        return new ReceivablesSummaryDocumentBuildResult
        {
            Outcome = ReceivablesSummaryOutcome.Found,
            IsSuccess = true,
            Document = document
        };
    }

    private static ReceivablesSummaryLogo? BuildIssuerLogo(Domain.Entities.IssuerProfile? issuer)
    {
        if (issuer?.LogoData is not { Length: > 0 })
        {
            return null;
        }

        var contentType = string.IsNullOrWhiteSpace(issuer.LogoContentType)
            ? "application/octet-stream"
            : issuer.LogoContentType.Trim();

        return new ReceivablesSummaryLogo
        {
            ContentId = ReceivablesSummaryComposer.IssuerLogoContentId,
            FileName = string.IsNullOrWhiteSpace(issuer.LogoFileName) ? "issuer-logo" : issuer.LogoFileName.Trim(),
            ContentType = contentType,
            Content = issuer.LogoData
        };
    }

    private static ReceivablesSummaryDocumentBuildResult ValidationFailure(string errorMessage)
    {
        return new ReceivablesSummaryDocumentBuildResult
        {
            Outcome = ReceivablesSummaryOutcome.ValidationFailed,
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

            if (!ReceivablesSummaryComposer.IsValidEmailAddress(candidate))
            {
                invalid.Add(candidate);
            }
        }

        return invalid;
    }
}

public sealed class ReceivablesSummaryDocumentBuildResult
{
    public ReceivablesSummaryOutcome Outcome { get; init; }

    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public ReceivablesSummaryDocument? Document { get; init; }
}
