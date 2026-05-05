using System.Globalization;

namespace Pineda.Facturacion.Application.UseCases.Reports;

public static class MexicoLocalDateRangeConverter
{
    private const string MexicoCityTimeZoneId = "America/Mexico_City";
    private const string WindowsMexicoCityTimeZoneId = "Central Standard Time (Mexico)";
    private const string LocalTextFormat = "yyyy-MM-dd HH:mm:ss";

    private static readonly Lazy<TimeZoneInfo> MexicoCityTimeZone = new(FindMexicoCityTimeZone);

    public static (DateTime FromUtc, DateTime ToUtcExclusive) ToUtcRange(DateOnly fromDate, DateOnly toDate)
    {
        var fromLocal = DateTime.SpecifyKind(fromDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var toLocalExclusive = DateTime.SpecifyKind(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);

        return (
            TimeZoneInfo.ConvertTimeToUtc(fromLocal, MexicoCityTimeZone.Value),
            TimeZoneInfo.ConvertTimeToUtc(toLocalExclusive, MexicoCityTimeZone.Value));
    }

    public static string FormatStampedAtLocal(DateTime stampedAtUtc)
    {
        return ToMexicoLocal(stampedAtUtc).ToString(LocalTextFormat, CultureInfo.InvariantCulture);
    }

    public static DateTime ToMexicoLocal(DateTime stampedAtUtc)
    {
        var utc = stampedAtUtc.Kind == DateTimeKind.Utc
            ? stampedAtUtc
            : DateTime.SpecifyKind(stampedAtUtc, DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(utc, MexicoCityTimeZone.Value);
    }

    private static TimeZoneInfo FindMexicoCityTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(MexicoCityTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(WindowsMexicoCityTimeZoneId);
        }
    }
}
