namespace Goro.Predicates.Values;

/// <summary>The types of the predicate language.</summary>
public enum GoroType
{
    String,
    Number,
    ByteCount,
    Duration,
    Boolean,
}

/// <summary>
/// The correspondence between Goro's types and the C# types that hold their data. This is the only
/// place it is written down; everything generic over a datum type learns its Goro type from here.
/// </summary>
public static class GoroTypes
{
    public static GoroType Of<T>() where T : notnull => Cache<T>.Type;

    /// <summary>The type's name in the language, as the specification spells it: <c>string</c>, <c>bytecount</c>.</summary>
    public static string Name(GoroType type) => type switch
    {
        GoroType.String => "string",
        GoroType.Number => "number",
        GoroType.ByteCount => "bytecount",
        GoroType.Duration => "duration",
        GoroType.Boolean => "boolean",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    private static class Cache<T> where T : notnull
    {
        public static readonly GoroType Type = typeof(T) switch
        {
            var t when t == typeof(string) => GoroType.String,
            var t when t == typeof(decimal) => GoroType.Number,
            var t when t == typeof(ByteCount) => GoroType.ByteCount,
            var t when t == typeof(Duration) => GoroType.Duration,
            var t when t == typeof(bool) => GoroType.Boolean,
            var t => throw new NotSupportedException($"{t} is not the datum type of any Goro type."),
        };
    }
}
