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

- 2026-10-05 -- Wave 1 built and collected: normalization (G1), lexer and parser (G2 + G3), the
  evaluator (G5), warnings, run outcome and the global options (G6), and the identifier catalog
  with the `file` namespace (G7). Each worker posted a plan at a checkpoint before writing code;
  the coordinator answered its questions and collected its changes. The full suite then ran
  1207 tests, all passing, none skipped. The five regex tests the evaluator had to skip without
  normalization were turned on at collection and pass. Decisions taken along the way:
  - **G1.** Literal and normalized preparation exactly as normalization.md says, and total. .NET's
    `string.Normalize` throws on U+FFFE, so text is normalized around it; U+FFFE is a stable code
    point under UAX #15, so this is exactly Unicode's result. .NET's invariant casing leaves `ı`
    and `İ` alone, so step 5 overrides them to `i`. The Default_Ignorable_Code_Point table is from
    UCD 16.0, which matches .NET 10's own tables. NFKD, NFC and casing come from the host's ICU.
    The `ё` rule reads "after" as the nearest preceding character that is not a mark, as the Latin
    and Greek rule does. Preparation is not idempotent: क + U+0345 + ा gives a different result on
    a second pass, so every datum is prepared exactly once. The cost is on par with the scratchpad
    candidate: about 110 ns for a mixed corpus, 30–40 ns for ASCII, with half the allocation for
    non-ASCII text. Span-based `TryNormalize` measured slower and is not used. NUnit's adapter
    silently drops test cases whose names contain U+FFFE or U+FFFF; `EscapedTestCase` works
    around it.
  - **G2 + G3.**
    - The lexer is strict maximal munch: `x == 5AND y == 1` and `-5BETWEEN -10..0` are valid
      predicates, while `5BETWEEN 1..10` (the bytecount `5B`, then `ETWEEN`) is not. Friendlier
      messages for a literal that runs into letters come from the parser when the parse fails.
    - The syntax diagnostics have kebab-case codes, and their suggestions are built from the
      user's own text: borrowed spellings, quoted and modified patterns, `~=` and `!~`, chained
      comparisons rewritten with `AND`, `IS NOT`, `IS NULL`, whitespace inside a literal, and
      qualified calls. A misplaced reserved word is reported without a suggestion, since nothing
      it could suggest exists.
    - The parser stops at the first error by way of an exception private to it.
    - `SyntaxPrinter` writes trees as S-expressions, and the parser is tested through it.
  - **G5.**
    - One routine, `OperatorEvaluation`, carries out evaluation.md's over/test/quantify for
      comparison, range and regex. Each operator supplies a struct saying how to prepare a datum
      and what to test.
    - Operands are evaluated in written order before the absence check, as evaluation.md writes
      it. Each usable datum is prepared once per operator evaluation. Comparison literals are
      prepared at evaluation time; range endpoints arrive prepared.
    - `EvaluationContext.Reported` is ordered by `SourceId`, keeping the first origin reported for
      each source, so a file's warnings do not depend on bag order.
    - Tests are hand-built bound trees over canned bindings. They cover operand-order symmetry
      (2,400 cases), the deduplication table, both logical tables, the state test table, and
      generated-predicate properties.
  - **G6.**
    - A stage returns a `FileOutcome`: what to render, what the file counts towards, and its
      warnings. Its factories make D3 hold by construction. A `RunTally` hands each file's warnings
      to the sink as one batch, and the sink drops suppressed categories on arrival.
      `ExitCodes.For` applies the precedence rules.
    - Code 20 is unreachable for `hash` and for `list` without a filter.
    - `--no-warn` takes a value only after `=`. Spectre would read the next argument as the value,
      so a bare `--no-warn` is rewritten before parsing.
    - Strict parsing is on, so a rejected command line returns 2, where Spectre gave 255 and
      silently accepted unknown options. An exception escaping a stage fails the run with 1.
    - In `hash`, any exception from the hasher means the file cannot be read: `-` or `null`, and
      one warning.
    - Discovery walks one directory at a time, so a directory that cannot be listed warns and the
      walk carries on. It follows symlinked directories as before, but never a link whose resolved
      target is the directory itself or one of its ancestors on the walk; such a link is skipped
      silently.
    - `GoroApp` is now the single composition root.
  - **G7.**
    - The catalog is one table per namespace, mirroring identifiers.md.
    - Tag identifiers are declared, but bound to a binding that throws `NotSupportedException`,
      which the tag line replaces.
    - `file::size` follows a symlink to its target, since `FileInfo` describes the link.
    - `file::duration` reads TagLib's audio properties, truncated to whole seconds. TagLib 2.3.0
      tolerated every one of sixteen shapes of damaged Id3v2 tag, so no deviation from warnings.md
      is known.
    - `id3v2::raw`, `ape::raw` and `vorbis::raw` written as identifiers are open-namespace fields
      named "raw".
  - **Process.** The harness removes an agent's worktree when the agent stops with nothing
    changed, which is exactly the state at a plan checkpoint. Each worker was therefore moved to a
    worktree the coordinator created (`.claude/worktrees/wave1-*`, detached at `c1535d1`).

  After review, PJ settled the questions wave 1 raised:
  - **Simpler prose, by changing the grammar where that costs nothing.** A quoted part may now
    follow any `::`, a leading one included (`identifier = ( name | "::" name_part ) { "::"
    name_part }`), so that "a quoted part must be preceded by `::`" is the whole rule. Whitespace
    around `::` is now a syntax error (`whitespace-in-identifier`, offering the identifier
    without it); the parser checks that an identifier's tokens touch. The `ё` rule's "after" now
    says "when that nearest character is".
  - **State the intent rather than an absolute.** testing.md says unusable data never selects a
    file *by default*, a state test or `FALLBACK()` being how a predicate says otherwise. The
    rationale's two statements of the promise now say "by default" too. warnings.md says a warning
    names where to look: the file, or a directory that could not be listed.
  - **The rest of the behaviour is accepted as implemented**: U+FFFE passes through unchanged, an
    MP3 cut short mid-frame hashes without warning, a symlink cycle is skipped silently, every file
    unreadable under `--no-warn=file` returns 0, and every operand is evaluated before the absence
    check.
  - implementation.md gains four requirements: prepare once, the sources of Unicode data,
    `file::size` through a link, and damaged tags under `file::duration`. The FAQ explains
    `--no-warn data`.
  - `Comparison<T>` became `ComparisonTest<T>`, beside `RangeTest` and `StateTest`, since the old
    name collided with `System.Comparison<T>` outside its namespace.
  - The worktree lesson is in `.claude/agents/line-worker.md`: a coordinator that wants
    checkpoints creates each worker's worktree itself.

  `dotnet test`: 1214 passed.

  Next step: wave 2, the binder (G4) and the `--filter` integration with its end-to-end tests.

