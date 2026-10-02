# Testing

This document records the classes of test that Goro considers important, why each one earns
dedicated attention, and the scenarios that must be covered for each. It is not a list of every
test in the suite: ordinary unit tests for ordinary code need no entry here. A class of test
belongs in this document when getting it wrong would produce **silently wrong results rather
than a visible failure**, or when correctness rests on behaviour Goro does not control.

## Predicate evaluation over tag data read through TagLibSharp

### Why this earns its own class of tests

Goro does not read tag structures itself; it reads them through TagLibSharp and then interprets
what it gets. That places a third-party library in the middle of every predicate that touches a
tag, and it means two kinds of breakage that no ordinary unit test would notice:

- **The library's behaviour differs by tag version in ways that are not obvious.** Reading an
  Id3v2.3 tag does not yield the same shape of data as reading an Id3v2.4 tag holding identical
  content. Goro's job is to hide that difference, so the tests must assert that it is hidden.
- **Some of what Goro relies on is emergent rather than contractual.** Recovering the text of a
  frame as it appears on disk depends on implementation details of the library rather than on a
  documented guarantee. A dependency upgrade can change it without any compilation error, and
  the symptom is a predicate quietly matching the wrong files.

A failure in this area does not crash and does not show up in output. It silently changes which
files a command operates on, which for a tool whose purpose is to decide what to touch is the
worst failure mode available.

### What the tests must establish

**Version independence.** For each identifier Goro defines, tag content that is semantically
identical must produce an identical value whether it was stored as Id3v2.2, v2.3 or v2.4. This
is the property that the documentation promises, so it is the property that must be tested
directly rather than inferred from the parts.

**Faithfulness of the raw namespaces.** An identifier in a `::raw` namespace must yield the datum
as recorded, uninterpreted, in the type that datum natively has. Where that is not achievable, the
test must pin the exact discrepancy so that it is a known and documented deviation rather than a
surprise.

### Known deviations are recorded as skipped tests, not as prose

Goro obtains the recorded text of a frame by a route that is faithful in almost every case and
inexactly faithful in a small, enumerable set of cases. That set is not documented only in prose:
each case gets a real test that asserts the *correct* behaviour and is **skipped**, grouped under
a category the test runner can exclude wholesale so that a normal run neither fails nor reports
noise.

This is deliberate, and it is preferred over the alternative of reading library internals to
achieve exactness. A route through internals would have to be guarded by tests that prove the
internals still behave as assumed, and no realistic suite can exercise the code paths a large
collection in the wild will; a dependency upgrade could therefore ship a silently broken release.
Choosing the inexact but supported route means the failure mode is a known, bounded, documented
near-miss with an obvious workaround, rather than an unbounded unknown.

Skipped tests are the right home for these cases because they state the correct expectation
precisely, they are impossible to lose track of, and the day a bug report arrives the work begins
by removing a skip rather than by reconstructing what the problem was.

### Canary tests guard the defects we work around

Where Goro compensates for a defect in a dependency, the compensation is invisible from outside:
the documentation describes correct behaviour and says nothing about the defect. That leaves a
hazard, because if the dependency is fixed the compensation becomes a second, opposite defect.

Every such workaround therefore carries a **canary test that asserts the defect is still
present** in the dependency. A canary failing is not a regression; it is notice that the
dependency changed and that the compensating logic and its tests must be revisited. Canaries are
grouped so that this intent is unmistakable to whoever sees one fail.

### Scenarios to cover

Tests must construct tag bytes directly rather than writing tags through TagLibSharp. Writing
through the library and reading back conceals defects, because the writer and the reader share
assumptions that a real-world file does not: a value written through the library arrives back
looking correct even when the same bytes produced by another tagger would not. The test project
already builds Id3v2 tag bytes by hand for other purposes, and that is the approach to extend.

Every scenario below is to be exercised for **each of Id3v2.2, v2.3 and v2.4**, and for both the
interpreted and the raw namespace.

