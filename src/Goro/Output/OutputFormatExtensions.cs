namespace Goro.Output;

/// <summary>
/// Owns everything about <see cref="OutputFormat"/>'s string representation and
/// meaning: name↔value mapping (for parsing and error messages) and which renderer
/// a given format selects. Centralizing this here means adding a format only
/// requires touching this one file, not every place that used to switch on the enum.
/// </summary>
public static class OutputFormatExtensions
{
    private static readonly (string Name, OutputFormat Value)[] Entries =
    [
        ("plain", OutputFormat.Plain),
        ("json", OutputFormat.Json),
    ];

    public static IReadOnlyList<string> ValidNames { get; } = Entries.Select(e => e.Name).ToArray();

    /// <summary>Case-insensitive; null/empty means "use the default" (<see cref="OutputFormat.Plain"/>).</summary>
    public static bool TryParse(string? value, out OutputFormat format)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            format = OutputFormat.Plain;
            return true;
        }

        var match = Entries.SingleOrDefault(e => string.Equals(e.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        format = match.Value;
        return match.Name is not null;
    }

    public static IOutputRenderer<TResult> CreateRenderer<TResult>(this OutputFormat format, Func<TResult, string> formatPlainLine) => format switch
    {
        OutputFormat.Plain => new PlainOutputRenderer<TResult>(formatPlainLine),
        OutputFormat.Json => new JsonOutputRenderer<TResult>(),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported output format."),
    };
}
