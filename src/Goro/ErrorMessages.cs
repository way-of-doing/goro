using Goro.Predicates.Values;

namespace Goro;

/// <summary>
/// The wording of every error Goro reports, in one place, so that it can be read and rewritten
/// together. Each member is one message, whole; where the wording varies by case, each variant is
/// a member of its own, and the code that finds the error chooses which. Only the fragments under
/// Words are substituted into other messages.
/// </summary>
/// <remarks>
/// A placeholder takes the user's text as written, or a name Goro spells, never punctuation:
/// backticks and the rest belong to the message. Warnings are worded where they are raised.
/// </remarks>
public static class ErrorMessages
{
    // ---------------------------------------------------------------------------------------------
    // Lexical

    public static string NonAsciiName(string word) => $"`{word}` is not a name: names are made of ASCII letters, digits and underscores. A part after `::` may be quoted to hold any other characters.";
    public static string UnknownEscape(string escape) => $"`{escape}` is not an escape. To write a backslash, double it: `\\\\`.";
    public static string HexEscapeDigits() => "`\\x` takes exactly two hexadecimal digits.";
    public static string UnicodeEscapeDigits() => "`\\u` takes exactly four hexadecimal digits, or one to six in braces.";
    public static string BracedEscapeDigits() => "`\\u{...}` takes one to six hexadecimal digits and a closing brace.";
    public static string CodePointOutOfRange(string escape) => $"`{escape}` is beyond U+10FFFF, the last code point.";
    public static string SurrogateCodePoint(string escape) => $"`{escape}` names a surrogate, which is not a character. Write the character itself in braces, or both halves of the pair with `\\uNNNN`.";
    public static string UnterminatedString() => "This string has no closing quote.";
    public static string LoneSurrogate(string escape) => $"`{escape}` is half of a surrogate pair, and the other half does not stand next to it.";
    public static string TrailingPeriod(string literal, string withoutPeriod) => $"`{literal}` ends in a period, which must be followed by a digit. Write `{withoutPeriod}`.";
    public static string ClockFieldOutOfRange(string field) => $"`{field}` is out of range: every field after the first runs from 00 to 59.";
    public static string UnrepresentableLiteral(string literal) => $"`{literal}` cannot be held exactly: it is too large or has too many significant digits.";
    public static string SignedClockDuration(string literal) => $"`{literal}` has a sign, but a duration cannot be negative.";
    public static string MalformedClockDuration(string literal) => $"`{literal}` is not a duration: a clock-form duration has two digits after each colon, and at most two colons.";
    public static string UnexpectedCharacter(string character) => $"`{character}` cannot appear here, outside a string.";

    // ---------------------------------------------------------------------------------------------
    // Syntax

