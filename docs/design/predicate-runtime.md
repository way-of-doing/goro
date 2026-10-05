# The predicate runtime

This is the working design of the [predicate-runtime-architecture](../directions/predicate-runtime-architecture.md)
line. It is exploration rather than specification: once its decisions are settled, what it says
moves into [architecture](../architecture.md) and [implementation notes](../implementation.md) in
their own terms, and whatever is left here is the record of how the design was reached.

**Status:** settled and built. [Architecture](../architecture.md) and
[implementation notes](../implementation.md) now describe the runtime as it is. What follows is
kept as the record of how it was reached -- the seams, the concerns on each side, and the options
weighed for each decision -- and is not updated as the code moves on.

Nothing here changes what a predicate means. Where a seam turned up a question the specification
does not answer, the question is asked rather than answered in passing.

## The stages at a glance

```
predicate text
   │  Lexer            text → tokens; decodes literals; never knows what is a function
   ▼
tokens
   │  Parser           tokens → syntax tree; one token of lookahead
   ▼
syntax tree            the predicate as written: every node keeps its span
   │  Binder           static analysis: resolve, type, check, intern sources, compile patterns
   ▼
compiled predicate     immutable, shared by every concurrent evaluation for the whole run
   │  Evaluator        once per file, with that file's evaluation context
   ▼
T, F or U  ──────────► predicate stage of the command's pipeline ──► file outcome ──► run outcome
                         │                                                              │
                         └──► per-file warnings ──► warning sink (stderr) ──────────────┘ exit code
```

The lexer, parser and binder together are the _compiler_; everything the architecture says a
predicate parser must report before any file is processed is reported by one of them. The
evaluator, the evaluation context and the warning machinery are the _runtime_.

## Seams, and the concerns on each side of them

The brief asks for the concerns on every seam to be gathered from both sides before either side is
designed. They are collected here, one seam at a time, with the documents they come from.

### S1. Text to tokens (lexer → parser)

What the text imposes:

- A token is always as long as it can be, and whitespace (Unicode `White_Space`) never appears
  inside one. This alone decides `5mb` against `5m b`, `1h10m`, `r"x"` against `r "x"`, `NOTx`, and
  `1..100` as a number, `..` and a number, since a period must be followed by a digit.
- Keywords, function names, identifiers and unit suffixes are matched case-insensitively with
  invariant rules; the contents of a string are not, and neither are the letters of an escape.
- Quoted strings process a closed set of escapes; anything else after a backslash is an error, as
  is a lone surrogate left by `\x` or `\u`, and `\u{...}` beyond U+10FFFF or naming a surrogate.
  Raw strings process nothing and double a quote to escape it.
- A number, bytecount or duration must be representable exactly as a `System.Decimal`.

What the parser needs:

- A kind, a span and, for literals, the decoded value: the string's text and **whether it was
  raw**, since the pattern of `=~` must be; a number's value; a bytecount's or duration's value
  and unit.
- Reserved words as keyword tokens, which the parser still accepts as a name part after `::`, so
  that `ape::and` and `::and` are identifiers while `and::x` is not.
- **Borrowed operator spellings as tokens rather than lexical errors.** The diagnostics the
  architecture promises for `=`, `<>`, `&&`, `!`, `!~` and `~=` all suggest a rewrite of the
  surrounding comparison (`!~` is answered with `NOT artist =~ r"^a"`), so they have to be
  diagnosed where the parser can see both operands.

Neither side needs anything lazy: a predicate is one command line argument, so the lexer produces
the whole token list up front.

### S2. Tokens to syntax tree (parser → binder)

What the grammar imposes:

- Four precedence levels, a `comparison_tail` that appears at most once, unbounded `NOT` and
  modifier stacking, and a single token of lookahead, which is enough because an identifier
  reference and a function call differ only by a following `(`.
- A range is two literals joined by `..` and a pattern is one raw string, both placed by the
  grammar, so parentheses are not admitted there: `(1)..2` and `artist =~ (r"^a")` are syntax
  errors.

What the binder and the diagnostics need:

