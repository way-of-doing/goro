---
status: proposed
size: broad
touches: concepts/predicates.md, concepts/evaluation.md, features/builtins/identifiers.md, design/rationale.md, design/deferred.md, architecture.md, testing.md, faq.md, src/Goro/Predicates
after: preferred, as-operator, semantic-analysis-passes, cardinality-and-constants
branch:
---
# Sources, concepts and fields

## Intent

Replace open, closed and raw namespaces with three constructs that each do one job.

A namespace does three jobs today: it says which tag format to read, it chooses a vocabulary --
Goro's names such as `track`, or the file's names such as `TRCK`, `MOOD` or `Album Artist` -- and
it chooses an interpretation, trimmed and parsed or raw. Every awkward rule around namespaces is
two of those jobs colliding: well-known names shadowing real fields, so that adding one later
changes what an existing predicate reads; raw counterparts that do not share their interpreted
identifier's name; `::raw` nested as though it were a place; quoted parts forcing the file's names
into identifier syntax; `TXXX`, `COMM` and `WXXX` needing a name in two parts; case-insensitive
matching imposed on names that belong to the format; and misspellings that silently match nothing.

**Sources.** `file`, `id3v1`, `id3v2`, `ape` and `vorbis` are sources: flat qualifiers, one level
deep. A source has no vocabulary of its own, and nothing nests under one.

**Concepts.** Goro's names -- `artist`, `track`, `year`, `genre` and the rest -- form one closed
table. A concept has a name and a type, and for each source that can supply it, a cell saying which
field it reads and how that field is interpreted; the existing rules for trimming, date-shaped data,
tracknumber-shaped data and genres become the interpretations cells refer to. `id3v2::track` is the
track concept read from Id3v2 alone, and plain `track` is the same concept across the tag sources
in preference order, which is `PREFERRED()` over its cells. A concept a source cannot supply, such
as `id3v1::albumartist`, is an error. Being closed, the table can grow without capturing anything a
predicate already reads, and a misspelled concept is an error with a suggestion.

**Fields.** `source::field("KEY", ...)` reads the source's own data as text, exactly as recorded.
It is a function, string-typed and never definite, and its arguments are literals, as the default
of `FALLBACK()` already is. The source decides how many arguments it takes and how a name is
matched: `vorbis::field("MOOD")`, `ape::field("Album Artist")`, `id3v2::field("TXXX", "MOOD")`.
"As recorded" stops being a place to go and becomes what `field()` returns.

Two general rules change to admit this. A function may be qualified by a source, so a name resolves
in its source whether or not a parenthesis follows it, replacing the rule that a qualified name is
never a call. And a function may read the file, which until now only identifiers did. `field()` is
specified as an ordinary function under the same heading as `COUNT()`, with no carve-out.

What goes: open and closed namespaces as a distinction, raw namespaces, nested namespaces, quoted
parts of identifiers, interpreted pass-through of arbitrary fields, the table of renamed raw
counterparts, the `[MOOD] calm` presentation of description frames, and the deferred entry on
addressing a single `TXXX`, `COMM` or `WXXX` frame. The deferred entry on misspelled identifiers in
open namespaces narrows to misspelled field names, where the person writing the predicate has
deliberately left Goro's vocabulary.

The tag sources are not yet implemented -- the runtime declares them and binds them to nothing --
so this changes the catalog, the grammar and the binder, and the line that implements the tag
sources starts from the result.

## Questions

Leanings are noted where there is one; none of these is decided.

- **The function's name.** `field` is the generic term and Vorbis's own. `tag` was the first
  candidate, but in Id3v2 terms the tag is the whole block. Per-format names, `frame` and `item`,
  are more exact and leave more to learn. Leaning: `field`.
- **How `id3v2::field()` addresses a frame.** By its v2.4 identifier whatever the file's revision,
  with obsolete identifiers such as `TYER` and `TT2` rejected statically with the one to write
  instead; or by the identifier on disk, so that revisions show through. Unified addressing is
  partly forced anyway, since TagLibSharp converts some frames as it reads them. Leaning: unified.
