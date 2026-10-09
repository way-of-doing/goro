using System.Collections.Frozen;

namespace Goro.Warnings;

/// <summary>
/// Reads the value of <c>--no-warn</c>: a comma-separated list of category names, where
/// <c>all</c> stands for every category. Names are case-insensitive, as Goro's other option values
/// are. See the "Suppressing warnings" section of docs/concepts/warnings.md.
/// </summary>
public static class WarningCategories
{
    private const string AllName = "all";

    private static readonly (string Name, WarningCategory Value)[] Entries =
    [
        ("data", WarningCategory.Data),
        ("unanswered", WarningCategory.Unanswered),
        ("incomplete", WarningCategory.Incomplete),
        ("file", WarningCategory.File),
    ];

    public static IReadOnlySet<WarningCategory> None { get; } = FrozenSet<WarningCategory>.Empty;

    public static IReadOnlySet<WarningCategory> All { get; } = Entries.Select(e => e.Value).ToFrozenSet();

    /// <summary>Every name <see cref="TryParse"/> accepts, for error messages.</summary>
    public static IReadOnlyList<string> ValidNames { get; } = [.. Entries.Select(e => e.Name), AllName];

    /// <summary>
    /// Parses a category list. Null or empty means every category, which is what the bare option
    /// means.
    /// </summary>
    /// <param name="unknown">The first name not recognised, when parsing fails.</param>
    public static bool TryParse(string? list, out IReadOnlySet<WarningCategory> categories, out string? unknown)
    {
        categories = None;
        unknown = null;

        if (string.IsNullOrWhiteSpace(list))
        {
            categories = All;
            return true;
        }

        var parsed = new HashSet<WarningCategory>();
        foreach (var item in list.Split(','))
        {
            var name = item.Trim();
            if (string.Equals(name, AllName, StringComparison.OrdinalIgnoreCase))
            {
                parsed.UnionWith(All);
                continue;
            }

            var match = Entries.SingleOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
            if (match.Name is null)
            {
                unknown = name;
                return false;
            }

            parsed.Add(match.Value);
        }

        categories = parsed.ToFrozenSet();
        return true;
    }
}
