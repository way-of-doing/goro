---
status: active
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

The promise to keep comes from [sources-and-fields](archive/sources-and-fields.md): `field()` and
`bytes()` give the data as recorded. [file-reading](../design/file-reading.md) showed that no
.NET library keeps it, and recorded what was decided as a result. This line writes those
decisions into the normative documents and builds them for MP3.
[flac-support](flac-support.md) and [ogg-support](ogg-support.md) then do the same for their
formats.

## Settled by the survey

Each point is in [file-reading](../design/file-reading.md#decisions), with the evidence behind it.

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
  [file-reading](../design/file-reading.md#what-the-hash-covers). The range changes from
  TagLibSharp's for many files, which is harmless only while no hash has been stored anywhere; the
  rationale should say so.
- **Junk after the last frame** is never hashed, and `goro audit` reports it. A summary header is
  trusted up to an exact frame end with no frame starting there, so such junk does not cost the
  file its duration; see [implementation](../implementation.md#choices-the-specifications-leave-open).
- **Several Id3v2 tags in a row:** the `id3v2` source is the first, as players and other readers
  take it; `goro audit` reports the others.
- **A constant-bitrate MP3 without a summary header** is counted from the edges, by checks
  recorded in [implementation](../implementation.md#choices-the-specifications-leave-open), with
  all of an MP3's duration logic in a single type.
- **Shapes found in real files** ([file-reading](../design/file-reading.md#real-world-test-data)),
  decided on 2026-10-08:
  - UTF-16 without a byte order mark is read as little-endian, an implementation choice;
  - terminator-separated values are read in every revision, a later UTF-16 value inheriting the
    first's byte order mark; recorded in [quirks](../design/quirks.md), and identifiers.md now
    says so;
  - a frame with an illegal identifier is stepped over by its size, a quirk audit surfaces;
  - `GRP1`, `MVNM` and `MVIN` are text frames for `field()`, a quirk, and identifiers.md now says
    so. The binder's check, `Id3v2Frames.HoldsText`, still refuses them: a one-line change for
    this line;
  - v2.2 frame names in a v2.3 tag (iTunes 12.1) are left out for now, recorded in
    [deferred](../design/deferred.md).
- **APEv1 text is read as ISO-8859-1,** as Id3v1 is. The specification says ASCII, and real files
  hold local code pages; see [implementation](../implementation.md#choices-the-specifications-leave-open).
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
audio, bytes after it), the audio's positions (`A` to `H` in [mp3-layout](../design/mp3-layout.md)),
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
and tail from windows read once, charges every read to a purpose, and refuses what its policy
does not allow, so that "never walks the whole file" is something a test can assert:

```csharp
public interface IByteSource : IDisposable          // RandomAccess.Read in production, byte[] in tests
{
    long Length { get; }
    int Read(long offset, Span<byte> into);
}

public enum ReadPurpose { Sniff, DeclaredStructure, Search, Payload }

public sealed record ReadPolicy(                     // defaults to begin with; configurable later
    int HeadWindow = 64 * 1024,
    int TailWindow = 64 * 1024,
    int SearchLimit = 256 * 1024,
    int PayloadLimit = 4 * 1024 * 1024);              // also the decompression cap

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

- **How `goro hash` finds `E` without a trusted summary header.** Walking the frames resynchronises
  over damage, but random bytes after the audio can hold chance frame headers, and resynchronising
  onto them would hash part of the junk. One answer is to end at the last frame followed by a run
  of good frames or by `F`. The duration is unusable for such a file anyway; only the hash needs
  this.
- **Committing real test files.** None are committed, their licences unweighed. music-metadata's
  samples are MIT-licensed, which permits it with the notice kept, and Goro is now MIT-licensed
  too.

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
- 2026-10-08 -- Refreshed after the [file-reading](../design/file-reading.md) survey, which
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
  Chained Ogg declined, recorded in [limitations](../design/limitations.md). The hash range leans
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
