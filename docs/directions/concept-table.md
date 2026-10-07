---
status: proposed
size: broad
touches: features/builtins/identifiers.md, design/rationale.md, design/deferred.md, testing.md, src/Goro/Predicates/Identifiers
after: sources-and-fields, mp3-support
branch:
---
# Discovering what tags hold, and filling in the concept table

## Intent

Find out what real collections record, and grow the concept table from those facts. The [sources-and-fields](sources-and-fields.md) line leaves a table with
only the bare minimum in it. This line decides what else belongs there, one concept at a time.

A concept earns its place by a concrete use, not by a format happening to record something. A
concept whose type cannot be decided yet is `opaque`, so it can be counted and tested for presence
and promoted later. But a concept nobody has a use for is better left out altogether: anything
recorded is already reachable through `field()` or `bytes()`. Attached pictures are the example.
What a predicate might want to ask of a picture is unknown, and asking anything of one would need
values with properties, which would need syntax of their own whose ergonomics matter more than
having a `picture` concept early.

## Questions

- **Which concepts.** Candidates that come up in discussion include the album artist, disc
  number, composer, lyrics, the compilation flag (see the deferred entry on boolean data from
  tags), ratings, and pictures. Each needs a concrete use before it gets in.
- **`comment`**, left out of the minimum until it has had more homework: which field its Vorbis
  cell reads, `COMMENT` or `DESCRIPTION`; reading only the Id3v2 `COMM` frame with an empty
  description, so that iTunes's `iTunNORM` and `iTunSMPB` are not comments; and Id3v1's comment,
  which nothing reaches until a concept does, Id3v1 having no `field()`.
- **Nonstandard but common fields**, and whether a cell should consult them, as the deferred
  entry on `year` and Vorbis `YEAR` asks.
- **Adding a cell changes the plain concept.** A plain concept prefers among its cells, so a cell
  added later changes what the plain concept reads for every file with that source. Whether the
  table may change in that way as it grows, being a convenience by design, or whether such a
  change needs a stronger reason, should be decided before the first cell is added.
- **Each cell's cardinality bounds**, from what the formats allow and what the reader does.
- **The preference order** across sources, kept or revisited.

## Facts to gather

- Which fields real collections actually use, how often, and under which names and spellings.
- The edge whitespace, trailing NULs and blank fields the [trimming](trimming.md) line asks
  about, which the same survey can count.

## Done when

The rationale records how a concept earns its place, the identifier documentation holds the table
as grown, and the catalog and its tests match.

## Log

- 2026-10-07 -- Split from sources-and-fields, which keeps only the minimum concept table.
