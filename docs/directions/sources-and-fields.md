---
status: active
size: broad
touches: concepts/predicates.md, concepts/evaluation.md, features/builtins/identifiers.md, design/rationale.md, design/deferred.md, architecture.md, testing.md, faq.md, src/Goro/Predicates
after: preferred, as-operator, semantic-analysis-passes, cardinality-and-constants
branch: line/sources-and-fields
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
- 2026-10-07 -- Line started. Proposed a model for typing what files record, built around an
  opaque type, `blob`, suggested in discussion. Fields are typed by what the format records:
  string for text, blob for anything else, and `id3v2::field()` typed statically by its frame
  identifier. Concepts are typed by their meaning. A blob is accepted only by `IS`, `COUNT()` and
  as the operand of `AS`, with no targets yet, so a blob concept can later be promoted to a richer
  type without making any valid predicate invalid or changing its result, provided the promotion
  keeps every occurrence's cardinality and state and agrees with the conversions from blob. A
  concept whose type is not blob never changes type. Not yet agreed; open within it are the
  type's name, keeping blobs out of `PREFERRED()`, and whether an APE binary item read through
  `ape::field()` is an unusable string. Next step: settle the proposal, record it in the
  rationale, and decide whether this line absorbs non-text-tag-data.
- 2026-10-07 -- Pressing on APE binary items found a flaw in the proposal: any conversion out of a
  promotable type binds every future promotion to keep it, so a blob that converts cannot be
  promoted freely. Proposed splitting it in two: `opaque`, the type of a concept whose type is
  not decided yet, accepted only by `IS` and `COUNT()` and promotable; and `blob`, recorded bytes,
  the type of fields that are not text, never promoted, and therefore free to gain conversions
  such as `AS STRING("iso-8859-1")`. Leanings: the accessor a predicate writes chooses the type,
  and what kind of item the file holds decides only usability. APE gets `ape::bytes()` beside
  `ape::field()`. Conversion targets take literal parameters, `AS STRING("latin1")`, and a
  parameter supplies only what the data cannot tell, so `AS IMAGE(png)` is discouraged. Not yet
  agreed. Next step: settle the split and the names, then continue with the remaining questions.
- 2026-10-07 -- Decided: two types, `opaque` and `blob`, with those names. Weighed parameters on
  conversion targets. Every predicate valid today keeps its meaning with them, since `AS target (`
  is a syntax error today and a blob is never promoted, so they need not land with this line;
  nothing produces a blob until the tag sources are read. Leaning for their spelling: unquoted
  words, `AS STRING(iso-8859-3)`, made possible by one context-dependent token rule. Inside the
  parentheses after a conversion target, a word may also contain hyphens. The lexer already looks
  back at the previous token for clock-form durations. Hyphens in names everywhere were rejected:
  `a-b` would then be a name, which costs any future arithmetic its minus. Next step: confirm
  that spelling and when parameters land, then continue with the remaining questions.
- 2026-10-07 -- Wrote up conversion parameters as a brief of their own,
  [conversion-parameters](conversion-parameters.md). On APE: the format allows one item per key,
  keys compared without regard to case, and an item is text, binary or a locator, so one key never
  holds both kinds in a tag. A probe against TagLibSharp 2.3.0, with two items under one key, one
  text and one binary, in either order, and with keys differing in case, found the last item kept
  every time, as identifiers.md already says. The kind varies only from file to file, which the
  two views handle. Leaning: `ape::bytes()` gives one occurrence per value of a text or locator
  item and one for a binary item, so it always has the same number of occurrences as
  `ape::field()` with the same key, and the two differ only in type and in which occurrences are
  usable. The probe also found that TagLibSharp decodes a text item as UTF-8 as it reads it and
  keeps nothing else: `Mot\xF6rhead` comes back with U+FFFD in place of the `ö`, and its
  `Render()` writes the replacement character. So the bytes of a text item can only be had by
  reading the APE tag ourselves. If the tag-reading line does that, invalid UTF-8 could become
  unusable rather than quietly garbled, and an older APE tag could be decoded as ISO-8859-1;
  both feed the damaged-data question. Next step: confirm the cardinality rule for
  `ape::bytes()`, then continue with the remaining questions.
- 2026-10-07 -- Agreed: `bytes()` gives one occurrence per item or frame and never splits a
  value. Splitting text on NUL would be Goro guessing in the one place meant to offer the most
  control, and nothing in the language could do more with split bytes than with strings. So
  `ape::bytes(k)` never holds more than one occurrence, APE keys being unique in a tag. Proposed:
  `field()` is a string and `bytes()` a blob for every source that has keys -- `id3v2`, `ape`,
  `vorbis` -- so a type depends on the function alone and never on an argument. `id3v2::field()`
  on a frame that is not text is then an error offering `id3v2::bytes()`; APE cannot know an
  item's kind before reading, so a binary item read through `ape::field()` stays unusable.
  `PREFERRED()` takes blobs, which are never promoted. Proposed for opaque values: each opaque
  concept has a type of its own, the same only within that concept, so `PREFERRED()` over cells
  of one concept is valid and stays valid after promotion, and no construct needs to leave opaque
  out. Proposed calling `field()` and `bytes()` _source functions_ in prose. Still open: frame
  addressing, trimming, interpretations as `AS` targets, the checks on arguments, the `file`
  source written unqualified, the leading `::`, Id3v1's byte-valued concepts, and the
  damaged-data probe. Next step: frame addressing and the checks on arguments, which are
  coupled.
