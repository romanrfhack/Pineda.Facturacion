using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Hosting;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.UseCases.AccountsReceivable;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Operations.AccountsReceivable;

public sealed class BackfillMissingAccountsReceivableInvoicesService
{
    private const string ProductionGuardEnvironmentVariable = "ALLOW_PROD_MISSING_AR_BACKFILL";
    private const string BackfillActionType = "AccountsReceivableInvoice.BackfillMissing";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly BillingDbContext _dbContext;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly CreateAccountsReceivableInvoiceFromFiscalDocumentService _createAccountsReceivableInvoiceFromFiscalDocumentService;

    public BackfillMissingAccountsReceivableInvoicesService(
        BillingDbContext dbContext,
        IHostEnvironment hostEnvironment,
        CreateAccountsReceivableInvoiceFromFiscalDocumentService createAccountsReceivableInvoiceFromFiscalDocumentService)
    {
        _dbContext = dbContext;
        _hostEnvironment = hostEnvironment;
        _createAccountsReceivableInvoiceFromFiscalDocumentService = createAccountsReceivableInvoiceFromFiscalDocumentService;
    }

    public async Task<BackfillMissingAccountsReceivableInvoicesResult> ExecuteAsync(
        BackfillMissingAccountsReceivableInvoicesCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var fiscalDocumentIds = NormalizeFiscalDocumentIds(command.FiscalDocumentIds);
        if (fiscalDocumentIds.Count == 0)
        {
            return Failure(command.CommitChanges, _hostEnvironment.EnvironmentName, "At least one fiscal document id is required.");
        }

        var databaseName = ResolveDatabaseName();
        var batchId = ResolveBatchId(command);
        var requestedBy = ResolveRequestedBy(command);

        var evaluations = new List<BackfillMissingAccountsReceivableInvoiceEvaluation>(fiscalDocumentIds.Count);
        foreach (var fiscalDocumentId in fiscalDocumentIds)
        {
            evaluations.Add(await EvaluateAsync(fiscalDocumentId, cancellationToken));
        }

        var items = evaluations
            .Select(x => x.Item)
            .OrderBy(x => x.FiscalDocumentId)
            .ToList();

        var result = BuildResult(
            command,
            _hostEnvironment.EnvironmentName,
            databaseName,
            batchId,
            items);

        if (!command.CommitChanges)
        {
            result.IsSuccess = true;
            return result;
        }

        if (string.IsNullOrWhiteSpace(command.ExpectedDatabaseName))
        {
            return Failure(result, "Commit blocked because --expected-database-name is required.");
        }

        if (!string.Equals(command.ExpectedDatabaseName, databaseName, StringComparison.Ordinal))
        {
            return Failure(
                result,
                $"Commit blocked because database '{databaseName ?? "(unknown)"}' does not match expected database '{command.ExpectedDatabaseName}'.");
        }

        if (string.IsNullOrWhiteSpace(requestedBy))
        {
            return Failure(result, "Commit blocked because --requested-by is required.");
        }

        if (_hostEnvironment.IsProduction() && !command.AllowProductionCommit)
        {
            return Failure(result, $"Commit blocked in Production. Set {ProductionGuardEnvironmentVariable}=true to proceed.");
        }

        var blockedIds = items
            .Where(x => string.Equals(x.Decision, BackfillDecision.Blocked, StringComparison.Ordinal))
            .Select(x => x.FiscalDocumentId)
            .ToArray();
        if (blockedIds.Length > 0)
        {
            return Failure(
                result,
                $"Commit blocked because the selection includes unsafe fiscal_document ids: {string.Join(", ", blockedIds)}.");
        }

        await using var transaction = await BeginTransactionIfSupportedAsync(cancellationToken);

        foreach (var evaluation in evaluations.Where(x => x.IsEligible))
        {
            var item = evaluation.Item;
            var createResult = await _createAccountsReceivableInvoiceFromFiscalDocumentService.ExecuteAsync(
                new CreateAccountsReceivableInvoiceFromFiscalDocumentCommand
                {
                    FiscalDocumentId = evaluation.FiscalDocument!.Id
                },
                cancellationToken);

            if (createResult.Outcome == CreateAccountsReceivableInvoiceFromFiscalDocumentOutcome.Created
                && createResult.AccountsReceivableInvoice is not null)
            {
                item.Outcome = "created";
                item.Message = "Accounts receivable invoice created.";
                item.AccountsReceivableInvoiceId = createResult.AccountsReceivableInvoice.Id;
                item.ProposedStatus = createResult.AccountsReceivableInvoice.Status.ToString();
                item.ProposedTotal = createResult.AccountsReceivableInvoice.Total;
                item.ProposedPaidTotal = createResult.AccountsReceivableInvoice.PaidTotal;
                item.ProposedOutstandingBalance = createResult.AccountsReceivableInvoice.OutstandingBalance;
                item.ProposedDueAtUtc = createResult.AccountsReceivableInvoice.DueAtUtc;

                await _dbContext.AuditEvents.AddAsync(
                    BuildAuditEvent(
                        command,
                        requestedBy,
                        batchId,
                        evaluation,
                        createResult.AccountsReceivableInvoice,
                        databaseName),
                    cancellationToken);

                continue;
            }

            if (createResult.Outcome == CreateAccountsReceivableInvoiceFromFiscalDocumentOutcome.Conflict)
            {
                var existingInvoice = await _dbContext.AccountsReceivableInvoices
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.FiscalDocumentId == evaluation.FiscalDocument!.Id, cancellationToken);

                if (existingInvoice is not null)
                {
                    item.Decision = BackfillDecision.Skipped;
                    item.Outcome = "skipped_existing_invoice_after_recheck";
                    item.Message = "Accounts receivable invoice already exists after re-check.";
                    item.AccountsReceivableInvoiceId = existingInvoice.Id;
                    item.ProposedStatus = existingInvoice.Status.ToString();
                    item.ProposedTotal = existingInvoice.Total;
                    item.ProposedPaidTotal = existingInvoice.PaidTotal;
                    item.ProposedOutstandingBalance = existingInvoice.OutstandingBalance;
                    item.ProposedDueAtUtc = existingInvoice.DueAtUtc;
                    continue;
                }
            }

            return Failure(
                result,
                createResult.ErrorMessage
                ?? $"Commit failed while creating the accounts receivable invoice for fiscal document '{evaluation.FiscalDocument!.Id}'.");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        result.IsSuccess = true;
        result.CreatedCount = items.Count(x => string.Equals(x.Outcome, "created", StringComparison.Ordinal));
        result.SkippedCount = items.Count(x => string.Equals(x.Decision, BackfillDecision.Skipped, StringComparison.Ordinal));
        result.BlockedCount = items.Count(x => string.Equals(x.Decision, BackfillDecision.Blocked, StringComparison.Ordinal));
        return result;
    }

