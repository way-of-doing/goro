---
status: proposed
size: focused
touches: concepts/predicates.md, design/rationale.md, design/deferred.md, faq.md, testing.md
after:
branch:
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
