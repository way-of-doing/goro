namespace Goro.Cli;

/// <summary>
/// Makes a <c>--no-warn</c> given no categories reach validation with an empty category list.
///
/// <c>--no-warn</c> requires its categories, attached with <c>=</c> or as the next argument, since
/// a bare one would suppress every category, the warnings about files that cannot be read among
/// them. Spectre.Console.Cli fills a missing value with a placeholder of its own, which would then
/// be reported as a category nobody wrote, so a <c>--no-warn</c> followed by nothing, by another
/// option, or by a <c>--</c> separator is given an empty argument as its value before Spectre sees
/// the command line, and validation names the mistake for what it is. Nothing after a <c>--</c>
/// separator is touched, since there a <c>--no-warn</c> is a pathspec.
/// </summary>
public static class NoWarnOption
{
    private const string Bare = "--no-warn";
    private const string Separator = "--";

    public static string[] MarkMissingValue(IEnumerable<string> args)
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

            result.Add(arg);
            if (!separated && arg == Bare && (i + 1 == input.Length || input[i + 1].StartsWith('-')))
            {
                result.Add("");
            }
        }

        return [.. result];
    }
}
