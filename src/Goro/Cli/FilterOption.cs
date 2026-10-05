namespace Goro.Cli;

/// <summary>
/// Makes <c>--filter &lt;predicate&gt;</c> take the next argument whole, whatever it begins with.
///
/// Spectre.Console.Cli reads a separate option value that begins with <c>-</c> as more options:
/// given <c>--filter "-1 &lt; x"</c> it binds <c>-1</c> alone, so a predicate would silently
/// become a different one. The attached form, <c>--filter=&lt;predicate&gt;</c>, is passed through
/// intact, so the separate form is rewritten to it before Spectre sees the command line, as
/// <see cref="NoWarnOption"/> rewrites <c>--no-warn</c>. Nothing
/// after a <c>--</c> separator is touched, since there a <c>--filter</c> is a pathspec.
/// </summary>
public static class FilterOption
{
    private const string Name = "--filter";
    private const string Separator = "--";

    public static string[] AttachValue(IEnumerable<string> args)
    {
        var input = args.ToArray();
        var result = new List<string>(input.Length);
        var separated = false;
        for (var i = 0; i < input.Length; i++)
        {
            var arg = input[i];
            if (!separated && arg == Separator)
            {
                separated = true;
            }

            // An empty value is left separate: Spectre binds it as it is, and it is then reported
            // as an empty predicate, where the attached form would be rejected as a missing value.
            if (!separated && arg == Name && i + 1 < input.Length && input[i + 1].Length > 0)
            {
                result.Add($"{Name}={input[++i]}");
                continue;
            }

            result.Add(arg);
        }

        return [.. result];
    }
}
