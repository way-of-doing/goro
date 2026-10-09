# Reading audio files

This is a survey made ahead of [mp3-support](../directions/mp3-support.md),
[ogg-support](../directions/ogg-support.md) and [flac-support](../directions/flac-support.md). It
is exploration rather than specification: it asks how Goro should read tag data and playing time,
what it can promise when a file is damaged, and whether any existing code reads files the way Goro
needs. Whatever the lines decide moves into the rationale and the normative documents in their own
terms.

Every claim below was checked by running code, against the fixture corpus in
`tests/Goro.Tests/Fixtures/Audio` (see its README) and the probes in
`scratchpad/Goro.Scratchpad/FileReading`. To repeat the measurements:

```sh
dotnet run -c Release --project scratchpad/Goro.Scratchpad -- corpus        # rebuild the corpus
dotnet run -c Release --project scratchpad/Goro.Scratchpad -- probe <dir>   # every reader over it
```

The reference for playing time is ffmpeg 9.0.2, decoding each file and counting the samples it
outputs. Where ffmpeg is wrong, the text says so and why.

## The answer in short

Goro should read all three formats itself, tags and playing time alike, and stop reading files
through TagLibSharp.

- **None of the .NET tag libraries tried gives tag data as recorded.** TagLibSharp, TagLibSharp2
  and ATL all decode text as they read it and keep none of its bytes. Each renames, merges, splits, trims or drops something a
  healthy file holds, and each turns text that does not decode into U+FFFD without saying so.
- **They fail as a whole where Goro needs a failure in part.** A damaged comment header makes
  TagLibSharp throw, and the duration is lost with it. A frame size running past the end of its tag
  makes it return the following frames' bytes as text.
- **Their playing time is not fit for whole seconds.** None of the three applies the gapless
  information an MP3 carries, so their answers run 44 to 80 ms long. TagLibSharp misreads a VBR
  file without a Xing header by a factor of three to four, and all three report 0 s for a FLAC file
  whose STREAMINFO does not give its length.
- **The code is small.** The prototype readers, which hold every rule described below, are about
  1,000 lines, plus about 350 of shared parsing and checksums. ogg-support and flac-support need
  most of that container parsing for hashing anyway.

## What Goro needs

The [identifier documentation](../features/builtins/identifiers.md) and the
[sources-and-fields](../directions/archive/sources-and-fields.md) line set the bar:

- `field()` gives the text of a datum exactly as recorded, with one occurrence for each value the
  format records.
- `bytes()` gives its content as recorded, with one occurrence for each frame, item or comment.
- An Id3v2 frame is named by its own identifier, through Goro's own rename table. It is never named
  by whatever a library converted it to.
- `file::duration` is whole seconds, truncated, so an error of tens of milliseconds changes the
  answer for a few files in every hundred.
- [Warnings](../concepts/warnings.md) currently makes a file unreadable if any of its tags cannot
  be parsed. The brief calls that a placeholder.

