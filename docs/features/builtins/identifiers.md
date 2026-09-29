# Predefined identifiers

## DESCRIPTION

Goro predicate expressions can refer to a number of built-in identifiers. This document lists those identifiers and describes what each one represents. Each identifier is given together with its namespace, as `namespace::identifier`. Identifiers in the global namespace are given as simply `identifier`, since the global namespace is implied if no namespace is specified, but they can also be explicitly qualified as in `::identifier`.

Identifiers are shown below in their conventional casing, but predicate syntax is case-insensitive and they may be written in any case.

An identifier resolves to null when the tag data it refers to is not present in a file. If the data is present but cannot be interpreted as the type given in the tables below — for example a year field holding something that is not a year — the identifier resolves to a _tainted null_ instead, which behaves the same way but raises a warning. See [Predicates](../../concepts/predicates.md) for what that means in an expression.

### Definitions

#### Open, closed, and raw namespaces

Some namespaces such as `id3v1` are _closed_: the full set of valid identifiers in that namespace is statically known, and therefore any identifier can be accepted or rejected as invalid without any file-specific context. Other namespaces such as `vorbis` are _open_: there is an unbounded set of identifiers in them that can resolve to a non-null value depending on the specific file, and the members of that set cannot be determined before looking at the file. Open namespaces typically include some standard documented identifiers, and also a set of identifiers that directly resolve to tag data and derive their names from that.

Goro will reject a predicate that refers to an unknown identifier in a closed namespace after parsing the predicate and before touching any files. On the other hand, an identifier within an open namespace will never be rejected as invalid; if the data it is intended to resolve to does not exist, the identifier simply resolves to null.

An identifier in an open namespace derives the field name it looks for from its own name, and several tag formats permit field names that the rules for an identifier cannot spell -- most commonly names containing spaces, such as the APE item key `Album Artist`. Such a field is reached by writing that part of the identifier as a quoted string, as in `ape::"album artist"`; see [Predicates](../../concepts/predicates.md) for the syntax. Every field name these formats permit can be written this way.

Orthogonally to this, some namespaces are "raw". This is not a technical property of namespaces, but rather a design convention. Namespaces with a nested `::raw` part are intended to provide unfettered access to tag data without the possibility of resolving to a tainted null value; therefore, such namespaces (whether open or closed) will define identifiers **of string type only**. Naturally, these identifiers can, depending on context, still resolve to untainted null and to multivalues.

#### Parsing dates

A common feature of several namespaces is an identifier named `year`. In many cases, this identifier is of type number while the underlying tag data can be any arbitrary string. The rules for extracting a value out of such _date-shaped_ data are as follows:

1. The tag data is first trimmed of any leading and trailing whitespace. If nothing remains, the identifier resolves to an untainted null, exactly as if no tag data had been present at all.
2. All data extraction from what is logically (bar potential low data precision) an instant in time first conceptually resolves _a full Gregorian calendar date_ in all cases, and then extracts only the relevant parts from that resolved date. For example, the identifier `id3v1::year` will ultimately only resolve to a simple integer year value, but conceptually, it will first resolve to a full date with the month and day parts defaulted, and then the year part only will be extracted from that date to produce `year`.
3. A number of potential date formats will be attempted to find a match, in fixed predetermined order of preference. Once the tag data is confirmed to match one of these formats, no further matching will be attempted; if the matched format does not include a month or day value, that value is defaulted to `1`.
4. Unless otherwise specified, an identifier that is documented to read date-shaped data will attempt to match the following formats, in order of highest preference on top:

| Format                | Description  |
|-----------------------|--------------|
| `YYYY`                | Year only; month and day default to 1
| `YYYY-MM`             | Day defaults to 1
| `YYYY-MM-DD`          |
| `YYYY-MM-DDTHH`       |
| `YYYY-MM-DDTHH:MM`    |
| `YYYY-MM-DDTHH:MM:SS` |

Every field other than the year must be written as exactly two digits, and the `T` separating the date from the time is a literal character. These are the timestamp formats that Id3v2 defines as its subset of ISO 8601, and they are accepted everywhere date-shaped data is read, not only in Id3v2 tags. The order in which they are listed does not affect the outcome, since no string can match more than one of them. The ISO 8601 constructs that Id3v2 mentions but does not include in that subset, notably durations written with a slash, are simply not among the formats listed above.

