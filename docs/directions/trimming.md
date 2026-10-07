---
status: proposed
size: focused
touches: concepts/normalization.md, concepts/predicates.md, concepts/evaluation.md, features/builtins/identifiers.md, design/rationale.md, testing.md, faq.md
after: sources-and-fields
branch:
---
# Whitespace at the edges of a string

## Intent

Decide, in one place and from the facts, what whitespace at the start and end of a string means
everywhere Goro meets it. Today trimming belongs to the interpreted namespaces. The
[sources-and-fields](sources-and-fields.md) line removes them: a field is the text as recorded,
and only concepts interpret. So the question of where edge whitespace stops mattering has to be
answered again -- for fields, concepts, comparison, conversion and literals alike -- and answered
the same way throughout rather than piece by piece.

## Where it stands

These are the rules as written today:

- **Interpreted identifiers** trim whitespace from both ends of the recorded text before using it,
  and leave whitespace inside alone (identifiers.md, rationale).
- **A string-typed field holding only whitespace** is absent, so `artist IS ABSENT` finds an
  artist frame of three spaces. A field of any other type holding only whitespace is unusable.
- **A split value**, such as a `TCON` frame reading `Rock; Metal`, has each part trimmed.
- **Date-shaped and tracknumber-shaped data** is trimmed before it is parsed.
- **Id3v1 padding** is stripped in every namespace, the raw ones included. It is not trimming:
  padding is not part of what was recorded.
- **Raw namespaces** trim nothing. Under sources-and-fields this becomes `field()`, which reads
  the text as recorded.
- **`AS` from text** converts only when the text is the literal and nothing else, so `" 5"` does
  not convert to a number.
- **Normalized comparison** keeps edge whitespace. It removes default-ignorable characters, among
  them zero-width spaces (normalization step 3), and NFKD turns no-break and ideographic spaces
  into plain ones (step 2).
- **`LITERALLY`** keeps everything.
- **String literals** keep their whitespace exactly: `"foo"` and `"foo "` are not equal.
- **`file` strings** are never trimmed. A path begins with `/` and a name ends with its extension,
  so in practice their edges are not whitespace.

## Questions

- **Normalized comparison.** Whether it ignores whitespace at the edges, as it ignores case and
  accents, leaving `LITERALLY` to see it -- and if so, whether for literals as well as for data.
  `normalization.md` already has a step that applies only to data read from files. Cases that
  bear on it:
  - `artist BETWEEN "the ".."the "`, meant as "begins with the word The", becomes
    `"the".."the"` if literals are trimmed, and matches Thelonious Monk;
  - `x == "foo "` can never be true if only data is trimmed;
  - `field =~ r"\s$"`, to find stray whitespace, never matches in normalized mode;
  - a field of spaces compares equal to `""`;
  - a value with a leading space no longer sorts before everything else;
  - a file named `" leading.mp3"` equals `"leading.mp3"`.
- **`AS` from text.** `vorbis::field("BPM") AS NUMBER` on `"120 "` is unusable and warns under
  today's rule, and trailing spaces are common. Ignoring edge whitespace there reopens a rule the
  as-operator line has just settled, so the two questions are best answered together.
- **Concepts.** Whether they still trim once comparison does. The absence of a blank field and the
  parsing of numbers both depend on it, so probably yes, but the reason should be stated rather
  than inherited.
- **Order among the normalization steps.** After step 3 at the earliest, or a zero-width space at
  the end hides a trailing space.
- **Which characters.** `White_Space` is the obvious set. NUL is not in it, and trailing NULs are
  a known habit of some taggers.
- **Line breaks** at the edges of lyrics and comments.

## Facts to gather

The decision should rest on what collections actually contain rather than on cases thought up in
the abstract:

- how often edge whitespace, trailing NULs and blank fields occur in a real collection, per format
  and per field;
- what the tag reader hands Goro for each -- TagLibSharp, or Goro's own reader if
  sources-and-fields moves to one;
- which tools write them, where that can be told.

## Done when

The rationale records what edge whitespace means and why, for each of the situations above; the
normalization, predicate, evaluation and identifier documents say so; and the testing scenarios
cover each case, including the ones under Questions.

## Log

- 2026-10-07 -- Split from sources-and-fields, where trimming came up in several places at once
  and was judged to deserve a decision of its own made from the facts.