| Scenario | What it is there to catch |
|---|---|
| A value containing a forward slash, such as an artist named `AC/DC` | The library splits some v2.3 frames on `/`; a single value must not become two |
| A value containing a semicolon, such as `Rock; Metal` | Goro's own split applies to genres only, and every resulting value must be trimmed -- `Metal` and not `" Metal"`, the space after a separator being the commonest convention there is |
| A genre written as a bare number, `17` | Must expand to the named genre on every version |
| A genre written as a reference, `(17)` | The parenthesised form is legal in v2.2 and v2.3 and out of spec in v2.4, but occurs there after a version migration |
| A genre reference with a refinement, `(17)Post-Rock` | Must yield both the referenced name and the refinement, on every version |
| Several genre references, `(51)(39)` | Every reference must be expanded, not only the first |
| A genre reference with an escaped parenthesis, `(17)((weird)` | The doubled parenthesis must be unescaped, on every version |
| `(RX)` and a bare `RX`, and the same for `CR` | Indistinguishable on v2.3, distinguishable on v2.4; both must map to the same result |
| A track number written `3/12` | Must yield the track and not be mistaken for two values |
| A date in each accepted timestamp form, and one in no accepted form | The latter must produce an unusable occurrence, not a wrong date |
| A date-shaped field holding `0000` | Must be unusable; there is no year zero, and resolving it to the number zero would match a great many files silently |
| A string-typed field that is present but empty, and one present but only whitespace | Both must be **absent** through the interpreted identifier, a file whose frame holds three spaces having no artist by any reading a person would recognise |
| The same two fields through the raw namespace | A usable empty string and a usable three-space string respectively: raw trims nothing, and this is where the information the interpreted namespace set aside is still visible |
| A value with whitespace at one end, through both namespaces | Trimmed through the interpreted identifier and untouched through the raw one |
| A value with whitespace *inside* it, such as `" AC / DC "` | Must yield `AC / DC`: trimming takes the ends and never the middle |
| A number-typed field, such as the one behind `id3v2::year`, present but empty or only whitespace | Must yield an unusable occurrence: the frame was written and holds no year |
| The same frame repeated, with and without distinguishing descriptions | Must yield a multivalue of the expected cardinality |
| A single v2.4 text frame holding several NUL-separated values | The only spec-sanctioned source of multiple values in one frame |
| A frame the library has no class for, holding text | Must be readable through the raw namespace |
| An `APIC` frame holding a picture | Must be an unusable occurrence and not an absent one, so that a predicate can tell a file that has cover art from one that does not |
| A v2.2 frame whose identifier the dependency maps to the wrong v2.4 name | Canary: Goro corrects the mapping internally, so the test asserts the underlying defect is still there |
| A `TCON` frame on v2.3 whose genres are separated by a forward slash, and one holding `(RX)` | Skipped: the recorded text is reported with a semicolon in place of the slash, and without the parentheses. Affects the raw namespace only |

### Global namespace resolution

An identifier in the global namespace is resolved across up to four tag formats in rank order, and
which format supplies the answer is decided by whether a format produced a *usable* occurrence
rather than merely a present one. Getting that wrong returns a value from the wrong tag without
any visible symptom, which is the same failure mode as the rest of this class and earns the same
treatment. The scenarios are few and they are exhaustive, so they should all be written.

These require a file carrying more than one tag format, so the fixtures differ from those above.

| Scenario | What it is there to catch |
|---|---|
| A preferred format absent, a lesser one holding a usable value | The ordinary fall-through: the lesser format's value is returned |
| A preferred format holding only unreadable data, a lesser one holding a usable value | The lesser format's value must be returned. Stopping at the preferred format because it was merely present is the specific regression this row exists to catch |
| A preferred format holding one usable and one unreadable occurrence, a lesser one holding a usable value | The preferred format wins and its value is returned entire, so using it warns; usability breaks ties, it does not outrank preference |
| Only one format holding anything, and it holds only unreadable data | The result must be unusable, not absent |
| No format holding anything | Absent |
| Both a preferred and a lesser format holding usable values | The lesser format must not be consulted at all, which needs asserting against the tag reader rather than against the result |
| A preferred format whose defect was routed around | No warning is emitted, the discarded occurrences not being part of the value returned. This is the documented price of the facade, so it is pinned deliberately rather than left to be discovered |

