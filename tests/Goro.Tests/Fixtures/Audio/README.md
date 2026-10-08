# Audio fixture corpus

Small audio files, healthy and damaged, for testing anything that reads tags, finds the audio
or works out a playing time. Every file is 3.3 seconds of audio or less, mono where the format
allows.

- `seeds/` holds the only files an encoder made, by
  `scratchpad/Goro.Scratchpad/FileReading/make-seeds.sh`. They carry no tags beyond what the
  encoder writes (LAME's Info frame, the encoder's vendor comment).
- Every other file is derived from a seed, byte for byte, by
  `scratchpad/Goro.Scratchpad/FileReading/Corpus.cs`: tags are written by hand rather than
  through a tag library, and damage is applied at known offsets. Rebuilding with
  `dotnet run -c Release --project scratchpad/Goro.Scratchpad -- corpus` gives identical bytes.
- `manifest.json` lists every derived file with what it records: for each tag field, the tag,
  the key as written, the description where the frame has one, the values as text, and the
  content bytes (as hex, or a SHA-256 for values over 4 KiB). It is the reference a reader is
  judged against.

Groups: **layout** files are healthy and differ in where the tags sit and how they are written;
**tag-damage** files hold a damaged tag over healthy audio; **audio-damage** files hold damaged or
unusual audio. Names starting `dmg-` are tag damage.

The seeds' playing times, as ffmpeg 9.0.2 decodes them: 3.300 s for every seed made from the main
source with gapless information (LAME tag, Opus pre-skip, Vorbis and FLAC exact lengths), 3.344 s
for the MPEG-1 seeds without an Info frame (128 frames of 1152 samples), 3.456 s for the MPEG-2.5
seed, 1.700 s for `mp3-cbr-b.mp3`, and 5.010 s for `ogg-chained-vorbis.ogg`. Two seeds are beyond
ffmpeg: it cannot read `mp3-freeformat.mp3`, which LAME decodes to 3.300 s, and it decodes only
the first link of `ogg-chained-vorbis-opus.ogg`, whose links are 3.300 s and 1.700 s long.

## mp3

### layout

| File | What it is |
|------|------------|
| `id3v24.mp3` | Id3v2.4 tag holding the standard frame set: UTF-8, UTF-16 and Latin-1 text, a two-value TPE1, TXXX twice with descriptions differing in case, two COMM, USLT, URL frames, APIC, PRIV, POPM, an empty TPE2, an unknown text frame TZZZ. |
| `id3v24-utf16be.mp3` | Id3v2.4 text in encoding 2, UTF-16BE without a byte order mark. |
| `id3v23.mp3` | Id3v2.3 tag: UTF-16 with BOM, a NUL-terminated value, TCON with a slash, TYER/TDAT/TIME, TORY, IPLS, RVAD. |
| `id3v22.mp3` | Id3v2.2 tag: three-letter frames, TCO as a bare reference, PIC with a three-letter image format. |
| `id3v23-unsync.mp3` | Id3v2.3 with the unsynchronisation flag: the whole tag body is unsynchronised, so the APIC holding 0xFF 0xE0 sequences is stored with inserted zero bytes. |
| `id3v24-frame-unsync.mp3` | Id3v2.4 with one frame unsynchronised (frame flag n) and a data length indicator (flag p). |
| `id3v24-tag-unsync.mp3` | Id3v2.4 with the tag-level unsynchronisation flag, every frame unsynchronised and flagged n. |
| `id3v23-compressed.mp3` | Id3v2.3 with a zlib-compressed COMM frame (flag i, decompressed size prefixed). |
| `id3v24-compressed.mp3` | Id3v2.4 with a zlib-compressed COMM frame (flags k and p). |
| `id3v23-encrypted.mp3` | Id3v2.3 with a TPE1 flagged as encrypted (method 0x80, registered by ENCR). The content is plain text; no reader can know that. |
| `id3v23-exthdr.mp3` | Id3v2.3 with an extended header. |
| `id3v24-exthdr-footer.mp3` | Id3v2.4 with an extended header and a footer. |
| `id3v24-big-padding.mp3` | Id3v2.4 with 64 KiB of padding, as taggers leave to make room for later edits. |
| `id3v24-duplicate-text-frames.mp3` | Id3v2.4 with TIT2 written twice, which the format forbids but taggers produce. |
| `id3v2-twice.mp3` | Two Id3v2 tags back to back (v2.4 then v2.3), as some taggers leave behind. |
| `id3v24-appended.mp3` | An Id3v2.4 tag with a footer appended after the audio (and before an Id3v1 tag), as the format allows. |
| `id3v10.mp3` | Id3v1.0: a 30-byte comment, no track number. |
| `id3v11.mp3` | Id3v1.1: track 3 in the last comment byte, genre 17. |
| `id3v11-latin1-genre255.mp3` | Id3v1.1 with Latin-1 text outside ASCII and genre byte 255 (none). |
| `ape2.mp3` | APEv2 tag with header and footer: a two-value Artist, a binary cover item, a locator item. |
| `ape2-footer-only.mp3` | APEv2 tag with a footer and no header. |
| `ape1-latin1.mp3` | APEv1 tag (version 1000, footer only) whose Title is ISO-8859-1, as v1 specified. |
| `ape2-id3v1.mp3` | APEv2 followed by Id3v1, the usual order. |
| `ape2-lyrics3-id3v1.mp3` | APEv2, then a Lyrics3v2 block, then Id3v1. |
| `ape2-at-start.mp3` | APEv2 tag (with header) before the audio, which some tools write and the format does not forbid. |
| `ape2-keys-differing-in-case.mp3` | APEv2 with ARTIST and Artist, which the format forbids. |
| `all-tags.mp3` | Id3v2.4, then audio, then APEv2, then Id3v1: every tag an MP3 commonly carries. |
| `id3v24-junk-before-audio.mp3` | Id3v2.4 followed by 1000 zero bytes the tag size does not cover, then the audio. |

