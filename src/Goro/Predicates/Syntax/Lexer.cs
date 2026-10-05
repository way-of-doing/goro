// Owned by the syntax group (G2 + G3) of the predicate-runtime-architecture line.
using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Text;
using Goro.Predicates.Diagnostics;
using Goro.Predicates.Values;
using static Goro.Predicates.Syntax.SyntaxDiagnosticCodes;

namespace Goro.Predicates.Syntax;

/// <summary>
/// Divides predicate text into tokens, taking every token as long as it can be and treating
/// whitespace as significant only inside a literal. Stops at the first error.
/// </summary>
/// <remarks>
/// The lexer rejects only text that no token can begin, and literals whose value is not allowed.
/// Two tokens that touch are the parser's business: <c>5AND</c> is a number and a keyword, which
/// the grammar may well accept, so a literal running into what follows is diagnosed there.
/// </remarks>
public static class Lexer
{
    /// <returns>Every token, ending with <see cref="TokenKind.EndOfText"/>; or the first error.</returns>
    public static StageResult<ImmutableArray<Token>> Lex(string text) => new Scanner(text).Run();

    // Longest first, so that the first spelling that matches is the longest token.
    private static readonly (string Spelling, TokenKind Kind)[] Punctuation =
    [
        ("==", TokenKind.EqualEqual),
        ("=~", TokenKind.EqualTilde),
        ("!=", TokenKind.BangEqual),
        ("!~", TokenKind.BangTilde),
        ("<=", TokenKind.LessEqual),
        ("<>", TokenKind.LessGreater),
        (">=", TokenKind.GreaterEqual),
        ("~=", TokenKind.TildeEqual),
        ("&&", TokenKind.AmpersandAmpersand),
        ("||", TokenKind.BarBar),
        ("::", TokenKind.DoubleColon),
        ("..", TokenKind.DotDot),
        ("=", TokenKind.Equal),
        ("!", TokenKind.Bang),
        ("<", TokenKind.Less),
        (">", TokenKind.Greater),
        ("(", TokenKind.OpenParen),
        (")", TokenKind.CloseParen),
        (",", TokenKind.Comma),
    ];

    // Longest first, for the same reason: 5mib is a bytecount in mebibytes, not 5m followed by "ib".
    private static readonly (string Suffix, BigInteger Bytes)[] ByteUnits =
    [
        ("kib", BigInteger.Pow(2, 10)),
        ("mib", BigInteger.Pow(2, 20)),
        ("gib", BigInteger.Pow(2, 30)),
        ("tib", BigInteger.Pow(2, 40)),
        ("kb", BigInteger.Pow(10, 3)),
        ("mb", BigInteger.Pow(10, 6)),
        ("gb", BigInteger.Pow(10, 9)),
        ("tb", BigInteger.Pow(10, 12)),
        ("b", BigInteger.One),
    ];

    // The fields of a duration in units form, in the order they must be written.
    private static readonly (char Unit, int Seconds)[] DurationUnits = [('h', 3600), ('m', 60), ('s', 1)];

    /// <summary>
    /// One pass over the text. Each scanning method returns its token, or records the error and
    /// returns null, which ends the pass.
    /// </summary>
    private sealed class Scanner(string text)
    {
        private readonly ImmutableArray<Token>.Builder tokens = ImmutableArray.CreateBuilder<Token>();
        private int position;
        private Diagnostic? error;

        public StageResult<ImmutableArray<Token>> Run()
        {
            while (true)
            {
                // Unicode White_Space and char.IsWhiteSpace are the same set, all of it in the BMP.
                while (position < text.Length && char.IsWhiteSpace(text[position]))
                {
                    position++;
                }

                if (position == text.Length)
                {
                    tokens.Add(new Token(TokenKind.EndOfText, new TextSpan(position, 0)));
                    return StageResult<ImmutableArray<Token>>.Success(tokens.ToImmutable());
                }

                var token = ScanToken();
                if (token is null)
                {
                    return StageResult<ImmutableArray<Token>>.Failure(error!);
                }

                tokens.Add(token);
            }
        }

