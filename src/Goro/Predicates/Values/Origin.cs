namespace Goro.Predicates.Values;

/// <summary>
/// The interned identity of a warning source: two sub-expressions that apply the same functions in
/// the same shape to the same identifiers and literals share one, however they were written.
/// </summary>
public readonly record struct SourceId(int Value);

/// <summary>
/// Where an unusable occurrence was born: the source it is deduplicated by, the text of the
/// sub-expression that produced it, exactly as written, which is what a warning quotes, and where in
/// the predicate that text starts, which decides which of several spellings of one source is quoted.
/// All three are known when the predicate is read, so one instance per node serves every file.
/// </summary>
public sealed record Origin(SourceId Source, string Text, int Start);