    public static string EmptyPredicate() => "The predicate is empty.";
    public static string UnexpectedEnd(string expected) => $"The predicate ends where {expected} should follow.";
    public static string UnexpectedToken(string expected, string found) => $"Expected {expected}, but found `{found}`.";
    public static string ChainedComparison() => "Comparisons cannot be chained. Join two of them with `AND`, or parenthesize one to compare its outcome.";
    public static string PatternNotRawString(string op) => $"The pattern of `{op}` must be a raw string literal, such as `r\"^the \"`.";
    public static string ModifiedPattern() => "The pattern is part of the match operator and takes no modifier; a modifier goes on the subject.";
    public static string ParenthesizedPattern() => "The pattern is part of the match operator, and is written without parentheses.";
    public static string QuotedPattern() => "A pattern is written as a raw string, so that its backslashes reach the regex engine as written.";
    public static string IsNot() => "There is no `IS NOT`: negate the state test with `NOT` instead.";
    public static string IsNull() => "Goro has no null: a value the file does not record is `ABSENT`.";
    public static string RangeEndpointNotLiteral() => "The ends of a range are literals, written as they are, without parentheses.";
    public static string ModifierAsWord(string modifier) => $"`{modifier}` is a modifier, and is written around the operand it applies to: `{modifier}(...)`.";
    public static string ReservedWord(string word) => $"`{word}` is a reserved word, so it cannot be used as an identifier.";
    public static string ReservedWordInIdentifier(string word) => $"`{word}` is a reserved word, so it can begin an identifier only after a leading `::`.";
    public static string WhitespaceInIdentifier() => "An identifier is written without whitespace around its `::`.";
    public static string QualifiedFunctionCall(string identifier) => $"`{identifier}` is qualified, and functions do not live in namespaces.";
    public static string BorrowedEqual() => "Equality is written `==`.";
    public static string BorrowedLessGreater() => "Inequality is written `!=`.";
    public static string BorrowedAnd() => "`&&` is written `AND` in a predicate.";
    public static string BorrowedOr() => "`||` is written `OR` in a predicate.";
    public static string BorrowedBang() => "Negation is written `NOT`.";
    public static string BorrowedTildeEqual() => "`~=` is not an operator: a regex match is written `=~`, and inequality `!=`.";
    public static string BorrowedBangTilde() => "There is no negated match operator: negate the match with `NOT` instead.";
    public static string SignedUnitLiteral(string literal) => $"`{literal}` has a sign, but bytecounts and durations cannot be negative.";
    public static string FractionalDurationFields(string literal) => $"`{literal}` is not a duration: its fields are whole numbers, as in `1h30m`.";
    public static string RunOnLiteral(string run, string literal, GoroType type, string next) => $"`{run}` is not a literal: `{literal}` reads as {A(type)}, which runs straight into `{next}`.";
    public static string WhitespaceInLiteral(string text) => $"A literal cannot contain whitespace, so `{text}` is not one.";

    // ---------------------------------------------------------------------------------------------
    // Semantic

