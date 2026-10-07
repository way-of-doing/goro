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
    private static readonly string[] Functions = ["COUNT", "FALLBACK", "PREFERRED"];

    private static readonly string[] Targets = ["NUMBER", "STRING", "DURATION", "BYTECOUNT"];

    // ---------------------------------------------------------------------------------------------
    // Names

    /// <summary>A source Goro does not define, before an identifier or a call; offers the closest.</summary>
    public Diagnostic UnknownSource(NameToken source, IEnumerable<string> known)
    {
        var closest = Spelling.Closest(source.Text, known);
        ImmutableArray<Suggestion> suggestions = closest is null ? [] : [new Suggestion(source.Span, closest)];
        return new Diagnostic(Codes.UnknownSource, source.Span, new ErrorMessage.UnknownSource(new Code(source.Text)), suggestions);
    }

    /// <summary>A concept that does not exist, or that the identifier's source cannot supply; offers the closest there is.</summary>
    public Diagnostic UnknownIdentifier(IdentifierSyntax identifier, IEnumerable<string> candidates)
    {
        var closest = Spelling.Closest(identifier.Name.Text, candidates);
        ImmutableArray<Suggestion> suggestions = closest is null ? [] : [new Suggestion(identifier.Name.Span, closest)];
        var message = identifier.Source is { } source
            ? (ErrorMessage)new ErrorMessage.UnknownIdentifierInSource(new Code(source.Text), new Code(identifier.Name.Text))
            : new ErrorMessage.UnknownIdentifier(new Code(identifier.Name.Text));
        return new Diagnostic(Codes.UnknownIdentifier, identifier.Span, message, suggestions);
    }

    /// <summary>A concept of the <c>file</c> source, written without it.</summary>
    public Diagnostic NeedsSource(IdentifierSyntax identifier, string source) =>
        new(Codes.NeedsSource, identifier.Span, new ErrorMessage.ConceptNeedsSource(Code(identifier), new Code(source)),
            [new Suggestion(identifier.Span, $"{source}::{Text(identifier)}")]);

    /// <summary><c>vorbis::field</c>, a source function without the arguments that would make it a call.</summary>
    public Diagnostic SourceFunctionNeedsArguments(IdentifierSyntax identifier) =>
        new(Codes.SourceFunctionNeedsArguments, identifier.Span, new ErrorMessage.SourceFunctionNeedsArguments(Code(identifier)));

    /// <summary><c>field("MOOD")</c>, a source function written without a source, which it cannot do without.</summary>
    public Diagnostic SourceFunctionNeedsSource(FunctionCallSyntax call) =>
        new(Codes.NeedsSource, call.Span, new ErrorMessage.SourceFunctionNeedsSource(new Code(call.Name.Text)));

    /// <summary>
    /// A qualified call of something that is not a source function of that source. A function written
    /// with a source, as <c>ape::count(x)</c>, is offered without it; anything else, the closest
    /// source function.
    /// </summary>
    public Diagnostic UnknownSourceFunction(FunctionCallSyntax call, IEnumerable<string> candidates)
    {
        var source = call.Source!;
        ImmutableArray<Suggestion> suggestions;
        if (Functions.Contains(call.Name.Text, StringComparer.OrdinalIgnoreCase))
        {
            suggestions = [new Suggestion(call.Span, Text(TextSpan.FromBounds(call.Name.Span.Start, call.Span.End)))];
        }
        else
        {
            var closest = Spelling.Closest(call.Name.Text, candidates);
            suggestions = closest is null ? [] : [new Suggestion(call.Name.Span, closest)];
        }

        return new Diagnostic(Codes.UnknownFunction, TextSpan.Covering(source.Span, call.Name.Span),
            new ErrorMessage.UnknownSourceFunction(new Code(source.Text), new Code(call.Name.Text)), suggestions);
    }

    /// <summary>An argument of a source function that is not a string literal.</summary>
    public Diagnostic ArgumentNotLiteral(FunctionCallSyntax call, ExpressionSyntax argument) =>
        new(Codes.ArgumentNotLiteral, argument.Span, new ErrorMessage.SourceFunctionArgumentNotLiteral(Code(Callee(call)), Code(argument)));

    /// <summary>An argument a source function rejected, with the rewrite that would make it acceptable, where there is one.</summary>
    public Diagnostic ArgumentRejected(FunctionCallSyntax call, int index, ArgumentRejection rejection)
    {
        var argument = call.Arguments[index];
        var (message, suggestions) = Rejection(call, argument, rejection);
        return new Diagnostic(Codes.ArgumentRejected, argument.Span, message, suggestions);
    }

    private (ErrorMessage Message, ImmutableArray<Suggestion> Suggestions) Rejection(
        FunctionCallSyntax call, ExpressionSyntax argument, ArgumentRejection rejection) =>
        rejection switch
        {
            ArgumentRejection.MalformedFrame(var written) =>
                (new ErrorMessage.MalformedFrame(new Code(written)), []),
            ArgumentRejection.FrameRenamed(var written, var renamed) =>
                (new ErrorMessage.FrameRenamed(new Code(written), new Code(renamed)), [new Suggestion(argument.Span, $"\"{renamed}\"")]),
            ArgumentRejection.FrameNotText(var frame) =>
                (new ErrorMessage.FrameNotText(new Code(frame)), call.Arguments.Length == 1 ? [new Suggestion(call.Name.Span, "bytes")] : []),
            ArgumentRejection.DescriptionNotTaken(var frame) =>
                (new ErrorMessage.DescriptionNotTaken(new Code(frame)),
                    [new Suggestion(call.Span, $"{Text(TextSpan.FromBounds(call.Span.Start, call.Arguments[0].Span.End))})")]),
            _ => throw new ArgumentOutOfRangeException(nameof(rejection), rejection, "Not a rejection the binder knows."),
        };

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
                suggestions.Add(new Suggestion(sideCore.Span, $"{Text(sideCore)} AS NUMBER"));
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

    public Diagnostic BooleanNotConvertible(ExpressionSyntax operand) =>
        new(Codes.BooleanNotConvertible, operand.Span, new ErrorMessage.BooleanNotConvertible(Code(operand)));

    /// <summary>
    /// A blob as the operand of an operator that would have to look inside it: a comparison, whose
    /// operator is at <paramref name="comparison"/>, or <c>BETWEEN</c> where that is null.
    /// </summary>
    public Diagnostic BlobOperand(ExpressionSyntax operand, TextSpan? comparison) =>
        new(Codes.BlobNotUsable, operand.Span,
            new ErrorMessage.BlobOperand(Code(operand), comparison is { } span ? Code(span) : new Code("BETWEEN")));

    public Diagnostic BlobNotConvertible(ExpressionSyntax operand) =>
        new(Codes.BlobNotUsable, operand.Span, new ErrorMessage.BlobNotConvertible(Code(operand)));

    public Diagnostic BlobInFallback(ExpressionSyntax argument) =>
        new(Codes.BlobNotUsable, argument.Span, new ErrorMessage.BlobInFallback(Code(argument)));

    public Diagnostic UnitsDoNotConvert(ExpressionSyntax operand, GoroType from) =>
        new(Codes.UnitsDoNotConvert, operand.Span, new ErrorMessage.UnitsDoNotConvert(Code(operand), UnitOf(from)));

    /// <summary>A target <c>AS</c> does not know; offers the closest, in the case the user wrote in.</summary>
    public Diagnostic UnknownTarget(NameToken target)
    {
        var closest = Spelling.Closest(target.Text, Targets);
        if (closest is not null && target.Text == target.Text.ToLowerInvariant())
        {
            closest = closest.ToLowerInvariant();
        }

        ImmutableArray<Suggestion> suggestions = closest is null ? [] : [new Suggestion(target.Span, closest)];
        return new Diagnostic(Codes.UnknownTarget, target.Span, new ErrorMessage.UnknownTarget(new Code(target.Text)), suggestions);
    }

    /// <summary>
    /// <c>ALL(x) AS NUMBER</c>: offers <c>ALL(x AS NUMBER)</c>, the conversion moved inside the
    /// modifiers, which go on the outside of it.
    /// </summary>
    public Diagnostic ModifierOnConversion(ModifierSyntax modifier, AsSyntax conversion, ExpressionSyntax unmodified)
    {
        var operand = conversion.Operand;
        var rewritten = Text(TextSpan.FromBounds(operand.Span.Start, unmodified.Span.Start))
            + Text(unmodified) + " AS " + conversion.Target.Text
            + Text(TextSpan.FromBounds(unmodified.Span.End, operand.Span.End));
        return new Diagnostic(Codes.MisplacedModifier, modifier.Span, new ErrorMessage.MisplacedModifierInConversion(Keyword(modifier)),
            [new Suggestion(conversion.Span, rewritten)]);
    }

    /// <summary><c>NUMBER(x)</c>: offers <c>x AS NUMBER</c>, parenthesizing an argument that binds more loosely.</summary>
    public Diagnostic ConversionCalled(FunctionCallSyntax call)
    {
        var argument = call.Arguments[0];
        var operand = argument is ComparisonSyntax or BetweenSyntax or MatchSyntax or StateTestSyntax or NotSyntax or LogicalSyntax
            ? $"({Text(argument)})"
            : Text(argument);
        return new Diagnostic(Codes.ConversionCalled, call.Span, new ErrorMessage.ConversionCalled(new Code(call.Name.Text)),
            [new Suggestion(call.Span, $"{operand} AS {call.Name.Text}")]);
    }

    /// <summary><c>CAST(x AS NUMBER)</c>: offers <c>x AS NUMBER</c>.</summary>
    public Diagnostic CastCalled(FunctionCallSyntax call, AsSyntax conversion) =>
        new(Codes.ConversionCalled, call.Span, new ErrorMessage.CastCalled(new Code(call.Name.Text)),
            [new Suggestion(call.Span, Text(conversion))]);

    public Diagnostic MatchSubjectNotString(ExpressionSyntax subjectCore, GoroType type)
    {
        ImmutableArray<Suggestion> suggestions = type is GoroType.Number or GoroType.ByteCount or GoroType.Duration
            ? [new Suggestion(subjectCore.Span, $"{Text(subjectCore)} AS STRING")]
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

    /// <summary>A source function given a number of arguments outside what it takes.</summary>
    public Diagnostic WrongArgumentCount(FunctionCallSyntax call, int minimum, int maximum) =>
        new(Codes.WrongArgumentCount, call.Span, minimum == maximum
            ? new ErrorMessage.WrongArgumentCount(Code(Callee(call)), minimum, call.Arguments.Length)
            : new ErrorMessage.WrongArgumentCountBetween(Code(Callee(call)), minimum, maximum, call.Arguments.Length));

    /// <summary>The function a call names, with its source where it was written with one.</summary>
    private static TextSpan Callee(FunctionCallSyntax call) =>
        TextSpan.Covering(call.Source?.Span ?? call.Name.Span, call.Name.Span);

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
