// Owned by the binder group (G4) of the predicate-runtime-architecture line.
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Goro.Messages;
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
        return new Diagnostic(Codes.UnknownNamespace, identifier.Span, new ErrorMessage.UnknownNamespace(new Code(written)), suggestions);
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
            ? (ErrorMessage)new ErrorMessage.UnknownIdentifier(new Code(name.Text))
            : new ErrorMessage.UnknownIdentifierInNamespace(Code(TextSpan.FromBounds(identifier.Parts[0].Span.Start, identifier.Parts[^2].Span.End)), new Code(name.Text));
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
        return new Diagnostic(Codes.UnknownFunction, name.Span, new ErrorMessage.UnknownFunction(new Code(name.Text)), suggestions);
    }

    public Diagnostic NullValue(TextSpan span) =>
        new(Codes.NullValue, span, new ErrorMessage.NullValue());

    /// <summary><c>x == NULL</c> or <c>x != NULL</c>, which is a state test written the way SQL writes it.</summary>
    public Diagnostic NullComparison(ComparisonSyntax comparison, NullSyntax @null, ExpressionSyntax other)
    {
        var negation = comparison.Operator == ComparisonOperator.NotEqual ? "NOT " : "";
        return new Diagnostic(Codes.NullValue, @null.Span, new ErrorMessage.NullComparison(),
            [new Suggestion(comparison.Span, $"{negation}{Text(other)} IS ABSENT")]);
    }

    // ---------------------------------------------------------------------------------------------
    // Types

    public Diagnostic TypeMismatch(ComparisonSyntax comparison, ExpressionSyntax leftCore, Bound left, ExpressionSyntax rightCore, Bound right)
    {
        var op = Code(comparison.OperatorSpan);
        var message = OnlyALiteralStandsIn(left, right) || OnlyALiteralStandsIn(right, left)
            ? (ErrorMessage)new ErrorMessage.TypeMismatchNumberVariable(Code(leftCore), left.Type!.Value, Code(rightCore), right.Type!.Value, op)
            : new ErrorMessage.TypeMismatch(Code(leftCore), left.Type!.Value, Code(rightCore), right.Type!.Value, op);

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
            new ErrorMessage.FallbackTypeMismatch(new Code(call.Name.Text), Code(argumentSyntax), argument.Type!.Value, Code(defaultSyntax), @default.Type!.Value),
            suggestions);
    }

    public Diagnostic RangeTypeMismatch(BetweenSyntax between, ExpressionSyntax subjectCore, GoroType subject, GoroType range) =>
        new(Codes.TypeMismatch, between.Span, new ErrorMessage.RangeTypeMismatch(Code(subjectCore), subject, Code(RangeSpan(between)), range));

    public Diagnostic NegativeUnitLiteral(LiteralSyntax literal, GoroType unit) =>
        new(Codes.NegativeUnitLiteral, literal.Span, new ErrorMessage.NegativeUnitLiteral(Code(literal), UnitOf(unit)));

    public Diagnostic FractionalDurationLiteral(LiteralSyntax literal) =>
        new(Codes.FractionalDurationLiteral, literal.Span, new ErrorMessage.FractionalDurationLiteral(Code(literal)));

    public Diagnostic BooleanCompared(ComparisonSyntax comparison) =>
        new(Codes.BooleanNotOrdered, comparison.Span, new ErrorMessage.BooleanCompared(Code(comparison.OperatorSpan)));

    public Diagnostic BooleanBetween(ExpressionSyntax subjectCore) =>
        new(Codes.BooleanNotOrdered, subjectCore.Span, new ErrorMessage.BooleanBetween(Code(subjectCore)));

    public Diagnostic BooleanRange(BetweenSyntax between) =>
        new(Codes.BooleanNotOrdered, RangeSpan(between), new ErrorMessage.BooleanRange());

    public Diagnostic BooleanNotConvertible(FunctionCallSyntax call) =>
        new(Codes.BooleanNotConvertible, call.Arguments[0].Span, new ErrorMessage.BooleanNotConvertible(new Code(call.Name.Text), Code(call.Arguments[0])));

    public Diagnostic MatchSubjectNotString(ExpressionSyntax subjectCore, GoroType type)
    {
        ImmutableArray<Suggestion> suggestions = type is GoroType.Number or GoroType.ByteCount or GoroType.Duration
            ? [new Suggestion(subjectCore.Span, $"STRING({Text(subjectCore)})")]
            : [];
        return new Diagnostic(Codes.MatchSubjectNotString, subjectCore.Span, new ErrorMessage.MatchSubjectNotString(Code(subjectCore), type), suggestions);
    }

    // ---------------------------------------------------------------------------------------------
    // Definiteness

    public Diagnostic NotACondition(ExpressionSyntax syntax, GoroType type) =>
        new(Codes.NotACondition, syntax.Span, new ErrorMessage.NotACondition(Code(syntax), type));

    public Diagnostic IndefiniteCondition(ExpressionSyntax syntax) =>
        new(Codes.IndefiniteCondition, syntax.Span, new ErrorMessage.IndefiniteCondition(Code(syntax)),
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
        ErrorMessage message = (leftCore, rightCore) switch
        {
            ({ } l, { } r) => new ErrorMessage.AmbiguousNotEqualBoth(Code(l), Code(r)),
            ({ } l, null) => new ErrorMessage.AmbiguousNotEqual(Code(l)),
            (null, { } r) => new ErrorMessage.AmbiguousNotEqual(Code(r)),
            _ => throw new ArgumentException("Some operand must need a quantifier."),
        };
        return new Diagnostic(Codes.AmbiguousNotEqual, comparison.OperatorSpan, message,
            [new Suggestion(comparison.Span, negated), new Suggestion(comparison.Span, universal)]);
    }

    // ---------------------------------------------------------------------------------------------
    // Modifiers

    /// <param name="outer">The expression the modifier was found in, parentheses and all.</param>
    /// <param name="unmodified">What is left of it with the modifiers taken off.</param>
    /// <param name="function">The function it was written in an argument of, if it was.</param>
    public Diagnostic MisplacedModifier(ModifierSyntax modifier, ExpressionSyntax outer, ExpressionSyntax unmodified, NameToken? function) =>
        new(Codes.MisplacedModifier, modifier.Span,
            function is null
                ? new ErrorMessage.MisplacedModifier(Keyword(modifier))
                : new ErrorMessage.MisplacedModifierInCall(Keyword(modifier), new Code(function.Text)),
            [new Suggestion(outer.Span, Text(unmodified))]);

    public Diagnostic ContradictoryQuantifiers(ModifierSyntax inner, ModifierSyntax outer) =>
        new(Codes.ContradictoryQuantifiers, inner.Span, new ErrorMessage.ContradictoryQuantifiers(Keyword(inner), Keyword(outer)));

    public Diagnostic LiterallyNotString(ModifierSyntax literally, ExpressionSyntax core, GoroType type) =>
        new(Codes.LiterallyNotString, literally.Span, new ErrorMessage.LiterallyNotString(Keyword(literally), Code(core), type));

    public Diagnostic LiterallyOnStateTest(ModifierSyntax literally) =>
        new(Codes.LiterallyOnStateTest, literally.Span, new ErrorMessage.LiterallyOnStateTest(Keyword(literally)),
            [new Suggestion(literally.Span, Text(literally.Operand))]);

    public Diagnostic QuantifierOnAbsentTest(ModifierSyntax quantifier) =>
        new(Codes.QuantifierOnAbsentTest, quantifier.Span, new ErrorMessage.QuantifierOnAbsentTest(),
            [new Suggestion(quantifier.Span, Text(quantifier.Operand))]);

    // ---------------------------------------------------------------------------------------------
    // Functions

    public Diagnostic WrongArgumentCount(FunctionCallSyntax call, int expected) =>
        new(Codes.WrongArgumentCount, call.Span, new ErrorMessage.WrongArgumentCount(new Code(call.Name.Text), expected, call.Arguments.Length));

    public Diagnostic FallbackDefaultNotLiteral(FunctionCallSyntax call) =>
        new(Codes.FallbackDefaultNotLiteral, call.Arguments[1].Span, new ErrorMessage.FallbackDefaultNotLiteral(new Code(call.Name.Text), Code(call.Arguments[1])));

    public Diagnostic InvalidNumberLiteral(FunctionCallSyntax call, LiteralSyntax literal) =>
        new(Codes.InvalidNumberLiteral, literal.Span, new ErrorMessage.InvalidNumberLiteral(Code(literal), new Code(call.Name.Text)));

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
            ? (ErrorMessage)new ErrorMessage.RangeUnitMissing(new Code(written))
            : new ErrorMessage.RangeUnitAmbiguous(new Code(written), UnitOf(TypedNodes.TypeOf(unit.Token)), new Code(candidate));
        return new Diagnostic(Codes.RangeUnitMissing, number.Span, message, suggestions);
    }

    public Diagnostic RangeEndpointTypes(BetweenSyntax between, GoroType minimum, GoroType maximum) =>
        new(Codes.RangeEndpointTypes, RangeSpan(between), new ErrorMessage.RangeEndpointTypes(Code(between.Minimum), minimum, Code(between.Maximum), maximum));

    public Diagnostic RangeReversed(BetweenSyntax between, GoroType type, ComparisonMode mode)
    {
        var order = type != GoroType.String ? RangeOrder.Plain
            : mode == ComparisonMode.Literal ? RangeOrder.Literal
            : RangeOrder.Normalized;
        var message = new ErrorMessage.RangeReversed(Code(RangeSpan(between)), Code(between.Minimum), Code(between.Maximum), order);
        var swapped = Text(between.Maximum) + Text(TextSpan.FromBounds(between.Minimum.Span.End, between.Maximum.Span.Start)) + Text(between.Minimum);
        return new Diagnostic(Codes.RangeReversed, RangeSpan(between), message,
            [new Suggestion(RangeSpan(between), swapped)]);
    }

    // ---------------------------------------------------------------------------------------------
    // Patterns

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

    private static Code Keyword(ModifierSyntax modifier) => new(modifier.Modifier.ToString().ToUpperInvariant());

    /// <summary>Bytecount or duration: the only types whose literals carry a unit.</summary>
    private static UnitType UnitOf(GoroType type) => type switch
    {
        GoroType.ByteCount => UnitType.ByteCount,
        GoroType.Duration => UnitType.Duration,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Only a bytecount or a duration has a unit."),
    };

    private string Text(SyntaxNode node) => Text(node.Span);

    private string Text(TextSpan span) => span.Of(text);

    private Code Code(SyntaxNode node) => new(Text(node));

    private Code Code(TextSpan span) => new(Text(span));
}
