using Goro.Reading.Bytes;

namespace Goro.Tests.Reading;

public class BoundedReaderTests
{
    private static readonly ReadPolicy Small = new(HeadWindow: 100, TailWindow: 100, SniffBudget: 10, SearchBudget: 50, StructureBudget: 30, PayloadLimit: 40);

    private static byte[] Counting(int length) => [.. Enumerable.Range(0, length).Select(i => (byte)i)];

    [Test]
    public void AFileTheWindowsWouldCover_IsReadOnce_AndEveryReadIsServedFromIt()
    {
        var reader = new BoundedReader(new MemoryByteSource(Counting(150)), Small);

        Assert.That(reader.TryRead(ReadPurpose.Payload, 120, 30, out var tail), Is.True);
        Assert.That(reader.TryRead(ReadPurpose.Sniff, 0, 10, out var head), Is.True);

        Assert.That(tail.ToArray(), Is.EqualTo(Counting(150)[120..]));
        Assert.That(head.ToArray(), Is.EqualTo(Counting(10)));
        Assert.That(reader.Log.BytesFromSource, Is.EqualTo(150));
        Assert.That(reader.Log.Records.Count(r => r.Outcome == ReadOutcome.FromSource), Is.EqualTo(1));
    }

    [Test]
    public void ALargerFile_HasItsHeadAndTailReadOnceEach_WhateverIsAskedOfThem()
    {
        var reader = new BoundedReader(new MemoryByteSource(Counting(1000)), Small);

        for (var i = 0; i < 5; i++)
        {
            reader.TryRead(ReadPurpose.Sniff, i * 10, 10, out _);
            reader.TryRead(ReadPurpose.Sniff, 990 - i * 10, 10, out _);
        }

        Assert.That(reader.Log.BytesFromSource, Is.EqualTo(200));
    }

    [Test]
    public void AReadInsideAWindow_IsServedWhateverItsSize_AndCostsNothing()
    {
        var reader = new BoundedReader(new MemoryByteSource(Counting(1000)), Small);

        Assert.That(reader.TryRead(ReadPurpose.Sniff, 0, 100, out _), Is.True, "the whole head, ten times the sniff budget");
        Assert.That(reader.TryRead(ReadPurpose.Sniff, 500, 10, out _), Is.True, "the budget is untouched");
    }

    // What the budgets are for: a file cannot be walked from end to end by reads that are each small.
    [TestCase(ReadPurpose.Sniff, 10)]
    [TestCase(ReadPurpose.Search, 50)]
    [TestCase(ReadPurpose.DeclaredStructure, 30)]
    public void SmallReadsBetweenTheWindows_AreRefusedOnceTheyAddUpToTheBudget(ReadPurpose purpose, int budget)
    {
        var reader = new BoundedReader(new MemoryByteSource(Counting(10_000)), Small);

        var served = 0;
        for (var offset = 100; offset < 9_900; offset += 2)
        {
            if (reader.TryRead(purpose, offset, 2, out _))
            {
                served += 2;
            }
        }

        Assert.That(served, Is.EqualTo(budget));
        Assert.That(reader.Log.BytesFromSource, Is.EqualTo(budget), "windows untouched, and nothing beyond the budget");
        Assert.That(reader.Log.WentOverLimit(purpose), Is.True);
    }

    [Test]
    public void EachPurpose_HasABudgetOfItsOwn()
    {
        var reader = new BoundedReader(new MemoryByteSource(Counting(10_000)), Small);

        Assert.That(reader.TryRead(ReadPurpose.Sniff, 500, 10, out _), Is.True);
        Assert.That(reader.TryRead(ReadPurpose.Sniff, 600, 1, out _), Is.False);
        Assert.That(reader.TryRead(ReadPurpose.DeclaredStructure, 700, 30, out _), Is.True);
        Assert.That(reader.TryRead(ReadPurpose.Search, 800, 50, out _), Is.True);
    }

    // A budget shared between values would make whether one can be read depend on which were read first.
    [Test]
    public void APayload_IsHeldToALimitForEachValue_NotABudget()
    {
        var reader = new BoundedReader(new MemoryByteSource(Counting(10_000)), Small);

        for (var i = 0; i < 20; i++)
        {
            Assert.That(reader.TryRead(ReadPurpose.Payload, 200 + i * 40, 40, out _), Is.True, $"value {i}");
        }

        Assert.That(reader.TryRead(ReadPurpose.Payload, 5000, 41, out _), Is.False, "over the limit for one value");
    }

    [Test]
    public void CanRead_Answers_WithoutChargingAnything()
    {
        var reader = new BoundedReader(new MemoryByteSource(Counting(10_000)), Small);

        Assert.That(reader.CanRead(ReadPurpose.DeclaredStructure, 500, 30), Is.True);
        Assert.That(reader.CanRead(ReadPurpose.DeclaredStructure, 500, 31), Is.False);
        Assert.That(reader.TryRead(ReadPurpose.DeclaredStructure, 500, 30, out _), Is.True, "asking cost nothing");
        Assert.That(reader.Log.Records, Has.Count.EqualTo(1));
    }

    [TestCase(-1, 10)]
    [TestCase(995, 10)]
    [TestCase(1000, 1)]
    [TestCase(0, -1)]
    [TestCase(1, long.MaxValue)]
    public void AReadOutsideTheFile_IsRefused_AndLogged(long offset, long count)
    {
        var reader = new BoundedReader(new MemoryByteSource(Counting(1000)), Small);

        Assert.That(reader.TryRead(ReadPurpose.Sniff, offset, count, out _), Is.False);
        Assert.That(reader.Log.Records.Single().Outcome, Is.EqualTo(ReadOutcome.OutsideFile));
    }

    [Test]
    public void AFileThatShrinksWhileBeingRead_ThrowsAnIOException()
    {
        var reader = new BoundedReader(new LyingSource(claimed: 1000, actual: 400), Small);

        Assert.Throws<IOException>(() => reader.TryRead(ReadPurpose.Sniff, 990, 10, out _));
    }

    private sealed class LyingSource(long claimed, int actual) : IByteSource
    {
        public long Length => claimed;

        public int Read(long offset, Span<byte> into) => offset >= actual ? 0 : (int)Math.Min(into.Length, actual - offset);
    }
}
