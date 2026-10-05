using System.Text;

namespace Goro.Predicates.Text;

/// <summary>Unicode normalization forms, made total over well-formed UTF-16.</summary>
/// <remarks>
/// <see cref="string.Normalize(NormalizationForm)"/> throws on the noncharacter U+FFFE as well as
/// on unpaired surrogates. U+FFFE is a stable code point in the sense of UAX #15: it has canonical
/// combining class 0, no decomposition, and composes with nothing, so no normalization form ever
/// acts across it. Normalizing the text between occurrences of it and joining the pieces back
/// together is therefore exactly what normalizing the whole would have been, had it been allowed.
/// </remarks>
internal static class UnicodeForms
{
    private const char Noncharacter = '\uFFFE';

    /// <summary>Normalizes text that has no unpaired surrogates; never throws on any such text.</summary>
    public static string Normalize(string text, NormalizationForm form)
    {
        if (!text.Contains(Noncharacter)) return text.Normalize(form);

        var pieces = text.Split(Noncharacter);
        for (int i = 0; i < pieces.Length; i++)
            pieces[i] = pieces[i].Normalize(form);
        return string.Join(Noncharacter, pieces);
    }
}
