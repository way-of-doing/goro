# Predicates

## NAME

predicate - filtering expression accepted by commands as argument

## SYNOPSIS

*command* --filter=*predicate*

## DESCRIPTION

### Overview

Some Goro commands have optional arguments that can be used to configure the scope of the command. This is achieved by writing _predicates_: conditions that determine if a file is in scope for processing based on its properties, tags, and other associated data. For example, `artist == "metallica"` is a predicate satisfied by files where the artist tag matches "metallica". In this predicate, `artist` and `"metallica"` are _values_ being compared by the _operator_ `==`, which determines if they are equal. A predicate as a whole must be an expression of type boolean: a file is in scope for processing when the predicate evaluates to true for it, and out of scope otherwise. A bare value such as `artist` is therefore not a valid predicate.

A predicate is supplied to a command as a single command line argument. Because predicate syntax uses double quotes to delimit string values, that argument normally has to be wrapped in single quotes so that the shell passes it through intact; see [goro list](../commands/list.md) for a worked example.

The rest of this section describes the syntax for predicates, and the various operators and other constructs that can appear inside them.

### Whitespace

The amount and type of whitespace characters between tokens is ignored when reading predicates. However, whitespace is still required to separate tokens which are grammatically distinct, and of course whitespace is entirely preserved within string values (so the values `"foo"` and `"foo "` are not equal).

### Case sensitivity

Predicate syntax is case-insensitive throughout, using invariant culture rules. The keyword operators (`AND`, `OR`, `NOT`, `BETWEEN`), the reserved words `NULL`, `TRUE` and `FALSE`, function names, identifiers, namespaces, and the unit suffixes of bytecount and duration literals are all matched without regard to case. `artist`, `Artist` and `ARTIST` are the same identifier, and similarly `id3v2::tit2` and `ID3V2::TIT2` are the same identifier.

The only case-sensitive text in a predicate is the contents of a string value -- and even those are converted to lowercase before being compared, unless `LITERALLY()` is used to prevent it. A quoted part of an identifier is not a string value in this sense and is matched without regard to case like any other part of an identifier, so `ape::"album artist"` and `ape::"Album Artist"` are the same identifier.

### Identifiers and namespaces

Identifiers are unquoted sequences that start with an ASCII letter and may contain ASCII letters, digits, and underscores. For example, `artist` and `year` are both simple identifiers. Characters outside this set, including accented letters and any other non-ASCII character, may not appear in an identifier; encountering one is a syntax error rather than a reference to an undefined identifier.

Each identifier may also be preceded by a _namespace_. Namespaces follow the same rules as identifiers: they start with an ASCII letter and may contain ASCII letters, digits, and underscores. Namespaces are part of the identifier, and are separated from it by a double colon. For example, `id3v2::TIT2` is a _qualified_ identifier where `id3v2` is the namespace.

In actuality, all identifiers have a namespace. If the namespace is not explicitly provided (i.e. no double colon in the identifier), the identifier is _unqualified_ and considered to be inside the _global namespace_. The global namespace can also be explicitly specified by using `::` without a namespace in front. For example, `artist` is an unqualified identifier implicitly in the global namespace, `::artist` is a functionally equivalent (but qualified) identifier but with the namespace explicitly provided, and `id3v1::artist` is a qualified identifier in the namespace `id3v1`.

Namespaces can also be hierarchically nested. For example, `foo::bar::baz` is a qualified identifier referring to `baz` in the namespace `foo::bar`.

Some of the names an identifier has to reach are not Goro's to choose. An [open namespace](../features/builtins/identifiers.md) derives the name it looks for from the identifier itself, and tag formats allow names that the rules above cannot spell -- an APE item key may contain spaces, as `Album Artist` does, and other formats permit further punctuation. For these, a part of an identifier may instead be written **as a quoted string**, in which case its contents are the name and the restrictions on identifier characters do not apply:

```
ape::"album artist" == "Various Artists"
```

A quoted part must be preceded by `::`, so the first part of an identifier is never quoted; a string at the start of an expression is always a string value. Quoting is available wherever it is useful rather than only where it is necessary, so `ape::"artist"` is permitted and means exactly the same as `ape::artist`: the two are one identifier written two ways, not two identifiers. Escapes inside a quoted part follow the same rules as in any other string, so a name containing a double quote or a backslash can be written by preceding it with a backslash.

For a list of predefined namespaces and identifiers within them together with a description of each one, see [built-in identifiers](../features/builtins/identifiers.md). A namespace is either _closed_, meaning that the identifiers it contains are known in advance, or _open_, meaning that it additionally admits identifiers whose names are derived from the tag data of the file being examined. Referring to an identifier that is not defined in a closed namespace is an error; an identifier in an open namespace is always accepted, and simply resolves to null when the file holds no data for it.

### Reserved words

The keyword operators `AND`, `OR`, `NOT`, and `BETWEEN` (case-insensitive) are reserved and cannot be used as a bare identifier in the global namespace. For example, `and` alone is always interpreted as the logical `AND` operator, never as an identifier named "and".

The keywords `NULL`, `TRUE`, and `FALSE` (case-insensitive) are also reserved and cannot be used as a bare identifier in the global namespace.

This restriction applies only to bare, unqualified identifiers. If an identifier happens to coincide with a keyword, it can still be referenced by explicitly qualifying it, e.g. `::and`.

The modifiers `ALL`, `ANY`, and `LITERALLY` (case-insensitive) are reserved on the same terms. They are reserved because, unlike a function, a modifier may only appear in one specific position, and recognizing the name is what allows a misplaced modifier to be reported as such instead of as a call to an undefined function.

Function names (such as `COUNT`, `FALLBACK`, `ISNULL`, `NUMBER`, and `STRING`) are not reserved words in this sense.

Although the grammar does not require it, no built-in identifier in the global namespace has the same name as a function, and no function is given the name of a built-in global identifier. This is a constraint on what Goro defines rather than on what a predicate may write.

### Types and values