- **The predicate as written.** Every node keeps its span into the original text: warnings quote
  the sub-expression an unusable occurrence was born at, and diagnostics compose their suggested
  rewrites out of the user's own text (`NOT genre == "metal"` is built from the spans of `genre`
  and `"metal"`, not reprinted from a tree).
- Parentheses kept as nodes. They are transparent to every rule, so the binder looks through them,
  but they are part of what was written.
- Modifiers kept as nodes. Whether one is misplaced, contradictory, or the quantifier `!=` needs
  can only be decided by the binder, which needs to see them where they were written.
- **A deliberately wider grammar in a few places**, so that mistakes are reported for what they
  are: after `=~` the parser reads an operand and then complains if it was not a bare raw string,
  which is what lets `artist =~ LITERALLY(r"^a")` be answered with `LITERALLY(artist) =~ r"^a"`; a
  second comparison operator after a comparison is reported as chaining rather than as an
  unexpected token; `NULL` parses as a primary so that `year == NULL` can be answered with
  `year IS ABSENT`.

### S3. Syntax tree to compiled predicate (the binder, and the identifier catalog it consults)

What the specification imposes on static analysis, from [predicates](../concepts/predicates.md)
and the static judgements of [evaluation](../concepts/evaluation.md):

- Identifier resolution: the namespace must exist, a name in a closed namespace must be defined, a
  name in an open namespace is always accepted; identifiers are case-insensitive, and a quoted part
  that a bare name could have spelled is the same identifier as the bare name.
- A type for every sub-expression, bottom-up, and the numeric literal exception, which runs the
  other way: a number literal (parenthesized or not) takes the type of the other operand when that
  is a bytecount or a duration, and must then be non-negative and, for a duration, whole. The same
  holds for range endpoints and for the default of `FALLBACK()`.
- Definiteness for every sub-expression, and the two places it matters: the operands of the logical
  operators and the predicate as a whole, and an operand of `!=`.
- Modifiers folded into the operator that reads them: a quantifier per operand (the outermost
  `ALL` or `ANY`, contradictory stacks being an error, an explicit one remembered for `!=`), and one
  comparison mode per operator.
- Ranges validated by comparing their endpoints the way the operator will, which for strings means
  **normalizing them in the operator's mode at bind time**.
- Patterns compiled at bind time. On .NET 10 the non-backtracking engine reports a malformed
  pattern as `RegexParseException`, carrying an offset, and each of the four unsupported construct
  families as `NotSupportedException`, so the two diagnostics fall out of the engine directly.
- Every error the specification lists, and the diagnostics the architecture promises: the closest
  defined identifier for an unresolvable one, both rewrites for `!=`, the raw form for a quoted
  pattern, both `=~` and `!=` for `~=`.

What the binder must produce for the runtime:

- An immutable tree of resolved, typed nodes, safe to evaluate concurrently.
- **Interned warning sources**: every structurally distinct value sub-expression gets a small
  integer, with modifiers and parentheses transparent, identifier spelling canonicalized and the
  two string forms indistinguishable. The table of sources is part of the output, since the
  set of sources a predicate can produce has to be enumerable before any file is read.
- Range endpoints already prepared in the operator's mode, compiled patterns, and every order
  chosen. Nothing else is prepared ahead: a literal operand of a comparison is a datum like any
  other and is prepared when the operator evaluates, since preparing a string twice is not
  guaranteed to give the same result as preparing it once.

What the identifier catalog must give the binder, for every identifier it can resolve: its
declared type, whether it is definite, and a binding the runtime can use to resolve it for a file.
The catalog is passed in rather than reached for, which is what lets the evaluation tests declare
the `x`, `m`, `s` and `t` of the testing document with whatever values a scenario needs.

### S4. Compiled predicate to evaluation (the runtime's core)

What [evaluation](../concepts/evaluation.md) imposes, and what the representation has to make
easy to get right:

- A value is absent or a bag of one or more occurrences, each usable (with a datum) or unusable
  (with a source, and the node it was born at, for quoting). **There is one representation of
  absence**: a bag of no occurrences must not exist separately from it.
- An absent operand makes a comparison, range or regex operator false before any iteration.
- Operands are iterated nested by quantifier, universals outermost, written order breaking ties;
  iteration is exhaustive; a combination with an unusable occurrence reports every such occurrence
  and is `U`; quantifiers combine `T`, `F` and `U` by Kleene's rules.
