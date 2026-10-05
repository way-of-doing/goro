using Goro.Predicates.Syntax;
using static Goro.Tests.Predicates.Syntax.SyntaxAssert;

namespace Goro.Tests.Predicates.Syntax;

public class SyntaxPrinterTests
{
    [TestCase("\"a\\\"b\\\\c\\n\\r\\t\"", "\"a\\\"b\\\\c\\n\\r\\t\"")]
    [TestCase("\"\\x01\"", "\"\\x01\"")]
    [TestCase("r\"a\"\"b\\\"", "r\"a\"\"b\\\"")]
    [TestCase("+1.50", "1.5")]
    [TestCase("-.5", "-0.5")]
    [TestCase("1.4kib", "1433.6b")]
    [TestCase("1h10m", "4200s")]
    [TestCase("90:00", "5400s")]
    [TestCase("true", "TRUE")]
    [TestCase("NULL", "NULL")]
    public void Literal_IsWrittenAsALiteral(string text, string printed)
    {
        Assert.That(Tree(text), Is.EqualTo(printed));
    }

    [TestCase("\"a\\\"b\\\\c\\n\\r\\t\\x01\"")]
    [TestCase("r\"a\"\"b\\\"")]
    [TestCase("1.4kib")]
    [TestCase("-0.000")]
    [TestCase("250:00:00")]
    public void Literal_ReadsBackToTheSameValue(string text)
    {
        var once = Tree(text);
        Assert.That(Tree(once), Is.EqualTo(once));
        var original = (LiteralSyntax)Parses(text).Root;
        var reread = (LiteralSyntax)Parses(once).Root;
        Assert.That(reread.Token with { Span = default }, Is.EqualTo(original.Token with { Span = default }));
    }
}
