namespace Goro.Predicates.Values;

/// <summary>
/// The choice <c>PREFERRED()</c> makes, and every concept written without a source with it: the first candidate
/// holding a usable occurrence, taken entire; failing that, the first that is not absent; failing
/// that, absent.
/// </summary>
/// <remarks>
/// This is the one implementation of the choice, so that a global identifier and its expansion
/// cannot drift apart. The candidates are enumerated lazily and the enumeration stops at the one
/// chosen, so a candidate after it is never computed: it loads nothing and cannot make the file
/// unreadable.
/// </remarks>
public static class Preference
{
    public static Value<T> Preferred<T>(IEnumerable<Value<T>> candidates) where T : notnull
    {
        Value<T>? firstPresent = null;
        foreach (var candidate in candidates)
        {
            if (candidate.IsAbsent)
            {
                continue;
            }

            if (candidate.Occurrences.Any(occurrence => occurrence is Usable<T>))
            {
                return candidate;
            }

            firstPresent ??= candidate;
        }

        return firstPresent ?? Value<T>.Absent;
    }
}