### Scenarios deliberately not covered

Recovering the *content* of a genuinely binary frame is out of scope: no identifier hands back
bytes, and none is planned. What is in scope, and is listed above, is that such a frame resolves
to an unusable occurrence rather than to an absent value, since that distinction is what lets a
predicate ask whether a file carries cover art at all.

## Predicate semantics

### Why this earns its own class of tests

A predicate decides which files a command touches. A defect in how one is evaluated therefore
does not crash and does not show up in output: it silently operates on the wrong set of files,
which is the failure mode this document already calls the worst available. That is the same
argument that earns tag reading a class of its own, and it applies with more force here, because
these rules are Goro's own and nothing outside the test suite constrains them.

Three areas carry nearly all of the risk.

**Quantifier semantics over multivalues.** Which combinations an operator evaluates, and in what
nesting, decides the answer whenever a file records the same datum more than once -- which on a
real collection is most files, for at least one identifier. Every rule here is one a plausible
implementation can get wrong while still returning a perfectly well-formed boolean.

**The distinction between absent and unusable.** Absence is intercepted before iteration and
makes an operator false, while unusability is evaluated inside it and makes the combinations it
touches unusable. For a predicate without `NOT` the two select exactly the same files, so an
implementation that conflates them -- most tempting of all, one that reads unusable as false --
passes every test that only checks which files a positive predicate selected. It fails only in
the places that tell the two apart: negation, the state tests, the warnings, universal
quantification, and what the command does with a file whose predicate could not be answered.

**Warning identity.** Deduplication is by interned source rather than by position, and the set of
sources a predicate can produce is enumerable before any file is read. That makes the mechanism
testable without a file at all, which is an opportunity the suite should take.

### What the tests must establish

**Operand order never changes an operator's result.** For every operator and every combination of
quantifiers, an expression and its mirror image agree: `ALL(a) == b` with `b == ALL(a)`, and
`a < ALL(b)` with `ALL(b) > a`. This is the property that nesting by quantifier exists to deliver,
and the reason it was chosen over nesting by written position, so it is the property to assert
directly rather than to infer from the parts.

**No operator is a rewriting of other operators.** Because an operator is one quantifier scope,
splitting one into two logical operators changes its meaning. `BETWEEN` must therefore be
evaluated once per combination rather than decomposed into a pair of comparisons.

**Iteration within an operator is exhaustive.** A settled result does not stop it, so an unusable
occurrence in an operand warns whether or not some other occurrence had already decided the
answer. The result is the same either way, which is exactly why this needs asserting on the
warnings rather than on the boolean: a test that only checks what an operator returned cannot
tell an exhaustive implementation from a short-circuiting one.

**Universal quantification is non-vacuous.** `ALL(x)` in any operator is false for an absent `x`,
where ordinary set semantics would make it true.

**Unusable data never selects a file.** A predicate that evaluates to true must be true whatever
the data that could not be read had held. Two properties together establish it, and both are
suited to generated predicates rather than hand-picked ones: a predicate without `NOT` selects
exactly the same files whether unusable results are kept or read as false, and a predicate with
`NOT` never selects a file that the same predicate would reject for some usable replacement of its
unusable occurrences.

**The logical operators follow their table, and short-circuit only on a settling operand.** Every
row of the table in the predicate documentation should be asserted, for both operand orders. The
short-circuit is observable through warnings, so it needs asserting there: an unusable first
operand of `AND` or `OR` must lead to the second operand being evaluated, and a false first operand
of `AND`, or a true one of `OR`, must not.

**An unusable boolean is never reported twice.** Comparing, testing, substituting for or combining
an unusable boolean emits nothing beyond what the operator that produced it emitted.

**The state test truth table holds in full.** The table in the predicate documentation is small
enough to assert in full, and should be. Its absent row matters most, being where
`ALL(x) IS USABLE` and `NOT x IS UNUSABLE` part company.