### tag-damage

| File | What it is |
|------|------------|
| `dmg-id3v2-size-past-eof.mp3` | Id3v2.4 tag size field claims 10 MB, more than the file holds. |
| `dmg-id3v2-size-short.mp3` | Id3v2.4 tag size field 300 bytes too short: it ends inside a frame, and the rest of the tag sits where the audio should start. |
| `dmg-id3v2-size-not-syncsafe.mp3` | Id3v2.4 tag size written as a plain integer (a byte with its top bit set), as some broken taggers do. |
| `dmg-id3v24-frame-overrun.mp3` | Id3v2.4: the fourth frame (TCON) declares a size that runs past the end of the tag. |
| `dmg-id3v24-itunes-sizes.mp3` | Id3v2.4 with every frame size written as a plain integer, as iTunes once did; the 400-byte COMM makes the difference visible. |
| `dmg-id3v24-bad-frame-id.mp3` | Id3v2.4: the third frame's identifier is 't!t2', not a legal identifier. |
| `dmg-id3v24-zero-size-frame.mp3` | Id3v2.4: a TIT3 frame with size 0 in the middle of the tag. |
| `dmg-id3v24-bad-encoding.mp3` | Id3v2.4: TIT2's encoding byte is 5, which no revision defines. |
| `dmg-id3v24-utf16-no-bom.mp3` | Id3v2.4: TIT2 in encoding 1 (UTF-16) without a byte order mark. |
| `dmg-id3v24-utf16-odd-length.mp3` | Id3v2.4: TIT2 in UTF-16 with an odd number of bytes. |
| `dmg-id3v24-invalid-utf8.mp3` | Id3v2.4: TPE1 declares UTF-8 and holds 'Mot\xF6rhead' in Latin-1. |
| `dmg-id3v23-codepage-as-latin1.mp3` | Id3v2.3: TPE1 declares Latin-1 but holds the local codepage (Windows-1251 Cyrillic), the classic mis-tagged file. |
| `dmg-id3v2-version-5.mp3` | A tag claiming Id3v2.5, a revision that does not exist, laid out as v2.4. |
| `dmg-id3v24-bad-zlib.mp3` | Id3v2.4: COMM flagged compressed whose data is not zlib. |
| `dmg-id3v24-truncated-in-tag.mp3` | A file that ends inside its Id3v2.4 tag: no audio at all. |
| `dmg-id3v24-zeroed-block.mp3` | Id3v2.4 with 64 bytes in the middle of the frames zeroed, as bit rot would. |
| `dmg-id3v1-garbage.mp3` | 'TAG' followed by 125 random bytes. |
| `dmg-ape-item-count-high.mp3` | APEv2 footer claims 50 items; there are 9. |
| `dmg-ape-size-past-start.mp3` | APEv2 footer claims a tag larger than the file. |
| `dmg-ape-item-overrun.mp3` | APEv2: the Album item's value size runs past the end of the tag. |
| `dmg-ape-invalid-utf8.mp3` | APEv2 text item Artist holding 'Mot\xF6rhead' in Latin-1, which is not valid UTF-8. |

