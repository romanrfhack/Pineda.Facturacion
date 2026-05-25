namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public static class PaymentComplementFileNameBuilder
{
    public static string Build(string uuid, string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uuid);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        return $"REP_{Sanitize(uuid.Trim())}.{extension.TrimStart('.')}";
    }

    private static string Sanitize(string value)
    {
        return string.Join(
            string.Empty,
            value.Select(static character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
    }
}
