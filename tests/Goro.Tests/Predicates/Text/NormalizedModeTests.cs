using Goro.Predicates.Text;

namespace Goro.Tests.Predicates.Text;

// The "String normalization" scenarios of docs/testing.md that concern normalized mode, and for every
// step of docs/concepts/normalization.md a script it is meant to affect beside one it is meant to
// leave alone. Where it matters whether a character is stored composed or decomposed, or it cannot
// be seen, the string is written with escapes.
public class NormalizedModeTests
{
    private static string Prepare(string text) => Normalization.Prepare(text, ComparisonMode.Normalized);

    private static void AssertEqual(string a, string b) =>
        Assert.That(Prepare(a), Is.EqualTo(Prepare(b)), $"\"{a}\" and \"{b}\" should be equal");

    private static void AssertDifferent(string a, string b) =>
        Assert.That(Prepare(a), Is.Not.EqualTo(Prepare(b)), $"\"{a}\" and \"{b}\" should remain different");

    // The scenarios table, row by row.

    [TestCase("Motörhead", "motorhead")]
    [TestCase("METALLICA", "metallica")]
    [TestCase("İstanbul", "istanbul")]
    [TestCase("Βαγγέλης", "βαγγελης")]
    public void FoldsCaseAndLatinAndGreekAccents(string text, string folded) => AssertEqual(text, folded);

    [Test]
    public void FoldsFinalSigma() => AssertEqual("ΟΔΥΣΣΕΥΣ", "οδυσσευς");

    [Test]
    public void FoldsCyrillicYo() => AssertEqual("Ёлка", "елка");

    [TestCase("Йога", "иога")]
    [TestCase("Київ", "Киів")]
    public void KeepsTheMarksOfCyrillicShortIAndYi(string letter, string withoutMark) => AssertDifferent(letter, withoutMark);

    [TestCase("Ørsted", "orsted")]
    [TestCase("Straße", "strasse")]
    [TestCase("Ænima", "aenima")]
    [TestCase("Łódź", "lodz")]
    [TestCase("Đà Nẵng", "da nang")]
    [TestCase("Þursaflokkurinn", "thursaflokkurinn")]
    public void FoldsLettersDecompositionCannotReach(string text, string folded) => AssertEqual(text, folded);

    [Test]
    public void FoldsALetterCarryingTwoDiacritics() => AssertEqual("Đội", "doi");

    [Test]
    public void KeepsKanaVoicingMarks() => AssertDifferent("ガンダム", "カンタム");

    [TestCase("\u0915\u0941\u092E\u093E\u0930", "\u0915\u092E\u093E\u0930")]   // कुमार, कमार
    [TestCase("\u0E44\u0E21\u0E49", "\u0E44\u0E21")]                               // ไม้, ไม
    public void KeepsDevanagariVowelSignsAndThaiToneMarks(string marked, string unmarked) => AssertDifferent(marked, unmarked);

    [Test]
    public void KeepsHebrewPoints() =>
        AssertDifferent("\u05E9\u05B8\u05C1\u05DC\u05D5\u05B9\u05DD", "\u05E9\u05DC\u05D5\u05DD");   // שָׁלוֹם, שלום

    [Test]
    public void HangulSurvivesDecompositionAndRecomposition() => Assert.That(Prepare("방탄소년단"), Is.EqualTo("방탄소년단"));

    [TestCase("ＹＭＯ", "ymo")]
    [TestCase("\uFF76\uFF9E\uFF9D\uFF80\uFF9E\uFF91", "ガンダム")]   // ｶﾞﾝﾀﾞﾑ, halfwidth, voicing marks apart
    [TestCase("\uFB01re", "fire")]                                       // ﬁre, with a ligature
    public void FoldsCompatibilityForms(string text, string folded) => AssertEqual(text, folded);

    [TestCase("Radio\u00ADhead", "radiohead")]   // a soft hyphen
    [TestCase("\u2764\uFE0F", "\u2764")]        // a variation selector
    public void RemovesDefaultIgnorableCharacters(string text, string without) => AssertEqual(text, without);

    [Test]
    public void ComposedAndDecomposedAreEqual() => AssertEqual("Mot\u00F6rhead", "Moto\u0308rhead");

