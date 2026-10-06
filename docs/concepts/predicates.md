# Predicates

## NAME

predicate - filtering expression accepted by commands as argument

## SYNOPSIS

*command* --filter=*predicate*

## DESCRIPTION

### Overview

Some Goro commands have optional arguments that can be used to configure the scope of the command. This is achieved by writing _predicates_: conditions that determine if a file is in scope for processing based on its properties, tags, and other associated data. For example, `artist == "metallica"` is a predicate satisfied by files where the artist tag matches "metallica". In this predicate, `artist` and `"metallica"` are _values_ being compared by the _operator_ `==`, which determines if they are equal. A predicate as a whole must be a [definite](#definite-expressions) expression of type boolean: a file is in scope for processing when the predicate evaluates to true for it, and out of scope when it evaluates to false. A predicate can also evaluate to an unusable boolean, when the data it needed for a file could not be interpreted; what a command does with such a file is for the command to say, and each command that accepts a predicate documents it. A bare value such as `artist` is not a valid predicate.

A predicate is supplied to a command as a single command line argument. Because predicate syntax uses double quotes to delimit string values, that argument normally has to be wrapped in single quotes so that the shell passes it through intact; see [goro list](../commands/list.md) for a worked example.

The rest of this section describes the syntax for predicates, and the various operators and other constructs that can appear inside them. [Evaluation](evaluation.md) defines what each of them evaluates to, compactly and precisely.

### Tokens and whitespace

A predicate is read as a sequence of _tokens_: literals, identifiers, operators and punctuation. Two rules govern how the text is divided into them, and both come up often enough to belong here rather than in the grammar.

**A token is always as long as it can be.** Where a stretch of text could be read as one longer token or as two shorter ones, the longer reading wins. This is what makes `5mb` a bytecount rather than a five-minute duration followed by a stray `b`, `1h10m` a single duration rather than three tokens, `r"x"` a raw string rather than an identifier followed by a string, and `NOTx` an identifier rather than the `NOT` operator applied to something called `x`.

**Whitespace separates tokens and is insignificant everywhere else.** Whitespace is any character with the Unicode `White_Space` property, such as the space, the tab, the line breaks, the no-break space and the ideographic space. The amount and kind of whitespace between two tokens is ignored, but whitespace may never appear *inside* one: `10 kb` and `1h 10m` are not literals, and `r "x"` is not a raw string. Whitespace is therefore required wherever two tokens would otherwise run together into one, as in `NOT x`. Inside a string value whitespace is preserved exactly, so `"foo"` and `"foo "` are not equal.

### Case sensitivity

Predicate syntax is case-insensitive throughout, using invariant culture rules. The keyword operators (`AND`, `OR`, `NOT`, `BETWEEN`, `IS`), the state names (`USABLE`, `UNUSABLE`, `ABSENT`), the reserved words `NULL`, `TRUE` and `FALSE`, function names, identifiers, namespaces, and the unit suffixes of bytecount and duration literals are all matched without regard to case. `artist`, `Artist` and `ARTIST` are the same identifier, and similarly `id3v2::tit2` and `ID3V2::TIT2` are the same identifier.

The only case-sensitive text in a predicate is the contents of a string value -- and even those are compared without regard to case unless `LITERALLY()` is used; see [Normalization](normalization.md). The letters of a string escape are a second and smaller exception: the escapes are the ones listed under [Values](#values) and no others, so `\n` is a line feed while `\N` is not an escape at all and is therefore an error. The hexadecimal digits of `\x` and `\u` may be written in either case, and the `r` prefixing a raw string may be either `r` or `R`, both being notation around a string rather than part of its contents. A quoted part of an identifier is not a string value in this sense and is matched without regard to case like any other part of an identifier, so `ape::"album artist"` and `ape::"Album Artist"` are the same identifier.

### Identifiers and namespaces

Identifiers are unquoted sequences that start with an ASCII letter and may contain ASCII letters, digits, and underscores. For example, `artist` and `year` are both simple identifiers. Characters outside this set, including accented letters and any other non-ASCII character, may not appear in an identifier; encountering one is a syntax error rather than a reference to an undefined identifier.

Each identifier may also be preceded by a _namespace_. Namespaces follow the same rules as identifiers: they start with an ASCII letter and may contain ASCII letters, digits, and underscores. Namespaces are part of the identifier, and are separated from it by a double colon. For example, `id3v2::TIT2` is a _qualified_ identifier where `id3v2` is the namespace.

In actuality, all identifiers have a namespace. If the namespace is not explicitly provided (i.e. no double colon in the identifier), the identifier is _unqualified_ and considered to be inside the _global namespace_. The global namespace can also be explicitly specified by using `::` without a namespace in front. For example, `artist` is an unqualified identifier implicitly in the global namespace, `::artist` is a functionally equivalent (but qualified) identifier but with the namespace explicitly provided, and `id3v1::artist` is a qualified identifier in the namespace `id3v1`.

Namespaces can also be hierarchically nested. For example, `foo::bar::baz` is a qualified identifier referring to `baz` in the namespace `foo::bar`.

A leading `::` starts a name at the global namespace, where every namespace sits, so `::id3v2::TIT2` and `id3v2::TIT2` are the same identifier.

An identifier is written without whitespace around its `::`, so `ape :: artist` is an error.

Some of the names an identifier has to reach are not Goro's to choose. An [open namespace](../features/builtins/identifiers.md) derives the name it looks for from the identifier itself, and tag formats allow names that the rules above cannot spell -- an APE item key may contain spaces, as `Album Artist` does, and other formats permit further punctuation. For these, a part of an identifier may instead be written **as a quoted string**, in which case its contents are the name and the restrictions on identifier characters do not apply:

```
ape::"album artist" == "Various Artists"
```

A quoted part must be preceded by `::`, so a string at the start of an expression is always a string value. Quoting is available wherever it is useful rather than only where it is necessary, so `ape::"artist"` is permitted and means exactly the same as `ape::artist`: the two are one identifier written two ways, not two identifiers. Escapes inside a quoted part follow the same rules as in any other string, so a name containing a double quote or a backslash can be written by preceding it with a backslash.

For a list of predefined namespaces and identifiers within them together with a description of each one, see [built-in identifiers](../features/builtins/identifiers.md). A namespace is either _closed_, meaning that the identifiers it contains are known in advance, or _open_, meaning that it additionally admits identifiers whose names are derived from the tag data of the file being examined. Naming a namespace that Goro does not define is an error, and so is referring to an identifier that is not defined in a closed namespace; an identifier in an open namespace is always accepted, and simply resolves to an absent value when the file holds no data for it.

### Reserved words

The keyword operators `AND`, `OR`, `NOT`, `BETWEEN`, and `IS` (case-insensitive) are reserved and cannot be used as a bare identifier in the global namespace. For example, `and` alone is always interpreted as the logical `AND` operator, never as an identifier named "and".

The state names `USABLE`, `UNUSABLE`, and `ABSENT` (case-insensitive) are reserved on the same terms.

The keywords `TRUE` and `FALSE` (case-insensitive) are the two boolean literals, and are reserved on the same terms. The keyword `NULL` (case-insensitive) is reserved as well, although it names nothing that exists: Goro has no null value.

A reserved word may still appear in an identifier, but never as its first part unless the identifier begins with `::`: `::and` and `ape::and` are identifiers, while `and::x` is not. So `NOT::x` is the `NOT` operator applied to `::x`.

The modifiers `ALL`, `ANY`, and `LITERALLY` (case-insensitive) are reserved on the same terms.

Function names (such as `COUNT`, `FALLBACK`, `NUMBER`, `PREFERRED` and `STRING`) are not reserved words in this sense.

Although the grammar does not require it, no built-in identifier in the global namespace has the same name as a function, and no function is given the name of a built-in global identifier. This is a constraint on what Goro defines rather than on what a predicate may write.

### Values

Values in predicates come from _identifiers_ (preagreed names to refer to various types of data specific to each file) and _literals_ (values specified directly when writing a predicate). For example, in the predicate `year == 2000`, `year` is an identifier and `2000` is a literal value. A literal enclosed in parentheses is still a literal, so wherever a rule calls for one, `(0)` serves exactly as `0` does.

Every value has three aspects. The first is always decided by the text of the predicate; the other two are decided by the file, except that the text of some expressions guarantees their cardinality (see [Definite expressions](#definite-expressions) below).

**Type** : A value's type dictates what operations may be performed on it and how their results are calculated. The types are `string`, `number`, `bytecount`, `duration` and `boolean`, and they are described below. A value's type never depends on the file being examined: identifiers have a declared type, each function and operator has a fixed result type, and literals are typed by their syntax.

**Cardinality** : A value is either **absent**, meaning that the file records nothing for it, or a bag of one or more _occurrences_. A bag is unordered and its occurrences are not deduplicated, so the same datum recorded twice in the underlying tags occurs twice in the bag. A bag of exactly one occurrence is indistinguishable from a simple value: no construct in the language can tell the two apart, and everything this document says about simple values holds unchanged for a bag of cardinality one. Absent is the value of cardinality zero, and it is the only one -- there is no empty bag distinct from absence.

**State** : Each occurrence in a bag is either **usable**, meaning that its data can be interpreted as the value's type, or **unusable**, meaning that the data is there but cannot be so interpreted. A tag field holding `nineteen ninety one` where a year is expected is one unusable occurrence. An unusable occurrence takes part in every operation as a usable one does -- it is counted, carried through functions and iterated over like any other -- but it cannot supply an answer, and it marks a defect in the file. An operator that needs its content emits a warning, and has no answer for any combination that includes it, so that the operator's result is also unusable unless some other occurrence seen during the operation settles it. Absent data, by contrast, is ordinary, and never warns on its own account.

Absence is a property of a whole value, while unusability is a property of a single occurrence. From this it follows that **a bag never contains an absence**: an occurrence that is not there is not an occurrence. Where a resolution rule discards an occurrence entirely, it lowers the value's cardinality, and a value all of whose occurrences are discarded is absent.

#### Types

**boolean**
: a truth value, true or false. The boolean literals are `TRUE` and `FALSE`, and boolean values are otherwise produced by the comparison, range, regex, state test and logical operators described later in this document.

Operators and the boolean literals always produce exactly one boolean, never an absent one, which makes them [definite](#definite-expressions). A boolean can be unusable: a comparison, range or regex operator whose answer depended on an occurrence it could not interpret has no answer to give, and its result is an unusable boolean (see [General operator rules](#general-operator-rules)).

Booleans are **unordered**. They may be compared with `==` and `!=`, tested with a state test, and passed to `COUNT()`, `FALLBACK()` and `PREFERRED()`, but it is an error to use one as an operand of an ordering operator, of `BETWEEN`, or of `=~`, or as an argument to `NUMBER()` or `STRING()`.

**string**
: a sequence of characters, written in either of two forms.

A **quoted string** is delimited by double quotes and processes escape sequences, as in `"metallica"`. Within one, a backslash introduces an escape, and these are all of them:

| Escape    | Denotes                                        |
|-----------|------------------------------------------------|
| `\"`      | a double quote |
| `\\`      | a backslash |
| `\n`      | line feed, U+000A |
| `\r`      | carriage return, U+000D |
| `\t`      | tab, U+0009 |
| `\xNN`    | the code unit given by two hexadecimal digits |
| `\uNNNN`  | the code unit given by four hexadecimal digits |
| `\u{N...}` | the character given by one to six hexadecimal digits, any code point other than a surrogate |

A backslash followed by anything else is an error rather than a literal backslash, so text written with single backslashes is rejected instead of being silently misread. `\x` and the unbraced `\u` take a fixed number of digits, which is what makes `"\x41B"` unambiguously `A` followed by `B`; the braced form ends at its closing brace. `\x` and the four-digit `\u` denote UTF-16 code units, so with them a character outside the Basic Multilingual Plane is written as a surrogate pair, and a surrogate that is not part of a pair is an error. The braced form `\u{...}` denotes a whole character instead, so `\u{1F3B5}` and `\uD83C\uDFB5` are the same string.

A **raw string** carries the prefix `r` and processes nothing whatsoever: every character between the quotes is part of the value, and a backslash is simply a backslash, so `r"\d{4}"` and `"\\d{4}"` denote the same text. A double quote inside a raw string is written by doubling it, so `r"say ""hi"""` is the value `say "hi"`.

The two forms differ in nothing but how the text between the quotes is read. A raw string has string type like any other, may appear anywhere a quoted string may, including as a quoted part of an identifier, and two literals denoting the same characters are indistinguishable regardless of which form wrote them. The pattern of the [regular expression match operator](#regular-expression-match-operator) is the one place that accepts a raw string alone.

**number**
: real dimensionless numbers, such as `2000`, `-1`, and `-.55`. Number literals are optionally preceded by one + or - sign character, followed by either an integer or a floating point number where the integer part is separated from the fractional part by a period. If the number has a fractional part, the integer part is optional and considered to be zero when it does not appear. The period must always be followed by at least one digit, so `1`, `1.5` and `.5` are all valid number literals while `1.` is not.

A number is a [`System.Decimal`](https://learn.microsoft.com/en-us/dotnet/api/system.decimal): a base-ten value carrying 28 to 29 significant digits. Bytecounts and durations are held the same way, as a number of bytes and a number of seconds. `1.4kib` is exactly 1433.6 bytes, and a decimal fraction written in a predicate is the value it appears to be rather than the closest approximation a binary fraction can manage. A number, bytecount or duration literal whose value cannot be represented exactly in this way, being too large or carrying too many significant digits, is an error.

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
: a numeric value that also has a unit and represents a duration in time, measured in **whole seconds**. Duration values never carry a sub-second component: a duration obtained from a file, such as `file::duration`, is truncated towards zero to a whole number of seconds when it is produced, so a file playing for 4 minutes and 5.7 seconds has a `file::duration` of exactly `4m5s`.

Valid duration literals can take two forms:
- `[#h][#m][#s]`, where hash signs `#` represent some nonnegative integer and the letters `h`, `m` and `s` are case-insensitive literals representing hours, minutes, and seconds respectively. At least one of the unit specifiers must be present, and those that do appear must appear in the order given above. The fields are unbounded and additive, so `90m` is ninety minutes and `1h100m` is two hours and forty minutes. For example, `1h10m`, `1h1s`, `90m` and `0s` are all valid duration literals.
- `#:##` for minutes and seconds, or `#:##:##` for hours, minutes and seconds. The double hash signs `##` stand for a two-digit number from 00 to 59, written with a leading zero where it is below ten. The leading field is written as any number of digits and is **not** bounded at 59, so the conventional way of writing a long playing time works: `90:00` is ninety minutes and `250:00:00` is two hundred and fifty hours. `1:59`, `01:59`, `0:00` and `3:30` are all valid.

Whitespace may not appear inside a duration literal: `1h10m` is a valid literal and `1h 10m` is not.

#### Ranges and multivalues

Two further concepts are closely related to types, but are not types themselves:

**ranges**
: ranges are always specified as literals, and represent a range of values that can be used in comparisons. Ranges have a type like any other value, so there are numeric ranges, string ranges, duration ranges, etc. Valid range literals take the form `min..max` where `min` and `max` must be literals of the same type. For example, `1..100` is a numeric range from 1 to 100 (inclusive) and `10kb..10mb` is a bytecount range.

A range whose endpoints are not of the same type is rejected rather than guessed at. `60..120kb` is an error, and the diagnostic says what the trouble is: it is unclear whether `60` means sixty bytes or sixty kilobytes, and the unit has to be given.

A range that could hold nothing is an error. For most types that means `min > max`; for strings it means that `min`, cut to the length of `max`, sorts after `max`, since string endpoints are prefixes (see [Range operator](#range-operator)), so `"mi".."m"` is a valid range holding everything that begins with "mi". The two endpoints are compared the way the operator will compare them, which for strings means that a `LITERALLY` on the operator's other operand decides whether they are compared normalized -- `LITERALLY(artist) BETWEEN "B".."a"` is a valid range while `artist BETWEEN "B".."a"` is not.

**multivalues**
: a _multivalue_ is the name this document gives to a bag of cardinality greater than one: the simultaneous existence of several occurrences where a single value might otherwise be expected. Multivalues cannot be specified as literals, but they arise whenever the same kind of data is recorded more than once within tags. For example, the identifier `artist` might be absent (if there is no artist tag of any kind), a simple string value (if there is exactly one artist tag), or a multivalue (e.g. if there are multiple Vorbis comments with the `Artist` key).

The concept of multivalues is intended to enable clear and precise explanations of how Goro behaves in the presence of multiple tags that provide the same data, and is presented in more detail in its own section below.

#### Definite expressions

Some expressions are guaranteed by their form to produce exactly one occurrence, whatever the file. Such an expression is **definite**. The definite expressions are:

- every literal;
- the result of every operator: comparison, range, regex, state test, and the logical operators `AND`, `OR` and `NOT`;
- `COUNT()`, whatever its argument;
- `FALLBACK()`, `NUMBER()` and `STRING()` applied to a definite argument;
- `PREFERRED()` whose every argument is definite;
- the identifiers `file::path`, `file::name` and `file::size`, which every file has exactly one of;
- a parenthesized definite expression.

No other identifier is definite, whatever its namespace.

Definiteness is a property of an expression, known when the predicate is read, and never a property of a value. An identifier that happens to resolve to a single occurrence for a particular file is not thereby definite, and nothing in the language can tell such a value from the result of a definite expression; the guarantee is about every file, not about this one. A definite expression is never absent, but it can still be unusable: a comparison that met data it could not interpret is definite and unusable.

Definiteness matters in two places. The operands of the logical operators, and a predicate as a whole, must be definite; see [Boolean operators](#boolean-operators). And an operand of `!=` that is not definite must have its quantifier written; see [Comparison operators](#comparison-operators).

### Operators

Values within Goro predicates can be compared with the _comparison operators_ `==` (equality), `!=` (inequality), `<` (less than), `>` (greater than), `<=` (less than or equal), and `>=` (greater than or equal). There are also _boolean_ or _logical operators_ that appear as words instead of symbols: `NOT`, `AND`, and `OR`.

#### General operator rules

There are some **fundamental rules which apply globally** to any operator sub-expression:

1. **An absent operand makes a comparison, range, or regex operator false**, regardless of what its other operands might be, and iteration over any multivalue operand does not run at all. For example, if there is no Id3v2 tag, the predicates `id3v2::track < 5` and `id3v2::track >= 5` will _both_ evaluate to false, because `id3v2::track` is absent in both cases. This rule does not concern the logical operators `AND`, `OR`, and `NOT`, whose operands are definite and therefore never absent; nor does it concern the state test operator `IS`, whose purpose includes observing absence.

2. **An unusable occurrence makes unusable every combination of operand values that includes it**, and consuming it emits a warning. How the combinations are then combined into the operator's result is described under [Multivalues](#multivalues): a combination that was answered can still settle the result, so that `vorbis::year == 1991` is true for a file holding one occurrence of `1991` and one that is not a year at all. See [Warnings](#warnings) for what counts as consuming an occurrence.

3. If an operator has multiple operands, it is an error for the operands to have different types. For example, `file::duration > "01:00"` is an error because `file::duration` has duration type and `"01:00"` is a literal of type string. Instead, `file::duration > 01:00` is correct because `01:00` without quotes is a valid duration literal as described earlier.

4. There is a very important exception to the previous rule: _a numeric literal, though not a non-literal numeric value, may stand for a bytecount or a duration_, and is converted to whichever of the two the other operand is. For duration the converted value is that number of seconds, which must be a whole number, as the seconds of a duration literal must be: `file::duration > 90` compares with ninety seconds, while `file::duration > 1.5` is an error. For bytecount it is that number of bytes, so `file::size > 1000` selects the files larger than a thousand bytes rather than a thousand kilobytes, and `file::size > 1kb` is how to say the latter. The same holds for range literals: `file::size BETWEEN 60..120` compares `file::size` with a range of 60 to 120 bytes. A numeric literal that would convert to a bytecount or a duration may not be negative, neither of those having negative values. A numeric literal does **not** stand for a string, and a string literal does not stand for a number: `id3v2::raw::TRCK > 9` and `year == "2000"` are both errors.

5. When comparing string values, an operator _normalizes_ them by default, so that differences of case, of accents on Latin and Greek letters, and of a few kinds of character form do not matter: `artist == "motorhead"` matches when the artist is recorded as "Motörhead". Normalization is a **mode of the operator** rather than a property of either value, so it is switched off for the whole comparison by a `LITERALLY()` modifier on either operand; see [Comparison modes](#comparison-modes). [Normalization](normalization.md) defines precisely how strings are prepared in either mode.

#### Comparison operators

The comparison operators `==`, `!=`, `<`, `<=`, `>`, `>=` directly compare their operands. Comparisons between values of type number, bytecount, and duration are trivial; strings are compared code point by code point, after being prepared as described under [Normalization](normalization.md): normalized by default, or literally under `LITERALLY()`. Booleans, being unordered, may be compared only with `==` and `!=`, so `(year < 2000) == (genre == "rock")` is true when the two conditions agree. An absent operand makes a comparison false regardless of what comparison it is, while an unusable occurrence makes the combination being evaluated unusable; see the general operator rules above.

**`!=` requires a quantifier on any operand that is not [definite](#definite-expressions).** Such an operand may be absent or hold several occurrences, and "not equal" then has more than one reading, so the predicate has to say which it means. `genre != "metal"` is an error, and the diagnostic offers the two forms that are usually meant:

- `NOT genre == "metal"`: no genre is metal, which is also true for a file that records no genre at all;
- `ALL(genre) != "metal"`: every genre is something other than metal, which is false for such a file, as every comparison with an absent operand is.

`ANY(genre) != "metal"` is valid too, and is true when some genre is something other than metal, including for a file tagged both "metal" and "rock". The quantifier means exactly what it means on any other operator; it is only that `!=` will not assume one. A definite operand needs none, so `COUNT(genre) != 1` and `(year < 2000) != (genre == "rock")` are valid as written.

#### Range operator

The range operator `BETWEEN` can be used to perform a range inclusion check; it determines if its first operand is part of the (inclusive) range defined by the second operand, for example `year BETWEEN 1990..2000`. An occurrence `x` is part of the range when `min <= x` and `x <= max` both hold of that same occurrence.

Strings are compared as the comparison operators compare them, with one difference: **each endpoint is compared with only as many characters of `x` as the endpoint itself has.** A string range therefore reaches every string beginning with its maximum, the way the spine of an encyclopedia volume marked "Ma–Mi" does: `artist BETWEEN "ma".."mi"` matches "Miles Davis", which `artist <= "mi"` does not, and a range whose endpoints are equal, such as `"the".."the"`, matches every string that begins with them. Characters here are those of the strings as the operator prepares them, normalized or literal.

`BETWEEN` is **a single operator**, and the two inequalities above describe _how one occurrence is tested_ rather than a rewriting of the predicate. The distinction is invisible for a simple value and decisive for a multivalue, because an operator is one quantifier scope and splitting it into two would create a second: for a value `v` with occurrences `0` and `20`, `v BETWEEN 1..10` is false, because no single occurrence lies in the range, whereas `1 <= v AND v <= 10` is *true*, each of its two comparisons finding a different occurrence to satisfy it. See [Multivalues](#multivalues).

Examples:

- `year BETWEEN 2000..2010` matches if the year is between 2000 and 2010, inclusive.
- `artist BETWEEN "ma".."mi"` matches "Metallica" and "Miles Davis", but not "Motörhead": in normalized mode case does not matter, "me" lies between "ma" and "mi", every name beginning with "mi" is within the range, and "mo" is beyond it.

#### Regular expression match operator

String values can be tested to see if they match a regular expression with the regex matching operator `=~`. Its left operand is the **subject**, the value being tested, and must be of type string. What follows the operator is the **pattern**, which is not an operand but part of the operator, as the range is part of `BETWEEN`: it is a raw string, written directly after `=~` with no parentheses or modifiers around it. `artist =~ r"^the "` is valid, while `artist =~ "^the "` and `title =~ artist` are errors. A raw string processes no escapes, so every backslash in a pattern reaches the regular expression engine as written. Regular expressions are [.NET-flavored](https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expressions), with the restriction described under [Supported constructs](#supported-constructs) below.

The regex matching operator will match if any substring of the subject matches the pattern, so if more exact matching is intended the anchors `^` and/or `$` have to be specified.

**Only the subject is normalized, and the pattern is never touched.** The subject is prepared exactly as for any other operator, and in normalized mode the match is case-insensitive. A pattern is what the user wrote, character for character, so **a pattern cannot match anything normalization removes or changes**: `artist =~ r"motö"` finds nothing at all, while `artist =~ r"mot"` matches "Motörhead". Applying `LITERALLY()` to the subject puts it in literal mode and makes the match case-sensitive, which is the way to write a pattern that means to match a diacritic. See [Normalization](normalization.md#regular-expressions), and the [rationale](../design/rationale.md) for why the pattern is exempt.

If the subject is a multivalue, matching follows the ordinary quantifier rules described under [Multivalues](#multivalues).

There is no negated version of this operator; to determine if a value does not match a regular expression, apply `NOT` to the result of matching it.

Examples:

- `artist =~ r"s$"` matches any artist whose name ends in "s" or "S"
- `artist =~ r"^\d+ "` matches any artist whose name begins with a number followed by a space

##### Supported constructs

Matching uses the **non-backtracking** engine, with **invariant culture** rules and, unless `LITERALLY()` says otherwise, case insensitivity. A match therefore takes time linear in the length of the subject whatever the pattern, and is never abandoned part way through: there is no matching timeout, and no result depends on the machine a run executes on or on what else that machine was doing at the time.

That engine does not support four families of construct, and a pattern using any of them is rejected rather than matched:

| Construct | Examples |
|---|---|
| lookaround, positive and negative | `(?=...)`, `(?!...)`, `(?<=...)`, `(?<!...)` |
| backreferences, numbered and named | `\1`, `\k<name>` |
| atomic groups | `(?>...)` |
| conditionals and balancing groups | `(?(1)a\|b)`, `(?<-x>...)` |

Everything else a .NET pattern may contain is available, including captures and named captures, alternation, greedy and lazy quantifiers, character classes, Unicode categories such as `\p{Lu}`, word boundaries, and inline options.

A pattern that uses one of these constructs, or that is not a valid regular expression at all, is an error reported when the predicate is read.

See the [rationale](../design/rationale.md) for why this engine was chosen and what the restriction costs.

#### State test operator

The state test operator `IS` asks about the cardinality and state of a value rather than about its contents. Its left operand is the value being examined, which may be of any type, and its right operand is one of the three state names:

- `x IS ABSENT` -- true when the file records nothing at all for `x`
- `x IS USABLE` -- true when an occurrence of `x` can be interpreted as its type
- `x IS UNUSABLE` -- true when an occurrence of `x` is there but cannot be so interpreted

`IS ABSENT` asks about a whole value, and applying a quantifier to its operand is an error. The other two ask about occurrences, and are therefore quantified exactly like the operand of any other operator: existentially by default, universally under `ALL()`. That is the entire guard vocabulary of the language, and the table below is the entirety of its behaviour:

| value of `x`                | `IS ABSENT` | `IS USABLE` | `IS UNUSABLE` | `ALL(x) IS USABLE` | `ALL(x) IS UNUSABLE` |
|-----------------------------|:-----------:|:-----------:|:-------------:|:------------------:|:--------------------:|
| absent                      | true        | false       | false         | false              | false
| one usable occurrence       | false       | true        | false         | true               | false
| one unusable occurrence     | false       | false       | true          | false              | true
| one usable and one unusable | false       | true        | true          | false              | false
| two unusable occurrences    | false       | false       | true          | false              | true

A state test is exempt from general operator rule 1. It also never consumes a value and therefore never warns, even for unusable occurrences.

Applied to a boolean, a state test asks whether a condition could be answered: `(NUMBER(x) > 5) IS UNUSABLE` is true for a file where the comparison met data it could not interpret, and never warns on its own account, although the comparison it examines will already have warned.

`ALL(x) IS USABLE` is the conservative guard: it requires both that `x` is present and that every one of its occurrences can be read. `NOT (x IS UNUSABLE)` is the same except that an absent value also satisfies it.

There is no `IS NOT`; a negated state test is written with the boolean negation operator. For example, `NOT (ALL(x) IS USABLE)` says that not every occurrence is usable, and `NOT (ANY(x) IS USABLE)` says that none of them is.

Examples:

- `ALL(NUMBER(vorbis::bpm)) IS USABLE AND NUMBER(vorbis::bpm) > 120` is the conservative guard: it admits only files where every `BPM` field converted, so the comparison never meets one that did not and nothing warns. A file holding one good `BPM` field and one piece of junk is passed over, silently.
- `ANY(NUMBER(vorbis::bpm)) IS USABLE AND NUMBER(vorbis::bpm) > 120` is the optimistic counterpart: it proceeds when at least one field converted, can still find a match among those, and warns about the rest.
- `year IS ABSENT` selects the files that record no year at all, which no comparison can express, since every comparison against an absent value is false whichever operator it uses
- `ANY(vorbis::year) IS UNUSABLE` selects the files whose Vorbis date fields need attention, and is the predicate to reach for when a run has reported warnings and the data behind them has to be found

#### Boolean operators

An expression inside a predicate can be negated with the `NOT` operator. For example, `year < 2000` and `NOT year >= 2000` are logically and functionally equivalent when `year` is a single usable occurrence.

**IMPORTANT** This equivalence does _not_ hold when `year` is absent. An absent operand makes every comparison false (see general operator rules above), so `year < 2000` evaluates to false, while `NOT year >= 2000` evaluates to `NOT false`, i.e. true. Nor does it hold when `year` is a multivalue, for the reason described under [Multivalues](#multivalues). Both are the same phenomenon, and it affects every comparison operator alike: a comparison such as `year < 2000` and its complement `year >= 2000` are complementary only for a value of cardinality one, an absent value making both false and a multivalue possibly making both true.

A single unusable occurrence of `year` does not break the equivalence. `year < 2000` and `year >= 2000` are both unusable, and so is `NOT year >= 2000`, since negating a comparison that could not be answered does not answer it.

Two expressions can be combined with the `AND` and `OR` logical operators. For example, `year < 2000 AND artist == "metallica"`.

Each of the three logical operators produces an unusable result exactly when an unusable operand leaves the answer open, and true or false whenever the other operand settles it:

| `a`      | `b`      | `a AND b` | `a OR b` | `NOT a`  |
|----------|----------|:---------:|:--------:|:--------:|
| true     | true     | true      | true     | false    |
| true     | false    | false     | true     | false    |
| false    | false    | false     | false    | true     |
| unusable | true     | unusable  | true     | unusable |
| unusable | false    | false     | unusable | unusable |
| unusable | unusable | unusable  | unusable | unusable |

The table is symmetrical, so `b AND a` and `b OR a` give the same results as `a AND b` and `a OR b`. A logical operator never consumes its operands and never warns.

The `AND` and `OR` operators are _short-circuiting_: their operands will always be evaluated in the (left-to-right) order of appearance in the expression, and the second operand will only be evaluated if the result of the operator cannot be determined after having evaluated the first operand. For example, in the expression `genre == "metal" and year between 1970..1980`, if `genre == "metal"` evaluates to false then the sub-expression `year between 1970..1980` will not be evaluated at all because we already know the operator's result will be false. An `AND` is therefore settled by a false first operand and an `OR` by a true one; an unusable first operand settles neither, so the second operand is evaluated, and may itself warn. Since operands are never reordered, the order they are written in also decides what a predicate costs to evaluate.

This short-circuiting behavior is intended to allow a [state test](#state-test-operator) to be used for checking that a value is fit to use before an operator uses it and emits a warning. A comparison cannot perform that check itself, since by the time it can tell that an occurrence is unusable it has already consumed it; refer to the state tests above for the guard idiom. A state test never evaluates to unusable, so a guard always settles an `AND` when it fails.

The operands of `AND`, `OR`, and `NOT` must be [definite](#definite-expressions) booleans, and it is an error to apply them to anything else. In practice this means their operands are comparisons, state tests, other logical expressions, the literals `TRUE` and `FALSE`, or a `FALLBACK()` or `PREFERRED()` of these, optionally parenthesized.

#### Grouping, precedence, and associativity

Expressions can be grouped with parentheses ( ). A parenthesized sub-expression is evaluated as a unit before anything outside the parentheses, regardless of what operators surround it. For example, `(genre == "jazz" OR genre == "blues") AND year < 2000` first evaluates the genre comparison, then combines the result with the year comparison.

Grouping is all that parentheses do: a parenthesized expression is in every other respect the expression it encloses. It has the same type, is definite when that expression is, is a literal when that expression is one, and is modified in the same way, so `(ALL(genre)) == "x"` is the same predicate as `ALL(genre) == "x"`. The one place parentheses cannot go is where the grammar itself places a literal: inside a range, or as the pattern of `=~`.

When parentheses are not used to make evaluation order explicit, operators are evaluated according to a fixed precedence, from tightest-binding to loosest-binding:

| Level        | Constructs                                                                            | Associativity   |
|:------------:|---------------------------------------------------------------------------------------|-----------------|
| 1 (tightest) | primary expressions: literals, identifiers, function calls, parenthesized expressions | --
| 2            | `==` `!=` `<` `>` `<=` `>=` `=~` `BETWEEN` `IS`                                       | non-associative
| 3            | `NOT`                                                                                 | unary, repeatable
| 4            | `AND`                                                                                 | left
| 5 (loosest)  | `OR`                                                                                  | left

An operator of higher precedence binds more tightly than one of lower precedence, meaning it is evaluated first. `NOT` binds more tightly than `AND`, which in turn binds more tightly than `OR`. For example, `artist == "metallica" AND year > 2000 OR NOT genre == "jazz"` is evaluated as if it had been written `((artist == "metallica") AND (year > 2000)) OR (NOT (genre == "jazz"))`, since `AND` binds more tightly than `OR`, `NOT` binds more tightly than `AND`, and the comparisons bind more tightly than `NOT`.

When a left-associative operator appears more than once in a chain without parentheses, it is evaluated left to right. For example, `a AND b AND c` is evaluated as `(a AND b) AND c`. This does not change the result for `AND` or `OR` chains, but it does determine the order in which operands are evaluated. `NOT` is a unary prefix operator and may be applied repeatedly, so `NOT NOT a` is valid and equivalent to `a`.

The comparison operators, the regex operator `=~`, the range operator `BETWEEN`, and the state test operator `IS` are non-associative and cannot be chained. `a == b == c` is a syntax error rather than being read as `(a == b) == c`. A condition of that kind must be written with an explicit logical operator, as in `a == b AND b == c`. Where comparing the outcome of one comparison with another is really what is meant, the parentheses say so: `(a == b) == (c == d)` is valid, its operands being booleans.

Parentheses should be used whenever the default precedence might not match the reader's expectation, even if they are not strictly required to produce the intended result.

### Multivalues

Some identifiers do not correspond to a single value because the underlying data source allows defining a value multiple times (e.g. Vorbis comments). In such cases, an identifier corresponds to a bag of occurrences instead of a single one, as described under [Values](#values): an unordered collection whose occurrences are not deduplicated, so the same datum occurring twice in the underlying tags occurs twice in the multivalue. A multivalue has a type like any other value, and each of its occurrences is usable or unusable. It is even possible for _every_ occurrence of a multivalue to be unusable. What a multivalue can never hold is an absence, since absence is a property of a whole value rather than of an occurrence.

When an operand is multivalue, it is evaluated according to a _quantifier_: existential (the default behavior) or universal (obtained by wrapping the operand in `ALL()`, see below). An operand's quantifier applies independently of any other operand's -- for example, in a comparison of two multivalues, one operand can be evaluated existentially while the other is evaluated universally.

When one or more operands are multivalue, the operator's result is calculated by nested iteration over the multivalue operands' occurrences, with the operator's test applied once to every combination of occurrences reached this way. An existentially-quantified operand contributes a true result if any one of its occurrences leads to one; a universally-quantified operand requires every one of its occurrences to lead to a true result.

A combination that includes an unusable occurrence has no answer of its own, so a quantifier gathers three kinds of outcome rather than two, and settles on true or false wherever the outcomes it has allow:

- an existentially-quantified operand is true if any of its occurrences leads to true; otherwise it is unusable if any leads to unusable; otherwise it is false;
- a universally-quantified operand is false if any of its occurrences leads to false; otherwise it is unusable if any leads to unusable; otherwise it is true.

These are the rules of the logical operators applied across a bag: an existential quantifier is an `OR` over the occurrences, and a universal one an `AND`.

**An operator is a single quantifier scope.** The iteration belongs to the operator, and every one of its value operands is reached within it, which is what makes an operator's test indivisible: a construct that looks like two operators joined by a logical operator has two scopes and is a different proposition, as the `BETWEEN` example above shows.

**The iteration does not short-circuit.** Every combination is evaluated even after the operator's result is settled, so the warnings a file produces never depend on the order of the occurrences in a bag. This is the one place where Goro declines to short-circuit: `AND` and `OR` do, and the [guard idiom](#state-test-operator) relies on it.

The loops are nested **by quantifier, not by position**: a universally-quantified operand always forms a loop outside any existentially-quantified one. Where two operands carry the same quantifier the nesting between them is immaterial, so which side of an operator an operand is written on never changes the nesting and therefore never changes the result. That is a claim about the quantifiers and not about the operators themselves: `a < b` and `b < a` remain different propositions, as always. Should an operator ever take more than two value operands, the same rule applies: universals outermost, existentials innermost, with the written order breaking ties among operands of the same quantifier.

An unusable occurrence participates in this iteration like any other, with general operator rule 2 applying to it: every combination that includes it is unusable. Under the default existential quantifier an unusable occurrence therefore cannot prevent a match that another occurrence supplies, and under `ALL()` it cannot prevent a mismatch that another occurrence supplies; what it prevents is an answer that would have depended on it. For example, if `vorbis::year` has two occurrences, one holding `1991` and one holding data that is not a date at all, then `vorbis::year == 1991` is true, because the first occurrence settles it; `ALL(vorbis::year) == 1991` is unusable, because whether every year is 1991 turns on the one that cannot be read; and `ALL(vorbis::year) == 2000` is false, because the first occurrence already shows that not every year is 2000.

Absence, by contrast, does not participate in this iteration at all: an absent operand makes the operator false before any iteration begins, by general operator rule 1. Universal quantification is therefore not vacuous: `ALL(genre) == "rock"` is false for a file with no genre at all, where ordinary quantifier semantics would make it true.

Examples:

- `genre == "metal"` matches when there are both "metal" and "rock" genre tags present, because `genre` is (by default) existentially quantified and at least one tag is equal to "metal"
- `ANY(genre) != "metal"` _also_ matches when there are both "metal" and "rock" genre tags present, because at least one tag is _not_ equal to "metal"
- `ALL(genre) == "metal"` does _not_ match in the same scenario, because not all of the values match "metal" once `genre` is universally quantified
- `genre BETWEEN "a".."b"` matches when any of the genre tags present begins with "a" or "b"

The regular expression operator also works transparently with multivalues in the same way: it matches when _any_ of the multiple values match the regular expression, by default.

**IMPORTANT** This behavior means that the example expressions `ANY(genre) != "metal"` and `NOT genre == "metal"`, which are strictly complementary if `genre` is a simple value, are no longer complementary if it is a multivalue. It is the same phenomenon described for an absent value under [Boolean operators](#boolean-operators), seen from the other side, and it is why `!=` requires the quantifier to be written; see [Comparison operators](#comparison-operators).

Examples:

- `ANY(genre) != "metal"` _does_ match when there are both "metal" and "rock" genre tags present, because at least one tag is _not_ equal to "metal"
- `NOT genre == "metal"` _does not_ match when there are both "metal" and "rock" genre tags present, because `genre == "metal"` matches and the negation operator inverts the match
- `ALL(genre) != "metal"` _does not_ match either, because not every tag is something other than "metal"

#### Comparisons between two multivalues

When **both** operands to an operator are multivalue, the quantifier carried by each one determines how the two are combined. In all three cases the operands are interchangeable, since nesting follows the quantifiers rather than the order the operands were written in.

If **neither** operand is wrapped in `ALL()` -- the default, existential case for both -- the result is true if and only if the operator holds for at least one pair of values, one taken from each operand. So when `a` and `b` are both multivalues, `a < b` is true if there is some value `x` in `a` and some value `y` in `b` for which `x < y`.

For example, suppose `a` is a multivalue of `1` and `2`, and `b` is a multivalue of `2` and `3`:

- `a == b` is true, because the equality holds for at least one pair (specifically, exactly one: `2` matches `2`)
- `ANY(a) != ANY(b)` is also true, because the inequality holds for several pairs (e.g. `1` does not match `2`, `2` does not match `3`)

If **both** operands are wrapped in `ALL()`, the result is true if and only if the operator holds for _every_ pair of values. This is well-defined but often degenerate: `ALL(a) == ALL(b)` can only be true if every value in `a` and every value in `b` are all the exact same value.

If **exactly one** operand is wrapped in `ALL()`, that operand forms the outer loop whichever side it was written on. The result is true if and only if, for _every_ value of the universally-quantified operand, there is _some_ value of the other operand for which the operator holds.

For example, suppose `a` is a multivalue of `1` and `2`, and `b` is a multivalue of `1`, `2` and `3`:

- `ALL(a) == b` is true: for every value in `a` there is a matching value in `b`
- `b == ALL(a)` is true as well, since it says exactly the same thing
- `ALL(b) == a` is false: `3` has no match in `a`

For the equality operator this reads naturally as a containment test: `ALL(a) == b` is true precisely when every value of `a` also occurs in `b`.

For an ordering operator the same rule reads as a comparison of aggregates, which is worth seeing once. Taking `a` of `1` and `2` and `b` of `2` and `3` again, `ALL(a) < b` is true, because every value of `a` is below *some* value of `b` -- which is to say because the largest value of `a` is below the largest value of `b`. It does not say that every value of `a` is below every value of `b`; that is what `ALL(a) < ALL(b)` says, and it is false here.

### Value conversions

With the exception of numeric literals, operators that have multiple operands require the operands to have the same type and produce an error if not. This means that a comparison such as `id3v2::track > vorbis::raw::tracknumber`, which attempts to compare a number with a string, is rejected with an error. To prevent this error a conversion of one of the values to the type of the other is required. Converting `id3v2::track` to a string might result in a comparison such as `"2" > "10"` -- this comparison result is, perhaps surprisingly, true, and it would be correct to instead write `id3v2::track > NUMBER(vorbis::raw::tracknumber)` to avoid this problem and compare numerically. However, `vorbis::raw::tracknumber` might not hold a valid numeric string, and in that case `NUMBER()` yields an unusable occurrence and the comparison is unusable rather than true or false, by general operator rule 2. The warning that consuming an unusable occurrence emits is what keeps this from happening silently, and a [state test](#state-test-operator) is how a predicate can decide for itself what to do about it.

#### Unusable occurrences

Unusable occurrences arise in three ways:

- from an identifier whose underlying tag data is present but cannot be interpreted as the identifier's declared type;
- from a type conversion function whose input cannot be converted; in particular, `NUMBER()` produces an unusable number when its input cannot be converted;
- from a comparison, range or regex operator that could not reach an answer because of an unusable occurrence among its operands; its result is an unusable boolean.

The first two are where a defect is found, and consuming the occurrence they produce causes Goro to emit a warning to alert you, as described under [Warnings](#warnings) below. The third is a consequence of the first two rather than a further defect.

Three constructs can examine an unusable occurrence without using it, and therefore without warning. A [state test](#state-test-operator) reports state rather than content, so `ALL(NUMBER(x)) IS USABLE` establishes that every occurrence of `x` converted to a number without reporting the ones that did not. `FALLBACK(expr, literal_default)` substitutes for an occurrence instead of reading it, so it is silent for an unusable occurrence exactly as it is for an absent value. And `PREFERRED()` chooses among its arguments by the state of their occurrences, passing over an argument with nothing usable in it without a word.

Because identifiers can produce unusable occurrences of their own, state tests and `FALLBACK()` are useful applied directly to identifiers and not only to the results of conversions. `FALLBACK(id3v2::track, 0)` substitutes zero both for a track frame that is missing and for one that is present but unusable, and `ANY(id3v2::track) IS USABLE` is the way to require that at least one usable track number exists; if `ANY` were replaced with `ALL`, the test would require that _all_ potentially existing track numbers are usable.

Both apply equally to the result of a comparison, so that `FALLBACK(year < 2000, TRUE)` is true for a file whose year cannot be read. `FALLBACK()` replaces the comparison's result, but cannot undo the warning the comparison emitted while producing it.

### Errors

Goro reports every error it possibly can when the predicate is read, before any file is processed.

Reporting errors this early is possible because the type of every sub-expression in a predicate is known statically, as described under [Values](#values), and so is whether it is [definite](#definite-expressions). A file decides only a value's cardinality and the state of each of its occurrences, never its type, and never the cardinality of a definite expression. Checking therefore needs no file at all.

The errors reported when a predicate is read include:

- syntax errors of any kind, including a non-ASCII character in an identifier, a chained comparison such as `a == b == c`, a reserved word used as a bare identifier, and a qualified name used as a function call;
- a reference to an identifier that is not defined, where the namespace it names is a closed one;
- a type mismatch between the operands of an operator, outside the numeric literal exception;
- an operand of `AND`, `OR` or `NOT`, or a predicate as a whole, that is not a definite boolean;
- an operand of `!=` that is not definite and carries neither `ALL` nor `ANY`;
- a boolean used as an operand of an ordering operator, of `BETWEEN`, or of `=~`, booleans being unordered;
- an argument of the wrong type to a function, such as a boolean passed to `NUMBER()` or `STRING()`;
- `LITERALLY()` applied to an operand that is not a string, or to the operand of a state test;
- a modifier applied anywhere other than to an operand of a comparison, range, regex, or state test operator, or to another such modifier;
- a range whose endpoints are not literals of the same type, or that could hold nothing, its `min` lying above its `max`;
- a pattern of `=~` that is not a raw string, is not a valid regular expression, or uses a construct the matching engine does not support.

Only conditions that genuinely depend on the contents of a file are left to be discovered during evaluation, and neither of them ever stops a run: a value that is absent, and an occurrence that cannot be interpreted. The first is ordinary; the second is reported as a warning, as described below.

### Warnings

This section covers the warnings a predicate produces. For what a warning is, what else can produce one, where they go, and how they relate to the exit code, see [Warnings](warnings.md).

A predicate emits a warning when an unusable occurrence is consumed while it is being evaluated. Evaluation always continues after a warning: it never interrupts processing, and it never changes the result of the predicate.

#### What counts as consuming an unusable occurrence

**Consuming an unusable occurrence means asking a question of its content.** Only consuming emits a warning. The comparison, range and regex operators are the constructs that ask such questions -- they are where data is turned into a truth value -- and everything else follows from that one rule:

- The comparison, range and regex operators need the content of their operands in order to answer. They consume, so an operator handed an unusable occurrence warns, and its result for the combinations that include the occurrence is unusable.
- `NUMBER()` and `STRING()` map an unusable occurrence to an unusable occurrence. They ask nothing of its content; they _propagate_ it, keeping its source, and they are silent.
- `COUNT()` reads cardinality, which is knowable without interpreting any occurrence. It is silent.
- `FALLBACK()` substitutes for an occurrence instead of reading it. It is silent.
- `PREFERRED()` reads the state of its arguments' occurrences, never their content. It is silent, and the arguments it passes over are discarded unreported.
- A [state test](#state-test-operator) reads state rather than content. It is silent.
- An operator's result is unusable only if the operator consumed an unusable occurrence, which has therefore already been reported. Whatever consumes such a result (for example `AND`, `OR` and `NOT`) cannot report anything new.
- A modifier computes nothing at all, so applying one leaves the operator that follows as the consumer.

This is what allows the guard `ALL(NUMBER(x)) IS USABLE AND NUMBER(x) > 5` to work even when the unusable data originates in `x` itself rather than in the conversion: `NUMBER()` propagates what it was handed, the state test reads the state of the result without consuming it, and `AND` short-circuits before any operator can.

#### Where a warning comes from

An unusable occurrence has a _source_: the identifier or the conversion at which it arose. A function that passes an occurrence through does not change its source. A warning is emitted at the point where the occurrence is consumed, but names its source: in `STRING(id3v1::year) == "1991"` applied to a file whose Id3v1 year field holds something that is not a year, the unusable occurrence arises at `id3v1::year`, is propagated through `STRING()`, and the warning is triggered by the consuming `==` but names `id3v1::year` as the source, which is where the problem actually is.

#### Deduplication

Warnings are deduplicated per file and per source. Evaluating a predicate against one file emits at most one warning for each distinct sub-expression that produced an unusable occurrence; the second and subsequent times the same source is reached while evaluating that same file, nothing further is emitted.

Two sub-expressions are the same source when they apply the same functions, in the same shape, to the same identifiers and literals. How they are written does not matter -- whitespace, letter case, whether a namespace is given explicitly, and which of the two string forms wrote a literal are all irrelevant -- and neither does where in the predicate they appear. The modifiers `ALL()`, `ANY()` and `LITERALLY()` are transparent for this purpose: they never make two otherwise identical sub-expressions into different sources.

In the following examples, `x` and `y` are identifiers that resolve to an unusable occurrence for the file being evaluated, `m` is a multivalue that includes at least one unusable occurrence, and `s` and `t` are identifiers holding strings that are not valid numbers.

| Predicate                          | Warnings | Why                                                                          |
|------------------------------------|:--------:|------------------------------------------------------------------------------|
| `x > 1`                            | 1        | a single source
| `x > x`                            | 1        | both mentions are the same source
| `x < 10 OR x > 20`                 | 1        | the same source, reached twice
| `x > y`                            | 2        | different identifiers are different sources
| `m > 1`                            | 1        | every occurrence of a multivalue shares the source of the identifier that produced it
| `NUMBER(s) > NUMBER(s)`            | 1        | structurally identical sub-expressions are one source
| `NUMBER(s) > NUMBER(t)`            | 2        | the sources differ below the top level
| `ALL(m) == "a" OR m == "b"`        | 1        | `ALL()` is transparent, so both operands have the same source
| `x > 1 AND y > 1`                  | 2        | an unusable first operand does not settle `AND`, so the second is evaluated as well
| `(x > 1) == (x > 1)`               | 1        | the outer `==` uses an unusable boolean, which has already been reported
| `FALLBACK(x > 1, FALSE)`           | 1        | `FALLBACK()` replaces the comparison's result after the comparison has warned
| `PREFERRED(x, y) > 1`              | 1        | neither argument has anything usable, so `x` is chosen and consumed, and `y` is discarded

Deduplication resets for every file. A predicate such as `id3v2::track == 1` applied to a collection in which 500 files have junk where the track number should be will therefore produce 500 warnings, one per affected file.

A sub-expression that is never evaluated never warns. In particular, the short-circuiting of `AND` and `OR` can be used deliberately to avoid a warning; refer to [State tests](#state-test-operator) for the guard idiom that relies on this. Only a first operand that is true or false can short-circuit, though, so a comparison that is itself unusable does not shield what follows it the way a state test does. Within a single operator there is no such escape, since iteration over a multivalue operand is exhaustive: if an operand holds an unusable occurrence and the operator is evaluated at all, the warning is emitted, whether or not some other occurrence already settled the result.

#### What a warning quotes

Each warning quotes the sub-expression responsible, exactly as it was written in the predicate. Where the same source is written in more than one place, the warning quotes the one written first among those that produced an unusable occurrence consumed in that file. A warning does not name a position within the predicate.

### Modifiers

Three constructs -- `ALL`, `ANY`, and `LITERALLY` -- are _modifiers_ rather than functions. A modifier does not compute a new value from its argument; it changes how the operator that consumes the value will treat it. The defaults are existential quantification and normalized comparison, so a modifier is only ever needed to depart from them, or to state a quantifier that `!=` will not assume.

The three are not quite the same kind of thing, which decides what writing one on one side of an operator does to the other side:

- `ALL` and `ANY` choose a **quantifier**, which belongs to the operand it is written on. Each operand of an operator carries its own, and they are independent: one side may be universal while the other is existential.
- `LITERALLY` chooses a **comparison mode**, which belongs to the operator. An operator either normalizes the strings it compares or it does not, and a `LITERALLY` on either operand settles that for the comparison as a whole.

**A modifier may only be applied to an operand of a comparison, range, regex, or state test operator, or to another such modifier**. For `BETWEEN` and `=~` that means the left operand alone, the range and the pattern on the right being parts of the operator rather than operands. Writing one anywhere else is an error. In particular a modifier may not appear as an argument to a function: `LITERALLY(genre) == "Pop"` is valid, while `COUNT(ALL(genre))` and `FALLBACK(LITERALLY(genre), "pop")` are not.

The restriction applies in one direction only. A modifier may be applied to any operand, including one that is itself a function call, so `LITERALLY(FALLBACK(genre, "pop")) == "Pop"` is valid: modifiers go on the outside, functions on the inside.

Modifiers may be stacked as deeply as desired and in any order, a quantifier and a comparison mode being independent choices. `LITERALLY(ALL(genre))` and `ALL(LITERALLY(genre))` mean the same thing. Repeating a modifier changes nothing, so `ALL(ALL(genre))` is simply universal, but stacking contradictory quantifiers, as in `ALL(ANY(genre))`, is an error.

#### Quantifiers

A quantifier decides what it means for an operand that holds several occurrences to satisfy an operator. It is a property of the operand it is written on, so each operand of an operator carries its own; see [Multivalues](#multivalues) for how the two are combined when they differ. Applying a quantifier to an operand that holds a single occurrence has no effect, and the operand is treated exactly as it would have been without it.

**ALL(expr)**
: causes the operand to be evaluated with a _universal_ quantifier when it is a multivalue, instead of the default _existential_ quantifier: an operator using this operand only matches if _all_ of the multiple values present match, rather than _any one_ value as is the default behavior. See [Multivalues](#multivalues) for the full evaluation rules, including how two multivalue operands are combined when their quantifiers differ.

Examples:

- `genre == "rock"` will match when `genre` is a multivalue representing both `"rock"` and `"metal"` because at least one of the values matches
- `ALL(genre) == "rock"` will _not_ match in the same scenario because not all of the values match `"rock"`

In case the operand that `ALL()` is applied to is not a multivalue, the modifier has no effect and the operand is treated exactly as it would have been without it.

**ANY(expr)**
: causes the operand to be evaluated with an _existential_ quantifier when it is a multivalue: an operator using this operand matches when _any one_ of the multiple values matches. This is the default behavior for every multivalue operand, so this modifier changes nothing about how an operand is evaluated. It is needed on an operand of `!=` that is not definite, where the quantifier has to be written (see [Comparison operators](#comparison-operators)), and is otherwise included as an explicit counterpart to `ALL()`, both to allow more expressiveness if desired (`ANY(genre) == "rock"` reads very naturally) and to make a quantifier choice explicit where it might otherwise be unclear at a glance -- for example, when comparing two multivalues where only one side is wrapped in `ALL()`.

#### Comparison modes

A comparison mode decides how an operator compares the strings it is given. There is one such mode, normalization, and one modifier to turn it off. Unlike a quantifier, a mode belongs to the operator rather than to an operand: an operator either normalizes or it does not, and there is no way for one side of a comparison to be normalized while the other is not. Writing the modifier on an operand is how that choice is expressed, and which operand it is written on makes no difference.

**LITERALLY(expr)**
: switches the operator that consumes this operand out of normalized comparison. Use it for case-sensitive matching, for matching on diacritics, and for other cases where the value is to be taken exactly as recorded.

Because the mode belongs to the operator, it is enough for _just one_ operand to carry the modifier -- `LITERALLY(x) == y`, `x == LITERALLY(y)`, and `LITERALLY(x) == LITERALLY(y)` all do exactly the same thing. There is no such thing as a `LITERALLY` value that could be passed around and compared against a normalized one; the modifier is a note to the operator, and the operator obeys it once.

Applied to the subject of `=~`, the modifier also makes the match case-sensitive, and leaves the subject's diacritics in place, which is what allows a pattern to match one.

Where an operand holds several occurrences, the mode applies to the comparison of every one of them, there being only one comparison mode in play. It is an error to apply `LITERALLY()` to an operand of any type other than string, and likewise to the operand of a state test, where there is no comparison for a mode to affect. An absent value and a string multivalue are of course still permitted.

Examples:

- `artist == "metallica"` matches "Metallica" because the equality operator normalizes its inputs by default.
- `artist == LITERALLY("metallica")` does _not_ match "Metallica" because the result of `LITERALLY()` prevents the equality operator from normalizing its inputs before comparing them.
- `artist BETWEEN "m".."m"` matches "Metallica" because, after normalization by default, "Metallica" begins with "m".
- `LITERALLY(artist) BETWEEN "m".."m"` does _not_ match "Metallica" because the result of `LITERALLY()` prevents the range operator from normalizing its inputs, and upper case "M" sorts before lower case "m".
- `artist =~ r"^met"` matches "Metallica" because the match is performed case-insensitively.
- `LITERALLY(artist) =~ r"^met"` does _not_ match "Metallica", because `LITERALLY()` makes the match case-sensitive and "Metallica" begins with a capital "M".
- `artist =~ r"motö"` matches nothing, because diacritics are removed from the subject and the pattern is left as written, so the `ö` in it has nothing to match.
- `LITERALLY(artist) =~ r"otö"` matches "Motörhead", because an unnormalized subject keeps its diacritics.

### Functions

There are a number of built-in functions that can be used inside predicate expressions. Functions are invoked by their name followed by a list of zero or more arguments in parentheses. For example, `FOO(x, y)` is a call to the function `foo` with arguments `x` and `y`.

A function call is always an **unqualified** name followed by an opening parenthesis. Functions do not live in namespaces, so a qualified name is never a function call and `foo::count(x)` is a syntax error. This is what allows a single name to serve both as a function name and, in principle, as an identifier: `count` on its own is an identifier reference, `count(...)` is a call, and the qualified form `::count` always and unambiguously refers to the identifier.

Function arguments follow the same type-matching discipline as operator operands, including the numeric-literal exception. Unless otherwise noted below, a function given an absent argument returns an absent value, and a function given an unusable occurrence returns an unusable occurrence.

The functions that can be used inside predicates are:

**COUNT(expr)**
: returns the number of occurrences of its argument: `0` if `expr` is absent, and otherwise the size of its bag. Unusable occurrences are counted like any other -- data that is there and cannot be read is still there -- and `COUNT()` never consumes one, so it never warns.

The count is of the occurrences an identifier resolved to, which is not always the number of tags they came from. A single Id3v2 `TCON` frame holding `(17)Post-Rock` resolves to two genres, so `COUNT(id3v2::genre)` is 2 for such a file, while a raw namespace resolves one occurrence per tag.

Example:

- `COUNT(genre) > 1` is true if and only if `genre` is a multivalue

**FALLBACK(expr, literal_default)**
: returns `expr` wherever it is usable, and `literal_default` wherever it is not. An absent `expr` becomes a single usable occurrence holding `literal_default`, and an unusable occurrence becomes a usable one holding `literal_default`.

Because this function substitutes for an occurrence rather than reading it, it never consumes one and therefore never warns. It is an error if `literal_default` is not a literal.

If `expr` is a multivalue, this function returns a multivalue of the same cardinality, with `FALLBACK()` applied to each occurrence of the input in turn. Its result is therefore never absent and never contains an unusable occurrence.

`expr` may be of any type, boolean included, and `TRUE` and `FALSE` are literals like any other. Applied to a comparison, `FALLBACK()` decides what a condition that could not be answered should mean; see [Unusable occurrences](#unusable-occurrences), including for why doing so does not silence the comparison.

**NUMBER(expr)**
: converts its argument to a number.

If `expr` is absent, or is already a number, this function returns the same value. It is an error to pass it a boolean, or a string literal that is not a valid number literal. If `expr` is a bytecount, it returns the count as a number of bytes. If `expr` is a duration, it returns total number of seconds. If `expr` is a string that satisfies the rules for a numeric literal, it is converted to a number and returned. If `expr` is a string that does _not_ validate as a numeric literal, or whose value cannot be represented exactly, the result is an unusable occurrence, which will emit a warning the first time an attempt is made to use it. An unusable input yields an unusable result, propagated in silence.

If the argument is a multivalue, this function returns a multivalue of the same cardinality, with `NUMBER()` applied to each occurrence of the input in turn.

**PREFERRED(expr1, expr2, ...)**
: chooses one of its arguments, preferring them in the order they are written. It takes two or more arguments, all of one type, and its result is:

- the first argument holding at least one usable occurrence, taken entire, unusable occurrences included;
- failing that, the first argument that is not absent, every occurrence of which is therefore unusable;
- failing that, absent.

Usability decides the choice, not mere presence: an argument holding only data that cannot be read does not stop the search, and one holding a usable occurrence wins against every argument after it, whatever else it holds.

The arguments are evaluated left to right, and evaluation stops at the first one holding a usable occurrence: the arguments after it are not evaluated at all, just as the second operand of an `AND` settled by its first is not. This function reads only the state of an occurrence, never its content, so it never consumes one and never warns. The unusable occurrences of an argument it passes over are not part of its result, and nothing reports them. Its result is [definite](#definite-expressions) when every argument is.

Taking two arguments, each absent or holding the occurrences shown:

| `vorbis::year`      | `ape::year` | `PREFERRED(vorbis::year, ape::year)` |
|---------------------|-------------|--------------------------------------|
| absent              | usable      | `ape::year`'s value
| unusable            | usable      | `ape::year`'s value; a preferred argument holding junk does not win over a usable one
| usable and unusable | usable      | `vorbis::year`'s value entire, the unusable occurrence included
| unusable            | absent      | `vorbis::year`'s value, all of it unusable; a defect is passed on rather than passed off as absence
| absent              | absent      | absent

Every identifier in the global namespace has the value of such a call; see [built-in identifiers](../features/builtins/identifiers.md).

Examples:

- `PREFERRED(vorbis::year, id3v2::year) < 2000` compares the Vorbis year of a file that has a usable one, and its Id3v2 year otherwise
- `PREFERRED(ape::"album artist", vorbis::albumartist) == "various artists"` looks for an album artist where the global namespace does not

**STRING(expr)**
: converts its argument to a string.

If `expr` is absent, or is already a string, this function returns the same value. It is an error to pass it a boolean. If `expr` is a bytecount, it returns the count of bytes as a plain number without a unit. If `expr` is a duration, it returns the total duration in seconds as a plain number without a unit. If `expr` is a number, it returns that number.

In every case the text is a number literal in one canonical form: always an integer part, a sign only for a negative number, no negative zero, no thousands separator, no exponent, and no trailing zeros in the fractional part. So `STRING(1.50)` and `STRING(1.5)` are both `"1.5"`, `STRING(.5)` is `"0.5"`, `STRING(+5)` is `"5"` and `STRING(-0)` is `"0"`, and the result always reads back through `NUMBER()` as the value it came from. Every value of every type it accepts has a string form, so this function never produces an unusable occurrence of its own; an unusable input yields an unusable result, propagated in silence.

If the argument is a multivalue, this function returns a multivalue of the same cardinality, with `STRING()` applied to each occurrence of the input in turn.

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
                | "=~" pattern
                | "IS" state ;

comparison_op   = "==" | "!=" | "<=" | ">=" | "<" | ">" ;
pattern         = raw_string ;
state           = "USABLE" | "UNUSABLE" | "ABSENT" ;

operand         = modifier "(" operand ")"
                | primary ;
modifier        = "ALL" | "ANY" | "LITERALLY" ;

primary         = literal
                | identifier
                | function_call
                | "(" expression ")" ;

function_call   = name "(" [ expression { "," expression } ] ")" ;
```

The four levels of `or_expr`, `and_expr`, `not_expr` and `comparison_expr` are what give the operators the precedence listed under [Grouping, precedence, and associativity](#grouping-precedence-and-associativity). `comparison_tail` appears at most once and never recurses, which is what makes the comparison, range, regex and state test operators non-associative.

The grammar admits a modifier wherever an `operand` can occur, and since the tail of a `comparison_expr` is optional, that is wherever an expression can occur. The restriction described under [Modifiers](#modifiers) is therefore completed by static analysis: `COUNT(ALL(genre))` parses, with `ALL(genre)` as the argument of `COUNT`, and is rejected. This is what the modifier names are reserved for, since recognizing one is what allows a misplaced modifier to be reported as such. The recursion in `operand` is what allows modifiers to be stacked without limit. Parentheses change nothing here, as everywhere else: `(ALL(genre)) == "x"` is accepted, its modifier being applied to the operand of `==`, and `COUNT((ALL(genre)))` is rejected exactly as `COUNT(ALL(genre))` is.

### Identifiers and names

```ebnf
identifier      = ( name | "::" name_part ) { "::" name_part } ;
name_part       = name | string ;
name            = letter { letter | digit | "_" } ;

letter          = "A" .. "Z" | "a" .. "z" ;
digit           = "0" .. "9" ;
```

### Literals

```ebnf
literal         = string | number | bytecount | duration | boolean ;
range           = literal ".." literal ;

boolean         = "TRUE" | "FALSE" ;

string          = quoted_string | raw_string ;

quoted_string   = '"' { string_char | escape } '"' ;
string_char     = <any character other than '"' and "\"> ;
escape          = "\" ( '"' | "\" | "n" | "r" | "t" )
                | "\x" hex hex
                | "\u" hex hex hex hex
                | "\u{" hex [ hex [ hex [ hex [ hex [ hex ] ] ] ] ] "}" ;

raw_string      = ( "r" | "R" ) '"' { raw_char | '""' } '"' ;
raw_char        = <any character other than '"'> ;

hex             = digit | "a" .. "f" | "A" .. "F" ;

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
duration_clock  = digits ":" two_digits [ ":" two_digits ] ;
two_digits      = digit digit ;
```

### Tokens

The rules for dividing predicate text into tokens are given under [Tokens and whitespace](#tokens-and-whitespace) and are not restated here. Four consequences bear on the productions above.

The `bytecount`, `duration` and `raw_string` productions are each a single token, so no whitespace may appear anywhere within them, including between a `raw_string`'s `r` and its opening quote.

An `identifier` is made of several tokens, but no whitespace may appear between them.

Inside a `raw_string`, a pair of quotes is taken as the escaped quote whenever two appear together, so the string ends only at a quote standing alone. `r"a""b"` is therefore the three characters `a"b`, and a raw string holding a single quote is written with four in a row.

An identifier that happens to be named `r` is unaffected by the raw string prefix wherever whitespace, an operator or a `::` follows it, since an `identifier` is never followed by a `string` in any production.

### Constraints not expressed by the grammar

Not every rule in this document is grammatical, and a construct that this grammar accepts may still be rejected. The following constraints are enforced by static analysis of a parsed predicate rather than by the grammar, and all of them are reported as [errors](#errors) when the predicate is read:

- every rule below treats a parenthesized expression exactly as the expression it encloses, so a `literal` in parentheses is a literal and a modified `operand` in parentheses is still a modified operand; where the grammar itself requires a literal, as `range` and `pattern` do, parentheses are not part of it;
- a `predicate` must be a definite expression of boolean type, so a bare `primary` such as `artist` parses but is not a valid predicate;
- the operands of an operator must have the same type, subject to the numeric literal exception, and the operands of `AND`, `OR` and `NOT` must be definite booleans;
- an `operand` of `!=` that is not definite must carry `ALL` or `ANY`;
- booleans are unordered, so a boolean may not be an operand of `<`, `<=`, `>`, `>=`, `BETWEEN` or `=~`, nor an endpoint of a `range`;
- the `operand` of `=~` must be a string, and its `pattern` must be a valid regular expression using only the constructs the matching engine supports;
- the `operand` of a state test may be of any type, boolean included, since a state test asks about cardinality and state rather than about content;
- an `identifier` must name a namespace Goro defines, an `identifier` in a closed namespace must be one the namespace defines, and an identifier's first `name` may be one of the reserved words only when the identifier begins with `::`;
- a `name_part` written as a `string` names the same thing as the equivalent bare `name` when the name is one a bare `name` could have spelled, so the two forms are one identifier and not two;
- a `function_call` names an existing function and supplies it with the number and types of arguments it accepts; in particular the second argument of `FALLBACK()` must be a literal, `PREFERRED()` takes at least two arguments, all of one type, `NUMBER()` and `STRING()` do not accept a boolean, and `NUMBER()` does not accept a string literal that is not a valid number literal;
- the `operand` a `LITERALLY` modifier is applied to must be of type string, and must not be the operand of a state test;
- `ALL` and `ANY` may not both be applied to the same operand, and neither may be applied to the operand of `IS ABSENT`;
- the two endpoints of a `range` must be literals of the same type, and the range must be able to hold something: `min` must not lie above `max` when compared with it as an occurrence would be, which for strings means cut to the length of `max`, and compared the way the operator will compare them, so that a `LITERALLY` on the operator's other operand decides whether string endpoints are compared normalized;
- a `number` that stands for a `bytecount` or a `duration` must not be negative, and one that stands for a `duration` must be a whole number;
- at least one of the three unit groups of a `duration_units` must be present, so the empty string does not satisfy that production;
- each `two_digits` of a `duration_clock` must denote a value between 0 and 59 inclusive, while its leading `digits` is unbounded;
- a `number`, `bytecount` or `duration` must have a value that can be represented exactly.
