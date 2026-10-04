---
status: proposed
size: focused
touches: features/builtins/identifiers.md, concepts/predicates.md, testing.md
after:
branch:
---
# What non-text tag data resolves to

## Intent

Decide what tag data that is not text -- attached pictures, binary APE items, and frames such as
`POPM`, `PRIV`, `UFID` and `GEOB` -- resolves to, so that "unusable" keeps a single meaning.

It is possible that some of this data is interesting to query at Goro level, for example `POPM`.
For such kinds of data the best approach might be to introduce new types to the language, or
consider if it is reasonable to use existing types. Other types of data might be entirely opaque
to Goro, and those of course should be treated uniformly.

## Done when

The identifier documentation says what such data resolves to, and "unusable" means only data that
should have been of its type and is not.