### audio-damage

| File | What it is |
|------|------------|
| `vbr-truncated-half.mp3` | VBR file with a Xing header, cut to half its length: the header still claims every frame. |
| `cbr-truncated-mid-frame.mp3` | CBR file whose last frame is cut 100 bytes short. |
| `vbr-xing-count-doubled.mp3` | VBR file whose Xing frame count has been doubled: the header lies. |
| `vbr-xing-zeroed.mp3` | VBR file whose Xing header bytes are zeroed: a silent first frame, then VBR audio with no summary. |
| `vbr-zeroed-4k.mp3` | VBR file with 4 KiB zeroed in the middle of the audio (bit rot). |
| `vbr-bitflips.mp3` | VBR file with 40 random single-bit flips in the audio, header bytes included. |
| `garbage-prepended.mp3` | 3000 random bytes, no tag, then a CBR file. |
| `garbage-appended.mp3` | A CBR file followed by 3000 random bytes and no tag. |
| `joined-cbr.mp3` | Two CBR files joined byte for byte: two Info frames, the first counting only the first file's frames. |
| `vbri.mp3` | The VBR file with its Xing frame rewritten as a Fraunhofer VBRI frame (no LAME tag). |
| `vbr-notag-id3v24.mp3` | VBR without a Xing header, behind an Id3v2.4 tag: duration needs a scan or an estimate. |

## ogg

### layout

| File | What it is |
|------|------------|
| `vorbis-comments.ogg` | Ogg vorbis with the standard comments: ARTIST twice, a lower-case key, an empty value, a padded value, METADATA_BLOCK_PICTURE. |
| `vorbis-comments-multipage.ogg` | Ogg vorbis whose comment packet is 150 KB, spread over three pages. |
| `opus-comments.opus` | Ogg opus with the standard comments: ARTIST twice, a lower-case key, an empty value, a padded value, METADATA_BLOCK_PICTURE. |
| `opus-comments-multipage.opus` | Ogg opus whose comment packet is 150 KB, spread over three pages. |
| `opus-output-gain.opus` | Ogg Opus with OpusHead output gain set to -6 dB, as loudness tools do: the decoded audio changes. |

### tag-damage

| File | What it is |
|------|------------|
| `vorbis-dmg-comment-values.ogg` | Ogg vorbis: one value is not valid UTF-8, one comment has no '=', one key holds a character the spec forbids. |
| `vorbis-dmg-comment-length-overrun.ogg` | Ogg vorbis: the third comment's length runs past the end of the packet. |
| `vorbis-dmg-comment-count-high.ogg` | Ogg vorbis: the comment count claims 40 comments; there are 11. |
| `vorbis-dmg-vendor-length-overrun.ogg` | Ogg vorbis: the vendor string length runs past the end of the packet. |
| `vorbis-dmg-comment-page-crc.ogg` | Ogg vorbis: one byte of a comment value changed after the page CRC was computed. |
| `opus-dmg-comment-values.opus` | Ogg opus: one value is not valid UTF-8, one comment has no '=', one key holds a character the spec forbids. |
| `opus-dmg-comment-length-overrun.opus` | Ogg opus: the third comment's length runs past the end of the packet. |
| `opus-dmg-comment-count-high.opus` | Ogg opus: the comment count claims 40 comments; there are 11. |
| `opus-dmg-vendor-length-overrun.opus` | Ogg opus: the vendor string length runs past the end of the packet. |
| `opus-dmg-comment-page-crc.opus` | Ogg opus: one byte of a comment value changed after the page CRC was computed. |
| `vorbis-dmg-no-framing-bit.ogg` | Ogg Vorbis comment header without its final framing bit. |

