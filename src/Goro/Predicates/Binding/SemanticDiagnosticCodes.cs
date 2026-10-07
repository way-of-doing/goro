namespace Goro.Predicates.Binding;

/// <summary>
/// The codes of the errors static analysis reports: everything the grammar accepts that the
/// specification still rejects. A code names the mistake rather than where it was found.
/// </summary>
public static class SemanticDiagnosticCodes
{
    // Names that resolve to nothing.
    public const string UnknownSource = "unknown-source";
    public const string UnknownIdentifier = "unknown-identifier";
    public const string NeedsSource = "needs-source";
    public const string UnknownFunction = "unknown-function";
    public const string SourceFunctionNeedsArguments = "source-function-needs-arguments";
    public const string NullValue = "null-value";

    // Types.
    public const string TypeMismatch = "type-mismatch";
    public const string NegativeUnitLiteral = "negative-unit-literal";
    public const string FractionalDurationLiteral = "fractional-duration-literal";
    public const string BooleanNotOrdered = "boolean-not-ordered";
    public const string BooleanNotConvertible = "boolean-not-convertible";
    public const string UnitsDoNotConvert = "units-do-not-convert";
    public const string UnknownTarget = "unknown-target";
    public const string ConversionCalled = "conversion-called";
    public const string MatchSubjectNotString = "match-subject-not-string";
    public const string BlobNotUsable = "blob-not-usable";

    // Exactly one.
    public const string NotACondition = "not-a-condition";
    public const string ConditionNotExactlyOne = "condition-not-exactly-one";
    public const string AmbiguousNotEqual = "ambiguous-not-equal";

    // Modifiers.
    public const string MisplacedModifier = "misplaced-modifier";
    public const string ContradictoryQuantifiers = "contradictory-quantifiers";
    public const string LiterallyNotString = "literally-not-string";
    public const string LiterallyOnStateTest = "literally-on-state-test";
    public const string QuantifierOnAbsentTest = "quantifier-on-absent-test";

    // Functions.
    public const string WrongArgumentCount = "wrong-argument-count";
    public const string ArgumentNotLiteral = "argument-not-literal";
    public const string ArgumentRejected = "argument-rejected";
    public const string FallbackDefaultNotConstant = "fallback-default-not-constant";
    public const string ConstantDoesNotConvert = "constant-does-not-convert";

    // Ranges.
    public const string RangeUnitMissing = "range-unit-missing";
    public const string RangeEndpointTypes = "range-endpoint-types";
    public const string RangeReversed = "range-reversed";

    // Patterns.
    public const string InvalidPattern = "invalid-pattern";
    public const string UnsupportedPatternConstruct = "unsupported-pattern-construct";
}
