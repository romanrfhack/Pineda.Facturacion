using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

internal static class FiscalOperationRobustnessPolicy
{
    public static bool CanStamp(FiscalDocumentStatus status) =>
        status is FiscalDocumentStatus.ReadyForStamping or FiscalDocumentStatus.StampingRejected;

    public static bool IsStampInProgress(FiscalDocumentStatus status) =>
        status == FiscalDocumentStatus.StampingRequested;

    public static bool CanCancel(FiscalDocumentStatus status) =>
        status is FiscalDocumentStatus.Stamped or FiscalDocumentStatus.CancellationRejected;

    public static bool IsCancellationInProgress(FiscalDocumentStatus status) =>
        status == FiscalDocumentStatus.CancellationRequested;

    public static bool IsRetryable(StampFiscalDocumentOutcome outcome) =>
        outcome == StampFiscalDocumentOutcome.ProviderUnavailable;

    public static bool IsRetryable(CancelFiscalDocumentOutcome outcome) =>
        outcome == CancelFiscalDocumentOutcome.ProviderUnavailable;

    public static bool IsRetryable(RespondFiscalCancellationAuthorizationOutcome outcome) =>
        outcome == RespondFiscalCancellationAuthorizationOutcome.ProviderUnavailable;

    public static string? BuildRetryAdvice(StampFiscalDocumentOutcome outcome) =>
        outcome switch
        {
            StampFiscalDocumentOutcome.ProviderUnavailable => "Puedes reintentar el timbrado manualmente. El último intento falló por una condición transitoria del proveedor.",
            StampFiscalDocumentOutcome.ProviderRejected => "No reintentes ciegamente. Revisa primero el mensaje del proveedor y corrige los datos fiscales antes de intentar de nuevo.",
            StampFiscalDocumentOutcome.ValidationFailed => "Corrige la validación local antes de reintentar el timbrado.",
            StampFiscalDocumentOutcome.Conflict => "Verifica si el timbrado ya está en curso o si el CFDI ya quedó timbrado antes de volver a intentar.",
            _ => null
        };

    public static string? BuildRetryAdvice(CancelFiscalDocumentOutcome outcome) =>
        outcome switch
        {
            CancelFiscalDocumentOutcome.ProviderUnavailable => "Puedes reintentar la cancelación manualmente. El último intento falló por una condición transitoria del proveedor.",
            CancelFiscalDocumentOutcome.ProviderRejected => "No reintentes ciegamente. Revisa primero el código y mensaje del proveedor para confirmar si el rechazo fue terminal.",
            CancelFiscalDocumentOutcome.ValidationFailed => "Corrige la validación local antes de reintentar la cancelación.",
            CancelFiscalDocumentOutcome.Conflict => "Verifica si la cancelación ya está en curso o si el CFDI ya quedó cancelado antes de volver a intentar.",
            _ => null
        };

    public static string? BuildRetryAdvice(RespondFiscalCancellationAuthorizationOutcome outcome) =>
        outcome switch
        {
            RespondFiscalCancellationAuthorizationOutcome.ProviderUnavailable => "Puedes reintentar la respuesta de autorización manualmente. El proveedor reportó una condición transitoria.",
            RespondFiscalCancellationAuthorizationOutcome.ProviderRejected => "No reintentes ciegamente. Revisa primero el mensaje del proveedor para confirmar si la autorización ya no está pendiente.",
            RespondFiscalCancellationAuthorizationOutcome.ValidationFailed => "Corrige la validación local antes de volver a responder la autorización.",
            RespondFiscalCancellationAuthorizationOutcome.Conflict => "La autorización ya fue resuelta o ya no está pendiente. Refresca el estado antes de intentar otra respuesta.",
            _ => null
        };

    public static string? BuildStampSupportMessage(
        string? providerCode,
        string? providerMessage,
        string? errorCode,
        string? providerTrackingId)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(providerCode))
        {
            parts.Add($"ProviderCode={providerCode.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(providerMessage))
        {
            parts.Add($"ProviderMessage={providerMessage.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            parts.Add($"ErrorCode={errorCode.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(providerTrackingId))
        {
            parts.Add($"TrackingId={providerTrackingId.Trim()}");
        }

        return parts.Count == 0 ? null : string.Join(" | ", parts);
    }
}
