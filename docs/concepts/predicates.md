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

### Tokens and whitespace

A predicate is read as a sequence of _tokens_: literals, identifiers, operators and punctuation. Two rules govern how the text is divided into them, and both come up often enough to belong here rather than in the grammar.

**A token is always as long as it can be.** Where a stretch of text could be read as one longer token or as two shorter ones, the longer reading wins. This is what makes `5mb` a bytecount rather than a five-minute duration followed by a stray `b`, `1h10m` a single duration rather than three tokens, `r"x"` a raw string rather than an identifier followed by a string, and `NOTx` an identifier rather than the `NOT` operator applied to something called `x`.

**Whitespace separates tokens and is insignificant everywhere else.** The amount and kind of whitespace between two tokens is ignored, but whitespace may never appear *inside* one: `10 kb` and `1h 10m` are not literals, and `r "x"` is not a raw string. Whitespace is therefore required wherever two tokens would otherwise run together into one, which is the whole of why `NOT x` needs its space. Inside a string value whitespace is preserved exactly, so `"foo"` and `"foo "` are not equal.

### Case sensitivity

Predicate syntax is case-insensitive throughout, using invariant culture rules. The keyword operators (`AND`, `OR`, `NOT`, `BETWEEN`, `IS`), the state names (`USABLE`, `UNUSABLE`, `ABSENT`), the reserved words `NULL`, `TRUE` and `FALSE`, function names, identifiers, namespaces, and the unit suffixes of bytecount and duration literals are all matched without regard to case. `artist`, `Artist` and `ARTIST` are the same identifier, and similarly `id3v2::tit2` and `ID3V2::TIT2` are the same identifier.

