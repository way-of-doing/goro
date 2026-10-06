using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Goro.Messages;
using Goro.Predicates.Binding;
using Goro.Predicates.Diagnostics;
using Goro.Predicates.Text;

namespace Goro.Predicates.Analyses;

/// <summary>
/// Compiles the pattern of every <c>=~</c>, the one thing that cannot wait for a file, reporting a
/// pattern that is malformed or uses a construct Goro does not support. Lowering takes the compiled
/// pattern from here.
/// </summary>
/// <remarks>
/// The non-backtracking engine reports a malformed pattern as <see cref="RegexParseException"/>,
/// with an offset into the pattern, and each construct it does not support as a
/// <see cref="NotSupportedException"/>, so both errors come straight from the engine. The canary
/// tests watch for a later .NET that starts accepting one of those constructs.
/// </remarks>
public static class Patterns
{
    public static PatternsAnalysis Analyse(SemanticTree tree)
    {
        var report = new PatternsDiagnostics(tree.Text);
        var diagnostics = new List<Diagnostic>();
        var patterns = new Dictionary<SemanticMatch, Regex>();
        foreach (var match in tree.Nodes.OfType<SemanticMatch>())
        {
            if (Compile(match.Pattern.Value, match.Mode, out var failure) is { } pattern)
            {
                patterns.Add(match, pattern);
            }
            else
            {
                diagnostics.Add(report.Pattern(match.Pattern, failure!));
            }
        }

        return new PatternsAnalysis(patterns, [.. diagnostics]);
    }

    /// <summary>Non-backtracking and culture-invariant always; case-insensitive in normalized mode.</summary>
    public static RegexOptions OptionsFor(ComparisonMode mode) =>
        RegexOptions.NonBacktracking | RegexOptions.CultureInvariant
            | (mode == ComparisonMode.Normalized ? RegexOptions.IgnoreCase : RegexOptions.None);

    /// <returns>The compiled pattern, or null with what went wrong in <paramref name="failure"/>.</returns>
    internal static Regex? Compile(string pattern, ComparisonMode mode, out PatternFailure? failure)
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

/// <summary>The compiled pattern of every <c>=~</c> of a semantic tree whose pattern is sound, and the errors about the others.</summary>
public sealed class PatternsAnalysis
{
    private readonly IReadOnlyDictionary<SemanticMatch, Regex> patterns;

    internal PatternsAnalysis(IReadOnlyDictionary<SemanticMatch, Regex> patterns, ImmutableArray<Diagnostic> diagnostics)
    {
        this.patterns = patterns;
        Diagnostics = diagnostics;
    }

    public ImmutableArray<Diagnostic> Diagnostics { get; }

    /// <summary>The pattern of <paramref name="match"/>, compiled in its operator's mode, if it is sound.</summary>
    public Regex? PatternOf(SemanticMatch match) => patterns.GetValueOrDefault(match);
}