Values in predicates come from _identifiers_ (preagreed names to refer to various types of data specific to each file) and _literals_ (values specified directly when writing a predicate). For example, in the predicate `year == 2000`, `year` is an identifier and `2000` is a literal value.

Each value has a _type_, which dictates what operations are allowed to be performed on it and how their results should be calculated. The types that Goro recognizes are:

**null**
: represents the absence of a value, or the inability to interpret one. These two cases are distinguished: a value that is simply absent is a plain null, while a value that is present but cannot be interpreted as the type it is supposed to have is a _tainted_ null, which behaves identically in every respect but causes a warning to be emitted. **Identifiers that refer to tag information resolve to a plain null when there is no such tag in the file, and to a tainted null when the tag is present but its contents cannot be interpreted as the identifier's type.** For example, all identifiers in the `id3v1` namespace resolve to plain null for a file that has no Id3v1 tags, whereas `id3v1::year` resolves to a tainted null for a file whose Id3v1 tag is present but holds something other than a year in its year field. The expression `NUMBER(title)` is likewise very probable to evaluate to a tainted null, because if `title` is present it is very unlikely to hold a value that is a valid number. Note that there is no way to write a null literal in expressions; null is only ever produced by evaluating sub-expressions such as identifiers and functions.

**boolean**
: represents a binary logical state (true or false). There is no way to write boolean literals in expressions; boolean values are produced by the various comparison and logical operators described later in this document. Boolean values may only appear as operands of the logical operators `AND`, `OR`, and `NOT`, and as the result of a predicate as a whole. It is an error to use a boolean value anywhere else, in particular as an operand of a comparison, range, or regex operator, or as an argument to any function.

**string**
: arbitrary alphanumeric sequences within double quotes, such as `"metallica"`. Strings can also contain double quotes, which need to be escaped within the string by preceding them with a backslash character. For example, `"qu\"ote"` is a string that represents the value `qu"ote`, with the double quote in the middle appearing escaped as `\"` within the literal. Strings can also contain backslashes, which need to be escaped in the same manner.

**number**
: real dimensionless numbers, such as `2000`, `-1`, and `-.55`. Number literals are optionally preceded by one + or - sign character, followed by either an integer or a floating point number where the integer part is separated from the fractional part by a period. If the number has a fractional part, the integer part is optional and considered to be zero when it does not appear. The period must always be followed by at least one digit, so `1`, `1.5` and `.5` are all valid number literals while `1.` is not.

**bytecount**
: a numeric value that also has a unit and represents a count of bytes. A bytecount literal is an unsigned number followed immediately by a unit suffix, with no whitespace between them. The available suffixes are, in SI (powers of 10) and IEC (powers of 2) form respectively:

| SI suffix | Value       | IEC suffix | Value      |
|-----------|-------------|------------|------------|
| `b`       | 1 byte      |            |            |
| `kb`      | 10³ bytes   | `kib`      | 2¹⁰ bytes  |
| `mb`      | 10⁶ bytes   | `mib`      | 2²⁰ bytes  |
| `gb`      | 10⁹ bytes   | `gib`      | 2³⁰ bytes  |
| `tb`      | 10¹² bytes  | `tib`      | 2⁴⁰ bytes  |

As everywhere else in predicate syntax, suffixes are case-insensitive: `KB`, `kb` and `Kb` are all the same unit. What distinguishes an IEC suffix from an SI one is the presence of the letter `i`, not the case of the letters. There are no units for bits, so `mb` always means megabytes.

The number may have a fractional part, in which case the literal denotes exactly the value implied and no rounding takes place: `1.4kib` is exactly 1433.6 bytes. Since a file's size is always a whole number of bytes, comparisons against such a literal are still exact and well defined, although an equality comparison against a fractional bytecount will never be true.

For example, `1.4mb`, `100b` and `10KiB` are all valid bytecount literals.

**duration**
: a numeric value that also has a unit and represents a duration in time, measured in **whole seconds**. Duration values never carry a sub-second component: a duration obtained from a file, such as `file::duration`, is truncated towards zero to a whole number of seconds when it is produced, so a file playing for 4 minutes and 5.7 seconds has a `file::duration` of exactly `4m5s`. Truncation rather than rounding is used, matching the way playing times are conventionally displayed. Because the precision of a duration value is the same as the precision the literal syntax can express, durations can be meaningfully compared for equality.

Valid duration literals can take two forms:
- `[#h][#m][#s]`, where hash signs `#` represent some nonnegative integer and the letters `h`, `m` and `s` are case-insensitive literals representing hours, minutes, and seconds respectively. At least one of the unit specifiers must be present, and those that do appear must appear in the order given above. For example, `1h10m`, `1h1s`, and `0s` are all valid duration literals.
- `[#:]##:##`, where the single hash sign `#` represents a nonnegative integer and the double hash signs `##` represent a nonnegative integer in the range 0 to 59, inclusive. Numbers represented by double hash signs must always be written as two digits, so if less than 10 must be prefixed by a zero. For example, `250:00:00`, `01:59`, and `00:00` are all valid duration literals.

Whitespace may not appear inside a duration literal: `1h10m` is a valid literal and `1h 10m` is not.

There are also two concepts closely related to value types, which are however not types themselves:

**ranges**
: ranges are always specified as literals, and represent a range of values that can be used in comparisons. Ranges have a type like any other value, so there are numeric ranges, string ranges, duration ranges, etc. Valid range literals take the form `min..max` where `min` and `max` must be literals of the same type. For example, `1..100` is a numeric range from 1 to 100 (inclusive) and `10kb..10mb` is a bytecount range.

Attempting to create a range such that `min > max` is an error.

