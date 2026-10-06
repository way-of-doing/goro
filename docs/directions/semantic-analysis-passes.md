---
status: active
size: focused
touches: architecture.md, implementation.md, src/Goro/Predicates, tests/Goro.Tests/Predicates
after:
branch: line/semantic-analysis-passes
---
# Bind, analyse, lower

## Intent

Split the binder's single pass into the shape mature compilers share: meaning established once, in
a semantic tree; facts about it computed by separate analyses, each owning one property and the
rules that use it; and the executable form produced last, by lowering. Nothing a predicate means or
reports changes.

The binder today makes one recursive pass that, in every node method, resolves names, works out
types, definiteness and literal-ness, builds the shape warning sources are interned by, folds
modifiers into quantifiers and comparison modes, checks every rule, and builds the typed evaluation
tree. It became that because there is no tree between the syntax tree, which is too raw, and the
evaluation tree, which is generic and shaped for evaluation -- the cost decision D1 in
[the predicate runtime design](../design/predicate-runtime.md) recorded as passes "other than
evaluation" needing a non-generic base or a generic visitor. Every analysis therefore has to happen
at the moment of building. The lines queued behind this one -- [as-operator](as-operator.md),
[sources-and-fields](sources-and-fields.md) and [cardinality-and-constants](cardinality-and-constants.md) -- would all
grow the same methods.

The stages become:

| Stage | Job | Produces |
|---|---|---|
| Parser | unchanged | syntax tree |
| Binder | resolve names; assign types, including the numeric literal exception, which needs only the syntactic fact that a literal is a number; fold parentheses and modifiers away into each operand's quantifier and each operator's mode, reporting misplaced, contradictory or mistyped modifiers | semantic tree: one non-generic node family, Goro types held as data |
| Analyses | each owns one property and the rules that read it | properties and diagnostics |
| Lowering | semantic tree to the typed evaluation tree, taking each compiled pattern and prepared range from the analysis that made it | `CompiledPredicate` |

The analyses, as of today's specification:

- **Cardinality**: definiteness, and the rules that read it -- a predicate must be a definite
  boolean, so must the operands of the logical operators, and `!=` needs a quantifier on an operand
  that is not definite. Replacing definiteness with bounds is a change to the specification and
  belongs to [cardinality-and-constants](cardinality-and-constants.md); this one gives it a place to
  happen.
- **Constants and ranges**: literal values, and the rules about them -- the default of `FALLBACK()`
  must be a literal, `NUMBER()` rejects a string literal that is not a number, and a range's
  endpoints must not be reversed when compared in the operator's mode.
- **Sources**: shapes, interning, and the source table.
- **Patterns**: whether a pattern is valid and uses only supported constructs; the compiled pattern
  is what lowering uses.

Types need names, and the analyses need types but not one another, so they may run in any order.
Lowering needs everything and runs only on a predicate with no errors. The analyses do run on a
tree with errors in it, an error node silencing every check that involves it as the error type does
now, so that every independent error is still reported in one run.

The names are the ones a reader will find elsewhere: a binder in Roslyn's sense, analyses named
for their property, and lowering. The evaluation tree of D1 stays as it is, and gets simpler to
justify, since evaluation is now the only pass over it.

## Questions

- **Where properties live.** Fields on the semantic nodes, filled in by each analysis, or side tables
  keyed by node that each analysis returns. Side tables keep the nodes immutable and make each
  analysis's output something a test can inspect on its own.
- **Diagnostics.** `BinderDiagnostics` holds every semantic diagnostic and its suggested rewrites;
  whether it splits along the same lines, so that each analysis owns the messages for its rules.
- **Tests.** Today binder tests observe properties through the errors they cause. Each analysis can
  now be tested by asserting its property directly, over predicates written as text and run through
  the parser and binder; which existing tests move, and which stay as end-to-end tests of the whole
  compiler.

## Done when

The binder produces a semantic tree, the four analyses and lowering each live in their own files
with their own tests, the whole suite passes with no change in behaviour, and `architecture.md`'s
account of the predicate compiler describes the binder, the analyses and lowering as they are.

## Log

