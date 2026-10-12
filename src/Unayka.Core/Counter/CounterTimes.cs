using System.Globalization;

namespace Unayka.Core.Counter;

public static class CounterTimes
{
    private static readonly TimeSpan MaputoOffset = TimeSpan.FromHours(2);

    public static string FormatTime(DateTimeOffset value) =>
        value.ToOffset(MaputoOffset).ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string FormatDateTime(DateTimeOffset value) =>
        value.ToOffset(MaputoOffset).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public static DateTimeOffset StartOfDay(DateTimeOffset now)
    {
        var local = now.ToOffset(MaputoOffset);
        return new DateTimeOffset(local.Year, local.Month, local.Day, 0, 0, 0, MaputoOffset);
    }

    public static int AgeInMinutes(DateTimeOffset createdAt, DateTimeOffset now) =>
        (int)Math.Max(0, (now - createdAt).TotalMinutes);
}