**multivalues**
: multivalues represent the simultaneous existence of several values where a single value might otherwise be expected. Multivalues cannot be specified as literals, but they can arise when the same kind of data is present multiple times within tags. For example, the identifier `artist` might resolve to null (if there is no artist tag of any kind), a simple string value (if there is exactly one artist tag; the value could be the empty string if that's what the tag records), or a multivalue, e.g. if there are multiple Vorbis comments with the `Artist` key.

The concept of multivalues is intended to enable clear and precise explanations of how Goro behaves in the presence of multiple tags that provide the same data, and is presented in more detail in its own section below.

### Operators

Values within Goro predicates can be compared with the _comparison operators_ `==` (equality), `!=` (inequality), `<` (less than), `>` (greater than), `<=` (less than or equal), and `>=` (greater than or equal). There are also _boolean_ or _logical operators_ that appear as words instead of symbols: `NOT`, `AND`, and `OR`.

#### General operator rules

There are some **fundamental rules which apply globally** to any operator sub-expression:

1. If any input to a comparison, range, or regex operator is the null value, the operator evaluates to false regardless of what other inputs to the operator might be. For example, if there is no Id3v2 tag, the predicates `id3v2::track == 5` and `id3v2::track != 5` will _both_ evaluate to false, because `id3v2::track` is a null-valued input to the operators in both cases. This rule does not concern the logical operators `AND`, `OR`, and `NOT`, whose operands are always boolean and therefore never null.
2. If an operator has multiple operands, it is an error for the operands to have different types. For example, `file::duration > "01:00"` is an error because `file::duration` has duration type and `"01:00"` is a literal of type string. Instead, `file::duration > 01:00` is correct because `01:00` without quotes is a valid duration literal as described earlier.
3. There is a very important exception to the previous rule: _numeric literals (but not non-literal numeric values!) are polymorphic and can be compared with any type_. In particular, they are automatically converted to the correct type (that of the other operand): for duration type, the converted value is a duration of the given number of seconds, which may have a fractional part even though durations obtained from files never do, and the comparison is then exact -- `file::duration >= 1.5` is false for a file of one second and true for a file of two; for bytecount type it represents the given number of bytes; and for string, the converted value is the decimal string representation of the number. This is also true for range literals; `file::size BETWEEN 60..120` compares `file::size` (a bytecount) with `60..120` (a range of numbers, not bytecounts), but the number range literal is automatically interpreted as a bytecount range literal of 60 to 120 bytes.
4. When working with string values, all operators _normalize_ the values before checking for a match by default. Normalization entails a) **removal of all diacritics**: the value is decomposed into Unicode NFD form and all diacritic mark characters are discarded; and b) **case insensitivity**, which for all operators except `~=` is achieved by converting the value to lowercase using invariant culture rules. The regex matching operator `~=` is the exception: case-folding a regular expression would silently rewrite it, so `~=` leaves the case of both operands alone and performs the match itself case-insensitively instead (see below). For example, `artist == "motorhead"` would match when the artist is recorded as "Motörhead", despite the difference in casing and diacritics. This normalization can be disabled on a per-case basis by using the `LITERALLY()` modifier (see below).

#### Comparison operators

The comparison operators `==`, `!=`, `<`, `<=`, `>`, `>=` directly compare their operands. Comparison between values of type number, bytecount, and duration are trivial; comparison between values of type string is by default _an ordinal Unicode comparison_ that is preceded by _normalization_ of the string values as detailed above (unless the `LITERALLY()` modifier is used to avoid this); comparison between null and any other value results in false.

#### Range operator

The range operator `BETWEEN` can be used to perform a range inclusion check; it determines if its first operand is part of the (inclusive) range defined by the second operand, for example `year BETWEEN 1990..2000`. When testing a value `v` against a range, the value is considered to be part of the range if and only if `min <= v` and `v <= max` are both true. If the value and range are strings, comparisons follow the same rules as for the simple comparison operators.

Examples:

- `year BETWEEN 2000..2010` matches if the year is between 2000 and 2010, inclusive.
- `artist BETWEEN "ma".."mi"` matches if the artist would be ordinally sorted somewhere between "ma" and "mi". It would match "Metallica", given that matching against strings is case-insensitive by default due to normalization and "me" sorts between "ma" and "mi".

#### Regular expression match operator

String values can be tested to see if they match a regular expression with the regex matching operator `~=`. The left operand is the value being tested and the right operand is the regular expression. Both operands must be of type string, and it is an error if either is not; the polymorphic numeric literal rule (general operator rule 3) does not apply to this operator. Regular expressions are [.NET-flavored](https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expressions).

The regex matching operator will match if any substring of the tested value matches the regular expression, so if more exact matching is intended the anchors `^` and/or `$` have to be specified.

Both operands are normalized by default, but this operator handles the case-insensitivity half of normalization differently from every other operator: rather than converting the operands to lowercase, which would silently rewrite the pattern, only diacritic removal is applied to the operands and the match itself is performed case-insensitively using invariant culture rules. As a consequence, unless `LITERALLY()` is used to suppress normalization, a pattern cannot express a case-sensitive condition and cannot match a diacritic.