        private Token? ScanToken()
        {
            var c = text[position];
            if (char.IsAsciiLetter(c))
            {
                return c is 'r' or 'R' && At(position + 1) == '"' ? ScanRawString() : ScanName();
            }

            if (StartsNumber(position))
            {
                return ScanNumeric();
            }

            if (c == '"')
            {
                return ScanQuotedString();
            }

            foreach (var (spelling, kind) in Punctuation)
            {
                if (text.AsSpan(position).StartsWith(spelling, StringComparison.Ordinal))
                {
                    var span = new TextSpan(position, spelling.Length);
                    position += spelling.Length;
                    return new Token(kind, span);
                }
            }

            if (NonAsciiWordLength(position) > 0)
            {
                return RejectNonAsciiName(position);
            }

            return c == ':' ? StrayColon() : RejectUnexpectedCharacter();
        }

        // A sign or a period begins a number only when a digit follows: "-.5" and ".5" do, "-x" and ".." do not.
        private bool StartsNumber(int index)
        {
            if (text[index] is '+' or '-')
            {
                index++;
            }

            return char.IsAsciiDigit(At(index)) || (At(index) == '.' && char.IsAsciiDigit(At(index + 1)));
        }

        private Token? ScanName()
        {
            var start = position;
            while (IsAsciiWordChar(At(position)))
            {
                position++;
            }

            if (NonAsciiWordLength(position) > 0)
            {
                return RejectNonAsciiName(start);
            }

            var span = TextSpan.FromBounds(start, position);
            var name = span.Of(text);
            return TokenFacts.TryGetReservedWord(name, out var keyword) ? new Token(keyword, span) : new NameToken(span, name);
        }

        private Token? RejectNonAsciiName(int start)
        {
            var end = start;
            while (true)
            {
                if (IsAsciiWordChar(At(end)))
                {
                    end++;
                    continue;
                }

                var length = NonAsciiWordLength(end);
                if (length == 0)
                {
                    break;
                }

                end += length;
            }

            var word = text[start..end];
            return Fail(NonAsciiName, TextSpan.FromBounds(start, end),
                $"`{word}` is not a name: names are made of ASCII letters, digits and underscores. "
                + "A part after `::` may be quoted to hold any other characters.");
        }

        // ------------------------------------------------------------------------------------------
        // Strings

        private Token? ScanQuotedString()
        {
            var start = position++;
            var value = new StringBuilder();

            // A high surrogate written as an escape must be followed at once by a low one.
            TextSpan? unpairedHighEscape = null;

            while (true)
            {
                if (position >= text.Length)
                {
                    return RejectUnterminatedString(start);
                }

                var c = text[position];
                if (c == '"')
                {
                    if (unpairedHighEscape is { } high)
                    {
                        return RejectLoneSurrogate(high);
                    }

                    position++;
                    return new StringToken(TextSpan.FromBounds(start, position), value.ToString(), IsRaw: false);
                }

                if (c != '\\')
                {
                    if (unpairedHighEscape is { } high && !char.IsLowSurrogate(c))
                    {
                        return RejectLoneSurrogate(high);
                    }

                    unpairedHighEscape = null;
                    value.Append(c);
                    position++;
                    continue;
                }

                var escapeStart = position;
                if (!TryScanEscape(start, out var decoded))
                {
                    return null;
                }

                var escapeSpan = TextSpan.FromBounds(escapeStart, position);
                if (decoded.Length == 1 && char.IsLowSurrogate(decoded[0]))
                {
                    var follows = unpairedHighEscape is not null
                        || (value.Length > 0 && char.IsHighSurrogate(value[^1]));
                    if (!follows)
                    {
                        return RejectLoneSurrogate(escapeSpan);
                    }

                    unpairedHighEscape = null;
                }
                else if (unpairedHighEscape is { } high)
                {
                    return RejectLoneSurrogate(high);
                }
                else if (decoded.Length == 1 && char.IsHighSurrogate(decoded[0]))
                {
                    unpairedHighEscape = escapeSpan;
                }

                value.Append(decoded);
            }
        }

