using System.Collections.Immutable;
using Goro.Predicates.Values;

namespace Goro.Messages;

/// <summary>
/// An error Goro can report, as data: which error it is, and what it is about. Turning it into a
/// sentence is the business of an <see cref="IErrorMessages"/>.
/// </summary>
/// <remarks>
/// <para>
/// No message is composed from fragments, since composition assumes one language's grammar. So a
/// property carries data, never words: the user's text or a name Goro spells (<see cref="Code"/>), a
/// type (<see cref="GoroType"/>, rendered as its Goro name), a number, a list of valid values, the
/// regex engine's reason (<see cref="ForeignText"/>), or an enum of the situations that call for
/// sentences of their own. Never a <see cref="string"/>; a test holds every case to this.
/// </para>
/// <para>
/// A case carries the data the engine has where it finds the error, whether or not the English
/// wording uses it, so that a message can be reworded without touching the engine.
/// </para>
/// </remarks>
public abstract record ErrorMessage
{
    private ErrorMessage()
    {
    }

    // ---------------------------------------------------------------------------------------------
    // Lexical

    public sealed record NonAsciiName(Code Word) : ErrorMessage;
    public sealed record UnknownEscape(Code Escape) : ErrorMessage;
    public sealed record HexEscapeDigits : ErrorMessage;
    public sealed record UnicodeEscapeDigits : ErrorMessage;
    public sealed record BracedEscapeDigits : ErrorMessage;
    public sealed record CodePointOutOfRange(Code Escape) : ErrorMessage;
    public sealed record SurrogateCodePoint(Code Escape) : ErrorMessage;
    public sealed record UnterminatedString : ErrorMessage;
    public sealed record LoneSurrogate(Code Escape) : ErrorMessage;
    public sealed record TrailingPeriod(Code Literal, Code WithoutPeriod) : ErrorMessage;
    public sealed record ClockFieldOutOfRange(Code Field) : ErrorMessage;
    public sealed record UnrepresentableLiteral(Code Literal) : ErrorMessage;
    public sealed record SignedClockDuration(Code Literal) : ErrorMessage;
    public sealed record MalformedClockDuration(Code Literal) : ErrorMessage;
    public sealed record UnexpectedCharacter(Code Character) : ErrorMessage;

    // ---------------------------------------------------------------------------------------------
    // Syntax

    public sealed record EmptyPredicate : ErrorMessage;
    public sealed record UnexpectedEnd(Expectation Expected) : ErrorMessage;
    public sealed record UnexpectedToken(Expectation Expected, Code Found) : ErrorMessage;
    public sealed record ChainedComparison : ErrorMessage;
    public sealed record PatternNotRawString(Code Operator) : ErrorMessage;
    public sealed record ModifiedPattern : ErrorMessage;
    public sealed record ParenthesizedPattern : ErrorMessage;
    public sealed record QuotedPattern : ErrorMessage;
    public sealed record IsNot : ErrorMessage;
    public sealed record IsNull : ErrorMessage;
    public sealed record RangeEndpointNotLiteral : ErrorMessage;
    public sealed record ModifierAsWord(Code Modifier) : ErrorMessage;
    public sealed record ReservedWord(Code Word) : ErrorMessage;
    public sealed record ReservedWordInIdentifier(Code Word) : ErrorMessage;
    public sealed record WhitespaceInIdentifier : ErrorMessage;
    public sealed record QualifiedFunctionCall(Code Identifier) : ErrorMessage;
    public sealed record BorrowedEqual : ErrorMessage;
    public sealed record BorrowedLessGreater : ErrorMessage;
    public sealed record BorrowedAnd : ErrorMessage;
    public sealed record BorrowedOr : ErrorMessage;
    public sealed record BorrowedBang : ErrorMessage;
    public sealed record BorrowedTildeEqual : ErrorMessage;
    public sealed record BorrowedBangTilde : ErrorMessage;
    public sealed record SignedUnitLiteral(Code Literal) : ErrorMessage;
    public sealed record FractionalDurationFields(Code Literal) : ErrorMessage;
    public sealed record RunOnLiteral(Code Run, Code Literal, NumericLiteral Kind, Code Next) : ErrorMessage;
    public sealed record WhitespaceInLiteral(Code Text) : ErrorMessage;

    // ---------------------------------------------------------------------------------------------
    // Semantic

