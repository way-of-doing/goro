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

- 2026-10-05 -- Seams mapped and the five far-reaching decisions settled; contract written.
  The working design is [design/predicate-runtime.md](../design/predicate-runtime.md): eight seams
  (S1–S8) with the concerns from both sides of each, seven concern groups (G1–G7), and the
  decisions, each with the options considered. PJ decided:
  - **D1, typed self-evaluating tree.** `Value<T>` and `BoundExpression<T>` over the datum types
    `string`, `decimal`, `ByteCount`, `Duration`, `bool`; the binder is the one place Goro's types
    become C# type parameters, so the evaluator cannot mix types and still compile. Definiteness
    stays a flag each node computes, as typing it too would double every function node.
    Rejected: an untyped tree with a separate evaluator (type errors become wrong answers at run
    time), and compilation to delegates (no measurable gain, harder to read).
  - **D2, synchronous evaluation over lazily loaded file data.** TagLibSharp is synchronous, so
    async would be ceremony; prefetching would make files unreadable that the predicate never
    needed to read, against warnings.md and the FAQ.
  - **D3, a file found unreadable mid-evaluation reports only its file warning**, any data warning
    met on the way being dropped. This was a gap in the specification: warnings.md now says so, and
    testing.md has the scenario.
  - **D4, the whole language over the `file` namespace**, with warnings, exit codes, `--filter`,
    `--no-warn` and `--strict-exit-code` for both commands. The tag namespaces are declared in the
    catalog but not resolved; they are to be a line of their own.
  - **D5, the first syntax error, then every semantic error**, the binder using an error type to
    silence cascades. Full parser recovery was judged not worth it for one-line inputs with no
    editor; recovery limited to the mistakes the parser already diagnoses (borrowed operator
    spellings and the like) was considered and declined by PJ, as error nodes for a modest gain.
  - Defaults accepted: hand-written recursive descent, no new packages, separate syntax and bound
    trees, structured diagnostics with suggestions composed from the user's text, borrowed operator
    spellings lexed as tokens, canary tests for the four regex construct families (all four throw
    `NotSupportedException` under the non-backtracking engine on .NET 10.0.11, malformed patterns
    `RegexParseException`), code under `Goro.Predicates`.

  The contract is under `src/Goro/Predicates`, with ownership per file as the design document's
  table gives it. `Value<T>`, `IdentifierName` and `NumberText` are implemented and tested as part
  of it; every other body throws `NotImplementedException`. `dotnet test`: 143 passed.

  Work is in two waves, since binder tests want predicate text and so need the real parser: wave 1
  is G1, G2 + G3 (one worker), G5, G6 and G7; wave 2 is G4 and the `--filter` integration.

  Next step: PJ's review of the contract, then commit it and start wave 1.
