---
status: landed
size: focused
touches: concepts/predicates.md, concepts/evaluation.md, features/builtins/identifiers.md, design/rationale.md, design/deferred.md, testing.md, faq.md, src/Goro/Predicates, tests/Goro.Tests/Predicates
after:
branch: line/preferred
---
# `PREFERRED()`

## Intent

Add a function that chooses among several candidates, with exactly the semantics the global
namespace already uses to choose among tag formats, so that how Goro resolves `artist` can be
written in predicate syntax and anybody can do the same for whatever the global namespace does not
cover.

`PREFERRED(e1, e2, ...)` takes two or more arguments of one type, under the usual type-matching
discipline for function arguments. Its result is:

- the first argument holding at least one usable occurrence, taken entire, unusable occurrences
  included;
- failing that, the first argument that is not absent, all of whose occurrences are therefore
  unusable;
- failing that, absent.

Arguments are evaluated left to right, and evaluation stops at the first one holding a usable
occurrence: the arguments after it are not evaluated at all, so they load nothing and warn about
nothing. This is the same short-circuit `AND` and `OR` have, justified the same way, by an order
the person writing the predicate chose and can see. The function reads only the state of an
occurrence, never its content, so it consumes nothing and never warns; the unusable occurrences of
an argument it passes over are discarded silently, as the global namespace discards them. The
result is definite when every argument is.

The name promises a choice and nothing more. `FIRST_USABLE` was considered and rejected because
the result can be unusable; returning absent instead in that case was rejected because it would
pass a defect off as absence, and the global namespace could no longer be written with the function.
`RANKED` was rejected because it suggests an ordering of all the candidates rather than a pick
among them.

Each global identifier is then documented as equal to a `PREFERRED()` over the format-specific
identifiers in order of preference, such as
`artist` = `PREFERRED(vorbis::artist, ape::artist, id3v2::artist, id3v1::artist)`, replacing the
prose description of best-effort resolution, and the identifier documentation speaks of
preference order rather than rank order. The two must agree in every observable respect,
including which formats are consulted.

Two clean-ups belong to the same line.

**COALESCE goes.** It was dropped, and `FALLBACK()`'s default restricted to a literal, leaving
cross-format preference unserved, because a construct choosing among candidates was said to raise a
question with no good answer: whether the candidates it did not use are evaluated. The global
namespace answered that question long ago, and this function adopts the answer. The deferred entry
is removed entirely, and the rationale's arguments that rest on the question are rewritten or
dropped: the second and third paragraphs of the question on why `FALLBACK()`'s second argument
must be a literal, and anything elsewhere that cites COALESCE. `FALLBACK()` keeps its literal
default, on its own ground of being total.

**"Unusable data never selects a file" stops being a guarantee.** It was never meant as one. It
describes what the operators try hard not to do -- a comparison that could not read its data does
not manufacture an answer -- while the person writing a predicate keeps complete control and may
write whatever they like: state tests, `FALLBACK()`, comparing a condition with `FALSE`, and now
`PREFERRED()`, all of which make a predicate's result depend on unreadable data by design.
Stated as a guarantee it is touchy, and it is already untrue as worded: `(year < 2000) == FALSE`
contains no `NOT` and still turns a false into a true, so the rationale's "nothing but negation
can turn false into true" is wrong, and the global namespace's fall-through breaks the stronger
form ("true only if it would be true whatever the unreadable data had held"). The places that
state it are reworded as design intent about the operators' defaults:

- `design/rationale.md`, in the questions on failed conversions, on why an operator meeting
  unusable data produces an unusable boolean ("the promise", "keeps the promise everywhere", the
  "nothing but negation" claim), and on the global namespace's fall-through;
- `testing.md`, the "Unusable data never selects a file by default" entry under predicate
  semantics, restated as a property of the operators rather than of predicates;
- `tests/Goro.Tests/Predicates/Evaluation/UnusableNeverSelectsTests.cs`, whose two properties are
  still worth keeping as tests of the operators' three-valued logic, but whose names and comments
  present them as a guarantee, and whose first comment repeats the "nothing but negation" claim.
  Its generator already avoids the constructs that break it; that is to be stated as the scope of
  the property rather than left implicit.

## Done when