    public sealed record UnknownNamespace(Code Namespace) : ErrorMessage;
    public sealed record UnknownIdentifier(Code Name) : ErrorMessage;
    public sealed record UnknownIdentifierInNamespace(Code Namespace, Code Name) : ErrorMessage;
    public sealed record UnknownFunction(Code Name) : ErrorMessage;
    public sealed record NullValue : ErrorMessage;
    public sealed record NullComparison : ErrorMessage;
    public sealed record TypeMismatch(Code Left, GoroType LeftType, Code Right, GoroType RightType, Code Operator) : ErrorMessage;
    public sealed record TypeMismatchNumberVariable(Code Left, GoroType LeftType, Code Right, GoroType RightType, Code Operator) : ErrorMessage;
    public sealed record FallbackTypeMismatch(Code Function, Code Argument, GoroType ArgumentType, Code Default, GoroType DefaultType) : ErrorMessage;
    public sealed record RangeTypeMismatch(Code Subject, GoroType SubjectType, Code Range, GoroType RangeType) : ErrorMessage;
    public sealed record NegativeUnitLiteral(Code Literal, UnitType Unit) : ErrorMessage;
    public sealed record FractionalDurationLiteral(Code Literal) : ErrorMessage;
    public sealed record BooleanCompared(Code Operator) : ErrorMessage;
    public sealed record BooleanBetween(Code Subject) : ErrorMessage;
    public sealed record BooleanRange : ErrorMessage;
    public sealed record BooleanNotConvertible(Code Function, Code Argument) : ErrorMessage;
    public sealed record MatchSubjectNotString(Code Subject, GoroType Type) : ErrorMessage;
    public sealed record NotACondition(Code Expression, GoroType Type) : ErrorMessage;
    public sealed record IndefiniteCondition(Code Expression) : ErrorMessage;
    public sealed record AmbiguousNotEqual(Code Operand) : ErrorMessage;
    public sealed record AmbiguousNotEqualBoth(Code Left, Code Right) : ErrorMessage;
    public sealed record MisplacedModifier(Code Modifier) : ErrorMessage;
    public sealed record MisplacedModifierInCall(Code Modifier, Code Function) : ErrorMessage;
    public sealed record ContradictoryQuantifiers(Code Inner, Code Outer) : ErrorMessage;
    public sealed record LiterallyNotString(Code Modifier, Code Operand, GoroType Type) : ErrorMessage;
    public sealed record LiterallyOnStateTest(Code Modifier) : ErrorMessage;
    public sealed record QuantifierOnAbsentTest : ErrorMessage;
    public sealed record WrongArgumentCount(Code Function, int Expected, int Actual) : ErrorMessage;
    public sealed record FallbackDefaultNotLiteral(Code Function, Code Default) : ErrorMessage;
    public sealed record InvalidNumberLiteral(Code Literal, Code Function) : ErrorMessage;
    public sealed record RangeUnitMissing(Code Number) : ErrorMessage;
    public sealed record RangeUnitAmbiguous(Code Number, UnitType Unit, Code WithUnit) : ErrorMessage;
    public sealed record RangeEndpointTypes(Code Minimum, GoroType MinimumType, Code Maximum, GoroType MaximumType) : ErrorMessage;
    public sealed record RangeReversed(Code Range, Code Minimum, Code Maximum, RangeOrder Order) : ErrorMessage;
    public sealed record InvalidPattern(Code Pattern, ForeignText Reason) : ErrorMessage;
    public sealed record UnsupportedPatternConstruct(PatternFamily Family) : ErrorMessage;

    // ---------------------------------------------------------------------------------------------
    // Pathspecs

    public sealed record PathSpecNotFound(Code PathSpec) : ErrorMessage;
    public sealed record GlobWithSeveralStars(Code PathSpec) : ErrorMessage;
    public sealed record GlobWildcardBeforeLastComponent(Code PathSpec) : ErrorMessage;
    public sealed record PathSpecNotListable(Code PathSpec) : ErrorMessage;

    // ---------------------------------------------------------------------------------------------
    // Command line

    public sealed record InvalidAlgorithm(Code Algorithm, ImmutableArray<Code> Valid) : ErrorMessage;
    public sealed record InvalidOutputFormat(Code Format, ImmutableArray<Code> Valid) : ErrorMessage;
    public sealed record UnknownWarningCategory(Code Category, ImmutableArray<Code> Valid) : ErrorMessage;
    public sealed record MissingWarningCategory(ImmutableArray<Code> Valid) : ErrorMessage;
}
