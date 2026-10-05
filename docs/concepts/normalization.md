# Normalization

## NAME

normalization - how string values are prepared before an operator compares them

## DESCRIPTION

> **Not vetted by an expert.** The rules in this document were worked out from the Unicode
> Standard and from what established libraries do, checked against a set of smoke tests, and
> benchmarked, but they have not been reviewed by a human expert in Unicode text processing. Treat
> them as a careful first version rather than settled practice.

### Overview

Every operator that compares strings -- the comparison operators, `BETWEEN` and `=~` -- prepares
them first, in one of two modes. In **normalized** mode, the default, differences of case, of
accents on Latin and Greek letters, and of a few kinds of character form stop mattering, so that
`artist == "motorhead"` matches "Motörhead". In **literal** mode, chosen with `LITERALLY()`, strings
are taken as recorded, apart from differences of encoding that are invisible on the page. How an
operator's mode is chosen is described under [Comparison modes](predicates.md#comparison-modes).

Once prepared, two strings are equal when they consist of the same code points, and they are
ordered by comparing their code points one at a time, a string that is a prefix of another sorting
first.

### Normalized mode

A string is prepared in normalized mode by the following steps, in this order.

1. **Malformed text is repaired.** A UTF-16 surrogate that is not part of a pair is replaced with
   U+FFFD REPLACEMENT CHARACTER.
2. **Compatibility forms are decomposed.** The string is put in Unicode normalization form NFKD.
   This folds fullwidth and halfwidth forms, ligatures, superscripts, subscripts and similar
   presentation variants: `ＹＭＯ` becomes `YMO`, `ﬁ` becomes `fi`, `²` becomes `2`, `½` becomes
   `1⁄2` and `™` becomes `TM`.
3. **Default-ignorable characters are removed.** Every code point with the Unicode property
   `Default_Ignorable_Code_Point` is removed, among them the soft hyphen, zero-width spaces and
   joiners, the byte order mark and variation selectors.
4. **Accents are removed from Latin and Greek letters.** A combining mark (general category `Mn`,
   `Mc` or `Me`) is removed when the nearest character before it that is not itself a combining
   mark lies in one of these ranges: U+0041–005A, U+0061–007A, U+00C0–02AF, U+0370–03FF,
   U+1D00–1DBF, U+1E00–1FFF, U+2C60–2C7F, U+A720–A7FF and U+AB30–AB6F. In addition, U+0308
   COMBINING DIAERESIS is removed when that nearest character is Cyrillic `е` or `Е`, so that `ё`
   matches `е`. Every other
   combining mark is kept: kana voicing marks, Thai and Indic vowel signs and tone marks, Hebrew
   and Arabic points, and the marks that make Cyrillic `й`, `ї` and `ў` letters of their own.
5. **Case is folded.** Each character is replaced by the lowercase form of its uppercase form,
   using the simple, one-to-one Unicode case mappings and no language-specific rules. So
   `ΟΔΥΣΣΕΥΣ` and `οδυσσευς` both become `οδυσσευσ`, and dotless `ı` becomes `i`.
6. **Some letters with no decomposition are folded.** These letters carry no combining mark for
   step 4 to remove, and are replaced as follows:

   | Letter | Becomes |   | Letter | Becomes |
   |:------:|:-------:|---|:------:|:-------:|
   | `ø`    | `o`     |   | `đ`    | `d`     |
   | `æ`    | `ae`    |   | `ð`    | `d`     |
   | `œ`    | `oe`    |   | `þ`    | `th`    |
   | `ß`    | `ss`    |   | `ħ`    | `h`     |
   | `ł`    | `l`     |   |        |         |

   Their uppercase forms are folded the same way, having been lowercased by step 5.
7. **The result is recomposed.** The string is put in Unicode normalization form NFC, which joins
   what step 2 took apart wherever a mark was kept, such as Hangul syllables and voiced kana.

Some examples of strings that are equal in normalized mode:

| These compare equal | Because of step |
|---|---|
| `Motörhead`, `MOTORHEAD`, `motorhead` | 4 and 5 |
| `Ørsted`, `orsted` | 6 |
| `Straße`, `strasse` | 6 |
| `Đội`, `doi` | 4 and 6 |
| `Ёлка`, `елка` | 4 |
| `ＹＭＯ`, `ymo` | 2 and 5 |
| `ｶﾞﾝﾀﾞﾑ`, `ガンダム` | 2 and 7 |
| `Radio­head` (with a soft hyphen), `radiohead` | 3 |

And some that are not:

| These remain different | Because |
|---|---|
| `ガンダム`, `カンタム` | kana voicing marks are kept |
| `Йога`, `иога` | the breve of `й` is kept |
| `कुमार`, `कमार` | Devanagari vowel signs are kept |
| `שָׁלוֹם`, `שלום` | Hebrew points are kept |

### Literal mode

A string is prepared in literal mode by step 1 above, and then put in normalization form NFC.
Nothing else is changed: case, accents, compatibility forms and default-ignorable characters all
remain. Two strings that differ only in whether an accented letter is stored as a single character
or as a letter followed by a combining mark are equal in literal mode.

### Regular expressions

The subject of `=~` is prepared exactly as any other operand is, in whichever mode the operator is
in. The pattern is never prepared. In normalized mode the match is case-insensitive, using
invariant rules; in literal mode it is case-sensitive.

A pattern therefore cannot match anything normalization removes or changes in the subject. In
normalized mode `motö` and `straße` match nothing, while `mot` and `strasse` match "Motörhead" and
"Straße"; and since the prepared subject is lowercase, a pattern such as `\p{Lu}` matches only in
literal mode.

### Literals

A string literal whose escapes leave a surrogate that is not part of a pair is an error, reported
when the predicate is read. Step 1 above applies only to data read from files.

## SEE ALSO

[Predicates](predicates.md), and the [rationale](../design/rationale.md) for why normalization is
defined this way.