**The regex operator normalizes its subject and not its pattern.** Every other operator
normalizes both of its operands, so an implementation that reuses the ordinary path for `~=` will
normalize the pattern too, and the symptom is a pattern that matches slightly different files
rather than any kind of failure. The asymmetry therefore needs asserting from both sides: that a
diacritic in the pattern matches nothing, and that the same pattern under `LITERALLY()` matches
the subject that still has one.

**Warning identity is structural.** The interning rules hold: differences of whitespace, letter
case, explicit namespace qualification and modifier wrapping do not produce distinct sources,
while a difference anywhere below the top level does. The set of sources a predicate can produce
is computable from the predicate alone.

**Every static error is reported before a file is opened.** Every condition the predicate
documentation calls an error needs a test asserting both the rejection and that nothing was
processed, since the guarantee under test is as much about when the error arrives as about whether
it arrives at all. The list of errors in that documentation is illustrative, so it is a starting
point for these tests rather than an inventory of them.

### Scenarios to cover

| Scenario | What it is there to catch |
|---|---|
| Each of the three two-multivalue quantifier combinations, each with its operands written in both orders | Nesting must follow the quantifiers and not the written positions |
| An ordering operator with exactly one operand under `ALL()` | The result is an aggregate comparison; an implementation reading it as "every one of these is less than every one of those" is wrong |
| `v BETWEEN 1..10` against a multivalue of `0` and `20` | Must be false; an implementation that desugars into two comparisons returns true |
| `ALL(x) == v` and `ALL(x) IS USABLE` for an absent `x` | Both false; universal quantification must not be vacuous |
| Every row of the state test truth table, including the absent row | The universally quantified tests must be false for an absent value, not the negations of the existential ones |
| `x != v` against `NOT x == v`, for an absent `x`, an unusable `x`, and a multivalue holding one of each | For the absent and multivalue cases the two are not complementary and each must give the documented answer; for the unusable case both must be unusable |
| `x < v` against `NOT x >= v`, for a single unusable `x` | Both unusable: negation must not turn a comparison that could not be answered into a true one |
| `x == v` and `ALL(x) == v` and `ALL(x) == w`, for `x` holding one occurrence equal to `v` and one unusable | True, unusable and false respectively: an answered combination settles a quantifier whenever it can, and only otherwise does the unusable one decide |
| `x > 1 AND y > 1`, with `x` unusable and `y` unusable | Two warnings: an unusable first operand must not settle `AND` |
| `FALSE AND y > 1` and `TRUE OR y > 1`, with `y` unusable | No warning: a settling first operand must short-circuit |
| `FALLBACK(x > 1, FALSE)` against `FALLBACK(x, 0) > 1`, with `x` unusable | One warning and none respectively: substituting for a comparison's result does not undo the comparison's warning |
| `(x > 1) == (x > 1)` and `(x > 1) IS UNUSABLE`, with `x` unusable | One warning in total for the first and none beyond the comparison's for the second; the second is true |
| A boolean as an operand of `<`, `BETWEEN` or `~=`, or as an argument to `NUMBER()` or `STRING()` | A static error: booleans are unordered and not convertible |
| `(a == b) == (c == d)` and `a == b == c` | The first is valid and compares two booleans; the second is still a syntax error |
| A guard whose right operand would warn, and the same guard with the operands transposed | Short-circuiting must hold, since the observable difference is a warning that is or is not emitted |
| An operator that finds a match among the usable occurrences of a bag that also holds an unusable one | The warning must be emitted anyway, since iteration within an operator does not short-circuit; a short-circuiting implementation would emit it or not according to the order of an unordered bag |
| The same bag with its occurrences supplied to the operator in the reverse order | The result, the warnings and the exit code must all be identical, which is the property exhaustive iteration exists to deliver |
| `NUMBER()` of a tag string with valid number syntax but more significant digits than a number holds | An unusable occurrence, not a rounded number |
| `file::extension` of `a.flac`, `a.tar.gz`, `.hidden`, `README` and `trailing.` | `flac` and `gz`, then absent three times: no dot, a leading dot only, and nothing after the last dot |
| `file::path` of a file on Windows | Written with `/` throughout, drive letter included, so that the same predicate means the same thing on every platform |
| `file::path` and `file::name` of a file reached through a symbolic link | The link's path and name, not the target's: paths are as discovered |
| `STRING()` of `1.50`, `.5`, `+5` and `-0` | `"1.5"`, `"0.5"`, `"5"` and `"0"`: one canonical form, whatever the value was written as |
| A value that is absent, and one that is a single unusable occurrence, passed to `COUNT()` | Must be 0 and 1 respectively, and neither may warn |
| Each row of the warning deduplication table | Source identity must be structural and independent of position |
| Each condition the predicate documentation calls an error | Every one reported with nothing processed, and with the exit code that says the run never started |
| A `~=` whose pattern holds a diacritic, against a subject that holds the same one | Must not match: the subject is normalized and the pattern is not |
| The same pair under `LITERALLY()` | Must match: an unnormalized subject keeps its diacritics |
| A `~=` whose pattern differs from the subject only in case | Must match by default, and must not under `LITERALLY()`, since the modifier makes the match case-sensitive |
| A pattern that would change meaning if it were decomposed, such as `e` followed by a combining acute and `?` | Must be matched as written; an implementation that normalizes the pattern turns it into a different pattern rather than a differently-spelled one |
| A pattern arriving from tag data that is invalid, or that uses an unsupported construct | Must warn and evaluate to unusable, and must not stop the run |
| `NOT (s ~= p)`, with `p` from tag data and not a valid pattern | Unusable, not true: no file may be selected on the strength of a match that never ran |

