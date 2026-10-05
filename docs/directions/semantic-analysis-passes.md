---
status: active
size: focused
touches: architecture.md, implementation.md, src/Goro/Predicates/Binding, tests/Goro.Tests/Predicates/Binding
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
| Lowering | semantic tree to the typed evaluation tree, compiling patterns and preparing range endpoints | `CompiledPredicate` |

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
