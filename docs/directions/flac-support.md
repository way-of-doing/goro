---
status: proposed
size: focused
touches: commands/hash.md, concepts/pathspecs.md, design/rationale.md, testing.md, src/Goro/Hashing, src/Goro/Discovery
after: file-discovery-scope
branch:
---
# FLAC files

## Intent

Answer every technical question that stands between FLAC and the audio hash guarantee of the
vision, that the hash changes if and only if the audio does, and then add FLAC to the files Goro
considers. Goro will not hash a format it cannot give that guarantee for, so this line is
what admits `.flac` to discovery.

## Questions

What is known so far comes from `scratchpad/Goro.Scratchpad/FormatSupport/HashStabilityProbe.cs`
and the 2026-10-05 entry in the [file-discovery-scope](file-discovery-scope.md) Log. TagLibSharp
2.3.0 reads FLAC through `Flac.File`, but reports `InvariantStartPosition` as 0, so the range
Goro hashes today takes in every metadata block and changes with every tag edit.

- Where the audio begins: after the metadata block that carries the last-block flag, it would
  seem, which Goro would have to find itself since TagLib does not report it. Confirm that nothing
  a tagger writes can land after that point, and that padding is always a metadata block.
- Whether `STREAMINFO` belongs in the hash. It is metadata, but a decoder needs it whenever a
  frame header defers to it for sample rate or sample size, and it carries the MD5 of the
  decoded audio. Does any tool rewrite it while leaving the audio alone?
- An Id3v2 tag before `fLaC`, and an Id3v1 or APE tag after the last frame, are not standard but
  exist in the wild. Confirm the boundaries hold with them present.
- How the boundary is found: by parsing the metadata block chain ourselves, or through anything
  TagLib exposes.
- Damaged tag data, which is definitely part of this line. Whatever
  [mp3-support](archive/mp3-support.md) settles about what a damaged tag resolves to has to
  hold for FLAC too: repeat its probe on damaged Vorbis comment blocks, on other damaged metadata
  blocks, and on the stray Id3v2 and APE tags above, and confirm that each yields what the
  specification says.

## Done when

The answers are recorded in the rationale, `goro hash` says what it hashes for a FLAC file, the
pathspec documentation admits `.flac`, and the code hashes FLAC files to a value that survives
tag edits, with tests that edit tags and compare hashes as the probe does, and with tests that
pin what damaged tag data in a FLAC file resolves to.

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
