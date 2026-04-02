namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public static class RepBaseDocumentTimelineBuilder
{
    public static IReadOnlyList<RepBaseDocumentTimelineEntry> BuildInternal(
        InternalRepBaseDocumentListItem summary,
        IReadOnlyList<InternalRepBaseDocumentPaymentHistoryReadModel> paymentHistory,
        IReadOnlyList<InternalRepBaseDocumentPaymentApplicationReadModel> paymentApplications,
        IReadOnlyList<InternalRepBaseDocumentPaymentComplementReadModel> paymentComplements)
    {
        var timeline = new List<RepBaseDocumentTimelineEntry>();

        foreach (var payment in paymentHistory)
        {
            timeline.Add(new RepBaseDocumentTimelineEntry
            {
                EventType = RepBaseDocumentTimelineEventTypes.PaymentRegistered,
                OccurredAtUtc = payment.CreatedAtUtc,
                SourceType = "AccountsReceivablePayment",
                Severity = "info",
                Title = "Pago registrado",
                Description = $"Se registró el pago #{payment.AccountsReceivablePaymentId} por {payment.PaymentAmount:0.00} {summary.CurrencyCode}.",
                Status = payment.PaymentComplementStatus,
                ReferenceId = payment.AccountsReceivablePaymentId,
                ReferenceUuid = payment.PaymentComplementUuid,
                Metadata = CreateMetadata(
                    ("paymentFormSat", payment.PaymentFormSat),
                    ("reference", payment.Reference))
            });
        }

        foreach (var application in paymentApplications)
        {
            timeline.Add(new RepBaseDocumentTimelineEntry
            {
                EventType = RepBaseDocumentTimelineEventTypes.PaymentApplied,
                OccurredAtUtc = application.CreatedAtUtc,
                SourceType = "AccountsReceivablePaymentApplication",
                Severity = "info",
                Title = "Pago aplicado al documento base",
                Description = $"Se aplicó {application.AppliedAmount:0.00} {summary.CurrencyCode} del pago #{application.AccountsReceivablePaymentId}.",
                Status = application.ApplicationSequence.ToString(),
                ReferenceId = application.AccountsReceivablePaymentId,
                Metadata = CreateMetadata(
                    ("applicationSequence", application.ApplicationSequence.ToString()),
                    ("previousBalance", application.PreviousBalance.ToString("0.00")),
                    ("newBalance", application.NewBalance.ToString("0.00")))
            });
        }

        foreach (var complement in paymentComplements)
        {
            AddComplementEvents(
                timeline,
                summary.CurrencyCode,
                summary.ReceiverRfc,
                complement.PaymentComplementId,
                complement.AccountsReceivablePaymentId,
                complement.Status,
                complement.Uuid,
                complement.ProviderName,
                complement.PaidAmount,
                complement.RemainingBalance,
                complement.IssuedAtUtc,
                complement.StampedAtUtc,
                complement.LastStatusCheckAtUtc,
                complement.LastKnownExternalStatus,
                complement.LastStatusProviderCode,
                complement.LastStatusProviderMessage,
                complement.StampUpdatedAtUtc,
                complement.CancellationStatus,
                complement.CancellationRequestedAtUtc,
                complement.CancelledAtUtc,
                complement.CancellationUpdatedAtUtc);
        }

        return timeline
            .OrderBy(x => x.OccurredAtUtc)
            .ThenBy(x => GetEventPriority(x.EventType))
            .ThenBy(x => x.ReferenceId)
            .ToList();
    }

    public static IReadOnlyList<RepBaseDocumentTimelineEntry> BuildExternal(
        ExternalRepBaseDocumentListItem summary,
        IReadOnlyList<ExternalRepBaseDocumentPaymentHistoryReadModel> paymentHistory,
        IReadOnlyList<ExternalRepBaseDocumentPaymentApplicationReadModel> paymentApplications,
        IReadOnlyList<ExternalRepBaseDocumentPaymentComplementReadModel> paymentComplements)
    {
        var timeline = new List<RepBaseDocumentTimelineEntry>
        {
            new()
            {
                EventType = RepBaseDocumentTimelineEventTypes.ExternalXmlImported,
                OccurredAtUtc = summary.ImportedAtUtc,
                SourceType = "ExternalRepBaseDocument",
                Severity = "info",
                Title = "XML externo importado",
                Description = $"Se importó el CFDI externo {summary.Uuid} desde {summary.SourceFileName}.",
                Status = summary.ValidationStatus,
                ReferenceId = summary.ExternalRepBaseDocumentId,
                ReferenceUuid = summary.Uuid,
                Metadata = CreateMetadata(
                    ("sourceFileName", summary.SourceFileName),
                    ("xmlHash", summary.XmlHash))
            }
        };

        if (string.Equals(summary.ValidationStatus, "Accepted", StringComparison.OrdinalIgnoreCase))
        {
            timeline.Add(new RepBaseDocumentTimelineEntry
            {
                EventType = RepBaseDocumentTimelineEventTypes.ExternalValidationAccepted,
                OccurredAtUtc = summary.ImportedAtUtc,
                SourceType = "ExternalRepBaseDocumentValidation",
                Severity = "info",
                Title = "Validación externa aceptada",
                Description = string.IsNullOrWhiteSpace(summary.ReasonMessage)
                    ? "La validación operativa del CFDI externo fue aceptada."
                    : summary.ReasonMessage,
                Status = summary.ValidationStatus,
                ReferenceId = summary.ExternalRepBaseDocumentId,
                ReferenceUuid = summary.Uuid,
                Metadata = CreateMetadata(("reasonCode", summary.ReasonCode))
            });
        }
        else if (string.Equals(summary.ValidationStatus, "Blocked", StringComparison.OrdinalIgnoreCase))
        {
            timeline.Add(new RepBaseDocumentTimelineEntry
            {
                EventType = RepBaseDocumentTimelineEventTypes.ExternalValidationBlocked,
                OccurredAtUtc = summary.ImportedAtUtc,
                SourceType = "ExternalRepBaseDocumentValidation",
                Severity = "warning",
                Title = "Validación externa bloqueada",
                Description = string.IsNullOrWhiteSpace(summary.ReasonMessage)
                    ? "La validación operativa del CFDI externo quedó bloqueada."
                    : summary.ReasonMessage,
                Status = summary.ValidationStatus,
                ReferenceId = summary.ExternalRepBaseDocumentId,
                ReferenceUuid = summary.Uuid,
                Metadata = CreateMetadata(("reasonCode", summary.ReasonCode))
            });
        }

        if (string.Equals(summary.SatStatus, "Unavailable", StringComparison.OrdinalIgnoreCase)
            && summary.LastSatCheckAtUtc.HasValue)
        {
            timeline.Add(new RepBaseDocumentTimelineEntry
            {
                EventType = RepBaseDocumentTimelineEventTypes.SatValidationUnavailable,
                OccurredAtUtc = summary.LastSatCheckAtUtc.Value,
                SourceType = "SatValidation",
                Severity = "warning",
                Title = "Validación SAT no disponible",
                Description = BuildSatUnavailableDescription(summary),
                Status = summary.SatStatus,
                ReferenceId = summary.ExternalRepBaseDocumentId,
                ReferenceUuid = summary.Uuid,
                Metadata = CreateMetadata(
                    ("providerCode", summary.LastSatProviderCode),
                    ("externalStatus", summary.LastSatExternalStatus),
                    ("cancellationStatus", summary.LastSatCancellationStatus))
            });
        }

        foreach (var payment in paymentHistory)
        {
            timeline.Add(new RepBaseDocumentTimelineEntry
            {
                EventType = RepBaseDocumentTimelineEventTypes.PaymentRegistered,
                OccurredAtUtc = payment.CreatedAtUtc,
                SourceType = "AccountsReceivablePayment",
                Severity = "info",
                Title = "Pago registrado",
                Description = $"Se registró el pago #{payment.AccountsReceivablePaymentId} por {payment.PaymentAmount:0.00} {summary.CurrencyCode}.",
                Status = payment.PaymentComplementStatus,
                ReferenceId = payment.AccountsReceivablePaymentId,
                ReferenceUuid = payment.PaymentComplementUuid,
                Metadata = CreateMetadata(
                    ("paymentFormSat", payment.PaymentFormSat),
                    ("reference", payment.Reference))
            });
        }

        foreach (var application in paymentApplications)
        {
            timeline.Add(new RepBaseDocumentTimelineEntry
            {
                EventType = RepBaseDocumentTimelineEventTypes.PaymentApplied,
                OccurredAtUtc = application.CreatedAtUtc,
                SourceType = "AccountsReceivablePaymentApplication",
                Severity = "info",
                Title = "Pago aplicado al documento base",
                Description = $"Se aplicó {application.AppliedAmount:0.00} {summary.CurrencyCode} del pago #{application.AccountsReceivablePaymentId}.",
                Status = application.ApplicationSequence.ToString(),
                ReferenceId = application.AccountsReceivablePaymentId,
                Metadata = CreateMetadata(
                    ("applicationSequence", application.ApplicationSequence.ToString()),
                    ("previousBalance", application.PreviousBalance.ToString("0.00")),
                    ("newBalance", application.NewBalance.ToString("0.00")))
            });
        }

        foreach (var complement in paymentComplements)
        {
            AddComplementEvents(
                timeline,
                summary.CurrencyCode,
                summary.ReceiverRfc,
                complement.PaymentComplementId,
                complement.AccountsReceivablePaymentId,
                complement.Status,
                complement.Uuid,
                complement.ProviderName,
                complement.PaidAmount,
                complement.RemainingBalance,
                complement.IssuedAtUtc,
                complement.StampedAtUtc,
                complement.LastStatusCheckAtUtc,
                complement.LastKnownExternalStatus,
                complement.LastStatusProviderCode,
                complement.LastStatusProviderMessage,
                complement.StampUpdatedAtUtc,
                complement.CancellationStatus,
                complement.CancellationRequestedAtUtc,
                complement.CancelledAtUtc,
                complement.CancellationUpdatedAtUtc);
        }

        return timeline
            .OrderBy(x => x.OccurredAtUtc)
            .ThenBy(x => GetEventPriority(x.EventType))
            .ThenBy(x => x.ReferenceId)
            .ToList();
    }

    private static void AddComplementEvents(
        ICollection<RepBaseDocumentTimelineEntry> timeline,
        string currencyCode,
        string receiverRfc,
        long paymentComplementId,
        long paymentId,
        string status,
        string? uuid,
        string? providerName,
        decimal paidAmount,
        decimal remainingBalance,
        DateTime? issuedAtUtc,
        DateTime? stampedAtUtc,
        DateTime? lastStatusCheckAtUtc,
        string? lastKnownExternalStatus,
        string? lastStatusProviderCode,
        string? lastStatusProviderMessage,
        DateTime? stampUpdatedAtUtc,
        string? cancellationStatus,
        DateTime? cancellationRequestedAtUtc,
        DateTime? cancelledAtUtc,
        DateTime? cancellationUpdatedAtUtc)
    {
        var baseMetadata = CreateMetadata(
            ("accountsReceivablePaymentId", paymentId.ToString()),
            ("providerName", providerName));

        if (issuedAtUtc.HasValue)
        {
            timeline.Add(new RepBaseDocumentTimelineEntry
            {
                EventType = RepBaseDocumentTimelineEventTypes.RepPrepared,
                OccurredAtUtc = issuedAtUtc.Value,
                SourceType = "PaymentComplementDocument",
                Severity = "info",
                Title = "REP preparado",
                Description = $"Se preparó el REP #{paymentComplementId} para el pago #{paymentId} por {paidAmount:0.00} {currencyCode}.",
                Status = status,
                ReferenceId = paymentComplementId,
                ReferenceUuid = uuid,
                Metadata = baseMetadata
            });
        }

        if (stampedAtUtc.HasValue)
        {
            timeline.Add(new RepBaseDocumentTimelineEntry
            {
                EventType = RepBaseDocumentTimelineEventTypes.RepStamped,
                OccurredAtUtc = stampedAtUtc.Value,
                SourceType = "PaymentComplementStamp",
                Severity = "info",
                Title = "REP timbrado",
                Description = $"El REP #{paymentComplementId} quedó timbrado para el receptor {receiverRfc}.",
                Status = status,
                ReferenceId = paymentComplementId,
                ReferenceUuid = uuid,
                Metadata = baseMetadata
            });
        }

        if (string.Equals(status, "StampingRejected", StringComparison.OrdinalIgnoreCase)
            && stampUpdatedAtUtc.HasValue)
        {
            timeline.Add(new RepBaseDocumentTimelineEntry
            {
                EventType = RepBaseDocumentTimelineEventTypes.RepStampingRejected,
                OccurredAtUtc = stampUpdatedAtUtc.Value,
                SourceType = "PaymentComplementStamp",
                Severity = "error",
                Title = "Timbrado de REP rechazado",
                Description = $"El PAC rechazó el timbrado del REP #{paymentComplementId}.",
                Status = status,
                ReferenceId = paymentComplementId,
                ReferenceUuid = uuid,
                Metadata = baseMetadata
            });
        }

        if (lastStatusCheckAtUtc.HasValue)
        {
            timeline.Add(new RepBaseDocumentTimelineEntry
            {
                EventType = RepBaseDocumentTimelineEventTypes.RepStatusRefreshed,
                OccurredAtUtc = lastStatusCheckAtUtc.Value,
                SourceType = "PaymentComplementStatusCheck",
                Severity = "info",
                Title = "Estatus de REP refrescado",
                Description = BuildStatusRefreshDescription(paymentComplementId, lastKnownExternalStatus, lastStatusProviderCode, lastStatusProviderMessage),
                Status = lastKnownExternalStatus,
                ReferenceId = paymentComplementId,
                ReferenceUuid = uuid,
                Metadata = CreateMetadata(
                    ("accountsReceivablePaymentId", paymentId.ToString()),
                    ("providerName", providerName),
                    ("providerCode", lastStatusProviderCode))
            });
        }

        if (cancellationRequestedAtUtc.HasValue)
        {
            timeline.Add(new RepBaseDocumentTimelineEntry
            {
                EventType = RepBaseDocumentTimelineEventTypes.RepCancellationRequested,
                OccurredAtUtc = cancellationRequestedAtUtc.Value,
                SourceType = "PaymentComplementCancellation",
                Severity = "warning",
                Title = "Cancelación de REP solicitada",
                Description = $"Se solicitó la cancelación del REP #{paymentComplementId}.",
                Status = cancellationStatus ?? status,
                ReferenceId = paymentComplementId,
                ReferenceUuid = uuid,
                Metadata = baseMetadata
            });
        }

        if (cancelledAtUtc.HasValue)
        {
            timeline.Add(new RepBaseDocumentTimelineEntry
            {
                EventType = RepBaseDocumentTimelineEventTypes.RepCancelled,
                OccurredAtUtc = cancelledAtUtc.Value,
                SourceType = "PaymentComplementCancellation",
                Severity = "warning",
                Title = "REP cancelado",
                Description = $"El REP #{paymentComplementId} quedó cancelado.",
                Status = cancellationStatus ?? status,
                ReferenceId = paymentComplementId,
                ReferenceUuid = uuid,
                Metadata = CreateMetadata(
                    ("accountsReceivablePaymentId", paymentId.ToString()),
                    ("remainingBalance", remainingBalance.ToString("0.00")),
                    ("providerName", providerName))
            });
        }

        if (string.Equals(cancellationStatus, "Rejected", StringComparison.OrdinalIgnoreCase)
            && cancellationUpdatedAtUtc.HasValue)
        {
            timeline.Add(new RepBaseDocumentTimelineEntry
            {
                EventType = RepBaseDocumentTimelineEventTypes.RepCancellationRejected,
                OccurredAtUtc = cancellationUpdatedAtUtc.Value,
                SourceType = "PaymentComplementCancellation",
                Severity = "error",
                Title = "Cancelación de REP rechazada",
                Description = $"La cancelación del REP #{paymentComplementId} fue rechazada por el proveedor o SAT.",
                Status = cancellationStatus,
                ReferenceId = paymentComplementId,
                ReferenceUuid = uuid,
                Metadata = baseMetadata
            });
        }
    }

    private static string BuildSatUnavailableDescription(ExternalRepBaseDocumentListItem summary)
    {
        if (!string.IsNullOrWhiteSpace(summary.LastSatProviderMessage))
        {
            return $"No fue posible validar el CFDI externo en SAT: {summary.LastSatProviderMessage}.";
        }

        return $"No fue posible validar el CFDI externo {summary.Uuid} en SAT al último refresh disponible.";
    }

    private static string BuildStatusRefreshDescription(
        long paymentComplementId,
        string? lastKnownExternalStatus,
        string? providerCode,
        string? providerMessage)
    {
        var statusFragment = string.IsNullOrWhiteSpace(lastKnownExternalStatus)
            ? "sin estatus externo reportado"
            : $"con estatus externo {lastKnownExternalStatus}";

        if (!string.IsNullOrWhiteSpace(providerMessage))
        {
            return $"Se refrescó el REP #{paymentComplementId} {statusFragment}: {providerMessage}.";
        }

        if (!string.IsNullOrWhiteSpace(providerCode))
        {
            return $"Se refrescó el REP #{paymentComplementId} {statusFragment} (código {providerCode}).";
        }

        return $"Se refrescó el REP #{paymentComplementId} {statusFragment}.";
    }

    private static IReadOnlyDictionary<string, string?> CreateMetadata(params (string Key, string? Value)[] values)
    {
        var metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                metadata[key] = value;
            }
        }

        return metadata;
    }

    private static int GetEventPriority(string eventType)
    {
        return eventType switch
        {
            RepBaseDocumentTimelineEventTypes.ExternalXmlImported => 0,
            RepBaseDocumentTimelineEventTypes.ExternalValidationAccepted => 1,
            RepBaseDocumentTimelineEventTypes.ExternalValidationBlocked => 2,
            RepBaseDocumentTimelineEventTypes.SatValidationUnavailable => 3,
            RepBaseDocumentTimelineEventTypes.PaymentRegistered => 10,
            RepBaseDocumentTimelineEventTypes.PaymentApplied => 11,
            RepBaseDocumentTimelineEventTypes.RepPrepared => 20,
            RepBaseDocumentTimelineEventTypes.RepStampingRejected => 21,
            RepBaseDocumentTimelineEventTypes.RepStamped => 22,
            RepBaseDocumentTimelineEventTypes.RepStatusRefreshed => 23,
            RepBaseDocumentTimelineEventTypes.RepCancellationRequested => 30,
            RepBaseDocumentTimelineEventTypes.RepCancelled => 31,
            RepBaseDocumentTimelineEventTypes.RepCancellationRejected => 32,
            _ => 999
        };
    }
}
