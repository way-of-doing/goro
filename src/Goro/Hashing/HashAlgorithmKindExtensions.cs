using System.Security.Cryptography;

namespace Goro.Hashing;

/// <summary>
/// Owns everything about <see cref="HashAlgorithmKind"/>'s string representation and
/// meaning: name↔value mapping (for parsing and error messages), the lowercase name
/// recorded in a <see cref="Goro.Domain.HashResult"/>, and the concrete
/// <see cref="HashAlgorithm"/> instance it selects. Centralizing this here means
/// adding an algorithm only requires touching this one file, not every place that
/// used to switch on the enum.
/// </summary>
public static class HashAlgorithmKindExtensions
{
    private static readonly (string Name, HashAlgorithmKind Value)[] Entries =
    [
        ("md5", HashAlgorithmKind.Md5),
        ("sha1", HashAlgorithmKind.Sha1),
    ];

    public static IReadOnlyList<string> ValidNames { get; } = Entries.Select(e => e.Name).ToArray();

    /// <summary>Case-insensitive; null/empty means "use the default" (<see cref="HashAlgorithmKind.Md5"/>).</summary>
    public static bool TryParse(string? value, out HashAlgorithmKind algorithm)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            algorithm = HashAlgorithmKind.Md5;
            return true;
        }

        var match = Entries.SingleOrDefault(e => string.Equals(e.Name, trimmed, StringComparison.OrdinalIgnoreCase));
        algorithm = match.Value;
        return match.Name is not null;
    }

    public static string ToName(this HashAlgorithmKind algorithm) =>
        Entries.SingleOrDefault(e => e.Value == algorithm).Name
        ?? throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unsupported hash algorithm.");

    public static HashAlgorithm CreateHashAlgorithm(this HashAlgorithmKind algorithm) => algorithm switch
    {
        HashAlgorithmKind.Md5 => MD5.Create(),
        HashAlgorithmKind.Sha1 => SHA1.Create(),
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unsupported hash algorithm."),
    };
}
