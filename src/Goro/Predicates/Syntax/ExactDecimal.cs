using System.Numerics;

namespace Goro.Predicates.Syntax;

/// <summary>
/// Arithmetic that either gives the exact <see cref="decimal"/> or fails, where
/// <see cref="decimal"/>'s own operators would round or throw. A literal is the value it appears to
/// be or it is an error, so a unit's scaling must never round.
/// </summary>
internal static class ExactDecimal
{
    private static readonly BigInteger MantissaLimit = BigInteger.One << 96;

    public static bool TryMultiply(decimal value, BigInteger factor, out decimal product)
    {
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value, bits);
        var mantissa = new BigInteger((uint)bits[0])
            | (new BigInteger((uint)bits[1]) << 32)
            | (new BigInteger((uint)bits[2]) << 64);
        var scale = (bits[3] >> 16) & 0xFF;
        var negative = bits[3] < 0;

        return TryCreate(mantissa * factor, scale, negative, out product);
    }

    public static bool TryFromInteger(BigInteger value, out decimal result) =>
        TryCreate(BigInteger.Abs(value), 0, value.Sign < 0, out result);

    private static bool TryCreate(BigInteger mantissa, int scale, bool negative, out decimal result)
    {
        while (scale > 0 && mantissa % 10 == 0)
        {
            mantissa /= 10;
            scale--;
        }

        if (mantissa >= MantissaLimit)
        {
            result = 0;
            return false;
        }

        var low = (int)(uint)(mantissa & uint.MaxValue);
        var middle = (int)(uint)((mantissa >> 32) & uint.MaxValue);
        var high = (int)(uint)(mantissa >> 64);
        result = new decimal(low, middle, high, negative, (byte)scale);
        return true;
    }
}
