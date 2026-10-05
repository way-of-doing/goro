// Owned by the binder group (G4) of the predicate-runtime-architecture line.
using System.Text.RegularExpressions;
using Goro.Messages;
using Goro.Predicates.Text;

namespace Goro.Predicates.Binding;

/// <summary>Compiles the pattern of <c>=~</c>, the one thing that cannot wait for a file.</summary>
/// <remarks>
/// The non-backtracking engine reports a malformed pattern as <see cref="RegexParseException"/>,
/// with an offset into the pattern, and each construct it does not support as a
/// <see cref="NotSupportedException"/>, so both errors come straight from the engine. The canary
/// tests watch for a later .NET that starts accepting one of those constructs.
/// </remarks>
internal static class Patterns
{
    /// <summary>Non-backtracking and culture-invariant always; case-insensitive in normalized mode.</summary>
    public static RegexOptions OptionsFor(ComparisonMode mode) =>
        RegexOptions.NonBacktracking | RegexOptions.CultureInvariant
            | (mode == ComparisonMode.Normalized ? RegexOptions.IgnoreCase : RegexOptions.None);

    /// <returns>The compiled pattern, or null with what went wrong in <paramref name="failure"/>.</returns>
    public static Regex? Compile(string pattern, ComparisonMode mode, out PatternFailure? failure)
    {
        failure = null;
        try
        {
            return new Regex(pattern, OptionsFor(mode));
        }
        catch (RegexParseException exception)
        {
            failure = new PatternFailure.Malformed(exception.Offset, Reason(exception));
        }
        catch (NotSupportedException exception)
        {
            failure = new PatternFailure.Unsupported(Family(exception.Message));
        }

        return null;
    }

    // "Invalid pattern 'a(' at offset 2. Not enough )'s." -> "Not enough )'s."
    private static string Reason(RegexParseException exception)
    {
        var marker = $" at offset {exception.Offset}. ";
        var at = exception.Message.LastIndexOf(marker, StringComparison.Ordinal);
        return at < 0 ? exception.Error.ToString() : exception.Message[(at + marker.Length)..];
    }

    // The engine names the construct in its message; the specification groups them in four families.
    private static PatternFamily Family(string message) => message switch
    {
        _ when message.Contains("lookahead", StringComparison.OrdinalIgnoreCase)
            || message.Contains("lookbehind", StringComparison.OrdinalIgnoreCase) => PatternFamily.Lookaround,
        _ when message.Contains("backreference", StringComparison.OrdinalIgnoreCase) => PatternFamily.Backreference,
        _ when message.Contains("atomic", StringComparison.OrdinalIgnoreCase) => PatternFamily.AtomicGroup,
        _ when message.Contains("conditional", StringComparison.OrdinalIgnoreCase)
            || message.Contains("balancing", StringComparison.OrdinalIgnoreCase) => PatternFamily.ConditionalOrBalancingGroup,
        _ => PatternFamily.Unknown,
    };
}

internal abstract record PatternFailure
{
    private PatternFailure()
    {
    }

    /// <param name="Offset">Where in the pattern the engine gave up, as it counts.</param>
    /// <param name="Reason">Why, in the engine's own words.</param>
    public sealed record Malformed(int Offset, string Reason) : PatternFailure;

    /// <param name="Family">The construct, as the specification groups them.</param>
    public sealed record Unsupported(PatternFamily Family) : PatternFailure;
}
