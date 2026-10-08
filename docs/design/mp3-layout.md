# The layout of an MP3 file

This is working material for [mp3-support](../directions/mp3-support.md), made to settle what
`goro hash` covers at the edges of an MP3's audio. It describes how an MP3 file is laid out, where
each kind of tag sits, and what goes wrong at each edge. Every mishap is tied to a lettered
position in the diagrams, and most to a file in the fixture corpus (`tests/Goro.Tests/Fixtures/Audio/mp3`).

A rendered version of the diagrams, with each mishap marked on its map, is at
[MP3 File Layout](https://claude.ai/artifact/JrMutMM1J1DAJk6KY6HkLs); this file is the source.

Positions measured on the corpus come from
`dotnet run -c Release --project scratchpad/Goro.Scratchpad -- boundaries`, which prints each
position below for every MP3 in it, beside TagLibSharp's invariant range: the range `goro hash`
hashes today.

## The anatomy

An MP3 file is a run of MPEG audio frames with tags in front of it and behind it. Nothing in the
file says where the audio starts or ends: a reader works it out by finding the tags, which do say
how long they are, and by finding frames, which begin with a sync pattern of eleven set bits.

```
  A          B        C       D                                   E         F                  H
  │          │        │       │                                   │         │                  │
  ┌──────────┬────────┬───────┬───────────────────────────────────┬─────────┬──────────────────┐
  │ leading  │  gap   │ Info  │ audio frames                      │   gap   │ trailing tags    │
  │ tags     │        │ frame │                                   │         │                  │
  └──────────┴────────┴───────┴───────────────────────────────────┴─────────┴──────────────────┘
```

| Position | Where                                                                                       | Found by
|----------|---------------------------------------------------------------------------------------------|----------
| `A`      | the start of the file                                                                       | -
| `B`      | where the leading tags say they end: the sum of their declared sizes                        | reading each tag's header, from `A` onwards
| `C`      | the first frame: a valid header with a matching header one frame length later             | searching for sync from `B`
| `D`      | the first audio frame: `C`, or the frame after it when `C` is an Info frame                | looking for `Xing`, `Info` or `VBRI` inside the frame at `C`
| `E`      | the end of the last whole frame                                                             | walking frame headers from `C`, resynchronising over damage
| `F`      | where the trailing tags start                                                               | reading each tag's footer, from `H` inwards
| `H`      | the end of the file                                                                         | -

In a healthy file the gaps are empty: `B = C` and `E = F`. Each gap holding something is a
mishap, described below.

**The Info frame** (`C` to `D`) is a frame of silence that LAME, ffmpeg and others put first. It
holds the frame count, the byte count and a seek table, which are summaries, and the LAME tag. That
tag's encoder delay and padding change what a gapless decoder outputs. Zeroing them in
`seeds/mp3-cbr.mp3` changes ffmpeg's output from 145,530 to 146,927 samples. Editing the byte
count field in the same frame changes nothing.

**Today's hash range** is TagLibSharp's `[InvariantStartPosition, InvariantEndPosition)`. On every
corpus file TagLibSharp could read, that is `[B, F)`: it includes both gaps, and the Info frame.

## Variation 1: no tags

What an encoder writes, before anything has tagged it. `seeds/mp3-cbr.mp3` is one.

```
  A=B=C     D                                                                   E=F=H
  │         │                                                                   │
  ┌─────────┬───────────────────────────────────────────────────────────────────┐
  │ Info    │ audio frames                                                      │
  │ frame   │                                                                   │
  └─────────┴───────────────────────────────────────────────────────────────────┘
```

With no tags, every mishap happens either to the audio itself, which is what the hash exists to
catch, or at its edges, with no tag to explain it.

- **M1. Damage inside the frames,** between `D` and `E`: bit flips (`vbr-bitflips.mp3`), a zeroed
  block (`vbr-zeroed-4k.mp3`). The hash must change, and does under any range. Finding `E` has to
  resynchronise over the damage: a naive walk from `C` stops at the zeroed block, at byte 12,583
  of 24,723.
- **M2. Truncation** inside the last frame (`cbr-truncated-mid-frame.mp3`). `E` is 26,748, the end
  of the last whole frame, and the partial frame runs to `H` at 26,857. The audio has changed, so
  the hash must change, and it does under either choice of end. The question is only whether the
  partial frame's 109 bytes count as audio.
- **M3. Garbage appended** after the last frame, with no tag (`garbage-appended.mp3`). `E` is
  26,957 and `H` is 29,957. A decoder finds false syncs in garbage: ffmpeg decodes 19 ms more from
  this file than from its seed. Whether that counts as the audio changing is a matter of
  definition. Garbage can be detected, but not explained.
- **M4. Garbage prepended,** with no tag (`garbage-prepended.mp3`). `C` is 3,000. Today's range
  starts at 0. ffmpeg misses the LAME tag behind the garbage and decodes the Info frame as audio.
- **M5. The Info frame rewritten.** Tools that "rebuild the VBR header" rewrite `C` to `D` without
  touching the audio (not verified with such a tool). Since the frame lies inside today's range,
  the hash changes. Its delay and padding are the only part that changes what a gapless decoder
  outputs.
- **M6. Files joined end to end** (`joined-cbr.mp3`): a second Info frame in the middle of the
  frames, where it decodes as a frame of silence. It is part of the audio as joined.

## Variation 2: Id3v2 in front, Id3v1 behind

The commonest layout. `id3v24.mp3` holds an Id3v2.4 tag; `id3v11.mp3` an Id3v1.1 tag.

```
  A                       B=C     D                                         E=F=G      H
  │                       │       │                                         │          │
  ┌───────────────────────┬───────┬─────────────────────────────────────────┬──────────┐
  │ Id3v2                 │ Info  │ audio frames                            │ Id3v1    │
  │ header·frames·padding │ frame │                                         │ 128 bytes│
  └───────────────────────┴───────┴─────────────────────────────────────────┴──────────┘
```

`G` is the start of the Id3v1 tag, always `H` minus 128, found by `TAG` there. With no other
trailing tag, it is also `F`. The Id3v2 header
holds a syncsafe size covering everything after its own 10 bytes; with a footer, 10 more follow
(`id3v24-exthdr-footer.mp3`).

- **M7. Junk between the tag and the audio:** `B < C`. The tag's padding ran on past its declared
  size, or a tagger shrank the tag and left the old bytes behind (`id3v24-junk-before-audio.mp3`:
  `B` 73, `C` 1,073). Today's range starts at `B`, so the junk is hashed, and a tagger that later
  tidies it away changes the hash.
- **M8. The size is too small:** `B` falls inside the tag (`dmg-id3v2-size-short.mp3`: `B` 304,
  `C` 604). The rest of the tag lies in the gap, frames and all. Today's range starts at 304, so
  part of the tag is hashed; correcting the tag changes the hash. The tag's structure is also
  broken, a rung 3 case.
- **M9. The size cannot be read:** not syncsafe (`dmg-id3v2-size-not-syncsafe.mp3`), or a
  revision that does not exist (`dmg-id3v2-version-5.mp3`). `B` is unknown, so the search for `C`
  has to start just after the 10-byte header. It finds 922 and 604 respectively. Today, TagLibSharp
  hashes the first file from 0, the whole tag included, and refuses the second.
- **M10. The size points past the end of the file** (`dmg-id3v2-size-past-eof.mp3`). `B` is beyond
  `H`. Searching from `B` finds nothing; searching from the header would find the audio. Today the
  file is refused.
- **M11. A false sync inside the tag,** when the search for `C` starts early as in M9 and M10.
  Picture data can hold `FF Ex`, which is exactly what unsynchronisation exists to prevent, and
  most taggers do not unsynchronise. Confirming a frame by the header that follows it makes a false
  match unlikely; it does not make it impossible. Not seen in the corpus.
- **M12. A false Id3v1.** No tag, but the last 128 bytes of audio start with `TAG`: about one file
  in 16.7 million, if those bytes are random. 128 bytes of audio would be taken for a tag. The
  reverse, `TAG` followed by garbage (`dmg-id3v1-garbage.mp3`), is a well-formed tag with nonsense
  in it, and harmless to the hash.

## Variation 3: everything behind the audio

An Id3v2 tag in front, and behind the audio an APEv2 tag, then a Lyrics3v2 block, then Id3v1.
`all-tags.mp3` has all but the Lyrics3v2 block, and `ape2-lyrics3-id3v1.mp3` all but the Id3v2
tag. The trailing tags are found from `H` inwards, each by the footer that ends it: Id3v1 by `TAG`
at `H` minus 128, Lyrics3v2 by `LYRICS200` before that, APE by `APETAGEX` before that.

```
  A         B=C     D                              E=F                F2         G         H
  │         │       │                              │                  │          │         │
  ┌─────────┬───────┬──────────────────────────────┬──────────────────┬──────────┬─────────┐
  │ Id3v2   │ Info  │ audio frames                 │ APEv2            │ Lyrics3v2│ Id3v1   │
  │         │ frame │                              │ hdr·items·footer │          │         │
  └─────────┴───────┴──────────────────────────────┴──────────────────┴──────────┴─────────┘
```

Here `F` is the start of the APE tag, which is the first trailing tag, `F2` the start of the
Lyrics3v2 block, and `G` the start of Id3v1.

- **M13. A block in the chain is not recognised.** Each tag is found only once the one behind it
  has been found and stepped over. TagLibSharp does not know Lyrics3v2, so it stops at `F2` and
  never sees the APE tag. In `ape2-lyrics3-id3v1.mp3` today's range ends at 27,379, while the audio
  ends at 26,957: the APE tag is hashed, so editing it changes today's hash. This is a defect in
  the hash Goro computes now.
- **M14. A damaged footer.** An APE tag whose footer is wiped, or whose size points before the
  start of the file (`dmg-ape-size-past-start.mp3`), is not stepped over. Its bytes become part of
  the gap `E` to `F`. TagLibSharp refuses the second file outright.
- **M15. An APE tag with a header and no footer.** The format does not allow it, but a reader
  looking for footers would never find it, and it too lands in the gap `E` to `F`.

## Variation 4: tags in unusual places

```
  A         B1         B=C     D                              E=F             G         H
  │         │          │       │                              │               │         │
  ┌─────────┬──────────┬───────┬──────────────────────────────┬───────────────┬─────────┐
  │ Id3v2   │ Id3v2 or │ Info  │ audio frames                 │ Id3v2 with    │ Id3v1   │
  │         │ APE (hdr)│ frame │                              │ footer (3DI)  │         │
  └─────────┴──────────┴───────┴──────────────────────────────┴───────────────┴─────────┘
```

`B1` is where the first leading tag ends and a second begins.

- **Two tags in front:** a second Id3v2 tag (`id3v2-twice.mp3`), or an APE tag with a header
  (`ape2-at-start.mp3`, `B` 370). Both are read, and `B` is where the last of them ends.
  TagLibSharp handles both today.
- **M16. A damaged second tag.** If the second tag's header is damaged, `B` stops at `B1`, and the
  second tag falls into the gap `B` to `C`, as in M7.
- **An Id3v2 tag appended at the end**, before any Id3v1 tag (`id3v24-appended.mp3`). The format
  allows it only with a footer, `3DI`, which is how it is found from behind.
- **M17. An appended Id3v2 tag without a footer.** Not allowed, and not findable from behind: it
  lands in the gap `E` to `F`.

## What the edges could be

Each edge of the hashed range has candidates. The mishaps above are what tell them apart.

**The start:**

| Start | What it means                                         | Mishaps that change the hash when they should not
|-------|-------------------------------------------------------|---------------------------------------------------
| `B`   | after the tags, as declared (today)                   | M7, M8, M16: junk and leftovers are hashed; M9, M10: undefined
| `C`   | the first frame                                       | M5: rewriting the Info frame
| `D`   | the first audio frame                                 | none, but a change to the gapless delay and padding goes unnoticed

**The end:**

| End   | What it means                                         | Mishaps that change the hash when they should not
|-------|-------------------------------------------------------|---------------------------------------------------
| `F`   | before the trailing tags, as found (today)            | M3, M13, M14, M15, M17: garbage and unrecognised tags are hashed
| `E`   | after the last whole frame                            | none; a partial last frame (M2) is left out, which changes nothing, since truncation changes the hash anyway

Two observations shape the choice:

- **Finding `E` costs `goro hash` nothing.** It reads every byte anyway, so it can walk the frame
  headers as it goes, resynchronising over damage as in M1. Elsewhere `E` is not needed.
- **The Info frame is half summary and half audio.** Its counts and seek table are summaries that
  tools rewrite. Its delay and padding decide what a gapless decoder outputs.

**Decided on 2026-10-08: the hash covers `[D, E)`,** the Info frame and its LAME tag left out, and
anything after `E` left out and reported by `goro audit`. That applies a rule for every format,
hash the codec's own stream and nothing the container adds; see
[file-reading](file-reading.md#what-the-hash-covers) for the rule and why the delay and padding
were not kept.

The gaps themselves are worth reporting whatever the range: bytes between `B` and `C`, or between
`E` and `F`, are something `goro audit` can show, and junk before the audio is the one sign left
of a tag damaged past recognition.