## Predicate reading

### Why this earns its own class of tests

The classes above are about what a predicate means once it has been read. This one is about the
step before that, and it earns separate attention because its failure mode is the sharpest in the
document: the same predicate text yielding a *different value*, with no error raised anywhere. A
literal read wrongly does not misbehave visibly. It silently selects a different set of files,
and the predicate on the command line still looks exactly like what the user meant.

Two things concentrate the risk.

**A string literal has two forms and escaping happens at two levels.** A quoted string processes
escapes and a raw string does not, and where the value is a regular expression the pattern engine
then processes escapes of its own. Most of the ways to get this wrong produce a valid string that
is not the intended one: dropping a backslash before a character that is not an escape, reading
more or fewer digits than `\x` and `\u` take, or mishandling the doubled quote that terminates a
raw string.

**Tokenisation rests on a rule the productions cannot state.** A token is always as long as it
can be, and several literals are only unambiguous because of it. Most mistakes here surface as
parse errors, which are visible and therefore cheap, but not all: the doubled-quote rule inside a
raw string decides a value rather than whether the text parses.

### What the tests must establish

**Every escape produces exactly its character, and nothing else is an escape.** The list in the
predicate documentation is closed, so the tests are the list plus the negative case: a backslash
followed by anything not on it is an error. That negative case matters more than it looks, because
the tempting implementation -- pass the unknown character through and drop the backslash -- is what
several languages do, and it would turn a mistyped pattern into a quietly different one.

**A raw string is what was written.** Byte for byte, backslashes included, with a doubled quote
standing for one quote and the string ending only at a quote that stands alone.

**The two forms are indistinguishable once read.** A quoted string and a raw string denoting the
same characters are one value, compare equal, and count as one warning source.

**The longest token wins.** Asserted on the literals where it decides something, rather than as a
property of the lexer in the abstract.

Static rejection of a bad pattern belongs to the error-reporting guarantee asserted in the class
above, and is not restated here; what this class adds is that the *unsupported* constructs are
rejected on the same terms as the malformed ones.

### Scenarios to cover

