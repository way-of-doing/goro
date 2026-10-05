using System.Collections.Immutable;
using Goro.Messages;
using Goro.Warnings;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Goro.Cli;

/// <summary>
/// The global options every command accepts: <c>--strict-exit-code</c> and
/// <c>--no-warn=&lt;category&gt;,...</c>. See docs/concepts/exit-codes.md and
/// docs/concepts/warnings.md.
/// </summary>
public abstract class GlobalSettings : CommandSettings
{
    [CommandOption("--strict-exit-code")]
    public bool StrictExitCode { get; set; }

    /// <summary>
    /// The category list given to <c>--no-warn</c>, or null when the option was not given. A value
    /// is required, so that a bare <c>--no-warn</c>, which would otherwise suppress every category
    /// including the warnings about files that cannot be read, is a mistake rather than a meaning.
    /// </summary>
    [CommandOption("--no-warn <CATEGORIES>")]
    public string? NoWarn { get; set; }

    /// <summary>The categories the run must not produce: none unless <c>--no-warn</c> was given.</summary>
    public IReadOnlySet<WarningCategory> SuppressedWarnings =>
        NoWarn is not null && WarningCategories.TryParse(NoWarn, out var categories, out _)
            ? categories
            : WarningCategories.None;

    // Spectre validates settings with no access to the container, so these messages are rendered by
    // the English provider directly rather than the registered one.
    public override ValidationResult Validate()
    {
        if (NoWarn is null)
        {
            return ValidationResult.Success();
        }

        var valid = WarningCategories.ValidNames.Select(name => new Code(name)).ToImmutableArray();
        if (NoWarn.Length == 0)
        {
            return ValidationResult.Error(EnglishErrorMessages.Instance.Render(new ErrorMessage.MissingWarningCategory(valid)));
        }

        return WarningCategories.TryParse(NoWarn, out _, out var unknown)
            ? ValidationResult.Success()
            : ValidationResult.Error(EnglishErrorMessages.Instance.Render(
                new ErrorMessage.UnknownWarningCategory(new Code(unknown ?? ""), valid)));
    }
}