- `AND` and `OR` short-circuit on a settling first operand only; state tests and `FALLBACK()` read
  state and never report; `NUMBER()` and `STRING()` propagate unusable occurrences with their
  sources unchanged, and `NUMBER()` gives a failed conversion a source of its own.

What the pipeline imposes:

- The compiled predicate is shared by arbitrarily many concurrent evaluations, so it holds no
  per-file state; the per-file state (the set of sources already reported, the file's data as it
  is loaded, the warnings so far) lives in an evaluation context created for one file and dropped
  after it.
- The result keeps `T`, `F` and `U` apart all the way to the command.

### S5. Evaluation to file data (identifier bindings)

What [warnings](../concepts/warnings.md) and the FAQ impose:

- **A file is read only as far as the predicate actually needs.** `file::path`, `file::name` and
  `file::extension` need nothing; `file::size` needs file system metadata; `file::duration` needs
  the audio properties; a tag identifier needs the tags. Because `AND` and `OR` short-circuit, what
  is needed depends on the file: `file::size > 10mb AND artist == "metallica"` reads tags only for
  the large files, and a file whose tags would have failed to parse is not unreadable if nothing
  asked for them.
- Whatever is loaded is loaded once per file, however many identifiers use it.
- Failing to load something that was needed makes the whole file unreadable: one file warning,
  and the file is not processed. See decision D3 for what this means mid-evaluation.
- The global namespace consults formats in rank order and stops at the first that yields a usable
  occurrence; the testing document wants that asserted against the tag reader, so the tag reader
  sits behind something a test can observe.

The tag namespaces themselves -- trimming, dates, track numbers, genres, the Id3v2 version
unification, canaries and skipped tests for TagLibSharp's quirks -- are a large body of work with
a test class of their own, and only their seam is part of this design; see decision D4.

### S6. Evaluation to warnings (warning sink)

- A data warning names the file and quotes, from the original text, the sub-expression its
  occurrence was born at. It is emitted at most once per file per source.
- A file warning names the file and the cause, once per file, never deduplicated.
- **A suppressed warning was not produced**: written nowhere, counted nowhere. Suppression is by
  category only, so it can be decided in one place.
- The exit code needs to know whether any warning of each category was produced, after
  suppression.
- Warnings arrive from many concurrent evaluations, and each must reach standard error as a whole
  line.

### S7. Compiled predicate to pipeline (the predicate stage and the run outcome)

- The predicate is compiled before any file is discovered; a rejected predicate, like a rejected
  pathspec, returns `2` with nothing on standard output.
- The predicate stage turns `T`, `F` and `U` into what its command does with them. For `goro list`,
  `T` lists the file and `F` and `U` do not, and all three count as examined; an unreadable file is
  not listed and does not count as examined.
- The run outcome gathers what `--strict-exit-code` needs: whether files were found, whether any
  were examined, whether any matched, and the warning categories produced; the precedence rules of
  [exit codes](../concepts/exit-codes.md) turn that into a code.
- Today the executor reports a failing file by writing the exception message to standard error
  itself. That becomes a file warning through the sink, which is also what `goro hash` needs.

### S8. Normalization (shared by the binder and the evaluator)

- One pure, total function from a string and a mode to a prepared string, exactly as
  [normalization](../concepts/normalization.md) defines; ordering by code point; never throws.
- The binder uses it on literals and range endpoints, the evaluator on data and subjects. A regex
  match in normalized mode adds case-insensitive invariant matching to the prepared subject.
- The scratchpad's `Normalization` experiment holds the candidates that were measured when the
  rules were chosen; it graduates by being rewritten, as anything from the scratchpad does.

## Concern groups

Grouping the concerns by seam gives seven pieces, each of which can be designed and built without
the others once the types on its seams are fixed:

| Group | Piece                              | Seams      | Owns |
|-------|------------------------------------|------------|------|
| G1    | Normalization                      | S8         | the normalizer and the string comparer
| G2    | Lexer                              | S1         | text to tokens, literal decoding, lexical diagnostics
| G3    | Parser                             | S1, S2     | tokens to syntax tree, syntax diagnostics
| G4    | Binder                             | S2, S3, S8 | static analysis, source interning, semantic diagnostics
| G5    | Evaluator                          | S3, S4     | the semantics of every bound node, the value model
| G6    | Warnings, run outcome, CLI         | S6, S7     | the warning sink, exit codes, the predicate stage, and `--filter`, `--no-warn` and `--strict-exit-code`
| G7    | Identifier catalog, file namespace | S3, S5     | the catalog, the `file` namespace, lazily loaded file data

The contract is therefore: tokens (S1), the syntax tree (S2), the bound tree and value model
(S3, S4), the catalog and the evaluation context (S3, S5), the warning types (S6), and the
normalizer's signature (S8). It is written and committed before any group starts, as the
[directions README](../directions/README.md) requires.

## Decisions

Most choices are local to one group and are left to it. Five shape every group at once, and are
put here with the options that were considered. All five were settled by PJ on 2026-10-05.

### D1. The shape of the compiled predicate

Goro's types are known statically, but only to the binder: C# sees one predicate type whatever the
user wrote. The question is where Goro's type discipline stops being a promise made by the binder
and starts being something the C# compiler checks, and that decides how the evaluator is written.

**A. A typed, self-evaluating tree.** Values are `Value<T>` over the datum types `string`,
`decimal`, `ByteCount`, `Duration` and `bool` (the middle two being small structs over `decimal`, so
that they cannot be mixed up with a number). Bound nodes are `BoundExpression<T>`, each with an
`Evaluate` that returns a `Value<T>`; a comparison is a `ComparisonTest<T>` over two
`BoundOperand<T>`; `COUNT` is a `BoundExpression<decimal>` over any `BoundExpression<T>`. The binder
is the one place where a type known only at run time of the compiler becomes a C# type parameter,
through a handful of switches on Goro's type; past that point, an evaluator that compared a string
with a duration would not compile.

_Prognosis._ The type rules of the specification are mirrored by the C# type system, which is the
most instructive thing this design could show a reader: static analysis produces a program that is
correct by construction, and each node's semantics sits next to its data in a few lines. The cost is
generic machinery -- the binder's type switches, generic node families, and passes over the tree
other than evaluation (enumerating sources, quoting) needing either a non-generic base or a generic
visitor. Adding a type later means a new datum type and new cases in the switches, which is the
right amount of friction for something the specification treats as closed.

**B. An untyped tree with a separate evaluator.** One `Value` holding `Datum`s, each tagged with its
Goro type, and bound nodes that are plain records; an `Evaluator` pattern-matches over them. The
binder's checks are the only guarantee, and the evaluator casts.

_Prognosis._ The most familiar shape for an interpreter, and the easiest to read for somebody who
has never seen generics used this way; new passes over the tree are ordinary pattern matches. What
it gives up is that a type error inside the evaluator surfaces as a wrong answer or an invalid
cast at run time, on some file, in exactly the class of code testing.md says fails silently.

**C. Compilation to delegates or expression trees.** Rejected. Evaluation is dominated by reading
files, a predicate is a handful of nodes, and the gain would be unmeasurable, while the result is
harder to read, step through and test, which works against the project's purpose as an example.

_Recommendation: A._ It is the more elegant of the two and the one that earns its machinery: the
rule it enforces is the one the whole static-error guarantee rests on.

**Decided: A.** Goro's type is carried by the type parameter. Definiteness stays a flag that each
bound node computes from its children, as the static judgements table does: typing it as well
would double every function node for a rule the binder checks in two places.

### D2. Synchronous evaluation over lazily loaded file data

The pipeline is asynchronous, and reading a file is I/O. The evaluator can be:

**A. Synchronous, with file data loaded lazily on first use.** The evaluation context loads each
kind of data (file system metadata, audio properties, tags) the first time a binding asks for it,
synchronously, and keeps it for the rest of the file. The pipeline stage calls the evaluator on a
thread-pool thread, as the executor already provides.

**B. Asynchronous throughout,** every `Evaluate` returning a `ValueTask`.

**C. Eager prefetch** of everything the predicate could need, determined statically, followed by a
synchronous evaluation over loaded data.