    public static string UnknownNamespace(string @namespace) => $"Goro has no namespace `{@namespace}`.";
    public static string UnknownIdentifier(string name) => $"Goro has no identifier `{name}`.";
    public static string UnknownIdentifierInNamespace(string @namespace, string name) => $"The namespace `{@namespace}` has no identifier `{name}`.";
    public static string UnknownFunction(string name) => $"Goro has no function `{name}`; its functions are `COUNT`, `FALLBACK`, `NUMBER` and `STRING`.";
    public static string NullValue() => "Goro has no null value: a value the file does not record is absent.";
    public static string NullComparison() => "Goro has no null value: a value the file does not record is absent, and `IS ABSENT` tests for that.";
    public static string TypeMismatch(string left, GoroType? leftType, string right, GoroType? rightType, string op) => $"`{left}` is {A(leftType)} and `{right}` is {A(rightType)}, so `{op}` cannot compare them.";
    public static string TypeMismatchNumberVariable(string left, GoroType? leftType, string right, GoroType? rightType, string op) => $"`{left}` is {A(leftType)} and `{right}` is {A(rightType)}, so `{op}` cannot compare them. Only a number literal stands for a bytecount or a duration.";
    public static string FallbackTypeMismatch(string function, string argument, GoroType? argumentType, string @default, GoroType? defaultType) => $"The default of `{function}()` must have the type of `{argument}`, which is {A(argumentType)}, and `{@default}` is {A(defaultType)}.";
    public static string RangeTypeMismatch(string subject, GoroType subjectType, string range, GoroType rangeType) => $"`{subject}` is {A(subjectType)}, and the range `{range}` holds {Plural(rangeType)}.";
    public static string NegativeUnitLiteral(string literal, GoroType unit) => $"`{literal}` stands for {A(unit)} here, and {A(unit)} cannot be negative.";
    public static string FractionalDurationLiteral(string literal) => $"`{literal}` stands for a duration here, and a duration is a whole number of seconds.";
    public static string BooleanCompared(string op) => $"Booleans are unordered, so `{op}` cannot compare them; only `==` and `!=` can.";
    public static string BooleanBetween(string subject) => $"Booleans are unordered, so `{subject}` cannot be the subject of `BETWEEN`.";
    public static string BooleanRange() => "Booleans are unordered, so a range cannot run between two of them.";
    public static string BooleanNotConvertible(string function, string argument) => $"`{function}()` does not accept a boolean, and `{argument}` is one.";
    public static string MatchSubjectNotString(string subject, GoroType type) => $"The subject of `=~` must be a string, and `{subject}` is {A(type)}.";
    public static string NotACondition(string role, string expression, GoroType type) => $"{Capitalized(role)} must be a condition, and `{expression}` is {A(type)}.";
    public static string IndefiniteCondition(string expression, string role) => $"`{expression}` may be absent or hold several values, so it cannot be {role} as it stands: compare it with `TRUE` or `FALSE` to say which reading is meant.";
    public static string AmbiguousNotEqual(string operand) => $"`{operand}` may be absent or hold several values, so `!=` must be told which reading is meant.";
    public static string AmbiguousNotEqualBoth(string left, string right) => $"`{left}` and `{right}` may each be absent or hold several values, so `!=` must be told which reading is meant.";
    public static string MisplacedModifier(string modifier, string place) => $"`{modifier}` cannot be applied to {place}: a modifier belongs on an operand of a comparison, `BETWEEN`, `=~` or `IS`.";
    public static string ContradictoryQuantifiers(string inner, string outer) => $"`{inner}` contradicts the `{outer}` around it: an operand is quantified one way or the other.";
    public static string LiterallyNotString(string modifier, string operand, GoroType type) => $"`{modifier}` chooses how strings are compared, and `{operand}` is {A(type)}.";
    public static string LiterallyOnStateTest(string modifier) => $"A state test compares nothing, so `{modifier}` has nothing to change in it.";
    public static string QuantifierOnAbsentTest() => "`IS ABSENT` asks about the whole value, so its operand takes no quantifier.";
    public static string WrongArgumentCount(string function, int expected, int actual) => $"`{function}()` takes {Arguments(expected)}, not {ArgumentCount(actual)}.";
    public static string FallbackDefaultNotLiteral(string function, string @default) => $"The default of `{function}()` must be a literal, such as `0` or `\"unknown\"`, and `{@default}` is not one.";
    public static string InvalidNumberLiteral(string literal, string function) => $"`{literal}` is not a number literal, so `{function}()` could never convert it.";
    public static string RangeUnitMissing(string number) => $"It is unclear what unit `{number}` is in: give both ends of the range a unit.";
    public static string RangeUnitAmbiguous(string number, GoroType unit, string withUnit) => $"It is unclear whether `{number}` means {number} {UnitsOf(unit)} or {withUnit}: give both ends of the range a unit.";
    public static string RangeEndpointTypes(string minimum, GoroType minimumType, string maximum, GoroType maximumType) => $"The ends of a range must be of one type, and `{minimum}` is {A(minimumType)} while `{maximum}` is {A(maximumType)}.";
    public static string RangeReversed(string range, string minimum, string maximum) => $"The range `{range}` holds nothing: `{minimum}` sorts after `{maximum}`.";
    public static string RangeReversedLiteral(string range, string minimum, string maximum) => $"The range `{range}` holds nothing: `{minimum}` sorts after `{maximum}` in a literal comparison.";
    public static string RangeReversedNormalized(string range, string minimum, string maximum) => $"The range `{range}` holds nothing: `{minimum}` sorts after `{maximum}` in a normalized comparison.";
    public static string InvalidPattern(string pattern, string reason) => $"`{pattern}` is not a valid regular expression: {reason}";
    public static string UnsupportedPatternConstruct(string family) => $"The pattern uses {family}, which Goro's non-backtracking regex engine does not support.";

    // ---------------------------------------------------------------------------------------------
    // Pathspecs

