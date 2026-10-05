namespace Goro.Predicates.Values;

/// <summary>One occurrence in a bag: usable, with a datum, or unusable.</summary>
public abstract record Occurrence<T> where T : notnull;

public sealed record Usable<T>(T Datum) : Occurrence<T> where T : notnull;

/// <summary>
/// An occurrence whose data could not be interpreted. <paramref name="Origin"/> is where it arose;
/// the unusable result of an operator has none, since whatever made it unusable was reported when
/// the operator consumed it.
/// </summary>
public sealed record Unusable<T>(Origin? Origin) : Occurrence<T> where T : notnull;
