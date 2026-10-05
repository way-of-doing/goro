// Owned by the evaluator group (G5) of the predicate-runtime-architecture line.
using Goro.Predicates.Values;

namespace Goro.Predicates.Binding;

/// <summary>
/// The conversions behind <c>NUMBER()</c> and <c>STRING()</c>, one per type they accept other than
/// their own. Only <see cref="NumberFromString"/> can fail.
/// </summary>
/// <remarks>
/// Both directions go through <see cref="NumberText"/>, so that a string <c>NUMBER()</c> accepts is
/// exactly a number literal, and what <c>STRING()</c> writes always reads back as the value it came
/// from. A bytecount is its number of bytes and a duration its number of seconds, without a unit.
/// </remarks>
public static class Conversions
{
    /// <summary>Fails for text that is not a number literal, or whose value a number cannot hold exactly.</summary>
    public static bool NumberFromString(string datum, out decimal number) => NumberText.TryParse(datum, out number);

    public static bool NumberFromByteCount(ByteCount datum, out decimal number)
    {
        number = datum.Bytes;
        return true;
    }

    public static bool NumberFromDuration(Duration datum, out decimal number)
    {
        number = datum.Seconds;
        return true;
    }

    public static bool StringFromNumber(decimal datum, out string text)
    {
        text = NumberText.Format(datum);
        return true;
    }

    public static bool StringFromByteCount(ByteCount datum, out string text) => StringFromNumber(datum.Bytes, out text);

    public static bool StringFromDuration(Duration datum, out string text) => StringFromNumber(datum.Seconds, out text);
}
