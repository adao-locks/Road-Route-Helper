using System.Globalization;

namespace Road_Route_Helper.Application.Formatting;

public static class DurationFormatter
{
    /// <summary>Formats a duration in total hours and minutes, for example "25h 00min".</summary>
    public static string Format(TimeSpan duration) =>
        string.Create(CultureInfo.InvariantCulture, $"{(long)duration.TotalHours}h {duration.Minutes:00}min");
}
