using Goro.Predicates.Values;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// How an identifier is resolved for one file. A binding is shared by every concurrent evaluation
/// and keeps nothing of its own; what it reads it gets through <see cref="FileData"/>.
/// </summary>
public abstract class IdentifierBinding<T> where T : notnull
{
    /// <summary>
    /// Absent when the file records nothing for the identifier; otherwise its occurrences, each
    /// unusable one carrying <paramref name="origin"/>.
    /// </summary>
    /// <exception cref="UnreadableFileException">Something the identifier needs cannot be read.</exception>
    public abstract Value<T> Resolve(FileData file, Origin origin);
}