What playing time means was not written down anywhere. It was decided on 2026-10-08 (see
[Decisions](#decisions)):

> **The length of the stream's timeline.** It is trimmed wherever the file says how: the LAME
> tag's encoder delay and padding, Opus pre-skip, and the exact lengths of Vorbis and FLAC. It
> ends at the last intact frame or page.

Damage in the middle of a stream does not shorten the timeline, while truncation does. Players
behave the same way, and the [audio hash](../commands/hash.md) is what reports the damage.

## Integrity: what can still be read

### Tags

Tag damage falls into six rungs. The fixture names are files in the corpus.

| Rung              | Detectable          | What a faithful reader can give                              | Examples
|-------------------|---------------------|--------------------------------------------------------------|----------
| 0. healthy        | -                   | everything, as recorded                                      | `id3v24.mp3`, `vorbis-comments.ogg`, `comments.flac`
| 1. quirks         | yes                 | everything, read as the writer meant it                      | iTunes-style v2.4 frame sizes, duplicate text frames, two Id3v2 tags, Id3v2 before `fLaC` or `OggS`, APE before the audio, APEv1 in Latin-1, two `VORBIS_COMMENT` blocks, a missing framing bit
| 2. one datum      | yes                 | every other datum intact; this one has bytes but no text     | invalid UTF-8 or UTF-16, encoding byte 5, a comment without `=`, bad zlib, an encrypted frame
| 3. structure lost | yes                 | everything before the damage; nothing after it               | a frame or item size running past the tag, a zeroed block, an item count too high, a comment length overrun
| 4. tag unusable   | yes                 | nothing from this tag; other tags and the audio unaffected   | a tag size past the end of the file or not syncsafe, Id3v2 "v2.5", an APE size past the start, a vendor length overrun
| 5. silent         | Ogg only (page CRC) | wrong text, unless the container has a checksum              | a bit flip inside valid text, random bytes after `TAG`

Two findings shape the rules:

- **Rung 2 is where libraries corrupt text silently.** TagLibSharp, TagLibSharp2 and ATL all read
  `Mot\xF6rhead` declared as UTF-8 as `Mot�rhead`, and an odd-length UTF-16 value as `Odd�`.
  The prototype reports such a datum as not decoding, and `bytes()` still has the bytes.
- **Rung 3 is where libraries either guess or give up.** TagLibSharp, given a TCON frame whose size
  runs past the tag, returns `(17)Post-RockTRCK\0\0\0…3/12TDRC…` as the genre. Given a Vorbis
  comment length that runs past its packet, it throws, and the file's duration is lost with it.
  TagLibSharp2 refuses the whole file in the same case, and stops silently at a zero-size Id3v2
  frame.

What each rung yields was decided on 2026-10-08; see [Decisions](#decisions). In short, rungs 0
to 3 give whatever is usable, rungs 3 and 4 also report the file, and no rung discards a file.

### Playing time

| Rung                      | Answer                                                     | Examples
|---------------------------|------------------------------------------------------------|----------
| 0. declared and confirmed | exact, from the header                                     | Xing frames whose byte count matches the audio; STREAMINFO matching the last frame; the last Ogg granule
| 1. declared value missing | FLAC: exact, from the last frame. MP3: unusable            | STREAMINFO total 0; VBR without Xing; CBR without an Info frame
| 2. declared value wrong   | FLAC, Ogg: exact, from observation. MP3: unusable          | truncated files, joined MP3s, a doubled Xing count, a doubled STREAMINFO total
| 3. damage mid-stream      | the nominal timeline, from the header or the last granule  | a zeroed block, an Ogg page failing its CRC, a missing Ogg page
| 4. no audio found         | unusable                                                   | a file ending inside its Id3v2 tag
| ambiguous                 | by policy                                                  | chained Ogg (summed); multiplexed Ogg (declined: no stream is "the" audio)

The cheap checks, all verified on the corpus:

- **MP3:** trust the Xing or VBRI frame count only if its byte count matches the audio found,
  within one frame, and the LAME tag's CRC passes. That CRC is CRC-16/ARC over every byte of the
  Info frame before the CRC field, so it covers the frame count. A doubled count with an unchanged
  byte count fails it. When more bytes follow than the header counts, it is still trusted if a
  frame ends exactly where it says the audio ends and no frame starts there; the rest is junk.
  Otherwise the duration is unusable: Goro does not scan.
- **FLAC:** read back from the end of the audio to the last frame whose header CRC-8 and frame
  CRC-16 both pass. If its end sample matches STREAMINFO, use STREAMINFO. If not, use the last
  frame, unless the bytes after it could hold the missing samples. That case is a damaged tail,
  not a truncation, and STREAMINFO stands.
- **Ogg:** use the last granule on a page that passes its CRC, minus Opus pre-skip. For a chain,
  sum the links, in seconds, since links can differ in sample rate.

With these rules the prototype gives no duration for 11 files: 10 MP3s whose header is missing or
fails its checks, and the multiplexed Ogg file. Of the 108 files with both a duration and an
ffmpeg reference, it agrees with ffmpeg to within 50 ms on 96. The other twelve are explained:

- **Five MP3s whose first frame does not follow the tag directly:** junk, garbage, an APE header,
  or a tag size that is too short or not syncsafe. ffmpeg misses the LAME tag in all of them and
  decodes the Info frame as audio, giving 3.370 s. The prototype's 3.300 s is right.
- **Six files with mid-stream damage:** the prototype reports the nominal timeline, by the
  definition above, and ffmpeg reports what it could decode. ffmpeg's Vorbis decoder stops
  entirely at a missing page, at 1.905 s of 3.300 s.
- **One Ogg seed:** ffmpeg decodes only the first link of a Vorbis-then-Opus chain, 3.300 s of
  5.000 s.

The prototype still scans every MP3 frame header, but only to record evidence for `goro audit`,
never to give the answer. A scan would be cheap in CPU, about 1.2 ms for a 5.3 MB, ten-minute VBR
file once cached, and 7 ms for 24 MB at 320 kbps. Its cost is reading the whole file rather than
about 128 KiB of it, which is part of why a header that fails its checks makes the duration
unusable instead.

Bytes appended after the last frame, with no tag to account for them, make the byte count
disagree. The exact-frame-end check above settles `mp3/garbage-appended.mp3`, which gets its
3.300 s while `mp3/joined-cbr.mp3`, where a frame starts at that point, stays unusable. The
check reads a few KiB, and since no answer is right for every file, it is recorded among
[implementation](../implementation.md#choices-the-specifications-leave-open)'s choices.

## Prior art, measured

Each library ran over every corpus file through an adapter in `Candidates.cs`. The fidelity scores
compare what it returned against `manifest.json`.

### Tags

- **TagLibSharp 2.3.0** (July 2022, still the latest release).
  - *Text:* not faithful. In v2.3, a `TPE1` of `AC/DC` is split into `AC` and `DC`;
    `TYER`, `TDAT` and `TIME` are merged into `TDRC`; `TORY` is renamed `TDOR`; `IPLS` is cut to
    its first value; and `RVAD` is dropped. In v2.2, frames are renamed and `TCO` `(17)` is lost.
    Compressed and encrypted frames are dropped. Empty values become no value. A second Id3v2 tag
    and a second `VORBIS_COMMENT` block are ignored. APE is not found behind a Lyrics3v2 block.
  - *Damage:* U+FFFD for text that does not decode; reads past a frame overrun into the following
    frames. Throws for a damaged comment header, an Id3v2 tag before `OggS`, "v2.5", and chained
    Ogg, losing the duration with the tags.
  - *Raw access:* `FrameFactory.AddFrameCreator` is handed each frame's stored bytes, with three
    catches. The identifier is already converted. A v2.3 tag is resynchronised first. And
    compressed and encrypted frames never reach it.
- **TagLibSharp2 0.6.0** (January 2026; young, clean-room, result-based API).
  - *Text:* the closest on Id3v2, keeping v2.3 frames as written. But v2.2 frames get v2.3 names;
    APE in MP3 is not read, and neither is an appended Id3v2 tag. A FLAC or Ogg file with an Id3v2
    tag in front is taken for an MP3, losing its comments and duration. Only the last
    `VORBIS_COMMENT` block is kept, and `APIC` is lost under tag-level unsynchronisation. APE
    items keep raw bytes, but only in WavPack, Monkey's Audio and Musepack.
  - *Damage:* U+FFFD for text that does not decode; stops silently at a zero-size frame; refuses
    the whole file for any damaged comment header, and for a missing framing bit.
- **ATL 7.18.0** (September 2026). One merged view across all tags: it trims values, reformats
  dates, expands genres and joins repeated fields. It is a unified tagger rather than a reader of
  what is recorded. U+FFFD for text that does not decode; reads past a frame overrun.
- **libvorbis and libFLAC**, through `vorbiscomment -R` and `metaflac`. Comment bytes come back as
  recorded. libFLAC refuses the whole metadata chain for every structural damage tried, and
  `vorbiscomment` 1.4.3 crashed (exit 139) on three of the damaged comment headers. They need a
  native binary for every platform, and cover Vorbis comments alone: no Id3v2 or APE.
- **mutagen 1.47** (Python, a reference only). The nearest existing design to what Goro needs:
  `translate=False` keeps v2.3 frames as written. It still drops empty and unknown frames from its
  main view, and refuses a whole APEv1 tag holding Latin-1 text.

### Playing time

All figures are seconds. Reference 3.300 s, except where noted. Under the decided rules an MP3 whose
summary header is missing or fails its checks has an unusable duration; the prototype's scan,
shown in brackets, is evidence only.

| File                                  | ffmpeg  | TagLibSharp | ATL   | TagLibSharp2 | NLayer  | prototype
|---------------------------------------|---------|-------------|-------|--------------|---------|------------------------
| CBR MP3 with LAME tag                 | 3.300   | 3.380       | 3.370 | 3.344        | 3.300   | 3.300
| VBR MP3 without Xing (3.344)          | 3.344   | 0.888       | 6.077 | 0.868        | 3.344   | unusable (scan 3.344)
| the same, ten minutes (597.342)       | 597.342 | 190.9       | 763.5 | not run      | 597.342 | unusable (scan 597.342)
| VBR MP3 cut in half (1.595)           | 1.595   | 3.344       | 3.344 | 3.344        | 3.300   | unusable (scan 1.607)
| Xing frame count doubled (3.319)      | 3.319   | 6.687       | 6.687 | 6.687        | 6.644   | unusable (scan 3.331)
| two CBR MP3s joined (5.095)           | 5.095   | 5.148       | 5.146 | 3.344        | 3.300   | unusable (scan 5.107)
| FLAC, STREAMINFO total 0              | 3.300   | 0           | 0     | 0            |         | 3.300
| FLAC, STREAMINFO total doubled        | 3.300   | 6.600       | 6.600 | 6.600        |         | 3.300
| FLAC cut at 60% (1.765)               | 1.765   | 3.300       | 3.300 | 3.300        |         | 1.765
| Ogg Vorbis chained, 3.3 + 1.7 s (5.0) | 5.010   | throws      | 3.300 | 1.700        |         | 5.000
| Ogg Opus cut at 60% (1.994)           | 1.994   | 2.987       | 3.000 | 2.994        |         | 1.994

ATL also has an exact mode, `MP3_parseExactDuration`, which scans. It still reports 3.370 s for the
CBR file with a LAME tag, and 3.423 s for the VBR file without Xing. NLayer applies the LAME tag
and scans when there is no header, but trusts any header it finds. NVorbis reads one link of a
chain (3.300 s of 5.010 s), and one stream of a multiplexed file (1.700 s, the shorter one).
Concentus.Oggfile reports 0 s once a comment page fails its CRC.

ffprobe's *claimed* durations are no better: 3.529 s for the VBR file without Xing, and 1.700 s for
the chained Vorbis file that decodes to 5.010 s.

### Low-level libraries with C# bindings

Bindings exist for libFLAC (FLACSharp), libopus (OpusSharp, Concentus as a port), and libsndfile
(NAudio.SoundFile). None of them was tried in code beyond libFLAC and libvorbis through their own
tools, above. They decode audio, which Goro does not need, and they bring a native binary for every
platform. They do nothing for Id3v2 or APE, which are most of the work.

## The approach per format

| Format | Prototype lines | Shared with hashing                                                     | What Goro reads itself
|--------|-----------------|-------------------------------------------------------------------------|-----------------------
| MP3    | 268, tags 420   | the tag boundaries are the hash range, which TagLibSharp computes today | Id3v2 in all three revisions (unsynchronisation, compression, extended headers, footers, appended tags); APE v1 and v2, with header or footer, at either end; Id3v1; Lyrics3v2 skipped; frame sync with confirmation; Xing, VBRI and the LAME tag with its CRC; a scan, as audit evidence only
| Ogg    | 116, tags 45    | ogg-support hashes packets, not pages                                   | a page walker with CRC and sequence checks; packet assembly; the comment parser
| FLAC   | 163             | flac-support needs the end of the metadata                              | a metadata block walker that trusts no length; STREAMINFO; the last intact frame

Lines are non-blank, non-comment lines of the prototype, structure and duration first. MP3's tags
are Id3v2 310, APE 84 and Id3v1 26 lines, and the Vorbis comment parser is shared by Ogg and FLAC.
Not counted are about 350 lines of shared parsing and checksums in `Containers.cs` and `Bin.cs`:
MPEG and FLAC frame headers, Ogg pages and packets, CRCs. The corpus builder uses them too.

That is about 1,350 lines of prototype, against a test corpus that already exists. A production
version with its tests might be two or three times as long. That is a real cost, but a bounded
one, and it removes a dependency whose behaviour Goro would otherwise have to pin with canaries
and skipped tests.

Two things TagLibSharp does today would need replacing: the MP3 hash range, and
`file::duration`. Writing tags, which the vision expects one day, is the one job a library might
still be wanted for. That can be weighed when it comes.

### What keeps the prototype from production quality

The prototype in `scratchpad/Goro.Scratchpad/FileReading` is reference material, not a starting
point to copy. `TagReaders.cs` and `Containers.cs` are the most useful parts; the rules they
encode are the point. What stands between them and production, roughly by importance:

1. **Whole files in memory.** Every reader takes the file as one `byte[]`. A production reader
   reads the head and the tail and seeks, which means separating where bytes come from from how
   they are parsed.
2. **No tests of its own.** The probe and the fuzzer are its only checks. The corpus and its
   manifest are ready to drive real tests.
3. **Untyped results.** Problems come back as strings and loose records. Production wants a typed
   condition per source (the rungs of the integrity ladder) and a typed duration outcome, since
   those are what the `incomplete` warning and `goro audit` consume.
4. **Known gaps.**
   - No search for the audio, or for `fLaC`, when a tag's size points past the end of the file.
   - The Vorbis start offset is assumed to be 0.
   - The refinement for junk after the last MP3 frame is not built.
   - Decompression is not capped.
   - The FLAC test telling a damaged tail from a truncation is a heuristic, checked on two files.
5. **Duplicated decoding in the corpus builder.** `Corpus.cs` decodes text again to write the
   manifest's expected values. That keeps the expectations independent of the readers under test,
   which is deliberate, but the copy is lenient and partly duplicated.

Two things are not kept in the repository. The probe's outputs (`durations.md`, `fidelity.md`,
`results.json`) are regenerated by `probe <dir>`, which needs ffmpeg installed. And
`make-seeds.sh` reproduces the seeds byte for byte only with the encoder versions it names, so
the committed seeds, not the script, are the reference.

## Verified along the way

Checked by running code, and worth knowing beyond this survey:

- **LAME writes the Info tag at the offset of a frame without CRC, even in a CRC-protected frame.**
  The [duration notes](https://claude.ai/artifact/LANGVMQgxowUJtqEaz7YcZ) say to add 2 bytes for a
  CRC. LAME 4.0 does not, and ffmpeg reads it where LAME puts it.
- **The LAME tag CRC covers the bytes before the CRC field.** Descriptions of it say "the first 190
  bytes", which is right only for stereo MPEG-1.
- **TagLibSharp reads APE tags in MP3 files correctly**, apart from text that is not valid UTF-8.
  A NullReferenceException seen early in the probe was the adapter's: an APE text item's `Value`
  is null.
- **TagLibSharp's MPEG reader makes an Id3v2 tag that is not on disk**, filled from the other tags.
  Asking it for the Id3v2 tag without checking `TagTypesOnDisk` reads APE and Id3v1 data as Id3v2.
- **ffmpeg decodes the Info frame as audio** whenever the first frame is not where it expects, and
  decodes only the first link of a chained Vorbis-then-Opus file.

- **"Never throws" needs fuzzing to be believed.** The prototype readers passed the whole corpus,
  but truncating and overwriting every corpus file 200 times found 16 exceptions: an integer
  overflow in a frame-size walk, and fields read past the end of a short header. After fixes,
  124,000 damaged reads give none (`fuzz 1000`). A production reader wants the same check in its
  test suite.

## Real-world test data

The corpus is hand-built, so its shapes are the ones somebody thought of. To find others, the
prototype was run over the test data of four tag-reading projects, files most of which came from
users' bug reports: mutagen (`ada28b2`), TagLib (`961dd69`), TagLib# (`da41dc3`) and music-metadata
(`9b71259`). That is 240 audio files: 163 MP3, 39 Ogg and 38 FLAC.
`scratchpad/Goro.Scratchpad/FileReading/fetch-realworld.sh` fetches them at those commits and
`realworld <dir> <out>` dumps what the prototype reads. Their licences (GPL-2.0, LGPL-2.1 or
MPL-1.1, LGPL-2.1, MIT) do not settle it: music-metadata's MIT licence covers its code, not the
commercial recordings and users' bug-report files most of its samples are. So none of it is in the
repository (decided on 2026-10-09).

**What held.** No reader threw. Of 944 Id3v2 text frames that mutagen also reads, 877 matched
exactly, and of 702 Vorbis comments, 687 did; most of the rest are mutagen merging Id3v1 into its
Id3v2 view, joining repeated frames into one, or normalising timestamps, where the prototype gives
what was recorded. Of 119 files with a duration from both, 113 agree with ffmpeg to within 50 ms.

**Shapes the corpus lacked**, each in a file mutagen reads and the prototype did not:

- **An illegal frame identifier with a good size.** A v2.3 tag whose first frame is named `Date`.
  The prototype stops at it and loses the six frames behind; mutagen steps over it by its size.
- **v2.2 frame names in a v2.3 tag**, written by iTunes 12.1.2.27: v2.3's frame layout, with
  three-letter identifiers padded by a zero byte. The prototype loses the whole tag.
- **UTF-16 without a byte order mark,** which the prototype calls undecodable and mutagen reads
  as little-endian; and **a second value inheriting the first value's byte order mark.**
- **iTunes text frames not named `T…`:** `GRP1`, `MVNM` and `MVIN`. The identifier documentation
  makes `field()` on them an error, since only `T…`, `W…`, `COMM` and `USLT` frames hold text.
- **Several Id3v2 tags in a row,** three in one file, holding different values. The prototype reads
  them all as one source; mutagen and TagLibSharp read the first.
- **A FLAC `VORBIS_COMMENT` block declaring fewer bytes than it holds,** which mutagen reads
  through and the prototype treats as a broken structure.
- **An Ogg Skeleton stream beside the audio,** an index rather than audio, which the rule declining
  multiplexed files throws out with it. Five such files, one for each codec.
- **Ogg files holding FLAC, Speex or Theora,** whose comments the prototype does not read and
  whose duration it gives as 0 rather than unusable. That last is a prototype bug.

**MP3s without a summary header are common, and all constant bitrate.** 50 of the 134 MP3s with
audio, conformance streams aside, have no Xing, Info or VBRI header. They include full songs,
Windows' sample `Sleep Away.mp3` and files written by iTunes 4.6 to Apple's Music 13.10, so Apple's
encoders evidently write constant-bitrate MP3s without one. Every one of the 50 is constant
bitrate, by a walk of its frames. That contradicts the assumption behind
[limitations](limitations.md)' entry on them, and suggests an answer that stays at the edges:

- take the bitrate of the first confirmed frame;
- confirm it at seven evenly spaced points, each a short bounded search for a frame;
- require a frame of that bitrate to end exactly where the trailing tags start;
- count the frames as the audio's length over the average frame length.

Tested against a walk of every frame, that gives the exact count for 32 of the 33 files it
accepts, and is one frame out on the 33rd, a deliberately corrupt file. It rejects 12, all of them
truncated or damaged test snippets. A variable-bitrate file without a header would still fail the
probes, as it should.

**After the decisions.** With the decisions taken on these findings built into the prototype (see
[Decisions](#decisions)), all its MP3 duration logic gathered in `Mp3Duration.cs`, it reads the
same 240 files without an exception. Tags match mutagen on 894 of 908 Id3v2 text frames. The rest
are the deferred iTunes 12.1 tag, a garbled `TLEN` that is unusable by design, and mutagen merging
repeated frames or normalising a timestamp. Durations agree with ffmpeg within 50 ms on 156 of the
159 files that both give one, and 31 MP3s without a summary header are counted from their edges.
The other 51 that ffmpeg decodes get no duration: 21 truncated test snippets whose header
describes the whole song, 14 header-less files the constant-bitrate checks reject, mostly
truncated snippets too, 5 conformance streams, 6 multiplexed Ogg files, 4 Ogg files in codecs Goro
does not read, and 1 whose LAME tag fails its CRC. Bringing the prototype up to date also found a
defect worth remembering: junk after a tag can pass for a free-format frame, whose length is only
the distance to the next sync, so a free-format frame is now confirmed by a third frame at the
same distance.

## What the hash covers

**Decided on 2026-10-08: the hash covers the codec's own stream, and nothing the container adds
around it.** The container's summaries, its trimming information and its tags are all left out.
For each format:

- **MP3:** the frames from `D` to `E` in [mp3-layout](mp3-layout.md), from the first audio frame to
  the end of the last whole frame. The Info frame is left out, LAME tag included. When the summary
  header is trusted up to an exact frame end, `E` is where it says the audio ends.
- **FLAC:** the frames, from the first to the end of the last whole frame. STREAMINFO is left out.
- **Ogg, both codecs:** every packet of the logical stream except the comment packet. That covers
  the Vorbis identification and setup headers, and `OpusHead`, and leaves out pages, granule
  positions and the comments.
- **MP4, should AAC be admitted:** the decoder configuration and the samples, in decode order.

**Why.** The first leaning was to hash the audio plus the decode parameters: values outside the
audio that change what a decoder outputs, measured below. It was given up for four reasons:

- **They are small.** Three bytes in MP3, 28 bits of STREAMINFO in FLAC, 8 bytes of granule in
  Ogg, a 28-byte edit list in MP4. Bit rot is very unlikely to land there, and nothing is lost
  that a whole frame's damage would not show anyway.
- **They are conditional.** STREAMINFO's sample rate matters only when frame headers defer to it,
  which no practical rule can follow.
- **They belong to the container, and tools rewrite them.** Rebuilding an Info frame, or fixing a
  gapless count, should not look like bit rot.
- **Picking fields means a canonical form for each format,** and a reason to keep each field. Whole
  units need neither.

**Where the codec's setup lives decides the boundary.** In MP3 and FLAC the codec's parameters
travel in every frame header, so hashing the frames covers everything a decoder needs. In Ogg
they live in header packets of their own, and "audio packets only" would leave out the Vorbis
codebooks, 3,077 bytes in the mono seed and 4,225 in a stereo file at quality 5, without which
no packet decodes: bit rot there would leave the file unplayable and its hash unchanged. Hashing
every packet but the comment covers them, stays clear of tag edits, since the comment is a packet
of its own, and picks no fields. It does take in `OpusHead`'s output gain, so editing the gain
changes the hash. MP3 behaves the same way, since gain tools rewrite each frame's own gain field
(known behaviour, not verified here).

**What this costs.** The hash no longer changes for every change in decoded output. Trimming the
end of an Ogg stream through its final granule, or rewriting MP3 gapless counts, changes what a
decoder outputs and leaves the hash as it was. [The vision](../vision.md) was reworded to match on
2026-10-08: the hash changes "if, and only if, the encoded audio stream would change as well",
where it had said the decoded audio bitstream.

### What was weighed

The decode parameters considered and set aside. Each row was tested by changing the field and
decoding with ffmpeg 9.0.2.

| Format     | Field                                            | Decode parameter | What changing it did
|------------|--------------------------------------------------|------------------|---------------------
| MP3        | LAME encoder delay and padding                   | yes              | 145,530 samples became 146,927
| MP3        | Info frame byte count                            | no               | nothing
| FLAC       | STREAMINFO sample rate, frames defer             | yes              | same samples, stream rate 100,001 Hz became 50,000 Hz
| FLAC       | STREAMINFO sample rate, frames explicit          | no               | nothing: the frame headers win
| FLAC       | STREAMINFO total samples, MD5, frame size bounds | no               | nothing
| Ogg Opus   | `OpusHead` pre-skip                              | yes              | 158,400 samples became 155,592
| Ogg Opus   | `OpusHead` output gain                           | yes              | same length, different samples
| Ogg Opus   | `OpusHead` input sample rate                     | no               | nothing
| Ogg Opus   | final granule position                           | yes              | 158,400 samples became 148,800
| Ogg Vorbis | final granule position                           | yes              | lowered by 500, 500 samples fewer; lowered by more than the last packet holds, ffmpeg ignores it
| Ogg Vorbis | identification header bitrate                    | no               | nothing
| AAC in MP4 | edit list (`elst`)                               | yes              | 145,530 samples; 146,554 with the edit list ignored

FLAC's stream parameters (sample rate, channels, sample size) matter only when frame headers
defer to STREAMINFO, but they never change under a tag edit, so including them always is
harmless. Not tested: the parameters needed to decode at all, without which there is no output to
compare (the Vorbis identification and setup headers, `OpusHead`'s channel mapping, AAC's
`AudioSpecificConfig`), and the Ogg first granule, which gives a start offset.

### AAC, as a likely later format

AAC fits the rule above, but not the byte-range thinking the MP3 hash started from. Three things were
measured on files ffmpeg wrote:

- **The audio is located by tables, not by position.** In an MP4 file the `moov` box holds the
  sample tables, whose chunk offsets are absolute positions in the file. Making the title 5,000
  characters longer moved the first chunk from byte 1,411 to 6,406, and so rewrote the table: a
  tag edit changes the bytes of the structure that locates the audio. The hash has to cover the
  samples the tables point to, in order, as for Ogg packets.
- **Trimming lives in the structure.** The edit list's media time of 1,024 is the encoder's
  priming, and decoding with it ignored gives 1,024 more samples.
- **Raw AAC (ADTS) is like MP3 without a LAME tag:** frames with headers, no trimming
  information, 147,456 samples for 145,530 of input. Id3v2 and APE tags can sit around it as they
  do around MP3.

What the model would struggle with, from the format's documentation and not tested here:

- **Gapless information inside the tags.** iTunes records priming and padding in an `iTunSMPB`
  item inside `ilst`, the tag. Under the rule above it is left out, like every other trimming
  value.
- **Tags and structure in one tree.** `ilst` lives inside `moov`, beside the sample tables, so
  the edges of an MP4 are not where its tags are. Its analysis would walk the box tree, reading box
  headers and skipping payloads, which is still bounded reading. `moov` can come before or after
  the audio, and fragmented files spread the tables across the file.
- **Several tracks.** Chapters as a text track, cover art as a video track: a multiplex, which
  Ogg's rule would decline.
- **Encrypted samples** (FairPlay), which cannot be decoded or meaningfully hashed.

## Decisions

Decided on 2026-10-08, for the three lines to write into the normative documents.

- **Goro reads all three formats itself,** tags and playing time alike.
- **No level of tag damage discards a file.**
- **What tag damage yields.** At rungs 0 to 3 a source gives whatever it holds that is usable.
  Where a tag clearly records a datum that cannot be read, the datum is an unusable occurrence:
  a value that does not decode, or the frame whose size runs past its tag. Anything else not
  found is absent, as usual, including whatever lay beyond a break in the structure.
  `PREFERRED()` takes partial data from a damaged source as it is. A later command-line option
  could raise the bar, for example to accept nothing worse than rung 1.
- **A wholly unusable tag (rung 4) is absent everywhere,** since it makes no definite claim that
  any particular datum is recorded. The file is still reported.
- **Damaged files are reported, not hidden.** From rung 3 down, a file is counted in a new summary
  warning (below). Every tag in a file is parsed whenever its tags are read, so the report does
  not depend on which sources a predicate happened to reach.
- **An Ogg page that fails its CRC** makes the comments on it unusable, with no exception for
  `bytes()`. Since the damaged bytes cannot be located within the page, the structure counts as
  broken at the start of the first failing page.
- **`bytes()`** gives a frame's content with the format's storage transformations undone:
  unsynchronisation, compression, and the prefix bytes the frame flags add (group, encryption
  method, data length). Where they cannot be undone, as for encryption or zlib that does not
  inflate, the occurrence is unusable for `bytes()` as for `field()`. Decompression is capped.
- **Playing time is the length of the stream's timeline,** as defined [above](#what-goro-needs).
- **MP3 needs a valid summary header.** Without one, or with one that fails its checks,
  `file::duration` is unusable, and the file is reported. Goro does not scan. The data warning
  this leads to is emitted as usual, and is not suppressed; the FAQ is to say that
  `file::duration IS USABLE AND …` avoids it.
- **A file whose audio cannot be found is unreadable;** one whose audio is found but whose playing
  time cannot be had has an unusable `file::duration`. "Cannot be found" includes giving up a
  bounded search, for the first frame behind a tag whose size cannot be trusted, for instance. The
  analysis describes such a file rather than failing on it, and each command decides what it
  means: `goro list` and `goro hash` treat it as unreadable, with the usual `file` warning, while
  `goro audit` reports it as a finding. `file::duration` stays exactly one; only
  `identifiers.md`'s "exactly one usable occurrence" changes. (Narrowed on 2026-10-08 from "the
  file can be opened".)
- **Multiplexed and chained Ogg files are declined,** as conservative defaults to revisit. Both
  are recorded in [limitations](limitations.md).
- **Tag values are read on demand.** Reading a file's tags indexes them: frame and item headers,
  comment lengths, where each value lies. Values, descriptions included, are read only when a
  predicate asks for them, and large binary payloads such as cover art only when `bytes()` asks.
  The index finds broken structure and unusable tags, the rungs that report the file, so the
  `incomplete` count needs no value read. A value that does not decode, rung 2, surfaces when it is
  read. A `TXXX`, `WXXX`, `COMM` or `USLT` frame whose description cannot be read might match any
  description, so it is an unusable occurrence for every description asked about.
- **The analysis reads only the edges of a file;** see the mp3-support brief for its shape. What
  it finds is exposed even where nothing uses it yet, such as whether a frame ends exactly where
  the trailing tags start. Its limits on reads have reasonable defaults, to become configurable.
- **The `incomplete` warning advertises `goro audit`,** even before audit exists.
- **APEv1 text is read as ISO-8859-1,** as Id3v1 is; recorded in
  [implementation](../implementation.md#choices-the-specifications-leave-open), and flagged by
  `goro audit`.
- **A constant-bitrate MP3 without a summary header is counted from the edges;** see
  [Real-world test data](#real-world-test-data) and
  [implementation](../implementation.md#choices-the-specifications-leave-open). Only a
  variable-bitrate one without a header has an unusable duration.
- **The first of several Id3v2 tags is the `id3v2` source;** `goro audit` reports the rest.
- **Bytes after the last MP3 frame are never hashed,** and `goro audit` reports them. A summary
  header is trusted up to an exact frame end with no frame starting there; recorded in
  [implementation](../implementation.md#choices-the-specifications-leave-open).
- **The hash covers the codec's own stream and nothing the container adds;** see
  [What the hash covers](#what-the-hash-covers).

### Warning categories

A new category reports files that were processed although part of what they hold could not be
read. It sits between `unanswered` and `file`, by how far a caller should distrust the result:

| Code | Category     | Meaning                                                                         | Emitted
|------|--------------|---------------------------------------------------------------------------------|--------
| `10` | `data`       | junk was used, whether or not an answer depended on it                          | per file and source
| `11` | `unanswered` | an answer depended on junk, and the command decided                             | once per run, a count
| `12` | `incomplete` | a file was processed although part of it could not be read                      | once per run, a count pointing to `goro audit`
| `13` | `file`       | a file was not processed at all                                                 | per file

`incomplete` ranks above `unanswered` because its failure can be silent: a key lost beyond a
broken structure is absent, so a predicate can be false without anything having been unanswered.
It ranks below `file` because the file was processed. A file can be counted in both `incomplete`
and `unanswered`. Its name covers a file that is merely old, such as an MP3 without an Info frame,
as well as a damaged one. Changing `file` from `12` to `13` touches
[exit codes](../concepts/exit-codes.md), and the rationale's "Why are there three categories of
warning?" becomes four.

## Open questions for the lines

- **All three lines:** how many files in a real collection get an unusable `file::duration` under
  these rules? Other projects' test data says many MP3s would; see
  [Real-world test data](#real-world-test-data) for a way to handle the constant-bitrate ones.
- **mp3-support, ogg-support, flac-support:** the shapes real files showed that the corpus lacked;
  see [Real-world test data](#real-world-test-data) and each brief.
