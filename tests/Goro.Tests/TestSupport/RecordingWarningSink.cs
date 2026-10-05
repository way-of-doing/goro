using System.Collections.Concurrent;
using Goro.Warnings;

namespace Goro.Tests.TestSupport;

/// <summary>A warning sink that keeps every batch it is given, suppressing nothing.</summary>
internal sealed class RecordingWarningSink : IWarningSink
{
    private readonly ConcurrentQueue<IReadOnlyList<Warning>> _batches = new();

    public IReadOnlyList<IReadOnlyList<Warning>> Batches => [.. _batches];

    public IReadOnlyList<Warning> Warnings => [.. _batches.SelectMany(b => b)];

    public void Emit(IReadOnlyList<Warning> batch) => _batches.Enqueue([.. batch]);

    public IReadOnlySet<WarningCategory> Produced => Warnings.Select(w => w.Category).ToHashSet();
}
