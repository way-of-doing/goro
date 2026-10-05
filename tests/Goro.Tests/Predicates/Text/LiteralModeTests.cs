using System.Text;
using Goro.Predicates.Text;

namespace Goro.Tests.Predicates.Text;

// Literal mode composes characters and repairs malformed text, and must otherwise hand back what was
// recorded. Repair is covered by NormalizationTotalityTests.
public class LiteralModeTests
{
    private static string Prepare(string text) => Normalization.Prepare(text, ComparisonMode.Literal);

    private static readonly string[] ComposedText =
    [
        "", "metallica", "METALLICA", "Motörhead", "İstanbul", "Βαγγέλης", "ΟΔΥΣΣΕΥΣ", "Ёлка", "Йога", "Київ",
        "Ørsted", "Straße", "Ænima", "Łódź", "Đà Nẵng", "Þursaflokkurinn", "Đội", "ガンダム", "방탄소년단",
        "\u0915\u0941\u092E\u093E\u0930", "\u0E44\u0E21\u0E49", "\u05E9\u05B8\u05C1\u05DC\u05D5\u05B9\u05DD",
        "ＹＭＯ", "\uFF76\uFF9E\uFF9D\uFF80\uFF9E\uFF91", "\uFB01re", "Radio\u00ADhead", "\u2764\uFE0F", "½ ² ™",
        "\U0001F600", "\uFFFE", "\uFFFF",
    ];

    private static IEnumerable<TestCaseData> ComposedCases => ComposedText.Select(text => EscapedTestCase.Of(text));

    // The test's own decomposition would throw on U+FFFE.
    private static IEnumerable<TestCaseData> DecomposableCases =>
        ComposedText.Where(text => !text.Contains('\uFFFE')).Select(text => EscapedTestCase.Of(text));

    [TestCaseSource(nameof(ComposedCases))]
    public void ChangesNothingInComposedText(string text) => Assert.That(Prepare(text), Is.EqualTo(text));

    [TestCaseSource(nameof(DecomposableCases))]
    public void ComposesDecomposedText(string text) =>
        Assert.That(Prepare(text.Normalize(NormalizationForm.FormD)), Is.EqualTo(text));

    [Test]
    public void ComposedAndDecomposedAreEqual() =>
        Assert.That(Prepare("Moto\u0308rhead"), Is.EqualTo(Prepare("Mot\u00F6rhead")).And.EqualTo("Mot\u00F6rhead"));

    [TestCase("Motörhead", "Motorhead")]
    [TestCase("Metallica", "metallica")]
    [TestCase("ＹＭＯ", "YMO")]
    [TestCase("Radio\u00ADhead", "Radiohead")]
    [TestCase("Straße", "Strasse")]
    public void FoldsNothing(string a, string b) => Assert.That(Prepare(a), Is.Not.EqualTo(Prepare(b)));

    [Test]
    public void PreparedSubject_KeepsDiacritics() =>
        Assert.That(Prepare("Motörhead").Contains("otö", StringComparison.Ordinal), Is.True);
}