    public static string PathSpecNotFound(string pathSpec) => $"Pathspec not found: {pathSpec}";
    public static string GlobWithSeveralStars(string pathSpec) => $"A glob may contain at most one '*': {pathSpec}";
    public static string GlobWildcardBeforeLastComponent(string pathSpec) => $"Wildcards may appear only in the last component of a glob: {pathSpec}";
    public static string PathSpecNotListable(string pathSpec) => $"Pathspec cannot be listed: {pathSpec}";

    // ---------------------------------------------------------------------------------------------
    // Command line

    public static string InvalidAlgorithm(string? algorithm, IEnumerable<string> valid) => $"Invalid algorithm '{algorithm}'. Valid values: {string.Join(", ", valid)}.";
    public static string InvalidOutputFormat(string? format, IEnumerable<string> valid) => $"Invalid output format '{format}'. Valid values: {string.Join(", ", valid)}.";
    public static string UnknownWarningCategory(string? category, IEnumerable<string> valid) => $"Unknown warning category '{category}'. Valid values: {string.Join(", ", valid)}.";

    // ---------------------------------------------------------------------------------------------
    // Words

    // How an error is presented: the start of its first line, and the start of each suggestion's.
    public static string ErrorPrefix() => "goro: error: ";
    public static string SuggestionLabel() => "try: ";

    // Types.
    public static string A(GoroType? type) => type switch
    {
        GoroType.String => "a string",
        GoroType.Number => "a number",
        GoroType.ByteCount => "a bytecount",
        GoroType.Duration => "a duration",
        GoroType.Boolean => "a boolean",
        _ => "of no known type",
    };

    public static string Plural(GoroType type) => type switch
    {
        GoroType.String => "strings",
        GoroType.Number => "numbers",
        GoroType.ByteCount => "bytecounts",
        GoroType.Duration => "durations",
        _ => "booleans",
    };

    // What a plain number at one end of a bytecount or duration range might be counting.
    public static string UnitsOf(GoroType type) => type == GoroType.ByteCount ? "bytes" : "seconds";

    // How many arguments a function takes, and how many it was given.
    public static string Arguments(int count) => count == 1 ? "one argument" : "two arguments";
    public static string ArgumentCount(int count) => count == 0 ? "none" : count.ToString();

    // Roles: where a condition is required. Places: where a modifier was written and cannot stand.
    public static string ThePredicate() => "the predicate";
    public static string OperandOfNot() => "the operand of `NOT`";
    public static string OperandOfAnd() => "an operand of `AND`";
    public static string OperandOfOr() => "an operand of `OR`";
    public static string ArgumentOf(string function) => $"an argument of `{function}()`";
    public static string ThisExpression() => "this expression";

    // What the parser expected where it found something else.
    public static string ExpectedOperatorOrEnd() => "an operator, or the end of the predicate";
    public static string ExpectedRangeSeparator() => "`..` between the two ends of the range";
    public static string ExpectedState() => "`USABLE`, `UNUSABLE` or `ABSENT`";
    public static string ExpectedLiteral() => "a literal";
    public static string ExpectedOperand() => "an operand";
    public static string ExpectedCloseParen() => "`)`";
    public static string ExpectedCommaOrCloseParen() => "`,` or `)`";
    public static string ExpectedName() => "a name after `::`";
    public static string ExpectedNameOrQuotedName() => "a name, or a quoted name, after `::`";

    // The families of regex construct the non-backtracking engine does not support.
    public static string Lookaround() => "lookaround";
    public static string Backreference() => "a backreference";
    public static string AtomicGroup() => "an atomic group";
    public static string ConditionalOrBalancingGroup() => "a conditional or a balancing group";
    public static string UnknownConstruct() => "a construct";

    // ---------------------------------------------------------------------------------------------

    private static string Capitalized(string text) => char.ToUpperInvariant(text[0]) + text[1..];
}
