---
status: active
size: focused
touches: concepts/predicates.md, concepts/evaluation.md, concepts/warnings.md, design/rationale.md, design/deferred.md, implementation.md, testing.md
after:
branch: line/drop-dynamic-regex
---
# Drop regex patterns that come from tag data

## Intent

Require the pattern of `~=` to be a literal, as the endpoints of a range and the default of
`FALLBACK()` already are, and then remove everything that existed only to support a pattern
arriving from tag data. The same reasons apply as for those two restrictions: a literal can be
checked before any file is opened, nobody has asked for the alternative, and restricting now
while relaxing later is the direction that breaks nothing.

It is quite possible that eliminating this feature allows simplification of other parts of the
spec which had become bloated or unwieldy just to support this. If these simplifications open
the door for advantageous redesign of other spec parts, the outcome of this work should clearly
describe such potential follow-ups.

## Done when

The specification says the pattern is a literal, every rule and passage that served a non-literal
pattern is gone or rewritten, the rationale records the reason next to the other two literal-only
restrictions, and the tests no longer cover the removed behaviour.

## Log
