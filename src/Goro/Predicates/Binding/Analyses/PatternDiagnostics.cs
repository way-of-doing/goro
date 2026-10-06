using Goro.Messages;
using Goro.Predicates.Diagnostics;
using Goro.Predicates.Syntax;
using Codes = Goro.Predicates.Binding.SemanticDiagnosticCodes;

namespace Goro.Predicates.Binding;

/// <summary>The errors of the patterns analysis.</summary>
internal sealed class PatternDiagnostics(string text) : SemanticDiagnostics(text)
{
    public Diagnostic Pattern(StringToken pattern, PatternFailure failure) => failure switch
    {
        PatternFailure.Malformed malformed => new Diagnostic(Codes.InvalidPattern, PatternSpan(pattern, malformed.Offset),
            new ErrorMessage.InvalidPattern(Code(pattern.Span), new ForeignText(malformed.Reason))),
        PatternFailure.Unsupported unsupported => new Diagnostic(Codes.UnsupportedPatternConstruct, pattern.Span,
            new ErrorMessage.UnsupportedPatternConstruct(unsupported.Family)),
        _ => throw new ArgumentOutOfRangeException(nameof(failure)),
    };

    /// <summary>
    /// The character of the pattern at which the engine gave up, which is the one before the offset
    /// it reports, located in the raw string as written, where a quote is two characters.
    /// </summary>
    private static TextSpan PatternSpan(StringToken pattern, int offset)
    {
        if (pattern.Value.Length == 0 || offset < 0)
        {
            return pattern.Span;
        }

        var target = Math.Clamp(offset - 1, 0, pattern.Value.Length - 1);
        var position = pattern.Span.Start + 2; // past r"
        for (var i = 0; i < target; i++)
        {
            position += pattern.Value[i] == '"' ? 2 : 1;
        }

        return new TextSpan(position, pattern.Value[target] == '"' ? 2 : 1);
    }
}
