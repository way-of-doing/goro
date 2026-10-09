---
status: landed
size: broad
touches: features/builtins/identifiers.md, concepts/warnings.md, concepts/exit-codes.md, commands/hash.md, design/rationale.md, implementation.md, testing.md, faq.md, src/Goro/Predicates/Identifiers, src/Goro/Hashing, src/Goro/Warnings, src/Goro/Goro.csproj
after: sources-and-fields
branch: line/mp3-support
---
# MP3 files, read by Goro

## Intent

Read MP3 files with Goro's own reader: the tags an MP3 can carry (Id3v2, Id3v1 and APE), where
its audio is, and its playing time. This binds the tag sources, which are declared and bound to
nothing today, to real data. It also replaces TagLibSharp, which today finds both the audio range
`goro hash` hashes and `file::duration`. MP3 is the only format discovery admits, so once this
line lands TagLibSharp leaves `src/`.

The promise to keep comes from [sources-and-fields](sources-and-fields.md): `field()` and
`bytes()` give the data as recorded. [file-reading](../../design/file-reading.md) showed that no
.NET library keeps it, and recorded what was decided as a result. This line writes those
decisions into the normative documents and builds them for MP3.
[flac-support](../flac-support.md) and [ogg-support](../ogg-support.md) then do the same for their
formats.

## Settled by the survey

