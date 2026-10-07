namespace Goro.Messages;

/// <summary>
/// Text that goes into a message as it is: the user's own, verbatim, or a name Goro spells, such as
/// a keyword, a function, a source or a unit. Never words of the message itself.
/// </summary>
public readonly record struct Code(string Text)
{
    public override string ToString() => Text;
}

/// <summary>
/// Text that another provider wrote, carried into a message as it is: the regex engine's reason why
/// a pattern does not parse. It is in whatever language that provider speaks.
/// </summary>
public readonly record struct ForeignText(string Text)
{
    public override string ToString() => Text;
}

/// <summary>What the parser was looking for where it found something else.</summary>
public enum Expectation
{
    OperatorOrEnd,
    RangeSeparator,
    State,
    Literal,
    Operand,
    CloseParen,
    CommaOrCloseParen,
    Name,
    Target,
}

/// <summary>The families of regex construct the non-backtracking engine does not support.</summary>
public enum PatternFamily
{
    Lookaround,
    Backreference,
    AtomicGroup,
    ConditionalOrBalancingGroup,

    /// <summary>A construct the engine rejected that falls in none of the families above.</summary>
    Unknown,
}

/// <summary>The kinds of literal that are written as digits.</summary>
public enum NumericLiteral
{
    Number,
    ByteCount,
    Duration,
}

/// <summary>The types whose literals carry a unit.</summary>
public enum UnitType
{
    ByteCount,
    Duration,
}

/// <summary>How the two ends of a range were found to be the wrong way round.</summary>
public enum RangeOrder
{
    /// <summary>Numbers, bytecounts and durations, which have one order.</summary>
    Plain,

    /// <summary>Strings compared in literal mode.</summary>
    Literal,

    /// <summary>Strings compared in normalized mode.</summary>
    Normalized,
}
