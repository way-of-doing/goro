---
status: active
size: focused
touches: concepts/predicates.md, concepts/evaluation.md, features/builtins/identifiers.md, design/rationale.md, testing.md, src/Goro/Predicates, tests/Goro.Tests/Predicates
after: semantic-analysis-passes
branch: line/cardinality-and-constants
---
# Cardinality bounds and constants

## Intent

Give two of the properties static analysis already tracks the precision their rules need:
definiteness becomes a consequence of cardinality bounds, and the rules that demand a literal demand
a constant instead. Both live in analyses that [semantic-analysis-passes](semantic-analysis-passes.md)
creates, and both are needed by the lines queued after this one.

**Cardinality bounds.** Every expression has a lower bound, 0 or 1, and an upper bound, 1 or many,
on the number of occurrences it can have for any file. Definite means exactly one. The catalog
declares bounds for each identifier instead of marking three of them definite by hand, and each
construct's bounds follow from its operands':

- a literal, an operator, and `COUNT()` are exactly one;
- a conversion, a modifier and parentheses have the bounds of what they apply to;
- `FALLBACK()` raises the lower bound to 1 and keeps the upper;
- `PREFERRED()`, once [preferred](preferred.md) has landed, is never absent if any argument is
  never absent, and has the largest upper bound among its arguments; whichever of the two lines lands
  second states this.

The rules that read definiteness are unchanged, and accept more because they know more:
`FALLBACK(file::extension, "") != "mp3"` and `FALLBACK(id3v1::genre, "") != "blues"` are exactly
one for every file and become valid, where today the documented way of deciding what absence means
is itself rejected. The diagnostic for `!=` also says what the actual problem is: an operand that
can be absent but never holds several occurrences has no use for `ALL` or `ANY`, so the error says
it may be absent and offers what settles that, rather than a quantifier that changes nothing.

The predicate documentation keeps the word "definite" and says which expressions are, now
including a `FALLBACK()` of an expression that never holds more than one occurrence; the bounds
themselves are stated in the static judgements of the evaluation document, and the identifier
documentation says for each identifier whether it can be absent and whether it can hold several
occurrences.

**Constants.** A constant is an expression whose value is known when the predicate is read: a
literal, a parenthesized constant, and a conversion of a constant. The rules that today require a
literal because a literal is the only way to write a known value require a constant instead, and a
conversion of a constant is carried out when the predicate is read, so one that fails is an error
then. A constant is therefore never absent and never unusable, which is what the rules were
relying on literals for. That is the general form of the special case that makes `NUMBER("x")` an error today, and
what makes `"x" AS NUMBER` one in [as-operator](as-operator.md). The places where the grammar itself
puts a literal, the endpoints of a range and the pattern of `=~`, are grammar rather than rules, and
stay literals.

Folding everything that could be folded -- `TRUE AND p`, `COUNT("x")`, `FALLBACK(5, 1)` -- is not
part of this line. Nobody writes those, folding them changes no answer and no warning, and the
saving is negligible beside reading a file. It becomes worth considering only with arithmetic or
named constants.

## Questions

- **`file::duration`.** Whether a file that can be read always has a duration. If it does, it is
  exactly one, like `file::size`; the [not-equal-on-multivalues](not-equal-on-multivalues.md) line
  left it out for want of an answer.
- **The numeric literal exception.** Whether a number constant that is not a literal, such as
  `NUMBER("1000")`, may stand for a bytecount or a duration as a number literal may. Keeping the
  exception to literals keeps it as narrow as it was designed to be.
- **Which rules switch to constants.** The default of `FALLBACK()` and the rejection of a failing
  conversion do. Each rule that names a literal is to be checked, including those added by lines
  that land in the meantime.
- **The bounds of each identifier.** The `file` namespace, Id3v1, and the global namespace are
  straightforward; the tag namespaces that are open today are declared once
  [sources-and-fields](sources-and-fields.md) has replaced them, where each concept's cell states its
  bounds.

## Done when

The evaluation document states bounds for every construct and defines definiteness through them, the
predicate and identifier documentation say what that means for a reader, the rules that required a
literal require a constant, the rationale records why definiteness became derived and why folding
stops at constants, the testing document's scenarios for `!=` include the newly valid `FALLBACK()`
forms and the new diagnostic, and the cardinality and constants analyses implement it, with tests.

