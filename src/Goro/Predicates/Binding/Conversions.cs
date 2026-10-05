// Owned by the evaluator group (G5) of the predicate-runtime-architecture line.
using Goro.Predicates.Values;

namespace Goro.Predicates.Binding;

/// <summary>
/// The conversions behind <c>NUMBER()</c> and <c>STRING()</c>, one per type they accept other than
/// their own. Only <see cref="NumberFromString"/> can fail.
/// </summary>
public static class Conversions
{
    public static bool NumberFromString(string datum, out decimal number) => throw new NotImplementedException();

    public static bool NumberFromByteCount(ByteCount datum, out decimal number) => throw new NotImplementedException();

    public static bool NumberFromDuration(Duration datum, out decimal number) => throw new NotImplementedException();

    public static bool StringFromNumber(decimal datum, out string text) => throw new NotImplementedException();

    public static bool StringFromByteCount(ByteCount datum, out string text) => throw new NotImplementedException();

    public static bool StringFromDuration(Duration datum, out string text) => throw new NotImplementedException();
}
