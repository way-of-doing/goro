using Goro.Output;
using Goro.Predicates.Binding;

namespace Goro.Domain;

/// <param name="Filter">The predicate a file must satisfy to be listed, already read and checked; null lists every file.</param>
public sealed record ListOptions(IReadOnlyList<string> PathSpecs, OutputFormat Output, CompiledPredicate? Filter = null);
