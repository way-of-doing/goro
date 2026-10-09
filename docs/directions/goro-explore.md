---
status: proposed
size: broad
touches: commands/explore.md (new), design/rationale.md, implementation.md, testing.md, faq.md, src/Goro/Commands, src/Goro/Cli, src/Goro/Reading, src/Goro/Messages
after:
branch:
---
# goro explore

## Intent

A command that takes a single file, walks the whole of it, and shows a person everything Goro can
see in it:
- a layout map of the file, with byte offsets and a visual representation;
- every tag block and every gap;
- every value of every tag Goro knows how to read, including where a value is unusable, and why.

It is for looking at one file closely: working out why a predicate did not match it, why its
duration is unusable, why two copies hash alike or not, or what a tagger actually wrote.

It is not [goro audit](goro-audit.md), and the two stay separate commands. Audit is a mass-file
affair: it goes over a collection and reports what is wrong or unusual in each file, for scripts
as much as for people. Explore goes over one file and shows all of it, healthy or not. Their
approaches and goals differ, while much of what they need from the reader (below) is the same.
That shared part is built once, in the reader, and composed by each command for what it needs; it
belongs to whichever line comes first.

## What it shows

**The layout map.** An ordered list of regions that covers every byte of the file exactly once,
from 0 to the file's length, each with its start, end and length:
- **Each tag block:** format and revision, whether it is the source its format is read from, and
  whether it is intact, broken off at an offset, or unusable. Structure inside a tag (headers,
  frames, padding) belongs to the tag's region on the map and is detailed in the listing.
- **The audio's own regions:** the summary frame (Xing, Info or VBRI), when there is one, and the
  audio `[D, E)` that `goro hash` covers.
- **Inside the audio:** the stretches the walk had to resynchronise over, as damage within the
  audio rather than separate regions.
- **Gaps:** junk before the audio, between tags, and after the audio, whether or not a summary
  header vouched for it.
- **What cannot be decided:** a tag whose size cannot be trusted gives the region from its header to
  the first frame as "this tag, or junk after it".

The visual representation draws the same regions in file order, in a form a terminal can show.
It is not to scale. Every region is drawn wide enough to carry its label, so a 128-byte Id3v1 tag
is a thin slice that still reads "id3v1", and a region far larger than its neighbours, such as
megabytes of audio, is drawn with a dotted interior to say that its extent is large. Fields are
not drawn; the table of tags below the map shows them.

**The tags.** A table accompanying the map, for every tag block found, source or not:
- the header facts: revision, flags, size;
- every field in file order, one row each:
  - its identifier as recorded on disk, and as Goro reads it where the two differ, as for `TT2` and
    `TIT2`;
  - its offset and stored length;
  - the storage transformations its flags declare;
  - its description, where it has one;
  - its values as `field()` gives them, a long one cut short with a marker saying so;
  - for content with no text, its length alone, as `binary (12345 bytes)`;
  - or that it is unusable, and why: the text does not decode, the zlib does not inflate, the
    frame runs past its tag, and so on.

Where a tag's structure breaks off, the table shows where, and that nothing after it was found.
Fields stepped over as quirks appear with their offset and size.

**The audio:**
- the stream's parameters: MPEG version, layer, sample rate, channel mode, and bitrate or bitrates;
- the summary frame's contents: frame and byte counts, and the LAME tag's delay, padding and CRC;
- the playing time, what it rests on, or why it is unusable;
- the hash range, and how `E` was settled: at the edges, by a trusted header, or by the walk's own
  judgement;
- the frame count the walk found.

**Every condition** the analysis and the walk record, with its offset, so that what the
`incomplete` warning counts can be seen for this file.

## How it renders

The business code and the rendering are kept apart. What explore finds -- the map, the tag table,
the audio, the conditions -- is a model, made by the reader and the command and knowing nothing of
how it is shown. Pluggable UI modules are fed that model, and each renders it in its own way.

- **A safe baseline** comes first and stays the standard: plain ASCII, readable on any terminal,
  over any pipe, in a log.
- **Other modules are where the fun is,** and are welcome as experiments: full Unicode, terminal
  colour and background animations, an interactive one. JSON for scripts is one more module.
- **Experiments never cost the foundation.** A module reads the model and nothing else. One that
  wants something the model lacks gets it by extending the model for every module, or does without;
  it never reaches into the reader, and never bends the model to suit its own drawing. A module can
  be added, changed or dropped without touching the business code, the baseline, or the other
  modules.

This is the concept `goro list` and `goro hash` already follow: the executor's results are handed
to an `IOutputRenderer<TResult>` (`src/Goro/Output`), plain or JSON, which knows nothing of how
they were produced. Explore's modules are the same idea for a richer model, one file rather than a
stream of results. Nothing suggests coupling them: explore, and audit after it, are free to define
rendering interfaces of their own, shaped by what they render, and need not share
`IOutputRenderer<TResult>` or be shared with it.