    private async Task<BackfillMissingAccountsReceivableInvoiceEvaluation> EvaluateAsync(
        long fiscalDocumentId,
        CancellationToken cancellationToken)
    {
        var fiscalDocument = await _dbContext.FiscalDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == fiscalDocumentId, cancellationToken);

        if (fiscalDocument is null)
        {
            return Blocked(fiscalDocumentId, "blocked_not_found", "Fiscal document was not found.");
        }

        var item = new BackfillMissingAccountsReceivableInvoiceItemResult
        {
            FiscalDocumentId = fiscalDocument.Id,
            BillingDocumentId = fiscalDocument.BillingDocumentId,
            DocumentType = NormalizeOptional(fiscalDocument.DocumentType),
            PaymentMethodSat = NormalizeOptional(fiscalDocument.PaymentMethodSat),
            PaymentFormSat = NormalizeOptional(fiscalDocument.PaymentFormSat),
            CurrencyCode = NormalizeOptional(fiscalDocument.CurrencyCode),
            ReceiverRfc = NormalizeOptional(fiscalDocument.ReceiverRfc),
            ReceiverLegalName = fiscalDocument.ReceiverLegalName,
            FiscalStatus = fiscalDocument.Status.ToString(),
            IssuedAtUtc = fiscalDocument.IssuedAtUtc
        };

        if (!string.Equals(NormalizeOptional(fiscalDocument.DocumentType), "I", StringComparison.Ordinal))
        {
            return Blocked(item, fiscalDocument, null, null, null, "blocked_document_type_not_income", "Fiscal document must be type I.");
        }

        if (fiscalDocument.Status != FiscalDocumentStatus.Stamped)
        {
            return Blocked(item, fiscalDocument, null, null, null, "blocked_fiscal_document_not_stamped", "Fiscal document must be stamped.");
        }

