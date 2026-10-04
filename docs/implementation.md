# Implementation notes

This document holds concerns that belong to how Goro is built rather than to what
it does. It is distinct from its neighbours in a specific way:
[architecture](architecture.md) describes the components and what each one is
responsible for, and [deferred features](design/deferred.md) records behaviour that
was considered and postponed. What lands here is narrower than either: a
requirement or a question about resource management, data structure choice or
performance. The requirements are settled and the implementation must honour them.
The questions are deliberately unsettled, their right answers depending on how the
finished program actually behaves rather than on anything an armchair can supply.

Nothing in this document changes what a command does or what a predicate means. If
an entry here ever turns out to, it is in the wrong file.

## Requirements

### Streaming the JSON output

`-o json` produces a single JSON array, and that array must be written as results
arrive rather than assembled in memory and serialised at the end. A run may cover a
hundred thousand files, and holding every result to produce a document whose shape
was known before the run started would be waste with nothing to show for it. The
renderer emits the opening bracket, then each record with its separator as the
executor yields it, then the closing bracket.

One externally visible consequence follows, and is accepted: a run interrupted part
way through leaves output that is not a valid JSON document, the closing bracket
never having arrived. Wanting a parseable prefix while also being content with a
partial answer is a narrow combination, and the format that would serve it -- one
JSON object per line, valid at every prefix -- can be added as its own `-o` option
if a real need for it turns up. Streaming the array now forecloses nothing.
