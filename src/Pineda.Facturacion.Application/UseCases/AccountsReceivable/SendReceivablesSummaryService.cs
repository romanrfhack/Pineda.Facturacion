using System.Net.Mail;
using System.Text.Json;
using Pineda.Facturacion.Application.Abstractions.Communication;
using Pineda.Facturacion.Application.Abstractions.Documents;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class SendReceivablesSummaryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ReceivablesSummaryDocumentFactory _documentFactory;
    private readonly IReceivablesSummaryPdfRenderer _pdfRenderer;
    private readonly IEmailSender _emailSender;
    private readonly IAuditEventRepository _auditEventRepository;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IUnitOfWork _unitOfWork;

    public SendReceivablesSummaryService(
        ReceivablesSummaryDocumentFactory documentFactory,
        IReceivablesSummaryPdfRenderer pdfRenderer,
        IEmailSender emailSender,
        IAuditEventRepository auditEventRepository,
        ICurrentUserAccessor currentUserAccessor,
        IUnitOfWork unitOfWork)
    {
        _documentFactory = documentFactory;
        _pdfRenderer = pdfRenderer;
        _emailSender = emailSender;
        _auditEventRepository = auditEventRepository;
        _currentUserAccessor = currentUserAccessor;
        _unitOfWork = unitOfWork;
    }

    public async Task<SendReceivablesSummaryResult> ExecuteAsync(
        ReceivablesSummaryCommand command,
        CancellationToken cancellationToken = default)
    {
        var buildResult = await _documentFactory.BuildDocumentAsync(command, cancellationToken);
        if (!buildResult.IsSuccess || buildResult.Document is null)
        {
            return new SendReceivablesSummaryResult
            {
                Outcome = buildResult.Outcome,
                ErrorMessage = buildResult.ErrorMessage
            };
        }

        var document = buildResult.Document;
        var html = ReceivablesSummaryComposer.BuildHtml(document);
        byte[]? pdfContent = null;
        string? pdfFileName = null;

        if (document.HasPdf)
        {
            try
            {
                pdfContent = await _pdfRenderer.RenderAsync(document, cancellationToken);
                pdfFileName = ReceivablesSummaryComposer.BuildPdfFileName(document);
            }
            catch (Exception exception)
            {
                var errorMessage = $"Falló la generación del PDF: {exception.Message}";
                await TryRecordHistoryAsync(document, ReceivablesSummaryOutcome.PdfGenerationFailed, null, errorMessage, cancellationToken);
                return new SendReceivablesSummaryResult
                {
                    Outcome = ReceivablesSummaryOutcome.PdfGenerationFailed,
                    ErrorMessage = errorMessage,
                    Document = document,
                    AttachedPdf = false
                };
            }
        }

        try
        {
            await _emailSender.SendAsync(
                new EmailMessage
                {
                    Subject = document.Subject,
                    Body = html,
                    IsBodyHtml = true,
                    Recipients = document.To,
                    CcRecipients = document.Cc,
                    BccRecipients = document.Bcc,
                    Attachments = pdfContent is null || string.IsNullOrWhiteSpace(pdfFileName)
                        ? []
                        :
                        [
                            new EmailAttachment
                            {
                                FileName = pdfFileName,
                                ContentType = "application/pdf",
                                Content = pdfContent
                            }
                        ]
                },
                cancellationToken);
        }
        catch (SmtpException exception)
        {
            var errorMessage = "Falló el envío del correo. Revisa la conectividad SMTP, credenciales o restricciones del servidor.";
            await TryRecordHistoryAsync(document, ReceivablesSummaryOutcome.DeliveryFailed, null, $"{errorMessage} {exception.Message}", cancellationToken);
            return new SendReceivablesSummaryResult
            {
                Outcome = ReceivablesSummaryOutcome.DeliveryFailed,
                ErrorMessage = errorMessage,
                Document = document,
                AttachedPdf = pdfContent is not null
            };
        }
        catch (InvalidOperationException exception)
        {
            var errorMessage = "El envío por correo no está configurado correctamente en el servidor.";
            await TryRecordHistoryAsync(document, ReceivablesSummaryOutcome.DeliveryFailed, null, exception.Message, cancellationToken);
            return new SendReceivablesSummaryResult
            {
                Outcome = ReceivablesSummaryOutcome.DeliveryFailed,
                ErrorMessage = errorMessage,
                Document = document,
                AttachedPdf = pdfContent is not null
            };
        }
        catch (Exception exception)
        {
            var errorMessage = "Falló el envío del correo por un error inesperado.";
            await TryRecordHistoryAsync(document, ReceivablesSummaryOutcome.DeliveryFailed, null, $"{errorMessage} {exception.Message}", cancellationToken);
            return new SendReceivablesSummaryResult
            {
                Outcome = ReceivablesSummaryOutcome.DeliveryFailed,
                ErrorMessage = errorMessage,
                Document = document,
                AttachedPdf = pdfContent is not null
            };
        }

        var sentAtUtc = DateTime.UtcNow;
        try
        {
            var historyId = await RecordHistoryAsync(document, ReceivablesSummaryOutcome.Sent, sentAtUtc, null, cancellationToken);
            return new SendReceivablesSummaryResult
            {
                Outcome = ReceivablesSummaryOutcome.Sent,
                IsSuccess = true,
                SentAtUtc = sentAtUtc,
                HistoryId = historyId,
                Document = document,
                AttachedPdf = pdfContent is not null
            };
        }
        catch (Exception exception)
        {
            return new SendReceivablesSummaryResult
            {
                Outcome = ReceivablesSummaryOutcome.HistoryFailed,
                ErrorMessage = $"El correo fue enviado, pero falló el registro del historial: {exception.Message}",
                SentAtUtc = sentAtUtc,
                Document = document,
                AttachedPdf = pdfContent is not null
            };
        }
    }

    private async Task TryRecordHistoryAsync(
        ReceivablesSummaryDocument document,
        ReceivablesSummaryOutcome outcome,
        DateTime? sentAtUtc,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            await RecordHistoryAsync(document, outcome, sentAtUtc, errorMessage, cancellationToken);
        }
        catch
        {
        }
    }

    private async Task<string> RecordHistoryAsync(
        ReceivablesSummaryDocument document,
        ReceivablesSummaryOutcome outcome,
        DateTime? sentAtUtc,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        var auditEvent = new AuditEvent
        {
            OccurredAtUtc = sentAtUtc ?? DateTime.UtcNow,
            ActorUserId = currentUser.UserId,
            ActorUsername = currentUser.Username,
            ActionType = "AccountsReceivable.SendSummary",
            EntityType = "FiscalReceiver",
            EntityId = document.ReceiverId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Outcome = outcome.ToString(),
            CorrelationId = $"receivables-summary-{Guid.NewGuid():N}",
            RequestSummaryJson = JsonSerializer.Serialize(new
            {
                document.ReceiverId,
                Scope = document.Scope.ToString(),
                Format = document.Format.ToString(),
                document.To,
                document.Cc,
                document.Bcc,
                document.Subject,
                InvoiceIds = document.Invoices.Select(x => x.AccountsReceivableInvoiceId).ToArray(),
                TotalsByCurrency = document.Selection.TotalsByCurrency
            }, JsonOptions),
            ResponseSummaryJson = JsonSerializer.Serialize(new
            {
                SentAtUtc = sentAtUtc,
                AttachedPdf = document.HasPdf,
                Status = outcome.ToString(),
                EmailProviderMessageId = (string?)null
            }, JsonOptions),
            ErrorMessage = errorMessage,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _auditEventRepository.AddAsync(auditEvent, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return auditEvent.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
