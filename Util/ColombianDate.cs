namespace FinancialApp.Backend.Util;

public static class ColombianDate
{
    private static readonly TimeZoneInfo ColombiaTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");

    public static DateTime Now()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ColombiaTimeZone);
    }

    public static DateTime Today()
    {
        return Now().Date;
    }
}
