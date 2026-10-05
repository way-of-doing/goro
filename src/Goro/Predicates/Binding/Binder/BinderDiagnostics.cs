// Owned by the binder group (G4) of the predicate-runtime-architecture line.
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Goro.Predicates.Diagnostics;
using Goro.Predicates.Identifiers;
using Goro.Predicates.Syntax;
using Goro.Predicates.Text;
using Goro.Predicates.Values;
using Codes = Goro.Predicates.Binding.BinderDiagnosticCodes;

namespace Goro.Predicates.Binding;

/// <summary>
/// The binder's errors, and the rewrites it offers for them. Every rewrite is composed from the
/// spans of what the user wrote, so that it changes only what was wrong.
/// </summary>
internal sealed partial class BinderDiagnostics(string text)
{
    private static readonly string[] Functions = ["COUNT", "FALLBACK", "NUMBER", "STRING"];

    // ---------------------------------------------------------------------------------------------
    // Names

    public Diagnostic UnknownNamespace(IdentifierSyntax identifier, IEnumerable<string> known)
    {
        var namespaceParts = identifier.Parts[..^1];
        var written = string.Join("::", namespaceParts.Select(part => part.Text));
        var closest = Spelling.Closest(written, known);
        ImmutableArray<Suggestion> suggestions = closest is null
            ? []
            : [new Suggestion(identifier.Span,
                Text(TextSpan.FromBounds(identifier.Span.Start, namespaceParts[0].Span.Start))
                + closest
                + Text(TextSpan.FromBounds(namespaceParts[^1].Span.End, identifier.Span.End)))];
        return new Diagnostic(Codes.UnknownNamespace, identifier.Span, ErrorMessages.UnknownNamespace(written), suggestions);
    }

    public Diagnostic UnknownIdentifier(IdentifierSyntax identifier, IEnumerable<IdentifierName> candidates)
    {
        var name = identifier.Parts[^1];
        var closest = Spelling.Closest(name.Text, candidates.Select(candidate => candidate.Name));
        ImmutableArray<Suggestion> suggestions = closest is null
            ? []
            : [new Suggestion(identifier.Span,
                Text(TextSpan.FromBounds(identifier.Span.Start, name.Span.Start)) + IdentifierName.Global(closest))];
        var message = identifier.Parts.Length == 1
            ? ErrorMessages.UnknownIdentifier(name.Text)
            : ErrorMessages.UnknownIdentifierInNamespace(Text(TextSpan.FromBounds(identifier.Parts[0].Span.Start, identifier.Parts[^2].Span.End)), name.Text);
        return new Diagnostic(Codes.UnknownIdentifier, identifier.Span, message, suggestions);
    }

    public Diagnostic UnknownFunction(NameToken name)
    {
        var closest = Spelling.Closest(name.Text, Functions);
        if (closest is not null && name.Text == name.Text.ToLowerInvariant())
        {
            closest = closest.ToLowerInvariant();
        }

        ImmutableArray<Suggestion> suggestions = closest is null ? [] : [new Suggestion(name.Span, closest)];
        return new Diagnostic(Codes.UnknownFunction, name.Span, ErrorMessages.UnknownFunction(name.Text), suggestions);
    }

    public Diagnostic NullValue(TextSpan span) =>
        new(Codes.NullValue, span, ErrorMessages.NullValue());

    /// <summary><c>x == NULL</c> or <c>x != NULL</c>, which is a state test written the way SQL writes it.</summary>
    public Diagnostic NullComparison(ComparisonSyntax comparison, NullSyntax @null, ExpressionSyntax other)
    {
        var negation = comparison.Operator == ComparisonOperator.NotEqual ? "NOT " : "";
        return new Diagnostic(Codes.NullValue, @null.Span, ErrorMessages.NullComparison(),
            [new Suggestion(comparison.Span, $"{negation}{Text(other)} IS ABSENT")]);
    }

    // ---------------------------------------------------------------------------------------------
    // Types

