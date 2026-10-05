using System.Collections.Immutable;
using Goro.Predicates.Values;
using static Goro.Messages.ErrorMessage;

// Every switch on an enum below names each of its values, so that a value added to the enum fails
// the build (CS8509, an error in this project) until it has a sentence. A number cast to the enum
// that names no value would still get past one; that is all this warning is about.
#pragma warning disable CS8524

namespace Goro.Messages;

/// <summary>
/// The wording of every error Goro reports, in English, in one place, so that it can be read and
/// rewritten together.
/// </summary>
/// <remarks>
/// Each message is spelled out whole: where the wording varies with a case, each case has a
/// sentence of its own, and nothing is ever assembled from fragments. A placeholder takes data
/// (the user's text as written, a name Goro spells, a type's name, a number, or the regex engine's
/// reason), never punctuation: backticks and the rest belong to the sentence. Warnings are worded
/// where they are raised.
/// </remarks>
public sealed class EnglishErrorMessages : IErrorMessages
{
    public static EnglishErrorMessages Instance { get; } = new();

    private EnglishErrorMessages()
    {
    }

    public string ErrorPrefix => "goro: error: ";

    public string SuggestionLabel => "try: ";

    public string Render(ErrorMessage message) => message switch
    {
        // -----------------------------------------------------------------------------------------
        // Lexical

        NonAsciiName => "Names are plain ASCII — quote the fancy ones, goro!",
        UnknownEscape(var escape) => $"`{escape}` isn't an escape — double the backslash, goro!",
        HexEscapeDigits => "`\\x` wants exactly two hex digits, goro!",
        UnicodeEscapeDigits => "`\\u` wants four hex digits, or up to six in braces, goro!",
        BracedEscapeDigits => "`\\u{...}` wants one to six hex digits and a `}`, goro!",
        CodePointOutOfRange(var escape) => $"`{escape}` is past the last code point U+10FFFF, goro!",
        SurrogateCodePoint(var escape) => $"`{escape}` isn't a character but a surrogate, goro!",
        UnterminatedString => "This string never closes its quote, goro!",
        LoneSurrogate(var escape) => $"`{escape}` lost its other surrogate half, goro!",
        TrailingPeriod => "A period needs a digit after it, goro!",
        ClockFieldOutOfRange(var field) => $"`{field}`? Minutes and seconds stop at 59, goro!",
        UnrepresentableLiteral(var literal) => $"`{literal}` is too heavy to hold, goro!",
        SignedClockDuration => "Durations can't go negative, goro!",
        MalformedClockDuration => "Clock durations look like `1:30` or `1:02:03`, goro!",
        UnexpectedCharacter(var character) => $"`{character}` has no business out here, goro!",

        // -----------------------------------------------------------------------------------------
        // Syntax

        EmptyPredicate => "Give the filter something to check, goro!",
        UnexpectedEnd(var expected) => expected switch
        {
            Expectation.OperatorOrEnd => "An operator is probably missing there, goro!",
            Expectation.RangeSeparator => "A `..` is probably missing there, goro!",
            Expectation.State => "`USABLE`, `UNUSABLE` or `ABSENT` is probably missing there, goro!",
            Expectation.Literal => "A literal is probably missing there, goro!",
            Expectation.Operand => "An operand is probably missing there, goro!",
            Expectation.CloseParen => "A `)` is probably missing there, goro!",
            Expectation.CommaOrCloseParen => "A `,` or `)` is probably missing there, goro!",
            Expectation.Name => "A name is probably missing after `::`, goro!",
            Expectation.NameOrQuotedName => "A name, or a quoted name, is probably missing after `::`, goro!",
        },
        UnexpectedToken(var expected, var found) => expected switch
        {
            Expectation.OperatorOrEnd => $"Expected an operator or the end of the predicate, found `{found}`, goro!",
            Expectation.RangeSeparator => $"Expected `..` between the two ends of the range, found `{found}`, goro!",
            Expectation.State => $"Expected `USABLE`, `UNUSABLE` or `ABSENT`, found `{found}`, goro!",
            Expectation.Literal => $"Expected a literal, found `{found}`, goro!",
            Expectation.Operand => $"Expected an operand, found `{found}`, goro!",
            Expectation.CloseParen => $"Expected `)`, found `{found}`, goro!",
            Expectation.CommaOrCloseParen => $"Expected `,` or `)`, found `{found}`, goro!",
            Expectation.Name => $"Expected a name after `::`, found `{found}`, goro!",
            Expectation.NameOrQuotedName => $"Expected a name, or a quoted name, after `::`, found `{found}`, goro!",
        },
        ChainedComparison => "One comparison at a time, goro!",
        PatternNotRawString => "A pattern is a raw string like `r\"^the \"`, goro!",
        ModifiedPattern => "Modifiers ride on the subject, not the pattern, goro!",
        ParenthesizedPattern => "The pattern goes without parentheses, goro!",
        QuotedPattern => "Patterns travel as raw strings, goro!",
        IsNot => "`NOT` goes in front, goro!",
        IsNull => "It's not null — it's `ABSENT`, goro!",
        RangeEndpointNotLiteral => "Range limits go bare, goro!",
        ModifierAsWord(var modifier) => $"`{modifier}` wraps around like `{modifier}(...)`, goro!",
        ReservedWord(var word) => $"`{word}` is a keyword, not a name, goro!",
        ReservedWordInIdentifier(var word) => $"`{word}` can't start a name without a leading `::`, goro!",
        WhitespaceInIdentifier => "We don't need spaces around `::`, goro!",
        QualifiedFunctionCall => "Functions don't live in namespaces, goro!",
        BorrowedEqual => "We compare with `==`, goro!",
        BorrowedLessGreater => "Unequal is `!=` around here, goro!",
        BorrowedAnd => "We say `AND` here, goro!",
        BorrowedOr => "We say `OR` here, goro!",
        BorrowedBang => "We say `NOT` here, goro!",
        BorrowedTildeEqual => "`~=` isn't ours — pick one below, goro!",
        BorrowedBangTilde => "No negated match — lead with `NOT`, goro!",
        SignedUnitLiteral => "Sizes and durations can't go negative, goro!",
        FractionalDurationFields => "Duration fields are whole numbers, like `1h30m`, goro!",
        RunOnLiteral(_, var literal, var kind, var next) => kind switch
        {
            NumericLiteral.Number => $"`{literal}` is a number running into `{next}`, goro!",
            NumericLiteral.ByteCount => $"`{literal}` is a bytecount running into `{next}`, goro!",
            NumericLiteral.Duration => $"`{literal}` is a duration running into `{next}`, goro!",
        },
        WhitespaceInLiteral(var text) => $"Keep `{text}` in one piece, goro!",

        // -----------------------------------------------------------------------------------------
        // Semantic

        UnknownNamespace(var @namespace) => $"There's no `{@namespace}` namespace, goro!",
        UnknownIdentifier(var name) => $"There's no `{name}` on our map, goro!",
        UnknownIdentifierInNamespace(var @namespace, var name) => $"`{@namespace}` has no `{name}`, goro!",
        UnknownFunction(var name) => $"No `{name}` here — we have `COUNT`, `FALLBACK`, `NUMBER` and `STRING`, goro!",
        NullValue => "No nulls here — absent is the word, goro!",
        NullComparison => "No nulls here — test with `IS ABSENT`, goro!",
        TypeMismatch(_, var left, _, var right, _) => $"`{Name(left)}` and `{Name(right)}` don't compare, goro!",
        TypeMismatchNumberVariable(_, var left, _, var right, _) => $"`{Name(left)}` and `{Name(right)}` don't compare; only a literal number stands in, goro!",
        FallbackTypeMismatch(_, var argument, var argumentType, var @default, var defaultType) => $"`{@default}` is `{Name(defaultType)}`, but `{argument}` is `{Name(argumentType)}`, goro!",
        RangeTypeMismatch(_, var subjectType, _, var rangeType) => $"`{Name(subjectType)}` can't sit in a `{Name(rangeType)}` range, goro!",
        NegativeUnitLiteral(_, var unit) => unit switch
        {
            UnitType.ByteCount => "A bytecount can't go negative, goro!",
            UnitType.Duration => "A duration can't go negative, goro!",
        },
        FractionalDurationLiteral => "Durations count whole seconds, goro!",
        BooleanCompared(var op) => $"Booleans don't do `{op}` — only `==` and `!=`, goro!",
        BooleanBetween => "Booleans don't sit in ranges, goro!",
        BooleanRange => "A range can't run between booleans, goro!",
        BooleanNotConvertible(var function, _) => $"`{function}()` won't take a boolean, goro!",
        MatchSubjectNotString(_, var type) => $"`=~` matches strings, and this is `{Name(type)}`, goro!",
        NotACondition(_, var type) => $"That's `{Name(type)}`, but a condition goes here, goro!",
        IndefiniteCondition(var expression) => $"Compare `{expression}` with `TRUE` or `FALSE` first, goro!",
        AmbiguousNotEqual => "Pick your `!=` below, goro!",
        AmbiguousNotEqualBoth => "Pick your `!=` below, goro!",
        MisplacedModifier(var modifier) => $"`{modifier}` can't go here, goro!",
        MisplacedModifierInCall(var modifier, var function) => $"`{modifier}` can't go inside `{function}()`, goro!",
        ContradictoryQuantifiers(var inner, var outer) => $"`{inner}` inside `{outer}`? Pick one, goro!",
        LiterallyNotString(var modifier, _, _) => $"`{modifier}` only matters for strings, goro!",
        LiterallyOnStateTest(var modifier) => $"A state test compares nothing — drop `{modifier}`, goro!",
        QuantifierOnAbsentTest => "`IS ABSENT` takes the whole value — no quantifier, goro!",
        WrongArgumentCount(var function, var expected, var actual) => expected switch
        {
            1 => $"`{function}()` takes one argument, not {actual}, goro!",
            2 => $"`{function}()` takes two arguments, not {actual}, goro!",
            _ => $"`{function}()` takes {expected} arguments, not {actual}, goro!",
        },
        FallbackDefaultNotLiteral => "The fallback has to be a literal, goro!",
        InvalidNumberLiteral(var literal, _) => $"`{literal}` will never be a number, goro!",
        RangeUnitMissing(var number) => $"`{number}` what? Give both ends a unit, goro!",
        RangeUnitAmbiguous(var number, var unit, var withUnit) => unit switch
        {
            UnitType.ByteCount => $"{number} bytes or {withUnit}? Give both ends a unit, goro!",
            UnitType.Duration => $"{number} seconds or {withUnit}? Give both ends a unit, goro!",
        },
        RangeEndpointTypes => "Both ends of a range need one type, goro!",
        RangeReversed(_, var minimum, var maximum, var order) => order switch
        {
            RangeOrder.Plain => $"`{minimum}` comes after `{maximum}` — swap them, goro!",
            RangeOrder.Literal => $"Literally, `{minimum}` comes after `{maximum}` — swap them, goro!",
            RangeOrder.Normalized => $"Normalized, `{minimum}` comes after `{maximum}` — swap them, goro!",
        },
        InvalidPattern(_, var reason) => $"That pattern won't parse — {reason.Text.TrimEnd('.')}, goro!",
        UnsupportedPatternConstruct(var family) => family switch
        {
            PatternFamily.Lookaround => "Our regex engine doesn't do lookaround, goro!",
            PatternFamily.Backreference => "Our regex engine doesn't do backreferences, goro!",
            PatternFamily.AtomicGroup => "Our regex engine doesn't do atomic groups, goro!",
            PatternFamily.ConditionalOrBalancingGroup => "Our regex engine doesn't do conditionals or balancing groups, goro!",
            PatternFamily.Unknown => "Our regex engine doesn't do that construct, goro!",
        },

        // -----------------------------------------------------------------------------------------
        // Pathspecs

        PathSpecNotFound(var pathSpec) => $"Nothing at `{pathSpec}`, goro!",
        GlobWithSeveralStars(var pathSpec) => $"`{pathSpec}` has more than one `*`, goro!",
        GlobWildcardBeforeLastComponent(var pathSpec) => $"`{pathSpec}`: wildcards go in the last part only, goro!",
        PathSpecNotListable(var pathSpec) => $"Can't list `{pathSpec}`, goro!",

        // -----------------------------------------------------------------------------------------
        // Command line

        InvalidAlgorithm(var algorithm, var valid) => $"No algorithm `{algorithm}` — pick from {List(valid)}, goro!",
        InvalidOutputFormat(var format, var valid) => $"No format `{format}` — pick from {List(valid)}, goro!",
        UnknownWarningCategory(var category, var valid) => $"No warning category `{category}` — pick from {List(valid)}, goro!",

        _ => throw new ArgumentOutOfRangeException(nameof(message), message, "This error has no English wording."),
    };

    // ---------------------------------------------------------------------------------------------
    // Formatting data: nothing here returns words.

    private static string Name(GoroType type) => GoroTypes.Name(type);

    private static string List(ImmutableArray<Code> values) => string.Join(", ", values);
}
