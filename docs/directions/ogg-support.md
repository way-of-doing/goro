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
- Damaged tag data, which is definitely part of this line. Whatever
  [mp3-support](mp3-support.md) settles about what a damaged tag resolves to has to
  hold for Ogg too: repeat its probe on damaged comment packets, for both codecs and including a
  comment spread over several pages, and confirm that each yields what the specification says.

## Done when

The answers are recorded in the rationale, `goro hash` says what it hashes for an Ogg file, the
pathspec documentation admits `.ogg` and `.opus`, and the code hashes both codecs, under either
extension, to a value that survives tag edits, with tests that edit tags and compare hashes as the
probe does, and with tests that pin what damaged tag data in an Ogg file resolves to.

## Log

- 2026-10-08 -- The [file-reading](../design/file-reading.md) survey answered several of the
  questions above, and its decisions hold here too. Goro reads Ogg itself, with no TagLib classes.
  Chained and multiplexed files are declined (see [limitations](../design/limitations.md)); what
  declining means operationally, presumably an unreadable file, is still to settle. Measured with
  ffmpeg: `OpusHead` pre-skip and output gain, and the final granule of both codecs, change the
  decoded output, while Opus's input sample rate and Vorbis's bitrate fields do not. The survey
  leans towards hashing packets plus such decode parameters, for every format. A comment on a page
  failing its CRC is unusable, the structure counting as broken from that page. The damaged-data
  rules of mp3-support apply, and the corpus already holds damaged comment packets for both
  codecs, a multipage comment included. Still open: the Vorbis start offset, which the prototype
  assumes to be 0, and the canonical form of the hashed parameters.
- 2026-10-08 -- Decided in file-reading: the hash covers every packet of the logical stream except
  the comment packet, so the identification and setup headers and `OpusHead` are in, and pages,
  granule positions and comments are out; see
  [What the hash covers](../design/file-reading.md#what-the-hash-covers). This replaces the
  leaning towards decode parameters above. Still open: how packet boundaries enter the hash, since
  concatenated packets would not show a damaged lacing value that moves one.
- 2026-10-08 -- Real files ([file-reading](../design/file-reading.md#real-world-test-data)) add two
  questions. Ogg Skeleton, an index stream beside the audio, makes five test files multiplexed, so
  declining multiplexed files throws them out; should Skeleton be ignored as not audio? And `.ogg`
  files can hold FLAC, Speex or Theora: declined as audio Goro does not read, presumably, and the
  prototype gives them 0 s rather than an unusable duration, a bug to avoid.
