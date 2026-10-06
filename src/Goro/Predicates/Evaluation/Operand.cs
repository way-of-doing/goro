using Goro.Predicates.Values;

namespace Goro.Predicates.Evaluation;

/// <summary>
/// An operand of an operator, with the quantifier its modifiers chose. The modifiers themselves are
/// gone by this point: a quantifier belongs to the operand, and a comparison mode to the operator.
/// </summary>
public sealed record Operand<T>(Expression<T> Expression, Quantifier Quantifier) where T : notnull;