| Scenario | What it is there to catch |
|---|---|
| Each of the eight escapes, in a quoted string | Each must produce exactly its character |
| `"\x41B"` and the four-digit `\u` equivalent | Both escapes take a fixed number of digits, so this is `A` followed by `B` and not a three-digit read |
| A backslash followed by a character that is not an escape, including `\N` and `\U` | Must be an error, not a dropped backslash and not a literal one |
| `\u{1F3B5}`, and `\u{D800}` or `\u{110000}` | The first is one character; the others are errors, being a surrogate and beyond the last code point |
| Two `\u` escapes forming a surrogate pair | Must produce the single character outside the Basic Multilingual Plane |
| `r"\d{4}"` used as a pattern | The backslash must reach the pattern engine unprocessed |
| `r"a""b"` | Must be the three characters `a"b` |
| A raw string of four consecutive quotes | Must be a string holding one quote |
| `r "x"`, with a space between prefix and quote | Must not be a raw string, the prefix having to abut the quote |
| An identifier named `r` followed by whitespace, by an operator, and by `::` | Must remain an identifier in each case; the raw-string prefix must not capture it |
| A quoted string and a raw string denoting the same characters, compared with each other and used twice in one predicate | Must be one value and one warning source |
| `5mb` against `5m`, and `1h10m` against `10 kb` and `1h 10m` | The longest token must win, and whitespace must not appear inside a literal |
| `1..100`, `1.5..2`, and `1.` | The range operator must survive the lexer, and a trailing period must be rejected |
| A no-break space and an ideographic space between two tokens | Both are whitespace, and the predicate parses as if a plain space had been written |
| `NOT::x == 1`, and `and::x` | The first is `NOT` applied to `::x == 1`; the second is an error, a reserved word never beginning an identifier unless it follows a leading `::` |
| `(ALL(genre)) == "x"` | Parses, and is rejected by static analysis: parentheses do not make a modifier's position acceptable |
| `TRUE`, `true` and `False` as literals, `::true` as an identifier, and `year == NULL` | The boolean literals are case-insensitive keywords and qualifying one makes it an identifier; `NULL` names nothing and must be rejected with a diagnostic saying what to write instead |
| A literal pattern using each of the four unsupported construct families | Each must be a static error, reported before any file is opened, on the same terms as a malformed pattern |
| A pattern written as a quoted string and as the equivalent raw string, for an escape both levels understand | `"\x41"` and `r"\x41"` must both match the same subject, by the string processing the escape in one case and the pattern engine in the other |

## String normalization

### Why this earns its own class of tests

Normalization decides which strings compare equal, so a defect in it does not fail; it quietly
widens or narrows every string comparison a predicate makes. It is also the class most exposed to
scripts and encodings that nobody on the project reads, where a wrong result looks no different
from a right one. The rules in [Normalization](concepts/normalization.md) have not been reviewed by
an expert, which makes it more important, not less, that what they say is pinned down by tests:
whatever is later found to be wrong should be found by changing a test, not by noticing a
mismatch.

The tests below began as smoke tests run while choosing between candidate normalizations. Most of
them are cases that the first, simpler rule got wrong.

### What the tests must establish

**Every step of normalized mode, on a script it is meant to affect and on one it is meant to
leave alone.** Accent removal is limited to Latin and Greek letters, so each test of a removal
needs a neighbour asserting that a similar-looking mark elsewhere survives.

**Literal mode changes nothing visible.** It composes characters and repairs malformed text, and
must otherwise hand back what was recorded.

**Normalization is total.** No input, however malformed, may make it throw; a run must never stop
because a tag holds text that is not valid UTF-16.

**The regex subject is prepared as every other operand is.** The pattern is not, and the
consequences of that are asserted directly.

### Scenarios to cover

