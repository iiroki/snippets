namespace Snippets.Duration;

public static class Duration
{
    public static TimeSpan Parse(string text)
    {
        var parts = text.Split(' ').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (parts.Count != 2)
        {
            throw new ArgumentException($"Invalid time span text: {text}");
        }

        var value = ParseValue(parts[0]);
        var factory = CreateTimeSpanFactory(parts[1]);
        return factory(value);
    }

    public static Func<double, TimeSpan> CreateTimeSpanFactory(string unit) =>
        unit.ToLower() switch
        {
            "ms" or "millisecond" or "milliseconds" => TimeSpan.FromMilliseconds,
            "s" or "sec" or "second" or "seconds" => TimeSpan.FromSeconds,
            "min" or "minute" or "minutes" => TimeSpan.FromMinutes,
            "h" or "hour" or "hours" => TimeSpan.FromHours,
            "d" or "day" or "days" => TimeSpan.FromDays,
            _ => throw new ArgumentException($"Unknown time span unit: {unit}")
        };

    private static double ParseValue(string text) =>
        double.TryParse(text, out var value) ? value : throw new ArgumentException($"Could not parse value: {text}");
}
