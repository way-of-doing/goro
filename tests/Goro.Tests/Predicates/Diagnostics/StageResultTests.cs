using Goro.Predicates.Diagnostics;
using Goro.Predicates.Syntax;

namespace Goro.Tests.Predicates.Diagnostics;

public class StageResultTests
{
    private static readonly Diagnostic AnError = new("some-error", new TextSpan(0, 1), "Something is wrong.");

    [Test]
    public void Then_AfterSuccess_RunsTheNextStage()
    {
        var result = StageResult<int>.Success(2).Then(n => StageResult<string>.Success(new string('x', n)));

        Assert.That(result.Value, Is.EqualTo("xx"));
    }

    [Test]
    public void Then_AfterFailure_PassesTheErrorsAlongWithoutRunningTheNextStage()
    {
        var ran = false;
        var result = StageResult<int>.Failure(AnError).Then(n =>
        {
            ran = true;
            return StageResult<string>.Success("");
        });

        Assert.That(ran, Is.False);
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Diagnostics, Is.EqualTo(new[] { AnError }));
    }

    [Test]
    public void Value_OfFailure_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _ = StageResult<int>.Failure(AnError).Value);
    }

    [Test]
    public void Failure_WithNoDiagnostics_Throws()
    {
        Assert.Throws<ArgumentException>(() => StageResult<int>.Failure([]));
    }

    [Test]
    public void Suggestion_ApplyTo_ReplacesItsSpan()
    {
        const string text = "genre != \"metal\"";
        var suggestion = new Suggestion(new TextSpan(0, text.Length), "NOT genre == \"metal\"");

        Assert.That(new Suggestion(new TextSpan(6, 2), "==").ApplyTo(text), Is.EqualTo("genre == \"metal\""));
        Assert.That(suggestion.ApplyTo(text), Is.EqualTo("NOT genre == \"metal\""));
    }
}