    // What the regular expression scenarios rely on of the prepared subject. The matching itself, and
    // the pattern left unprepared, belong to the evaluator's tests.

    [Test]
    public void PreparedSubject_HasNoDiacriticAndNoCapital() => Assert.That(Prepare("Motörhead"), Is.EqualTo("motorhead"));

    [TestCase("방탄소년단", "소년")]
    [TestCase("ガンダム", "ガン")]
    public void PreparedSubject_IsComposed(string subject, string composedPart) =>
        Assert.That(Prepare(subject).Contains(composedPart, StringComparison.Ordinal), Is.True, Prepare(subject));

    [Test]
    public void PreparedSubject_HasLettersFolded() => Assert.That(Prepare("Straße"), Is.EqualTo("strasse"));

    // Step 1 is covered by NormalizationTotalityTests, and step 3 in full by DefaultIgnorableTests.

    // Step 2: compatibility forms are decomposed, and text without them is left as it is.

    [TestCase("²", "2")]
    [TestCase("½", "1\u20442")]
    [TestCase("™", "tm")]
    [TestCase("\u00A0", " ")]   // a no-break space is a compatibility form of a space
    public void Step2_DecomposesCompatibilityForms(string text, string prepared) => Assert.That(Prepare(text), Is.EqualTo(prepared));

    [TestCase("宇多田ヒカル")]
    [TestCase("Кино")]
    [TestCase("\u0915\u0941\u092E\u093E\u0930")]
    public void Step2_LeavesTextWithoutCompatibilityFormsAsItIs(string text) => Assert.That(Prepare(text), Is.EqualTo(text.ToLowerInvariant()));

    // Step 3: removal happens before step 4 looks for a mark's base, so a mark is not stranded by it.

    [Test]
    public void Step3_ARemovedCharacterIsNotTheBaseOfAMark()
    {
        AssertEqual("e\u200D\u0301", "e");
        AssertEqual("\u0435\u200D\u0308", "\u0435");
    }

    [TestCase("Radio-head", "radiohead")]
    [TestCase("Radio head", "radiohead")]
    public void Step3_KeepsVisibleCharacters(string text, string without) => AssertDifferent(text, without);

    // Step 4: a mark goes when the nearest character before it that is not a mark is in one of the
    // Latin and Greek ranges, and stays otherwise. Each range is tried at both ends: inside, and
    // outside where the character just outside is not itself a mark.

    [TestCase(0x0041), TestCase(0x005A), TestCase(0x0061), TestCase(0x007A)]
    [TestCase(0x00C0), TestCase(0x02AF)]
    [TestCase(0x0370), TestCase(0x03FF)]
    [TestCase(0x1D00), TestCase(0x1DBF)]
    [TestCase(0x1E00), TestCase(0x1FFC)]   // U+1FFD..1FFE decompose to a space and a mark, and U+1FFF is unassigned
    [TestCase(0x2C60), TestCase(0x2C7F)]
    [TestCase(0xA720), TestCase(0xA7FF)]
    [TestCase(0xAB30), TestCase(0xAB6B)]   // U+AB6C..AB6F are unassigned
    public void Step4_RemovesAMarkAfterALatinOrGreekCharacter(int baseCodePoint)
    {
        var letter = char.ConvertFromUtf32(baseCodePoint);
        AssertEqual(letter + "\u0301", letter);
    }

    [TestCase(0x0040), TestCase(0x005B), TestCase(0x0060), TestCase(0x007B)]
    [TestCase(0x00BF), TestCase(0x02B9)]   // U+02B0..02B8 decompose to Latin letters
    [TestCase(0x0402)]
    [TestCase(0x1CFA)]
    [TestCase(0x2010)]
    [TestCase(0x2C5F), TestCase(0x2C80)]
    [TestCase(0xA71F), TestCase(0xA800)]
    [TestCase(0xAB2E), TestCase(0xAB70)]
    public void Step4_KeepsAMarkAfterAnyOtherCharacter(int baseCodePoint)
    {
        var character = char.ConvertFromUtf32(baseCodePoint);
        AssertDifferent(character + "\u0301", character);
    }

