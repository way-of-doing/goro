// Owned by the binder group (G4) of the predicate-runtime-architecture line.
namespace Goro.Predicates.Binding;

/// <summary>Finds the name somebody probably meant, for a name that names nothing.</summary>
internal static class Spelling
{
    /// <summary>
    /// The candidate closest to <paramref name="written"/>, ignoring case, if any is close enough: one
    /// edit away for a name of up to three characters, two for up to six, three beyond. An edit is an
    /// insertion, a deletion, a substitution or the transposition of two neighbours, so <c>yaer</c> is
    /// one edit from <c>year</c>. Ties go to the earliest candidate.
    /// </summary>
    public static string? Closest(string written, IEnumerable<string> candidates)
    {
        var limit = written.Length <= 3 ? 1 : written.Length <= 6 ? 2 : 3;
        string? best = null;
        var bestDistance = int.MaxValue;
        foreach (var candidate in candidates)
        {
            var distance = Distance(written.ToLowerInvariant(), candidate.ToLowerInvariant());
            if (distance <= limit && distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>The optimal string alignment distance between two strings.</summary>
    public static int Distance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++)
        {
            d[i, 0] = i;
        }

        for (var j = 0; j <= b.Length; j++)
        {
            d[0, j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
                }
            }
        }

        return d[a.Length, b.Length];
    }
}
