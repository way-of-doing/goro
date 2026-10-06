namespace Goro.Predicates.Values;

/// <summary>
/// How an operator reads the occurrences of one operand: every value carries this bit, existential
/// unless <c>ALL()</c> makes it universal. The binder folds the modifier into the operand that reads
/// it, and the operator iterates accordingly.
/// </summary>
public enum Quantifier
{
    Existential,
    Universal,
}