    [Test]
    public void Step4_LooksPastOtherMarksForTheBase()
    {
        AssertEqual("o\u0323\u0302", "o");                        // ộ, decomposed
        AssertDifferent("\u0915\u0941\u0301", "\u0915\u0941");   // a Devanagari vowel sign, then an acute
    }

    [Test]
    public void Step4_KeepsAMarkWithNoBase() => AssertDifferent("\u0301a", "a");

    [Test]
    public void Step4_KeepsTheMarksOfOtherScripts()
    {
        AssertDifferent("\u0627\u064E", "\u0627");   // Arabic fatha
        AssertDifferent("\u0430\u0301", "\u0430");   // Cyrillic а with a stress mark
        AssertDifferent("\u04D3", "\u0430");          // ӓ: the diaeresis is folded after е only
    }

    [Test]
    public void Step4_RemovesTheDiaeresisAfterCyrillicE_PastOtherMarks()
    {
        // е, a stress mark, then a diaeresis: the nearest character before the diaeresis that is not
        // a mark is е.
        AssertEqual("\u0435\u0301\u0308", "\u0435\u0301");
        AssertEqual("\u0415\u0308", "\u0435");
    }

    // Step 5: simple case mappings, and no language-specific rules.

    [Test]
    public void Step5_FoldsDotlessIToI() => Assert.That(Prepare("ı"), Is.EqualTo("i"));

    [Test]
    public void Step5_FoldsCapitalIToDottedI() => Assert.That(Prepare("DIŞ"), Is.EqualTo("dis"));

    [Test]
    public void Step5_FoldsCaseInScriptsBeyondLatinAndGreek()
    {
        AssertEqual("\u13A0", "\uAB70");           // Cherokee
        AssertEqual("КИНО", "кино");
        AssertEqual("\U00010400", "\U00010428");   // Deseret, above the BMP
    }

    [TestCase("宇多田ヒカル")]
    [TestCase("방탄소년단")]
    public void Step5_LeavesUncasedScriptsAlone(string text) => Assert.That(Prepare(text), Is.EqualTo(text));

    // Step 6: every entry of the letter table, upper and lower case, and letters outside it kept.

    [TestCase("ø", "o"), TestCase("Ø", "o")]
    [TestCase("æ", "ae"), TestCase("Æ", "ae")]
    [TestCase("œ", "oe"), TestCase("Œ", "oe")]
    [TestCase("ß", "ss"), TestCase("ẞ", "ss")]
    [TestCase("ł", "l"), TestCase("Ł", "l")]
    [TestCase("đ", "d"), TestCase("Đ", "d")]
    [TestCase("ð", "d"), TestCase("Ð", "d")]
    [TestCase("þ", "th"), TestCase("Þ", "th")]
    [TestCase("ħ", "h"), TestCase("Ħ", "h")]
    public void Step6_FoldsEveryLetterOfTheTable(string letter, string folded) => Assert.That(Prepare(letter), Is.EqualTo(folded));

    [TestCase("ŋ")]
    [TestCase("ŧ")]
    [TestCase("ɖ")]
    public void Step6_KeepsOtherLettersWithoutDecomposition(string letter) => Assert.That(Prepare(letter), Is.EqualTo(letter));

    // Step 7: whatever keeps its mark is recomposed.

    [TestCase("\u0439")]   // й
    [TestCase("\u0457")]   // ї
    [TestCase("ガンダム")]
    [TestCase("방탄소년단")]
    public void Step7_RecomposesWhatKeepsItsMark(string composed) => Assert.That(Prepare(composed), Is.EqualTo(composed));

    [Test]
    public void Step7_ComposesWhatWasStoredDecomposed() => Assert.That(Prepare("\u0438\u0306"), Is.EqualTo("\u0439"));

    [Test]
    public void Step7_DoesNotUndoCompatibilityFolding() => Assert.That(Prepare("\uFB01"), Is.EqualTo("fi"));

    // The fast path for ASCII.

    [TestCase("", "")]
    [TestCase("metallica", "metallica")]
    [TestCase("Nine Inch Nails 42", "nine inch nails 42")]
    [TestCase("AC/DC", "ac/dc")]
    public void Ascii_IsLowercased(string text, string prepared) => Assert.That(Prepare(text), Is.EqualTo(prepared));
}