    public Diagnostic TypeMismatch(ComparisonSyntax comparison, ExpressionSyntax leftCore, Bound left, ExpressionSyntax rightCore, Bound right)
    {
        var op = Text(comparison.OperatorSpan);
        var message = OnlyALiteralStandsIn(left, right) || OnlyALiteralStandsIn(right, left)
            ? ErrorMessages.TypeMismatchNumberVariable(Text(leftCore), left.Type, Text(rightCore), right.Type, op)
            : ErrorMessages.TypeMismatch(Text(leftCore), left.Type, Text(rightCore), right.Type, op);

        var suggestions = ImmutableArray.CreateBuilder<Suggestion>();
        foreach (var (side, sideCore, other) in new[] { (left, leftCore, right), (right, rightCore, left) })
        {
            if (Unquoted(side, other.Type) is { } unquoted)
            {
                suggestions.Add(unquoted);
            }
            else if (side.Type == GoroType.String && side.Literal is null && other.Type == GoroType.Number)
            {
                suggestions.Add(new Suggestion(sideCore.Span, $"NUMBER({Text(sideCore)})"));
            }
        }

        return new Diagnostic(Codes.TypeMismatch, comparison.Span, message, suggestions.ToImmutable());
    }

    public Diagnostic FallbackTypeMismatch(FunctionCallSyntax call, Bound argument, Bound @default)
    {
        var argumentSyntax = call.Arguments[0];
        var defaultSyntax = call.Arguments[1];
        ImmutableArray<Suggestion> suggestions = Unquoted(@default, argument.Type) is { } unquoted ? [unquoted] : [];
        return new Diagnostic(Codes.TypeMismatch, defaultSyntax.Span,
            ErrorMessages.FallbackTypeMismatch(call.Name.Text, Text(argumentSyntax), argument.Type, Text(defaultSyntax), @default.Type),
            suggestions);
    }

    public Diagnostic RangeTypeMismatch(BetweenSyntax between, ExpressionSyntax subjectCore, GoroType subject, GoroType range) =>
        new(Codes.TypeMismatch, between.Span, ErrorMessages.RangeTypeMismatch(Text(subjectCore), subject, Text(RangeSpan(between)), range));

    public Diagnostic NegativeUnitLiteral(LiteralSyntax literal, GoroType unit) =>
        new(Codes.NegativeUnitLiteral, literal.Span, ErrorMessages.NegativeUnitLiteral(Text(literal), unit));

    public Diagnostic FractionalDurationLiteral(LiteralSyntax literal) =>
        new(Codes.FractionalDurationLiteral, literal.Span, ErrorMessages.FractionalDurationLiteral(Text(literal)));

    public Diagnostic BooleanCompared(ComparisonSyntax comparison) =>
        new(Codes.BooleanNotOrdered, comparison.Span, ErrorMessages.BooleanCompared(Text(comparison.OperatorSpan)));

    public Diagnostic BooleanBetween(ExpressionSyntax subjectCore) =>
        new(Codes.BooleanNotOrdered, subjectCore.Span, ErrorMessages.BooleanBetween(Text(subjectCore)));

    public Diagnostic BooleanRange(BetweenSyntax between) =>
        new(Codes.BooleanNotOrdered, RangeSpan(between), ErrorMessages.BooleanRange());

    public Diagnostic BooleanNotConvertible(FunctionCallSyntax call) =>
        new(Codes.BooleanNotConvertible, call.Arguments[0].Span, ErrorMessages.BooleanNotConvertible(call.Name.Text, Text(call.Arguments[0])));

    public Diagnostic MatchSubjectNotString(ExpressionSyntax subjectCore, GoroType type)
    {
        ImmutableArray<Suggestion> suggestions = type is GoroType.Number or GoroType.ByteCount or GoroType.Duration
            ? [new Suggestion(subjectCore.Span, $"STRING({Text(subjectCore)})")]
            : [];
        return new Diagnostic(Codes.MatchSubjectNotString, subjectCore.Span, ErrorMessages.MatchSubjectNotString(Text(subjectCore), type), suggestions);
    }

    // ---------------------------------------------------------------------------------------------
    // Definiteness

    public Diagnostic NotACondition(ExpressionSyntax syntax, GoroType type, string role) =>
        new(Codes.NotACondition, syntax.Span, ErrorMessages.NotACondition(role, Text(syntax), type));

    public Diagnostic IndefiniteCondition(ExpressionSyntax syntax, string role) =>
        new(Codes.IndefiniteCondition, syntax.Span, ErrorMessages.IndefiniteCondition(Text(syntax), role),
            [new Suggestion(syntax.Span, $"{Text(syntax)} == TRUE"), new Suggestion(syntax.Span, $"{Text(syntax)} == FALSE")]);

