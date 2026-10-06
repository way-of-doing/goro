namespace Goro.Predicates.Values;

/// <summary>
/// The outcome of a boolean that is exactly one: what a predicate evaluates to for one file, and what the
/// logical operators work with. <see cref="Unusable"/> is a missing answer, not a third truth.
/// </summary>
public enum Truth
{
    False,
    True,
    Unusable,
}

public static class Truths
{
    private static readonly Value<bool> FalseValue = Value<bool>.Single(new Usable<bool>(false));
    private static readonly Value<bool> TrueValue = Value<bool>.Single(new Usable<bool>(true));
    private static readonly Value<bool> UnusableValue = Value<bool>.Single(new Unusable<bool>(null));

    public static Truth Of(bool value) => value ? Truth.True : Truth.False;

    /// <summary>A boolean of exactly one occurrence, as every condition is.</summary>
    public static Value<bool> ToValue(this Truth truth) => truth switch
    {
        Truth.False => FalseValue,
        Truth.True => TrueValue,
        _ => UnusableValue,
    };

    public static Truth ToTruth(this Occurrence<bool> occurrence) => occurrence switch
    {
        Usable<bool>(var datum) => Of(datum),
        _ => Truth.Unusable,
    };

    /// <summary>The truth of a boolean that is exactly one.</summary>
    /// <exception cref="InvalidOperationException">The value does not hold exactly one occurrence.</exception>
    public static Truth ToTruth(this Value<bool> value) => value.Occurrences switch
    {
        [var single] => single.ToTruth(),
        _ => throw new InvalidOperationException($"A condition holds exactly one occurrence, not {value}."),
    };
}