- 2026-10-07 -- A type of its own for each opaque concept withdrawn: it would make the list of
  types an open family rather than a closed list. Back to one `opaque` type, accepted only by
  constructs that take any type on its own (`IS`, `COUNT()`), so it is kept out of `PREFERRED()`;
  that costs only reordering the cells of one concept. The types are then string, number,
  bytecount, duration, boolean, blob and opaque. Decided: frames are addressed by their v2.4
  identifier; the `file` source is never written unqualified. Deferred: interpretations as `AS`
  targets. A probe of TagLibSharp 2.3.0 shows why addressing by v2.4 identifier felt shaky:
  - In a v2.4 tag, `TYER` and `TDAT` are left as they are.
  - In a v2.3 tag, `TYER`, `TDAT` and `TIME` are merged into a `TDRC` of `1991-01-02T12:00`,
    which contradicts identifiers.md. `TORY` becomes `TDOR`, `RVAD` disappears, and an `IPLS`
    holding `producer` and `Someone` comes back as `producer` alone.
  - In a v2.2 tag, `PIC` becomes an `APIC` with its content converted.

  Proposed rule: a frame is addressed by a fixed table keyed on its own identifier, whatever the
  tag's revision, and is renamed to its v2.4 identifier only where v2.4 holds the same data in the
  same form: `TYER` and `TORY` are, `TDAT`, `RVAD` and `PIC` keep their own names. Writing a
  renamed identifier is an error offering the v2.4 one. This holds only if Goro lists the frames
  itself rather than taking TagLibSharp's converted view, as APE text items already needed.
  Proposed checks on arguments: a frame identifier's shape and the rename table, `field()`
  refused for frames that are not text, and a description as the second argument of `TXXX`,
  `WXXX`, `COMM` and `USLT`, optional and matched case-insensitively; no language argument and no
  tables of well-known names. Trimming in normalized comparison: examples that would break it
  were gathered. The largest is `field() AS NUMBER` failing on `"120 "`, which reopens the
  as-operator rule that text converts only when it is the literal and nothing else. Leanings:
  remove the leading `::`; Id3v1 needs one new concept, the genre byte, and loses only untrimmed
  text and the year as text. Next step: settle trimming and `AS` together, then confirm the
  addressing rule.
- 2026-10-07 -- Addressing by a table keyed on each frame's own identifier is accepted as a first
  attempt. Whether to keep TagLibSharp is to be reconsidered once its handling of Vorbis is
  looked at as closely as APE and Id3v2 have been. Decided: the checks on arguments as proposed,
  and removing the leading `::`. Trimming split out to its own brief, [trimming](trimming.md),
  for a decision made from the facts; this line settles only that `field()` reads the text as
  recorded. Proposed: no concept for Id3v1's genre byte. `id3v1::genre` is a string, being a cell
  of `genre`, so the byte would need a name of its own. All it adds over `id3v1::genre IS
  UNUSABLE` is the exact index of a byte the table does not define, so Id3v1 needs no further
  concepts. Next step: probe TagLibSharp's Vorbis reading, then decide whether Goro reads tags
  itself.
- 2026-10-07 -- Decided: no concept for Id3v1's genre byte for now; it is easy to add if it is
  ever wanted.
- 2026-10-07 -- Agreed:
  - `field` and `bytes` as the names;
  - "source functions" in prose;
  - a source function written without a source is an error;
  - `vorbis::bytes()` exists;
  - an APE binary item read through `ape::field()` is unusable.

  The promise that source functions give the data as recorded is kept, even if that means Goro
  reads some formats without TagLibSharp. That decision, the damaged-data question and the
  growth of the concept table moved to a new brief, [concept-table](concept-table.md), since they
  rest on the same discovery. No `picture` concept: nothing concrete is wanted of one yet, and
  asking anything of a picture would need values with properties and syntax of their own. This
  line keeps the bare minimum of concepts. Proposed minimum: today's `artist`, `album`, `title`,
  `genre`, `year`, `track` and `comment`, reading the fields they read today, except that:
  - the Id3v2 cell of `comment` reads only the `COMM` frame with an empty description, so
    iTunes's `iTunNORM` is not a comment;
  - `comment` has no Vorbis cell yet;
  - Vorbis's `description` goes, `vorbis::field("DESCRIPTION")` reaching it.

  With no opaque concept, `opaque` would have no values, so it is proposed to enter the
  specification with its first concept and to live in the rationale until then. Next step:
  confirm the minimum, then write the rationale entry.