_Prognosis._ TagLibSharp's reading is synchronous, so B would put `await` on every node for I/O
that blocks a thread anyway; it doubles the cost of reading every node to gain nothing today and
nothing plausible later. C breaks the specification: a file whose tags cannot be parsed would be
found unreadable even when short-circuiting meant its tags were never needed, and the FAQ's advice
on putting cheap conditions first would stop being true. A matches what the specification says
about which files can fail to be read, keeps the evaluator plain code, and leaves concurrency to
the executor, where it already is.

_Recommendation: A._ **Decided: A.**

### D3. A file found unreadable part way through evaluating its predicate (a question for the specification)

Evaluation can produce a data warning and then fail to read the file:
`NUMBER(file::name) > 1 OR artist == "x"` reports `NUMBER(file::name)` for a file whose name is not
a number, then needs the tags, and finds them unparseable. The file is unreadable and "has not been
processed". The specification does not say whether the data warning emitted on the way still
stands. The mechanism is not in doubt: the binding throws, the exception unwinds the evaluation,
and the predicate stage turns it into the file's outcome. What is in doubt is what is reported.

**A. Only the file warning.** The file's data warnings are held in its evaluation context until the
file is finished, and discarded if it turns out to be unreadable. The output says one thing about
the file, the thing that matters most, and the exit code is `11` either way.

**B. Both.** Every warning is reported as it occurs.

_Prognosis._ A matches "one file that cannot be read is one warning" and "has not been processed":
a half-evaluated predicate's remarks are about a computation that was abandoned. B is not wrong,
and costs nothing either, but leaves a reader to wonder why a file that was not processed has
remarks about its data. Holding warnings per file is wanted anyway, so that a file's warnings
reach standard error together rather than interleaved with other files'.

_Recommendation: A_, recorded in the warnings document as a sentence, since it is observable.

**Decided: A.** The sentence is in [warnings](../concepts/warnings.md), and the testing document
has a scenario for it.

### D4. How far the code goes

The brief's "Done when" asks for "a code skeleton with tests". The seams cut the work into pieces
that can be built side by side, which makes it cheap to go further than a skeleton, but where to
stop is a question of what this line is for.

**A. A walking skeleton.** Every contract type real, every stage present and wired end to end
through `goro list --filter`, but each stage complete only for one slice of the language (say
`file::size`, `==`, `AND`); the rest stubbed, with failing or skipped tests marking what is left.

**B. The whole language over the `file` namespace.** Lexer, parser, binder and evaluator complete
for everything the specification defines; normalization complete; warnings, run outcome,
`--filter`, `--no-warn` and `--strict-exit-code` complete for both commands; the identifier catalog
real, with the `file` namespace implemented and the tag namespaces declared but resolving through a
seam with no implementation behind it yet. The tag namespaces become a line of their own.

**C. B, and the tag namespaces too.**

_Prognosis._ A finishes soonest but leaves most of the risk the testing document describes, the
quantifier and absent-against-unusable semantics, still to be found later, and each stub has to be
returned to by somebody who will have to reconstruct the design around it. B retires that risk
while the design is fresh, and every piece of it is fully specified already. C doubles the line
with work that is about TagLibSharp rather than about the runtime, and has a test class of its
own; it is a natural line to run next, with the seam B leaves as its contract.

_Recommendation: B._ **Decided: B.**

### D5. How many errors one run reports

**A. The first error only,** whatever stage finds it.

**B. The first syntax error, then every semantic error.** The lexer and parser stop at the first
error, since a predicate is one line and guessing at what a broken one meant rarely helps; the
binder carries on past an error with an "error" type that silences the errors it would otherwise
cascade into, and reports every independent one: two misspelled identifiers are two errors.

**C. Full recovery,** the parser resynchronizing and reporting several syntax errors.

_Prognosis._ A is the simplest and is often irritating in practice: fixing a predicate one
misspelling at a time. C costs a great deal of parser machinery for one-line inputs, and recovery
heuristics produce the confusing follow-on errors compilers are known for. Recovery earns its keep
in editors, which need a tree for every keystroke of a half-typed file; Goro has none, and an
expression language without statement terminators has few places a parser can safely resume from.
B is where most compilers land, and the error type is a small, well-understood device that is also
a good thing for a reader to see.

