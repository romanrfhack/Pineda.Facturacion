using System.Globalization;

namespace Pineda.Facturacion.Infrastructure.Documents;

internal static class SpanishCurrencyTextFormatter
{
    public static string FormatMx(decimal amount)
    {
        amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        var integerPart = decimal.ToInt64(decimal.Truncate(amount));
        var cents = decimal.ToInt32((amount - decimal.Truncate(amount)) * 100m);

        var words = ToWords(integerPart);
        var pesoWord = integerPart == 1 ? "PESO" : "PESOS";
        return $"{words} {pesoWord} {cents.ToString("00", CultureInfo.InvariantCulture)}/100 M.N.";
    }

    private static string ToWords(long value)
    {
        if (value == 0)
        {
            return "CERO";
        }

        if (value < 0)
        {
            return $"MENOS {ToWords(Math.Abs(value))}";
        }

        if (value >= 1_000_000_000_000)
        {
            return value == 1_000_000_000_000
                ? "UN BILLON"
                : $"{ToWords(value / 1_000_000_000_000)} BILLONES {ToWords(value % 1_000_000_000_000)}".Trim();
        }

        if (value >= 1_000_000)
        {
            if (value == 1_000_000)
            {
                return "UN MILLON";
            }

            return $"{ToWords(value / 1_000_000)} MILLONES {ToWords(value % 1_000_000)}".Trim();
        }

        if (value >= 1000)
        {
            if (value == 1000)
            {
                return "MIL";
            }

            if (value < 2000)
            {
                return $"MIL {ToWords(value % 1000)}".Trim();
            }

            return $"{ToWords(value / 1000)} MIL {ToWords(value % 1000)}".Trim();
        }

        if (value >= 100)
        {
            if (value == 100)
            {
                return "CIEN";
            }

            var hundreds = value / 100;
            var remainder = value % 100;
            var prefix = hundreds switch
            {
                1 => "CIENTO",
                2 => "DOSCIENTOS",
                3 => "TRESCIENTOS",
                4 => "CUATROCIENTOS",
                5 => "QUINIENTOS",
                6 => "SEISCIENTOS",
                7 => "SETECIENTOS",
                8 => "OCHOCIENTOS",
                9 => "NOVECIENTOS",
                _ => string.Empty
            };

            return $"{prefix} {ToWords(remainder)}".Trim();
        }

        if (value >= 30)
        {
            var tens = value / 10;
            var units = value % 10;
            var prefix = tens switch
            {
                3 => "TREINTA",
                4 => "CUARENTA",
                5 => "CINCUENTA",
                6 => "SESENTA",
                7 => "SETENTA",
                8 => "OCHENTA",
                9 => "NOVENTA",
                _ => string.Empty
            };

            return units == 0 ? prefix : $"{prefix} Y {ToWords(units)}";
        }

        return value switch
        {
            1 => "UN",
            2 => "DOS",
            3 => "TRES",
            4 => "CUATRO",
            5 => "CINCO",
            6 => "SEIS",
            7 => "SIETE",
            8 => "OCHO",
            9 => "NUEVE",
            10 => "DIEZ",
            11 => "ONCE",
            12 => "DOCE",
            13 => "TRECE",
            14 => "CATORCE",
            15 => "QUINCE",
            16 => "DIECISEIS",
            17 => "DIECISIETE",
            18 => "DIECIOCHO",
            19 => "DIECINUEVE",
            20 => "VEINTE",
            21 => "VEINTIUN",
            22 => "VEINTIDOS",
            23 => "VEINTITRES",
            24 => "VEINTICUATRO",
            25 => "VEINTICINCO",
            26 => "VEINTISEIS",
            27 => "VEINTISIETE",
            28 => "VEINTIOCHO",
            29 => "VEINTINUEVE",
            _ => string.Empty
        };
    }
}