Each point is in [file-reading](../../design/file-reading.md#decisions), with the evidence behind it.

- **The reader is Goro's own,** for Id3v2 (all three revisions), APE (v1 and v2) and Id3v1. Frames
  are listed by their identifier on disk, so the rename table of sources-and-fields applies on
  Goro's side, and nothing a library converted leaks through.
- **Text that does not decode in its declared encoding** is an unusable occurrence. So is an
  unknown encoding byte, an encrypted frame, or zlib that will not inflate.
- **Damaged tag data.**
  - No level of damage discards the file.
  - Up to a broken structure, a source gives whatever it holds that is usable. A datum the tag
    clearly records but that cannot be read is an unusable occurrence; anything else not found is
    absent.
  - A wholly unusable tag is absent everywhere.
  - From a broken structure down, the file is counted in the new `incomplete` warning.
  - Every tag of a file is parsed whenever its tags are read.
- **`bytes()`** gives a frame's content with unsynchronisation, compression and the flag prefixes
  undone. Where they cannot be undone, the occurrence is unusable. Decompression is capped at a
  few megabytes per frame.
- **Playing time** is the length of the stream's timeline, with the LAME tag's encoder delay and
  padding removed.
  - An MP3 needs a valid summary header (Xing, Info or VBRI) whose byte count agrees with the audio
    and whose LAME CRC, where there is one, passes.
  - Otherwise `file::duration` is unusable and the file is reported. Goro does not scan.
  - The data warning that follows is not suppressed; the FAQ is to suggest
    `file::duration IS USABLE AND …`.
- **A file whose audio cannot be found is unreadable,** including when a bounded search gives up;
  one whose audio is found but whose playing time cannot be had has an unusable
  `file::duration`, which stays exactly one. `goro list` and `goro hash` treat the first as
  unreadable; `goro audit` reports it as a finding.
- **Tag values are read on demand.** Reading the tags indexes them; values and descriptions are
  read when a predicate asks, cover art only when `bytes()` does. A `TXXX`, `WXXX`, `COMM` or
  `USLT` frame whose description cannot be read is an unusable occurrence for every description
  asked about.
- **The hash covers `[D, E)`:** the frames from the first audio frame to the end of the last whole
  frame, without the Info frame or anything after `E`, by the rule for every format in
  [file-reading](../../design/file-reading.md#what-the-hash-covers). The range changes from
  TagLibSharp's for many files, which is harmless only while no hash has been stored anywhere; the
  rationale should say so.
- **Junk after the last frame** is never hashed, and `goro audit` reports it. A summary header is
  trusted up to an exact frame end with no frame starting there, so such junk does not cost the
  file its duration; see [implementation](../../implementation.md#choices-the-specifications-leave-open).
- **Several Id3v2 tags in a row:** the `id3v2` source is the first, as players and other readers
  take it; `goro audit` reports the others.
- **A constant-bitrate MP3 without a summary header** is counted from the edges, by checks
  recorded in [implementation](../../implementation.md#choices-the-specifications-leave-open), with
  all of an MP3's duration logic in a single type.
- **Shapes found in real files** ([file-reading](../../design/file-reading.md#real-world-test-data)),
  decided on 2026-10-08:
  - UTF-16 without a byte order mark is read as little-endian, an implementation choice;
  - terminator-separated values are read in every revision, a later UTF-16 value inheriting the
    first's byte order mark; recorded in [quirks](../../design/quirks.md), and identifiers.md now
    says so;
  - a frame with an illegal identifier is stepped over by its size, a quirk audit surfaces;
  - `GRP1`, `MVNM` and `MVIN` are text frames for `field()`, a quirk, and identifiers.md now says
    so. The binder's check, `Id3v2Frames.HoldsText`, still refuses them: a one-line change for
    this line;
  - v2.2 frame names in a v2.3 tag (iTunes 12.1) are left out for now, recorded in
    [deferred](../../design/deferred.md).
- **APEv1 text is read as ISO-8859-1,** as Id3v1 is. The specification says ASCII, and real files
  hold local code pages; see [implementation](../../implementation.md#choices-the-specifications-leave-open).
  The identifier documentation's claim that APEv1 is ISO-8859-1 by specification needs correcting.
- **Warning categories.** A new `incomplete` category, exit code `12`, between `unanswered` and
  `file`, which becomes `13`. It is one warning per run, a count, and it advertises `goro audit`
  even before audit exists. It lands as part of this line (decided 2026-10-08), although it
  reaches every format.

The old questions about TagLibSharp's deviations, canaries and skipped tests no longer apply: there
is no third-party reader left to deviate.

## The reader's shape

Agreed in discussion on 2026-10-08; the types are an outline, not a specification.

**An edge analysis, shared by every command.** One step reads only the edges of a file and
returns a `FileLayout`: the tags found and an index of their fields, the gaps (junk before the
audio, bytes after it), the audio's positions (`A` to `H` in [mp3-layout](../../design/mp3-layout.md)),
the duration outcome, and the conditions found. It never throws over what a file contains, only
over input and output: "audio not found" is part of the layout, and each command decides what it
means. Everything it learns is exposed, even where nothing uses it yet, such as whether a frame
ends exactly where the trailing tags start (`E = F`).

**Head, tail, collate.** The analysis is three pure steps: what the head holds (leading tags, the
first frame, the Info frame), what the tail holds (the trailing tags, found from the end
inwards, and whether a frame ends at `F`), and a collation that cross-checks the two and gives
the duration outcome. The steps run one after another within a file; Goro's concurrency stays
across files.

**Bounded, tracked reads.** The steps reach the bytes only through a reader that serves the head
and tail from windows read once, charges every other read to a purpose against the file's
allowance, and refuses what the allowance does not cover, so that "never walks the whole file" is
something a test can assert. (Revised in review on 2026-10-08: the limits are budgets for the whole
file, kept by a `ReadAllowance` that each file's reader takes from the shared `ReadPolicy`, and a
payload has a limit for each value; see the Log.)

```csharp
public interface IByteSource : IDisposable          // RandomAccess.Read in production, byte[] in tests
{
    long Length { get; }
    int Read(long offset, Span<byte> into);
}

public enum ReadPurpose { Sniff, DeclaredStructure, Search, Payload }

public sealed record ReadPolicy(                     // shared and immutable; defaults to begin with, configurable later
    int HeadWindow = 64 * 1024,
    int TailWindow = 64 * 1024,
    int SniffBudget = 4 * 1024,                       // budgets: totals for the whole file
    int SearchBudget = 256 * 1024,
    int StructureBudget = 16 * 1024 * 1024,
    int PayloadLimit = 4 * 1024 * 1024)               // for each value; also the decompression cap
{
    public ReadAllowance CreateAllowance();
}

public sealed class ReadAllowance                    // one per file: what is left, and whether a read fits it
{
    public bool Allows(ReadPurpose purpose, long count);
    public bool TryCharge(ReadPurpose purpose, long count);
}

public sealed class BoundedReader
{
    public BoundedReader(IByteSource source, ReadPolicy policy);
    public ReadResult Read(ReadPurpose purpose, long offset, int count);    // a refusal is a result
    public SearchResult Search(long from, SearchDirection direction, Func<ReadOnlySpan<byte>, long, bool> accept);
    public ReadLog Log { get; }                       // purpose, offset and length of every read
}

public sealed record FileLayout(
    AudioFormat Format,
    IReadOnlyList<TagIndex> Tags,                     // each tag's region, condition and field entries
    Region? LeadingGap, Region? TrailingGap,
    AudioLocation Audio,                              // Found(C, D, E-if-known, F) | NotFound(why)
    DurationOutcome Duration,                         // Known(samples, rate, basis) | Unusable(why)
    IReadOnlyList<Condition> Conditions,
    ReadLog Reads);

public sealed record FieldEntry(string Key, Region Stored, Transform Transform);   // located, not read

public sealed class TagValues                         // values on demand, through the same reader
{
    public ValueResult Bytes(FieldEntry field);
    public ValueResult Text(FieldEntry field);
}
```

**Loading and disposal.** `FileData` does not become disposable: it holds an `IFileDataLoader`
and does not know how loading works. The runtime creates the loader, which holds the file's open
handle and is disposable, primes `FileData` with it, and disposes it when the file is done, in a
`using` of its own. There is one loader per file, shared by all of that file's facets. The facets
for tags and for `file::duration` read from the layout; `goro hash` takes its range from the same
layout and finds `E` as it reads the frames.

## Questions

- **How `goro hash` finds `E` without a trusted summary header.** Answered on 2026-10-09: the walk
  resynchronises over damage, and a frame counts only when it ends at the limit or belongs to a run
  of three; two passes, headers then bytes. See implementation.md, "Finding where the audio ends".
- **Committing real test files.** Answered on 2026-10-09: none are committed. music-metadata's
  repository is MIT-licensed, but its samples are mostly commercial recordings (Beth Hart, Queen,
  Windows' "Sleep Away") and files from users' bug reports, whose rights its licence cannot grant;
  the other three projects' licences are copyleft. The corpus's `rw-` fixtures rebuild every shape
  they showed with Goro's own audio, and `fetch-realworld.sh` pins the originals for anyone who
  wants them.

## Done when

- **Rationale:** records why Goro reads tags itself, what damaged data resolves to, the timeline
  definition of playing time, and the warning categories.
- **Identifier documentation:** describes MP3 tags as they are actually read. That covers the
  damaged-data rules and `bytes()` for transformed frames. "Exactly one usable occurrence" for
  `file::duration` changes.
- **Warnings and exit codes:** have the `incomplete` category, and lose the rule that one damaged
  tag makes a file unreadable.
- **`goro hash`:** says what it hashes for an MP3.
- **Implementation notes:** lose the entry on damaged tags and `file::duration`, which described
  TagLibSharp.
- **Testing:** testing.md's TagLibSharp section becomes one about Goro's reader, driven by the
  fixture corpus in `tests/Goro.Tests/Fixtures/Audio`, and includes a fuzz test that the reader
  never throws.
- **FAQ:** has the `file::duration IS USABLE` entry.
- **Code:** the tag sources an MP3 can carry are bound to real data; `file::duration` and the hash
  range come from the new reader; and TagLibSharp is no longer referenced by `src/`.

## Log

- 2026-10-07 -- Split from sources-and-fields, which specifies the source functions and leaves
  reading to this line.
- 2026-10-08 -- Refreshed after the [file-reading](../../design/file-reading.md) survey, which
  answered most of the original questions and recorded the decisions above. The survey's prototype
  readers in `scratchpad/Goro.Scratchpad/FileReading` are reference material, not code to copy;
  the survey says what keeps them from production quality. Proposed steps, each able to land on
  its own:
  1. The rationale entries and the normative documents, the warning category included.
  2. The reader and the tags facet, binding the tag sources.
  3. `file::duration` from the reader.
  4. The hash range from the reader, then TagLibSharp removed.

  Decided the same day: the `incomplete` category lands in this line, and the decompression cap
  is a few megabytes per frame.
- 2026-10-08 -- Agreed the reader's shape (above): an edge analysis returning a `FileLayout`
  through bounded, tracked reads, tag values read on demand, and a disposable loader the runtime
  owns behind `FileData`. Narrowed the unreadable rule to files whose audio cannot be found.
  Chained Ogg declined, recorded in [limitations](../../design/limitations.md). The hash range leans
  towards audio plus decode parameters, measured for every format in file-reading.
- 2026-10-08 -- Decided: the hash covers `[D, E)`, by a rule for every format, hashing the codec's
  own stream and nothing the container adds. APEv1 text is read as ISO-8859-1. A summary header is
  trusted up to an exact frame end, and the junk after it is never hashed. Next step: pick the line
  up, starting with step 1.
- 2026-10-08 -- Picked up the line. Step 1 written, awaiting review:
  - rationale entries on why Goro reads tags itself, why damaged tag data does not make a file
    unreadable, playing time as the timeline, and what the hash covers, the last noting that the
    MP3 range changed from TagLibSharp's while no hash was stored; "three categories" became four;
  - identifiers.md: damaged data, `bytes()` with the storage transformations undone, an unreadable
    description, the first of several Id3v2 tags, and `file::duration` as the timeline, exactly one
    but possibly unusable. The Vorbis sentence saying `field()` is never unusable now defers to the
    damaged-data rule; ogg-support and flac-support should check it against their decisions;
  - warnings.md and exit-codes.md: the `incomplete` category at `12`, `file` at `13`, and the rule
    that a damaged tag makes a file unreadable gone;
  - hash.md says what an MP3's hash covers; implementation.md trades its TagLibSharp entry for edge
    reading and the reader never throwing; testing.md's TagLibSharp section is now one about
    Goro's reader, with damage, quirks, playing time and hash range scenarios; the FAQ has the
    `file::duration IS USABLE` entry.

  Decided with PJ: a file counts as `incomplete` once a run opens it, whatever the run needed from
  it; the two once-per-run warnings come in code order, `unanswered` then `incomplete`; the 4 MiB
  decompression cap stays out of the spec, a frame over it being one that cannot be decompressed.
  The steps gained one, since none put the warning category into code:

  1b. The `incomplete` category in `src/`: `file` renumbered to `13`, `--no-warn=incomplete`, and
      the count, with nothing to count until step 2.

  Still open: `architecture.md` says a load failure makes a file unreadable and that evaluation is
  synchronous because the tag library is; both change with step 2's loader. Next step: review of
  step 1, then 1b, in plan mode.
- 2026-10-08 -- Step 1 committed (`a9d1e6b`). Step 1b written, awaiting review: the `incomplete`
  category in `src/`, exit code `12` with file warnings at `13`, `--no-warn=incomplete`, and an
  `IncompleteWarning` emitted once per run after the `unanswered` one, worded by Goro rather than by
  the command. Each file's outcome now says how much of it was read (`FileReading`: not opened,
  read, read in part), and the tally counts the files opened and those read in part; an unreadable
  file counts as neither. `goro hash` marks every hashed file read; `goro list --filter` takes it
  from `FileData.Opened`, which a facet sets unless it only asks the file system (`OpensFile`), so a
  predicate on `file::size` opens nothing. Nothing is read in part yet: the CLI tests wrap the real
  planner in one that marks chosen files so. Next step: review of 1b, then step 2, the reader and
  the tags facet. Step 2 replaces `FileData.Opened` with the loader, which reports read-in-part
  from the layout's conditions.
- 2026-10-08 -- Step 1b committed (`a6a297c`). Step 2 split in three, each landing on its own:
  2a the reader, 2b the loader and the source functions, 2c the concepts (trimming, genres,
  track numbers, dates), since nothing in `src/` interpreted tag text yet either. 2a written,
  awaiting review:
  - `src/Goro/Reading/`: `BoundedReader` over an `IByteSource`, with head and tail windows and a
    log. Reads outside the windows are checked against a `ReadAllowance`, which each file's reader
    takes from the shared, immutable `ReadPolicy`. Sniffing, searching and declared structure each
    have a budget for the whole file; a payload has a limit for each value and is charged nothing.
    A structure budget (16 MiB) was added for a pre-v2.4 tag unsynchronised as a whole.
  - `Mp3Analysis`: head, tail and collation, giving a `FileLayout` with typed conditions.
  - Indexes for Id3v2, APE and Id3v1 that read no values, and `TagValues` reading them on demand.
  - The rename table moved to `Reading/Tags/Id3v2FrameNames`, so that `Reading` depends on
    nothing in `Predicates`.

  Found while testing:
  - the first-frame search read its whole 256 KiB limit for every file, and now reads in 16 KiB
    steps, so a healthy file costs its two windows and nothing more;
  - a header with nothing after it no longer counts as a confirmed frame, since sync near the end
    of a file that ends inside its tag passed for audio;
  - a zero-size frame is a single unreadable datum and not a break, so testing.md's damage table
    was corrected;
  - data after the start of an Id3v2 tag's padding counts as a break, recorded in implementation.md;
  - the corpus builder recorded `IPLS` as holding no text, though it is read as `TIPL`, so
    `Corpus.cs` was fixed and the manifest regenerated, changing only that row.

  In review PJ pointed out that the limits as first written were per read, so that a file could
  still be walked whole by small reads, and nothing tested otherwise. They became per-file budgets,
  held by an allowance per file as PJ suggested, and a refusal in the analysis now breaks off the
  structure and reports the file as incomplete; before, a refused frame header ended a tag's walk
  silently. Tests now assert that small reads stop at the budget, that a tag of 20,000 frames and
  a run of 500 tags are cut off and reported, and that no damaged file costs its analysis more than
  the policy's ceiling.

  Decided with PJ: when an MP3 holds two APE tags, the first in file order is the source, as for
  Id3v2. Also decided: `$02` and `$03`, which only v2.4 defines, are read in v2.2 and v2.3 tags
  too, recorded in quirks.md. From review:
  - only `FileByteSource` is disposable, since `BoundedReader` never owns its source and whoever
    opens a file holds it by its own type;
  - the Id3v2 encodings are an `IId3v2TextEncoding` with four stateless singletons, chosen by
    `IId3v2TextEncoding.FromByte` after section 4 of the Id3v2.4 structure document. They had been
    bare numbers switched on inside a class whose name suggested any format. What a frame's strings
    share, the inherited UTF-16 byte order and the quirks met, is a `FrameTextState` the caller
    passes by reference, since it belongs to reading one frame.

  Next step: review of 2a, then plan 2b.
- 2026-10-08 -- Step 2a committed (`556ed9b`). Step 2b written, awaiting review: the loader and the
  tag source functions.
  - `IFileDataLoader` / `FileDataLoader`: one per file, created by `PredicateStage` in a `using`,
    opening the file only when something inside it is first needed. `FileData` holds it, and
    `FileFacet.Load` takes the `FileData`. Whether a file was opened is the loader's to say, which
    retired 1b's `FileData.Opened` and `FileFacet.OpensFile`.
  - `TagsFacet` makes a file with no audio unreadable ("no MPEG audio found"). The TagLibSharp
    duration facet checks the layout first, so the rule, and "counts once opened", hold for a
    `file::duration` predicate too.
  - `field()` and `bytes()` are bound for `id3v2`, `ape` and `vorbis` (absent in an MP3, though the
    file is still opened). An Id3v2 description, and an APE key, match with `OrdinalIgnoreCase`, as
    `SourceCallName` compares names. Of APE keys differing only in case, the last is read.
    `HoldsText` gained Apple's three frames.
  - The concepts are still unbound, for 2c. `goro hash` still reads through TagLibSharp and counts no
    tag damage, until step 4.
  - architecture.md gained a File reader section, and its Executor section now covers the
    `incomplete` warning, which 1b had left out. Six test names still said `Returns12` for file
    warnings after 1b's renumbering, and were corrected.

  Run over the corpus, `goro list --filter 'id3v2::field("TIT2") == "Title v2.2"'` lists the one
  file, warns about the two titles that do not decode and the file that ends inside its tag, and
  counts 10 of 69 files as read only in part. Next step: review of 2b, then plan 2c.
- 2026-10-09 -- Step 2b committed (`1af6935`, with PJ's syntax change). Step 2c written, awaiting
  review: the concepts. Decided with PJ:
  - the genre table takes TagLib's spellings (`Alternative Rock`, `Psychedelic`, `Bebop`, ...), so
    that a genre recorded by number matches the same genre recorded as text; recorded in quirks.md;
  - entry 133 is `Worldbeat`, as current TagLib has it (TagLibSharp 2.3.0 still had the original
    slur, which first led me to ask the question on a wrong premise);
  - a refinement that only repeats its reference's name, without regard to case, adds no value, so
    `(17)Rock` is one genre, and the original spelling counts as a repeat too (`(67)Psychadelic`).
    identifiers.md's genre rule 3 now says so.

  Source of the table: TagLib's `id3v1genres.cpp`, entries 0 to 147. The original spellings came
  from Id3v2.3's Appendix A (to 125) and TagLib's table of legacy names (129, 133). The table is
  now listed in a new normative `features/builtins/genres.md`, generated from the code.
  - `Predicates/Identifiers/Interpretation/`: `Genres` (table and `TCON` resolution),
    `TrackNumbers`, `DateShapes`, and `ConceptInterpretation`, which says per concept how recorded
    text becomes occurrences, with a convention per source where there is one (`TCON` for Id3v2, a
    byte for Id3v1). A trimming helper was planned and turned out to be one line within it.
  - Every cell is bound through `TagBindings.Cell`. `NotImplemented` is gone, with the tests that
    expected it. Vorbis cells are absent in an MP3.

  Over the corpus, `genre == "rock"` and `year == 1991` list the expected files, and warn where a
  genre or year is damaged.

  From review:
  - the catalog's table names each cell's field by a constant, such as
    `ConceptFieldMapping.Id3v2.Artist`, which shows the order of a row;
  - source names are `SourceNames` constants wherever they are mapped to anything, which retired
    `FileSource.Name` and the catalog's private strings;
  - Id3v1's field names are `Id3v1Fields`, shared by the reader and the mapping;
  - `DateShapes`' digit parsing goes through `int.TryParse`, and checks its bounds, so it cannot
    overflow whatever it is asked to read.

  Next step: review of 2c; then step 3, `file::duration` from the reader.
- 2026-10-09 -- Step 2c committed (`73f4e27`). Step 3 written, awaiting review: `file::duration`
  from Goro's reader.
  - `Reading/Mp3/Mp3Duration` holds all of an MP3's duration logic: the Xing, Info or VBRI header
    with the LAME tag and its CRC-16/ARC; the trust checks, within one frame of the audio or up to
    an exact frame end with nothing starting there; the seven-probe constant-bitrate count from the
    edges.
  - `FileLayout` gained `Duration` (`Known` with its basis, or `Unusable` with the problem) and the
    `SummaryFrame`. An unusable duration is a `DurationUnusable` condition, which makes the file
    incomplete.
  - `file::duration` reads the layout, and is one unusable occurrence where the playing time
    cannot be had. TagLibSharp's facet is gone: TagLibSharp is now only `goro hash`'s, until step 4.

  Over the corpus every LAME-encoded fixture gives 3.300 s, header-less CBR files 3.344 s, the
  MPEG-2.5 seed 3.456 s. Seven files have no usable duration: the doubled Xing count, the
  half-truncated and joined files, header-less VBR, and the truncated header-less CBR file. That
  agrees with the survey once the constant-bitrate decision is applied.

  Decided with PJ: deciding the playing time stays eager, so the probes run for any predicate that
  opens a header-less file. Deciding it only when asked is recorded in deferred.md as a thing to
  explore. Next step: review of step 3, then step 4, the hash range from the reader and
  TagLibSharp removed.
- 2026-10-09 -- Step 3 committed (`8ba137b`). Step 4 written, awaiting review: the hash range from
  Goro's reader, and TagLibSharp removed from `src/`. Decided with PJ: `E` is the end of the last
  frame that ends at the walk's limit or belongs to a run of three consecutive frames, found by a
  walk that resynchronises over damage, in a first pass before the hash.
  - `Reading/Mp3/Mp3AudioRange` finds `[D, E)`.
  - `Hashing/Mp3AudioHasher` replaces `TagLibAudioHasher`, and `IAudioHasher` returns an
    `AudioHash` with `Incomplete`, so `goro hash` now counts damaged tags like `goro list`.
  - The `TagLibSharp` package reference is gone. Two comments in `src/` cite TagLib as prior art.

  Over the corpus:
  - the CBR seed, with and without its Info frame, shares one hash with the 59 fixtures built from
    either, whatever their tags or junk;
  - the VBR seed shares one with its versions without a header, with a doubled Xing count, and with
    VBRI;
  - only damaged audio and different encodings hash differently.

  At PJ's request, the goro-audit brief now lists, among its MP3 findings, a hash span whose end
  the walk had to settle by its own judgement.

  Every item of Done when is now met. What remains before the line can land: review of this step,
  and the open question on committing real test files, which the MIT licence now bears on.
- 2026-10-09 -- Step 4 committed (`93ba301`). PJ asked for the MIT-licensed test files, music-metadata's,
  to be committed with their notice. Looking closer, they are mostly commercial recordings and
  bug-report files, which the repository's MIT licence cannot cover, and 53 MB in all, so on PJ's
  decision none are committed. The open question is closed, and file-reading.md and the fetch
  script now say why; the brief had wrongly called the samples MIT-licensed. The line's code and
  docs are complete. Next step: review the line as a whole and land it on main with a merge commit.
- 2026-10-09 -- Landed on main with a merge commit, and moved to the archive. Goro reads MP3 tags,
  playing time and the hash range itself, and no longer depends on TagLibSharp. What it set aside
  for others: Ogg and FLAC (ogg-support, flac-support), the findings for goro-audit, and in
  deferred.md, v2.2 names in v2.3 tags and deciding a header-less file's playing time only when
  asked.
