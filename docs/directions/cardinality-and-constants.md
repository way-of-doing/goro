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
