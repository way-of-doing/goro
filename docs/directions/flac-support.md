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
  [mp3-support](mp3-support.md) settles about what a damaged tag resolves to has to
  hold for FLAC too: repeat its probe on damaged Vorbis comment blocks, on other damaged metadata
  blocks, and on the stray Id3v2 and APE tags above, and confirm that each yields what the
  specification says.

## Done when

The answers are recorded in the rationale, `goro hash` says what it hashes for a FLAC file, the
pathspec documentation admits `.flac`, and the code hashes FLAC files to a value that survives
tag edits, with tests that edit tags and compare hashes as the probe does, and with tests that
pin what damaged tag data in a FLAC file resolves to.

## Log
