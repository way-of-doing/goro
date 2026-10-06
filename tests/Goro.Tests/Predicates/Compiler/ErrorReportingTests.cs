using Goro.Messages;
using Goro.Predicates.Binding;
using Goro.Predicates.Syntax;
using Goro.Predicates.Values;
using Goro.Tests.Predicates.Support;
using static Goro.Tests.Predicates.Support.CompileAssert;
using Codes = Goro.Predicates.Binding.SemanticDiagnosticCodes;

namespace Goro.Tests.Predicates.Compiler;

/// <summary>
/// How many errors reading a predicate reports: the first syntax error, or else every independent
/// semantic error, in text order, each once.
/// </summary>
public class ErrorReportingTests
{
    [Test]
    public void SyntaxError_StopsBeforeBinding()
    {
        var error = Error("artst == ");

        Assert.That(error.Code, Is.EqualTo(SyntaxDiagnosticCodes.UnexpectedEnd));
    }

    [Test]
    public void EveryIndependentError_IsReported_InTextOrder()
    {
        const string text = "yeer == 1 AND COUNT(ALL(genre)) > 1 AND file::duration > 1.5 OR artist BETWEEN \"b\"..\"a\"";

        var errors = Errors(text);

        Assert.That(errors.Select(e => e.Code), Is.EqualTo(new[]
        {
            Codes.UnknownIdentifier, Codes.MisplacedModifier, Codes.FractionalDurationLiteral, Codes.RangeReversed,
        }));
        Assert.That(errors.Select(e => e.Span.Start), Is.Ordered);
    }

    [Test]
    public void ErrorFoundAtAnOperator_IsOrderedByWhereItIsMarked()
    {
        const string text = "genre != artst";

        var errors = Errors(text);

        Assert.That(errors.Select(e => Marked(text, e)), Is.EqualTo(new[] { "!=", "artst" }));
    }

    [Test]
    public void AMessage_CarriesThePredicatesOwnText_AsWritten()
    {
        Assert.That(Error("artist  ==  1.50").Message, Is.EqualTo(
            new ErrorMessage.TypeMismatch(new Code("artist"), GoroType.String, new Code("1.50"), GoroType.Number, new Code("=="))));
    }

    [Test]
    public void BindingReadsNoFile()
    {
        var catalog = TestCatalog.Standard();

        Compiles("x > 1 AND genre == \"x\"", catalog);
        Errors("x > 1 AND genre == 1", catalog);

        Assert.That(catalog.Resolutions("x") + catalog.Resolutions("genre"), Is.Zero);
    }

    [Test]
    public void EveryCode_IsKebabCase()
    {
        var codes = typeof(SemanticDiagnosticCodes).GetFields().Select(field => (string)field.GetValue(null)!);

        Assert.That(codes, Is.All.Match("^[a-z]+(-[a-z]+)*$"));
        Assert.That(codes, Is.Unique);
    }
}