## Log

- 2026-10-05 -- Proposed in discussion, as its own line between semantic-analysis-passes and the
  lines that need it. Decided there: bounds of 0 or 1 below and 1 or many above, definiteness as
  exactly one, the bounds of each construct above, constants as literals and conversions of
  constants, and no general constant folding. Next step: pick up the line once
  semantic-analysis-passes has landed.
- 2026-10-06 -- Spec and implementation done by the coordinator; nothing is committed yet.
  - **Decided with PJ.**
    - `file::duration` is exactly one, since a file whose audio properties cannot be read is
      already unreadable.
    - Only number *literals* stand in for bytecounts and durations.
    - An `!=` operand that can be absent but never several gets its own diagnostic, "`x` may be
      absent", offering `NOT x == y` and `FALLBACK(x, <empty>) != y`. The empty value is the
      type's: `""`, `0`, `0b`, `0s` or `FALSE`. A written quantifier still satisfies the rule.
  - **Spec.**
    - evaluation.md's static judgements give every construct's bounds and whether it is
      constant, with definite defined as `1..1`.
    - predicates.md derives its definite list from the bounds, gains a Constants section, and
      says "constant" for FALLBACK's default and for failing conversions. Its `!=` section covers
      operands that can only be absent.
    - identifiers.md states each namespace's bounds.
    - The rationale explains why definiteness is derived and why folding stops at constants, and
      revises the `!=` and FALLBACK answers.
    - testing.md's `!=` rows are rewritten, with a constants row added; architecture.md is updated.
  - **Code.**
    - `Values.Bounds` is new, and declarations, catalog rows and evaluation nodes carry `Bounds`,
      with `IsDefinite` derived from it.
    - `SemanticExpression.IsConstant` is new.
    - Cardinality keeps bounds per node and chooses between the two `!=` diagnostics.
    - Constants folds every conversion of a constant through `TypedNodes.ConvertLiteral` and
      reports one that fails, the `NUMBER(STRING("abc"))` case included. The FALLBACK rule became
      `fallback-default-not-constant`.
    - Lowering uses the folded literal, and Sources no longer counts a constant conversion as a
      source. The binder type-checks any constant FALLBACK default.
  - **Tests.** 2187 pass, 48 of them new:
    - bounds for every construct;
    - the new diagnostic, with each of its rewrites checked to compile;
    - the newly valid forms;
    - constant defaults and their folded values;
    - failing conversions;
    - the no-stand-in rule;
    - a constant conversion not being a source;
    - catalog bounds.
  - **Open.** Both rewrites the new diagnostic offers treat absence the same way by default, so
    absence compares as "not equal" either way. They differ on unreadable data, and in that the
    FALLBACK default can be edited. A reader wanting "absent counts as equal" writes their own
    default, or a quantifier. The tag namespaces that are open today keep `0..many` until
    sources-and-fields gives each concept's cell its own bounds. Next step: PJ's review, then
    commit and land.
- 2026-10-07 -- PJ reversed the decision, recorded under Intent, to keep the word "definite". With
  definiteness derived from bounds, the term needed a definition that "exactly one" does not.
  "Singular" was considered and rejected because it reads as the upper bound alone:
  `id3v1::genre` would be called singular, yet it can be absent. The docs now say "exactly one"
  throughout.
  - predicates.md's section is now "Exactly one", with the `#exactly-one` anchor and the links to it
    updated, and the rationale records why the word went.
  - In code there are no `IsDefinite` properties at all. They only duplicated
    `bounds == Bounds.ExactlyOne`, which reads as the spec's own definition, so `Bounds`,
    `Expression`, `IdentifierDeclaration` and `CardinalityAnalysis` lose them.
  - The condition diagnostic became `condition-not-exactly-one`
    (`ErrorMessage.ConditionNotExactlyOne`). Its English wording is unchanged and never mentioned
    the term.
  - `DefinitenessTests` became `ExactlyOneTests`, and test names and comments follow.
  - design/predicate-runtime.md and earlier briefs keep the old word as history.

  2187 tests pass. Next step: PJ's review, then commit and land.