- 2026-10-07 -- The reader and damaged-data questions moved to a brief of their own,
  [mp3-support](mp3-support.md), named after flac-support: the same kind of questions for the one
  format Goro hashes today, whose tags nothing reads yet. flac-support and ogg-support now refer
  to it for damaged data, and concept-table keeps only the growth of the table, after
  mp3-support. Decided: `comment` leaves the minimum, not being global today, until it has had
  more homework, which concept-table records. `opaque` stays out of the specification until a
  concept needs it; the idea is kept in this Log and goes to the rationale with that concept. The
  minimum is now `artist`, `album`, `title`, `genre` and `year`, with `track` to confirm: it is
  not global today either, but its interpretation is fully specified. Next step: settle `track`,
  then write the rationale entry.
- 2026-10-07 -- Decided: `track` stays. The minimum concept table is `artist`, `album`, `title`,
  `genre`, `year` and `track`, each reading what it reads today. The four new briefs land on main
  with this line. Every design question is now settled here or moved to a brief of its own. Next
  step: the writing phase -- the rationale, then the normative documents, then the catalog, parser
  and binder -- proposed as a plan before it starts.
- 2026-10-07 -- Step 1 written: the rationale and the normative documents. `predicate-runtime.md`
  is left alone, being the record of a landed line. Choices made while writing that had not been
  discussed:
  - `id3v2::bytes()` gives each frame's content without its frame header. What it gives for a
    frame the tag unsynchronises, compresses or encrypts is added to mp3-support's questions,
    with `testing.md`'s policy of recording a reader's known deviations as skipped tests.
  - Frame identifiers are matched without regard to case.
  - The arguments of source functions are string literals.
  - Two calls differing only in the case of a name their source matches without regard to case
    are one warning source.
  - `FALLBACK()` is named as refusing blobs, though that already follows from there being no blob
    constant.
  - Removed from the rationale: the entries on Id3v1's raw genre and track typed as numbers, and
    on quoted names. The PREFERRED() entry keeps "global identifiers" for the history it
    tells.
  - The FAQ's junk-genre answer becomes `id3v1::genre IS UNUSABLE`.
  - `testing.md` gains rows for frame renaming, descriptions, the new errors, and a short table
    for APE.

  The code still implements namespaces. Next step: review of step 1, then a file-by-file plan
  for the catalog, parser and binder.
- 2026-10-07 -- Asked whether `field()` always being a string narrows how richer types can enter
  the language. There are three ways in, not two: new concepts, new conversion targets, and new
  source functions, each of which only makes valid what was an error. `bytes()` reaches every
  item, frame or comment whole, so nothing a keyed source records is out of reach of a later
  reader. The real limit is that a conversion knows the value and not where it came from: a blob
  does not say whether it is an `APIC` body, an APE cover item or a FLAC picture block. A
  conversion out of a blob can therefore decode only what is the same everywhere, such as text in
  an encoding or base64, unless a parameter names the layout. Structure particular to a format,
  under a name the predicate chooses, needs a construct that knows its source: a further source
  function. Strings in an unordered bag also lose structure that spans values, such as the
  role-and-person pairs of `TIPL`; `bytes()` keeps it. Nothing decided; the observation goes to
  the rationale only if wanted.
- 2026-10-07 -- Step 2 built: the catalog, parser and binder follow the new specification, and
  2460 tests pass, 143 more than when the line started. A call of a source function has only
  literal arguments, so the binder resolves it whole, into a declaration like an identifier's.
  From there on the two are one thing, `SemanticRead`, to every analysis and to the evaluator.
  What changed:
  - **Names.** `IdentifierName` is now a source and a name. `SourceCallName` is its counterpart
    for calls. Both are `DeclaredName`s, compared without regard to case, which is what makes
    `vorbis::field("mood")` and `vorbis::field("MOOD")` one warning source.
  - **Catalog.** `BuiltInCatalog` holds the table of six concepts, with the field each cell reads
    for the line that reads tags, and the source functions. `Id3v2Frames` holds the shape check,
    the rename table (three v2.3 frames and 54 of v2.2), the v2.2 frames that keep their names,
    the text-frame rule, and the four frames that take a description.
  - **Bindings.** Tag bindings stay not implemented.
  - **Removed.** Open and closed namespaces, quoted parts, nested names and the leading `::`.
    Each of the old spellings is answered by a diagnostic with a suggestion where there is one.
  - **Docs.** One `testing.md` row now says that `NOT::x == 1` is the leading-`::` error.

  Not done: the canned source function the plan offered the test catalog, since no test
  evaluates a source call. Next step: review of steps 1 and 2, then commit and land the line.
