---
status: proposed
size: broad
touches: concepts/predicates.md, design/rationale.md, design/deferred.md
after:
branch:
---
# Mapping over values, and projections

## Intent

Find notation for applying something to every occurrence of a value, and in particular for taking a
part out of a value that has parts, before the language gains any such value. Every value in a
predicate is a bag of occurrences, each usable or unusable, with absence as the bag of none, so
every value is something a function can be mapped over, and the language already does so
implicitly: an operator meets each occurrence in turn, and a conversion converts each one. What it
has no way to say is "this, applied to each occurrence", or "this part of each occurrence".

The need comes from [pictures](pictures.md), where a picture has parts (a picture type, a media
type, a description, dimensions, the image), but the notation should not be designed around
pictures. Whatever compound type comes later should be served by the same notation, and the core
type system is meant to be settled once rather than extended case by case.

## Prior art to look at

- **jq:** paths and projections over JSON (`.[].name`), with iteration made explicit by `[]`, and
  `map()`, `select()` and the empty stream as the analogue of absence.
- **PowerShell:** member enumeration, where `$files.Name` projects a property out of every element
  of a collection and gives a single value when there was one, close to Goro's rule that a bag of
  one is indistinguishable from a simple value.

Others are worth a look once those two are understood: JMESPath's projections (`people[*].name`),
XPath's path steps over node sets, SQL's column projection, and LINQ's `Select`.

## Questions

1. **What a projection does to state and cardinality.** A part of an unusable occurrence is
   presumably unusable, and a part of an absent value absent; a part that a usable occurrence does
   not carry, such as a picture without a description, is absent or unusable by some rule. How do
   those combine across a bag?
2. **Explicit or implicit mapping.** PowerShell maps member access implicitly; jq makes iteration
   explicit. Goro's operators already iterate implicitly, with quantifiers to say how the results
   combine. Where does a projection sit among them, and do `ALL()` and `ANY()` still read naturally
   on a projected value?
3. **Spelling.** A projection must not collide with `::`, which joins a source to what it
   qualifies, nor with anything the grammar reserves. What reads well in a predicate written on a
   command line?
4. **Beyond projections.** Is mapping an arbitrary function worth having, or are projections and
   the existing implicit iteration all a predicate language needs?
5. **The cardinality-one invariant.** No construct may tell a simple value from a bag of one. Does
   every candidate notation keep that?

## Done when

A notation for projections, and for mapping more generally if wanted, is decided and recorded in
the rationale with the prior art weighed, and written into predicates.md once a compound type
needs it. A decision that projections are not wanted is an equally good end.

## Log

- 2026-10-10 -- Proposed at PJ's suggestion during flac-support, as groundwork for
  [pictures](pictures.md), which needs a type with parts. Starting points: jq and PowerShell.
