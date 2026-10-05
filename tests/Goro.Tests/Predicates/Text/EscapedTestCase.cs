namespace Goro.Tests.Predicates.Text;

/// <summary>
/// Test cases whose arguments are shown with everything but printable ASCII escaped.
/// </summary>
/// <remarks>
/// Not a nicety: the test adapter silently drops a case whose name holds a noncharacter such as
/// U+FFFE, and a custom attribute cannot hold a lone surrogate at all, so text like that has to come
/// from a source, under a name that is plain ASCII.
/// </remarks>
internal static class EscapedTestCase
{
    public static TestCaseData Of(params string[] arguments) =>
        new TestCaseData(arguments.Cast<object>().ToArray()).SetArgDisplayNames(arguments.Select(Escape).ToArray());

    public static string Escape(string text) =>
        "\"" + string.Concat(text.Select(c => c is >= ' ' and <= '~' ? c.ToString() : $"\\u{(int)c:X4}")) + "\"";
}