The only case-sensitive text in a predicate is the contents of a string value -- and even those are converted to lowercase before being compared, unless `LITERALLY()` is used to prevent it. The letters of a string escape are a second and smaller exception: the escapes are the ones listed under [Values](#values) and no others, so `\n` is a line feed while `\N` is not an escape at all and is therefore an error. The hexadecimal digits of `\x` and `\u` may be written in either case, and the `r` prefixing a raw string may be either `r` or `R`, both being notation around a string rather than part of its contents. A quoted part of an identifier is not a string value in this sense and is matched without regard to case like any other part of an identifier, so `ape::"album artist"` and `ape::"Album Artist"` are the same identifier.

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

For a list of predefined namespaces and identifiers within them together with a description of each one, see [built-in identifiers](../features/builtins/identifiers.md). A namespace is either _closed_, meaning that the identifiers it contains are known in advance, or _open_, meaning that it additionally admits identifiers whose names are derived from the tag data of the file being examined. Referring to an identifier that is not defined in a closed namespace is an error; an identifier in an open namespace is always accepted, and simply resolves to an absent value when the file holds no data for it.

### Reserved words

The keyword operators `AND`, `OR`, `NOT`, `BETWEEN`, and `IS` (case-insensitive) are reserved and cannot be used as a bare identifier in the global namespace. For example, `and` alone is always interpreted as the logical `AND` operator, never as an identifier named "and".

The state names `USABLE`, `UNUSABLE`, and `ABSENT` (case-insensitive) are reserved on the same terms, since they are what the right-hand side of the `IS` operator is written with.

The keywords `NULL`, `TRUE`, and `FALSE` (case-insensitive) are also reserved and cannot be used as a bare identifier in the global namespace. None of the three names anything that exists: Goro has no null value, and it has no boolean literals. They are reserved so that a predicate written in the expectation that they do exist -- `year == NULL` being the obvious one -- can be told what to write instead of being reported as a reference to an undefined identifier.

This restriction applies only to bare, unqualified identifiers. If an identifier happens to coincide with a keyword, it can still be referenced by explicitly qualifying it, e.g. `::and`.

The modifiers `ALL`, `ANY`, and `LITERALLY` (case-insensitive) are reserved on the same terms. They are reserved because, unlike a function, a modifier may only appear in one specific position, and recognizing the name is what allows a misplaced modifier to be reported as such instead of as a call to an undefined function.

Function names (such as `COUNT`, `FALLBACK`, `NUMBER`, and `STRING`) are not reserved words in this sense.

Although the grammar does not require it, no built-in identifier in the global namespace has the same name as a function, and no function is given the name of a built-in global identifier. This is a constraint on what Goro defines rather than on what a predicate may write.

### Values

Values in predicates come from _identifiers_ (preagreed names to refer to various types of data specific to each file) and _literals_ (values specified directly when writing a predicate). For example, in the predicate `year == 2000`, `year` is an identifier and `2000` is a literal value.

Every value has three aspects, and only the first of them is decided by the text of the predicate.

**Type** : A value's type dictates what operations may be performed on it and how their results are calculated. The types are `string`, `number`, `bytecount`, `duration` and `boolean`, and they are described below. A value's type never depends on the file being examined: identifiers have a declared type, each function has a fixed result type, and literals are typed by their syntax. This is what allows every type error in a predicate to be reported before any file is opened.

**Cardinality** : A value is either **absent**, meaning that the file records nothing for it, or a bag of one or more _occurrences_. A bag is unordered and its occurrences are not deduplicated, so the same datum recorded twice in the underlying tags occurs twice in the bag. A bag of exactly one occurrence is indistinguishable from a simple value: no construct in the language can tell the two apart, and everything this document says about simple values holds unchanged for a bag of cardinality one. Absent is the value of cardinality zero, and it is the only one -- there is no empty bag distinct from absence.

**State** : Each occurrence in a bag is either **usable**, meaning that its data can be interpreted as the value's type, or **unusable**, meaning that the data is there but cannot be so interpreted. A tag field holding `nineteen ninety one` where a year is expected is one unusable occurrence. An unusable occurrence takes part in every operation exactly as a usable one does, with a single difference: using it emits a warning, because it marks a defect in the file that somebody will want to go and fix. Absent data, by contrast, is ordinary, and never warns on its own account.

Absence is a property of a whole value, while unusability is a property of a single occurrence. From this it follows that **a bag never contains an absence**: an occurrence that is not there is not an occurrence. Where a resolution rule discards an occurrence entirely, it lowers the value's cardinality, and a value all of whose occurrences are discarded is absent.

These three aspects are the whole of the model. There is no fourth state, there is no null, and none of the three can be written as a literal: a file decides cardinality and state, and nothing in a predicate can state them directly. What a predicate can do is _ask_, with the [state test](#state-test-operator) operator.

The types that Goro recognizes are:

**boolean**
: represents a binary logical state (true or false). Boolean is the one type with no dynamic aspect: a boolean is always a single usable occurrence, never absent and never unusable, because the operators that produce booleans produce false rather than nothing when handed an operand that is absent or unusable. There is no way to write boolean literals in expressions; boolean values are produced by the various comparison, state test and logical operators described later in this document. Boolean values may only appear as operands of the logical operators `AND`, `OR`, and `NOT`, and as the result of a predicate as a whole. It is an error to use a boolean value anywhere else, in particular as an operand of a comparison, range, or regex operator, or as an argument to any function.

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

A backslash followed by anything else is an error rather than a literal backslash, so text written with single backslashes is rejected instead of being silently misread. Both `\x` and `\u` take a fixed number of digits, which is what makes `"\x41B"` unambiguously `A` followed by `B`. The escapes denote UTF-16 code units, so a character outside the Basic Multilingual Plane is written as a surrogate pair.

A **raw string** carries the prefix `r` and processes nothing whatsoever: every character between the quotes is part of the value, and a backslash is simply a backslash. This is the form to reach for when writing a regular expression, where a quoted string would need each of its backslashes doubled -- `r"\d{4}"` and `"\\d{4}"` denote the same pattern, and the first is the one worth reading. A double quote inside a raw string is written by doubling it, so `r"say ""hi"""` is the value `say "hi"`.

Where a pattern is concerned the two forms often agree, because a .NET pattern understands the same five character escapes: `"\x41"` hands the pattern a literal `A` while `r"\x41"` hands it the escape, and both match `A`. They part company over everything else a pattern needs -- `\d`, `\s`, `\b`, `\p{...}`, and a literal backslash -- which is meaningful only to the pattern and therefore wants a raw string.

The two forms differ in nothing but how the text between the quotes is read. A raw string has string type like any other, may appear anywhere a quoted string may, including as a quoted part of an identifier, and two literals denoting the same characters are indistinguishable regardless of which form wrote them.

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
: a _multivalue_ is the name this document gives to a bag of cardinality greater than one: the simultaneous existence of several occurrences where a single value might otherwise be expected. Multivalues cannot be specified as literals, but they arise whenever the same kind of data is recorded more than once within tags. For example, the identifier `artist` might be absent (if there is no artist tag of any kind), a simple string value (if there is exactly one artist tag), or a multivalue (e.g. if there are multiple Vorbis comments with the `Artist` key).

The concept of multivalues is intended to enable clear and precise explanations of how Goro behaves in the presence of multiple tags that provide the same data, and is presented in more detail in its own section below.

### Operators

Values within Goro predicates can be compared with the _comparison operators_ `==` (equality), `!=` (inequality), `<` (less than), `>` (greater than), `<=` (less than or equal), and `>=` (greater than or equal). There are also _boolean_ or _logical operators_ that appear as words instead of symbols: `NOT`, `AND`, and `OR`.

#### General operator rules

There are some **fundamental rules which apply globally** to any operator sub-expression:

1. **An absent operand makes a comparison, range, or regex operator false**, regardless of what its other operands might be, and iteration over any multivalue operand does not run at all. For example, if there is no Id3v2 tag, the predicates `id3v2::track == 5` and `id3v2::track != 5` will _both_ evaluate to false, because `id3v2::track` is absent in both cases. This rule does not concern the logical operators `AND`, `OR`, and `NOT`, whose operands are always boolean and therefore never absent; nor does it concern the state test operator `IS`, whose purpose includes observing absence.
2. **An unusable occurrence makes false every combination of operand values that includes it**, and consuming it emits a warning. Under the default existential quantifier an unusable occurrence therefore simply fails to contribute a match, whereas under `ALL()` a single one is enough to make the operator false. See [Multivalues](#multivalues) for what a combination is, and [Warnings](#warnings) for what counts as consuming one.
3. If an operator has multiple operands, it is an error for the operands to have different types. For example, `file::duration > "01:00"` is an error because `file::duration` has duration type and `"01:00"` is a literal of type string. Instead, `file::duration > 01:00` is correct because `01:00` without quotes is a valid duration literal as described earlier.
4. There is a very important exception to the previous rule: _numeric literals (but not non-literal numeric values!) are polymorphic and can be compared with any type_. In particular, they are automatically converted to the correct type (that of the other operand): for duration type, the converted value is a duration of the given number of seconds, which may have a fractional part even though durations obtained from files never do, and the comparison is then exact -- `file::duration >= 1.5` is false for a file of one second duration and true for a file of two; for bytecount type it represents the given number of bytes; and for string, the converted value is the decimal string representation of the number. This is also true for range literals; `file::size BETWEEN 60..120` compares `file::size` (a bytecount) with `60..120` (a range of numbers, not bytecounts), but the number range literal is automatically interpreted as a bytecount range literal of 60 to 120 bytes.
5. When comparing string values, an operator _normalizes_ them by default. Normalization entails a) **removal of diacritics**: the value is decomposed into Unicode NFD form and all combining marks are discarded; and b) **case insensitivity**, achieved by converting the value to lowercase using invariant culture rules. For example, `artist == "motorhead"` matches when the artist is recorded as "Motörhead", despite the difference in both casing and diacritics. Normalization is a **mode of the operator** rather than a property of either value, so it is switched off for the whole comparison by a `LITERALLY()` modifier on either operand; see [Comparison modes](#comparison-modes). The regex matching operator `~=` is normalized differently, since only one of its operands is a value being compared at all; see [Regular expression match operator](#regular-expression-match-operator).

#### Comparison operators

The comparison operators `==`, `!=`, `<`, `<=`, `>`, `>=` directly compare their operands. Comparison between values of type number, bytecount, and duration are trivial; comparison between values of type string is by default _an ordinal Unicode comparison_ that is preceded by _normalization_ of the string values as detailed above (unless the `LITERALLY()` modifier is used to avoid this). An absent operand makes a comparison false whichever comparison it is, as does an unusable occurrence in the combination being evaluated; see the general operator rules above.

#### Range operator

The range operator `BETWEEN` can be used to perform a range inclusion check; it determines if its first operand is part of the (inclusive) range defined by the second operand, for example `year BETWEEN 1990..2000`. An occurrence `x` is part of the range when `min <= x` and `x <= max` both hold of that same occurrence. If the value and range are strings, comparisons follow the same rules as for the simple comparison operators.

`BETWEEN` is **a single operator**, and the two inequalities above describe _how one occurrence is tested_ rather than a rewriting of the predicate. The distinction is invisible for a simple value and decisive for a multivalue, because an operator is one quantifier scope and splitting it into two would create a second: for a value `v` with occurrences `0` and `20`, `v BETWEEN 1..10` is false, because no single occurrence lies in the range, whereas `1 <= v AND v <= 10` is *true*, each of its two comparisons finding a different occurrence to satisfy it. See [Multivalues](#multivalues).

Examples:

- `year BETWEEN 2000..2010` matches if the year is between 2000 and 2010, inclusive.
- `artist BETWEEN "ma".."mi"` matches if the artist would be ordinally sorted somewhere between "ma" and "mi". It would match "Metallica", given that matching against strings is case-insensitive by default due to normalization and "me" sorts between "ma" and "mi".

#### Regular expression match operator

String values can be tested to see if they match a regular expression with the regex matching operator `~=`. The left operand is the **subject**, the value being tested; the right operand is the **pattern**. Both must be of type string, and it is an error if either is not; the polymorphic numeric literal rule (general operator rule 4) does not apply to this operator. Regular expressions are [.NET-flavored](https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expressions), with the restriction described under [Supported constructs](#supported-constructs) below.

The regex matching operator will match if any substring of the subject matches the pattern, so if more exact matching is intended the anchors `^` and/or `$` have to be specified.

**Only the subject is normalized, and the pattern is never touched.** Diacritics are removed from the subject exactly as for any other operator, and case insensitivity is obtained from the match itself rather than by lowercasing anything. A pattern is what the user wrote, character for character.

The consequence to remember is that **a pattern cannot match a diacritic**, because the subject no longer has any by the time the match runs: `artist ~= "motö"` finds nothing at all, while `artist ~= "mot"` matches "Motörhead". Applying `LITERALLY()` to either operand switches the whole comparison to unnormalized, which leaves the subject's diacritics intact and makes the match case-sensitive, and is the way to write a pattern that means to match one. See the [rationale](../design/rationale.md) for why the pattern is exempt from normalization.

If either operand is a multivalue, matching follows the ordinary quantifier and nested iteration rules described under [Multivalues](#multivalues); each individual pattern of a multivalue pattern operand is validated independently.

There is no negated version of this operator; to determine if a value does not match a regular expression, apply `NOT` to the result of matching it.

Examples:

- `artist ~= "s$"` matches any artist whose name ends in "s" or "S"
- `artist ~= r"^\d+ "` matches any artist whose name begins with a number followed by a space. Written as a quoted string the same pattern is `"^\\d+ "`, which is why a raw string is the better habit for patterns

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

See the [rationale](../design/rationale.md) for why this engine was chosen and what the restriction costs.

##### Invalid patterns

A pattern that is not a valid supported regular expression is rejected. Where this happens depends on whether the pattern is known in advance:

- **A literal pattern is validated when the predicate is read**, and a bad one is an error reported before any file is processed, like every other error this document describes.
- **A pattern arriving from tag data cannot be validated in advance.** If it turns out not to be valid when it is evaluated, the operator evaluates to false and a warning is emitted, following the same policy as warnings for unusable occurrences (see [Value conversions](#value-conversions)).

**IMPORTANT** An invalid pattern making the operator evaluate to false has the same consequence under negation as an absent or unusable operand does: `NOT (artist ~= sometag)` evaluates to true when `sometag` is not a valid regular expression, so a file can be selected on the strength of a match that never ran. The warning is what surfaces this; see the discussion of `!=` versus `NOT ==` above for the same underlying phenomenon.

#### State test operator

The state test operator `IS` asks about the cardinality and state of a value rather than about its contents. Its left operand is the value being examined, and its right operand is one of the three state names:

- `x IS ABSENT` -- true when the file records nothing at all for `x`
- `x IS USABLE` -- true when an occurrence of `x` can be interpreted as its type
- `x IS UNUSABLE` -- true when an occurrence of `x` is there but cannot be so interpreted

`IS ABSENT` asks about a whole value, and no quantifier affects it. The other two ask about occurrences, and are therefore quantified exactly like the operand of any other operator: existentially by default, universally under `ALL()`. That is the entire guard vocabulary of the language, and the table below is the entirety of its behaviour:

| value of `x`                | `IS ABSENT` | `IS USABLE` | `IS UNUSABLE` | `ALL(x) IS USABLE` | `ALL(x) IS UNUSABLE` |
|-----------------------------|:-----------:|:-----------:|:-------------:|:------------------:|:--------------------:|
| absent                      | true        | false       | false         | false              | false
| one usable occurrence       | false       | true        | false         | true               | false
| one unusable occurrence     | false       | false       | true          | false              | true
| one usable and one unusable | false       | true        | true          | false              | false
| two unusable occurrences    | false       | false       | true          | false              | true

A state test is exempt from general operator rule 1, since reporting absence is what it is for. It also never consumes a value and therefore never warns, however unusable the data it examined turns out to be. Those two properties together are what make it a guard.

No state test is derivable from the others, because an absent value makes both quantified forms false and so breaks the duality that would otherwise collapse them. In particular `ALL(x) IS USABLE` is the conservative guard -- it requires both that `x` is present and that every one of its occurrences can be read -- whereas `NOT (x IS USABLE)` is a weaker statement that an absent value also satisfies.

There is deliberately no `IS NOT`. Because the quantifiers are explicit and dual, every negated reading is already expressible: `NOT (ALL(x) IS USABLE)` says that not every occurrence is usable, and `NOT (ANY(x) IS USABLE)` says that none of them is. An `IS NOT` would express nothing new while introducing another instance of the `!=` versus `NOT ==` distinction described below.

Examples:

- `ALL(NUMBER(id3v2::raw::TRCK)) IS USABLE AND NUMBER(id3v2::raw::TRCK) > 5` is the safe way to compare a converted value: the guard admits only files where every occurrence converted, and because `AND` short-circuits the comparison never sees an occurrence that did not
- `ANY(NUMBER(id3v2::raw::TRCK)) IS USABLE AND NUMBER(id3v2::raw::TRCK) > 5` is the optimistic counterpart, which proceeds when at least one occurrence converted and accepts a warning about the ones that did not
- `year IS ABSENT` selects the files that record no year at all, which no comparison can express, since every comparison against an absent value is false whichever operator it uses
- `ANY(vorbis::year) IS UNUSABLE` selects the files whose Vorbis date fields need attention, and is the predicate to reach for when a run has reported warnings and the data behind them has to be found

#### Boolean operators

An expression inside a predicate can be negated with the `NOT` operator. For example, `year < 2000` and `NOT year >= 2000` are logically and functionally equivalent when `year` is a usable occurrence.

**IMPORTANT** This equivalence does _not_ hold if `year` is absent, or if the occurrence being compared is unusable. Because such an operand makes every comparison operator evaluate to false regardless of which operator it is (see general operator rules above), `year < 2000` evaluates to false, while `NOT year >= 2000` evaluates to `NOT false`, i.e. true. This is the same underlying phenomenon noted below for `!=` and `NOT ==` on multivalues, and it applies to any comparison operator and its logical complement whenever absent or unusable data is involved, not just `!=`/`==`.

Two expressions can be combined with the `AND` and `OR` logical operators. For example, `year < 2000 AND artist == "metallica"`.

The `AND` and `OR` operators are _short-circuiting_: their operands will always be evaluated in the (left-to-right) order of appearance in the expression, and the second operand will only be evaluated if the result of the operator cannot be determined after having evaluated the first operand. For example, in the expression `genre == "metal" and year between 1970..1980`, if `genre == "metal"` evaluates to false then the sub-expression `year between 1970..1980` will not be evaluated at all because we already know the operator's result will be false.

This short-circuiting behavior is intended to allow a [state test](#state-test-operator) to be used for checking that a value is fit to use before an operator uses it and emits a warning. A comparison cannot perform that check itself, since it evaluates to false whether the value was absent, was unusable, or was simply not a match; refer to the state tests above for the guard idiom.

The operands of `AND`, `OR`, and `NOT` must be of type boolean, and it is an error to apply them to a value of any other type. Since booleans are only ever produced by the comparison, range, and regex operators and by the logical operators themselves, in practice this means their operands must be comparisons or other logical expressions, optionally parenthesized.

#### Grouping, precedence, and associativity

Expressions can be grouped with parentheses ( ). A parenthesized sub-expression is evaluated as a unit before anything outside the parentheses, regardless of what operators surround it. For example, `(genre == "jazz" OR genre == "blues") AND year < 2000` first evaluates the genre comparison, then combines the result with the year comparison.

When parentheses are not used to make evaluation order explicit, operators are evaluated according to a fixed precedence, from tightest-binding to loosest-binding:

| Level        | Constructs                                                                            | Associativity   |
|:------------:|---------------------------------------------------------------------------------------|-----------------|
| 1 (tightest) | primary expressions: literals, identifiers, function calls, parenthesized expressions | --
| 2            | `==` `!=` `<` `>` `<=` `>=` `~=` `BETWEEN` `IS`                                       | non-associative
| 3            | `NOT`                                                                                 | unary, repeatable
| 4            | `AND`                                                                                 | left
| 5 (loosest)  | `OR`                                                                                  | left

An operator of higher precedence binds more tightly than one of lower precedence, meaning it is evaluated first. `NOT` binds more tightly than `AND`, which in turn binds more tightly than `OR`. For example, `artist == "metallica" AND year > 2000 OR NOT genre == "jazz"` is evaluated as if it had been written `((artist == "metallica") AND (year > 2000)) OR (NOT (genre == "jazz"))`, since `AND` binds more tightly than `OR`, `NOT` binds more tightly than `AND`, and the comparisons bind more tightly than `NOT`.

When a left-associative operator appears more than once in a chain without parentheses, it is evaluated left to right. For example, `a AND b AND c` is evaluated as `(a AND b) AND c`. This does not change the result for `AND` or `OR` chains, but it does determine the order in which operands are evaluated. `NOT` is a unary prefix operator and may be applied repeatedly, so `NOT NOT a` is valid and equivalent to `a`.

The comparison operators, the regex operator `~=`, the range operator `BETWEEN`, and the state test operator `IS` are non-associative and cannot be chained. `a == b == c` is an error rather than being read as `(a == b) == c`, because the result of the first comparison is a boolean and booleans are not valid operands of a comparison. A condition of that kind must be written with an explicit logical operator, as in `a == b AND b == c`.

Parentheses should be used whenever the default precedence might not match the reader's expectation, even if they are not strictly required to produce the intended result.

### Multivalues

Some identifiers do not correspond to a single value because the underlying data source allows defining a value multiple times (e.g. Vorbis comments). In such cases, an identifier corresponds to a bag of occurrences instead of a single one, as described under [Values](#values): an unordered collection whose occurrences are not deduplicated, so the same datum occurring twice in the underlying tags occurs twice in the multivalue. A multivalue has a type like any other value, and each of its occurrences is usable or unusable. It is even possible for _every_ occurrence of a multivalue to be unusable. What a multivalue can never hold is an absence, since absence is a property of a whole value rather than of an occurrence.

When an operand is multivalue, it is evaluated according to a _quantifier_: existential (the default behavior) or universal (obtained by wrapping the operand in `ALL()`, see below). An operand's quantifier applies independently of any other operand's -- for example, in a comparison of two multivalues, one operand can be evaluated existentially while the other is evaluated universally.

When one or more operands are multivalue, the operator's result is calculated by nested iteration over the multivalue operands' occurrences, with the operator's test applied once to every combination of occurrences reached this way. An existentially-quantified operand contributes a true result if any one of its occurrences leads to one; a universally-quantified operand requires every one of its occurrences to lead to a true result.

**An operator is a single quantifier scope.** The iteration belongs to the operator, and every one of its value operands is reached within it, which is what makes an operator's test indivisible: a construct that looks like two operators joined by a logical operator has two scopes and is a different proposition, as the `BETWEEN` example above shows. Nothing in the language may therefore be defined by rewriting it into other operators.

**The iteration does not short-circuit.** Every combination is evaluated even after the operator's result is settled. This changes no result -- a quantifier's answer does not depend on how many witnesses were examined -- but it decides which warnings a file produces, and that has to be decided, because a bag is unordered. If iteration stopped at the first witness, then for a value holding one matching occurrence and one unusable one, whether a warning was emitted would depend on which occurrence the iteration happened to reach first, over a collection the specification declares to have no order. A run's warnings, and with them its exit code under `--strict-exit-code`, would vary between implementations, between releases, and potentially between runs, for a predicate whose answer never changed. Exhaustive evaluation costs nothing worth counting, since the bags involved hold a handful of occurrences at most.

This is the one place where Goro deliberately declines to short-circuit. The `AND` and `OR` operators do short-circuit, and are relied on to; see [Boolean operators](#boolean-operators) and [State tests](#state-test-operator) for the guard idiom that depends on it. The difference is that the operands of `AND` and `OR` appear in a written order the reader chose, whereas the occurrences within a bag do not.

The loops are nested **by quantifier, not by position**: a universally-quantified operand always forms a loop outside any existentially-quantified one. Where two operands carry the same quantifier the nesting between them is immaterial to the result, so the order in which the operands were written never affects the result of an operator. Should an operator ever take more than two value operands, the same rule applies: universals outermost, existentials innermost, with the written order breaking ties among operands of the same quantifier.

An unusable occurrence participates in this iteration like any other, with general operator rule 2 applying to it: every combination that includes it makes the operator false for that combination. Under the default existential quantifier an unusable occurrence therefore simply fails to contribute a match, whereas under `ALL()` a single one is enough to make the operator false overall. For example, if `vorbis::year` has two occurrences, one holding `1991` and one holding data that is not a date at all, then `vorbis::year == 1991` is true while `ALL(vorbis::year) == 1991` is false.

Absence, by contrast, does not participate in this iteration at all: an absent operand makes the operator false before any iteration begins, by general operator rule 1. This deserves stating separately, because it is the one place where the identification of absence with the bag of cardinality zero must not be read set-theoretically. Ordinary quantifier semantics would make `ALL(genre) == "rock"` _vacuously true_ for a file with no genre whatsoever, there being no counterexample available to find. Goro makes it false instead: a predicate demanding that every genre be rock is a predicate about genres, and a file that has none does not satisfy it. Universal quantification in Goro is non-vacuous, and it is non-vacuous because absence is intercepted before iteration rather than because the quantifier itself is unusual.

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

With the exception of numeric literals, operators that have multiple operands require the operands to have the same type and produce an error if not. This means that a comparison such as `id3v2::track > vorbis::raw::tracknumber`, which attempts to compare a number with a string, is rejected with an error. To prevent this error a conversion of one of the values to the type of the other is required. Converting `id3v2::track` to a string might result in a comparison such as `"2" > "10"` -- this comparison result is, perhaps surprisingly, true, and it would be correct to instead write `id3v2::track > NUMBER(vorbis::raw::tracknumber)` to avoid this problem and compare numerically. However, `vorbis::raw::tracknumber` might not hold a valid numeric string, and in that case `NUMBER()` yields an unusable occurrence and the comparison always evaluates to false, by general operator rule 2. The warning that consuming an unusable occurrence emits is what keeps this from happening silently, and a [state test](#state-test-operator) is how a predicate can decide for itself what to do about it.

#### Unusable occurrences

Unusable occurrences arise in two ways: from an identifier whose underlying tag data is present but cannot be interpreted as the identifier's declared type, and from a type conversion function whose input cannot be converted. In particular, the type conversion function `NUMBER()` produces an unusable occurrence when its input cannot be converted to the appropriate type. An unusable occurrence takes part in every operation exactly as a usable one does, with a single difference: using it causes Goro to emit a warning to alert you, as described under [Warnings](#warnings) below.

Two constructs can examine an unusable occurrence without using it, and therefore without warning. A [state test](#state-test-operator) reports state rather than content, so `ALL(NUMBER(x)) IS USABLE` establishes that every occurrence of `x` converted to a number without reporting the ones that did not. And `FALLBACK(expr, literal_default)` substitutes for an occurrence instead of reading it, so it is silent for an unusable occurrence exactly as it is for an absent value.

Because identifiers can produce unusable occurrences of their own, state tests and `FALLBACK()` are useful applied directly to identifiers and not only to the results of conversions. `FALLBACK(id3v2::track, 0)` substitutes zero both for a track frame that is missing and for one that is present but unusable, and `ANY(id3v2::track) IS USABLE` is the way to require that at least one usable track number exists; if `ANY` were replaced with `ALL`, the test would require that _all_ potentially existing track numbers are usable.

### Errors

Goro reports every error it possibly can when the predicate is read, before any file is processed. This is a deliberate constraint rather than a convenience: a command may be operating on a hundred thousand files, and it is not acceptable for such a run to abort halfway through because of a mistake that could have been pointed out at the start.

Reporting errors this early is possible because the type of every sub-expression in a predicate is known statically, as described under [Values](#values). A file decides only a value's cardinality and the state of each of its occurrences, never its type. Type checking therefore needs no file at all.

The errors reported when a predicate is read include:

- syntax errors of any kind, including a non-ASCII character in an identifier, a chained comparison such as `a == b == c`, a reserved word used as a bare identifier, and a qualified name used as a function call;
- a reference to an identifier that is not defined, where the namespace it names is a closed one;
- a type mismatch between the operands of an operator, outside the numeric literal exception;
- a boolean value used anywhere other than as an operand of `AND`, `OR` or `NOT`, and a predicate whose value as a whole is not boolean;
- an argument of the wrong type to a function, and a modifier applied to an operand it cannot affect, such as `LITERALLY()` applied to something other than a string, or to the operand of a state test;
- a modifier applied anywhere other than to an operand of a comparison, range, regex, or state test operator, or to another such modifier;
- a range whose endpoints are not literals of the same type, or whose `min` is greater than its `max`;
- a regular expression literal that is not a valid regular expression, or that uses a construct the matching engine does not support.

Only conditions that genuinely depend on the contents of a file are left to be discovered during evaluation, and neither of them ever stops a run: a value that is absent or an occurrence that cannot be interpreted, and a regular expression arriving from tag data that turns out not to be valid. These are reported as warnings instead, as described below.

### Warnings

A warning is emitted when an unusable occurrence is consumed while evaluating a predicate, and likewise when a regular expression arriving from tag data fails to compile. Evaluation always continues after a warning: a warning never interrupts processing, and it never changes the result of the predicate.

#### What counts as consuming an unusable occurrence

**Consuming an unusable occurrence means producing a result derived from its content.** Only consuming emits a warning. Everything else follows from that one rule, and nothing is exempt from it:

- The comparison, range and regex operators interpret the content of their operands in order to produce a result. They consume, so an operator handed an unusable occurrence warns.
- `NUMBER()` and `STRING()` map an unusable occurrence to an unusable occurrence. They derive nothing from its content; they _propagate_ it, keeping the source it was born with, and they are silent.
- `COUNT()` reads cardinality, which is knowable without interpreting any occurrence. It is silent.
- `FALLBACK()` substitutes for an occurrence instead of reading it. It is silent.
- A [state test](#state-test-operator) reads state rather than content. It is silent.
- A modifier computes nothing at all, so applying one leaves the operator that follows as the consumer.

This is what allows the guard `ALL(NUMBER(x)) IS USABLE AND NUMBER(x) > 5` to work even when the unusable data originates in `x` itself rather than in the conversion: `NUMBER()` propagates what it was handed, the state test reads the state of the result without consuming it, and `AND` short-circuits before any operator can.

#### Where a warning comes from

An unusable occurrence is born once, at the identifier or the conversion that failed, and it carries that _source_ for the remainder of the evaluation. A function or operator that passes it through changes neither its state nor its source. A warning is therefore emitted at the point where the occurrence is consumed, but reports where it was born: in `STRING(id3v1::year) == "1991"` applied to a file whose Id3v1 year field holds something that is not a year, the unusable occurrence is born at `id3v1::year`, is propagated through `STRING()`, and the warning is triggered by the consuming `==` but names `id3v1::year` as the source, which is where the problem actually is.

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

Deduplication resets for every file. A predicate such as `id3v2::track == 1` applied to a collection in which 500 files have junk where the track number should be will therefore produce 500 warnings, one per affected file. This is deliberate: each warning names a file whose data needs attention, and collapsing the repeats would discard exactly the information that makes the warning actionable.

A sub-expression that is never evaluated never warns. In particular, the short-circuiting of `AND` and `OR` can be used deliberately to avoid a warning; refer to [State tests](#state-test-operator) for the guard idiom that relies on this. Within a single operator there is no such escape, since iteration over a multivalue operand is exhaustive: if an operand holds an unusable occurrence and the operator is evaluated at all, the warning is emitted, whether or not some other occurrence already settled the result.

#### Where warnings go

Warnings are written to standard error and never to standard output, so that they cannot interfere with the machine-readable output of commands such as `goro list -o json`. They do not by themselves make a command fail: a run that warned is still a run that completed, and reporting that fact through the exit code is opt-in (see [Exit codes](exit-codes.md)). Each warning names the file being processed and quotes the sub-expression responsible, exactly as it was written in the predicate. Where the same source occurs in more than one place, the quoted text is that of the occurrence which actually produced the value. Because deduplication means only one of several equally responsible occurrences is reported, a warning deliberately does not name a position within the predicate.

### Modifiers

Three constructs -- `ALL`, `ANY`, and `LITERALLY` -- are _modifiers_ rather than functions. A modifier does not compute a new value from its argument; it changes how the operator that consumes the value will treat it. The defaults are existential quantification and normalized comparison, so a modifier is only ever needed to depart from them.

The three are not quite the same kind of thing, and the difference is worth stating because it decides what writing one on one side of an operator does to the other side.

- `ALL` and `ANY` choose a **quantifier**, which belongs to the operand it is written on. Each operand of an operator carries its own, and they are independent: one side may be universal while the other is existential.
- `LITERALLY` chooses a **comparison mode**, which belongs to the operator. An operator either normalizes the strings it compares or it does not, and a `LITERALLY` on either operand settles that for the comparison as a whole.

Because a modifier's effect is realized by an operator rather than by the modifier itself, **a modifier may only be applied to an operand of a comparison, range, regex, or state test operator, or to another such modifier**. Writing one anywhere else is an error. In particular a modifier may not appear as an argument to a function: `LITERALLY(genre) == "Pop"` is valid, while `COUNT(ALL(genre))` and `FALLBACK(LITERALLY(genre), "pop")` are not.

The restriction applies in one direction only. A modifier may be applied to any operand, including one that is itself a function call, so `LITERALLY(FALLBACK(genre, "pop")) == "Pop"` is valid: modifiers go on the outside, functions on the inside.

Modifiers may be stacked as deeply as desired and in any order, a quantifier and a comparison mode being independent choices. `LITERALLY(ALL(genre))` and `ALL(LITERALLY(genre))` mean the same thing, and where the same choice is made twice the outermost wins, so `ALL(ANY(genre))` is universally quantified.

#### Quantifiers

A quantifier decides what it means for an operand that holds several occurrences to satisfy an operator. It is a property of the operand it is written on, so each operand of an operator carries its own; see [Multivalues](#multivalues) for how the two are combined when they differ. Applying a quantifier to an operand that holds a single occurrence has no effect, and the operand is treated exactly as it would have been without it.

**ALL(expr)**
: causes the operand to be evaluated with a _universal_ quantifier when it is a multivalue, instead of the default _existential_ quantifier: an operator using this operand only matches if _all_ of the multiple values present match, rather than _any one_ value as is the default behavior. See [Multivalues](#multivalues) for the full evaluation rules, including how two multivalue operands are combined when their quantifiers differ.

Examples:

- `genre == "rock"` will match when `genre` is a multivalue representing both `"rock"` and `"metal"` because at least one of the values matches
- `ALL(genre) == "rock"` will _not_ match in the same scenario because not all of the values match `"rock"`

In case the operand that `ALL()` is applied to is not a multivalue, the modifier has no effect and the operand is treated exactly as it would have been without it.

**ANY(expr)**
: causes the operand to be evaluated with an _existential_ quantifier when it is a multivalue: an operator using this operand matches when _any one_ of the multiple values matches. This is the default behavior for every multivalue operand, so this modifier is never strictly necessary, but it is included as an explicit counterpart to `ALL()`, both to allow more expressiveness if desired (`ANY(genre) == "rock"` reads very naturally) and to make a quantifier choice explicit where it might otherwise be unclear at a glance -- for example, when comparing two multivalues where only one side is wrapped in `ALL()`.

#### Comparison modes

A comparison mode decides how an operator compares the strings it is given. There is one such mode, normalization, and one modifier to turn it off. Unlike a quantifier, a mode belongs to the operator rather than to an operand: an operator either normalizes or it does not, and there is no way for one side of a comparison to be normalized while the other is not. Writing the modifier on an operand is how that choice is expressed, and which operand it is written on makes no difference.

**LITERALLY(expr)**
: switches the operator that consumes this operand out of normalized comparison. Use it for case-sensitive matching, for matching on diacritics, and for other cases where the value is to be taken exactly as recorded.

Because the mode belongs to the operator, it is enough for _just one_ operand to carry the modifier -- `LITERALLY(x) == y`, `x == LITERALLY(y)`, and `LITERALLY(x) == LITERALLY(y)` all do exactly the same thing. There is no such thing as a `LITERALLY` value that could be passed around and compared against a normalized one; the modifier is a note to the operator, and the operator obeys it once.

Applied to either operand of `~=`, the modifier additionally makes the match case-sensitive, since for that operator case insensitivity comes from the match rather than from rewriting a value. It also leaves the subject's diacritics in place, which is what allows a pattern to match one.

Where an operand holds several occurrences, the mode applies to the comparison of every one of them, there being only one comparison mode in play. It is an error to apply `LITERALLY()` to an operand of any type other than string, and likewise to the operand of a state test, where there is no comparison for a mode to affect. An absent value and a string multivalue are of course still permitted.

Examples:

- `artist == "metallica"` matches "Metallica" because the equality operator normalizes its inputs by default.
- `artist == LITERALLY("metallica")` does _not_ match "Metallica" because the result of `LITERALLY()` prevents the equality operator from normalizing its inputs before comparing them.
- `artist BETWEEN "m".."n"` matches "Metallica" because, after normalization by default, "Metallica" sorts between "m" and "n".
- `LITERALLY(artist) BETWEEN "m".."n"` does _not_ match "Metallica" because the result of `LITERALLY()` prevents the range operator from normalizing its inputs, and upper case "M" does not sort between lower case "m" and "n".
- `artist ~= "^met"` matches "Metallica" because the match is performed case-insensitively.
- `artist ~= LITERALLY("^met")` does _not_ match "Metallica", because `LITERALLY()` makes the match case-sensitive and "Metallica" begins with a capital "M".
- `artist ~= "motö"` matches nothing, because diacritics are removed from the subject and the pattern is left as written, so the `ö` in it has nothing to match.
- `LITERALLY(artist) ~= "otö"` matches "Motörhead", because an unnormalized subject keeps its diacritics.

### Functions

There are a number of built-in functions that can be used inside predicate expressions. Functions are invoked by their name followed by a list of zero or more arguments in parentheses. For example, `FOO(x, y)` is a call to the function `foo` with arguments `x` and `y`.

A function call is always an **unqualified** name followed by an opening parenthesis. Functions do not live in namespaces, so a qualified name is never a function call and `foo::count(x)` is a syntax error. This is what allows a single name to serve both as a function name and, in principle, as an identifier: `count` on its own is an identifier reference, `count(...)` is a call, and the qualified form `::count` always and unambiguously refers to the identifier.

Function arguments follow the same type-matching discipline as operator operands, including the numeric-literal exception. Unless otherwise noted below, a function given an absent argument returns an absent value, and a function given an unusable occurrence returns an unusable occurrence.

The functions that can be used inside predicates are:

**COUNT(expr)**
: returns the cardinality of its argument, as a number. Specifically, the result of this function is:

- `0` if `expr` is absent
- `1` if `expr` is a single occurrence
- a positive integer greater than 1 if `expr` is a multivalue, equal to the count of its occurrences

Unusable occurrences are counted like any other, so a value of two unusable occurrences has a count of 2. Data that is there and cannot be read is a different thing from data that is not there, and `COUNT()` counts what is there.

This function reads cardinality and nothing else. It never interprets an occurrence, so it never consumes one and never warns, however unusable its argument turns out to be; see [Warnings](#warnings).

Note that `COUNT()` answers a question about cardinality rather than about fitness for use. `COUNT(expr) == 0` is exactly `expr IS ABSENT`, and says nothing at all about whether the occurrences of a value that is not absent can be read; a [state test](#state-test-operator) is how to ask that.

Example:

- `COUNT(genre) > 1` is true if and only if `genre` is a multivalue

**FALLBACK(expr, literal_default)**
: returns `expr` wherever it is usable, and `literal_default` wherever it is not. An absent `expr` becomes a single usable occurrence holding `literal_default`, and an unusable occurrence becomes a usable one holding `literal_default`.

Because this function substitutes for an occurrence rather than reading it, it never consumes one and therefore never warns, however many of its input's occurrences are unusable. It is an error if `literal_default` is not a literal.

If `expr` is a multivalue, this function returns a multivalue of the same cardinality, with `FALLBACK()` applied to each occurrence of the input in turn. Its result is therefore never absent and never contains an unusable occurrence.

**NUMBER(expr)**
: converts its argument to a number.

If `expr` is absent, or is already a number, this function returns the same value. If `expr` is a bytecount, it returns the count as a number of bytes. If `expr` is a duration, it returns total number of seconds. If `expr` is a string that satisfies the rules for a numeric literal, it is converted to a number and returned. If `expr` is a string that does _not_ validate as a numeric literal, the result is an unusable occurrence, which will emit a warning the first time an attempt is made to use it. An unusable input yields an unusable result, propagated in silence.

If the argument is a multivalue, this function returns a multivalue of the same cardinality, with `NUMBER()` applied to each occurrence of the input in turn.

**STRING(expr)**
: converts its argument to a string.

If `expr` is absent, or is already a string, this function returns the same value. If `expr` is a bytecount, it returns the decimal string representation of the count of bytes as a plain number without a unit. If `expr` is a duration, it returns the decimal string representation of the total duration in seconds as a plain number without a unit. If `expr` is a number, it returns its decimal string representation. Every value of every type has a string form, so this function never produces an unusable occurrence of its own; an unusable input yields an unusable result, propagated in silence.

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
                | "~=" operand
                | "IS" state ;

comparison_op   = "==" | "!=" | "<=" | ">=" | "<" | ">" ;
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

Because a modifier can only occur within an `operand`, and `operand` occurs only as an operand of a comparison, range, regex or state test operator, the placement restriction described under [Modifiers](#modifiers) is imposed by the grammar rather than by a rule laid on top of it. The recursion in `operand` is what allows modifiers to be stacked without limit. Note that `primary` admits a parenthesized `expression` but `operand` does not, so a modifier may not be wrapped in redundant grouping parentheses: `(ALL(genre)) == "x"` does not parse.

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

string          = quoted_string | raw_string ;

quoted_string   = '"' { string_char | escape } '"' ;
string_char     = <any character other than '"' and "\"> ;
escape          = "\" ( '"' | "\" | "n" | "r" | "t" )
                | "\x" hex hex
                | "\u" hex hex hex hex ;

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
duration_clock  = [ digits ":" ] two_digits ":" two_digits ;
two_digits      = digit digit ;
```

### Tokens

The rules for dividing predicate text into tokens are given under [Tokens and whitespace](#tokens-and-whitespace) and are not restated here. Three consequences bear on the productions above.

The `bytecount`, `duration` and `raw_string` productions are each a single token, so no whitespace may appear anywhere within them, including between a `raw_string`'s `r` and its opening quote.

Inside a `raw_string`, a pair of quotes is taken as the escaped quote whenever two appear together, so the string ends only at a quote standing alone. `r"a""b"` is therefore the three characters `a"b`, and a raw string holding a single quote is written with four in a row.

Because an `identifier` is never followed by a `string` in any production, reading `r"` as the start of a raw string takes nothing away: no predicate that parsed before can change meaning, and an identifier that happens to be named `r` is unaffected wherever whitespace, an operator or a `::` follows it.

### Constraints not expressed by the grammar

Not every rule in this document is grammatical, and a construct that this grammar accepts may still be rejected. The following constraints are enforced by static analysis of a parsed predicate rather than by the grammar, and all of them are reported as [errors](#errors) when the predicate is read:

- a `predicate` must have boolean type, so a bare `primary` such as `artist` parses but is not a valid predicate;
- the operands of an operator must have the same type, subject to the numeric literal exception, and the operands of `AND`, `OR` and `NOT` must be boolean while the operands of every other operator must not be;
- both operands of `~=` must be strings, and its right operand must be a valid regular expression if it is a literal;
- the `operand` of a state test may be of any type other than boolean, since a state test asks about cardinality and state rather than about content;
- an `identifier` in a closed namespace must be one the namespace defines, and an identifier's first `name` may not be one of the reserved words unless the identifier is qualified;
- a `name_part` written as a `string` names the same thing as the equivalent bare `name` when the name is one a bare `name` could have spelled, so the two forms are one identifier and not two;
- a `function_call` names an existing function and supplies it with the number and types of arguments it accepts; in particular the second argument of `FALLBACK()` must be a literal;
- the `operand` a `LITERALLY` modifier is applied to must be of type string, and must not be the operand of a state test;
- the two endpoints of a `range` must be literals of the same type, and `min` must not be greater than `max`;
- at least one of the three unit groups of a `duration_units` must be present, so the empty string does not satisfy that production;
- each `two_digits` of a `duration_clock` must denote a value between 0 and 59 inclusive.