If tag data exists for a date-shaped identifier and the data is neither pure whitespace nor matches any of the above formats, the identifier will resolve to a tainted null value.

#### Parsing track numbers

A common feature of several namespaces is an identifier named `track` that is of type number while the underlying tag data can be any arbitrary string. The rules for extracting a value out of such data are as follows. In every case the tag data is first trimmed of any leading and trailing whitespace; whitespace within the data is not affected.

- If the tag data does not exist, or nothing remains after trimming, `track` will resolve to an untainted null
- Otherwise, if the tag data is the ASCII representation of an unsigned integer, `track` will resolve to that integer (as numeric value)
- Otherwise, if the tag data is of the form "X/Y" (ASCII representations of two unsigned integers X and Y separated by a forward slash, with no whitespace inbetween), `track` will resolve to X (as a numeric value)
- Otherwise, `track` will resolve to a tainted null

Note that this only applies when `track` is typed as a number; raw namespaces do not define any identifiers of type number, so identifiers such as `vorbis::raw::tracknumber` which are of type string do not follow these parsing rules and can never resolve to a tainted null.

### Global namespace

Includes high-convenience accessors for structured metadata, such as `artist` to retrieve the artist name on a best-effort basis without having to specify where it comes from. This namespace is **closed**.

| Identifier  | Type       | Description  |
|-------------|------------|--------------|
| `artist`    | string     | A best-effort attempt on the track artist name (see below)
| `album`     | string     | A best-effort attempt on the album name (see below)
| `genre`     | string     | A best-effort attempt on the musical genre (see below)
| `title`     | string     | A best-effort attempt on the track title (see below)
| `year`      | number     | A best-effort attempt on the track release year (see below)

Depending on the tag data present in each file, and on which tag formats are consulted, any of these identifiers can resolve to a multivalue: a global identifier has whatever cardinality the format that resolved it produced. `artist` may therefore be a single value in one file and a multivalue in the next.

The meaning of "best-effort" is:
  - tag formats are ranked by order of preference, strongest to weakest: vorbis > ape > id3v2 > id3v1
  - for each tag, try to resolve it from the strongest format; if it resolves to null, try the next ranked format
  - less preferred formats will not be evaluated at all if a more preferred one produces a non-null value (a kind of short-circuiting)
  - if _any_ attempted format resolved to a tainted null and the final value of an identifier is also null, then that null value will also be tainted

All of this exists to serve one goal, which might be framed as "the obvious naive attempt should succeed". Somebody who wants the files by Metallica should be able to write `artist == "metallica"` and get them, without first having to learn which tag formats their collection happens to use, which of them this particular file carries, how each one spells things, or that in some tracks Metallica are not the only artists performing. Every rule above -- the ranking, the fall-through, the short-circuiting, the propagation of taint -- is machinery in service of that one sentence working.

On the other hand, convenience is only convenient while it is helping, and a facade that guesses well for most files will occasionally guess against what you actually want. When that happens, do not fight it. Disregard the global namespace entirely and address the tag you mean, through the format-specific namespaces or, if you want the bytes as they were recorded and nothing else, through their `raw` counterparts. Nothing in this section is applicable to the latter: they resolve exactly one thing from exactly one place, and they are the supported way to be precise.

### Namespace `ape`

Includes identifiers to access APE tag information in files that carry it. This namespace is **open**. Since a single APE item may hold any number of values, any identifier in this namespace can potentially resolve to a multivalue. Despite the name, identifiers in this namespace read an APE tag of either revision; the older one is described under the caveats below.

Unlike other tag formats, APE prescribes no fixed set of item keys and no naming scheme for them, and tools accordingly present whichever keys a file happens to contain rather than mapping them onto names of their own. Goro does the same: an identifier in this namespace names an item key directly, matched case-insensitively. The identifiers below are listed not because they are aliases for something else -- each names the key it reads -- but because they are the conventional keys for the data they hold, and because two of them are interpreted rather than passed through as text.