Which module a run uses by default, and which options a user can select, are settled later.

## What the reader needs first

The reader is shared, and composed by each command, so that none pays for what it does not
use (see "File reader" in [architecture](../architecture.md)). What mp3-support built is the
baseline: the edge analysis every command runs, values read on demand, and the walk `goro hash`
runs. Explore adds to it, as further pieces a command opts into, and never as more work for every
command.

From the discussion of 2026-10-09 after mp3-support landed. Most of a region-level map is already
in `FileLayout` and `Mp3AudioRange`. What is missing:

- **The walk records what it sees.** Today it gives only `E`. Explore needs:
  - the frames it passed, as a count and the bitrates met;
  - every stretch it resynchronised over, with its extent;
  - whether a run of three or the edge settled `E`.

  This wants to be a piece the walk can be composed with, so that `goro hash` pays nothing for it.
- **Tags in unexpected places are recognised.** The analysis looks for tags only from either end,
  so these go unrecognised: a tag in the middle of the file, as in two files joined; a trailing v2.3
  tag, which has no footer; an APE tag with only a header. Where the walk resynchronises, it can
  check for `ID3`, `APETAGEX` and `TAG` and index what it finds as a tag block that is not a source.
  Audit wants the same finding.
- **The identifier as recorded is kept** on each field, beside the name Goro reads it under.
- **A frame stepped over for an illegal identifier is listed as a field** with its size, rather
  than only as a quirk at an offset.

Recognising tags where the walk resynchronises affects `goro hash` too: such a tag sits inside
`[D, E)` today and is hashed. Whether it should be cut out of the range is a question for this line
or audit, and changes stored hashes if it is answered yes.

## Questions

- **The name.** `explore`, `inspect`, `show`, `dump`.
- **The argument.**
  - Exactly one file, named by its path, rather than pathspecs.
  - Does it have to be an MP3? Discovery judges a file by its extension, and explore could read any
    file the readers recognise from its contents.
  - What is a directory, or several paths: an error?
- **Errors and warnings.** "Nothing about one file is ever an error" was written for runs over
  collections. Here the file is the whole run:
  - a file that cannot be opened: a `file` warning, an error, or exit code `1`?
  - a file with no audio: a map showing tags and no audio, since that is exactly what someone
    exploring it wants to see?
- **Defaults and options,** settled later: which module renders by default, whether a user can pick
  another, and options such as showing a value whole where the table has cut it.
- **JSON.** A module like any other, but it makes the model a contract, which is worth deciding
  deliberately. Every module spells its messages whole through the message provider, placeholders
  carrying data and never words.
- **Formats.** It lands with MP3. Ogg and FLAC follow as ogg-support and flac-support land. What
  does it say of a format Goro does not read: its regions unknown, the whole file one region?

## Done when

- **`commands/explore.md`** describes the command: its argument, what the map and the listing hold,
  its output formats, its exit codes.
- **The rationale** records why exploring one file is a command of its own beside audit.
- **The reader** has the additions above, with implementation.md updated where its notes change
  (the walk, tags found while resynchronising).
- **The command** builds the model of everything listed under "What it shows", for MP3, and renders
  it through the baseline ASCII module. The model is what is tested, on the fixture corpus:
  - for every fixture, the map's regions cover every byte exactly once;
  - every recorded field the manifest lists appears with its values;
  - every condition the analysis records appears.

  The baseline module keeps a snapshot of its rendering for a few fixtures. Other modules are not
  needed for the line to land.
- **The FAQ** points to it from questions such as why a file's duration is unusable.

## Log

- 2026-10-09 -- Proposed by PJ after mp3-support landed, from a discussion of what a map of an
  MP3 would still lack. At region level only two things do: tags anywhere but the edges, and a tag
  whose size cannot be trusted, which is inherent and drawn as such. Listing values completely
  needs the identifier as recorded, and stepped-over frames as fields.
- 2026-10-09 -- Decided with PJ: explore and audit stay distinct commands, since audit is a
  mass-file affair whose approach and goals differ. They share business code in the reader, which is
  written to be composed for each command's needs without any command paying for what it does not
  use; mp3-support built the baseline. Next step: settle the remaining questions, then pick it up.
- 2026-10-09 -- Decided with PJ on the rendering: the map is not to scale; every region is wide
  enough to carry its label, and a very large one has a dotted interior. Fields are not drawn, the
  table of tags showing them. Long values are cut with a marker, and binary content shows as
  `binary (12345 bytes)`.
- 2026-10-09 -- Decided with PJ on the structure: explore builds a model, and pluggable UI modules
  render it. A safe ASCII baseline is the standard. Unicode, colour, animation and interactive
  modules are welcome experiments, provided an experiment never costs the foundation: modules read
  the model and nothing else, and any one can be added or dropped alone. Defaults and user options
  are settled later.