- 2026-10-05 -- Proposed after a discussion of where the binder's growing responsibilities should
  live, looking at how javac (`Attr`, `Flow`, `Lower`), Roslyn (binder, flow analysis and
  `NullableWalker`, local rewriting), Rust and SQL planners divide the same work. Decided there:
  the four stages, analyses owning their own rules, analyses running over trees with errors, and
  cardinality kept beside the type rather than in it, XQuery-style occurrence indicators having
  been weighed and found too heavy for five simple types. Next step: pick up the line, ideally
  before as-operator and sources-and-fields, so that both land in the new structure.
- 2026-10-06 -- Implemented on `line/semantic-analysis-passes`, all four analyses and lowering in
  one change rather than one step at a time, since the binder could not emit a semantic tree while
  any analysis still lived inside it. The brief's questions, as decided:
  - **Properties live in side tables.** Semantic nodes are immutable classes compared by identity.
    Each analysis returns a result object holding its tables and its diagnostics, which its tests
    inspect directly: `CardinalityAnalysis.IsDefinite`, `ConstantsAnalysis.RangeOf`,
    `PatternsAnalysis.PatternOf`, `SourcesAnalysis.OriginOf`.
  - **Diagnostics split by owner**: `BinderDiagnostics`, `CardinalityDiagnostics`,
    `ConstantDiagnostics` and `PatternDiagnostics`, over a small base that quotes the user's text.
    The codes stay one register, renamed `SemanticDiagnosticCodes`.
  - **Tests.** The existing binding tests were left untouched apart from that rename, as the
    independent check that nothing changed. The new tests, in `Binding/Analyses`, assert each
    stage's property directly.

  Further decisions:
  - The patterns analysis compiles each pattern, and the constants analysis prepares each range's
    ends, since judging reversal needs them prepared. Lowering takes both rather than doing the
    work again, so the table above was corrected.
  - Negative and fractional unit literals belong to constants, as rules about a literal's value.
    The binder still decides that a number stands for a unit, since that needs only the syntactic
    fact that it is a number.
  - The binder skips the `FALLBACK` type check when the default is not a literal, which keeps the
    old rule that one mistake is reported once.
  - A malformed `FALLBACK` call stays as definite as its first argument, as before.
  - Errors are ordered by where they start, then by where they end, so that of two errors starting
    at the same character, the one about the smaller part comes first. The old single pass produced
    exactly that order by finding the inner one first. With separate stages it has to be stated.
  - The semantic nodes are named `Semantic*`, since "bound" belongs to the evaluation tree.

  Checked, beyond the suite: a harness compiled about 215,000 predicates with this branch and with
  `main` and compared every diagnostic, message, suggestion, source table and lowered evaluation
  tree. That covered every string and code span in the tests and docs, plus generated predicates,
  8,105 of which compiled. Nothing differed.

  Still open:
  - Whether to rename the evaluation tree away from "Bound" so that Roslyn's vocabulary lines up,
    with the binder producing the bound (semantic) tree.
  - Whether to regroup the existing `*BindingTests` by the stage that now owns each rule.

  Next step: review. Then merge, so that as-operator and sources-and-fields start from this
  structure.
- 2026-10-06 -- Gave each layer a namespace of its own, the folder matching it, so that the
  pipeline can be read off the namespaces:
  - `Goro.Predicates` holds `PredicateCompiler` and `CompiledPredicate`, the compiler's public face.
  - `Binding` holds the binder and the semantic tree. The `Binder/` and `Semantics/` folders are
    flattened into it.
  - `Analyses` holds the four analyses.
  - `Lowering` holds lowering. Its class is now `Lowerer`, since a class cannot share its
    namespace's name.
  - `Evaluation` holds the evaluation tree, which joins `EvaluationContext` and the other run-time
    helpers that were already there.

  The evaluation tree dropped its "Bound" prefix: `Expression`, `Expression<T>`, `IExpressionFunc`,
  `Condition`, `BooleanExtensions`, `Operand<T>`, and the test helper `Nodes`. This settles the
  first open question. "Binding" now means what it means in Roslyn: the binder and the tree it
  produces.

  The analyses' diagnostics classes are now named like everything else in each analysis, after its
  static class: `ConstantsDiagnostics` and `PatternsDiagnostics`.

  The tests mirror the same layout. `Compiler/` holds the end-to-end tests, still grouped by topic,
  since that is how the specification is read; the `*BindingTests` became `*Tests`, and `BindAssert`
  became `CompileAssert`. This settles the second open question. `Binding/`, `Analyses/` and
  `Lowering/` hold the per-stage tests, and `Support/` holds the helpers they share.

  `design/predicate-runtime.md` and the predicate-runtime-architecture brief keep the old names,
  as records of what was decided then.

  Checked: the suite passes unchanged, 2051 tests. The comparison harness, rerun against `main`
  over the same 215,000 predicates, matched exactly once the renamed types were normalised.

  Still open: `Identifiers` and `Evaluation` refer to each other, since `IdentifierDeclaration.Bind`
  creates an `IdentifierReference`, which holds its declaration. Lowering could build the reference
  instead.

  Next step: review, then merge into `main`.
