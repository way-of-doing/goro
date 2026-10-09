---
status: active
size: focused
touches: commands/hash.md, concepts/pathspecs.md, features/builtins/identifiers.md, design/rationale.md, design/quirks.md, design/flac-layout.md, architecture.md, implementation.md, testing.md, src/Goro/Reading, src/Goro/Hashing, src/Goro/Discovery, src/Goro/Predicates/Identifiers
after: file-discovery-scope
branch: line/flac-support
---
# FLAC files

## Intent

Answer every technical question that stands between FLAC and the audio hash guarantee of the
vision, that the hash changes if and only if the audio does, and then add FLAC to the files Goro
considers. Goro will not hash a format it cannot give that guarantee for, so this line is
what admits `.flac` to discovery.

## Settled

The questions this brief first asked, each answered by the [file-reading](../design/file-reading.md)
survey or by [flac-layout](../design/flac-layout.md), which measures each claim:

- **Where the audio begins:** at the end of the metadata block flagged last, `K`, confirmed by a
  frame header there; Goro walks the block chain itself, trusting no length. Every tagger edit
  tried, metaflac's and ffmpeg's, moved the frames without changing a byte of them.
- **STREAMINFO is not hashed:** the hash covers `[D, E)`, the frames from the first to the end of
  the last whole one ([What the hash covers](../design/file-reading.md#what-the-hash-covers)). No
  edit tried rewrote STREAMINFO, and metaflac has no option to.
- **Stray Id3v2, APE and Id3v1 tags** lie outside `[D, E)`: in front of `fLaC`, and behind the last
  frame. Seen in a real file, a 190 KB Id3v2 tag in front and Id3v1 behind.
- **Damaged tag data** resolves by the rules mp3-support wrote into identifiers.md; the corpus holds
  damaged comment blocks, broken block chains and the stray tags, and flac-layout lists what each
  yields from libFLAC, ffmpeg and the .NET libraries.

## Questions

From [flac-layout](../design/flac-layout.md#questions-for-flac-support), which numbers ten
questions and gives the evidence for each. Decided on 2026-10-09:

- **1 and 3. A block's recorded length wrong,** too long or too short: wherever the contents are
  recovered, by the chain walking cleanly from where they end, the file is read as though the length
  were right. Everyday commands say nothing and the file is not incomplete; `goro audit` and `goro
  explore` surface it, and quirks.md records it. A chain that is lost is a broken structure.
- **2. Bytes left over at the end of a comment block** are a broken structure.
- **4. The last frame's end** is found by parsing the frame body, not by scanning for a CRC-16 match.
- **5. Playing time.** STREAMINFO's total when the last frame confirms it; the last intact frame
  when a frame cut off by the end of the audio proves truncation, or a short final block proves a
  natural end; otherwise, with bytes and no header after the last intact frame, `file::duration` is
  unusable and the file incomplete.
- **6. The `id3v2`, `ape` and `id3v1` sources** are read in a FLAC file.
- **7. Of two VORBIS_COMMENT blocks,** the first is the `vorbis` source.
- **8. A FLAC PICTURE block** is not reached by this line. A file can hold a different picture in a
  `METADATA_BLOCK_PICTURE` comment, so the block needs syntax apart from the `vorbis` source, and
  a picture has parts the language has no type for; both move to [pictures](pictures.md).
- **9. A comment without `=`** is absent, and `goro audit` reports it as a finding.
- **10. The audio frames' CRCs** are checked by every command that reads the whole file (`goro
  hash`, `goro audit`, `goro explore`), not by `goro list`. The walk of the whole file reports each
  failure: the frame, where it is, and which check failed.

Every question is settled. `goro hash` stays silent about failing CRCs (10), and the search for the
end of a block that does not describe itself is left out (3).

## Done when

The answers are recorded in the rationale; identifiers.md says what the sources of a FLAC file
read; `goro hash` says what it hashes for a FLAC file; the pathspec documentation admits `.flac`;
and the code reads FLAC tags, playing time and the hash range with Goro's own reader. The tests
edit tags and compare hashes, and pin what damaged tag data in a FLAC file resolves to, driven by
the fixture corpus.

## Log

- 2026-10-08 -- The [file-reading](../design/file-reading.md) survey answered several of the
  questions above, and its decisions hold here too. Goro reads FLAC itself, walking the metadata
  blocks without trusting their lengths, so it finds the audio's start without TagLib. Measured
  with ffmpeg: STREAMINFO's sample rate changes the stream's rate when frame headers defer to it,
  and nothing when they do not; its total samples, MD5 and frame size bounds change nothing. The
  survey leans towards hashing the frames plus the stream parameters. Playing time is STREAMINFO
  when the last intact frame confirms it, else that frame. The damaged-data rules of mp3-support
  apply, and the corpus holds damaged comment blocks, broken block chains, and stray Id3v2, APE
  and Id3v1 tags. Still open: the heuristic telling a damaged tail from a truncation, which rests
  on two fixtures, and the canonical form of the hashed parameters.
- 2026-10-08 -- Decided in file-reading: the hash covers the frames, from the first to the end of the
  last whole frame, and STREAMINFO is left out; see
  [What the hash covers](../design/file-reading.md#what-the-hash-covers). This replaces the
  leaning towards the frames plus the stream parameters above.
- 2026-10-08 -- Real files ([file-reading](../design/file-reading.md#real-world-test-data)) include
  a `VORBIS_COMMENT` block declaring fewer bytes than it holds, an encoder bug mutagen reads
  through and the prototype treats as a broken structure. Decide whether to read through it, as a
  quirk.
- 2026-10-09 -- Picked up the line. Following mp3-support's method, wrote
  [flac-layout](../design/flac-layout.md), with a rendered version at
  [FLAC File Layout](https://claude.ai/artifact/AexqBEnddAEwPJm4cMLvSf): the specifications (RFC
  9639, the Vorbis I comment header, and the stray tags no specification allows), the positions
  `A` to `H` with `K` for where the metadata says it ends, four variations and eighteen mishaps
  `L1` to `L18`, each measured. New in scratchpad: `flac-boundaries [dir]`, which prints the
  positions for every FLAC file under a directory. What the measurements showed:
  - every tag edit tried moved `D` (from 4,496 to between 396 and 13,507) and left the frames and
    STREAMINFO byte-identical; ffmpeg's remux drops SEEKTABLE and APPLICATION blocks;
  - in a 4-minute file, delimiting frames by CRC-16 alone would end one early 252 times, while a
    search for headers by sync and CRC-8 found no chance header at all, so frames are delimited by
    the next header and its number, and only the last frame's end needs CRC-16;
  - a missing last-block flag is detected exactly, the first frame reading as block type 127; a
    block length too long makes zeroes read as 207 empty STREAMINFO blocks;
  - libFLAC refuses the whole file for every metadata damage in the corpus, ffmpeg for some;
    readers disagree on two comment blocks (first, merged, last);
  - the real-world files add a PICTURE block declaring length 0 around a whole PNG, a STREAMINFO of
    18 bytes, empty SEEKTABLE and PADDING blocks, and six truncated snippets.

  The Questions section above now lists what is open, in four groups. Next step: settle them with
  PJ, starting with groups 1 and 3, which decide what the reader's index of a FLAC file holds; then
  plan the normative documents, as mp3-support's step 1.
- 2026-10-09 -- Decided with PJ: a block declared too short is read through when its contents end
  exactly at a valid next structure, as a quirk audit reports; the last frame's end comes from
  parsing the frame body; the stray tags' sources are read, since they bear on the concepts, though
  few files will carry them; a comment without `=` is absent and an audit finding. Added a fifth
  group at PJ's question of what Goro's hash adds when FLAC checksums every frame: within one file
  the format's checks are the stronger, needing no stored hash, while Goro's hash adds a reference
  outside the file that sees any change, one mechanism across formats, and a way to compare copies.
  So the open question is whether the walk checks each frame's CRC-16, nearly free once the body is
  parsed, and how a failure is reported; the MD5 needs a decoder and stays out. Next step: the six
  questions still open, then plan the normative documents.
- 2026-10-09 -- Decided with PJ: bytes left over after a comment list are a broken structure; the
  first of two VORBIS_COMMENT blocks is the source; a PICTURE block gets syntax of its own, since
  `issue-266.flac` holds a different picture in each form and `vorbis::bytes()` must keep meaning
  comments. For question 3, measured what a wrong block length leaves readable: a block's end has
  two candidates, its recorded length and, where the block describes itself, its contents' own
  extent, and in all seven files where they differ exactly one lets the chain walk cleanly to the
  first frame. Clarified that question 10 is about the audio frames: FLAC checksums no metadata.
  Next step: questions 3, 5, 8 (the syntax) and 10, then plan the normative documents.
- 2026-10-09 -- Decided with PJ: a recorded length wrong in either direction, with the contents
  recovered, is silent for everyday commands and surfaced only by audit and explore, which extends
  decision 1; PICTURE blocks are `flac::picture()`; the audio frames' CRCs are checked by every
  command that reads the whole file, never by `goro list`, and the walk reports each failure with
  its frame, position and check. Rewrote question 5 in flac-layout with its context: two signals
  from the tail, a frame cut off by the end of the file and a short final block, settle every
  fixture but `last-frame-damaged.flac`, leaving the size rule or an unusable duration for bytes
  with no header after the last intact frame. Next step: questions 5, 10 (`goro hash`) and 3 (the
  search), then plan the normative documents.
- 2026-10-09 -- Decided with PJ, settling the last questions: `goro hash` says nothing of a frame
  failing its CRC, since counting it as incomplete would stretch that warning over a second concern;
  a playing time the tail cannot settle is unusable, junk appended to a file being far likelier than
  damage on its last frame header; the search for the end of an opaque block is left for `goro
  explore` to want. Next step: plan the line's steps, as mp3-support's were: the normative documents
  first (rationale, identifiers.md with `flac::picture()`, quirks.md, hash.md, pathspecs.md,
  implementation.md, testing.md), then the reader, sources, playing time and hash range in `src/`.
- 2026-10-10 -- Decided with PJ: `flac::picture()` is dropped from this line, accepting that a FLAC
  file's PICTURE blocks cannot be queried for now. The syntax, the type a picture is and a concept
  across formats go to a new brief, [pictures](pictures.md), which may want an earlier exploration
  of notation for mapping over values and projecting parts of compound ones. The pathspec
  documentation may admit `.flac` before the code does. The line's steps:
  1. the normative documents, and the findings in the goro-audit and goro-explore briefs;
  2. the reader: five new corpus fixtures (a damaged Id3v2 size before `fLaC`, junk before the
     marker, both picture forms in one file, a PICTURE block declaring length 0, an 18-byte
     STREAMINFO), and the FLAC edge analysis with its comment index;
  3. the `vorbis`, `id3v2`, `ape` and `id3v1` sources bound for FLAC files;
  4. `file::duration` for FLAC, its logic in one type;
  5. the hash: the walk of the whole file with its CRC report, FLAC hashing, and `.flac` admitted
     to discovery.

  Next step: step 1.
- 2026-10-10 -- Step 1 written, awaiting review:
  - identifiers.md: in a FLAC file the `vorbis` source is the first comment block; a comment
    without `=` is absent; bytes after the last comment break the list off; `file::duration` is
    unusable where a FLAC file's tail cannot settle its playing time;
  - quirks.md: a block whose recorded length is wrong, read from its contents when the chain then
    reads cleanly to the first frame; a chain with no block flagged last;
  - hash.md: what a FLAC file's hash covers; pathspecs.md admits `.flac`, ahead of the code;
  - rationale.md: the FLAC playing-time rule, added to the timeline entry; why a recovered block
    length is silent; why a comment without `=` is absent where an undecodable Id3v2 description is
    unusable; why stray tags are read; why `goro hash` says nothing of failing frame CRCs;
  - implementation.md: reading a FLAC file from its edges, finding where its audio ends and
    checking its frames, and its playing time among the choices the specifications leave open;
  - architecture.md: the walk checks the frames' checksums;
  - testing.md: FLAC tables for tags and damage, playing time and the hash range, and `c.flac`
    among the pathspec scenarios;
  - the goro-audit and goro-explore briefs: the FLAC findings, and the search left to explore.

  Two calls of mine, by the rule that a fault losing nothing is silent: a chain with no block flagged
  last is not counted as incomplete, being read in full and its end found exactly; and an 18-byte
  STREAMINFO is read for the fields it holds, its total among them. PJ agreed both. The new
  briefs, [pictures](pictures.md) and [value-mapping](value-mapping.md), the notation for
  projections that pictures needs, land with this line. Next step: step 2, the reader, in plan mode.
- 2026-10-10 -- The steps revised: `FileLayout` carries the playing time, decided in every analysis,
  so the reader is not whole without it, and the old step 4 joins step 2. Four steps remain: 2 the
  reader with its duration, 3 the sources and the loader, 4 the hash. Step 2 is split for review:
  2a the parsing pieces, 2b the analysis that composes them. 2a written, awaiting review:
  - six corpus fixtures, the existing ones regenerated byte for byte: `dmg-id3v2-size-short.flac`,
    `id3v2-junk-before-marker.flac`, `dmg-application-length-overrun.flac`, and three mirroring
    real files, `rw-picture-block-and-comment.flac`, `rw-picture-block-length-zero.flac`,
    `rw-streaminfo-short.flac`;
  - `Reading/Flac/`: `FlacFrameHeader`; `FlacFrameBody`, which finds a frame's end by walking its
    subframes and residual to the bit; `FlacMetadata`, the chain with the two-candidate rule, the no
    last flag quirk and lost chains, and STREAMINFO, 18 bytes or 34;
  - `Tags/VorbisCommentIndex`: the comment list as its own lengths describe it, then indexed within
    the block's decided region. A comment without `=` is absent and a finding; one running past its
    block is unusable and breaks the list off; bytes after the last comment break it off. A name
    that does not decode is unusable for every name asked about, as an undecodable Id3v2
    description is (my call, for review). A value is one UTF-8 string, NULs and all;
  - `Binary` gains FLAC's CRC-8 and CRC-16; `TagFormat.Vorbis`, `FieldForm.VorbisComment` and the
    FLAC conditions (`FlacQuirkApplied`, `MetadataChainLost`, `CommentWithoutName`,
    `StreamInfoShort`) are new.

  Checked beyond the suite, with a throwaway test: the body walk lands exactly on every frame of
  the 38 real-world files and of a 4-minute stereo file (10,336 frames, LPC and mid/side), no CRC
  failing, and stops at the cut frame of each truncated snippet. Next step: review of 2a, then 2b.
- 2026-10-10 -- From review of 2a: the CRC tables are constants computed offline rather than built
  at run time, and the checksums have tests of their own. They moved from `Binary`, which is
  internal, to a public `Checksums`, the LAME tag's CRC-16/ARC with them, since the tests reach
  only public types. 2a committed; next step: 2b, the analysis.