| Scenario | What it is there to catch |
|---|---|
| `Motörhead`, `METALLICA`, `İstanbul`, `Βαγγέλης` against their unaccented lowercase forms | Equal: case folding and accent removal on Latin and Greek |
| `ΟΔΥΣΣΕΥΣ` against `οδυσσευς` | Equal: final sigma, which plain lowercasing misses |
| `Ёлка` against `елка` | Equal: the one Cyrillic accent that is folded |
| `Йога` against `иога`, and `Київ` against `Киів` | Different: `й` and `ї` are letters, and their marks must survive |
| `Ørsted`, `Straße`, `Ænima`, `Łódź`, `Đà Nẵng`, `Þursaflokkurinn` against `orsted`, `strasse`, `aenima`, `lodz`, `da nang`, `thursaflokkurinn` | Equal: each entry of the letter table, which decomposition cannot reach |
| `Đội` against `doi` | Equal: a letter carrying two diacritics, which a per-character table would miss |
| `ガンダム` against `カンタム` | Different: kana voicing marks are not accents |
| `कुमार` against `कमार`, and `ไม้` against `ไม` | Different: Devanagari vowel signs and Thai tone marks are kept |
| Hebrew with points against the same word without | Different, as the rules currently stand |
| `방탄소년단` against itself, both normalized | Equal: Hangul survives decomposition and recomposition intact |
| `ＹＭＯ` against `ymo`, `ｶﾞﾝﾀﾞﾑ` against `ガンダム`, and `ﬁre` against `fire` | Equal: compatibility forms fold, including halfwidth kana recomposing with its voicing mark |
| `Radio` and `head` joined by a soft hyphen, and `❤` followed by a variation selector | Equal to `radiohead` and to a plain `❤`: default-ignorable characters are removed |
| `ö` stored precomposed against `o` followed by a combining diaeresis, in both modes | Equal in both: literal mode still composes |
| `Motörhead` against `Motorhead`, and `Metallica` against `metallica`, in literal mode | Different: literal mode folds neither accents nor case |
| A string holding an unpaired surrogate, in both modes | Must not throw; the surrogate becomes U+FFFD |
| A string literal whose escapes leave an unpaired surrogate | A static error, reported before any file is opened |
| `^mot`, `^MOT` and `motö` against the subject `Motörhead` | The first two match, the third does not: the subject is prepared, the pattern is not, and the match ignores case |
| `소년` against `방탄소년단`, and `ガン` against `ガンダム` | Both match: the prepared subject must be recomposed for a pattern written in composed form to find it |
| `strasse` against the subject `Straße` | Matches: letter folds apply to the subject as well |
| `otö` against `Motörhead` under `LITERALLY()` | Matches: literal mode keeps the subject's diacritics |
| `abc` against `abd`, and U+FFFD against U+1F600 | Ordered as written: strings compare by code point, so U+FFFD sorts before a character outside the BMP even though its UTF-16 code unit is larger |

## Unreadable files and pathspecs

### Why this earns its own class of tests

Every other class here is about getting a computation right. This one is about what happens when
the environment refuses, and it earns attention for two reasons that have nothing to do with the
rules being subtle.

The first is that these paths are **expensive to provoke and therefore tend to go untested**.
Asserting that a permission error warns rather than aborting needs a file whose permissions were
deliberately broken. Asserting what happens when a file disappears mid-run needs it removed
between discovery and processing. Asserting what a damaged file does needs bytes written by hand
that no tagger would ever produce. Each is a small piece of fixture work that is easy to skip, and
the result of skipping all of them is that the least-exercised code in the program is the code
that runs when a collection turns out to be in exactly the state Goro was built for.

The second is that the guarantee under test is the central one. A run over a hundred thousand
files must not abort because one of them is damaged, and it must not quietly return an incomplete
answer as though it were a complete one. Either failure is worse than the condition that provoked
it.

### What the tests must establish

**A file that cannot be read never stops the run.** Whatever the cause, processing continues with
the next file and the run reports completion.

**It warns exactly once.** Not once per predicate sub-expression that would have touched the file,
and not once per attempt to read it: one file that cannot be read is one warning.

**Each command's output says so in the way its own question implies.** This is the assertion most
worth writing, because the tempting implementation -- leaving the file out -- is correct for
`list` and wrong for `hash`, and no test of the happy path would reveal the difference.

**A file the run never had to open cannot fail to be read.** Which files are exposed to this
depends on what the command needs from each one, so the absence of a warning is as much a property
under test as its presence.

**The exit code distinguishes an incomplete answer from a merely remarkable one.** The precedence
rules matter more than the individual codes, since they are what a script branches on and what no
single-condition test exercises.

### Scenarios to cover

