# The layout of a FLAC file

This is working material for [flac-support](../directions/archive/flac-support.md), made as
[mp3-layout](mp3-layout.md) was for MP3: how a FLAC file is laid out, where each kind of tag sits,
and what goes wrong at each edge. Every mishap is tied to a lettered position in the diagrams, and
most to a file in the fixture corpus (`tests/Goro.Tests/Fixtures/Audio/flac`) or in the real-world
test data of [file-reading](file-reading.md#real-world-test-data). Mishaps are numbered `L1` on,
L for `fLaC`, so that they never read as MP3's `M1` on.

A rendered version of the diagrams, with each mishap marked on its map, is at
[FLAC File Layout](https://claude.ai/artifact/AexqBEnddAEwPJm4cMLvSf); this file is the source.

Positions measured on the corpus come from
`dotnet run -c Release --project scratchpad/Goro.Scratchpad -- flac-boundaries [dir]`, which prints
each position below for every FLAC file under the directory, beside TagLibSharp's invariant range.
Decoders' behaviour is from libFLAC 1.5.0 (`flac -t`, `metaflac`) and ffmpeg 9.0.2.

## The specifications

- **[RFC 9639](https://www.rfc-editor.org/rfc/rfc9639)** (December 2024), the FLAC format:
  section 6 for the layout, 8 for the metadata blocks, 9 for frames, 11 for what a reader must
  survive. It replaces the format page on xiph.org.
- **The [Vorbis I comment header](https://xiph.org/vorbis/doc/v-comment.html).** RFC 9639
  section 8.6 restates it for FLAC, with three differences. The comment block has no framing bit.
  Its lengths are bounded by the block's own, at most 2<sup>24</sup> bytes. And a field name may use
  printable ASCII from `0x20` to `0x7E` except `=`, where the Vorbis document stops at `0x7D`, so
  `~` is legal in a FLAC name and not in a Vorbis one.
- **Id3v2, APE and Id3v1** say nothing of FLAC, and RFC 9639 leaves them no room: a stream is
  `fLaC`, the metadata blocks, then frames. Files carry them anyway. libFLAC skips an Id3v2 tag in
  front with a warning ("non-standard and strongly discouraged"), and says of 128 bytes behind the
  audio that start with `TAG` that they probably caused the error it reports.

FLAC inside Ogg, Matroska or MP4 (RFC 9639 section 10) is not this document's concern.

## The anatomy

A FLAC file is the marker `fLaC`, a chain of metadata blocks, then audio frames. Each block starts
with a 4-byte header: a flag saying whether it is the last block, a type, and a 24-bit length. The
first block is STREAMINFO. The tags are blocks too: VORBIS_COMMENT for text, PICTURE for images.

```
  A          B         C      C+4                                     D                                      E          F          H
  │          │         │      │                                       │                                      │          │          │
  ┌──────────┬─────────┬──────┬───────────────────────────────────────┬──────────────────────────────────────┬──────────┬──────────┐
  │ Id3v2    │   gap   │ fLaC │ metadata blocks                       │ audio frames                         │   gap    │ APE,     │
  │ (stray)  │         │      │ STREAMINFO · … · the last-flagged one │                                      │          │ Id3v1    │
  └──────────┴─────────┴──────┴───────────────────────────────────────┴──────────────────────────────────────┴──────────┴──────────┘
                                                                      K
```

| Position | Where                                                                    | Found by
|----------|--------------------------------------------------------------------------|----------
| `A`      | the start of the file                                                    | -
| `B`      | where the leading Id3v2 tags say they end                                | reading each tag's header, from `A` onwards
| `C`      | the `fLaC` marker                                                        | looking at `B`
| `K`      | where the metadata says it ends: the end of the block flagged last       | walking the block headers from `C` + 4, trusting each length
| `D`      | the first frame                                                          | a frame header at `K`: the sync code, a passing CRC-8, frame or sample number 0
| `E`      | the end of the last whole frame                                          | walking frame to frame from `D`; the last frame's own body says where it ends
| `F`      | where the trailing tags start                                            | reading each tag's footer, from `H` inwards
| `H`      | the end of the file                                                      | -

In a healthy file without stray tags, `A = B = C`, `K = D` and `E = F = H`. `G`, as in MP3, is the
start of an Id3v1 tag, always `H` minus 128.

Four things set FLAC apart from MP3, and most of what follows comes from one of them:

- **The metadata declares where it ends.** In a healthy file nothing is searched for: `K` comes
  from the block chain, and `D = K` is confirmed by a frame header there. MP3 has to search for
  its first frame in every file.
- **The tags live inside the format, ahead of the audio.** A tag edit grows into PADDING or, when
  it does not fit, rewrites the file and moves the frames. Tried with metaflac (a small edit, a
  9 KB comment, removing the padding, adding seek points, `--remove-all`), with an ffmpeg remux and
  retag, and with a full decode and re-encode: `D` moved from 4,496 to anywhere between 396 and
  13,507, while the bytes from `D` to the end, and STREAMINFO, stayed identical in every case.
  ffmpeg's remux also drops SEEKTABLE and APPLICATION blocks, which no hash notices and
  `goro audit` could.
- **Frames do not declare their length.** A frame ends where the next one starts, found by its
  sync code, its CRC-8 and its number: the frame number one higher, or, in a stream of variable
  block size, the sample number one block later. Only the last frame has no next header, and
  only its own body, read to the bit, says where it ends; its CRC-16 then confirms it.
  CRC-16 alone is no delimiter: in a 4-minute, 14.6 MB file of 10,336 frames, the running CRC-16
  matches the next two bytes at 252 positions before a frame's true end, about one in every 65,536
  bytes as chance predicts. A search for headers, by contrast, found no chance header passing its
  CRC-8 anywhere in the audio.
- **The audio is checksummed and the metadata is not.** Each frame header has a CRC-8, each frame a
  CRC-16, and STREAMINFO an MD5 of the decoded audio, which only a decoder can check. No checksum
  covers a metadata block, so damage to a tag can be silent, rung 5 of
  [file-reading](file-reading.md#tags), exactly as in MP3.

**The hash range** that TagLibSharp gives is `[C, F)` on every corpus file it reads: the metadata
and any garbage after the audio inside it, so that every tag edit would change the hash.
[file-reading](file-reading.md#what-the-hash-covers) decided `[D, E)`.

## Variation 1: what an encoder writes

flac 1.5.0 writes STREAMINFO, SEEKTABLE, a VORBIS_COMMENT holding only its vendor string, and
8 KiB of PADDING (`seeds/flac.flac`). Encoding from a pipe it cannot seek back in, it leaves
SEEKTABLE out and STREAMINFO's total samples and MD5 zero (`seeds/flac-piped.flac`).

```
  A=B=C   C+4                                         K=D                                                        E=F=H
  │       │                                           │                                                          │
  ┌───────┬───────────────────────────────────────────┬──────────────────────────────────────────────────────────┐
  │ fLaC  │ STREAMINFO · SEEKTABLE · VORBIS_COMMENT · │ audio frames                                             │
  │       │ PADDING (last)                            │                                                          │
  └───────┴───────────────────────────────────────────┴──────────────────────────────────────────────────────────┘
```

- **L1. Damage inside the frames,** between `D` and `E` (`zeroed-4k.flac`). The walk loses the
  numbering at the zeroed block and resynchronises on the next frame number it finds, so the
  damaged frames stay inside `[D, E)` and the hash changes. libFLAC aborts decoding there; ffmpeg
  decodes 2.928 s of 3.300 s.
- **L2. Truncation** inside a frame (`truncated.flac`: `E` 26,868, `H` 27,961). The partial frame
  has a good header and no matching CRC-16, and is left out. The audio changed, so the hash must
  change, and does. Of the 38 real-world FLAC files, six are snippets cut this way from whole
  songs, their STREAMINFO still counting millions of samples.
- **L3. Garbage appended,** with no tag (`garbage-appended.flac`: `E` 46,603, `H` 49,603). The last
  frame's CRC-16 does not match at `F`, so its end has to be found by looking for where it does.
  Between the true end and that frame's start, a false match is about as likely as the frame is
  long over 65,536: one file in fifty for a frame of 1.3 KB. Parsing the frame body finds the end
  exactly instead. libFLAC reports lost sync, ffmpeg four errors.
- **L4. The last frame's header damaged** (`last-frame-damaged.flac`: `E` 45,961, `F` 46,603). With
  its CRC-8 failing, it is not a frame, and the bytes from the previous frame's end to `F` look just
  like L3's garbage. Leaving them out changes the hash, as damage should.
- **L5. STREAMINFO wrong**: its total samples zero (`streaminfo-total-zero.flac`) or doubled
  (`streaminfo-total-doubled.flac`). Outside the hash, so only the playing time is at stake;
  TagLibSharp, TagLibSharp2 and ATL report 0 s and 6.6 s.
- **L6. STREAMINFO malformed:** 18 bytes where it must be 34, every field there but the MD5
  (mutagen's `106-invalid-streaminfo.flac`). libFLAC refuses the metadata altogether. The chain is
  intact and the frames are at `K`, so the audio is found, and the total is there to be read.
- **L7. No audio at all.** A file that ends inside its metadata or just after it (mutagen's
  `ooming-header.flac` and `52-overwritten-metadata.flac`), or 82 bytes into its first frame
  (`flac_application.flac`). Unreadable, by the rule for every format.

## Variation 2: tagged

What a tagger leaves: STREAMINFO, SEEKTABLE, VORBIS_COMMENT, APPLICATION, PICTURE and PADDING
(`comments.flac`, `D` 4,496 of 46,603). Real files put PICTURE before or after VORBIS_COMMENT, and
one of the real-world files holds two pictures.

```
  A=B=C   C+4         V                         V2                                 K=D                       E=F=H
  │       │           │                         │                                  │                         │
  ┌───────┬───────────┬─────────────────────────┬────────────┬─────────┬───────────┬─────────────────────────┐
  │ fLaC  │STREAMINFO │ VORBIS_COMMENT          │ APPLICATION│ PICTURE │ PADDING   │ audio frames            │
  │       │SEEKTABLE  │ vendor · count · fields │            │         │ (last)    │                         │
  └───────┴───────────┴─────────────────────────┴────────────┴─────────┴───────────┴─────────────────────────┘
```

`V` is where the comment block starts and `V2` where its declared length says it ends. Inside it,
the comments say their own extent: a vendor length, a count, and a length for each field. That
gives every block of a FLAC file two descriptions of its end, the chain's and its contents', and
most of what goes wrong in the metadata is the two disagreeing.

- **L8. A block's length too long** (`dmg-block-length-overrun.flac`, 200 bytes too many). The next
  block header is read from inside the blocks that follow, and four zero bytes read as an empty
  STREAMINFO: 207 of them, out of the comment, application, picture and padding data, before the
  first frame reads as a block of type 127 and the chain breaks. `K` is lost; `D` is 1,259, found by
  searching for the first frame. libFLAC and ffmpeg refuse the file.
- **L9. A block's length too short**: a VORBIS_COMMENT declaring 48 bytes and holding 147
  (`rw-comment-block-too-short.flac`: `K` 94, `D` 193), from an encoder bug mutagen reads through;
  and a PICTURE declaring 0 bytes around a whole PNG (mutagen's `106-short-picture-block-size.flac`).
  The contents end where a valid structure begins: the first frame in the first case, garbage
  read as blocks in the second, until the search finds the frames at 4,186. libFLAC refuses both
  files; TagLibSharp throws on both.
- **L10. No block flagged last** (`dmg-no-last-block-flag.flac`). The walk reaches the first frame
  and reads its sync code, `FF F8`, as a block header: last-flagged, of type 127. RFC 9639 forbids
  type 127 for exactly this reason, so the mishap is recognised where it happens, at 491, and the
  frame is there. libFLAC refuses the file; ffmpeg reads the tags and decodes nothing.
- **L11. A comment's length running past its block** (`dmg-comment-length-overrun.flac`). The
  comments before it are intact. libFLAC refuses the whole metadata chain over it, ffmpeg gives the
  first two comments, and TagLibSharp throws.
- **L12. A block of zeroes inside the comments** (`dmg-zeroed-comment.flac`). It shows two ways.
  The lengths it overwrites read as 0, so the count of 10 is honoured with nine empty comments,
  which have no `=` and so no name; the one before them is a title holding `Tit` and six NUL bytes,
  valid UTF-8. And the comment list then ends at byte 74 of a block of 185, leaving 111 bytes
  unread, which hold what is left of the real comments. metaflac lists the ten comments as read;
  `flac -t` calls the metadata bad.
- **L13. Values that do not decode, or name nothing** (`dmg-comment-values.flac`): Latin-1 in a
  UTF-8 value, a comment without `=`, and a name holding `~`, which RFC 9639 allows. metaflac
  prints `Mot#rhead`; ffmpeg, TagLibSharp, TagLibSharp2 and ATL give `Mot�rhead`.
- **L14. A false frame header inside the metadata,** when `D` has to be searched for, as in L8 and
  L9. A picture can hold `FF F8`, and a header also needs a passing CRC-8, frame number 0 and a
  confirming second frame, which makes a false match unlikely but not impossible. Not seen.

## Variation 3: tags that do not belong

An Id3v2 tag in front, and behind the audio an APEv2 tag, an Id3v1 tag, or both
(`id3v2-prefix.flac`, `id3v1-suffix.flac`, `ape-suffix.flac`). Among the real-world files,
`a kind of magic.flac` carries a 190 KB Id3v2 tag, cover art included, in front of `fLaC`, and an
Id3v1 tag behind.

```
  A          B=C    C+4                       K=D                                          E=F        G          H
  │          │      │                         │                                            │          │          │
  ┌──────────┬──────┬─────────────────────────┬────────────────────────────────────────────┬──────────┬──────────┐
  │ Id3v2    │ fLaC │ metadata blocks         │ audio frames                               │ APEv2    │ Id3v1    │
  │          │      │                         │                                            │          │          │
  └──────────┴──────┴─────────────────────────┴────────────────────────────────────────────┴──────────┴──────────┘
```

- **L15. The Id3v2 size damaged**: too short, not syncsafe, or past the end of the file, as M8 to
  M10 in MP3. Here the search has a far better anchor than a frame sync: the four bytes `fLaC`
  followed by a STREAMINFO header, `00 00 00 22` or `80 00 00 22`. Not in the corpus.
- **L16. Junk between the tag and the marker,** `B < C`: padding beyond the tag's declared size, as
  M7 in MP3. The same search finds `C`. Not in the corpus.
- **L17. A trailing tag damaged or unrecognised:** its bytes lie between `E` and `F`, and are L3's
  garbage by another name.
- **L18. A false trailing tag,** `TAG` at `H` minus 128 inside the last frame, about one file in
  16.7 million. FLAC can tell where MP3 cannot: if the last frame's CRC-16 matches at `H`, the
  bytes are audio. The same check settles a false APE footer.

TagLibSharp finds all three, its range starting after the Id3v2 tag and ending before the trailing
ones. TagLibSharp2 takes the Id3v2-prefixed file for an MP3, losing its comments and its playing
time.

## Variation 4: the metadata in unusual arrangements

Each of these is well formed, or nearly, and has to be read rather than survived.

- **Two VORBIS_COMMENT blocks,** which RFC 9639 forbids (`two-comment-blocks.flac`, TagLib's
  `multiple-vc.flac`). Readers disagree: libFLAC's `metaflac --export-tags-to` and TagLibSharp read
  the first, ffmpeg and ATL merge the two, and TagLibSharp2 reads the last.
- **VORBIS_COMMENT last, after PADDING** (`comment-block-last.flac`). Legal; the order of blocks
  after STREAMINFO is free.
- **Empty blocks:** a SEEKTABLE of length 0 (TagLib's `empty-seektable.flac`) and a PADDING of length
  0 (`zero-sized-padding.flac`). Legal.
- **Several APPLICATION blocks, a CUESHEET** (mutagen's `flac_application.flac`,
  `silence-44-s.flac`). Stepped over; so would be the reserved types 7 to 126, none seen.
- **A second STREAMINFO,** forbidden, and in practice the sign of L8: a chain read from the wrong
  place.

## What the edges could be

**The start:**

| Start | What it means                                         | Mishaps that change the hash when they should not
|-------|-------------------------------------------------------|---------------------------------------------------
| `C`   | the marker (TagLibSharp)                              | every tag edit: the metadata is hashed
| `K`   | where the metadata says it ends                       | none in a healthy file; L8, L9: undefined or wrong
| `D`   | the first frame                                       | none

**The end:**

| End   | What it means                                         | Mishaps that change the hash when they should not
|-------|-------------------------------------------------------|---------------------------------------------------
| `F`   | before the trailing tags, as found                    | L3, L17: garbage and unrecognised tags are hashed
| `E`   | after the last whole frame                            | none, once the last frame's body is parsed; by a CRC-16 scan alone, L3 in about one file in fifty

[file-reading](file-reading.md#what-the-hash-covers) decided `[D, E)` for FLAC as for MP3, before
this document existed, and nothing here argues against it. What remains is how `D` and `E` are
found:

- **`D` is `K`** whenever a frame header with number 0 is there, which is every healthy file and
  L10. Otherwise it is searched for from `C` + 4, a frame being confirmed by a second one.
- **`E` costs `goro hash` nothing,** as for MP3: the frames are read anyway, and each is delimited
  by the next header. Only the last frame's end needs more than a header, and only when something
  follows it: its body is parsed, as decided below.

## Questions for flac-support

Grouped by what causes them.

**The two descriptions of a block's end** (L8, L9, L12). The chain says where each block ends, and
the block's contents say so again.

1. When a block's length is too short and its contents run on to end exactly where a valid next
   structure starts, a frame or a block header, is the block read through? That is what mutagen
   does for the comment block (the quirk the brief already asks about), and its picture case is the
   same shape. Reading through needs a rule for when the contents are believable.

   **Decided on 2026-10-09: yes,** in exactly that case. It is a quirk, recorded in
   [quirks](quirks.md) and reported by `goro audit`.
2. Bytes left over at the end of a comment block (L12). In `dmg-zeroed-comment.flac` the zeroes
   overwrote the lengths of comments 2 to 10, which therefore read as 0, giving nine empty comments
   (absent, by the decision on question 9). What is left of the real comments lies unread in the
   111 bytes between the end of the comment list and the end of the block. Those leftover bytes are
   the only sign that anything was lost. Are they a broken structure, counting the file as
   incomplete, or ignored? No comment block in the 38 real-world files has any bytes left over;
   only damaged corpus files do.

   **Decided on 2026-10-09: a broken structure.**
3. What can be read when a block's recorded length is wrong (L8, L9, and L6 for comparison). A
   block's end has two candidates: where its recorded length puts it, and, for a block whose
   contents describe their own extent, where the contents end. STREAMINFO is always 34 bytes; a
   VORBIS_COMMENT gives a vendor length, a count and a length for each comment; a PICTURE gives a
   length for its media type, its description and its data. PADDING, APPLICATION and SEEKTABLE
   say nothing of their own extent. The right candidate is the one from which the rest of the
   chain walks cleanly, every later block well formed and the last-flagged one ending exactly at a
   frame header with a passing CRC-8 and number 0, a check chance is very unlikely to pass.
   Measured on every file in the corpus and the real-world data whose candidates differ, exactly
   one of the two passes:

   | File                                 | Block          | Recorded end             | Contents' end            | Right one
   |--------------------------------------|----------------|--------------------------|--------------------------|-----------
   | `dmg-block-length-overrun.flac`      | VORBIS_COMMENT | 431, nothing valid there | 231, chain to the frames | contents
   | `rw-comment-block-too-short.flac`    | VORBIS_COMMENT | 94, nothing valid        | 193, the first frame     | contents
   | `52-too-short-block-size.flac`       | VORBIS_COMMENT | 94, nothing valid        | 221, the first frame     | contents
   | `106-short-picture-block-size.flac`  | PICTURE        | 90, nothing valid        | 222, chain to the frames | contents
   | `dmg-zeroed-comment.flac`            | VORBIS_COMMENT | 253, chain to the frames | 142, nothing valid       | recorded
   | `dmg-comment-length-overrun.flac`    | VORBIS_COMMENT | 231, chain to the frames | past the end of the file | recorded
   | `106-invalid-streaminfo.flac`        | STREAMINFO     | 26, chain to the frames  | 42, nothing valid        | recorded

   What that means for what is read correctly:

   - **The contents are right and the length wrong,** too long or too short, in a block that
     describes itself: everything. The block's own data, every block after it, and `K`. Only the
     length was wrong. Decision 1 covers the too-short case; the too-long case is its mirror.
   - **The length is right and the contents damaged:** every block, `K` included, and the damaged
     block up to its damage. That is question 2 for a comment list ending early (decided: a broken
     structure), L11 for one running past its block (the comments before the overrun are read),
     and L6 for STREAMINFO, whose 18 bytes hold every field but the MD5.
   - **A block that does not describe itself, with a wrong length:** no second candidate. The
     blocks before it are read correctly; the rest of the chain is lost, and `D` is found by
     searching for the first frame. The audio, and so the hash, is unaffected. Not seen in the
     corpus or the real-world data. A search from the damaged block for a position from which the
     chain walks cleanly to the frames could recover the blocks after it, a PICTURE among them.
   - **Neither candidate passes:** as the previous case, from the damaged block on.

   **Decided on 2026-10-09:** wherever the contents are recovered, the recorded length too long or
   too short, the file is read as though the length were right. Everyday commands say nothing, and
   the file is not counted as incomplete; only `goro audit` and `goro explore` surface it. This
   extends decision 1 to both directions. A file whose chain is lost is incomplete, a broken
   structure, even when its comments were read before the break. A search for the end of a block
   that does not describe itself is left out: nothing Goro reads needs those blocks, and
   `goro explore` may want one later.

**Frames without lengths** (L2, L3, L4).

4. How the last frame's end is found when it is not at `F`. A CRC-16 match found by scanning is
   wrong about one time in fifty for a frame of 1.3 KB. Reading the subframes' own structure, their
   headers and Rice-coded residuals, finds the end to the bit with no decoding of samples, at the
   cost of a parser for the frame body. Or the scan's first match is kept as good enough for a case
   that needs junk after the audio to arise.

   **Decided on 2026-10-09: the frame body is parsed.**
5. Playing time when the frames fall short of STREAMINFO's total.

   `file::duration` is the length of the stream's timeline: damage in the middle does not shorten
   it, and truncation does ([file-reading](file-reading.md#what-goro-needs)). A FLAC file states the
   timeline twice. STREAMINFO declares the total samples, which may be 0 for unknown and may be
   wrong. And every frame header gives its frame number and block size, so the last frame's header
   says exactly where the stream ends: in this audio, frame 35 of 4,096-sample blocks with a final
   block of 2,170 gives 35 × 4,096 + 2,170 = 145,530 samples. The analysis reads only the edges,
   so it finds the last frames by searching back from `F` for headers with a passing CRC-8.

   When the last frame's end and STREAMINFO agree, that is the answer. When they disagree, the
   frames ending early means one of two things, which call for different answers:

   - **truncation:** the file was cut, the samples are gone, and the timeline is shorter;
   - **damage at the end:** the last frames are there but unreadable, so the search back from `F`
     stops at an earlier frame, and the timeline is STREAMINFO's.

   The survey's rule tells them apart by size. If the bytes after the last intact frame are at
   least half what the missing samples would take at the file's average rate, the tail is damaged
   and STREAMINFO stands; otherwise it is a truncation. That threshold was set on two fixtures,
   and a byte rate varies with the music: silence encodes to a few bytes a frame, noise to
   kilobytes.

   The tail usually shows which it is without measuring anything, now that frame bodies are
   parsed (decision 4):

   | File                            | STREAMINFO | What the tail holds                                                          | Size rule  | Structure says
   |---------------------------------|------------|------------------------------------------------------------------------------|------------|----------------
   | `comments.flac`                 | 145,530    | frame 35, a 2,170-sample block, ends exactly at `F`                          | 3.300 s    | 3.300 s, confirmed
   | `streaminfo-total-zero.flac`    | 0          | the same                                                                     | 3.300 s    | 3.300 s: a short final block can only be the last
   | `streaminfo-total-doubled.flac` | 291,060    | the same                                                                     | 3.300 s    | 3.300 s: the same
   | `truncated.flac`                | 145,530    | frame 19's header intact, its body running past the end of the file          | 1.765 s    | 1.765 s: a frame cut off is a truncation
   | `last-frame-damaged.flac`       | 145,530    | frame 34 ends at 45,961; then 642 bytes with no valid header up to `F`       | 3.300 s    | cannot tell
   | `garbage-appended.flac`         | 145,530    | frame 35 ends at 46,603; then 3,000 bytes of junk                            | 3.300 s    | 3.300 s, confirmed

   Two signals settle most files. A frame whose header is intact but whose body runs past the end
   of the audio is proof of truncation. And in a stream of fixed block size, a block shorter than
   the rest can only be the last (RFC 9639 section 8.2), so a final short block is proof the stream
   ended there naturally, whatever STREAMINFO says. What is left is bytes after the last intact
   frame with no header in them: a damaged last frame, or junk appended to a file that was already
   cut at a frame boundary. Nothing in the bytes tells those apart.

   Two ways to answer that last case:

   - **Keep the size rule for it,** as the survey did: STREAMINFO when the bytes are enough to hold
     the missing samples. `last-frame-damaged.flac` gets 3.300 s; ffmpeg decodes 3.251 s.
   - **Make `file::duration` unusable,** and the file incomplete, as an MP3 whose summary header
     fails its checks is. Goro then never guesses a playing time. The case needs damage exactly at
     the end of the file, so it should be rare.

   **Decided on 2026-10-09: unusable,** and the file incomplete. Goro does not guess, and junk
   appended to a file is far likelier than damage falling exactly on its last frame header.

**Data outside the format** (L15 to L18, two comment blocks).

6. Are the `id3v2`, `ape` and `id3v1` sources read in a FLAC file? The prototype reads them, and
   concepts written without a source would then fall through from Vorbis to them.

   **Decided on 2026-10-09: yes,** since they bear on the concepts written without a source, though
   few files are expected to carry them.
7. With two VORBIS_COMMENT blocks, is the `vorbis` source the first, as for several Id3v2 or APE
   tags in an MP3, with `goro audit` reporting the second?

   **Decided on 2026-10-09: yes,** the first.

**Data the `vorbis` source does not reach.**

8. PICTURE blocks are not Vorbis comments, so `vorbis::bytes("METADATA_BLOCK_PICTURE")`, the Ogg
   convention, finds nothing in a FLAC file that keeps its art in PICTURE blocks. A FLAC file can
   hold both: music-metadata's `issue-266.flac` has a `METADATA_BLOCK_PICTURE` comment holding a
   400×396 front cover, and a PICTURE block holding a 600×594 picture of type "Other". So each has
   to be reachable on its own, and `vorbis::bytes()` must keep meaning comments: a FLAC file could
   just as well hold a comment named `PICTURE`.

   The data is the same structure in both places. A PICTURE block's payload, from `comments.flac`
   (big-endian lengths, as everywhere in FLAC but the comments):

   ```
   00 00 00 03                 picture type: 3, front cover
   00 00 00 09 image/png       media type, by length
   00 00 00 05 cover           description, UTF-8, by length
   00 00 00 01 00 00 00 01     width 1, height 1
   00 00 00 18 00 00 00 00     colour depth 24, colours used 0
   00 00 00 45 89 50 4e 47 …   data length 69, then the PNG
   ```

   The comment `METADATA_BLOCK_PICTURE=AAAAAwAAAAlpbWFnZS9wbmcA…` in `vorbis-comments.ogg` is that
   payload in base64, and decodes to exactly the bytes above. Of the real-world files, 9 of 37 FLAC
   files carry PICTURE blocks and 1 also the comment; 8 Ogg files carry the comment.

   **Decided on 2026-10-09:** the PICTURE block gets syntax of its own, apart from the `vorbis`
   source, and a concept spanning every format's cover art follows once each form is reachable.
   **Revised on 2026-10-10:** flac-support does not reach PICTURE blocks at all. The syntax, the
   type a picture is and the concept move to a line of their own,
   [pictures](../directions/pictures.md), and until it lands a FLAC file's PICTURE blocks cannot be
   queried.

9. A comment without `=` names nothing (L12, L13). Is it an unusable occurrence for every name asked
   about, as an Id3v2 frame whose description cannot be read is, or absent?

   **Decided on 2026-10-09: absent,** and reported by `goro audit` as a finding.

**Checksums the format already has.** FLAC checksums every audio frame, and nothing else: no
metadata block, the comments included, has a checksum of any kind. Each audio frame has a CRC-8
over its header and a CRC-16 over the whole frame. Goro's hash cannot see damage without a hash stored earlier to compare
against, and these can, the first time a file is seen. The walk that finds `E` reads every frame
anyway, and with the frame body parsed it knows where each one ends, so checking each CRC-16 costs
almost nothing. STREAMINFO's MD5 is different: checking it means decoding the audio, which Goro
does not do, and `flac -t` already does it.

10. Are the audio frames' CRC-16s checked, and where does a failure go: a finding for `goro audit`, the
    `incomplete` warning of `goro hash`, or both?

    **Decided on 2026-10-09:** every command that reads the whole file checks them, `goro hash`,
    `goro audit` and `goro explore` among them; `goro list` reads only the edges and does not. The
    walk of the whole file reports each failure: the frame, where it is, and which check failed.
    `goro hash` stays silent about them: counting them as incomplete would stretch that warning
    over a second concern, and not every command needs to speak of every finding.