        var fiscalStamp = await _dbContext.FiscalStamps
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.FiscalDocumentId == fiscalDocument.Id, cancellationToken);
        item.FiscalStampId = fiscalStamp?.Id;
        item.Uuid = fiscalStamp?.Uuid;

        if (fiscalStamp is null || fiscalStamp.Status != FiscalStampStatus.Succeeded || string.IsNullOrWhiteSpace(fiscalStamp.Uuid))
        {
            return Blocked(item, fiscalDocument, fiscalStamp, null, null, "blocked_missing_succeeded_stamp", "A succeeded fiscal stamp with UUID is required.");
        }

        var existingInvoices = await _dbContext.AccountsReceivableInvoices
            .AsNoTracking()
            .Where(x => x.FiscalDocumentId == fiscalDocument.Id || x.FiscalStampId == fiscalStamp.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var existingInvoiceForFiscalDocument = existingInvoices
            .FirstOrDefault(x => x.FiscalDocumentId == fiscalDocument.Id);
        if (existingInvoiceForFiscalDocument is not null)
        {
            item.Decision = BackfillDecision.Skipped;
            item.Outcome = "skipped_existing_invoice";
            item.Message = "Accounts receivable invoice already exists.";
            item.AccountsReceivableInvoiceId = existingInvoiceForFiscalDocument.Id;
            item.ProposedStatus = existingInvoiceForFiscalDocument.Status.ToString();
            item.ProposedTotal = existingInvoiceForFiscalDocument.Total;
            item.ProposedPaidTotal = existingInvoiceForFiscalDocument.PaidTotal;
            item.ProposedOutstandingBalance = existingInvoiceForFiscalDocument.OutstandingBalance;
            item.ProposedDueAtUtc = existingInvoiceForFiscalDocument.DueAtUtc;

            return new BackfillMissingAccountsReceivableInvoiceEvaluation
            {
                FiscalDocument = fiscalDocument,
                FiscalStamp = fiscalStamp,
                ExistingInvoice = existingInvoiceForFiscalDocument,
                Item = item
            };
        }

        if (existingInvoices.Count > 0)
        {
            return Blocked(
                item,
                fiscalDocument,
                fiscalStamp,
                null,
                existingInvoices.First(),
                "blocked_duplicate_invoice_by_fiscal_stamp",
                "A different accounts receivable invoice already uses the same fiscal stamp.");
        }

        var activeCancellationCount = await _dbContext.FiscalCancellations
            .AsNoTracking()
            .CountAsync(
                x => x.FiscalDocumentId == fiscalDocument.Id
                    && (x.Status == FiscalCancellationStatus.Requested || x.Status == FiscalCancellationStatus.Cancelled),
                cancellationToken);
        if (activeCancellationCount > 0)
        {
            return Blocked(item, fiscalDocument, fiscalStamp, null, null, "blocked_fiscal_cancellation_present", "Fiscal document has an active cancellation record.");
        }

        if (!string.Equals(NormalizeOptional(fiscalDocument.PaymentMethodSat), "PPD", StringComparison.Ordinal))
        {
            return Blocked(item, fiscalDocument, fiscalStamp, null, null, "blocked_payment_method_not_ppd", "Fiscal document must use MetodoPago PPD.");
        }

        if (!string.Equals(NormalizeOptional(fiscalDocument.PaymentFormSat), "99", StringComparison.Ordinal))
        {
            return Blocked(item, fiscalDocument, fiscalStamp, null, null, "blocked_payment_form_not_99", "Fiscal document must use FormaPago 99.");
        }

        if (!fiscalDocument.IsCreditSale)
        {
            return Blocked(item, fiscalDocument, fiscalStamp, null, null, "blocked_not_credit_sale", "Fiscal document must be marked as credit sale.");
        }

        if (!fiscalDocument.CreditDays.HasValue || fiscalDocument.CreditDays.Value <= 0)
        {
            return Blocked(item, fiscalDocument, fiscalStamp, null, null, "blocked_invalid_credit_days", "Credit days must be greater than zero.");
        }

        var currencyCode = NormalizeOptional(fiscalDocument.CurrencyCode);
        if (!string.Equals(currencyCode, "MXN", StringComparison.Ordinal))
        {
            return Blocked(item, fiscalDocument, fiscalStamp, null, null, "blocked_currency_not_supported", $"Currency '{currencyCode}' is not supported.");
        }

        var billingDocument = await _dbContext.BillingDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == fiscalDocument.BillingDocumentId, cancellationToken);
        if (billingDocument is null)
        {
            return Blocked(item, fiscalDocument, fiscalStamp, null, null, "blocked_missing_billing_document", "Billing document was not found.");
        }

        var fiscalReceiver = await _dbContext.FiscalReceivers
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == fiscalDocument.FiscalReceiverId, cancellationToken);
        if (fiscalReceiver is null)
        {
            return Blocked(item, fiscalDocument, fiscalStamp, billingDocument, null, "blocked_missing_fiscal_receiver", "Fiscal receiver was not found.");
        }

        var relatedPaymentComplementCount = await _dbContext.PaymentComplementRelatedDocuments
            .AsNoTracking()
            .CountAsync(
                x => x.FiscalDocumentId == fiscalDocument.Id
                    || x.FiscalStampId == fiscalStamp.Id
                    || x.RelatedDocumentUuid == fiscalStamp.Uuid,
                cancellationToken);
        if (relatedPaymentComplementCount > 0)
        {
            return Blocked(item, fiscalDocument, fiscalStamp, billingDocument, null, "blocked_existing_payment_complement", "Payment complement rows already exist for this fiscal document.");
        }

        var relatedPaymentApplicationCount = await (
            from application in _dbContext.AccountsReceivablePaymentApplications.AsNoTracking()
            join invoice in _dbContext.AccountsReceivableInvoices.AsNoTracking()
                on application.AccountsReceivableInvoiceId equals invoice.Id
            where invoice.FiscalDocumentId == fiscalDocument.Id || invoice.FiscalStampId == fiscalStamp.Id
            select application.Id)
            .CountAsync(cancellationToken);
        if (relatedPaymentApplicationCount > 0)
        {
            return Blocked(item, fiscalDocument, fiscalStamp, billingDocument, null, "blocked_existing_payment_applications", "Payment applications already exist for this fiscal document.");
        }

        var normalizedTotal = CfdiMonetaryRules.RoundMonetary(fiscalDocument.Total, fiscalDocument.CurrencyCode);
        if (normalizedTotal <= 0m)
        {
            return Blocked(item, fiscalDocument, fiscalStamp, billingDocument, null, "blocked_invalid_total", "Operational total must be greater than zero.");
        }

        item.Decision = BackfillDecision.Eligible;
        item.Outcome = "eligible_create";
        item.Message = "Eligible to create accounts receivable invoice.";
        item.PlannedAction = "CreateAccountsReceivableInvoice";
        item.ProposedStatus = AccountsReceivableInvoiceStatus.Open.ToString();
        item.ProposedTotal = normalizedTotal;
        item.ProposedPaidTotal = 0m;
        item.ProposedOutstandingBalance = normalizedTotal;
        item.ProposedDueAtUtc = fiscalDocument.IssuedAtUtc.AddDays(fiscalDocument.CreditDays.Value);

        return new BackfillMissingAccountsReceivableInvoiceEvaluation
        {
            FiscalDocument = fiscalDocument,
            FiscalStamp = fiscalStamp,
            BillingDocument = billingDocument,
            FiscalReceiver = fiscalReceiver,
            Item = item
        };
    }

    private AuditEvent BuildAuditEvent(
        BackfillMissingAccountsReceivableInvoicesCommand command,
        string requestedBy,
        string batchId,
        BackfillMissingAccountsReceivableInvoiceEvaluation evaluation,
        AccountsReceivableInvoice invoice,
        string? databaseName)
    {
        var now = DateTime.UtcNow;
        return new AuditEvent
        {
            OccurredAtUtc = now,
            ActorUsername = requestedBy,
            ActionType = BackfillActionType,
            EntityType = "AccountsReceivableInvoice",
            EntityId = invoice.Id.ToString(),
            Outcome = "Created",
            CorrelationId = batchId,
            RequestSummaryJson = Serialize(new
            {
                batchId,
                fiscalDocumentId = evaluation.FiscalDocument!.Id,
                billingDocumentId = evaluation.FiscalDocument.BillingDocumentId,
                fiscalStampId = evaluation.FiscalStamp!.Id,
                uuid = evaluation.FiscalStamp.Uuid,
                requestedBy,
                command.Notes,
                databaseName,
                validationSnapshot = new
                {
                    evaluation.FiscalDocument.DocumentType,
                    fiscalStatus = evaluation.FiscalDocument.Status.ToString(),
                    stampStatus = evaluation.FiscalStamp.Status.ToString(),
                    evaluation.FiscalDocument.PaymentMethodSat,
                    evaluation.FiscalDocument.PaymentFormSat,
                    evaluation.FiscalDocument.IsCreditSale,
                    evaluation.FiscalDocument.CreditDays,
                    evaluation.FiscalDocument.CurrencyCode,
                    evaluation.FiscalDocument.Total,
                    normalizedTotal = itemTotal(evaluation.Item),
                    evaluation.FiscalDocument.ReceiverRfc,
                    evaluation.FiscalDocument.ReceiverLegalName
                }
            }),
            ResponseSummaryJson = Serialize(new
            {
                invoiceId = invoice.Id,
                status = invoice.Status.ToString(),
                invoice.Total,
                invoice.PaidTotal,
                invoice.OutstandingBalance,
                invoice.DueAtUtc,
                rollbackGuidance = "Rollback is manual only and requires verifying that no payment applications or payment complements were created after this batch."
            }),
            CreatedAtUtc = now
        };

        static decimal itemTotal(BackfillMissingAccountsReceivableInvoiceItemResult item) => item.ProposedTotal ?? 0m;
    }

    private static BackfillMissingAccountsReceivableInvoicesResult BuildResult(
        BackfillMissingAccountsReceivableInvoicesCommand command,
        string environmentName,
        string? databaseName,
        string batchId,
        IReadOnlyList<BackfillMissingAccountsReceivableInvoiceItemResult> items)
    {
        return new BackfillMissingAccountsReceivableInvoicesResult
        {
            CommitChanges = command.CommitChanges,
            BatchId = batchId,
            EnvironmentName = environmentName,
            DatabaseName = databaseName,
            EvaluatedCount = items.Count,
            EligibleCount = items.Count(x => string.Equals(x.Decision, BackfillDecision.Eligible, StringComparison.Ordinal)),
            SkippedCount = items.Count(x => string.Equals(x.Decision, BackfillDecision.Skipped, StringComparison.Ordinal)),
            BlockedCount = items.Count(x => string.Equals(x.Decision, BackfillDecision.Blocked, StringComparison.Ordinal)),
            Items = items
        };
    }

    private static BackfillMissingAccountsReceivableInvoicesResult Failure(
        bool commitChanges,
        string environmentName,
        string errorMessage)
    {
        return new BackfillMissingAccountsReceivableInvoicesResult
        {
            CommitChanges = commitChanges,
            EnvironmentName = environmentName,
            ErrorMessage = errorMessage,
            IsSuccess = false
        };
    }

    private static BackfillMissingAccountsReceivableInvoicesResult Failure(
        BackfillMissingAccountsReceivableInvoicesResult result,
        string errorMessage)
    {
        result.IsSuccess = false;
        result.ErrorMessage = errorMessage;
        return result;
    }

    private static BackfillMissingAccountsReceivableInvoiceEvaluation Blocked(
        long fiscalDocumentId,
        string outcome,
        string message)
    {
        return new BackfillMissingAccountsReceivableInvoiceEvaluation
        {
            Item = new BackfillMissingAccountsReceivableInvoiceItemResult
            {
                FiscalDocumentId = fiscalDocumentId,
                Decision = BackfillDecision.Blocked,
                Outcome = outcome,
                Message = message
            }
        };
    }

    private static BackfillMissingAccountsReceivableInvoiceEvaluation Blocked(
        BackfillMissingAccountsReceivableInvoiceItemResult item,
        FiscalDocument fiscalDocument,
        FiscalStamp? fiscalStamp,
        BillingDocument? billingDocument,
        AccountsReceivableInvoice? existingInvoice,
        string outcome,
        string message)
    {
        item.Decision = BackfillDecision.Blocked;
        item.Outcome = outcome;
        item.Message = message;
        item.AccountsReceivableInvoiceId = existingInvoice?.Id;
        item.ProposedStatus = existingInvoice?.Status.ToString();
        item.ProposedTotal = existingInvoice?.Total;
        item.ProposedPaidTotal = existingInvoice?.PaidTotal;
        item.ProposedOutstandingBalance = existingInvoice?.OutstandingBalance;
        item.ProposedDueAtUtc = existingInvoice?.DueAtUtc;

        return new BackfillMissingAccountsReceivableInvoiceEvaluation
        {
            FiscalDocument = fiscalDocument,
            FiscalStamp = fiscalStamp,
            BillingDocument = billingDocument,
            ExistingInvoice = existingInvoice,
            Item = item
        };
    }

    private async Task<IDbContextTransaction?> BeginTransactionIfSupportedAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
    }

    private string? ResolveDatabaseName()
    {
        if (!_dbContext.Database.IsRelational())
        {
            return _dbContext.Database.ProviderName;
        }

        return _dbContext.Database.GetDbConnection().Database;
    }

    private static IReadOnlyList<long> NormalizeFiscalDocumentIds(IReadOnlyCollection<long> fiscalDocumentIds)
    {
        return fiscalDocumentIds
            .Where(x => x > 0)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
    }

    private static string ResolveBatchId(BackfillMissingAccountsReceivableInvoicesCommand command)
    {
        return string.IsNullOrWhiteSpace(command.BatchId)
            ? Guid.NewGuid().ToString("N")
            : command.BatchId.Trim();
    }

    private static string ResolveRequestedBy(BackfillMissingAccountsReceivableInvoicesCommand command)
    {
        return string.IsNullOrWhiteSpace(command.RequestedBy)
            ? string.Empty
            : command.RequestedBy.Trim();
    }

    private static string NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    private static string Serialize(object value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    private static class BackfillDecision
    {
        public const string Eligible = "Eligible";
        public const string Skipped = "Skipped";
        public const string Blocked = "Blocked";
    }

    private sealed class BackfillMissingAccountsReceivableInvoiceEvaluation
    {
        public FiscalDocument? FiscalDocument { get; init; }

        public FiscalStamp? FiscalStamp { get; init; }

        public BillingDocument? BillingDocument { get; init; }

        public FiscalReceiver? FiscalReceiver { get; init; }

        public AccountsReceivableInvoice? ExistingInvoice { get; init; }

        public BackfillMissingAccountsReceivableInvoiceItemResult Item { get; init; } = new();

        public bool IsEligible => string.Equals(Item.Decision, BackfillDecision.Eligible, StringComparison.Ordinal);
    }
}

