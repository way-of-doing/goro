namespace Goro.Predicates.Syntax;

public enum ComparisonOperator
{
    Equal,
    NotEqual,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
}

public enum LogicalOperator
{
    And,
    Or,
}

public enum ModifierKind
{
    All,
    Any,
    Literally,
}

/// <summary>The state names a state test asks about.</summary>
public enum TestedState
{
    Absent,
    Usable,
    Unusable,
}