If the pattern operand is a string literal, it is validated when the predicate is read, and an invalid pattern is reported as an error before any file is processed. If the pattern operand is not a literal — for example, it comes from a tag — it cannot be validated in advance; if it turns out not to be a valid regular expression when it is evaluated, the operator evaluates to false and a warning is emitted, following the same policy as warnings for tainted null values (see [Value conversions](#value-conversions)).

Regular expression matching is always subject to an implementation-defined timeout, applied to each individual match attempt. If no result is available within the (generous) allowed time limit, the operator evaluates to false and a warning is emitted, exactly as for an invalid pattern. Note that this makes the result of a predicate containing `~=` potentially dependent on the machine it runs on and on system load; the warning is what surfaces that this has happened.

If either operand is a multivalue, matching follows the ordinary quantifier and nested iteration rules described under [Multivalues](#multivalues); each individual pattern of a multivalue pattern operand is validated independently.

There is no negated version of this operator; to determine if a value does not match a regular expression, apply `NOT` to the result of matching it.

**IMPORTANT** Note that an invalid pattern making the operator evaluate to false has the same consequence under negation as a null operand does: `NOT (artist ~= sometag)` evaluates to true when `sometag` is not a valid regular expression, so a file can be selected on the strength of a failed match. The warning is what surfaces this; see the discussion of `!=` versus `NOT ==` above for the same underlying phenomenon.

Example: `artist ~= "s$"` matches any artist whose name ends in "s" or "S"

#### Boolean operators

An expression inside a predicate can be negated with the `NOT` operator. For example, `year < 2000` and `NOT year >= 2000` are logically and functionally equivalent when `year` is not null.

**IMPORTANT** This equivalence does _not_ hold if `year` evaluates to null. Because the null operand makes every comparison operator evaluate to false regardless of which operator it is (see general operator rules above), `year < 2000` evaluates to false, while `NOT year >= 2000` evaluates to `NOT false`, i.e. true. This is the same underlying phenomenon noted below for `!=` and `NOT ==` on multivalues, and it applies to any comparison operator and its logical complement whenever a null operand is involved, not just `!=`/`==`.

Two expressions can be combined with the `AND` and `OR` logical operators. For example, `year < 2000 AND artist == "metallica"`.

The `AND` and `OR` operators are _short-circuiting_: their operands will always be evaluated in the (left-to-right) order of appearance in the expression, and the second operand will only be evaluated if the result of the operator cannot be determined after having evaluated the first operand. For example, in the expression `genre == "metal" and year between 1970..1980`, if `genre == "metal"` evaluates to false then the sub-expression `year between 1970..1980` will not be evaluated at all because we already know the operator's result will be false.

This short-circuiting behavior is intended to allow the function `ISNULL()` to be used for checking if a value is null (the comparison operators would always result in false if given null as an operand) before trying to use it and potentially triggering a warning; refer to the function for more details.

The operands of `AND`, `OR`, and `NOT` must be of type boolean, and it is an error to apply them to a value of any other type. Since booleans are only ever produced by the comparison, range, and regex operators and by the logical operators themselves, in practice this means their operands must be comparisons or other logical expressions, optionally parenthesized.

#### Grouping, precedence, and associativity

Expressions can be grouped with parentheses ( ). A parenthesized sub-expression is evaluated as a unit before anything outside the parentheses, regardless of what operators surround it. For example, `(genre == "jazz" OR genre == "blues") AND year < 2000` first evaluates the genre comparison, then combines the result with the year comparison.

When parentheses are not used to make evaluation order explicit, operators are evaluated according to a fixed precedence, from tightest-binding to loosest-binding:

| Level        | Constructs                                                                            | Associativity   |
|:------------:|---------------------------------------------------------------------------------------|-----------------|
| 1 (tightest) | primary expressions: literals, identifiers, function calls, parenthesized expressions | --
| 2            | `==` `!=` `<` `>` `<=` `>=` `~=` `BETWEEN`                                            | non-associative
| 3            | `NOT`                                                                                 | unary, repeatable
| 4            | `AND`                                                                                 | left
| 5 (loosest)  | `OR`                                                                                  | left

An operator of higher precedence binds more tightly than one of lower precedence, meaning it is evaluated first. `NOT` binds more tightly than `AND`, which in turn binds more tightly than `OR`. For example, `artist == "metallica" AND year > 2000 OR NOT genre == "jazz"` is evaluated as if it had been written `((artist == "metallica") AND (year > 2000)) OR (NOT (genre == "jazz"))`, since `AND` binds more tightly than `OR`, `NOT` binds more tightly than `AND`, and the comparisons bind more tightly than `NOT`.

When a left-associative operator appears more than once in a chain without parentheses, it is evaluated left to right. For example, `a AND b AND c` is evaluated as `(a AND b) AND c`. This does not change the result for `AND` or `OR` chains, but it does determine the order in which operands are evaluated. `NOT` is a unary prefix operator and may be applied repeatedly, so `NOT NOT a` is valid and equivalent to `a`.

The comparison operators, the regex operator `~=`, and the range operator `BETWEEN` are non-associative and cannot be chained. `a == b == c` is an error rather than being read as `(a == b) == c`, because the result of the first comparison is a boolean and booleans are not valid operands of a comparison. A condition of that kind must be written with an explicit logical operator, as in `a == b AND b == c`.

Parentheses should be used whenever the default precedence might not match the reader's expectation, even if they are not strictly required to produce the intended result.

### Multivalues

Some identifiers do not correspond to a single value because the underlying data source allows defining a value multiple times (e.g. Vorbis comments). In such cases, an identifier logically corresponds to a bag of values instead of a single one: an unordered collection whose values are not deduplicated, so the same value occurring twice in the underlying tags occurs twice in the multivalue. As with single values that have a type but might also be null, multivalues have a type and each of the values they represent is a value of that type, or null. It is even possible for _all_ values inside a multivalue to be null.

When an operand is multivalue, it is evaluated according to a _quantifier_: existential (the default behavior) or universal (obtained by wrapping the operand in `ALL()`, see below). An operand's quantifier applies independently of any other operand's -- for example, in a comparison of two multivalues, one operand can be evaluated existentially while the other is evaluated universally.

When one or more operands are multivalue, the operator's result is calculated by nested iteration over the multivalue operands' possible values, with the operator evaluated once for every combination of values reached this way. At each level of iteration, an existentially-quantified operand causes the result to be true as soon as any one of its values leads to a true result; a universally-quantified operand requires every one of its values to lead to a true result.

The loops are nested **by quantifier, not by position**: a universally-quantified operand always forms a loop outside any existentially-quantified one. Where two operands carry the same quantifier the nesting between them is immaterial to the result, so the order in which the operands were written never affects the result of an operator. Should an operator ever take more than two value operands, the same rule applies: universals outermost, existentials innermost, with the written order breaking ties among operands of the same quantifier.

A null among a multivalue's values participates in this iteration like any other value, with general operator rule 1 applying to it: every combination that includes it makes the operator false for that combination. Under the default existential quantifier a null value therefore simply fails to contribute a match, whereas under `ALL()` a single null value is enough to make the operator false overall. For example, if genre is a multivalue of "rock" and null, `genre == "rock"` is true while `ALL(genre) == "rock"` is false.

Examples:

- `genre == "metal"` matches when there are both "metal" and "rock" genre tags present, because `genre` is (by default) existentially quantified and at least one tag is equal to "metal"
- `genre != "metal"` _also_ matches when there are both "metal" and "rock" genre tags present, because at least one tag is _not_ equal to "metal"
- `ALL(genre) == "metal"` does _not_ match in the same scenario, because not all of the values match "metal" once `genre` is universally quantified
- `genre BETWEEN "a".."b"` matches when any of the genre tags present sorts between "a" and "b"

The regular expression operator also works transparently with multivalues in the same way: it matches when _any_ of the multiple values match the regular expression, by default.

**IMPORTANT** This behavior means that the example expressions `genre != "metal"` and `NOT genre == "metal"`, which are strictly complementary if `genre` is a simple value, are no longer complementary if it is a multivalue.

Examples:

- `genre != "metal"` _does_ match when there are both "metal" and "rock" genre tags present, because at least one tag is _not_ equal to "metal"
- `NOT genre == "metal"` _does not_ match when there are both "metal" and "rock" genre tags present, because `genre == "metal"` matches and the negation operator inverts the match

#### Comparisons between two multivalues

When **both** operands to an operator are multivalue, the quantifier carried by each one determines how the two are combined. In all three cases the operands are interchangeable, since nesting follows the quantifiers rather than the order the operands were written in.

If **neither** operand is wrapped in `ALL()` -- the default, existential case for both -- the result is true if and only if the operator holds for at least one pair of values, one taken from each operand. So when `a` and `b` are both multivalues, `a < b` is true if there is some value `x` in `a` and some value `y` in `b` for which `x < y`.

For example, suppose `a` is a multivalue of `1` and `2`, and `b` is a multivalue of `2` and `3`:

- `a == b` is true, because the equality holds for at least one pair (specifically, exactly one: `2` matches `2`)
- `a != b` is also true, because the inequality holds for several pairs (e.g. `1` does not match `2`, `2` does not match `3`)

If **both** operands are wrapped in `ALL()`, the result is true if and only if the operator holds for _every_ pair of values. This is well-defined but often degenerate: `ALL(a) == ALL(b)` can only be true if every value in `a` and every value in `b` are all the exact same value.

If **exactly one** operand is wrapped in `ALL()`, that operand forms the outer loop whichever side it was written on. The result is true if and only if, for _every_ value of the universally-quantified operand, there is _some_ value of the other operand for which the operator holds.

For example, suppose `a` is a multivalue of `1` and `2`, and `b` is a multivalue of `1`, `2` and `3`:

- `ALL(a) == b` is true: for every value in `a` there is a matching value in `b`
- `b == ALL(a)` is true as well, since it says exactly the same thing
- `ALL(b) == a` is false: `3` has no match in `a`

For the equality operator this reads naturally as a containment test: `ALL(a) == b` is true precisely when every value of `a` also occurs in `b`.

### Value conversions

With the exception of numeric literals and null, operators that have multiple operands require the operands to have the same type, and produce an error if not. This means that a comparison such as `track > id3v2::raw::XXXX`, which potentially attempts to compare a number with a string, is rejected with an error. To prevent this error a conversion of one of the values to the type of the other is required. Converting `track` to a string might result in a comparison such as `"2" > "10"` -- this comparison result is, perhaps surprisingly, true, and it would be correct to instead write `track > NUMBER(id3v2::raw::XXXX)` to avoid this problem and compare numerically. However, `id3v2::raw::XXXX` might not be a valid numeric string value, and in that case `NUMBER()` would return null and the comparison would always evaluate to false because it compares a number to null. If this happens due to e.g. an erroneous tag, it could result in Goro silently skipping processing of files without providing any notification.

#### Tainted null values

_Tainted null_ values arise in two ways: from an identifier whose underlying tag data is present but cannot be interpreted as the identifier's declared type, and from a type conversion function whose input cannot be converted. In particular, the type conversion function `NUMBER()` will produce a tainted null value if the input cannot be converted to the appropriate type. Tainted null values behave exactly the same as other null values, with just one difference: using a tainted null value causes Goro to emit a warning to alert you, as described under [Warnings](#warnings) below.

There is an exception to this: the function `ISNULL()` will _never_ produce a warning if its argument is tainted, so the construction `NOT ISNULL(NUMBER(x))` can be used to safely check that `x` is not null and convertible to a number; likewise the function `FALLBACK(expr, literal_default)` will also not produce a warning if `expr` is a tainted null, but simply return `literal_default` instead as it would for an untainted null.

Because identifiers can produce tainted nulls of their own, `ISNULL()` and `FALLBACK()` can be useful applied directly to identifiers and not only to the results of conversions. `FALLBACK(track, 0)` substitutes zero both for a track tag that is missing and for one that is present but unusable, and `NOT ISNULL(track)` is the way to require that a usable track number exists.

### Errors

Goro reports every error it possibly can when the predicate is read, before any file is processed. This is a deliberate constraint rather than a convenience: a command may be operating on a hundred thousand files, and it is not acceptable for such a run to abort halfway through because of a mistake that could have been pointed out at the start.

Reporting errors this early is possible because the type of every sub-expression in a predicate is known statically. Identifiers have a declared type, each function has a fixed result type, and literals are typed by their syntax. Nothing about a particular file can change the type of an expression: a file determines only whether a value is present, absent or unusable, and whether it is single or multivalued. Type checking therefore needs no file at all.

The errors reported when a predicate is read include:

- syntax errors of any kind, including a non-ASCII character in an identifier, a chained comparison such as `a == b == c`, a reserved word used as a bare identifier, and a qualified name used as a function call;
- a reference to an identifier that is not defined, where the namespace it names is a closed one;
- a type mismatch between the operands of an operator, outside the numeric literal exception;
- a boolean value used anywhere other than as an operand of `AND`, `OR` or `NOT`, and a predicate whose value as a whole is not boolean;
- an argument of the wrong type to a function, and a modifier applied to an operand it cannot affect, such as `LITERALLY()` applied to something other than a string;
- a modifier applied anywhere other than to an operand of a comparison, range, or regex operator, or to another such modifier;
- a range whose endpoints are not literals of the same type, or whose `min` is greater than its `max`;
- a regular expression literal that is not a valid regular expression.

Only conditions that genuinely depend on the contents of a file are left to be discovered during evaluation, and none of them ever stops a run: a value that is missing or cannot be interpreted, a regular expression arriving from tag data that turns out not to be valid, and a match that exceeds the matching timeout. These are reported as warnings instead, as described below.

### Warnings

A warning is emitted when a tainted null value is consumed while evaluating a predicate, and likewise when a regular expression fails to compile or a match exceeds the matching timeout. Evaluation always continues after a warning: a warning never interrupts processing, and it never changes the result of the predicate.

#### What counts as using a tainted null

A function that returns null when given a tainted null is _propagating_ it, and propagation never emits a warning. A function that returns a non-null result derived from a tainted input is _consuming_ it, and consuming emits a warning -- except for `ISNULL()` and `FALLBACK()`, which are the sanctioned suppressors and never warn. Operators always consume, since they always produce a boolean, so an operator given a tainted null always warns.

For example, `NUMBER()` and `STRING()` both return null when given null, so they pass a tainted null along in silence, whereas `COUNT()` returns a number and therefore consumes the taint and warns. A modifier never consumes a taint either, since it computes nothing: applying one leaves the operator that follows as the consumer. This is what allows the guard `NOT ISNULL(NUMBER(x)) AND NUMBER(x) > 5` to work even when the taint originates in `x` itself rather than in the conversion: `NUMBER()` is propagating an already (tainted) null value, and the first consumer of the taint is `ISNULL()` which explicitly guards against it.

#### Where a warning comes from

A tainted null is born once, at the identifier or the conversion that failed, and it carries that _source_ for the remainder of the evaluation. A function or operator that passes a tainted null through neither re-taints it nor changes its source. A warning is therefore emitted at the point where the value is consumed, but reports where it was born: in `STRING(id3v1::year) == "1991"` applied to a file whose Id3v1 year field holds something that is not a year, the taint is born at `id3v1::year`, is propagated through `STRING()`, and the warning is triggered by the consuming `==` but names `id3v1::year` as the source, which is where the problem actually is.

#### Deduplication

Warnings are deduplicated per file and per source. Evaluating a predicate against one file emits at most one warning for each distinct sub-expression that produced tainted data; the second and subsequent times the same source is reached while evaluating that same file, nothing further is emitted.

Two sub-expressions are the same source when they apply the same functions, in the same shape, to the same identifiers and literals. How they are written does not matter -- whitespace, letter case, and whether a namespace is given explicitly are all irrelevant -- and neither does where in the predicate they appear. The modifiers `ALL()`, `ANY()` and `LITERALLY()` are transparent for this purpose: they never make two otherwise identical sub-expressions into different sources.

In the following examples, `x` and `y` are identifiers that resolve to a tainted null for the file being evaluated, `m` is a string multivalue that includes at least one tainted null, and `s` and `t` are identifiers holding strings that are not valid numbers.

| Predicate                          | Warnings | Why                                                                          |
|------------------------------------|:--------:|------------------------------------------------------------------------------|
| `x > 1`                            | 1        | a single source
| `x > x`                            | 1        | both occurrences are the same source
| `x < 10 OR x > 20`                 | 1        | the same source, reached twice
| `x > y`                            | 2        | different identifiers are different sources
| `m > 1`                            | 1        | every value of a multivalue shares the source of the identifier that produced it
| `NUMBER(s) > NUMBER(s)`            | 1        | structurally identical sub-expressions are one source
| `NUMBER(s) > NUMBER(t)`            | 2        | the sources differ below the top level
| `ALL(m) == "a" OR m == "b"`        | 1        | `ALL()` is transparent, so both operands have the same source

Deduplication resets for every file. A predicate such as `track == 1` applied to a collection in which 500 files have junk where the track number should be will therefore produce 500 warnings, one per affected file. This is deliberate: each warning names a file whose data needs attention, and collapsing the repeats would discard exactly the information that makes the warning actionable.

A value that is never evaluated never warns. In particular, the short-circuiting of `AND` and `OR` can be used deliberately to avoid a warning; refer to `ISNULL()` for the details.

#### Where warnings go

Warnings are written to standard error and never to standard output, so that they cannot interfere with the machine-readable output of commands such as `goro list -o json`. They do not by themselves make a command fail: a run that warned is still a run that completed, and reporting that fact through the exit code is opt-in (see [Exit codes](exit-codes.md)). Each warning names the file being processed and quotes the sub-expression responsible, exactly as it was written in the predicate. Where the same source occurs in more than one place, the quoted text is that of the occurrence which actually produced the value. Because deduplication means only one of several equally responsible occurrences is reported, a warning deliberately does not name a position within the predicate.

### Modifiers

Three constructs -- `ALL`, `ANY`, and `LITERALLY` -- are _modifiers_ rather than functions. A modifier does not compute a new value from its argument; it changes how the operator that consumes the value will treat it. `ALL` and `ANY` select the quantifier applied to a multivalue, and `LITERALLY` suppresses the normalization of string values. The default behavior of every value is that of `ANY` and of normalized comparison, so a modifier is only ever needed to depart from that.

Because a modifier's effect is realized by an operator rather than by the modifier itself, **a modifier may only be applied to an operand of a comparison, range, or regex operator, or to another such modifier**. Writing one anywhere else is an error. In particular a modifier may not appear as an argument to a function: `LITERALLY(genre) == "Pop"` is valid, while `COUNT(ALL(genre))` and `FALLBACK(LITERALLY(genre), "pop")` are not.

The restriction applies in one direction only. A modifier may be applied to any operand, including one that is itself a function call, so `LITERALLY(FALLBACK(genre, "pop")) == "Pop"` is valid: modifiers go on the outside, functions on the inside.

Modifiers may be stacked as deeply as desired and in any order, since each one sets an independent property of the operand. `LITERALLY(ALL(genre))` and `ALL(LITERALLY(genre))` mean the same thing, and where the same property is set twice the outermost modifier wins, so `ALL(ANY(genre))` is universally quantified.

The modifiers are:

**ALL(expr)**
: causes the operand to be evaluated with a _universal_ quantifier when it is a multivalue, instead of the default _existential_ quantifier: an operator using this operand only matches if _all_ of the multiple values present match, rather than _any one_ value as is the default behavior. See [Multivalues](#multivalues) for the full evaluation rules, including how two multivalue operands are combined when their quantifiers differ.

Examples:

- `genre == "rock"` will match when `genre` is a multivalue representing both `"rock"` and `"metal"` because at least one of the values matches
- `ALL(genre) == "rock"` will _not_ match in the same scenario because not all of the values match `"rock"`

In case the operand that `ALL()` is applied to is not a multivalue, the modifier has no effect and the operand is treated exactly as it would have been without it.

**ANY(expr)**
: causes the operand to be evaluated with an _existential_ quantifier when it is a multivalue: an operator using this operand matches when _any one_ of the multiple values matches. This is the default behavior for every multivalue operand, so this modifier is never strictly necessary, but it is included as an explicit counterpart to `ALL()`, both to allow more expressiveness if desired (`ANY(genre) == "rock"` reads very naturally) and to make a quantifier choice explicit where it might otherwise be unclear at a glance -- for example, when comparing two multivalues where only one side is wrapped in `ALL()`.

In case the operand that `ANY()` is applied to is not a multivalue, the modifier has no effect and the operand is treated exactly as it would have been without it.

**LITERALLY(expr)**
: prevents any operator that compares strings from normalizing its inputs before testing. Use this modifier to achieve case-sensitive matching, matching on diacritics, and other specialized scenarios. It also works with regular expression matching.

Note that it is enough for _just one_ of the operands to a string operator to be a `LITERALLY()` value to disable normalization -- `LITERALLY(x) == y`, `x == LITERALLY(y)`, and `LITERALLY(x) == LITERALLY(y)` all do exactly the same thing.

When applied to either operand of `~=`, this modifier additionally makes the match case-sensitive, since for that operator case insensitivity is part of normalization rather than of the match itself.

If the operand is a multivalue, the effect applies individually to each of its values. It is an error to apply `LITERALLY()` to an operand of any type other than string; null and string multivalues are of course still permitted.

Examples:

- `artist == "metallica"` matches "Metallica" because the equality operator normalizes its inputs by default.
- `artist == LITERALLY("metallica")` does _not_ match "Metallica" because the result of `LITERALLY()` prevents the equality operator from normalizing its inputs before comparing them.
- `artist BETWEEN "m".."n"` matches "Metallica" because, after normalization by default, "Metallica" sorts between "m" and "n".
- `LITERALLY(artist) BETWEEN "m".."n"` does _not_ match "Metallica" because the result of `LITERALLY()` prevents the range operator from normalizing its inputs, and upper case "M" does not sort between lower case "m" and "n".
- `artist ~= "^met"` matches "Metallica" because diacritics are removed from both operands and the match is performed case-insensitively.
- `artist ~= LITERALLY("^met")` does _not_ match "Metallica" because the result of `LITERALLY()` prevents the regex matching operator from normalizing its inputs.

### Functions

There are a number of built-in functions that can be used inside predicate expressions. Functions are invoked by their name followed by a list of zero or more arguments in parentheses. For example, `FOO(x, y)` is a call to the function `foo` with arguments `x` and `y`.

A function call is always an **unqualified** name followed by an opening parenthesis. Functions do not live in namespaces, so a qualified name is never a function call and `foo::count(x)` is a syntax error. This is what allows a single name to serve both as a function name and, in principle, as an identifier: `count` on its own is an identifier reference, `count(...)` is a call, and the qualified form `::count` always and unambiguously refers to the identifier.

Function arguments follow the same type-matching discipline as operator operands, including the numeric-literal exception. Unless otherwise noted below, a function given a null argument returns null.

The functions that can be used inside predicates are:

**COUNT(expr)**
: returns the number of values in its argument, as a number. Specifically, the result of this function is:

- `0` if `expr` evaluates to null
- `1` if `expr` is a non-null single value
- a positive integer greater than 1 if `expr` is a multivalue, equal to the count of its individual values

Values that are null are counted like any other, so a multivalue of two null values has a count of 2, which is distinct from plain null having a count of 0.

Because this function returns a number derived from its argument instead of passing a null onwards, it consumes a tainted null rather than propagating it, and therefore emits a warning; see [Warnings](#warnings).

Note that this function _cannot_ be used as a substitute for `ISNULL()`. `COUNT(expr) == 0` holds only when expr is plain null, whereas `ISNULL(expr)` is also true for a multivalue that merely contains a null value. `ISNULL()` is the correct way to determine whether a value is usable.

Example:

- `COUNT(genre) > 1` is true if and only if `genre` is a multivalue

**FALLBACK(expr, literal_default)**
: returns the value of `expr` if non-null, or the value of `literal_default` otherwise.

This function _does not trigger a taint warning even if its first argument is a tainted null value_. It is an error if `literal_default` is not a literal.

If `expr` is a multivalue, this function returns a multivalue of the same cardinality with each of the individual values being the result of applying `FALLBACK()` with `literal_default` on each value of the input. No warnings will be emitted even if the input contains multiple tainted nulls.

**ISNULL(expr)**
: returns true if `expr` has no usable value, or false otherwise. Specifically, this function returns true if `expr` evaluates to null, and also if `expr` is a multivalue and any one of its values is null. It returns false _only_ when `expr` is a single non-null value, or a multivalue none of whose values is null.

Unlike most functions, `ISNULL()` does not apply to each value of a multivalue individually: it examines the multivalue as a whole and always returns a single boolean.

This function _does not trigger a taint warning_ even if its argument is a tainted null value, or contains one. Therefore, it is the sanctioned way (through its negation, `NOT ISNULL()`) of checking the validity of a conversion with `NUMBER()` without potentially emitting a warning.

Examples:

- `ISNULL(genre)` is true when the file has no genre tag at all
- `ISNULL(NUMBER(id3v2::raw::TRCK))` is true when the frame is absent, and also when the frame is present more than once and at least one of the occurrences does not hold a valid number
- `NOT ISNULL(NUMBER(x)) AND NUMBER(x) > 5` is safe: because `AND` short-circuits, the right operand is only evaluated when every value of `x` converted successfully

**NUMBER(expr)**
: converts its argument to a number.

If `expr` is null or already a number, this function returns the same value. If `expr` is a bytecount, it returns the count as a number of bytes. If `expr` is a duration, it returns total number of seconds. If `expr` is a string that satisfies the rules for a numeric literal, it is converted to a number and returned. If `expr` is a string that does _not_ validate as a numeric literal, the function returns a null value to represent the absence of a usable number; this value is tainted and will result in a warning being emitted the first time an attempt is made to use it.

If the argument is a multivalue, this function returns a multivalue of the same cardinality with each of the individual values being produced by applying `NUMBER()` on each value of the input.

**STRING(expr)**
: converts its argument to a string.

If `expr` is null or already a string, this function returns the same value. If `expr` is a bytecount, it returns the decimal string representation of the count of bytes as a plain number without a unit. If `expr` is a duration, it returns the decimal string representation of the total duration in seconds as a plain number without a unit. If `expr` is a number, it returns its decimal string representation.

If the argument is a multivalue, this function returns a multivalue of the same cardinality with each of the individual values being produced by applying `STRING()` on each value of the input.

## GRAMMAR

The following grammar is given in EBNF. Terminals are quoted; `{ x }` means zero or more repetitions of `x`, `[ x ]` means `x` is optional, and `|` separates alternatives. All quoted keywords, function names, identifier names and unit suffixes are matched case-insensitively.

### Expressions

```ebnf
predicate       = expression ;

expression      = or_expr ;
or_expr         = and_expr { "OR" and_expr } ;
and_expr        = not_expr { "AND" not_expr } ;
not_expr        = "NOT" not_expr
                | comparison_expr ;

comparison_expr = operand [ comparison_tail ] ;
comparison_tail = comparison_op operand
                | "BETWEEN" range
                | "~=" operand ;

comparison_op   = "==" | "!=" | "<=" | ">=" | "<" | ">" ;

operand         = modifier "(" operand ")"
                | primary ;
modifier        = "ALL" | "ANY" | "LITERALLY" ;

primary         = literal
                | identifier
                | function_call
                | "(" expression ")" ;

function_call   = name "(" [ expression { "," expression } ] ")" ;
```

The four levels of `or_expr`, `and_expr`, `not_expr` and `comparison_expr` are what give the operators the precedence listed under [Grouping, precedence, and associativity](#grouping-precedence-and-associativity). `comparison_tail` appears at most once and never recurses, which is what makes the comparison, range and regex operators non-associative.

Because a modifier can only occur within an `operand`, and `operand` occurs only as an operand of a comparison, range or regex operator, the placement restriction described under [Modifiers](#modifiers) is imposed by the grammar rather than by a rule laid on top of it. The recursion in `operand` is what allows modifiers to be stacked without limit. Note that `primary` admits a parenthesized `expression` but `operand` does not, so a modifier may not be wrapped in redundant grouping parentheses: `(ALL(genre)) == "x"` does not parse.

### Identifiers and names

```ebnf
identifier      = [ "::" ] name { "::" name_part } ;
name_part       = name | string ;
name            = letter { letter | digit | "_" } ;

letter          = "A" .. "Z" | "a" .. "z" ;
digit           = "0" .. "9" ;
```

### Literals

```ebnf
literal         = string | number | bytecount | duration ;
range           = literal ".." literal ;

string          = '"' { string_char | escape } '"' ;
string_char     = <any character other than '"' and "\"> ;
escape          = "\" ( '"' | "\" ) ;

number          = [ "+" | "-" ] unsigned_number ;
unsigned_number = digits [ "." digits ]
                | "." digits ;
digits          = digit { digit } ;

bytecount       = unsigned_number byte_unit ;
byte_unit       = "b"
                | "kb" | "mb" | "gb" | "tb"
                | "kib" | "mib" | "gib" | "tib" ;

duration        = duration_units | duration_clock ;
duration_units  = [ digits "h" ] [ digits "m" ] [ digits "s" ] ;
duration_clock  = [ digits ":" ] two_digits ":" two_digits ;
two_digits      = digit digit ;
```

### Whitespace

Whitespace may appear freely between tokens and is insignificant there, but it may not appear **inside** a token. This matters for `bytecount` and `duration`, whose productions above are single tokens: `10kb` and `1h10m` are literals, whereas `10 kb` and `1h 10m` are not.

### Constraints not expressed by the grammar

Not every rule in this document is grammatical, and a construct that this grammar accepts may still be rejected. The following constraints are enforced by static analysis of a parsed predicate rather than by the grammar, and all of them are reported as [errors](#errors) when the predicate is read:

- a `predicate` must have boolean type, so a bare `primary` such as `artist` parses but is not a valid predicate;
- the operands of an operator must have the same type, subject to the numeric literal exception, and the operands of `AND`, `OR` and `NOT` must be boolean while the operands of every other operator must not be;
- both operands of `~=` must be strings, and its right operand must be a valid regular expression if it is a literal;
- an `identifier` in a closed namespace must be one the namespace defines, and an identifier's first `name` may not be one of the reserved words unless the identifier is qualified;
- a `name_part` written as a `string` names the same thing as the equivalent bare `name` when the name is one a bare `name` could have spelled, so the two forms are one identifier and not two;
- a `function_call` names an existing function and supplies it with the number and types of arguments it accepts; in particular the second argument of `FALLBACK()` must be a literal;
- the `operand` a `LITERALLY` modifier is applied to must be of type string;
- the two endpoints of a `range` must be literals of the same type, and `min` must not be greater than `max`;
- at least one of the three unit groups of a `duration_units` must be present, so the empty string does not satisfy that production;
- each `two_digits` of a `duration_clock` must denote a value between 0 and 59 inclusive.
