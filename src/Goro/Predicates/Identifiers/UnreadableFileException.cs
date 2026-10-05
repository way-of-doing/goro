namespace Goro.Predicates.Identifiers;

/// <summary>
/// Something an evaluation needed from a file could not be read. It abandons the evaluation: the
/// file was not processed, and is reported with one file warning and nothing else.
/// </summary>
public sealed class UnreadableFileException(string path, string reason, Exception? innerException = null)
    : Exception($"{path}: {reason}", innerException)
{
    public string Path { get; } = path;

    public string Reason { get; } = reason;
}
