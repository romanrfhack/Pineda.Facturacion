using Pineda.Facturacion.Application.Common;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

internal static class FiscalStatusOperationalInterpreter
{
    public static FiscalStatusOperationalInterpretation Interpret(
        string? codigoEstatus,
        string? estado,
        string? esCancelable,
        string? estatusCancelacion)
    {
        var normalizedCode = Normalize(codigoEstatus);
        var normalizedState = Normalize(estado);
        var normalizedCancelability = FiscalMasterDataNormalization.NormalizeOptionalText(esCancelable);
        var normalizedCancellationStatus = Normalize(estatusCancelacion);
        var supportMessage = BuildSupportMessage(codigoEstatus, estado, esCancelable, estatusCancelacion);

        if (normalizedState is "CANCELADO" or "CANCELLED" or "CANCELED")
        {
            return Build(FiscalOperationalStatus.Cancelled, "Documento cancelado en SAT.", supportMessage);
        }

        if (normalizedCancellationStatus == "EN PROCESO")
        {
            return Build(FiscalOperationalStatus.CancellationPending, "La cancelación fue solicitada y sigue en proceso en SAT.", supportMessage);
        }

        if (normalizedCancellationStatus == "SOLICITUD RECHAZADA")
        {
            return Build(FiscalOperationalStatus.CancellationRejected, "La solicitud de cancelación fue rechazada por SAT.", supportMessage);
        }

        if (normalizedCancellationStatus == "PLAZO VENCIDO")
        {
            return Build(FiscalOperationalStatus.CancellationExpired, "La solicitud de cancelación venció sin respuesta en SAT.", supportMessage);
        }

        if (normalizedCancellationStatus is "CANCELADO CON ACEPTACION" or "CANCELADO SIN ACEPTACION")
        {
            return Build(FiscalOperationalStatus.Cancelled, "Documento cancelado en SAT.", supportMessage);
        }

        if (normalizedState == "VIGENTE")
        {
            return Build(FiscalOperationalStatus.Active, BuildActiveUserMessage(normalizedCancelability), supportMessage);
        }

        if (normalizedState == "NO ENCONTRADO" || normalizedCode.StartsWith("N 602", StringComparison.Ordinal))
        {
            return Build(FiscalOperationalStatus.NotFound, "SAT no encontró el CFDI consultado.", supportMessage);
        }

        if (normalizedState == "EXPRESION IMPRESA INVALIDA" || normalizedCode.StartsWith("N 601", StringComparison.Ordinal))
        {
            return Build(FiscalOperationalStatus.QueryError, "SAT reportó una expresión impresa inválida para este CFDI.", supportMessage);
        }

        if (normalizedCode.StartsWith("S", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(normalizedState))
        {
            return Build(FiscalOperationalStatus.QueryError, "SAT respondió satisfactoriamente, pero sin un estado interpretable.", supportMessage);
        }

        return Build(FiscalOperationalStatus.QueryError, "La respuesta de SAT no pudo interpretarse de forma operativa.", supportMessage);
    }

    public static FiscalStatusOperationalInterpretation BuildUnavailable(string? errorMessage)
    {
        var userMessage = string.IsNullOrWhiteSpace(errorMessage)
            ? "No fue posible consultar el estado SAT del CFDI."
            : errorMessage.Trim();
        var supportMessage = string.IsNullOrWhiteSpace(errorMessage)
            ? "Consulta SAT no disponible."
            : $"Consulta SAT no disponible. Detalle: {errorMessage.Trim()}";

        return Build(FiscalOperationalStatus.QueryError, userMessage, supportMessage);
    }

    private static FiscalStatusOperationalInterpretation Build(FiscalOperationalStatus status, string userMessage, string supportMessage)
    {
        return new FiscalStatusOperationalInterpretation
        {
            Status = status,
            UserMessage = userMessage,
            SupportMessage = supportMessage
        };
    }

    private static string BuildActiveUserMessage(string? cancelability)
    {
        if (string.IsNullOrWhiteSpace(cancelability))
        {
            return "Documento vigente en SAT.";
        }

        return $"Documento vigente en SAT. {cancelability}.";
    }

    private static string BuildSupportMessage(string? codigoEstatus, string? estado, string? esCancelable, string? estatusCancelacion)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(codigoEstatus))
        {
            parts.Add($"CodigoEstatus={codigoEstatus.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(estado))
        {
            parts.Add($"Estado={estado.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(esCancelable))
        {
            parts.Add($"EsCancelable={esCancelable.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(estatusCancelacion))
        {
            parts.Add($"EstatusCancelacion={estatusCancelacion.Trim()}");
        }

        return parts.Count > 0
            ? string.Join(" | ", parts)
            : "Sin detalles adicionales en la respuesta SAT.";
    }

    private static string Normalize(string? value)
    {
        return FiscalMasterDataNormalization.NormalizeOptionalText(value)?.ToUpperInvariant()
            .Replace("Á", "A", StringComparison.Ordinal)
            .Replace("É", "E", StringComparison.Ordinal)
            .Replace("Í", "I", StringComparison.Ordinal)
            .Replace("Ó", "O", StringComparison.Ordinal)
            .Replace("Ú", "U", StringComparison.Ordinal)
            ?? string.Empty;
    }
}