Note what B means when both kinds of error are present: a predicate that does not parse has no
tree to analyze, so only its syntax error is reported, and its semantic errors appear on the next
run, once that is fixed. Each run costs milliseconds and processes nothing.

_Recommendation: B._ **Decided: B.** A variant was considered and not taken: letting the parser
carry on past the mistakes it is already built to diagnose -- the borrowed operator spellings, a
quoted or modified pattern, a chained comparison -- since there it knows exactly what was written
and needs no guesswork to resume, so that `artist = "metallica" && year = 2000` would report all
three borrowed spellings at once. It would have cost error nodes in the syntax tree for a modest
gain.

## Taken as given unless objected to

- **Hand-written lexer and recursive-descent parser**, as the architecture already implies, with
  no parser library and no new packages anywhere in the line.
- **Two trees**: a syntax tree of what was written, and a bound tree of what it means.
- **Diagnostics** are structured -- an error code, a span, a message and suggested replacements,
  each a span and replacement text -- and are rendered with the predicate echoed and the span
  marked beneath it. Their exact wording belongs to the implementation.
- **The unsupported regex constructs are rejected by the engine**, so each family gets a canary
  test: if a later .NET supports one, the specification's promise that it is rejected would
  otherwise lapse silently.
- **Code lives under `Goro.Predicates`**, in `Syntax` (tokens, lexer, syntax tree, parser),
  `Binding` (binder, bound tree, sources), `Evaluation` (context, values), `Identifiers` (catalog,
  namespaces), `Text` (normalization) and `Diagnostics`; warnings and the run outcome sit beside the
  pipeline, being shared with `goro hash`.

## The contract

The contract lives under `src/Goro/Predicates`, one folder per namespace. A file that a group fills
in starts with a comment naming its owner; everything else is the contract. A group may add files
of its own beside the ones it owns, under its own folder, and write the bodies and private members
of the files it owns, but never change a public signature. Where it cannot do its work without such
a change, it stops and says why.

| Group   | Owns                                                                                            |
|---------|-------------------------------------------------------------------------------------------------|
| G1      | `Text/Normalization.cs`, `Text/StringOrder.cs`
| G2 + G3 | `Syntax/Lexer.cs`, `Syntax/Parser.cs`
| G4      | `Binding/Binder.cs`
| G5      | the bound nodes in `Binding` (literal, identifier reference, `COUNT`, `FALLBACK`, conversions, comparison, range, regex, state test, logical operators), `Binding/Conversions.cs`, `Evaluation/EvaluationContext.cs`
| G6      | the warning machinery, in a `Warnings` folder beside `Pipeline`, and the existing command, execution and output code it changes
| G7      | `Identifiers/FileData.cs`, `Identifiers/BuiltInCatalog.cs`

Three pieces of the contract carry behaviour, because their behaviour is what they are: `Value<T>`,
whose one representation of absence is the specification's invariant; `IdentifierName`, whose
equality is what makes `ape::"artist"` and `APE::artist` one identifier; and `NumberText`, the
number literal syntax that the lexer and `NUMBER()` both read and the canonical form `STRING()`
writes. They are implemented and tested with the contract.

## Next

The contract is written as C# types, with behaviour only where a type's meaning is its behaviour
(the one representation of absence, identifier equality, the number literal text both the lexer and
`NUMBER()` read), and committed on the line branch. The groups then go to `line-worker`s, each in
its own worktree.

Binder tests are predicates written as text, and writing them against hand-built syntax trees would
make the most heavily tested piece the hardest to test. So the binder waits for the parser, and the
work runs in two waves:

| Wave | Groups                   | Why then |
|------|--------------------------|----------|
| 1    | G1, G2 + G3, G5, G6, G7  | each depends only on the contract; the lexer and parser are one worker, since the token seam is internal to reading text and parser tests want text
| 2    | G4, and the `--filter` integration | the binder is tested through the real parser; the predicate stage and its end-to-end tests need the whole compiler

After each wave the coordinator collects the workers' changes, runs the whole suite, and stops for
review.
