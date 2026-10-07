---
status: landed
size: focused
touches: concepts/predicates.md, concepts/evaluation.md, features/builtins/identifiers.md, design/rationale.md, design/deferred.md, faq.md, testing.md
after:
branch: line/not-equal-on-multivalues
---
# What `!=` means on a multivalue

## Intent

Settle the question recorded in `deferred.md`: `genre != "metal"` is true for a file tagged both
"metal" and "rock", while nearly everybody writing it means "not metal".

The decision taken is to make `!=` an error when an operand is not definite and carries no explicit
`ALL` or `ANY`, with a diagnostic offering `NOT (x == v)`, where an absent value counts as "not v",
or `ALL(x) != v`, where it does not. That forces the one choice people get wrong without noticing.

Companion question: `file::path`, `file::name` and `file::size` are documented as always exactly
one occurrence, yet are not definite, only because no identifier is. Declaring them definite
would keep `file::path != "..."` free of a needless quantifier.

## Done when

The meaning of `!=` is specified, the deferred entry is removed, and the rationale
records the reasoning.

## Log

- 2026-10-04 -- Specified `!=` as decided in the Intent. An operand of `!=` that is not definite
  must carry `ALL` or `ANY`, and otherwise the predicate is rejected with a diagnostic offering
  `NOT x == v` and `ALL(x) != v`. Evaluation is unchanged, which is why `concepts/evaluation.md`
  needed no edit: a quantifier on `!=` means what it means anywhere else, and `ANY(x) != v` is
  exactly the old `x != v`. The rule rests on definiteness, not on a value holding several
  occurrences, because the error has to be decided from the text, and because absence alone
  separates the two readings (`id3v1::genre` is never a multivalue and still needs the choice).
  Edits: `concepts/predicates.md` (the rule under Comparison operators; "definiteness matters in
  two places"; every example the rule invalidates rewritten, including general rule 1's
  `id3v2::track != 5`, now `< 5` and `>= 5`; `ANY` is no longer "never strictly necessary"; the
  error list and static constraints), `design/rationale.md` (a new Q placed after the one on the
  logical operators, recording the alternatives rejected: documentation only, a universal default
  for `!=`, removing `!=`), `testing.md` (static scenarios for the rule; the `!=` against `NOT ==`
  row now quantified), `faq.md` (how to say "not metal"), and the `deferred.md` entry removed.
  The companion question was settled the same way: `file::path`, `file::name` and `file::size`
  are declared definite, each resolving to exactly one occurrence for every file evaluated (a file
  whose metadata cannot be had is unreadable and never evaluated). `file::duration` probably
  qualifies on the same grounds, but whether a readable file can lack a duration is unstated, so
  it was left out. Reviewed and landed the same day.
