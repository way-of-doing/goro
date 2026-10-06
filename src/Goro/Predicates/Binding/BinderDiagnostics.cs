using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Goro.Messages;
using Goro.Predicates.Diagnostics;
using Goro.Predicates.Identifiers;
using Goro.Predicates.Syntax;
using Goro.Predicates.Values;
using Codes = Goro.Predicates.Binding.SemanticDiagnosticCodes;

namespace Goro.Predicates.Binding;

/// <summary>The binder's errors, about names, types and modifiers, and the rewrites it offers for them.</summary>
internal sealed partial class BinderDiagnostics(string text) : SemanticDiagnostics(text)
{
    private static readonly string[] Functions = ["COUNT", "FALLBACK", "NUMBER", "PREFERRED", "STRING"];

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

    public Diagnostic TypeMismatch(
        ComparisonSyntax comparison, ExpressionSyntax leftCore, SemanticExpression left, ExpressionSyntax rightCore, SemanticExpression right)
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
            else if (side.Type == GoroType.String && side is not SemanticLiteral && other.Type == GoroType.Number)
            {
                suggestions.Add(new Suggestion(sideCore.Span, $"NUMBER({Text(sideCore)})"));
            }
        }

        return new Diagnostic(Codes.TypeMismatch, comparison.Span, message, suggestions.ToImmutable());
    }

    /// <summary>
    /// An argument of a function whose arguments must agree in type, such as the default of
    /// <c>FALLBACK()</c>, that does not agree with the one whose type the call took.
    /// </summary>
    /// <param name="expected">The position of the argument whose type the call took.</param>
    /// <param name="mismatched">The position of the argument that does not agree with it.</param>
    public Diagnostic ArgumentTypeMismatch(
        FunctionCallSyntax call, int expected, SemanticExpression expectedArgument, int mismatched, SemanticExpression argument)
    {
        var expectedSyntax = call.Arguments[expected];
        var argumentSyntax = call.Arguments[mismatched];
        ImmutableArray<Suggestion> suggestions = Unquoted(argument, expectedArgument.Type) is { } unquoted ? [unquoted] : [];
        return new Diagnostic(Codes.TypeMismatch, argumentSyntax.Span,
            new ErrorMessage.ArgumentTypeMismatch(new Code(call.Name.Text), Code(expectedSyntax), expectedArgument.Type!.Value, Code(argumentSyntax), argument.Type!.Value),
            suggestions);
    }

    public Diagnostic RangeTypeMismatch(BetweenSyntax between, ExpressionSyntax subjectCore, GoroType subject, GoroType range) =>
        new(Codes.TypeMismatch, between.Span, new ErrorMessage.RangeTypeMismatch(Code(subjectCore), subject, Code(RangeSpan(between)), range));

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

    public Diagnostic NotACondition(ExpressionSyntax syntax, GoroType type) =>
        new(Codes.NotACondition, syntax.Span, new ErrorMessage.NotACondition(Code(syntax), type));

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

    public Diagnostic TooFewArguments(FunctionCallSyntax call, int minimum) =>
        new(Codes.WrongArgumentCount, call.Span, new ErrorMessage.TooFewArguments(new Code(call.Name.Text), minimum, call.Arguments.Length));

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
            : new ErrorMessage.RangeUnitAmbiguous(new Code(written), UnitOf(SemanticLiteral.TypeOf(unit.Token)), new Code(candidate));
        return new Diagnostic(Codes.RangeUnitMissing, number.Span, message, suggestions);
    }

    public Diagnostic RangeEndpointTypes(BetweenSyntax between, GoroType minimum, GoroType maximum) =>
        new(Codes.RangeEndpointTypes, RangeSpan(between), new ErrorMessage.RangeEndpointTypes(Code(between.Minimum), minimum, Code(between.Maximum), maximum));

    // ---------------------------------------------------------------------------------------------

    /// <summary>A string literal whose contents would be a literal of the type wanted, written without its quotes.</summary>
    private static Suggestion? Unquoted(SemanticExpression side, GoroType? wanted)
    {
        if (side is not SemanticLiteral { Token: StringToken text } literal || wanted is null)
        {
            return null;
        }

        if (Lexer.Lex(text.Value) is not { Succeeded: true, Value: [var token, { Kind: TokenKind.EndOfText }] }
            || !TokenFacts.IsLiteral(token.Kind))
        {
            return null;
        }

        var type = SemanticLiteral.TypeOf(token);
        var fits = type == wanted || (type == GoroType.Number && wanted is GoroType.ByteCount or GoroType.Duration);
        return fits ? new Suggestion(literal.Syntax.Span, text.Value) : null;
    }

    private static bool OnlyALiteralStandsIn(SemanticExpression number, SemanticExpression unit) =>
        number.Type == GoroType.Number && number is not SemanticLiteral && unit.Type is GoroType.ByteCount or GoroType.Duration;

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
}
