using System.Net.Mail;
using System.Text.Json;
using Pineda.Facturacion.Application.Abstractions.Communication;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.Orders;

public sealed class SendOrderDebtSummaryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly OrderDebtSummaryDocumentFactory _documentFactory;
    private readonly IEmailSender _emailSender;
    private readonly IAuditEventRepository _auditEventRepository;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IUnitOfWork _unitOfWork;

    public SendOrderDebtSummaryService(
        OrderDebtSummaryDocumentFactory documentFactory,
        IEmailSender emailSender,
        IAuditEventRepository auditEventRepository,
        ICurrentUserAccessor currentUserAccessor,
        IUnitOfWork unitOfWork)
    {
        _documentFactory = documentFactory;
        _emailSender = emailSender;
        _auditEventRepository = auditEventRepository;
        _currentUserAccessor = currentUserAccessor;
        _unitOfWork = unitOfWork;
    }

    public async Task<SendOrderDebtSummaryResult> ExecuteAsync(
        OrderDebtSummaryCommand command,
        CancellationToken cancellationToken = default)
    {
        var buildResult = await _documentFactory.BuildDocumentAsync(command, cancellationToken);
        if (!buildResult.IsSuccess || buildResult.Document is null)
        {
            return new SendOrderDebtSummaryResult
            {
                Outcome = buildResult.Outcome,
                ErrorMessage = buildResult.ErrorMessage
            };
        }

        var document = buildResult.Document;
        var html = OrderDebtSummaryComposer.BuildHtml(document);

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
                    BccRecipients = document.Bcc
                },
                cancellationToken);
        }
        catch (SmtpException exception)
        {
            var errorMessage = "Falló el envío del correo. Revisa la conectividad SMTP, credenciales o restricciones del servidor.";
            await TryRecordHistoryAsync(document, OrderDebtSummaryOutcome.DeliveryFailed, null, $"{errorMessage} {exception.Message}", cancellationToken);
            return new SendOrderDebtSummaryResult
            {
                Outcome = OrderDebtSummaryOutcome.DeliveryFailed,
                ErrorMessage = errorMessage,
                Document = document
            };
        }
        catch (InvalidOperationException exception)
        {
            var errorMessage = "El envío por correo no está configurado correctamente en el servidor.";
            await TryRecordHistoryAsync(document, OrderDebtSummaryOutcome.DeliveryFailed, null, exception.Message, cancellationToken);
            return new SendOrderDebtSummaryResult
            {
                Outcome = OrderDebtSummaryOutcome.DeliveryFailed,
                ErrorMessage = errorMessage,
                Document = document
            };
        }
        catch (Exception exception)
        {
            var errorMessage = "Falló el envío del correo por un error inesperado.";
            await TryRecordHistoryAsync(document, OrderDebtSummaryOutcome.DeliveryFailed, null, $"{errorMessage} {exception.Message}", cancellationToken);
            return new SendOrderDebtSummaryResult
            {
                Outcome = OrderDebtSummaryOutcome.DeliveryFailed,
                ErrorMessage = errorMessage,
                Document = document
            };
        }

        var sentAtUtc = DateTime.UtcNow;
        try
        {
            var historyId = await RecordHistoryAsync(document, OrderDebtSummaryOutcome.Sent, sentAtUtc, null, cancellationToken);
            return new SendOrderDebtSummaryResult
            {
                Outcome = OrderDebtSummaryOutcome.Sent,
                IsSuccess = true,
                SentAtUtc = sentAtUtc,
                HistoryId = historyId,
                Document = document
            };
        }
        catch (Exception exception)
        {
            return new SendOrderDebtSummaryResult
            {
                Outcome = OrderDebtSummaryOutcome.HistoryFailed,
                ErrorMessage = $"El correo fue enviado, pero falló el registro del historial: {exception.Message}",
                SentAtUtc = sentAtUtc,
                Document = document
            };
        }
    }

    private async Task TryRecordHistoryAsync(
        OrderDebtSummaryDocument document,
        OrderDebtSummaryOutcome outcome,
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
        OrderDebtSummaryDocument document,
        OrderDebtSummaryOutcome outcome,
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
            ActionType = "Orders.SendDebtSummary",
            EntityType = "FiscalReceiver",
            EntityId = document.ReceiverId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Outcome = outcome.ToString(),
            CorrelationId = $"orders-debt-summary-{Guid.NewGuid():N}",
            RequestSummaryJson = JsonSerializer.Serialize(new
            {
                document.ReceiverId,
                Format = document.Format.ToString(),
                document.To,
                document.Cc,
                document.Bcc,
                document.Subject,
                LegacyOrderIds = document.Orders.Select(order => order.LegacyOrderId).ToArray(),
                TotalsByCurrency = document.Selection.TotalsByCurrency
            }, JsonOptions),
            ResponseSummaryJson = JsonSerializer.Serialize(new
            {
                SentAtUtc = sentAtUtc,
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
