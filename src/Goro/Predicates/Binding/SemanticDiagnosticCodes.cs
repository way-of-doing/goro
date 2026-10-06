namespace Goro.Predicates.Binding;

/// <summary>
/// The codes of the errors static analysis reports: everything the grammar accepts that the
/// specification still rejects. A code names the mistake rather than where it was found.
/// </summary>
public static class SemanticDiagnosticCodes
{
    // Names that resolve to nothing.
    public const string UnknownNamespace = "unknown-namespace";
    public const string UnknownIdentifier = "unknown-identifier";
    public const string UnknownFunction = "unknown-function";
    public const string NullValue = "null-value";

    // Types.
    public const string TypeMismatch = "type-mismatch";
    public const string NegativeUnitLiteral = "negative-unit-literal";
    public const string FractionalDurationLiteral = "fractional-duration-literal";
    public const string BooleanNotOrdered = "boolean-not-ordered";
    public const string BooleanNotConvertible = "boolean-not-convertible";
    public const string MatchSubjectNotString = "match-subject-not-string";

    // Definiteness.
    public const string NotACondition = "not-a-condition";
    public const string IndefiniteCondition = "indefinite-condition";
    public const string AmbiguousNotEqual = "ambiguous-not-equal";

    // Modifiers.
    public const string MisplacedModifier = "misplaced-modifier";
    public const string ContradictoryQuantifiers = "contradictory-quantifiers";
    public const string LiterallyNotString = "literally-not-string";
    public const string LiterallyOnStateTest = "literally-on-state-test";
    public const string QuantifierOnAbsentTest = "quantifier-on-absent-test";

    // Functions.
    public const string WrongArgumentCount = "wrong-argument-count";
    public const string FallbackDefaultNotLiteral = "fallback-default-not-literal";
    public const string InvalidNumberLiteral = "invalid-number-literal";

    // Ranges.
    public const string RangeUnitMissing = "range-unit-missing";
    public const string RangeEndpointTypes = "range-endpoint-types";
    public const string RangeReversed = "range-reversed";

    // Patterns.
    public const string InvalidPattern = "invalid-pattern";
    public const string UnsupportedPatternConstruct = "unsupported-pattern-construct";
}
