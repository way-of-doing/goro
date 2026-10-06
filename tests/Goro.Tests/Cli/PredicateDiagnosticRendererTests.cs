using Goro.Cli;
using Goro.Messages;
using Goro.Predicates.Diagnostics;

namespace Goro.Tests.Cli;

public class PredicateDiagnosticRendererTests
{
    // Built from code points so that this file stays plain ASCII.
    private static readonly string CombiningAcute = char.ConvertFromUtf32(0x0301);
    private static readonly string GClef = char.ConvertFromUtf32(0x1D11E);

    private static string[] Render(string text, params Diagnostic[] diagnostics) =>
        Render(EnglishErrorMessages.Instance, text, diagnostics);

    private static string[] Render(IErrorMessages messages, string text, params Diagnostic[] diagnostics)
    {
        var writer = new StringWriter { NewLine = "\n" };
        PredicateDiagnosticRenderer.Write(writer, messages, text, diagnostics);
        return writer.ToString().Split('\n')[..^1];
    }

    private static Diagnostic Error(int start, int length, params Suggestion[] suggestions) =>
        new("test", new TextSpan(start, length), new ErrorMessage.ChainedComparison(), [.. suggestions]);

    [Test]
    public void ADiagnostic_IsItsMessage_ThePredicateEchoed_TheSpanMarked_AndEachSuggestionAsTheWholePredicateRewritten()
    {
        const string text = "file::name = \"a.mp3\"";
        var equals = new TextSpan(11, 1);

        var lines = Render(text, new Diagnostic("borrowed-operator", equals, new ErrorMessage.BorrowedEqual(), [new Suggestion(equals, "==")]));

        Assert.That(lines, Is.EqualTo(new[]
        {
            "goro: error: We need to use `==` to compare like that, goro!",
            "  file::name = \"a.mp3\"",
            "             ^",
            "  try: file::name == \"a.mp3\"",
        }));
    }

    [Test]
    public void TheMarker_SpansTheWholeSpan()
    {
        Assert.That(Render("file::name == \"a.mp3\"", Error(14, 7))[2], Is.EqualTo("  " + new string(' ', 14) + "^^^^^^^"));
    }

    [Test]
    public void AnEmptySpan_IsMarkedWithOneMark_WhereItFalls()
    {
        Assert.That(Render("a == b", Error(4, 0))[2], Is.EqualTo("      ^"));
    }

    [Test]
    public void AnEmptySpanAtTheEnd_IsMarkedJustPastTheLastCharacter()
    {
        Assert.That(Render("a ==", Error(4, 0))[2], Is.EqualTo("      ^"));
    }

    [Test]
    public void ADiagnosticWithoutSuggestions_SuggestsNothing()
    {
        Assert.That(Render("a == b", Error(0, 1)), Has.Length.EqualTo(3));
    }

    [Test]
    public void SeveralSuggestions_AreEachTheWholePredicateRewritten_InOrder()
    {
        const string text = "artist ~= \"a\"";
        var lines = Render(text, Error(7, 2,
            new Suggestion(new TextSpan(7, 6), "=~ r\"a\""),
            new Suggestion(new TextSpan(7, 2), "!=")));

        Assert.That(lines[3..], Is.EqualTo(new[]
        {
            "  try: artist =~ r\"a\"",
            "  try: artist != \"a\"",
        }));
    }

    [Test]
    public void SeveralDiagnostics_AreEachWrittenInFull_InOrder()
    {
        var lines = Render("a == b", Error(0, 1), new Diagnostic("test", new TextSpan(5, 1), new ErrorMessage.BooleanRange()));

        Assert.That(lines, Is.EqualTo(new[]
        {
            "goro: error: One comparison at a time, goro!",
            "  a == b",
            "  ^",
            "goro: error: A range can't run between booleans, goro!",
            "  a == b",
            "       ^",
        }));
    }

    [Test]
    public void AnEmptyPredicate_IsReportedWithoutAnEcho()
    {
        Assert.That(Render("", new Diagnostic("empty-predicate", new TextSpan(0, 0), new ErrorMessage.EmptyPredicate())),
            Is.EqualTo(new[] { "goro: error: Give the filter something to check, goro!" }));
    }

    [Test]
    public void TheWords_AreAllTheProvidersOwn_ItsPrefix_ItsMessage_AndItsSuggestionLabel()
    {
        var lines = Render(new NamingMessages(), "a = b",
            new Diagnostic("borrowed-operator", new TextSpan(2, 1), new ErrorMessage.BorrowedEqual(), [new Suggestion(new TextSpan(2, 1), "==")]));

        Assert.That(lines, Is.EqualTo(new[]
        {
            "E> BorrowedEqual",
            "  a = b",
            "    ^",
            "  => a == b",
        }));
    }

    /// <summary>A provider in no language at all, which names each message by its case.</summary>
    private sealed class NamingMessages : IErrorMessages
    {
        public string Render(ErrorMessage message) => message.GetType().Name;

        public string ErrorPrefix => "E> ";

        public string SuggestionLabel => "=> ";
    }

    // --- alignment ---

    [Test]
    public void ACombiningMarkBeforeTheSpan_TakesNoColumnOfItsOwn()
    {
        var text = $"e{CombiningAcute} = 1";

        Assert.That(Render(text, Error(3, 1))[2], Is.EqualTo("    ^"));
    }

    [Test]
    public void ASurrogatePairBeforeTheSpan_TakesOneColumn()
    {
        var text = $"{GClef} = 1";

        Assert.That(Render(text, Error(3, 1))[2], Is.EqualTo("    ^"));
    }

    [Test]
    public void ASpanOverACharacterAndItsCombiningMark_IsOneMarkWide()
    {
        var text = $"x == e{CombiningAcute}";

        Assert.That(Render(text, Error(5, 2))[2], Is.EqualTo("       ^"));
    }

    [Test]
    public void ASpanStartingAtACombiningMark_IsMarkedFromTheCharacterItCombinesWith()
    {
        var text = $"x == e{CombiningAcute}";

        Assert.That(Render(text, Error(6, 1))[2], Is.EqualTo("       ^"));
    }

    [Test]
    public void ALineBreakOrTabInThePredicate_IsEchoedAsASpace_KeepingTheEchoOnOneLine_AndTheMarkerAligned()
    {
        const string text = "a ==\r\n\tb = c";

        var lines = Render(text, Error(9, 1, new Suggestion(new TextSpan(9, 1), "==")));

        Assert.That(lines, Is.EqualTo(new[]
        {
            "goro: error: One comparison at a time, goro!",
            "  a ==   b = c",
            "           ^",
            "  try: a ==   b == c",
        }));
    }
}