    /// <summary>
    /// <c>genre != "metal"</c>: offers <c>NOT genre == "metal"</c> and <c>ALL(genre) != "metal"</c>,
    /// the two readings predicates.md says are usually meant.
    /// </summary>
    public Diagnostic AmbiguousNotEqual(ComparisonSyntax comparison, ExpressionSyntax? leftCore, ExpressionSyntax? rightCore)
    {
        var (left, right) = (comparison.Left, comparison.Right);
        var negated = "NOT "
            + Text(TextSpan.FromBounds(left.Span.Start, comparison.OperatorSpan.Start))
            + "=="
            + Text(TextSpan.FromBounds(comparison.OperatorSpan.End, right.Span.End));
        var universal = Universal(left, leftCore is not null)
            + Text(TextSpan.FromBounds(left.Span.End, right.Span.Start))
            + Universal(right, rightCore is not null);
        var message = (leftCore, rightCore) switch
        {
            ({ } l, { } r) => ErrorMessages.AmbiguousNotEqualBoth(Text(l), Text(r)),
            ({ } l, null) => ErrorMessages.AmbiguousNotEqual(Text(l)),
            (null, { } r) => ErrorMessages.AmbiguousNotEqual(Text(r)),
            _ => throw new ArgumentException("Some operand must need a quantifier."),
        };
        return new Diagnostic(Codes.AmbiguousNotEqual, comparison.OperatorSpan, message,
            [new Suggestion(comparison.Span, negated), new Suggestion(comparison.Span, universal)]);
    }

    // ---------------------------------------------------------------------------------------------
    // Modifiers

    /// <param name="outer">The expression the modifier was found in, parentheses and all.</param>
    /// <param name="unmodified">What is left of it with the modifiers taken off.</param>
    /// <param name="place">Where it was written, such as "an argument of `COUNT()`".</param>
    public Diagnostic MisplacedModifier(ModifierSyntax modifier, ExpressionSyntax outer, ExpressionSyntax unmodified, string place) =>
        new(Codes.MisplacedModifier, modifier.Span, ErrorMessages.MisplacedModifier(Keyword(modifier), place),
            [new Suggestion(outer.Span, Text(unmodified))]);

    public Diagnostic ContradictoryQuantifiers(ModifierSyntax inner, ModifierSyntax outer) =>
        new(Codes.ContradictoryQuantifiers, inner.Span, ErrorMessages.ContradictoryQuantifiers(Keyword(inner), Keyword(outer)));

    public Diagnostic LiterallyNotString(ModifierSyntax literally, ExpressionSyntax core, GoroType type) =>
        new(Codes.LiterallyNotString, literally.Span, ErrorMessages.LiterallyNotString(Keyword(literally), Text(core), type));

    public Diagnostic LiterallyOnStateTest(ModifierSyntax literally) =>
        new(Codes.LiterallyOnStateTest, literally.Span, ErrorMessages.LiterallyOnStateTest(Keyword(literally)),
            [new Suggestion(literally.Span, Text(literally.Operand))]);

    public Diagnostic QuantifierOnAbsentTest(ModifierSyntax quantifier) =>
        new(Codes.QuantifierOnAbsentTest, quantifier.Span, ErrorMessages.QuantifierOnAbsentTest(),
            [new Suggestion(quantifier.Span, Text(quantifier.Operand))]);

    // ---------------------------------------------------------------------------------------------
    // Functions

    public Diagnostic WrongArgumentCount(FunctionCallSyntax call, int expected) =>
        new(Codes.WrongArgumentCount, call.Span, ErrorMessages.WrongArgumentCount(call.Name.Text, expected, call.Arguments.Length));

    public Diagnostic FallbackDefaultNotLiteral(FunctionCallSyntax call) =>
        new(Codes.FallbackDefaultNotLiteral, call.Arguments[1].Span, ErrorMessages.FallbackDefaultNotLiteral(call.Name.Text, Text(call.Arguments[1])));

    public Diagnostic InvalidNumberLiteral(FunctionCallSyntax call, LiteralSyntax literal) =>
        new(Codes.InvalidNumberLiteral, literal.Span, ErrorMessages.InvalidNumberLiteral(Text(literal), call.Name.Text));

    // ---------------------------------------------------------------------------------------------
    // Ranges

