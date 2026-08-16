using System.Globalization;

namespace PA_Website.Helpers;

public static class BulgarianDate
{
    private static readonly string[] DateFormats =
    {
        "dd.MM.yyyy",
        "d.M.yyyy",
        "dd/MM/yyyy",
        "d/M/yyyy",
        "yyyy-MM-dd"
    };

    private static readonly string[] DateTimeFormats =
    {
        "dd.MM.yyyy HH:mm",
        "d.M.yyyy HH:mm",
        "dd/MM/yyyy HH:mm",
        "yyyy-MM-ddTHH:mm",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd HH:mm:ss"
    };

    public static bool TryParseDate(string? value, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return DateTime.TryParseExact(
            value.Trim(),
            DateFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    public static bool TryParseDateTime(string? value, out DateTime dateTime)
    {
        dateTime = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        if (DateTime.TryParseExact(
                trimmed,
                DateTimeFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out dateTime))
        {
            return true;
        }

        return DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime);
    }

    public static DateTime ParseBirthDate(string value)
    {
        if (TryParseDate(value, out var date))
            return date;

        throw new FormatException("Невалиден формат на датата. Използвайте дд.мм.гггг.");
    }
}