- 2026-10-05 -- Wave 2 built and collected: the binder (G4), and `--filter` on `goro list`. The
  37 command-line tests the `--filter` worker wrote, skipped until the binder existed, were turned
  on at collection and passed first time. `dotnet test`: 1664 passed, and one test is skipped on
  purpose (see the last item). Decisions:
  - **The binder is one recursive pass** with three entry points: a condition (the root, or a
    logical operand), an operand (where modifiers are folded into the operator), and a value. A
    failed sub-expression binds to the error type, which counts as definite and silences every
    check involving it. Unknown names and functions, and `NULL`, produce it. Every independent
    error is reported, in text order.
  - **The bridge to typed nodes** is `Apply` with small `IBoundExpressionFunc`s wherever the result
    is generic, and type switches wherever both types are concrete.
  - **Sources.** Only identifier references and conversions are interned, since only they can be
    where an unusable occurrence is born. Identifiers are numbered by `IdentifierName` equality.
    Every node gets a canonical shape, so that a conversion's identity covers its whole argument.
    Parentheses and modifiers add nothing to a shape, and raw and quoted spellings of one string
    are one shape.
  - **Ranges.** Endpoints must agree as written, so `60..120kb` is an error whatever is being
    tested, and the diagnostic offers `60kb..120kb`. A range of plain numbers takes the subject's
    bytecount or duration type. Endpoints are prepared once, by the operator's order.
  - **The numeric literal exception sees through modifiers**, so `ALL(90) < file::duration`
    compares with ninety seconds.
  - **`!=` gets one diagnostic per comparison**, offering `NOT l == r` and `ALL(…)` around each
    operand that needs it. `NUMBER(number)` and `STRING(string)` are elided. `NUMBER("5")` stays a
    conversion.
  - **Diagnostics offer rewrites built from the user's text.** The closest identifier, namespace or
    function is found by optimal string alignment distance (at most 1 edit for names up to 3
    characters, 2 up to 6, 3 beyond). `year == "2000"` offers `year == 2000`, and
    `id3v2::raw::TRCK > 9` offers `NUMBER(id3v2::raw::TRCK) > 9`. `year == NULL` offers
    `year IS ABSENT`. A misplaced modifier offers the expression without it.
  - **Patterns** are compiled at bind time. A malformed one is reported at the engine's offset,
    mapped through the raw string's doubled quotes. An unsupported construct names its family,
    and canaries cover ten patterns across the four families.
  - **`--filter`** compiles the predicate before the pathspecs are resolved, and the first
    rejection ends the run with 2, having touched no file. Predicate and pathspec errors now share
    the `goro: error:` prefix. A predicate error is printed as the message, the predicate echoed
    with the span marked under it (columns counted in grapheme clusters, so East Asian wide
    characters leave the marker short, an accepted limitation), and each suggestion as the whole
    predicate rewritten. Spectre cut short a separately written `--filter` value beginning with
    `-`, so `--filter X` is rewritten to `--filter=X` before parsing, as `--no-warn` already is.
  - **`PredicateStage`** returns T as listed, and F and U as examined but not listed. An
    unreadable file gets its one file warning, worded as `goro hash` words it (D3). Any other
    exception fails the run.
  - **A leading `::` starts a name at the global namespace**, where every namespace sits, so
    `::file::size` is `file::size`. PJ preferred this wording to calling the `::` redundant, so
    that a future scoping feature could give an unrooted name a relative meaning without changing
    any rooted one. predicates.md says so, and testing.md has the row.
  - **Waiting for the tag line:** testing.md's D3 row written with `artist`, and "the same file
    under a predicate mentioning a tag identifier". Their counterparts use `file::duration`.
  - **A spec error found and fixed.** normalization.md said "`\p{Lu}` matches only in literal
    mode". But normalized mode matches with `IgnoreCase`, and under it .NET 10 lets `\p{Lu}` and
    `[A-Z]` match both cases, with or without the non-backtracking engine. PJ chose to fix the doc
    rather than the matching, since matching the lowercased subject without `IgnoreCase` would
    break `^MOT` against "Motörhead". The doc now says case classes match either case in
    normalized mode, so a pattern that tells case apart works only in literal mode, and a test
    asserts this.

  `dotnet test`: 1668 passed.

  Next step: describe the architecture in architecture.md and implementation.md, correct the
  line-worker note on worktrees, and remove the collected worktrees.