    /// <summary><c>60..120kb</c>: a number at one end and a unit at the other.</summary>
    public Diagnostic RangeUnitMissing(BetweenSyntax between)
    {
        var (number, unit) = between.Minimum.Token is NumberToken
            ? (between.Minimum, between.Maximum)
            : (between.Maximum, between.Minimum);
        var written = Text(number);
        var suffix = UnitSuffix(unit.Token);
        var candidate = written + suffix;
        ImmutableArray<Suggestion> suggestions = suffix is not null
            && Lexer.Lex(candidate) is { Succeeded: true, Value: [var token, { Kind: TokenKind.EndOfText }] }
            && token.Kind == unit.Token.Kind
            ? [new Suggestion(number.Span, candidate)]
            : [];
        var message = suffix is null
            ? ErrorMessages.RangeUnitMissing(written)
            : ErrorMessages.RangeUnitAmbiguous(written, TypedNodes.TypeOf(unit.Token), candidate);
        return new Diagnostic(Codes.RangeUnitMissing, number.Span, message, suggestions);
    }

    public Diagnostic RangeEndpointTypes(BetweenSyntax between, GoroType minimum, GoroType maximum) =>
        new(Codes.RangeEndpointTypes, RangeSpan(between), ErrorMessages.RangeEndpointTypes(Text(between.Minimum), minimum, Text(between.Maximum), maximum));

    public Diagnostic RangeReversed(BetweenSyntax between, GoroType type, ComparisonMode mode)
    {
        var (range, minimum, maximum) = (Text(RangeSpan(between)), Text(between.Minimum), Text(between.Maximum));
        var message = type != GoroType.String ? ErrorMessages.RangeReversed(range, minimum, maximum)
            : mode == ComparisonMode.Literal ? ErrorMessages.RangeReversedLiteral(range, minimum, maximum)
            : ErrorMessages.RangeReversedNormalized(range, minimum, maximum);
        var swapped = Text(between.Maximum) + Text(TextSpan.FromBounds(between.Minimum.Span.End, between.Maximum.Span.Start)) + Text(between.Minimum);
        return new Diagnostic(Codes.RangeReversed, RangeSpan(between), message,
            [new Suggestion(RangeSpan(between), swapped)]);
    }

    // ---------------------------------------------------------------------------------------------
    // Patterns

    public Diagnostic Pattern(StringToken pattern, PatternFailure failure) => failure switch
    {
        PatternFailure.Malformed malformed => new Diagnostic(Codes.InvalidPattern, PatternSpan(pattern, malformed.Offset),
            ErrorMessages.InvalidPattern(Text(pattern.Span), malformed.Reason)),
        PatternFailure.Unsupported unsupported => new Diagnostic(Codes.UnsupportedPatternConstruct, pattern.Span,
            ErrorMessages.UnsupportedPatternConstruct(unsupported.Family)),
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

    // ---------------------------------------------------------------------------------------------

    /// <summary>A string literal whose contents would be a literal of the type wanted, written without its quotes.</summary>
    private static Suggestion? Unquoted(Bound side, GoroType? wanted)
    {
        if (side.Literal?.Token is not StringToken text || wanted is null)
        {
            return null;
        }

        if (Lexer.Lex(text.Value) is not { Succeeded: true, Value: [var token, { Kind: TokenKind.EndOfText }] }
            || !TokenFacts.IsLiteral(token.Kind))
        {
            return null;
        }

        var type = TypedNodes.TypeOf(token);
        var fits = type == wanted || (type == GoroType.Number && wanted is GoroType.ByteCount or GoroType.Duration);
        return fits ? new Suggestion(side.Literal.Span, text.Value) : null;
    }

    private static bool OnlyALiteralStandsIn(Bound number, Bound unit) =>
        number.Type == GoroType.Number && number.Literal is null && unit.Type is GoroType.ByteCount or GoroType.Duration;

    /// <summary>The unit a bytecount or a single-unit duration was written with: <c>kb</c> of <c>120kb</c>.</summary>
    private string? UnitSuffix(Token unit)
    {
        var written = Text(unit.Span);
        return unit.Kind switch
        {
            TokenKind.ByteCount => written.TrimStart("0123456789.".ToCharArray()),
            TokenKind.Duration when SingleUnitDuration().Match(written) is { Success: true } match => match.Groups[1].Value,
            _ => null,
        };
    }

    [GeneratedRegex("^[0-9]+([hmsHMS])$")]
    private static partial Regex SingleUnitDuration();

    private string Universal(ExpressionSyntax operand, bool wrap) => wrap ? $"ALL({Text(operand)})" : Text(operand);

    private static TextSpan RangeSpan(BetweenSyntax between) => TextSpan.Covering(between.Minimum.Span, between.Maximum.Span);

    private static string Keyword(ModifierSyntax modifier) => modifier.Modifier.ToString().ToUpperInvariant();

    private string Text(SyntaxNode node) => Text(node.Span);

    private string Text(TextSpan span) => span.Of(text);
}
