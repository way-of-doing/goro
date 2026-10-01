# Predefined identifiers

## DESCRIPTION

Goro predicate expressions can refer to a number of built-in identifiers. This document lists those identifiers and describes what each one represents. Each identifier is given together with its namespace, as `namespace::identifier`. Identifiers in the global namespace are given as simply `identifier`, since the global namespace is implied if no namespace is specified, but they can also be explicitly qualified as in `::identifier`.

Identifiers are shown below in their conventional casing, but predicate syntax is case-insensitive and they may be written in any case.

An identifier is **absent** when the file records nothing for it. When the data is there but cannot be interpreted as the type given in the tables below — for example a year field holding something that is not a year — the identifier resolves to an **unusable** occurrence instead, which behaves the same way in every respect but raises a warning when it is used. See [Predicates](../../concepts/predicates.md) for what absence and unusability mean in an expression, and [Absent, usable, and unusable data](#absent-usable-and-unusable-data) below for the rules that decide which of them a given piece of tag data produces.

### Definitions

#### Open, closed, and raw namespaces

Some namespaces such as `id3v1` are _closed_: the full set of valid identifiers in that namespace is statically known, and therefore any identifier can be accepted or rejected as invalid without any file-specific context. Other namespaces such as `vorbis` are _open_: there is an unbounded set of identifiers in them that can resolve to a value depending on the specific file, and the members of that set cannot be determined before looking at the file. Open namespaces typically include some standard documented identifiers, and also a set of identifiers that directly resolve to tag data and derive their names from that.

Goro will reject a predicate that refers to an unknown identifier in a closed namespace after parsing the predicate and before touching any files. On the other hand, an identifier within an open namespace will never be rejected as invalid; if the data it is intended to resolve to does not exist, the identifier is simply an absent value.

An identifier in an open namespace derives the field name it looks for from its own name, and several tag formats permit field names that the rules for an identifier cannot spell -- most commonly names containing spaces, such as the APE item key `Album Artist`. Such a field is reached by writing that part of the identifier as a quoted string, as in `ape::"album artist"`; see [Predicates](../../concepts/predicates.md) for the syntax. Every field name these formats permit can be written this way.

Orthogonally to this, some namespaces are "raw". This is not a technical property of namespaces, but rather a design convention. Namespaces with a nested `::raw` part are intended to provide unfettered access to tag data without interpreting it, and their typing follows from that: **a raw identifier has the type the recorded datum natively has**, so that reading it requires no interpretation and can never fail. Tag fields hold text, so in practice every raw identifier is of string type, with two exceptions -- the genre and the track of an Id3v1 tag, which are single numeric bytes rather than text and are accordingly typed as numbers. Nothing in a raw namespace is ever unusable on account of tag data failing to parse as something else. Naturally, these identifiers can, depending on context, still be absent and still resolve to multivalues; and where the underlying data is neither text nor a scalar of any kind, they are always unusable occurrences, which is the one remaining way a raw identifier can be.

The difference between the two is a matter of policy rather than of plumbing, and it is worth stating plainly because every namespace that reads tag data comes in both forms. An **interpreted** namespace makes innocent assumptions on your behalf, so that the obvious predicate works against the tags people actually write: it trims whitespace from the edges of a value, splits a genre frame that holds a list, expands a genre table reference into a name, and reads a date out of whatever shape the date was written in. Each of those is nearly always right and occasionally not. A **raw** namespace assumes nothing at all and hands back the datum as recorded. **If the convenience is a problem, go to `raw`.**

#### Absent, usable, and unusable data

[Predicates](../../concepts/predicates.md) describes every value as being either absent or a bag of occurrences, each of which is usable or unusable. Which of the three a given identifier produces for a given file turns on two questions that are worth keeping apart, because they are answered by different things: whether the file records anything for that identifier at all, and what the recorded datum then yields.

**Whether anything is recorded.** A format whose fields are optional -- Vorbis comments, APE items, Id3v2 frames -- records a field because a tagger wrote one, so a field that is there is a datum and a field that is not there is **absent**. A fixed-layout format, Id3v1 alone among those Goro reads, has every field present whether or not anything was ever put into one, so trailing NUL-or-space padding records nothing: a field that is padding all the way through is absent, in the interpreted and raw namespaces alike.

That last rule matters more than it looks. The four bytes of the Id3v1 year field exist in every Id3v1 tag ever written, so treating a blank one as recorded-but-unusable would warn about a substantial fraction of any real collection while telling its owner nothing they could act on.

**What the datum yields**, where it was recorded, is where the interpreted and raw namespaces part company, in the manner described above. Interpretation trims whitespace from the start and end of the recorded text before using it, leaving whitespace inside untouched, so `" AC / DC "` yields `AC / DC`. A raw identifier trims nothing, whitespace being part of what was recorded. Trimming earns its keep where a value has been split: a `TCON` frame holding `Rock; Metal` yields the genres `Rock` and `Metal` rather than `Rock` and `" Metal"`, the convention of putting a space after a separator being widespread enough that a predicate written against `"metal"` had better find it.

An identifier whose type is string then yields the text it has. One of any other type interprets that text, and when it cannot the result is an **unusable occurrence** -- a field holding `last tuesday` where a date was expected being the ordinary case.

**Nothing left to yield.** Once trimmed, a field may hold nothing at all, and the two kinds of identifier answer differently on purpose. For a string-typed identifier the value is **absent**: a file whose artist frame holds three spaces has no artist by any reading a person would recognise, and `artist IS ABSENT` finds it. For an identifier of any other type it is an **unusable occurrence**, a year frame that fails to say a year being a defect in the file rather than an absence of information, and worth a warning. The dividing line is whether interpretation was involved: a string needs none, so nothing there means nothing was recorded, while a number needed the text interpreted and the text supplied nothing to interpret.

**Data that is not text.** Some tag data is neither text nor a scalar of any kind, and so has no recorded form a value could hold: an APE item flagged as binary, and an Id3v2 attached picture. Such data resolves to an unusable occurrence, which is a different thing from being absent and can be told apart from it with a state test, so a predicate can ask whether a file has cover art. This is also the one way an identifier in a raw namespace can be unusable, since a raw namespace never interprets and so is never defeated by content that failed to parse.

#### Parsing dates

A common feature of several namespaces is an identifier named `year`. In many cases, this identifier is of type number while the underlying tag data can be any arbitrary string. The rules for extracting a value out of such _date-shaped_ data are as follows:

1. The tag data is trimmed, as [interpretation](#open-closed-and-raw-namespaces) trims everything. If nothing remains, there is no date to read and the identifier resolves to an unusable occurrence, a field that was written and says no date being a defect rather than an absence. A fixed-layout field that was only ever padding is absent instead, having recorded nothing to begin with.
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

If tag data exists for a date-shaped identifier and the data is neither pure whitespace nor matches any of the above formats, the identifier resolves to an unusable occurrence.

A matched format must also denote a real date. The Gregorian calendar has no year zero, so a year of `0000` does not resolve to the number zero but to an unusable occurrence. This case earns its own sentence because it is common rather than exotic: `0000` is what a great many taggers write into an Id3v1 year field they have nothing to put in, and it is written data rather than padding, so the blank-field rule above does not reach it. Resolving it to zero would make `id3v1::year == 0` true across a large part of a collection and `year BETWEEN 1900..2030` quietly false for the same files, with no warning and nothing at all to notice.

#### Parsing track numbers

A common feature of several namespaces is an identifier named `track` that is of type number while the underlying tag data can be any arbitrary string. The rules for extracting a value out of such data are as follows. In every case the tag data is first trimmed of any leading and trailing whitespace; whitespace within the data is not affected.

- If the tag data does not exist, `track` is absent; if it exists but nothing remains once [trimmed](#open-closed-and-raw-namespaces), it is an unusable occurrence, unless the field was fixed-layout padding that recorded nothing, in which case it is absent
- Otherwise, if the tag data is the ASCII decimal notation of an unsigned integer, `track` will resolve to that integer (as numeric value)
- Otherwise, if the tag data is of the form "X/Y" (ASCII decimal notation of two unsigned integers X and Y separated by a forward slash, with no whitespace inbetween), `track` will resolve to X (as a numeric value)
- Otherwise, `track` resolves to an unusable occurrence

Note that these rules are about extracting a number out of text, so they apply only where a number-typed identifier reads a text field. An identifier such as `vorbis::raw::tracknumber`, being of string type, hands the text back unparsed and is never unusable on that account. Nor do the rules apply to `id3v1::track` and `id3v1::raw::track`, which are number-typed but read a single byte and therefore have nothing to parse.

### Global namespace

Includes high-convenience accessors for structured metadata, such as `artist` to retrieve the artist name on a best-effort basis without having to specify where it comes from. This namespace is **closed**.

This namespace is interpreted: everything in it is subject to the assumptions described under
[open, closed, and raw namespaces](#open-closed-and-raw-namespaces), and the format-specific `raw` namespaces is where to go if one of them is in
your way.

| Identifier  | Type       | Description  |
|-------------|------------|--------------|
| `artist`    | string     | A best-effort attempt on the track artist name (see below)
| `album`     | string     | A best-effort attempt on the album name (see below)
| `genre`     | string     | A best-effort attempt on the musical genre (see below)
| `title`     | string     | A best-effort attempt on the track title (see below)
| `year`      | number     | A best-effort attempt on the track release year (see below)

Depending on the tag data present in each file, and on which tag formats are consulted, any of these identifiers can resolve to a multivalue: a global identifier has whatever cardinality was produced by the format that resolved it, meaning the one the rules below select. `artist` may therefore be a single value in one file and a multivalue in the next.

The meaning of "best-effort" is:
  - tag formats are ranked by order of preference, strongest to weakest: vorbis > ape > id3v2 > id3v1
  - the result is the value of the highest-ranked format that produced **at least one usable occurrence**, and that value is taken entire, unusable occurrences included
  - failing that, it is the value of the highest-ranked format that produced any non-absent result, which is necessarily (due to the previous rule) a value all of whose occurrences are unusable
  - failing that, every format was absent, and so is the result

Usability is what decides the fall-through, not mere presence: a format holding data that cannot be read does not stop the search, because the whole point of consulting several formats is to come back with something usable. A format that holds a usable year loses to none but a better-ranked format that also holds one.

Evaluation stops as soon as a format yields a usable occurrence, since nothing a less preferred format could produce would change the answer; until then every format in rank order is consulted. Worked through:

| vorbis                    | ape          | result |
|---------------------------|--------------|--------|
| absent                    | usable       | ape's value; the ordinary fall-through
| unusable                  | usable       | ape's value; a preferred format holding junk does not win over a usable one
| usable and unusable       | usable       | vorbis's value entire, the unusable occurrence included, since the preferred format did produce something usable
| unusable                  | absent       | vorbis's value, all of it unusable; a defect is reported rather than passed off as absence
| absent                    | absent       | absent

One consequence is worth stating plainly, because it is the price of the convenience. Where a preferred format holds unreadable data and a lesser one holds a usable value, the defect is discarded silently: the occurrences that could not be read are not part of the value returned, so nothing warns about them. This is deliberate -- a facade that reported every defect it routed around would warn constantly on precisely the collections it exists to make bearable -- and it is why the format-specific namespaces are the supported way to be precise. A predicate such as `ANY(vorbis::year) IS UNUSABLE` finds exactly the files whose defect the facade papered over.

All of this exists to serve one goal, which might be framed as "the obvious naive attempt should succeed". Somebody who wants the files by Metallica should be able to write `artist == "metallica"` and get them, without first having to learn which tag formats their collection happens to use, which of them this particular file carries, how each one spells things, or that in some tracks Metallica are not the only artists performing. Every rule above -- the ranking, the fall-through, the short-circuiting, the preference for a usable occurrence over a merely present one -- is machinery in service of that one sentence working.

On the other hand, convenience is only convenient while it is helping, and a facade that guesses well for most files will occasionally guess against what you actually want. When that happens, do not fight it. Disregard the global namespace entirely and address the tag you mean, through the format-specific namespaces or, if you want the bytes as they were recorded and nothing else, through their `raw` counterparts. Nothing in this section is applicable to the latter: they resolve exactly one thing from exactly one place, and they are the supported way to be precise.

### Namespace `ape`

Includes identifiers to access APE tag information in files that carry it. This namespace is **open**. Since a single APE item may hold any number of values, any identifier in this namespace can potentially resolve to a multivalue. Despite the name, identifiers in this namespace read an APE tag of either revision; the older one is described under the caveats below.

This namespace is interpreted: everything in it is subject to the assumptions described under
[open, closed, and raw namespaces](#open-closed-and-raw-namespaces), and `ape::raw` is where to go if one of them is in
your way.

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

Because this namespace is open, any identifier not listed above resolves to the value(s) of the item of the same name, as a string, or to an absent value if the file carries no such item. An item key that the rules for an identifier cannot spell -- `Album Artist`, for instance, and a great many APE keys contain spaces -- is named by writing that part of the identifier as a quoted string, as in `ape::"album artist"`.

No genre conventions are applied here. Those belong to Id3v2, whose genre frame accumulated them; APE expresses several values directly rather than by delimiter, so there is nothing to second-guess, and a value of `17` in a `Genre` item is the genre named "17" rather than a reference to the Id3v1 table.

An APE item need not hold text. An item flagged as an external locator resolves to that locator as a string, since a locator is text. An item flagged as binary -- a `Cover Art (Front)` item, typically -- has no string form and resolves to an unusable occurrence, which is how a predicate can tell such an item apart from one that is not in the file at all.

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

This namespace is interpreted: everything in it is subject to the assumptions described under
[open, closed, and raw namespaces](#open-closed-and-raw-namespaces), and `id3v1::raw` is where to go if one of them is in
your way.

| Identifier       | Type       | Description  |
|------------------|------------|--------------|
| `id3v1::artist`  | string     | Track artist name
| `id3v1::album`   | string     | Album name
| `id3v1::comment` | string     | Comments
| `id3v1::genre`   | string     | Musical genre; see [Genre](#genre) below
| `id3v1::title`   | string     | Track title
| `id3v1::track`   | number     | Track number, recorded as a single byte; Id3v1.1 only, see [A fixed structure](#a-fixed-structure) below
| `id3v1::year`    | number     | Year; this value comes from [date-shaped](#parsing-dates) data

#### A fixed structure

Id3v1 differs from every other format Goro reads in a way that shows through in what its identifiers resolve to. An Id3v1 tag is a block of 128 bytes with a fixed layout, not a collection of fields a tagger chooses to write. Every field is therefore present in every tag, conventionally padded with NUL bytes or spaces where nothing was put into it, and the presence of a field says nothing whatever about whether anything was recorded in it. A field padded that way is consequently **absent** rather than unusable, by the rule for [a blank field](#absent-usable-and-unusable-data); this is the one format where that reading applies.

Two further consequences of the fixed layout are worth knowing.

The track number is not part of Id3v1 as originally specified. Id3v1.1 takes the last two bytes of the comment field and uses them for a NUL byte followed by a single track byte, which is also how a reader tells the two revisions apart. So `id3v1::comment` reads 30 bytes on a tag with no track number and 28 bytes on one that has it, and `id3v1::track` is absent on a tag of the original revision. A track byte of zero is the convention for "not recorded" and is absent as well, zero not being a track number that exists.

Every text field has a fixed width of 30 bytes, and a tagger writing a longer value has no option but to truncate it. `id3v1::artist` can therefore fail to compare equal to an artist name that the same file records in full elsewhere, which is one more reason to prefer the global namespace unless an Id3v1 tag is specifically what is wanted.

#### Genre

The Id3v1 genre is a single byte holding an index into the genre table -- the same 148-entry table, original entries together with the widely adopted extensions, that the Id3v2 genre conventions refer to. It resolves as follows:

- the value 255 is the convention for "no genre recorded", and is absent
- a value that indexes an entry the table defines resolves to the name of that genre, as a string
- a value that indexes no defined entry is an unusable occurrence: the byte is there and it names no genre

The byte itself is available as `id3v1::raw::genre`, which is number-typed and reports the index whether or not the table defines an entry for it.

One quirk deserves stating plainly, because it is not Goro's to fix and it affects a great many files. Index 0 is a genuine entry, and its genre is "Blues". A tagger that fills a tag with zero bytes rather than leaving the genre byte at 255 therefore produces a file that claims to be Blues, indistinguishable by its genre byte alone from a file whose owner meant it. Nothing in the byte can tell the two apart, so Goro does not try: `id3v1::genre` resolves to "Blues" for both. What does distinguish them is that the rest of such a tag is blank as well, which a predicate can say directly -- `id3v1::genre == "blues" AND id3v1::artist IS ABSENT AND id3v1::title IS ABSENT` finds the zero-filled tags without catching the deliberate ones.

#### Encoding

Id3v1 specifies its text as ISO-8859-1, and Goro reads it as such. Taggers in the wild frequently wrote whatever their local codepage happened to be instead, so a value holding any character outside ASCII may not read correctly, in the same way and for the same reason as the older revision of an APE tag. Such a value is still text, so it is a usable occurrence rather than an unusable one; there is no state for data that decoded without complaint and came out wrong, and inventing one would not help, since Goro has no way to know.

### Namespace `id3v1::raw`

Provides the same Id3v1 tag information as the `id3v1` namespace, but without interpreting it. This namespace is **closed** and raw. Identifiers within it can _never_ be multivalues because the Id3v1 tag structure does not allow it.

| Identifier            | Type       | Description  |
|-----------------------|------------|--------------|
| `id3v1::raw::artist`  | string     | Track artist name
| `id3v1::raw::album`   | string     | Album name
| `id3v1::raw::comment` | string     | Comments
| `id3v1::raw::genre`   | number     | Genre table index as recorded, such as `17`; see below
| `id3v1::raw::title`   | string     | Track title
| `id3v1::raw::track`   | number     | Track number as recorded; see below
| `id3v1::raw::year`    | string     | Year as recorded, such as `"1991"` or `"0000"`

`id3v1::raw::year` hands back the recorded text rather than a number, so a year field holding `0000` yields the string `"0000"` here while `id3v1::year` is unusable, and the raw identifier is never unusable on account of failing to parse.

The genre and the track of an Id3v1 tag are not text but single numeric bytes, and the type each one has here is the type it has on disk. `id3v1::raw::genre` is the byte itself: `17` where `id3v1::genre` is "Rock", `200` where `id3v1::genre` is unusable because the table defines no such entry, and absent at 255. `id3v1::raw::track` is likewise the track byte, absent at zero, and resolves to exactly what `id3v1::track` does -- there being nothing to interpret, the interpreted and raw readings coincide, as they already do for most of the `ape` namespace.

Typing these two as numbers rather than as text is arbitrary only in the sense that the format is: a raw identifier reports the datum as recorded, and what is recorded here is a number. It needs no further rule and it earns two things. A junk index can be named exactly, so `id3v1::raw::genre BETWEEN 148..254` finds the files using an index the table never defined and `id3v1::raw::genre == 0` finds those whose genre byte was never set away from the first table entry. And a predicate that asks this namespace for a genre *name* -- `id3v1::raw::genre == "rock"` -- is a type mismatch, so it is rejected before a single file is opened, instead of warning once per file across an entire collection.

### Namespace `id3v2`

Includes identifiers to access Id3v2 tag information. This namespace is **open**.

This namespace is interpreted: everything in it is subject to the assumptions described under
[open, closed, and raw namespaces](#open-closed-and-raw-namespaces), and `id3v2::raw` is where to go if one of them is in
your way.

| Identifier       | Type       | Description  |
|------------------|------------|--------------|
| `id3v2::artist`  | string     | Value(s) of the `TPE1` frame
| `id3v2::album`   | string     | Value(s) of the `TALB` frame
| `id3v2::comment` | string     | Text of the `COMM` frame(s), with any description dropped
| `id3v2::genre`   | string     | Genre(s) of the `TCON` frame; see [Genres](#genres) below
| `id3v2::title`   | string     | Value(s) of the `TIT2` frame
| `id3v2::track`   | number     | Value(s) of the `TRCK` frame; this value comes from [tracknumber-shaped](#parsing-track-numbers) data
| `id3v2::year`    | number     | Value(s) of the `TDRC` frame; this value comes from [date-shaped](#parsing-dates) data

Because this namespace is open, any identifier not listed above is taken to be a frame identifier and resolves to the content of the frames of that name. `id3v2::TIT3` reads the `TIT3` frames, and an identifier naming a frame the file does not carry is absent.

#### Frame names and tag versions

Id3v2 exists in three revisions that differ in how frames are named: v2.2 uses three-character identifiers, while v2.3 and v2.4 use four. **Goro presents a single unified view in which every frame is named by its v2.4 identifier, whatever the file actually contains.** A v2.2 `TT2` frame and a v2.3 `TIT2` frame are both read as `id3v2::TIT2`, and there is no need to know or ask which revision a file uses. The few v2.2 frames that have no v2.4 counterpart keep their original three-character name, so `id3v2::CRM` reads a v2.2 `CRM` frame.

One consequence is worth remembering: a frame must be referred to by its v2.4 name even when the file stores it under an older one. The year of a v2.3 file lives in a `TYER` frame on disk but is read as `id3v2::TDRC`; because the namespace is open, writing `id3v2::TYER` is not an error and is simply absent. Of course, this is only relevant if you care about specific frames; otherwise, `id3v2::year` completely sidesteps these issues.

#### Common behaviour

Everything in this section applies to every identifier in the namespace and behaves identically on every tag revision. Where a revision genuinely differs, it is called out under [Per-version caveats](#per-version-caveats) below; anything not mentioned there does not vary.

The starting point for every identifier is the text of the frame **as recorded in the file**. Goro performs all further interpretation itself rather than inheriting it, which is what allows the result to be independent of the revision the file happens to use.

A value becomes a multivalue for either of two reasons, which are not distinguished from one another. The first is that a frame occurs more than once in the tag: `COMM`, `TXXX` and `WXXX` are told apart by a description rather than by their name, so a tag may hold several of each. The second is that a single text frame holds several values, which from v2.4 onwards the format expresses directly.

Trimming and the other assumptions an interpreted namespace makes are described under [open, closed, and raw namespaces](#open-closed-and-raw-namespaces) and under [absent, usable, and unusable data](#absent-usable-and-unusable-data), and apply here as they do everywhere else. They matter most for the genre frame, where a value genuinely is a list and the convention of putting a space after the separator is widespread.

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

This namespace is interpreted: everything in it is subject to the assumptions described under
[open, closed, and raw namespaces](#open-closed-and-raw-namespaces), and `vorbis::raw` is where to go if one of them is in
your way.

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

Because this namespace is open, _any_ identifier within it not explicitly defined above can still resolve to a string value read from tags, or be absent if the tag holds no field whose name matches that of the identifier. For example, the identifier `vorbis::custom` would look for Vorbis comment fields with the name `"custom"` (matched case-insensitively) and would resolve to: an absent value, if no such field exists in the tag; a string value, if exactly one such field exists in the tag; or a multivalue of strings, if more than one field exists.

### Namespace `vorbis::raw`

Provides the same Vorbis comment data as the `vorbis` namespace, but without interpreting it. This namespace is **open** and raw. As with `vorbis`, any identifier in it can potentially resolve to a multivalue, since Vorbis fields may appear any number of times.

This namespace defines no well-known identifiers of its own. Every identifier in it resolves to the value(s) of the Vorbis comment field of the same name, matched case-insensitively, as a string. `vorbis::raw::artist` reads the field "ARTIST", `vorbis::raw::custom` reads the field "CUSTOM", and neither is ever unusable, Vorbis comment values being text by definition.

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

So `vorbis::raw::track` is not the uninterpreted form of `vorbis::track`; it reads a field literally named "TRACK", which is not a standard Vorbis field and will usually be absent.