        /// <summary>Reads the escape at the current backslash, leaving the position after it.</summary>
        private bool TryScanEscape(int stringStart, out string decoded)
        {
            decoded = "";
            var start = position++;
            if (position >= text.Length)
            {
                RejectUnterminatedString(stringStart);
                return false;
            }

            var letter = text[position++];
            switch (letter)
            {
                case '"': decoded = "\""; return true;
                case '\\': decoded = "\\"; return true;
                case 'n': decoded = "\n"; return true;
                case 'r': decoded = "\r"; return true;
                case 't': decoded = "\t"; return true;
                case 'x':
                    return TryScanFixedHex(start, 2, "`\\x` takes exactly two hexadecimal digits.", out decoded);
                case 'u' when At(position) == '{':
                    return TryScanBracedEscape(start, out decoded);
                case 'u':
                    return TryScanFixedHex(start, 4, "`\\u` takes exactly four hexadecimal digits, or one to six in braces.", out decoded);
                default:
                    var length = char.IsHighSurrogate(letter) && char.IsLowSurrogate(At(position)) ? 2 : 1;
                    var span = new TextSpan(start, 1 + length);
                    Fail(UnknownEscape, span, $"`{span.Of(text)}` is not an escape. To write a backslash, double it: `\\\\`.");
                    return false;
            }
        }

        private bool TryScanFixedHex(int start, int digits, string rule, out string decoded)
        {
            decoded = "";
            var hexStart = position;
            while (position - hexStart < digits && char.IsAsciiHexDigit(At(position)))
            {
                position++;
            }

            if (position - hexStart < digits)
            {
                Fail(MalformedEscape, TextSpan.FromBounds(start, position), rule);
                return false;
            }

            decoded = ((char)int.Parse(text.AsSpan(hexStart, digits), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)).ToString();
            return true;
        }

