# Testing

This document records the classes of test that Goro considers important, why each one earns
dedicated attention, and the scenarios that must be covered for each. It is not a list of every
test in the suite: ordinary unit tests for ordinary code need no entry here. A class of test
belongs in this document when getting it wrong would produce **silently wrong results rather
than a visible failure**, or when correctness rests on behaviour Goro does not control.

## Reading audio files

### Why this earns its own class of tests

Goro reads tag structures, playing time and the audio's position itself, from formats written by
decades of taggers and encoders that each read the specifications their own way. Every predicate
that touches a tag, every `file::duration` and every hash rests on that reading, and nearly every
way of getting it wrong is silent:

- **Text read slightly wrongly is still text.** A value decoded in the wrong byte order, a frame
  read under the wrong name, or a slash taken for a separator gives a well-formed string, and the
  symptom is a predicate quietly matching the wrong files.
- **Damage that is handled wrongly looks like ordinary data.** A reader that runs past a broken
  frame size hands back the frames after it as text, and one that gives up too early turns data
  that was there into absence.
- **A playing time a few tens of milliseconds out changes the whole second** for a few files in
  every hundred, and nothing in the output says which.
- **A hash range a few bytes out either takes in tag data,** so that a tag edit looks like bit rot,
  or leaves out audio, so that bit rot there goes unseen.

A failure in this area does not crash and does not show up in output. It silently changes which
files a command operates on, or what a stored hash means, which for a tool whose purpose is to
decide what to touch is the worst failure mode available.

### What the tests must establish

**Version independence.** For each concept Goro defines, and each frame a source function names
by its v2.4 identifier, tag content that is semantically identical must produce an identical value
whether it was stored as Id3v2.2, v2.3 or v2.4. This is the property that the documentation
promises, so it is the property that must be tested directly rather than inferred from the parts.

**Faithfulness of the source functions.** `field()` must yield the datum's text as recorded,
uninterpreted, and `bytes()` its content as recorded, with only the storage transformations the
format defines undone.

