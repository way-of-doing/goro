using Goro.Messages;
using Goro.Warnings;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Goro.Cli;

/// <summary>
/// The global options every command accepts: <c>--strict-exit-code</c> and
/// <c>--no-warn[=&lt;category&gt;,...]</c>. See docs/concepts/exit-codes.md and
/// docs/concepts/warnings.md.
/// </summary>
public abstract class GlobalSettings : CommandSettings
{
    [CommandOption("--strict-exit-code")]
    public bool StrictExitCode { get; set; }

    /// <summary>
    /// Set when <c>--no-warn</c> was given, with the category list as its value. The value can only
    /// arrive attached with <c>=</c>; see <see cref="NoWarnOption"/> for why.
    /// </summary>
    [CommandOption("--no-warn [CATEGORIES]")]
    public FlagValue<string>? NoWarn { get; set; }

    /// <summary>The categories the run must not produce: none unless <c>--no-warn</c> was given.</summary>
    public IReadOnlySet<WarningCategory> SuppressedWarnings =>
        NoWarn is { IsSet: true } && WarningCategories.TryParse(NoWarn.Value, out var categories, out _)
            ? categories
            : WarningCategories.None;

    public override ValidationResult Validate()
    {
        if (NoWarn is { IsSet: true } && !WarningCategories.TryParse(NoWarn.Value, out _, out var unknown))
        {
            // Spectre validates settings with no access to the container, so this message is
            // rendered by the English provider directly rather than the registered one.
            return ValidationResult.Error(EnglishErrorMessages.Instance.Render(
                new ErrorMessage.UnknownWarningCategory(new Code(unknown ?? ""), [.. WarningCategories.ValidNames.Select(name => new Code(name))])));
        }

        return ValidationResult.Success();
    }
}
