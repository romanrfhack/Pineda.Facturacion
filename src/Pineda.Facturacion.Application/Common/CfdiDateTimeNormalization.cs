namespace Pineda.Facturacion.Application.Common;

internal static class CfdiDateTimeNormalization
{
    private static readonly string[] CfdiTimeZoneIds =
    [
        "America/Mexico_City",
        "Central Standard Time (Mexico)"
    ];

    public static DateTime NormalizeIncomingUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => ConvertMexicoCityLocalToUtc(value)
        };
    }

    public static DateTime NormalizeIncomingUtcOrNow(DateTime? value, DateTime utcNow)
    {
        return value.HasValue
            ? NormalizeIncomingUtc(value.Value)
            : EnsureUtc(utcNow);
    }

    private static DateTime ConvertMexicoCityLocalToUtc(DateTime value)
    {
        var unspecifiedLocal = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        var cfdiTimeZone = ResolveCfdiTimeZone();
        if (cfdiTimeZone is not null)
        {
            return TimeZoneInfo.ConvertTimeToUtc(unspecifiedLocal, cfdiTimeZone);
        }

        return DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime();
    }

    private static TimeZoneInfo? ResolveCfdiTimeZone()
    {
        foreach (var timeZoneId in CfdiTimeZoneIds)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return null;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