### audio-damage

| File | What it is |
|------|------------|
| `vorbis-truncated.ogg` | Ogg vorbis cut at 60% of its length: no EOS page, and the last page is incomplete. |
| `vorbis-audio-page-crc.ogg` | Ogg vorbis with one byte changed in the middle of an audio page, so its CRC fails. |
| `vorbis-last-page-crc.ogg` | Ogg vorbis with the body of the last page damaged, so the page holding the final granule fails its CRC. |
| `vorbis-garbage-appended.ogg` | Ogg vorbis followed by 3000 random bytes. |
| `vorbis-id3v2-prepended.ogg` | An Id3v2.4 tag before the Ogg vorbis stream, which some tools write. |
| `vorbis-page-missing.ogg` | Ogg vorbis with one audio page in the middle removed: a gap in the page sequence numbers. |
| `opus-truncated.opus` | Ogg opus cut at 60% of its length: no EOS page, and the last page is incomplete. |
| `opus-audio-page-crc.opus` | Ogg opus with one byte changed in the middle of an audio page, so its CRC fails. |
| `opus-last-page-crc.opus` | Ogg opus with the body of the last page damaged, so the page holding the final granule fails its CRC. |
| `opus-garbage-appended.opus` | Ogg opus followed by 3000 random bytes. |
| `opus-id3v2-prepended.opus` | An Id3v2.4 tag before the Ogg opus stream, which some tools write. |
| `opus-page-missing.opus` | Ogg opus with one audio page in the middle removed: a gap in the page sequence numbers. |

## flac

### layout

| File | What it is |
|------|------------|
| `comments.flac` | FLAC with STREAMINFO, SEEKTABLE, VORBIS_COMMENT (the standard comments), APPLICATION, PICTURE and PADDING. |
| `comment-block-last.flac` | FLAC whose VORBIS_COMMENT is the last metadata block, after PADDING. |
| `two-comment-blocks.flac` | FLAC with two VORBIS_COMMENT blocks, which the format forbids but files carry. |
| `id3v2-prefix.flac` | An Id3v2.4 tag before fLaC, as some rippers write. |
| `id3v1-suffix.flac` | Id3v1.1 after the last frame. |
| `ape-suffix.flac` | APEv2 and Id3v1 after the last frame. |

### audio-damage

| File | What it is |
|------|------------|
| `streaminfo-total-zero.flac` | STREAMINFO total samples 0 and MD5 zero, as an encoder writing to a pipe leaves them (seed flac-piped.flac, retagged). |
| `streaminfo-total-doubled.flac` | STREAMINFO total samples doubled: the header lies. |
| `truncated.flac` | FLAC cut at 60% of its length, in the middle of a frame. |
| `zeroed-4k.flac` | FLAC with 4 KiB zeroed in the middle of the frames. |
| `last-frame-damaged.flac` | FLAC with the last frame's header bytes changed, so its CRC-8 fails. |
| `garbage-appended.flac` | FLAC followed by 3000 random bytes. |

### tag-damage

| File | What it is |
|------|------------|
| `dmg-comment-values.flac` | FLAC VORBIS_COMMENT: invalid UTF-8, a comment without '=', a forbidden key character. |
| `dmg-comment-length-overrun.flac` | FLAC VORBIS_COMMENT: the third comment's length runs past the end of the block. |
| `dmg-block-length-overrun.flac` | FLAC: the VORBIS_COMMENT block header claims 200 bytes more than the block holds, so the next block header is misread. |
| `dmg-no-last-block-flag.flac` | FLAC whose metadata blocks never set the last-block flag, so a reader walks into the first frame. |
| `dmg-zeroed-comment.flac` | FLAC with 64 bytes in the middle of the VORBIS_COMMENT block zeroed. |