**What damage yields.** Each kind of damage must resolve as
[Built-in identifiers](features/builtins/identifiers.md#absent-usable-and-unusable-data) says:
an unusable occurrence where a datum is clearly recorded and cannot be read, absence beyond a break
and for a tag that cannot be read at all, the rest of the file unaffected, and the file counted in
the `incomplete` warning exactly when its structure is broken or its playing time cannot be had.

**The reader never throws over what a file holds.** The corpus alone does not show this: the
prototype passed it, and a fuzzer damaging its files still found 16 exceptions. The suite
therefore carries a fuzz test, truncating and overwriting every corpus file at random, that
asserts the analysis returns for every one.

**Reads stay at the edges.** The analysis behind tags and `file::duration` must never walk the
whole file. Its reads are logged, so the test asserts what was read rather than inferring it from
timing: on a long file, every read lies within the windows at either end or a bounded search.

**Expected values are independent of the reader.** The corpus manifest records what each fixture
holds, written by the corpus builder rather than read back by the reader under test, so that a
misreading cannot land in both the code and its expectation.

### Fixtures

The fixtures are the corpus in `tests/Goro.Tests/Fixtures/Audio`, whose README lists every file,
and tag bytes built by hand in a test where a scenario needs a shape of its own. Nothing is written
through a tag library: a writer and a reader that share assumptions conceal exactly the defects a
file from another tagger would show. The corpus's `rw-` files rebuild shapes found in other
projects' test data, which is not in the repository; they are what keep the quirks in
[quirks](design/quirks.md) tested.

### Scenarios to cover

Every scenario in the first table is to be exercised for **each of Id3v2.2, v2.3 and v2.4**, both
through concepts and through source functions.

| Scenario | What it is there to catch |
|---|---|
| A value containing a forward slash, such as an artist named `AC/DC` | Outside genres a slash is part of the value; a single value must not become two |
| A value containing a semicolon, such as `Rock; Metal` | Goro's own split applies to genres only, and every resulting value must be trimmed -- `Metal` and not `" Metal"`, the space after a separator being the commonest convention there is |
| A genre written as a bare number, `17` | Must expand to the named genre on every version |
| A genre written as a reference, `(17)` | The parenthesised form is legal in v2.2 and v2.3 and out of spec in v2.4, but occurs there after a version migration |
| A genre reference with a refinement, `(17)Post-Rock` | Must yield both the referenced name and the refinement, on every version |
| Several genre references, `(51)(39)` | Every reference must be expanded, not only the first |
| A genre reference with an escaped parenthesis, `(17)((weird)` | The doubled parenthesis must be unescaped, on every version |
| `(RX)` and a bare `RX`, and the same for `CR` | Both must map to the same result |
| A `TCON` frame through `field()`, holding `(17)/Post-Rock` | The text as recorded, slash and parentheses included: the genre conventions belong to the concept |
| A track number written `3/12` | Must yield the track and not be mistaken for two values |
| A date in each accepted timestamp form, and one in no accepted form | The latter must produce an unusable occurrence, not a wrong date |
| A date-shaped field holding `0000` | Must be unusable; there is no year zero, and resolving it to the number zero would match a great many files silently |
| A string-typed field that is present but empty, and one present but only whitespace | Both must be **absent** through the concept, a file whose frame holds three spaces having no artist by any reading a person would recognise |
| The same two fields through `field()` | A usable empty string and a usable three-space string respectively: a source function trims nothing, and this is where the information the concept set aside is still visible |
| A value with whitespace at one end, through a concept and through `field()` | Trimmed through the concept and untouched through `field()` |
| A value with whitespace *inside* it, such as `" AC / DC "` | Must yield `AC / DC`: trimming takes the ends and never the middle |
| A number-typed field, such as the one behind `id3v2::year`, present but empty or only whitespace | Must yield an unusable occurrence: the frame was written and holds no year |
| The same frame repeated, with and without distinguishing descriptions | Must yield a multivalue of the expected cardinality |
| A single text frame holding several NUL-separated values, in each revision | The only spec-sanctioned source of multiple values in one frame in v2.4, and read the same way in v2.2 and v2.3 |
| A text frame Goro has no particular knowledge of, such as `TZZZ` | Must be readable through `field()` |
| An `APIC` frame holding a picture | Must be a usable blob through `bytes()`, holding the frame's content as recorded, so that a predicate can tell a file that has cover art from one that does not; `id3v2::field("APIC")` must be an error |
| A `TYER` frame, in a v2.3 tag and in a v2.4 tag | Read as `TDRC` in both: the name depends on the frame and never on the revision the tag declares |
| A v2.3 tag holding `TYER`, `TDAT` and `TIME` | `id3v2::field("TDRC")` must be the year alone, and `TDAT` and `TIME` readable under their own names |
| `TXXX`, `COMM` and `USLT` frames with several descriptions, differing in letter case | `field()` with a description must read only the frames whose description matches it without regard to case, and without one must read them all |

How Id3v2 stores a frame, and what real files do with it:

| Scenario | What it is there to catch |
|---|---|
| A tag unsynchronised as a whole, and a single frame unsynchronised with a data length indicator | `bytes()` gives the content with the inserted bytes removed, and the flag prefix left out |
| A compressed frame, in v2.3 and in v2.4 | `bytes()` and `field()` read the decompressed content |
| A compressed frame whose zlib does not inflate, and one that would inflate past the cap | Unusable through both `bytes()` and `field()`, and the frames around it intact |
| An encrypted frame | Unusable through both: no reader can know its content, even when it happens to be plain text |
| An extended header, and a v2.4 footer | Stepped over, every frame read |
| An Id3v2.4 tag appended after the audio | Read as the file's `id3v2` source |
| Several Id3v2 tags one after another | Only the first is the `id3v2` source: values of the others must not appear |
| `TIT2` written twice | Two occurrences, though the format forbids it |
| UTF-16 text without a byte order mark | Read as little-endian |
| A second terminator-separated UTF-16 value without its own byte order mark, in v2.3 | Read in the first value's byte order |
| A frame with an illegal identifier and a good size | Stepped over by its size, the frames after it read |
| `GRP1`, `MVNM` and `MVIN` | Text frames: readable through `field()`, where any other non-`T` frame is an error |
| Every frame size written as a plain integer, as iTunes once did | Read as the writer meant, every frame intact |

Damage, for every tag format an MP3 carries. Each row also asserts whether the file is counted in
the `incomplete` warning, and that it is never unreadable on this account.

| Scenario | What it is there to catch |
|---|---|
| A value declared UTF-8 that is not, and UTF-16 of odd length | One unusable occurrence through `field()`, its bytes intact through `bytes()`; never U+FFFD, and not counted as incomplete |
| An encoding byte no revision defines | The same |
| A frame description that does not decode | Unusable for every description asked about, and without one |
| A frame whose size runs past the end of its tag | That frame unusable, the frames before it intact, nothing after it found; counted |
| A zero-size frame | That frame unusable, having no encoding byte, and the frames after it read; not counted |
| A block of zeroes in the middle of a tag | What lies before the break intact, what lies after absent; counted |
| A tag size past the end of the file, one not syncsafe, and a version byte of 5 | The `id3v2` source absent everywhere, other tags and `file::duration` unaffected; counted |
| A tag size too short, so the tag's end lies inside a frame | The same, the audio still found behind it |
| A file ending inside its Id3v2 tag | Unreadable, there being no audio |
| An APE item running past its tag, an item count too high, and a tag size reaching before the start of the file | As for the corresponding Id3v2 damage |
| APEv2 text that is not valid UTF-8 | One unusable occurrence through `field()`, a usable blob through `bytes()` |
| APEv1 text outside ASCII | Read as ISO-8859-1, usable |
| Random bytes after `TAG` | Read as an Id3v1 tag of whatever they spell: nothing can tell |

APE, which has one revision worth testing and no frames:

| Scenario | What it is there to catch |
|---|---|
| A text item holding two values | Two occurrences through `field()`, and one through `bytes()`, which never splits |
| A binary item | One unusable occurrence through `field()`, and one usable blob through `bytes()` |
| Two items whose keys differ only in case | Only the last is seen, through every concept and source function alike |
| A tag with a footer only, one before the audio, and one behind a Lyrics3v2 block | Each found and read |

Playing time, for MP3. Every row asserts the whole seconds `file::duration` gives, and whether the
file is counted in the `incomplete` warning.

| Scenario | What it is there to catch |
|---|---|
| A CBR file with a LAME tag | The encoder delay and padding removed: 3.300 s and not the 3.344 s of its frames |
| A VBR file with a Xing header, and one with a VBRI header | The header's frame count, trimmed |
| A first frame that does not follow the tag directly: junk, an APE tag, a tag size too short | The LAME tag still found, and the Info frame not counted as audio |
| A Xing frame count doubled, so that the LAME CRC fails | Unusable, not twice the length |
| A file cut in half, its header describing the whole | Unusable: the byte count disagrees |
| Junk appended after the last frame, a frame ending exactly where the header says | The header trusted, the junk not counted |
| Two files joined, a frame starting where the first one's header ends | Unusable |
| A CBR file without a summary header | Counted from its edges, exact |
| The same file truncated mid-frame | Unusable: no frame ends where the trailing tags start |
| A VBR file without a summary header | Unusable, not an estimate: no scan |
| Junk after a tag that could pass for a free-format frame | Not taken for one without a third frame at the same distance |
| A playing time of 3.9 seconds | 3 seconds: truncated, not rounded |

The hash range, for MP3:

| Scenario | What it is there to catch |
|---|---|
| The same audio under every tag layout in the corpus, and under every edit to a tag: values, sizes, padding, a tag added or removed | One hash for all of them. This is the property the hash exists for |
| An Info frame rewritten, or the LAME tag's gapless counts changed | The hash unchanged: they belong to the container |
| Junk before the first frame and after the last, and an APE tag behind a Lyrics3v2 block | None of it hashed |
| A bit flipped inside an audio frame | The hash changes |

### Concepts written without a source

A concept written without a source is resolved across up to four tag formats in order of
preference, as a `PREFERRED()` of its cells, and which format supplies the answer is decided by whether a format produced a *usable* occurrence
rather than merely a present one. Getting that wrong returns a value from the wrong tag without
any visible symptom, which is the same failure mode as the rest of this class and earns the same
treatment. The scenarios are few and they are exhaustive, so they should all be written.

These require a file carrying more than one tag format, so the fixtures differ from those above.
The same scenarios are asserted of `PREFERRED()` itself, over identifiers that need no file, and
each concept written without a source is asserted to be built from its cells in the documented order.

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

Interpreting the *content* of a binary frame is out of scope: nothing in the language reads a
blob's content yet. What is in scope, and is listed above, is that `bytes()` gives that content as
recorded, and that a frame is a blob rather than absent, since that distinction is what lets a
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

**The operators never answer from unusable data.** Where a comparison, range, regex or logical
operator gives true or false, it must give the same answer whatever the unreadable data had held.
This is a property of the operators rather than of predicates: state tests, `FALLBACK()`,
`PREFERRED()` and concepts written without a source make a result depend on unreadable data by design, and so
does comparing a condition with a boolean. Two properties together establish it over predicates
built from the operators alone, and both are suited to generated predicates rather than
hand-picked ones: such a predicate without `NOT` selects exactly the same files whether unusable
results are kept or read as false, and a predicate with `NOT` that answers at all gives the same
answer for every usable replacement of its unusable occurrences.

**The logical operators follow their table, and short-circuit only on a settling operand.** Every
row of the table in the predicate documentation should be asserted, for both operand orders. The
short-circuit is observable through warnings, so it needs asserting there: an unusable first
operand of `AND` or `OR` must lead to the second operand being evaluated, and a false first operand
of `AND`, or a true one of `OR`, must not.

**`PREFERRED()` stops at the first usable argument, and passes over the rest in silence.** The
arguments after the one chosen must not be evaluated, which shows only in what evaluating them
would have done: a later argument whose data would make the file unreadable must leave it readable.
The unusable occurrences of an argument passed over must not warn, even when the operator consuming
the result warns about the argument chosen.

**An unusable boolean is never reported twice.** Comparing, testing, substituting for or combining
an unusable boolean emits nothing beyond what the operator that produced it emitted.

**The state test truth table holds in full.** The table in the predicate documentation is small
enough to assert in full, and should be. Its absent row matters most, being where
`ALL(x) IS USABLE` and `NOT x IS UNUSABLE` part company.

**The regex operator normalizes its subject and not its pattern.** Every other operator normalizes
every string it compares, so an implementation that reuses the ordinary path for `=~` will
normalize the pattern too, and the symptom is a pattern that matches slightly different files
rather than any kind of failure. The asymmetry therefore needs asserting from both sides: that a
diacritic in the pattern matches nothing, and that the same pattern under `LITERALLY()` matches the
subject that still has one.

**Warning identity is structural.** The interning rules hold: differences of whitespace, letter
case, string form, the case of a name a source function matches without regard to case, and
modifier wrapping do not produce distinct sources,
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
| `artist BETWEEN "ma".."mi"` against "Miles Davis", "Metallica" and "Motörhead", and `"the".."the"` against "The Beatles" and "Th" | True, true, false, true, false: a string endpoint is compared with only as many characters as it has. Plain string order would silently leave out every name beginning with the maximum |
| `artist BETWEEN "mi".."m"` and `artist BETWEEN "mz".."ma"` | The first is valid, holding every name beginning with "mi"; the second is reversed, an error. Reversal is judged by the same prefix comparison |
| `ALL(x) == v` and `ALL(x) IS USABLE` for an absent `x` | Both false; universal quantification must not be vacuous |
| Every row of the state test truth table, including the absent row | The universally quantified tests must be false for an absent value, not the negations of the existential ones |
| `ANY(x) != v`, `ALL(x) != v` and `NOT x == v`, for an absent `x`, an unusable `x`, and a multivalue holding one of each | The last two agree everywhere except for the absent `x`, which only `NOT x == v` selects; `ANY(x) != v` is not the complement of `x == v` for the absent and multivalue cases and must give the documented answer; for the unusable case all three must be unusable |
| `x < v` against `NOT x >= v`, for a single unusable `x` | Both unusable: negation must not turn a comparison that could not be answered into a true one |
| `x == v` and `ALL(x) == v` and `ALL(x) == w`, for `x` holding one occurrence equal to `v` and one unusable | True, unusable and false respectively: an answered combination settles a quantifier whenever it can, and only otherwise does the unusable one decide |
| `x > 1 AND y > 1`, with `x` unusable and `y` unusable | Two warnings: an unusable first operand must not settle `AND` |
| `FALSE AND y > 1` and `TRUE OR y > 1`, with `y` unusable | No warning: a settling first operand must short-circuit |
| `FALLBACK(x > 1, FALSE)` against `FALLBACK(x, 0) > 1`, with `x` unusable | One warning and none respectively: substituting for a comparison's result does not undo the comparison's warning |
| `(x > 1) == (x > 1)` and `(x > 1) IS UNUSABLE`, with `x` unusable | One warning in total for the first and none beyond the comparison's for the second; the second is true |
| A boolean as an operand of `<`, `BETWEEN` or `=~`, or as the operand of `AS` | A static error: booleans are unordered and not convertible |
| `(a == b) == (c == d)` and `a == b == c` | The first is valid and compares two booleans; the second is still a syntax error |
| A guard whose right operand would warn, and the same guard with the operands transposed | Short-circuiting must hold, since the observable difference is a warning that is or is not emitted |
| An operator that finds a match among the usable occurrences of a bag that also holds an unusable one | The warning must be emitted anyway, since iteration within an operator does not short-circuit; a short-circuiting implementation would emit it or not according to the order of an unordered bag |
| The same bag with its occurrences supplied to the operator in the reverse order | The result, the warnings and the exit code must all be identical, which is the property exhaustive iteration exists to deliver |
| A source written twice, as in `m == M`, over a bag holding a usable and an unusable occurrence, in both orders | The warning quotes `m` both times: the spelling written first, not the one an operator happened to reach first |
| `AS NUMBER` of a tag string with valid number syntax but more significant digits than a number holds | An unusable occurrence, not a rounded number |
| `file::extension` of `a.flac`, `a.tar.gz`, `.hidden`, `README` and `trailing.` | `flac` and `gz`, then absent three times: no dot, a leading dot only, and nothing after the last dot |
| `file::path` of a file on Windows | Written with `/` throughout, drive letter included, so that the same predicate means the same thing on every platform |
| `file::path` and `file::name` of a file reached through a symbolic link | The link's path and name, not the target's: paths are as discovered |
| `AS STRING` of `1.50`, `.5`, `+5` and `-0` | `"1.5"`, `"0.5"`, `"5"` and `"0"`: one canonical form, whatever the value was written as |
| `AS STRING` of `4m5s`, `4:05`, `1.4kib` and `0b` | `"245s"`, `"245s"`, `"1433.6b"` and `"0b"`: the canonical literal of the value's own type, always in its base unit |
| Every value of every type converted `AS STRING` and back to its own type | The value it came from: the round trip must hold through the literal grammar alone |
| `"4:05"`, `"4m5s"` and `"245"` as durations, and `"10kib"` and `"10240"` as bytecounts | One duration and one bytecount respectively: a literal of the target type, or a plain number of seconds or bytes |
| `"1.5"` and `"-1"` as durations, `"-1"` as a bytecount, `" 5"` as a number, and `"245s"` as a number | Each unusable: a fraction of a second, a negative unit, whitespace around the literal, and a literal of another type |
| `1.5 AS DURATION`, `-1 AS BYTECOUNT` and `"x" AS NUMBER` | Each a static error, a conversion of a constant that fails |
| `file::duration AS BYTECOUNT` and `file::size AS DURATION` | Each a static error: a duration and a bytecount do not convert into each other |
| `x AS NUMBER > 5`, `x AS NUMBER AS STRING`, and `x AS foo` | A conversion binds more tightly than a comparison and chains left to right; an unknown target is an error offering the closest one, the targets not being reserved |
| `ALL(x) AS NUMBER > 5` | An error, the operand of `AS` taking no modifier; the diagnostic must offer `ALL(x AS NUMBER) > 5` |
| `ANY(x) AS NUMBER != 1`, and `LITERALLY(s) AS STRING BETWEEN "B".."a"` | One error each: the rest of the predicate is checked with the misplaced modifiers where the rewrite puts them, so `!=` sees its quantifier and the range is judged in literal mode. `ALL(x) AS NUMBER IS ABSENT` is two errors, its rewrite being wrong too |
| `NUMBER(x) > 5`, `STRING(x) == "a"`, `DURATION(x) > 1m` and `CAST(x AS NUMBER) > 5` | Each a single error offering the `AS` spelling, and nothing else reported for the same predicate |
| `x AS NUMBER` with `x` unusable, and over a multivalue holding one occurrence that converts and one that does not | The unusable occurrence keeps its source, the failed one takes the conversion as its source, the cardinality is kept, and nothing warns until an operator consumes them |
| A value that is absent, and one that is a single unusable occurrence, passed to `COUNT()` | Must be 0 and 1 respectively, and neither may warn |
| Each row of the warning deduplication table | Source identity must be structural and independent of position |
| Each condition the predicate documentation calls an error | Every one reported with nothing processed, and with the exit code that says the run never started |
| A `=~` whose pattern holds a diacritic, against a subject that holds the same one | Must not match: the subject is normalized and the pattern is not |
| The same pair under `LITERALLY()` | Must match: an unnormalized subject keeps its diacritics |
| A `=~` whose pattern differs from the subject only in case | Must match by default, and must not under `LITERALLY()`, since the modifier makes the match case-sensitive |
| A pattern that would change meaning if it were decomposed, such as `e` followed by a combining acute and `?` | Must be matched as written; an implementation that normalizes the pattern turns it into a different pattern rather than a differently-spelled one |

## Predicate reading

### Why this earns its own class of tests

The classes above are about what a predicate means once it has been read. This one is about the
step before that, and it earns separate attention because its failure mode is the sharpest in the
document: the same predicate text yielding a *different value*, with no error raised anywhere. A
literal read wrongly does not misbehave visibly. It silently selects a different set of files,
and the predicate on the command line still looks exactly like what the user meant.

Two things concentrate the risk.

**A string literal has two forms.** A quoted string processes escapes and a raw string does not.
Most of the ways to get this wrong produce a valid string that is not the intended one: dropping a
backslash before a character that is not an escape, reading more or fewer digits than `\x` and `\u`
take, or mishandling the doubled quote that terminates a raw string.

**Tokenisation rests on a rule the productions cannot state.** A token is always as long as it
can be, and several literals are only unambiguous because of it. Most mistakes here surface as
parse errors, which are visible and therefore cheap, but not all: the doubled-quote rule inside a
raw string decides a value rather than whether the text parses.

### What the tests must establish

**Every escape produces exactly its character, and nothing else is an escape.** The list in the
predicate documentation is closed, so the tests are the list plus the negative case: a backslash
followed by anything not on it is an error. That negative case matters more than it looks, because
the tempting implementation -- pass the unknown character through and drop the backslash -- is what
several languages do, and it would turn a mistyped string into a quietly different one.

**A raw string is what was written.** Byte for byte, backslashes included, with a doubled quote
standing for one quote and the string ending only at a quote that stands alone.

**The two forms are indistinguishable once read.** A quoted string and a raw string denoting the
same characters are one value, compare equal, and count as one warning source.

**The longest token wins.** Asserted on the literals where it decides something, rather than as a
property of the lexer in the abstract.

Static rejection of a bad pattern belongs to the error-reporting guarantee asserted in the class
above, and is not restated here; what this class adds is that the *unsupported* constructs are
rejected on the same terms as the malformed ones, and that a pattern is accepted only as a raw
string written directly after the operator.

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
| `ape :: artist` and `ape:: artist` | Each a syntax error, an identifier being written without whitespace around its `::`; the diagnostic offers the identifier without it |
| `::artist`, `::file::size` and `ape::"artist"` | Each an error: there is no leading `::` and no quoted part. The diagnostics offer `artist`, `file::size` and `ape::field("artist")` |
| `id3v2::track::x` | An error, a source being one level deep |
| `and::x`, `ape::and` and `NOT::x == 1` | Errors: the first two because nothing is named after a reserved word, the third because there is no leading `::`, the diagnostic offering `NOT x == 1` |
| `vorbis::field`, `field("MOOD")`, `ape::count(genre)` and `id3v1::field("x")` | Errors: a source function needs its arguments and its source, `COUNT()` belongs to no source, and Id3v1 has no source functions |
| `id3v2::field("TYER")`, `id3v2::field("APIC")`, `id3v2::field("TIT2", "x")` and `id3v2::field("TI")` | Errors: a renamed frame, offering `TDRC`; a frame that does not hold text, offering `bytes()`; a description given to a frame that has none; and an identifier of the wrong shape |
| `id3v1::size`, `file::artist`, `artistt` and `size` | Errors: a concept qualified by a source that cannot supply it, a misspelled concept with a suggestion, and a `file` concept written without its source |
| `(ALL(genre)) == "x"` and `COUNT((ALL(genre)))` | The first is valid, the same predicate as `ALL(genre) == "x"`; the second is rejected exactly as `COUNT(ALL(genre))` is. Parentheses only group, around a modifier as anywhere else |
| `genre != "x"`, `LITERALLY(genre) != "x"`, `FALLBACK(genre, "") != "x"` and `ALL(a) != b` | Each is an error: `LITERALLY` is not a quantifier, `FALLBACK()` settles absence but not several occurrences, and every operand that is not exactly one needs a quantifier of its own. The diagnostic for the first must offer both `NOT genre == "x"` and `ALL(genre) != "x"` |
| `file::extension != "mp3"`, `id3v1::genre != "blues"` and `id3v1::year != 1991` | Each is an error, its operand able to be absent but never several. The diagnostic must say that the operand may be absent and offer `NOT file::extension == "mp3"` and `FALLBACK(file::extension, "") != "mp3"`, with `0` as the default for a number; offering a quantifier here is the defect this row exists to catch |
| `ANY(genre) != "x"`, `LITERALLY(ALL(genre)) != "x"`, `COUNT(genre) != 1`, `file::size != 0`, `file::duration != 3m` and `(a == b) != (c == d)` | All valid: a quantifier anywhere in a stack of modifiers satisfies the rule, and an operand that is exactly one needs none |
| `FALLBACK(file::extension, "") != "mp3"`, `FALLBACK(id3v1::genre, "") != "blues"`, `PREFERRED(id3v1::genre, "x") != "y"` and `ANY(id3v1::genre) != "blues"` | All valid: the first three are exactly one, `FALLBACK()` and `PREFERRED()` removing absence from an operand that never holds several, and the last carries a quantifier, which still satisfies the rule though it was not needed |
| `FALLBACK(year, "5" AS NUMBER)`, `FALLBACK(artist, 5 AS STRING)`, `FALLBACK(year, "x" AS NUMBER)`, `FALLBACK(year, COUNT(genre))` and `FALLBACK(file::size, "5" AS NUMBER)` | The first two are valid, a conversion of a constant being a constant; the third is an error when the predicate is read, the conversion failing; the fourth is an error, `COUNT()` not being a constant; the fifth is a type mismatch, only a number literal standing for a bytecount |
| `TRUE`, `true` and `False` as literals, and `year == NULL` | The boolean literals are case-insensitive keywords; `NULL` names nothing and must be rejected with a diagnostic saying what to write instead |
| `artist =~ "^a"`, `title =~ artist`, `artist =~ (r"^a")`, `artist =~ LITERALLY(r"^a")` and `artist =~ ALL(r"^a")` | Each is a syntax error, the pattern being a raw string that is part of the operator. The diagnostic for the first must offer `r"^a"`, and the one for `LITERALLY` must offer `LITERALLY(artist) =~ r"^a"` |
| `artist ~= r"^a"`, `artist ~= "a"` and `artist !~ r"^a"` | Each is a syntax error, `~=` and `!~` being spellings borrowed from other languages. The diagnostic for each `~=` must offer both `=~` and `!=`, since either may have been meant, and the one for the second must offer `artist =~ r"a"` rather than `artist =~ "a"`; the one for `!~` must offer `NOT artist =~ r"^a"` |
| `FALLBACK(year, (0))`, `file::duration > (90)` and `("x") AS NUMBER` | A literal in parentheses is a literal: the first is valid, the second compares with ninety seconds, and the third is the same static error as `"x" AS NUMBER` |
| `(1)..2` | A syntax error: a range is built from literals by the grammar, which does not admit parentheses there |
| A pattern using each of the four unsupported construct families | Each must be a static error, reported before any file is opened, on the same terms as a malformed pattern |

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
| A file that is not audio at all, and one that ends inside its Id3v2 tag | Must warn and continue, not propagate an exception from the reader |
| A predicate mentioning only `file::path`, `file::name` or `file::extension`, against a file that cannot be opened at all | Must **not** warn, and must evaluate: nothing was read |
| A predicate mentioning only `file::size`, against a file in which no audio can be found | Must **not** warn: nothing needed the file's contents, so nothing failed |
| The same file under a predicate mentioning a tag concept | Must warn, and the file must not be listed |
| A file whose Id3v2 tag cannot be read at all, under a predicate mentioning a tag concept and under `goro hash` | Not unreadable: the predicate is evaluated with the source absent, the hash is computed, and the file is counted in the `incomplete` warning |
| A file whose tags are healthy but whose playing time cannot be had, under a predicate mentioning only `artist` | Counted in the `incomplete` warning: a file counts once it is opened, whatever the predicate asked |
| Several incomplete files, one also unanswered and one unreadable | One `incomplete` warning for the run, after the `unanswered` one, counting the first two and not the unreadable file |
| A predicate that meets uninterpretable data in a file and then finds the file cannot be read, such as `file::name AS NUMBER > 1 OR artist == "x"` | One file warning and no data warning: a file that was not processed has nothing to say about its data |
| `goro list` with no filter, over a file that cannot be opened | Must list it and must not warn, the command having needed nothing but the path |
| `goro hash` over an unreadable file, in both output formats | The row must appear, with `-` in plain and `null` in JSON. Omitting the row is precisely the failure this scenario exists to catch |
| `goro list --filter` over an unreadable file | The file must not be listed |
| `goro list --filter` over a file whose predicate evaluates to unusable | The file must not be listed, the data warnings that made it unusable must be on standard error, and the run must end with one `unanswered` warning counting it, after every other warning |
| The same file under the same predicate wrapped as `FALLBACK(..., TRUE)` | The file must be listed, and there must be no `unanswered` warning: the predicate, not the command, decides here |
| Several files whose predicates evaluate to unusable | One `unanswered` warning for the run, not one per file, with the count and the number examined right |
| One unreadable file among many readable ones, with `--strict-exit-code` | Code `13` |
| An unreadable file together with a tag that could not be interpreted | Code `13`, not `10`: within a group the higher-numbered code wins |
| An unreadable file in a run where nothing matched | Code `13`, not `20`, an unreadable file not counting as examined |
| An incomplete file together with an unanswered predicate | Code `12`, which outranks `11` |
| An incomplete file together with an unreadable one | Code `13` |
| An incomplete file in a run where nothing matched | Code `12`, not `20`: the file was examined, and the `1`x group takes precedence |
| A data warning and an empty result, with no unreadable file | Code `10`, not `20`: the `1`x group takes precedence over the `2`x group |
| A file whose predicate evaluates to unusable, with `--strict-exit-code` | Code `11`, which outranks the `10` its data warnings would give |
| An unanswered predicate together with an unreadable file | Code `13`, and the unreadable file not counted in the `unanswered` warning |
| An unanswered predicate in a run where nothing else matched | Code `11`, not `20`: the file was examined, and the `1`x group takes precedence |
| All of the above without `--strict-exit-code` | Code `0` in every case; the option must be the only thing that surfaces any of this |
| A pathspec naming a file that does not exist | An error before anything is processed, code `2`, and no output at all |
| A pathspec naming a directory that cannot be listed at all | The same, since it is equally knowable before the run starts |
| A glob matching nothing | Not an error: it contributes no files, and with nothing else matching the run returns `21` |
| A glob with two `*`s, and one with a wildcard in a directory component, such as `music/*/a.mp3` | Each an error before anything is processed, code `2`, and no output at all |
| A subdirectory that cannot be listed, inside a directory pathspec | Must warn and continue, and every sibling entry must still be processed |
| A pathspec naming a file that does not exist, given after a directory holding MP3 files | The same: every pathspec is resolved before any file is processed, so nothing from the directory may be output first |
| A directory holding `a.mp3`, `b.MP3` and `cover.jpg` | Only the two MP3 files, whatever the case of the extension, and no warning for the image |
| A file with another extension named directly, and nothing else | Not an error and no warning: no file is considered, and with `--strict-exit-code` the run returns `21` |
| A glob such as `dir/*.mp3`, over MP3 files directly in `dir` and in its subdirectories | Only those directly in `dir`: a glob matches the entries of one directory |
| A glob matching a subdirectory and a file with another extension | The subdirectory is walked in full, and the file is passed over |
| A file with the extension `mp3` that holds no audio, under `goro list` without a filter | Listed, without a warning: a file is judged by its name, and nothing had to be read |
| A file named `.mp3`, and one named `track.mp3.bak` | Neither is considered: the first has no extension at all, as for `file::extension`, and the second's is `bak` |

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

**The categories are independent.** Suppressing one must leave the others entirely alone, in both
channels.

### Scenarios to cover

| Scenario | What it is there to catch |
|---|---|
| A run over data that warns under `--no-warn=data`, against the same run over clean data | Standard output, standard error and exit code must all agree. A test checking only standard error would pass an implementation that still returned `10` |
| `--no-warn=data` where a data warning would have fired and nothing matched | Code `20`, which is the outcome the option exists to make reachable |
| `--no-warn=data` where the only file's predicate evaluated to unusable | Code `11`, with the `unanswered` warning the only line on standard error: suppressing data warnings must not hide a file whose answer the command decided |
| `--no-warn=unanswered` where a predicate evaluated to unusable | Code `10`: the data warnings remain, and the summary is gone |
| `--no-warn=data,unanswered` where the only file's predicate evaluated to unusable | Code `20`: the file was examined and not listed, and nothing reports why, the caller having said neither is a problem |
| `--no-warn=data` where a file also could not be read | Code `13` regardless: the other categories are untouched |
| `--no-warn=incomplete` where an incomplete file's predicate evaluated to unusable | Code `11`, with the `incomplete` warning absent and the `unanswered` one still there |
| `--no-warn=file` where a file could not be read and a data warning fired | Code `10`, with the file warning absent from standard error |
| `--no-warn=all`, `--no-warn all` and `--no-warn=data,unanswered,incomplete,file` | All three identical in every channel |
| `--no-warn` given no categories, at the end of the line or followed by another option | Rejected as a command line error with code `2`, before anything is processed: a bare option must not quietly silence the warnings about files that cannot be read |
| `--no-warn=data` on a run with no unusable data at all | Identical to the same run without the option: suppressing something that did not happen must change nothing |
| An unrecognised category name | Rejected as a command line error with code `2`, before anything is processed |
| A category name in a different case | Accepted: category names are case-insensitive, as the other option values Goro takes already are |