        private bool TryScanBracedEscape(int start, out string decoded)
        {
            decoded = "";
            var hexStart = ++position;
            while (char.IsAsciiHexDigit(At(position)))
            {
                position++;
            }

            var digits = position - hexStart;
            if (digits is < 1 or > 6 || At(position) != '}')
            {
                if (At(position) == '}')
                {
                    position++;
                }

                Fail(MalformedEscape, TextSpan.FromBounds(start, position),
                    "`\\u{...}` takes one to six hexadecimal digits and a closing brace.");
                return false;
            }

            var codePoint = int.Parse(text.AsSpan(hexStart, digits), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
            position++;
            var span = TextSpan.FromBounds(start, position);
            if (codePoint > 0x10FFFF)
            {
                Fail(CodePointOutOfRange, span, $"`{span.Of(text)}` is beyond U+10FFFF, the last code point.");
                return false;
            }

            if (codePoint is >= 0xD800 and <= 0xDFFF)
            {
                Fail(SurrogateCodePoint, span,
                    $"`{span.Of(text)}` names a surrogate, which is not a character. "
                    + "Write the character itself in braces, or both halves of the pair with `\\uNNNN`.");
                return false;
            }

            decoded = char.ConvertFromUtf32(codePoint);
            return true;
        }

        private Token? ScanRawString()
        {
            var start = position;
            position += 2;
            var value = new StringBuilder();
            while (true)
            {
                if (position >= text.Length)
                {
                    return RejectUnterminatedString(start);
                }

                var c = text[position++];
                if (c != '"')
                {
                    value.Append(c);
                }
                else if (At(position) == '"')
                {
                    value.Append('"');
                    position++;
                }
                else
                {
                    return new StringToken(TextSpan.FromBounds(start, position), value.ToString(), IsRaw: true);
                }
            }
        }

        private Token? RejectUnterminatedString(int start) =>
            Fail(UnterminatedString, TextSpan.FromBounds(start, text.Length), "This string has no closing quote.");

        private Token? RejectLoneSurrogate(TextSpan escape) =>
            Fail(LoneSurrogate, escape,
                $"`{escape.Of(text)}` is half of a surrogate pair, and the other half does not stand next to it.");

        // ------------------------------------------------------------------------------------------
        // Numbers, bytecounts and durations

        /// <summary>
        /// Reads every literal that can start here and keeps the longest: a number, a bytecount, or
        /// a duration in either form. Only an unsigned number takes a unit.
        /// </summary>
        private Token? ScanNumeric()
        {
            var start = position;
            var signed = text[start] is '+' or '-';
            var unsignedStart = signed ? start + 1 : start;
            var integerEnd = SkipDigits(unsignedStart);
            var numberEnd = integerEnd;
            var hasFraction = false;
            if (At(integerEnd) == '.' && char.IsAsciiDigit(At(integerEnd + 1)))
            {
                numberEnd = SkipDigits(integerEnd + 1);
                hasFraction = true;
            }
            else if (At(integerEnd) == '.' && At(integerEnd + 1) != '.')
            {
                var span = TextSpan.FromBounds(start, integerEnd + 1);
                return Fail(TrailingPeriod, span,
                    $"`{span.Of(text)}` ends in a period, which must be followed by a digit. Write `{text[start..integerEnd]}`.");
            }

            if (signed)
            {
                position = numberEnd;
                return Number(start, numberEnd);
            }

            var byteUnit = MatchByteUnit(numberEnd);
            var byteCountEnd = byteUnit is { } unit ? numberEnd + unit.Suffix.Length : start;
            var unitsEnd = hasFraction ? start : DurationUnitsEnd(start);
            var clockEnd = hasFraction ? start : ClockEnd(integerEnd);
            var longest = new[] { numberEnd, byteCountEnd, unitsEnd, clockEnd }.Max();
            position = longest;

            if (longest == numberEnd)
            {
                return Number(start, numberEnd);
            }

            if (longest == byteCountEnd)
            {
                return ByteCount(start, numberEnd, byteUnit!.Value);
            }

            return longest == unitsEnd ? DurationFromUnits(start, unitsEnd) : DurationFromClock(start, integerEnd, clockEnd);
        }

        private Token? Number(int start, int end)
        {
            var span = TextSpan.FromBounds(start, end);
            return NumberText.TryParse(span.Of(text), out var value)
                ? new NumberToken(span, value)
                : RejectUnrepresentable(span);
        }

        private (string Suffix, BigInteger Bytes)? MatchByteUnit(int at)
        {
            foreach (var unit in ByteUnits)
            {
                if (text.AsSpan(at).StartsWith(unit.Suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return unit;
                }
            }

            return null;
        }

        private Token? ByteCount(int start, int numberEnd, (string Suffix, BigInteger Bytes) unit)
        {
            var span = TextSpan.FromBounds(start, numberEnd + unit.Suffix.Length);
            return NumberText.TryParse(text.AsSpan(start, numberEnd - start), out var count)
                && ExactDecimal.TryMultiply(count, unit.Bytes, out var bytes)
                    ? new ByteCountToken(span, new ByteCount(bytes))
                    : RejectUnrepresentable(span);
        }

        /// <summary>Where <c>[#h][#m][#s]</c> starting here ends; <paramref name="start"/> if it does not.</summary>
        private int DurationUnitsEnd(int start)
        {
            var end = start;
            var next = 0;
            while (next < DurationUnits.Length)
            {
                var digitsEnd = SkipDigits(end);
                if (digitsEnd == end)
                {
                    break;
                }

                var unit = Array.FindIndex(DurationUnits, next, u => char.ToLowerInvariant(At(digitsEnd)) == u.Unit);
                if (unit < 0)
                {
                    break;
                }

                end = digitsEnd + 1;
                next = unit + 1;
            }

            return end;
        }

        private Token? DurationFromUnits(int start, int end)
        {
            var seconds = BigInteger.Zero;
            var fieldStart = start;
            for (var i = start; i < end; i++)
            {
                if (char.IsAsciiDigit(text[i]))
                {
                    continue;
                }

                var unit = DurationUnits.First(u => u.Unit == char.ToLowerInvariant(text[i]));
                seconds += BigInteger.Parse(text.AsSpan(fieldStart, i - fieldStart), CultureInfo.InvariantCulture) * unit.Seconds;
                fieldStart = i + 1;
            }

            return Duration(TextSpan.FromBounds(start, end), seconds);
        }

        /// <summary>Where <c>#:##</c> or <c>#:##:##</c> after these leading digits ends; the start if it does not.</summary>
        private int ClockEnd(int integerEnd)
        {
            var end = integerEnd;
            for (var field = 0; field < 2 && IsClockField(end); field++)
            {
                end += 3;
            }

            return end;
        }

        private bool IsClockField(int colon) =>
            At(colon) == ':' && char.IsAsciiDigit(At(colon + 1)) && char.IsAsciiDigit(At(colon + 2));

        private Token? DurationFromClock(int start, int integerEnd, int end)
        {
            var seconds = BigInteger.Parse(text.AsSpan(start, integerEnd - start), CultureInfo.InvariantCulture);
            for (var colon = integerEnd; colon < end; colon += 3)
            {
                var field = int.Parse(text.AsSpan(colon + 1, 2), CultureInfo.InvariantCulture);
                if (field > 59)
                {
                    var span = new TextSpan(colon + 1, 2);
                    return Fail(ClockFieldOutOfRange, span,
                        $"`{span.Of(text)}` is out of range: every field after the first runs from 00 to 59.");
                }

                seconds = seconds * 60 + field;
            }

            return Duration(TextSpan.FromBounds(start, end), seconds);
        }

        private Token? Duration(TextSpan span, BigInteger seconds) =>
            ExactDecimal.TryFromInteger(seconds, out var value)
                ? new DurationToken(span, new Duration(value))
                : RejectUnrepresentable(span);

        private Token? RejectUnrepresentable(TextSpan span) =>
            Fail(UnrepresentableLiteral, span,
                $"`{span.Of(text)}` cannot be held exactly: it is too large or has too many significant digits.");

        /// <summary>
        /// A colon that is not half of <c>::</c>. Straight after a number it is a clock-form
        /// duration gone wrong, which is worth saying.
        /// </summary>
        private Token? StrayColon()
        {
            if (tokens.Count == 0 || tokens[^1] is not { Kind: TokenKind.Number or TokenKind.Duration } previous
                || previous.Span.End != position)
            {
                return RejectUnexpectedCharacter();
            }

            var end = position;
            while (At(end) == ':' || char.IsAsciiDigit(At(end)))
            {
                end++;
            }

            var span = TextSpan.FromBounds(previous.Span.Start, end);
            if (text[previous.Span.Start] is '+' or '-' && IsClockField(position))
            {
                return Fail(SignedUnitLiteral, span, $"`{span.Of(text)}` has a sign, but a duration cannot be negative.");
            }

            return Fail(MalformedLiteral, span,
                $"`{span.Of(text)}` is not a duration: a clock-form duration has two digits after each colon, "
                + "and at most two colons.");
        }

        private Token? RejectUnexpectedCharacter()
        {
            var length = char.IsHighSurrogate(text[position]) && char.IsLowSurrogate(At(position + 1)) ? 2 : 1;
            var span = new TextSpan(position, length);
            return Fail(UnexpectedCharacter, span, $"`{span.Of(text)}` cannot appear here, outside a string.");
        }

        // ------------------------------------------------------------------------------------------

        private char At(int index) => index < text.Length ? text[index] : '\0';

        private int SkipDigits(int index)
        {
            while (char.IsAsciiDigit(At(index)))
            {
                index++;
            }

            return index;
        }

        private static bool IsAsciiWordChar(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

        /// <summary>The length of a non-ASCII letter, digit or mark starting here, or 0.</summary>
        private int NonAsciiWordLength(int index)
        {
            if (index >= text.Length || char.IsAscii(text[index])
                || Rune.DecodeFromUtf16(text.AsSpan(index), out var rune, out var length) != System.Buffers.OperationStatus.Done)
            {
                return 0;
            }

            return Rune.IsLetterOrDigit(rune) || Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark
                ? length
                : 0;
        }

        private Token? Fail(string code, TextSpan span, string message)
        {
            error = new Diagnostic(code, span, message);
            return null;
        }
    }
}