| Identifier      | Type       | Description  |
|-----------------|------------|--------------|
| `ape::artist`   | string     | Value(s) of the `Artist` item
| `ape::album`    | string     | Value(s) of the `Album` item
| `ape::comment`  | string     | Value(s) of the `Comment` item
| `ape::genre`    | string     | Value(s) of the `Genre` item
| `ape::title`    | string     | Value(s) of the `Title` item
| `ape::track`    | number     | Value(s) of the `Track` item; this value comes from [tracknumber-shaped](#parsing-track-numbers) data
| `ape::year`     | number     | Value(s) of the `Year` item; this value comes from [date-shaped](#parsing-dates) data, since an APE `Year` item commonly records a full date rather than only a year

Because this namespace is open, any identifier not listed above resolves to the value(s) of the item of the same name, as a string, or to an untainted null if the file carries no such item. An item key that the rules for an identifier cannot spell -- `Album Artist`, for instance, and a great many APE keys contain spaces -- is named by writing that part of the identifier as a quoted string, as in `ape::"album artist"`.

Of the identifiers above, only `ape::track` and `ape::year` are interpreted at all; every other one resolves to the item's values as recorded, exactly as any identifier in this namespace does. In particular no genre conventions are applied. APE carries none of the historical baggage that Id3v2's genre frame accumulated: it expresses several values directly rather than by delimiter, so there is nothing to second-guess, and the references to the Id3v1 genre table are a convention of Id3v2 alone -- a value of `17` in a `Genre` item is the genre named "17". A consequence is that for every identifier other than `track` and `year`, the `ape::raw` counterpart resolves to exactly the same thing.

An APE item need not hold text. An item flagged as an external locator resolves to that locator as a string, since a locator is text. An item flagged as binary -- a `Cover Art (Front)` item, typically -- has no string form and resolves to an untainted null.

Two quirks are worth knowing. APE forbids a tag from carrying two keys that differ only in case, but files that do so exist, and Goro sees only the last of them. And the earlier revision of the format specified its values as ISO-8859-1 rather than UTF-8; such files are uncommon, but a value in one that uses any character outside ASCII will not read correctly.

### Namespace `ape::raw`

Provides the same APE tag information as the `ape` namespace, but without interpreting it. This namespace is **open** and raw.

This namespace defines no well-known identifiers: every identifier names an item key and resolves to its value(s) as recorded. Because APE item keys and Goro's convenience identifiers happen to coincide, each identifier here is the uninterpreted counterpart of the one of the same name in `ape`. `ape::raw::track` yields a string such as `"3/12"`, and `ape::raw::year` yields the recorded date verbatim.

### Namespace `file`

Includes identifiers to access basic file properties. This namespace is **closed**.

| Identifier       | Type       | Description  |
|------------------|------------|--------------|
| `file::duration` | duration   | The playtime duration of the audio file, truncated to whole seconds
| `file::size`     | bytecount  | The size of the audio file on disk

### Namespace `id3v1`

Includes identifiers to access Id3v1 tag information. This namespace is **closed**. Identifiers within it can _never_ be multivalues because the Id3v1 tag structure does not allow it.

| Identifier       | Type       | Description  |
|------------------|------------|--------------|
| `id3v1::artist`  | string     | Track artist name
| `id3v1::album`   | string     | Album name
| `id3v1::comment` | string     | Comments
| `id3v1::genre`   | string     | Musical genre
| `id3v1::title`   | string     | Track title
| `id3v1::year`    | number     | Year

### Namespace `id3v1::raw`

Provides the same Id3v1 tag information as the `id3v1` namespace, but without interpreting it. This namespace is **closed** and raw. Identifiers within it can _never_ be multivalues because the Id3v1 tag structure does not allow it.

| Identifier            | Type       | Description  |
|-----------------------|------------|--------------|
| `id3v1::raw::artist`  | string     | Track artist name
| `id3v1::raw::album`   | string     | Album name
| `id3v1::raw::comment` | string     | Comments
| `id3v1::raw::genre`   | string     | Musical genre
| `id3v1::raw::title`   | string     | Track title
| `id3v1::raw::year`    | string     | Year; the type of this identifier is the only difference with the `id3v1` namespace

### Namespace `id3v2`

Includes identifiers to access Id3v2 tag information. This namespace is **open**.

| Identifier       | Type       | Description  |
|------------------|------------|--------------|
| `id3v2::artist`  | string     | Value(s) of the `TPE1` frame
| `id3v2::album`   | string     | Value(s) of the `TALB` frame
| `id3v2::comment` | string     | Text of the `COMM` frame(s), with any description dropped
| `id3v2::genre`   | string     | Genre(s) of the `TCON` frame; see [Genres](#genres) below
| `id3v2::title`   | string     | Value(s) of the `TIT2` frame
| `id3v2::track`   | number     | Value(s) of the `TRCK` frame; this value comes from [tracknumber-shaped](#parsing-track-numbers) data
| `id3v2::year`    | number     | Value(s) of the `TDRC` frame; this value comes from [date-shaped](#parsing-dates) data

Because this namespace is open, any identifier not listed above is taken to be a frame identifier and resolves to the content of the frames of that name. `id3v2::TIT3` reads the `TIT3` frames, and an identifier naming a frame the file does not carry resolves to an untainted null.

#### Frame names and tag versions

Id3v2 exists in three revisions that differ in how frames are named: v2.2 uses three-character identifiers, while v2.3 and v2.4 use four. **Goro presents a single unified view in which every frame is named by its v2.4 identifier, whatever the file actually contains.** A v2.2 `TT2` frame and a v2.3 `TIT2` frame are both read as `id3v2::TIT2`, and there is no need to know or ask which revision a file uses. The few v2.2 frames that have no v2.4 counterpart keep their original three-character name, so `id3v2::CRM` reads a v2.2 `CRM` frame.

One consequence is worth remembering: a frame must be referred to by its v2.4 name even when the file stores it under an older one. The year of a v2.3 file lives in a `TYER` frame on disk but is read as `id3v2::TDRC`; because the namespace is open, writing `id3v2::TYER` is not an error and simply resolves to null. Of course, this is only relevant if you care about specific frames; otherwise, `id3v2::year` completely sidesteps these issues.

#### Common behaviour

Everything in this section applies to every identifier in the namespace and behaves identically on every tag revision. Where a revision genuinely differs, it is called out under [Per-version caveats](#per-version-caveats) below; anything not mentioned there does not vary.

The starting point for every identifier is the text of the frame **as recorded in the file**. Goro performs all further interpretation itself rather than inheriting it, which is what allows the result to be independent of the revision the file happens to use.

A value becomes a multivalue for either of two reasons, which are not distinguished from one another. The first is that a frame occurs more than once in the tag: `COMM`, `TXXX` and `WXXX` are told apart by a description rather than by their name, so a tag may hold several of each. The second is that a single text frame holds several values, which from v2.4 onwards the format expresses directly.

Every value is trimmed of leading and trailing whitespace before it is used. This matters because several conventions in the wild put a space after a separator, and an untrimmed value would fail to compare equal to the obvious thing a predicate would be written against.

#### Genres

The `TCON` frame has accumulated more conventions than any other, and Goro understands all of them, on every revision. A `TCON` frame may hold free text, a number referring to the Id3v1 genre table, one or more such references written in parentheses, a reference followed by free text refining it, the reserved values for remixes and covers, or several genres run together with a separator. Resolution proceeds as follows:

1. The value is split on forward slashes and semicolons. This split is applied to genres only: it is safe here, where a value genuinely is a list, and it is not safe for identifiers such as `id3v2::artist`, where a slash is part of the name.
2. A parenthesized reference `(n)` is replaced by the name of Id3v1 genre `n`. Several references may appear in sequence, and each becomes a separate value. A doubled opening parenthesis is an escape for a literal one.
3. Text following a reference is a refinement, and becomes a value of its own in addition to the name of the reference. So `(17)Post-Rock` yields both `Rock` and `Post-Rock`, and matches a predicate written against either.
4. A value consisting only of digits is also a reference to the Id3v1 genre table, and is replaced by the corresponding name.
5. The reserved values `RX` and `CR`, with or without parentheses, become `Remix` and `Cover`.
6. A reference to a table position that has no genre assigned to it is left as recorded.

The table referred to throughout is the Id3v1 genre table together with its widely adopted extensions, 148 entries in total.

#### Per-version caveats

**Id3v2.2.** Frames are named by their v2.4 equivalents, as described above. A text frame never holds more than one value, so a multivalue can only arise from a repeated frame.

**Id3v2.3.** A text frame never holds more than one value. The year is recorded in a different frame from the day and month, and only the former is presented as `TDRC`, so a complete date cannot be recovered from a v2.3 file; `id3v2::year` is unaffected, since it needs the year alone. Two small fidelity deviations affect the `id3v2::raw` namespace only and are described there.

**Id3v2.4.** A single text frame may hold several values, and this is the only revision in which the format says so. The parenthesized genre conventions belong to the earlier revisions and are not part of this one, but files carrying them are common, since that is what a conversion from an earlier revision leaves behind, and Goro understands them here too.

### Namespace `id3v2::raw`

Provides the same Id3v2 tag information as the `id3v2` namespace, but without interpreting it. This namespace is **open** and raw.

This namespace defines no well-known identifiers. Every identifier in it is taken to be a frame name, exactly as in `id3v2`, and resolves to the text of the frames of that name as recorded in the file. `id3v2::raw::TRCK` is the uninterpreted counterpart of `id3v2::track` and yields a string such as `"3/12"`; `id3v2::raw::TCON` yields whatever the genre frame records, references and separators included.

Frames that carry a description alongside their value -- `TXXX`, `COMM` and `WXXX` -- resolve to a string holding both, written as the description in square brackets followed by the value, as in `[MOOD] calm`. A tag holding several such frames therefore resolves to a multivalue of one such string per frame. Addressing an individual one of them by its description is not currently possible; see the deferred design notes.

Two deviations from exact fidelity are known, both confined to Id3v2.3 and both arising below Goro rather than within it. A `TCON` frame whose recorded value separates genres with a forward slash is reported with semicolons in their place, and the parentheses around the reserved values `(RX)` and `(CR)` are not preserved. Neither deviation affects any identifier in the `id3v2` namespace, since those are resolved from the same text by rules that treat the two separators alike and accept the reserved values with or without parentheses.

### Namespace `vorbis`

Includes identifiers to access Vorbis comments data in files that carry it. This namespace is **open**. Since all Vorbis fields are allowed to appear an unlimited number of times, any identifier in this namespace can potentially result in a multivalue.

Well-known identifiers in this namespace are listed below. All of these are defined as resolving to the value(s) of Vorbis comments with a specific, **case-insensitive** field name:

| Identifier            | Type       | Description  |
|-----------------------|------------|--------------|
| `vorbis::artist`      | string     | Value(s) from the field "ARTIST"
| `vorbis::album`       | string     | Value(s) from the field "ALBUM"
| `vorbis::description` | string     | Value(s) from the field "DESCRIPTION"
| `vorbis::genre`       | string     | Value(s) from the field "GENRE"
| `vorbis::title`       | string     | Value(s) from the field "TITLE"
| `vorbis::track`       | number     | Value(s) from the field "TRACKNUMBER"; this value comes from [tracknumber-shaped](#parsing-track-numbers) data
| `vorbis::year`        | number     | Value(s) from the field "DATE"; this value comes from [date-shaped](#parsing-dates) data

Because this namespace is open, _any_ identifier within it not explicitly defined above can still resolve to a string value read from tags, or null if the tag holds no field whose name matches that of the identifier. For example, the identifier `vorbis::custom` would look for Vorbis comment fields with the name `"custom"` (matched case-insensitively) and would resolve to: an untainted null, if no such field exists in the tag; a string value, if exactly one such field exists in the tag; or a multivalue of strings, if more than one fields exist.

### Namespace `vorbis::raw`

Provides the same Vorbis comment data as the `vorbis` namespace, but without interpreting it. This namespace is **open** and raw. As with `vorbis`, any identifier in it can potentially resolve to a multivalue, since Vorbis fields may appear any number of times.

This namespace defines no well-known identifiers of its own. Every identifier in it resolves to the value(s) of the Vorbis comment field of the same name, matched case-insensitively, as a string. `vorbis::raw::artist` reads the field "ARTIST", `vorbis::raw::custom` reads the field "CUSTOM", and neither can ever resolve to a tainted null.

Because there are no well-known names here, an identifier in this namespace does not always have the same name as its interpreted counterpart in `vorbis`. Where the two differ it is because the interpreted identifier is named for what it means rather than for the field it reads:

| Interpreted identifier | Raw counterpart             | Vorbis field  |
|------------------------|-----------------------------|---------------|
| `vorbis::artist`       | `vorbis::raw::artist`       | "ARTIST"
| `vorbis::album`        | `vorbis::raw::album`        | "ALBUM"
| `vorbis::description`  | `vorbis::raw::description`  | "DESCRIPTION"
| `vorbis::genre`        | `vorbis::raw::genre`        | "GENRE"
| `vorbis::title`        | `vorbis::raw::title`        | "TITLE"
| `vorbis::track`        | `vorbis::raw::tracknumber`  | "TRACKNUMBER"
| `vorbis::year`         | `vorbis::raw::date`         | "DATE"

So `vorbis::raw::track` is not the uninterpreted form of `vorbis::track`; it reads a field literally named "TRACK", which is not a standard Vorbis field and will usually resolve to null.
