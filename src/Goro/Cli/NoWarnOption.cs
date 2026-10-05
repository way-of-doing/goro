namespace Goro.Cli;

/// <summary>
/// Makes <c>--no-warn</c> take its optional value only when attached with <c>=</c>, as its synopsis
/// <c>--no-warn[=&lt;category&gt;,...]</c> says and as git's optional option values work.
///
/// Spectre.Console.Cli binds an option with an optional value differently: given
/// <c>--no-warn /music</c>, it takes the next argument as the value, so the pathspec would be read
/// as a category list and rejected. Spectre has no setting for the attached-only form, so the bare
/// option is rewritten to its explicit equivalent, <c>--no-warn=all</c>, before Spectre sees the
/// command line. Nothing after a <c>--</c> separator is touched, since there a <c>--no-warn</c> is a
/// pathspec.
/// </summary>
public static class NoWarnOption
{
    private const string Bare = "--no-warn";
    private const string Separator = "--";

    public static string[] AttachValues(IEnumerable<string> args)
    {
        var result = new List<string>();
        var separated = false;
        foreach (var arg in args)
        {
            if (!separated && arg == Separator)
            {
                separated = true;
            }

            result.Add(!separated && arg == Bare ? $"{Bare}=all" : arg);
        }

        return [.. result];
    }
}
