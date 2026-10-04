---
status: active
size: sweeping
touches: architecture.md, implementation.md, src/Goro, tests/Goro.Tests
after:
branch: line/predicate-runtime-architecture
---
# The predicate runtime

## Intent

Create and specify a concrete architecture for the whole predicate runtime: the compiler and its
stages (lexer, parser, static analysis), the evaluator, the warning sink, and the seams between
them and the pipeline, down to the level of detail at which implementation can start without
further design.

## Instructions

Remember that Goro's architecture should not just enable the end goal; we hold it to a very high
standard, and want it to be exemplary from an engineering perspective and also a great learning
experience for those who dive into something similar for the first time. Elegance is a great bonus.

The design will include several parts, and some of them will not interface at all. Before starting
on each one, consider the interface seams and collect concerns from all sides regarding each
interface; this common context and any related tensions should be considered while designing the
interfacing components, while the design of non interfacing components will be indifferent.
Grouping the concerns this way enables some safe workflow parallelism: we can have as many
parallel lines of work as distinct concern groups, since the decisions in each line cannot affect
any other.

Some design decisions will have far-reaching consequences; in such cases, explore the reasonable
options and provide a rich background context and prognosis for each one. Do not stumble on, but
pause and request feedback.

## Done when

The architecture is written down at the level of stages, responsibilities and the data that
crosses each seam, a code skeleton with tests realises it, and `architecture.md` and
`implementation.md` describe it in their respective terms.

## Log
