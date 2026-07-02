using System.Net.Mail;

namespace Pineda.Facturacion.Application.Common;

internal static class EmailRecipientParser
{
    private static readonly char[] RecipientSeparators = [';', ',', '\r', '\n'];

    public static IReadOnlyList<string> SplitRecipients(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(RecipientSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(recipient => recipient.Trim())
            .Where(recipient => recipient.Length > 0)
            .ToArray();
    }

    public static IReadOnlyList<string> NormalizeRecipients(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return [];
        }

        var normalizedRecipients = new List<string>();

        foreach (var candidate in EnumerateCandidates(values))
        {
            if (TryNormalizeEmailAddress(candidate, out var normalizedRecipient))
            {
                normalizedRecipients.Add(normalizedRecipient);
            }
        }

        return normalizedRecipients
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> FindInvalidRecipients(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return [];
        }

        var invalidRecipients = new List<string>();

        foreach (var candidate in EnumerateCandidates(values))
        {
            if (!TryNormalizeEmailAddress(candidate, out _))
            {
                invalidRecipients.Add(candidate);
            }
        }

        return invalidRecipients
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsValidEmailAddress(string? value)
    {
        return TryNormalizeEmailAddress(value, out _);
    }

    public static string? JoinNormalizedRecipients(IEnumerable<string>? values)
    {
        var normalizedRecipients = NormalizeRecipients(values);
        return normalizedRecipients.Count == 0
            ? null
            : string.Join("; ", normalizedRecipients);
    }

    private static IEnumerable<string> EnumerateCandidates(IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            foreach (var candidate in SplitRecipients(value))
            {
                yield return candidate;
            }
        }
    }

    private static bool TryNormalizeEmailAddress(string? value, out string normalizedAddress)
    {
        normalizedAddress = string.Empty;

        var candidate = value?.Trim();
        if (string.IsNullOrWhiteSpace(candidate)
            || candidate.Contains(';', StringComparison.Ordinal)
            || candidate.Contains(',', StringComparison.Ordinal)
            || candidate.Any(char.IsControl))
        {
            return false;
        }

        try
        {
            var address = new MailAddress(candidate);
            if (!string.Equals(address.Address, candidate, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(address.User)
                || !address.Host.Contains('.', StringComparison.Ordinal)
                || address.Host.StartsWith(".", StringComparison.Ordinal)
                || address.Host.EndsWith(".", StringComparison.Ordinal))
            {
                return false;
            }

            normalizedAddress = address.Address;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
