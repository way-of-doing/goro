---
status: proposed
size: broad
touches: commands/audit.md (new), concepts/warnings.md, concepts/exit-codes.md, design/rationale.md, faq.md, src/Goro/Commands, src/Goro/Cli
after: mp3-support
branch:
---
# goro audit

## Intent

A command that says, file by file, everything Goro's readers found wrong or unusual in a file:
what, where, and what it means for the data Goro reads from it.

Goro answers "but what if I want to know about detail X" with two tools. Warnings, mostly
summaries emitted once at the end of a run, are a heads-up during any command. `goro audit` is
where the detail lives. This is what lets the other commands stay quiet: the `incomplete`
warning (see [file-reading](../design/file-reading.md#warning-categories)) counts the files that
were processed although part of them could not be read, and points here for the rest.

Everything below was gathered while [file-reading](../design/file-reading.md) was surveyed, and
is recorded here so that none of it is lost. None of it is decided.

## What it could report

Everything the readers can detect. The survey's prototype readers detect all of it today, as
loose strings; the readers built by mp3-support, ogg-support and flac-support would detect it as
typed conditions.

**Tags, per source**, by the rungs of the integrity ladder:

- **Quirks**, read as the writer meant:
  - iTunes-style v2.4 frame sizes written as plain integers;
  - text frames repeated, which the format forbids;
  - two Id3v2 tags in a row, or an Id3v2 tag appended at the end;
  - an Id3v2 tag before `fLaC` or `OggS`;
  - an APE tag before the audio;
  - APEv1 text in Latin-1;
  - APE keys differing only in case, of which only the last is seen;
  - two `VORBIS_COMMENT` blocks;
  - a Vorbis comment key holding a character the spec forbids;
  - a Vorbis comment header without its framing bit;
  - frames stored unsynchronised or compressed, which are legal but worth knowing about.
- **One datum unreadable:**
  - an unknown text encoding byte;
  - invalid UTF-8;
  - UTF-16 without a byte order mark, or with an odd length;
  - a comment without `=`;
  - zlib that will not inflate;
  - an encrypted frame;
  - an APE text item that is not UTF-8.
- **Structure broken:** where it broke (offset, and the frame or item it happened in), and what was
  read before it. Examples are a frame size running past the tag, a zero-size frame, an illegal
  frame identifier, an item count too high, a comment length overrun, and an Ogg comment page
  failing its CRC.
- **Tag unusable:** why. Examples are a size past the end of the file, a size that is not
  syncsafe, an unknown major version, an APE size past the start, and a vendor length overrun.

**Choices Goro made where a specification is silent** (see
[implementation](../implementation.md#choices-the-specifications-leave-open)): an APEv1 value
holding bytes outside ASCII, read as ISO-8859-1 and possibly the wrong text.

**Layout:**

- several Id3v2 tags in a row, of which only the first is the `id3v2` source;
- every quirk Goro applied to read a file (see [quirks](../design/quirks.md)), and UTF-16 text read
  as little-endian for want of a byte order mark;
- unexplained bytes before the audio. Junk, or a tag whose header was wiped: the one way a tag
  damaged past recognition can still be noticed;
- bytes after the last frame that no tag accounts for. These are never hashed, so audit is the
  only place they show;
- a tag size that disagrees with where the next structure starts.

**Playing time:** the evidence and the outcome.

- **MP3:**
  - whether there is a Xing, Info or VBRI header;
  - whether its byte count agrees with the audio found;
  - whether the LAME tag's CRC passes;
  - the encoder delay and padding;
  - and so why `file::duration` was, or was not, usable.
- **FLAC:** STREAMINFO against the last intact frame. Covers a total of 0, a total that disagrees,
  truncation, and a damaged tail.
- **Ogg:**
  - truncation (no end-of-stream page);
  - chained links;
  - multiplexed streams (declined);
  - gaps in page sequence numbers;
  - the last page failing its CRC.

**Audio integrity found by reading the whole file**, which the other commands never do just to
find a duration:

- MP3: places where frame sync is lost, and how many bytes were skipped; Info frames in the
  middle of a file, a sign of files joined end to end;
- FLAC: frames failing their CRC-16;
- Ogg: pages failing their CRC.

## Asides worth considering

- **Checksums already in the files.** FLAC frames carry a CRC-16 and Ogg pages a CRC-32, so an
  audit that reads the whole file can find bit rot in FLAC and Ogg *without a stored hash*. That
  overlaps the reason Goro exists (see [vision](../vision.md)). FLAC's STREAMINFO also holds an
  MD5 of the decoded audio, but checking it means decoding FLAC, which Goro does not do. MP3 frames
  rarely carry a CRC, but LAME's tag is described as holding a CRC of the whole audio ("music
  CRC"), which a whole-file read could check. Unlike the tag's own CRC, this one has not yet been
  verified by running code.
- **Prevalence questions only a collection can answer.** How many MP3s get an unusable
  `file::duration` because they have no valid summary header? How common is each quirk? The survey
  had no real damaged collection to look at. The maintainer's own collection is almost entirely
  well behaved, being encoded and tagged by one person from FLAC. Audit run by users is how these
  get answered.
- **The future command-line options it informs.** Two were floated:
  - raising the bar for tag data, so that only rungs 0 and 1 give usable occurrences;
  - allowing a full scan to find an MP3's duration when its header is missing or fails its checks.
  Audit is how somebody decides whether they need either.
- **Silent corruption.** Random bytes after `TAG`, or a flipped bit inside valid text, cannot be
  detected. A heuristic, such as control characters in Id3v1 text, could flag "suspicious"
  values. Whether audit should ever guess is open; everything else it reports is a fact.
- **Real-world damage samples.** Since few collections are damaged in interesting ways, test files
  from projects that collect them could widen the corpus: mutagen's and TagLib's test data,
  ffmpeg's FATE samples. Their licences decide whether they can be committed.

## Questions

- **The name.** `audit`, `check`, `doctor`, `inspect`.
- **Depth and cost.** Reading every byte of every file is what makes the integrity checks
  possible, and is expensive on slow storage. A quick mode reporting only what the other commands'
  reads detect, and a deep mode reading everything, or one mode only?
- **What is reported by default.** Quirks are not problems. Hidden unless asked, a separate
  level, or always listed?
- **Finding identity.** Each kind of finding probably wants a stable identifier for scripts, such
  as `id3v2.frame-overrun`, beside its message. Each message is spelled whole for each kind of
  finding, with placeholders carrying data, never words.
- **Output.** Plain and JSON, as the other commands have. One record per file holding its findings,
  or one per finding? Streaming, as JSON output must be (see [implementation](../implementation.md)).
- **Scoping.** Pathspecs, as every command takes. A predicate as well, so that one can audit, say,
  only files whose `file::duration IS UNUSABLE`?
- **Warnings and exit codes.**
  - Audit's findings are its output, not warnings. But a file that cannot be read at all: a
    finding, a `file` warning, or both?
  - With `--strict-exit-code`, does audit return a code saying whether it found anything, the way
    `20` says a query matched nothing?
- **What the `incomplete` warning says.** It points to `goro audit`. Until audit exists, it needs
  wording that does not name a command the user cannot run.
- **Formats.** Audit is only as good as the readers. It could land with MP3 alone and grow as
  ogg-support and flac-support land.
- **Hash.** `goro hash` reads every byte too. Should it report what it can see along the way, such
  as lost sync or failing CRCs, or is that audit's job alone?

## Done when

`commands/audit.md` describes the command; warnings, exit codes and the FAQ refer to it where
they should; the rationale records why detail lives in a command rather than in warnings; and
the command reports, for MP3 at least, every condition the reader detects, with tests driven by
the fixture corpus.

## Log

- 2026-10-08 -- Proposed at the end of the file-reading survey. The survey's decisions make the
  other commands report damage only as a count, so the detail needs a home. This brief collects
  everything about that home raised during the survey. Next step: pick it up after mp3-support has
  typed conditions to report, or earlier if the reader's condition types are designed with audit
  as their consumer.
- 2026-10-08 -- mp3-support agreed an edge analysis that every command shares, returning a
  `FileLayout` and never throwing over what a file contains. Audit's quick mode would be that
  analysis reported in full; its deep mode adds the walk the other commands never do. A file whose
  audio cannot be found is unreadable to `goro list` and `goro hash` but a finding to audit, which
  is the first case of a condition each command interprets in its own way. Chained and
  multiplexed Ogg are declined, and a file concatenated with itself, which only a walk detects, is
  audit's to find. The `incomplete` warning advertises audit even before it exists.
