using System.Collections.Immutable;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// The name of a call of a source function, such as <c>vorbis::field("MOOD")</c>. Every source Goro
/// reads matches the names its source functions take without regard to case, so every part is
/// compared that way: <c>vorbis::field("mood")</c> reads what <c>vorbis::field("MOOD")</c> reads,
/// and is the same name.
/// </summary>
public sealed class SourceCallName(string source, string function, ImmutableArray<string> arguments) : DeclaredName
{
    public string Source { get; } = source;

    public string Function { get; } = function;

    /// <summary>The arguments, as the source function canonicalised them.</summary>
    public ImmutableArray<string> Arguments { get; } = arguments;

    public override bool Equals(DeclaredName? other) =>
        other is SourceCallName call
        && SameName(Source, call.Source)
        && SameName(Function, call.Function)
        && Arguments.Length == call.Arguments.Length
        && Arguments.Zip(call.Arguments).All(pair => SameName(pair.First, pair.Second));

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Source, StringComparer.OrdinalIgnoreCase);
        hash.Add(Function, StringComparer.OrdinalIgnoreCase);
        foreach (var argument in Arguments)
        {
            hash.Add(argument, StringComparer.OrdinalIgnoreCase);
        }

        return hash.ToHashCode();
    }

    public override string ToString() =>
        $"{Source}::{Function}({string.Join(", ", Arguments.Select(argument => $"\"{argument.Replace("\\", "\\\\").Replace("\"", "\\\"")}\""))})";
}
