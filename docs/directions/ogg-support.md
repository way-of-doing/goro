---
status: proposed
size: broad
touches: commands/hash.md, concepts/pathspecs.md, design/rationale.md, testing.md, src/Goro/Hashing, src/Goro/Discovery
after: file-discovery-scope
branch:
---
# Ogg Vorbis and Ogg Opus files

## Intent

Answer every technical question that stands between Ogg Vorbis and Ogg Opus and the audio hash
guarantee of the vision, that the hash changes if and only if the audio does, and then add `.ogg`
and `.opus` to the files Goro considers. Goro will not hash a format it cannot give that guarantee
for, so this line is what admits them to discovery.

## Questions

What is known so far comes from `scratchpad/Goro.Scratchpad/FormatSupport/HashStabilityProbe.cs`
and the 2026-10-05 entry in the [file-discovery-scope](file-discovery-scope.md) Log. TagLibSharp
2.3.0 reads both extensions through `Ogg.File` and takes the codec from the stream, so Opus in a
`.ogg` and Vorbis in a `.opus` read correctly. The range Goro hashes today is stable while a tag
edit leaves the comment on the pages it already had. Once the comment needs more pages, as
embedded cover art easily does, TagLib renumbers every later page, and each page's sequence
number and CRC are inside the range.

- What is hashed, if not bytes in place: the packets of the logical stream rather than its pages,
  it would seem, since page boundaries, sequence numbers and CRCs are all free to change under a
  tag edit while the packets are not.
- Which header packets belong in the hash. The comment packet must not. The Vorbis identification
  and setup headers, and `OpusHead`, are needed to decode at all. `OpusHead` also carries pre-skip
  and output gain, which change the decoded output, and loudness tools are known to edit the gain.
- Whether granule positions belong in it. The last one trims the end of the stream, so it changes
  the decoded output.
- Chained and multiplexed files: several logical streams one after another, or interleaved. What
  TagLib reads in each case, and whether Goro hashes every stream or declines the file.
- How the packets are read: through TagLib's Ogg classes, if they expose enough, or by a reader
  of our own.

## Done when

The answers are recorded in the rationale, `goro hash` says what it hashes for an Ogg file, the
pathspec documentation admits `.ogg` and `.opus`, and the code hashes both codecs, under either
extension, to a value that survives tag edits, with tests that edit tags and compare hashes as the
probe does.

## Log