public sealed class BackfillMissingAccountsReceivableInvoicesCommand
{
    public IReadOnlyCollection<long> FiscalDocumentIds { get; init; } = [];

    public bool CommitChanges { get; init; }

    public bool AllowProductionCommit { get; init; }

    public string? BatchId { get; init; }

    public string? ExpectedDatabaseName { get; init; }

    public string? RequestedBy { get; init; }

    public string? Notes { get; init; }
}

public sealed class BackfillMissingAccountsReceivableInvoicesResult
{
    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public string BatchId { get; set; } = string.Empty;

    public bool CommitChanges { get; init; }

    public string EnvironmentName { get; init; } = string.Empty;

    public string? DatabaseName { get; init; }

    public int EvaluatedCount { get; init; }

    public int EligibleCount { get; init; }

    public int CreatedCount { get; set; }

    public int SkippedCount { get; set; }

    public int BlockedCount { get; set; }

    public IReadOnlyList<BackfillMissingAccountsReceivableInvoiceItemResult> Items { get; init; } = [];
}

public sealed class BackfillMissingAccountsReceivableInvoiceItemResult
{
    public long FiscalDocumentId { get; set; }

    public long? BillingDocumentId { get; set; }

    public long? FiscalStampId { get; set; }

    public long? AccountsReceivableInvoiceId { get; set; }

    public string Decision { get; set; } = string.Empty;

    public string Outcome { get; set; } = string.Empty;

    public string PlannedAction { get; set; } = "None";

    public string Message { get; set; } = string.Empty;

    public string DocumentType { get; set; } = string.Empty;

    public string FiscalStatus { get; set; } = string.Empty;

    public string PaymentMethodSat { get; set; } = string.Empty;

    public string PaymentFormSat { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = string.Empty;

    public string? Uuid { get; set; }

    public string ReceiverRfc { get; set; } = string.Empty;

    public string ReceiverLegalName { get; set; } = string.Empty;

    public DateTime? IssuedAtUtc { get; set; }

    public decimal? ProposedTotal { get; set; }

    public decimal? ProposedPaidTotal { get; set; }

    public decimal? ProposedOutstandingBalance { get; set; }

    public DateTime? ProposedDueAtUtc { get; set; }

    public string? ProposedStatus { get; set; }
}
