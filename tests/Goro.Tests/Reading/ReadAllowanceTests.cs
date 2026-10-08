using Goro.Reading.Bytes;

namespace Goro.Tests.Reading;

public class ReadAllowanceTests
{
    private static readonly ReadPolicy Policy = new(SniffBudget: 10, SearchBudget: 50, StructureBudget: 30, PayloadLimit: 40);

    [Test]
    public void ABudget_IsSpentByWhatItIsChargedForTogether()
    {
        var allowance = Policy.CreateAllowance();

        Assert.That(allowance.TryCharge(ReadPurpose.Search, 30), Is.True);
        Assert.That(allowance.TryCharge(ReadPurpose.Search, 20), Is.True);
        Assert.That(allowance.TryCharge(ReadPurpose.Search, 1), Is.False);
        Assert.That(allowance.Remaining(ReadPurpose.Search), Is.Zero);
    }

    [Test]
    public void ARefusedCharge_SpendsNothing()
    {
        var allowance = Policy.CreateAllowance();

        Assert.That(allowance.TryCharge(ReadPurpose.DeclaredStructure, 31), Is.False);
        Assert.That(allowance.TryCharge(ReadPurpose.DeclaredStructure, 30), Is.True);
    }

    [Test]
    public void Allows_SpendsNothing()
    {
        var allowance = Policy.CreateAllowance();

        Assert.That(allowance.Allows(ReadPurpose.Sniff, 10), Is.True);
        Assert.That(allowance.Remaining(ReadPurpose.Sniff), Is.EqualTo(10));
    }

    [Test]
    public void APayload_IsNeverCharged()
    {
        var allowance = Policy.CreateAllowance();

        Assert.That(Enumerable.Range(0, 100).All(_ => allowance.TryCharge(ReadPurpose.Payload, 40)), Is.True);
        Assert.That(allowance.Allows(ReadPurpose.Payload, 41), Is.False);
    }

    // The policy is shared by every file; what one file spends is its own.
    [Test]
    public void EachAllowance_StartsFromThePolicy_WhatAnotherHasSpent()
    {
        var first = Policy.CreateAllowance();
        first.TryCharge(ReadPurpose.Sniff, 10);

        Assert.That(Policy.CreateAllowance().Remaining(ReadPurpose.Sniff), Is.EqualTo(10));
    }
}