| Scenario | What it is there to catch |
|---|---|
| A file whose permissions deny reading | Must warn and continue, and the run must complete rather than abort |
| A file removed between discovery and processing | The same treatment; the window is real on a large collection, so this must not be a distinct failure mode |
| A file that is not audio at all, and one whose audio is truncated | Must warn and continue, not propagate an exception from the tag library |
| A predicate mentioning only `file::path`, `file::name` or `file::extension`, against a file that cannot be opened at all | Must **not** warn, and must evaluate: nothing was read |
| A predicate mentioning only `file::size`, against a file whose tags cannot be read | Must **not** warn: nothing needed the tags, so nothing failed |
| The same file under a predicate mentioning a tag identifier | Must warn, and the file must not be listed |
| `goro list` with no filter, over a file that cannot be opened | Must list it and must not warn, the command having needed nothing but the path |
| `goro hash` over an unreadable file, in both output formats | The row must appear, with `-` in plain and `null` in JSON. Omitting the row is precisely the failure this scenario exists to catch |
| `goro list --filter` over an unreadable file | The file must not be listed |
| `goro list --filter` over a file whose predicate evaluates to unusable | The file must not be listed, and the data warnings that made it unusable must be on standard error |
| The same file under the same predicate wrapped as `FALLBACK(..., TRUE)` | The file must be listed: the predicate, not the command, decides here |
| One unreadable file among many readable ones, with `--strict-exit-code` | Code `11` |
| An unreadable file together with a tag that could not be interpreted | Code `11`, not `10`: within a group the higher-numbered code wins |
| An unreadable file in a run where nothing matched | Code `11`, not `20`, an unreadable file not counting as examined |
| A data warning and an empty result, with no unreadable file | Code `10`, not `20`: the `1`x group takes precedence over the `2`x group |
| All of the above without `--strict-exit-code` | Code `0` in every case; the option must be the only thing that surfaces any of this |
| A pathspec naming a file that does not exist | An error before anything is processed, code `2`, and no output at all |
| A pathspec naming a directory that cannot be listed at all | The same, since it is equally knowable before the run starts |
| A glob matching nothing | Not an error: it contributes no files, and with nothing else matching the run returns `21` |
| A subdirectory that cannot be listed, inside a directory pathspec | Must warn and continue, and every sibling entry must still be processed |

## Warning suppression

### Why this earns its own class of tests

`--no-warn` makes a promise about the *absence* of evidence: a suppressed warning was not
produced, and nothing anywhere distinguishes a condition that was suppressed from one that never
occurred. A promise of that shape cannot be checked by confirming that some output is missing,
because almost any defect also makes output missing. It has to be checked by running two things
that should be indistinguishable and asserting that they are, across every channel at once.

The second reason is that suppression reaches the exit code, which is the part a script depends on
and the part that no amount of testing standard error would touch.

### What the tests must establish

**Indistinguishability.** A run over data that warns, with that category suppressed, must agree
with a run over clean data in everything observable: standard output, standard error, and exit
code. That is the assertion, and it is a good deal stronger than "standard error was empty".

**Suppression reaches the exit code.** A suppressed category cannot produce its code, which is
what makes the `2`x codes reachable at all on a collection that warns routinely.

**The categories are independent.** Suppressing one must leave the other entirely alone, in both
channels.

### Scenarios to cover

| Scenario | What it is there to catch |
|---|---|
| A run over data that warns under `--no-warn=data`, against the same run over clean data | Standard output, standard error and exit code must all agree. A test checking only standard error would pass an implementation that still returned `10` |
| `--no-warn=data` where a data warning would have fired and nothing matched | Code `20`, which is the outcome the option exists to make reachable |
| `--no-warn=data` where the only file's predicate evaluated to unusable | Code `20`: the file was examined and not listed, and nothing reports why, the caller having said uninterpretable data is not a problem |
| `--no-warn=data` where a file also could not be read | Code `11` regardless: the other category is untouched |
| `--no-warn=file` where a file could not be read and a data warning fired | Code `10`, with the file warning absent from standard error |
| `--no-warn` bare, `--no-warn=all`, and `--no-warn=data,file` | All three identical in every channel |
| `--no-warn=data` on a run with no unusable data at all | Identical to the same run without the option: suppressing something that did not happen must change nothing |
| An unrecognised category name | Rejected as a command line error with code `2`, before anything is processed |
| A category name in a different case | Accepted: category names are case-insensitive, as the other option values Goro takes already are |