- 2026-10-05 -- The architecture written down, and the error wording collected.
  - architecture.md now describes the runtime component by component: the input interface (and
    its two pre-parse rewrites), the predicate compiler's three stages and what each hands the
    next, the identifier catalog and lazily loaded file data, the evaluator, the pipeline's per-file
    outcome, the executor's tally, the warning sink, and the output renderer, with a diagram of a
    run. implementation.md gains the symlink cycle guard and how a file's reported sources are
    stored. The design document is marked as the settled record of how the design was reached.
  - Every error's wording is now in `src/Goro/ErrorMessages.cs`, so that PJ can rewrite it in one
    place. Each sentence appears there whole, with one member per variant. Some 1,800 predicates
    gave byte-identical diagnostics before and after the move. The only change a user sees is that
    a suggestion now begins `try:` instead of `did you mean:`. Warnings keep their wording where
    they are raised. The tag identifiers' temporary "not implemented yet" message stays in
    `TagBindings`, since the tag line will remove it.
  - The line-worker agent's note on worktrees is corrected. The harness keeps a worker inside its
    own worktree, and removes that worktree if the worker stops with nothing changed, so a worker
    asked to stop at a checkpoint first writes its plan into `.line-worker/plan.md`.

  The brief's "Done when" is met: the architecture is written down at the level of stages,
  responsibilities and the data crossing each seam, the code realises it with tests (1668
  passing), and architecture.md and implementation.md describe it.

  Next step: PJ rewrites the error messages in `ErrorMessages.cs`, starting from the coordinator's
  drafts, which are left unstaged; then a review of the whole line and its merge into main.