`PREFERRED()` is specified under Functions in the predicate documentation and in the evaluation
document, the global identifiers are defined through it, COALESCE appears nowhere outside session
records, the unusable-data wording is intent rather than guarantee everywhere it occurs, the
rationale records why the function's semantics are the global namespace's, and the function is
implemented with tests: the global namespace's worked table replayed through `PREFERRED()`, each
global identifier agreeing with its expansion, and the short-circuit asserted through warnings and
through a later argument whose data would have made the file unreadable.

## Log

- 2026-10-05 -- Proposed after a review of the spec found that the global namespace already
  answers the question COALESCE was dropped over. The semantics in the Intent were agreed in
  discussion, and so was the name, as recorded there; nothing is decided beyond them. Next step:
  pick up the line.
- 2026-10-06 -- Line started. Handing it to a Sonnet `line-worker` was considered and decided
  against, by the directions README's own tests. Most of the line is normative prose (seven
  documents), which stays with Opus or the coordinator. The code's likely mistakes would be silent:
  dropping the unusable occurrences of the chosen argument instead of taking it entire, or
  evaluating every argument eagerly, both give well-formed answers. Nothing independent checks the
  short-circuit, since the worker would write those tests as well. And the line is focused and
  sequential, spec before code, with no parallel phase, so a worker starting cold would cost more
  than the piece. The coordinator does it. Next step: read the touched documents and the global
  namespace's resolution code, then propose a plan with the spec edits first, as the contract,
  and the implementation and tests after them.
- 2026-10-06 -- Spec and implementation done, all by the coordinator; nothing is committed yet.
  - **Decided with PJ: a global identifier has its expansion's value but keeps its own source.**
    `artist` has exactly the value `PREFERRED(vorbis::artist, ape::artist, id3v2::artist,
    id3v1::artist)` has, consulting the same formats, but its unusable occurrences have `artist`
    as their source, as evaluation.md says of every identifier. The two could not agree on
    warnings otherwise: a warning quotes what was written, and the expansion would dedupe with
    any other mention of `vorbis::artist`. The docs therefore say "has the value of", and the
    deferred question of naming the format in a warning stays open.
  - **Spec.** `PREFERRED()` is specified in predicates.md (with the worked table moved there) and
    in evaluation.md. identifiers.md gives each global identifier's expansion. The rationale has
    a new question on why the function's semantics are the global namespace's, and the FALLBACK
    and range-endpoint questions are rewritten without COALESCE. The deferred COALESCE entry is
    removed. The unusable-data wording in the rationale, testing.md and the test file is now
    intent about the operators, and the "nothing but negation" claim is corrected with
    `(year < 2000) == FALSE`. The FAQ answers "a global identifier picked the wrong value" with
    `PREFERRED()`.
  - **Code.** One choice, `Values/Preference.Preferred`, serves both the `Preferred<T>`
    evaluation node and a new `PreferredBinding<T>`. The global namespace's bindings are now
    `PreferredBinding`s over the format identifiers' bindings, so they will follow the tag line
    when it replaces those, with no further change. The binder takes a minimum arity for
    `PREFERRED` and types it by its first argument that is not a number literal.
    `FallbackTypeMismatch` became the shared `ArgumentTypeMismatch`, and `TooFewArguments` is
    new.
  - **Tests.** 2107 pass, 53 of them new:
    - the worked table and every other two-argument combination;
    - the short-circuit, asserted through resolution counts, an unreadable later argument and
      warnings;
    - the compiler's arity, types, stand-ins and definiteness;
    - each global identifier's candidates being the documented four in the documented order;
    - `PreferredBinding` agreeing with the node over every combination of bags.

    `UnusableNeverSelectsTests` is now `OperatorDefaultsOverUnusableDataTests`, with its scope
    stated.
  - **Open.** That a global identifier agrees with its expansion over real tags can only be
    checked once the tag line lands; the structural test is what stands in for it until then.
    design/predicate-runtime.md still says "rank order", left as design history. Next step:
    PJ's review, then commit on the branch and, if PJ approves, merge to main.
- 2026-10-06 -- Landed: PJ asked for it to be committed and completed, so it is marked `landed`
  and merged into main. Left for later:
  - checking a global identifier against its expansion over real tags, which waits for the
    tag-reading line;
  - `PREFERRED()`'s bounds: it is never absent if any argument never is, which
    cardinality-and-constants anticipates stating;
  - "rank order" in design/predicate-runtime.md.

  Next on the recommended queue: unanswered-predicates.
