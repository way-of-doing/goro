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

## Open questions

These are deliberately left open. Each one should be revisited once the feature it
concerns is implemented and has been run against a real collection, since that is
the only thing that can supply the numbers the decision needs.

### Caching compiled regular expressions

The right operand of `~=` need not be a literal. Where it is, the pattern is known
when the predicate is read, and there is no question to answer: it is compiled once
while the pipeline is planned and shared, along with the rest of the immutable
predicate graph, by every concurrent invocation.

Where the pattern comes from tag data, it is known only per file, and a run over a
large collection may therefore construct a great many distinct matchers. Two things
make that worth thinking about rather than ignoring. Constructing a non-backtracking
matcher is materially more expensive than constructing a backtracking one, so the
engine chosen for its matching guarantees is the more costly one to build. And any
cache is shared mutable state reached from every concurrent pipeline invocation,
which is exactly what the architecture says the predicate graph must not hold, so it
would have to be introduced deliberately and made safe without becoming a point of
contention.

The questions to answer later are whether a cache is wanted at all, and if so what
it is keyed on, how large it is allowed to grow, and what it evicts. None of them
can be answered now, because the shape of the workload is unknown: a collection
whose files all carry the same pattern and one whose files each carry a different
one want opposite things, and it is not yet clear that either occurs often enough to
design for. The conservative reading is that a predicate matching against tag-supplied
patterns is a rare thing to write, and that the simplest implementation is therefore
the right one until somebody demonstrates otherwise.
