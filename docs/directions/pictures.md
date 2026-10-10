---
status: proposed
size: broad
touches: concepts/predicates.md, features/builtins/identifiers.md, design/rationale.md, testing.md, src/Goro/Predicates, src/Goro/Reading
after: flac-support, value-mapping
branch:
---
# Pictures, as a type and a concept

## Intent

Make the pictures a file carries, cover art first, something a predicate can ask about in every
format Goro reads, through one concept. That needs two decisions first: how a predicate reaches
picture data that no source function reaches today, and what type a picture is, since it has parts
(a picture type, a media type, a description, dimensions, the image) and the language has no
values with parts.

The line was split from [flac-support](archive/flac-support.md), which found that a FLAC file's own
PICTURE blocks are out of reach and set reaching them aside rather than settle syntax in passing.
Until this line lands, a FLAC file's PICTURE blocks cannot be queried.

## What is known

From [flac-layout](../design/flac-layout.md), question 8, measured on the corpus and on the
real-world test data of [file-reading](../design/file-reading.md#real-world-test-data).

How each format records a picture:

| Format        | Where                                           | Structure                                                                                        | Reached today
|---------------|-------------------------------------------------|--------------------------------------------------------------------------------------------------|---------------
| Id3v2.3, v2.4 | an `APIC` frame                                 | text encoding byte, media type ending in a NUL, picture type byte, description, image             | `id3v2::bytes("APIC")`, the frame as recorded
| Id3v2.2       | a `PIC` frame, keeping its own name             | the same, with a three-letter image format in place of a media type                              | `id3v2::bytes("PIC")`, the frame as recorded
| APE           | a binary item such as `Cover Art (Front)`       | a file name or description ending in a NUL, then the image; the picture type is in the key       | `ape::bytes(key)`, the item as recorded
| FLAC          | a PICTURE metadata block, any number of them    | picture type, media type and description by length, width, height, colour depth, colours, image  | not at all
| Ogg, and FLAC | a `METADATA_BLOCK_PICTURE` Vorbis comment       | the PICTURE block's payload, byte for byte, in base64                                            | `vorbis::bytes(...)`, as base64 text

- A FLAC file can hold both forms at once, and they need not agree. music-metadata's
  `issue-266.flac` has a 400×396 front cover in a `METADATA_BLOCK_PICTURE` comment and a 600×594
  picture of type "Other" in a PICTURE block.
- Of the 37 real-world FLAC files, 9 carry PICTURE blocks, and one of them also the comment. Eight
  Ogg files carry the comment. The older `COVERART` comment convention was not found in any of them.
- `vorbis::bytes()` must keep meaning comments: a FLAC file could hold a comment named `PICTURE`,
  and syntax reaching the block must not hide it.

## Questions

1. **Reaching FLAC's own metadata.** `flac::picture()` was proposed in flac-support and set aside.
   Is there a `flac` source for the metadata blocks that are not comments, and what else would it
   hold (APPLICATION, CUESHEET)? Or does the picture concept reach PICTURE blocks with no source
   function of its own?
2. **Projections.** Taking a part out of a picture is a map over the bag of occurrences: an
   unusable picture would give an unusable part, and an absent picture an absent part. The
   notation is explored first, for compound types in general, by
   [value-mapping](value-mapping.md); this line applies it.
3. **The type.** Is a picture a new type, or one instance of a general compound type? The core type
   system is meant to be settled once rather than extended case by case, so the answer has to hold
   for whatever else turns out to have parts.
4. **The concept.** Its cells across the forms above, the order a concept written without a
   source prefers them in, and how a FLAC file's two forms rank against each other.
5. **Relation to [non-text-tag-data](non-text-tag-data.md),** which asks what tag data that is not
   text resolves to. Pictures are its largest case: does this line take them over, leaving it the
   rest (`POPM`, `PRIV`, `UFID`, `GEOB`, binary APE items that are not pictures)?

## Done when

A picture type and a concept for it are specified in predicates.md and identifiers.md, with the
reasoning in the rationale; a predicate can ask about the pictures of a file in every format Goro
reads, a FLAC file's PICTURE blocks included; and tests pin each form, including a file holding
two forms that disagree.

## Log

- 2026-10-10 -- Split from flac-support, which set `flac::picture()` aside so as not to settle syntax
  for compound values in passing. PJ suggested exploring notation for mapping over values first, and
  for projections that work with compound types in general; that became a line of its own,
  [value-mapping](value-mapping.md), which comes first. Next step: value-mapping.
