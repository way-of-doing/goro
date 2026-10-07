---
status: proposed
size: broad
touches: features/builtins/identifiers.md, concepts/warnings.md, design/rationale.md, implementation.md, testing.md, src/Goro/Predicates/Identifiers, src/Goro/Hashing
after: sources-and-fields
branch:
---
# Tags in MP3 files

## Intent

Answer every technical question that stands between the tags an MP3 file carries -- Id3v2, Id3v1
and APE -- and what the specification promises about them, and then read them. MP3 is the one
format Goro hashes today, and the documentation describes its tags at length, but nothing reads
them yet: every tag identifier is declared and bound to nothing. This line is what binds the
sources an MP3 can carry to real data, as [flac-support](flac-support.md) and
[ogg-support](ogg-support.md) will for theirs.

The promise to keep comes from [sources-and-fields](sources-and-fields.md): `field()` and
`bytes()` give the data as recorded. That promise is kept even if it means Goro reads some formats
without TagLibSharp.

## Questions

What is known so far comes from two probes against TagLibSharp 2.3.0, built by hand and recorded
in the sources-and-fields Log on 2026-10-07:

- **APE:** one item per key, the last one kept, which is as the format requires; text items
  decoded as UTF-8 as they are read, with the recorded bytes not kept, so `Mot\xF6rhead` comes
  back with U+FFFD and `Render()` writes the replacement character.
- **Id3v2:** in a v2.4 tag, `TYER` and `TDAT` are left as they are; in a v2.3 tag, `TYER`, `TDAT`
  and `TIME` are merged into a single `TDRC`, `TORY` becomes `TDOR`, `RVAD` is dropped, and an
  `IPLS` loses all but its first value; in a v2.2 tag, `PIC` becomes an `APIC` with converted
  content.

The questions:

- **Which reader, per tag format.** For each of Id3v2, Id3v1 and APE: whether TagLibSharp can be
  made to give the data as recorded, or Goro reads that format itself. APE is small to read: a
  32-byte footer and a list of items. Id3v2 is not: unsynchronisation, extended headers, the
  compression and encryption flags, three revisions of frame header, tags appended at the end of
  the file, and files holding more than one Id3v2 tag.
- **Addressing frames.** sources-and-fields adopted, as a first attempt, a fixed table keyed on
  each frame's own identifier: a frame is renamed to its v2.4 identifier only where v2.4 holds
  the same data in the same form, whatever the tag's revision. Confirm it against real tags, or
  replace it.
- **Text encodings.** Id3v2 declares an encoding for each text frame: ISO-8859-1, UTF-16 with a
  byte order mark, UTF-16BE, or UTF-8. Decide what a frame whose text does not decode in its
  declared encoding yields, and what an APE text item that is not valid UTF-8 yields.
- **Damaged tag data.** `warnings.md` makes a file unreadable if any one of its tags cannot be
  parsed. That rule was a placeholder chosen with nothing known. Build damaged tags for each
  format and record what every field and concept yields: absent, wrong text, unusable, or an
  exception. Then choose between the whole file being unreadable and damage making only the
  affected source's occurrences unusable, which would let `PREFERRED()` route around it. Whatever
  is chosen here, flac-support and ogg-support have to hold for their formats too.
- **Where the tags are.** Tags in unexpected places, such as an APE tag before the audio or an
  Id3v2 tag appended at the end, and whether the hashed range excludes every tag the reader finds
  and nothing else.
- **Known deviations.** `testing.md` records a deviation of the reader as a skipped test, a
  policy written for a reader faithful in almost every case, and lists one: a v2.3 `TCON` frame
  separating genres with a slash is reported with a semicolon, and `(RX)` without its
  parentheses. Under the promise these are defects to remove, so decide what becomes of the
  policy. Decide also what `bytes()` gives for a frame the tag unsynchronises, compresses or
  encrypts.
- **`file::duration`.** `implementation.md` notes that TagLibSharp parses the tags while it locates
  the audio, so a strict tag parser would make a file unreadable for a predicate that never asked
  about its tags. If Goro reads tags itself, decide what locates the audio.

## Done when

The rationale records which reader each format uses and why, and what damaged tag data resolves
to; the identifier documentation describes MP3 tags as they are actually read, correcting what
the probes contradicted; `warnings.md` says what damaged tag data does; and the tag sources an MP3
can carry are bound to real data, with tests that pin what hand-built tags, damaged ones included,
resolve to.

## Log

- 2026-10-07 -- Split from sources-and-fields, which specifies the source functions and leaves
  reading to this line.