- 2026-10-06 -- Removed `IdentifierDeclaration.Bind`, which was the one reference from `Identifiers`
  into `Evaluation`. `TypedNodes.Reference` now builds the reference, with one type pattern per
  datum type. That puts it where every other step from a Goro type held as data to a C# type
  parameter already happens. `Bind` had been a second place doing it, through a virtual call.
  `Identifiers` now depends only on `Values`. Checked by the suite, 2051 tests, and by the
  comparison against `main`, which still matches exactly.

  Two other cycles remain between namespaces:
  - `Analyses` and `Lowering`, introduced by the previous entry's split: the constants analysis
    prepares range ends with `TypedNodes` and `PreparedRange`, which are in `Lowering`, while
    lowering reads the analyses' results.
  - `Syntax` and `Diagnostics`, which predates this line: `Diagnostic` holds a `TextSpan`, and the
    lexer and parser return `StageResult`s.

  Next step: decide how to break those two, then review and merge into `main`.
- 2026-10-06 -- Broke both remaining cycles, so no namespace under `Goro.Predicates` depends on
  itself through another. Each layer now uses only those before it, in the order `Values`, `Text`,
  `Diagnostics`, `Syntax`, `Identifiers`, `Evaluation`, `Binding`, `Analyses`, `Lowering`, then the
  root. `Diagnostics` depends on nothing in `Goro.Predicates`. `Goro.Messages`, which it uses, uses
  only `Values`.
  - `TypedNodes` and `PreparedRange` moved from `Lowering` to `Evaluation`: they are how the
    evaluation tree is built from types held as data, and both the constants analysis and lowering
    use them. `TypedNodes.Literal` now takes a token and a type rather than a `SemanticLiteral`, so
    it no longer reaches into `Binding`. `Lowering` is left with `Lowerer` alone.
  - `TextSpan` moved from `Syntax` to `Diagnostics`, since a span is where in the predicate's text a
    diagnostic points. This leaves `Diagnostics` holding a span, the diagnostic itself and
    `StageResult`, which might not all belong together. It is still a small namespace, and a move
    that shows the question is better than a cycle that hides it. A candidate for splitting later.

  `Binding` uses `Evaluation` only for `Quantifier`, which `SemanticOperand` carries. That edge is
  in the right direction but a little odd, and `Quantifier` could live lower down.

  Checked: the suite passes, 2051 tests, and the comparison against `main` still matches exactly.
  The analyzer for unnecessary usings (IDE0005), run with documentation generation and style
  enforcement on, finds none left.

  Next step: review, then merge into `main`.
- 2026-10-06 -- Moved `Quantifier` from `Evaluation` to `Values`. It was all `Binding` used from
  `Evaluation`, so the binder and the evaluation tree are now siblings: neither knows the other,
  and they meet only in `Analyses` and `Lowering`. The order is now `Values`, `Text`,
  `Diagnostics`, `Syntax`, `Identifiers`, then `Binding` and `Evaluation` side by side, then
  `Analyses`, `Lowering` and the root.

  `Binding` was considered and rejected as its home. Eight of the nine files using it are on the
  evaluation side, and the tree the compiler produces should not depend on the compiler.
  `Syntax` was rejected too: a quantifier is never written, `ANY` being the default.

  `Values` follows the model of `ALL` and `ANY` as value modifiers: every value carries a
  quantifier bit, and the bit says how its occurrences are read. It mirrors `ComparisonMode`,
  the bit `LITERALLY` sets, which sits in `Text` beside the normalization it governs.

  Checked: the suite passes, 2051 tests; the comparison against `main` matches exactly; IDE0005
  finds no unnecessary usings.

  Next step: review, then merge into `main`.