- **Where trimming goes.** A field is untrimmed. The choices are to accept that, to add `TRIM()`,
  or to make normalized comparison ignore whitespace at the edges of a string, alongside case and
  accents; `LITERALLY` would still see it. The last applies to every string comparison, including
  `file::path`, and means a field holding only spaces is present rather than absent. Leaning: the
  last.
- **Whether concepts can be written in predicate syntax.** Exposing the interpretations makes
  every cell a one-line definition and lets anybody apply Goro's interpretation to any field. As
  functions, a `YEAR()` beside the concept `year` would break the rule that no function shares a
  name with a global identifier. The [as-operator](as-operator.md) line removes that obstacle:
  interpretations become targets of `AS`, as in `id3v2::track = id3v2::field("TRCK") AS TRACK`,
  in a position where no concept can be named. Genre resolution changes how many occurrences
  there are, so it cannot be a target, and may not be worth exposing in any form.
- **Data that is not text.** If `field()` reads text alone, attached pictures and other non-text
  data reach a predicate through concepts of their own, and `id3v2::field("APIC")` can be rejected
  statically. An APE item flagged binary is known only once the file is read, so what `field()`
  gives for one, absent or unusable, still needs an answer. This overlaps the
  [non-text-tag-data](non-text-tag-data.md) line, which this one probably absorbs.
- **What is checked about a field's arguments.** Literal arguments make per-format checks cheap:
  the shape of an Id3v2 frame identifier, and the arguments each kind of frame needs -- whether
  `TXXX` without a description is an error or every `TXXX` frame, and whether `COMM` takes its
  language.
- **`field()` with no source.** Key spaces differ between formats, so it has no single meaning.
  Leaning: an error; choosing across formats is a concept, or a `PREFERRED()` of fields.
- **Id3v1, which has no keys.** It has no use for `field()`. Its recorded facts that are not text,
  the genre byte and the track byte, need names as concepts only Id3v1 can supply.
- **The `file` source.** Whether its concepts can be written without `file::`, as tag concepts can.
- **The leading `::`.** It exists to reach a global identifier hidden by a reserved word or a
  function name. With a closed vocabulary that Goro names, it may have nothing left to do.
- **The concept table itself.** Which concepts there are and which field each cell reads is content
  rather than foundation, but some of today's choices only made sense per namespace: Vorbis offers
  `description` where every other format offers `comment`. Each cell also declares its
  cardinality bounds, as [cardinality-and-constants](cardinality-and-constants.md) defines them.
- **What damaged tag data resolves to.** `warnings.md` says a file is unreadable if any one of its
  tags cannot be parsed. That was a placeholder, chosen in a batch of small leftovers as the rule
  needing no further rules, and nothing about the tag library informed it. What is known since:
  TagLibSharp 2.3.0 read the audio's duration through sixteen kinds of damaged Id3v2 tag built by
  hand, so with today's library the rule almost never fires. What nobody has looked at is the tag
  data that comes out of a damaged tag. If damaged frames are dropped or garbled quietly, the real
  failure is a damaged field resolving to absent or to wrong text with no warning, which is what
  the value model exists to prevent. Settling this starts with a probe: build damaged tags for
  each source, APE included, and record what each field and concept yields -- absent, wrong text,
  unusable, or an exception. With that in hand, choose between the whole file being unreadable and
  damage making the affected source's occurrences unusable, which would let `PREFERRED()` route
  around it, and make the warnings document describe what actually happens.

## Done when

The rationale records why a namespace was doing three jobs and what replaced it; the predicate
documentation, the evaluation document and the grammar speak of sources, concepts and fields, with
`field()` specified under Functions; the identifier documentation is a table of concepts with a
cell per source, followed by what `field()` takes for each source; every question above is settled
in the Log or by the spec; and the catalog, parser and binder are rebuilt to match, with tests,
leaving the reading of tag data to its own line.

## Log

- 2026-10-05 -- Proposed after a review of the spec traced the trouble with open namespaces to a
  namespace doing three jobs. The three constructs in the Intent, `field()` as an ordinary function,
  and functions qualified by a source were agreed in discussion; everything under Questions is
  open. Next step: settle the questions, starting with the function's name and frame addressing,
  then write the rationale entry.
