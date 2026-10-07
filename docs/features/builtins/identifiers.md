# Built-in identifiers

## DESCRIPTION

A predicate reads a file through _sources_, _concepts_ and _source functions_. This document lists them and describes what each one represents.

Names are shown below in their conventional casing, but predicate syntax is case-insensitive and they may be written in any case.

A concept or a source function is **absent** when the file records nothing for it. When the data is there but cannot be interpreted as the type given below -- for example a year field holding something that is not a year -- the result is an **unusable** occurrence instead, which leaves any comparison that depends on it without an answer and raises a warning when it is used. See [Predicates](../../concepts/predicates.md) for what absence and unusability mean in an expression, and [Absent, usable, and unusable data](#absent-usable-and-unusable-data) below for the rules that decide which of them a given piece of tag data produces.

Every concept and source function that reads tag data can be absent, since a file need not carry any given tag. Whether one can also hold more than one occurrence is said with its source, and the `file` source says both for each of its concepts. Together these decide which expressions are [exactly one](../../concepts/predicates.md#exactly-one).

### Definitions

#### Sources, concepts and source functions

The **sources** are `file`, which describes the file itself, and the four tag formats Goro reads: `id3v1`, `id3v2`, `ape` and `vorbis`. A source is written before what it qualifies, separated from it by `::`, and is one level deep: nothing nests under a source.

A **concept** is one of Goro's names for something a file can record, such as `artist` or `year`. A concept has one type, and for each source that can supply it, a _cell_ saying which of that source's data it reads and how the data is interpreted. Qualified by a source, as in `id3v2::year`, a concept reads that source alone, and it is an error to qualify a concept by a source that has no cell for it. Written without a source, as in `year`, a tag concept reads the tag sources in order of preference; see [Concepts](#concepts). The concepts of the `file` source belong to it alone and are always written with it, as in `file::size`. Goro knows every concept there is, so naming one that does not exist is an error, which suggests the concept probably meant.

A **source function** reads a source's own data without interpreting it, by the name the source itself gives that data: `vorbis::field("MOOD")`, `ape::field("Album Artist")`, `id3v2::field("TXXX", "MOOD")`. Its arguments are string literals, and each source decides how many it takes and how a name is matched. A source function is always written with its source, since every format names its data in its own way, and writing one without a source is an error. There are two:

- `field(...)` is a string: the data as text, exactly as recorded, with one occurrence for each value the format records;
- `bytes(...)` is a blob: the data as recorded, with one occurrence for each item, frame or comment, never split.

`id3v2`, `ape` and `vorbis` have both. `id3v1` has fields in fixed places rather than named ones, and `file` records nothing, so neither has source functions.

Concepts interpret, and source functions do not. `id3v2::track` is the number a `TRCK` frame denotes, while `id3v2::field("TRCK")` is the text the frame holds, such as `"3/12"`. A concept makes innocent assumptions on your behalf: it trims whitespace from the edges of a value, splits a genre frame that holds a list, expands a genre table reference into a name, and reads a date out of whatever shape the date was written in. Each of those is nearly always right and occasionally not. **If the convenience is a problem, go to a source function.**

#### Absent, usable, and unusable data

[Predicates](../../concepts/predicates.md) describes every value as being either absent or a bag of occurrences, each of which is usable or unusable. Which of the three a given concept or source function produces for a given file turns on two questions that are worth keeping apart, because they are answered by different things: whether the file records anything for it at all, and what the recorded datum then yields.

**Whether anything is recorded.** A format whose fields are optional -- Vorbis comments, APE items, Id3v2 frames -- records a field because a tagger wrote one, so a field that is there is a datum and a field that is not there is **absent**. A fixed-layout format, Id3v1 alone among those Goro reads, has every field present whether or not anything was ever put into one, so trailing NUL-or-space padding records nothing: a field that is padding all the way through is absent.

**What the datum yields**, where it was recorded, is where concepts and source functions part company. A concept trims whitespace from the start and end of the recorded text before using it, leaving whitespace inside untouched, so `" AC / DC "` yields `AC / DC`. Where a concept splits a value, each part is trimmed, so a `TCON` frame holding `Rock; Metal` yields the genres `Rock` and `Metal`. A source function trims and splits nothing beyond what the format itself defines. The padding of a fixed-layout field is not part of what was recorded, and is never part of a value.

A concept whose type is string then yields the text it has. One of any other type interprets that text, and when it cannot the result is an **unusable occurrence** -- a field holding `last tuesday` where a date was expected being the ordinary case.

**Nothing left to yield.** Once trimmed, a field may hold nothing at all. For a string-typed concept the value is then **absent**, so `artist IS ABSENT` finds a file whose artist frame holds three spaces. For a concept of any other type it is an **unusable occurrence**. `field()` yields whatever was recorded, so it yields that frame's three spaces.

**Data that is not text.** Some tag data is not text: an APE item flagged as binary, and every Id3v2 frame other than a text, URL, comment or lyrics frame. `bytes()` reads it, as a blob. `field()` does not: `id3v2::field()` naming a frame that does not hold text is an error, since the frame identifier says so, while an APE item is known to be binary only once the file is read, so `ape::field()` resolves one to an unusable occurrence.

#### Parsing dates

The concept `year` is a number, while the underlying tag data can be any arbitrary string. The rules for extracting a value out of such _date-shaped_ data are as follows:

1. The tag data is trimmed, as a concept trims everything. If nothing remains, the concept resolves to an unusable occurrence. A fixed-layout field that was only ever padding is absent instead.
2. All data extraction from what is logically (bar potential low data precision) an instant in time first conceptually resolves _a full Gregorian calendar date_ in all cases, and then extracts only the relevant parts from that resolved date. For example, `id3v1::year` will ultimately only resolve to a simple integer year value, but conceptually, it will first resolve to a full date with the month and day parts defaulted, and then the year part only will be extracted from that date to produce `year`.
3. A number of potential date formats will be attempted to find a match, in fixed predetermined order of preference. Once the tag data is confirmed to match one of these formats, no further matching will be attempted; if the matched format does not include a month or day value, that value is defaulted to `1`.
4. Unless otherwise specified, a cell that is documented to read date-shaped data will attempt to match the following formats, in order of highest preference on top:

| Format                | Description  |
|-----------------------|--------------|
| `YYYY`                | Year only; month and day default to 1
| `YYYY-MM`             | Day defaults to 1
| `YYYY-MM-DD`          |
| `YYYY-MM-DDTHH`       |
| `YYYY-MM-DDTHH:MM`    |
| `YYYY-MM-DDTHH:MM:SS` |

Every field other than the year must be written as exactly two digits, and the `T` separating the date from the time is a literal character. These are the timestamp formats that Id3v2 defines as its subset of ISO 8601, and they are accepted everywhere date-shaped data is read, not only in Id3v2 tags. The order in which they are listed does not affect the outcome, since no string can match more than one of them. The ISO 8601 constructs that Id3v2 mentions but does not include in that subset, notably durations written with a slash, are simply not among the formats listed above.

If tag data exists for a date-shaped cell and the data is neither pure whitespace nor matches any of the above formats, the concept resolves to an unusable occurrence.

A matched format must also denote a real date. The Gregorian calendar has no year zero, so a year of `0000` resolves to an unusable occurrence rather than to the number zero. `0000` is written data rather than padding, so the blank-field rule above does not apply to it, even in an Id3v1 tag.

#### Parsing track numbers

The concept `track` is a number, while the underlying tag data can be any arbitrary string. The rules for extracting a value out of such data are as follows. In every case the tag data is first trimmed of any leading and trailing whitespace; whitespace within the data is not affected.

- If the tag data does not exist, `track` is absent; if it exists but nothing remains once trimmed, it is an unusable occurrence
- Otherwise, if the tag data is the ASCII decimal notation of an unsigned integer, `track` will resolve to that integer (as numeric value)
- Otherwise, if the tag data is of the form "X/Y" (ASCII decimal notation of two unsigned integers X and Y separated by a forward slash, with no whitespace inbetween), `track` will resolve to X (as a numeric value)
- Otherwise, `track` resolves to an unusable occurrence

Note that these rules are about extracting a number out of text, so they apply only where `track` reads a text field. `vorbis::field("TRACKNUMBER")`, being a string, hands the text back unparsed and is never unusable on that account. Nor do the rules apply to `id3v1::track`, which reads a single byte and therefore has nothing to parse.

### Concepts

Each concept has a cell for every tag source, giving the field it reads:

| Concept   | Type   | `vorbis`      | `ape`    | `id3v2` | `id3v1` |
|-----------|--------|---------------|----------|---------|---------|
| `artist`  | string | `ARTIST`      | `Artist` | `TPE1`  | artist
| `album`   | string | `ALBUM`       | `Album`  | `TALB`  | album
| `genre`   | string | `GENRE`       | `Genre`  | `TCON`  | genre
| `title`   | string | `TITLE`       | `Title`  | `TIT2`  | title
| `track`   | number | `TRACKNUMBER` | `Track`  | `TRCK`  | track
| `year`    | number | `DATE`        | `Year`   | `TDRC`  | year

`track` reads [tracknumber-shaped](#parsing-track-numbers) data and `year` [date-shaped](#parsing-dates) data, except where the section on a source says otherwise. How each source resolves a genre is described with the source.

Written without a source, each concept has the value of a [`PREFERRED()`](../../concepts/predicates.md#functions) of its cells, in the order of the columns above: Vorbis comments, then APE, then Id3v2, then Id3v1. So `artist` is `PREFERRED(vorbis::artist, ape::artist, id3v2::artist, id3v1::artist)`.

So a concept written without a source takes its value, entire, from the most preferred source holding a usable occurrence, and the sources after that one are not consulted. A source holding only data that cannot be read does not stop the search, and what it held is passed over silently. Such a concept has whatever cardinality the chosen source produced, so `artist` may be a single value in one file and a multivalue in the next, and it can be absent, as every source can.

Writing the expansion out differs in one respect only, which is where a warning points. An unusable occurrence of `year` has `year` as its source, so `year == 1991` warns about `year` rather than about the source whose data could not be read.

None of this touches a concept qualified by a source, which resolves exactly one thing from exactly one place.

### Source `ape`

Reads the APE tag of a file that carries one. Since a single APE item may hold any number of values, every concept and every `field()` of this source can resolve to a multivalue. Despite the name, this source reads an APE tag of either revision; the older one is described under the caveats below.

Unlike other tag formats, APE prescribes no fixed set of item keys and no naming scheme for them, and tools accordingly present whichever keys a file happens to contain rather than mapping them onto names of their own. The keys the concepts read are the conventional ones for the data they hold. `ape::year` reads [date-shaped](#parsing-dates) data, since an APE `Year` item commonly records a full date rather than only a year. No genre conventions are applied: a value of `17` in a `Genre` item is the genre named "17", not a reference to the Id3v1 table.

| Source function   | Type   | Description  |
|-------------------|--------|--------------|
| `ape::field(key)` | string | The values of the item named `key`
| `ape::bytes(key)` | blob   | The value of the item named `key`, as recorded

A key is matched without regard to case. An APE item need not hold text. An item flagged as an external locator holds text, the locator, so `field()` reads it as it reads any other. An item flagged as binary -- a `Cover Art (Front)` item, typically -- has no text to give, and `field()` resolves it to an unusable occurrence. `bytes()` reads an item of any kind, and gives its whole value as a single occurrence, so `ape::bytes(key)` never holds more than one occurrence.

Two quirks are worth knowing. APE forbids a tag from carrying two keys that differ only in case, but files that do so exist, and Goro sees only the last of them. And the earlier revision of the format specified its values as ISO-8859-1 rather than UTF-8; such files are uncommon, but a value in one that uses any character outside ASCII will not read correctly.

### Source `file`

Describes the file itself.

| Concept           | Type       | Description  |
|-------------------|------------|--------------|
| `file::duration`  | duration   | The playtime duration of the audio file, truncated to whole seconds
| `file::extension` | string     | The part of the file name after its last dot, without the dot, such as `mp3`
| `file::name`      | string     | The file name, extension included, such as `01 Intro.mp3`
| `file::path`      | string     | The absolute path of the file
| `file::size`      | bytecount  | The size of the file on disk

A path is the one the file was discovered under: symbolic links are not resolved. It is written with `/` as the separator on every platform, so that a file Windows calls `C:\Music\01 Intro.mp3` has the `file::path` `C:/Music/01 Intro.mp3`. `file::extension` is absent when the name has no dot, when its only dot is the first character, or when nothing follows its last dot; `file::path` and `file::name` are never absent. None of the three is ever unusable, and none needs anything read from the file. `file::size`, by contrast, describes the file itself rather than how it was reached, so for a file reached through a symbolic link it is the size of the file the link leads to.

`file::path`, `file::name`, `file::size` and `file::duration` are
[exactly one](../../concepts/predicates.md#exactly-one): each resolves to exactly one usable
occurrence for every file a predicate is evaluated against, since a file whose size or audio
properties cannot be had is a [file that cannot be read](../../concepts/warnings.md), and is not
evaluated at all. `file::extension` can be absent, and never holds more than one occurrence.

### Source `id3v1`

Reads the Id3v1 tag of a file that carries one. Its concepts _never_ hold more than one occurrence, because the Id3v1 tag structure does not allow it. Id3v1 has no source functions.

`id3v1::genre` is described under [Genre](#genre) below, and `id3v1::track` under [A fixed structure](#a-fixed-structure): it is recorded as a single byte, and only by Id3v1.1.

#### A fixed structure

Id3v1 differs from every other format Goro reads in a way that shows through in what its concepts resolve to. An Id3v1 tag is a block of 128 bytes with a fixed layout, not a collection of fields a tagger chooses to write. Every field is therefore present in every tag, conventionally padded with NUL bytes or spaces where nothing was put into it, and the presence of a field says nothing whatever about whether anything was recorded in it. A field padded that way is consequently **absent** rather than unusable, by the rule for [a blank field](#absent-usable-and-unusable-data); this is the one format where that reading applies.

Two further consequences of the fixed layout are worth knowing.

The track number is not part of Id3v1 as originally specified. Id3v1.1 takes the last two bytes of the comment field and uses them for a NUL byte followed by a single track byte, which is also how a reader tells the two revisions apart. So `id3v1::track` is absent on a tag of the original revision. A track byte of zero is the convention for "not recorded" and is absent as well, zero not being a track number that exists.

Every text field has a fixed width of 30 bytes, and a tagger writing a longer value has no option but to truncate it. `id3v1::artist` can therefore fail to compare equal to an artist name that the same file records in full elsewhere.

#### Genre

The Id3v1 genre is a single byte holding an index into the genre table -- the same 148-entry table, original entries together with the widely adopted extensions, that the Id3v2 genre conventions refer to. It resolves as follows:

- the value 255 is the convention for "no genre recorded", and is absent
- a value that indexes an entry the table defines resolves to the name of that genre, as a string
- a value that indexes no defined entry is an unusable occurrence: the byte is there and it names no genre

Index 0 is a genuine entry, and its genre is "Blues". A tagger that fills a tag with zero bytes rather than leaving the genre byte at 255 therefore produces a file that claims to be Blues, indistinguishable by its genre byte alone from a file whose owner meant it. Goro does not try to tell them apart: `id3v1::genre` resolves to "Blues" for both. What does distinguish them is that the rest of such a tag is blank as well.

#### Encoding

Id3v1 specifies its text as ISO-8859-1, and Goro reads it as such. Taggers in the wild frequently wrote whatever their local codepage happened to be instead, so a value holding any character outside ASCII may not read correctly, in the same way as the older revision of an APE tag. Such a value is still text, so it is a usable occurrence rather than an unusable one.

### Source `id3v2`

Reads the Id3v2 tag of a file that carries one. Every concept and every source function of this source can resolve to a multivalue, for the reasons given under [Common behaviour](#common-behaviour). `id3v2::genre` is described under [Genres](#genres) below.

| Source function                          | Type   | Description  |
|------------------------------------------|--------|--------------|
| `id3v2::field(frame)`                    | string | The text of every frame named `frame`
| `id3v2::field(frame, description)`       | string | The text of every frame named `frame` whose description is `description`
| `id3v2::bytes(frame)`                    | blob   | The content of every frame named `frame`, as recorded

A frame is named by its identifier, four letters or digits such as `TIT2`, matched without regard to case; see [Frame names and tag versions](#frame-names-and-tag-versions) for which identifier that is.

`field()` reads only frames that hold text: those whose identifier begins with `T` or `W`, and `COMM` and `USLT`, and naming any other frame is an error. Four of these, `TXXX`, `WXXX`, `COMM` and `USLT`, carry a description alongside their value, and are told apart by it rather than by their identifier. `field()` gives the value alone. Without a description it reads every such frame, and with one it reads only the frames whose description matches, compared without regard to case: `id3v2::field("TXXX", "MOOD")` reads the user-defined text frames described as `MOOD`, and `id3v2::field("COMM", "")` the comments with no description. Giving a description for any other frame is an error. The language a `COMM` or `USLT` frame records is not part of what `field()` reads.

`bytes()` reads a frame of any kind, and gives one occurrence for each frame, holding its content without the frame header.

#### Frame names and tag versions

Id3v2 exists in three revisions that differ in how frames are named: v2.2 uses three-character identifiers, while v2.3 and v2.4 use four. **Goro names every frame by its v2.4 identifier, wherever v2.4 has a frame holding the same data in the same form.** A v2.2 `TT2` frame and a v2.3 `TIT2` frame are both read as `TIT2`, and there is no need to know or ask which revision a file uses. The name depends on the frame alone and never on the revision the tag declares, so a v2.3 `TYER` frame, which records a year and is therefore a valid `TDRC`, is read as `TDRC` in whatever tag it is found.

A frame that has no counterpart of the same form keeps its own identifier: `TDAT` and `TIME`, which hold the day and time of a v2.3 date; `RVAD` and `EQUA`, whose v2.4 successors record their data differently; and v2.2 frames such as `PIC`, whose picture format is written differently from that of `APIC`, and `CRM`, which v2.4 lacks.

Naming a frame by an identifier that Goro reads under another is an error that offers the one to write: `id3v2::field("TYER")` offers `id3v2::field("TDRC")`.

#### Common behaviour

Everything in this section applies to every concept and source function of the source and behaves identically on every tag revision. Where a revision genuinely differs, it is called out under [Per-version caveats](#per-version-caveats) below; anything not mentioned there does not vary.

The starting point for every concept is the text of the frame **as recorded in the file**. Goro performs all further interpretation itself rather than inheriting it, which is what allows the result to be independent of the revision the file happens to use.

A value becomes a multivalue for either of two reasons, which are not distinguished from one another. The first is that a frame occurs more than once in the tag: `COMM`, `TXXX` and `WXXX` are told apart by a description rather than by their identifier, so a tag may hold several of each. The second is that a single text frame holds several values, which from v2.4 onwards the format expresses directly.

#### Genres

The `TCON` frame has accumulated more conventions than any other, and `id3v2::genre` understands all of them, on every revision. A `TCON` frame may hold free text, a number referring to the Id3v1 genre table, one or more such references written in parentheses, a reference followed by free text refining it, the reserved values for remixes and covers, or several genres run together with a separator. Resolution proceeds as follows:

1. The value is split on forward slashes and semicolons. This applies to genres only: in other frames, such as the one behind `id3v2::artist`, a slash is part of the value.
2. A parenthesized reference `(n)` is replaced by the name of Id3v1 genre `n`. Several references may appear in sequence, and each becomes a separate value. A doubled opening parenthesis is an escape for a literal one.
3. Text following a reference is a refinement, and becomes a value of its own in addition to the name of the reference. So `(17)Post-Rock` yields both `Rock` and `Post-Rock`, and matches a predicate written against either.
4. A value consisting only of digits is also a reference to the Id3v1 genre table, and is replaced by the corresponding name.
5. The reserved values `RX` and `CR`, with or without parentheses, become `Remix` and `Cover`.
6. A reference to a table position that has no genre assigned to it is left as recorded.

The table referred to throughout is the Id3v1 genre table together with its widely adopted extensions, 148 entries in total.

#### Per-version caveats

**Id3v2.2.** Frames are named by their v2.4 equivalents, as described above. A text frame never holds more than one value, so a multivalue can only arise from a repeated frame.

**Id3v2.3.** A text frame never holds more than one value. The year is recorded in a different frame from the day and month: `TYER` is read as `TDRC`, while `TDAT` and `TIME` keep their names, so `id3v2::year` reads the year of a v2.3 file as it does any other, and the rest of the date is reached through `id3v2::field("TDAT")` and `id3v2::field("TIME")`.

**Id3v2.4.** A single text frame may hold several values, and this is the only revision in which the format says so. The parenthesized genre conventions belong to the earlier revisions and are not part of this one, but files carrying them are common, since that is what a conversion from an earlier revision leaves behind, and Goro understands them here too.

### Source `vorbis`

Reads the Vorbis comments of a file that carries them. Since every Vorbis field may appear any number of times, every concept and every source function of this source can resolve to a multivalue.

| Source function        | Type   | Description  |
|------------------------|--------|--------------|
| `vorbis::field(name)`  | string | The values of every comment whose field name is `name`
| `vorbis::bytes(name)`  | blob   | The value of every comment whose field name is `name`, as recorded

A field name is matched without regard to case, so `vorbis::field("mood")` reads a field recorded as `MOOD`. Each comment gives one occurrence, and `field()` is never unusable, Vorbis comment values being text by definition.
