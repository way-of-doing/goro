---
status: active
size: focused
touches: concepts/predicates.md, concepts/evaluation.md, design/rationale.md, design/deferred.md, testing.md, src/Goro/Predicates, tests/Goro.Tests
after: semantic-analysis-passes, cardinality-and-constants
branch: line/as-operator
---
# Conversions as an operator, `x AS T`

## Intent

Replace the conversion functions `NUMBER()` and `STRING()` with a conversion operator,
`x AS NUMBER`, so that conversions are one construct with rules of their own and functions are
left with nothing but functions: `COUNT()`, `FALLBACK()`, and those that later lines add.

**What `AS` does.** `x AS T` converts each occurrence of `x` to the target `T` and never changes
how many there are. An absent `x` gives absent; an unusable occurrence stays unusable and keeps its
source; an occurrence that cannot be converted becomes an unusable occurrence whose source is the
`AS` expression. `AS` reads nothing of an unusable occurrence, so it consumes nothing and never
warns. Converting to the type a value already has changes nothing. The result is definite when the
operand is. A boolean is neither an operand nor a target, as it is not of `NUMBER()` and
`STRING()` today; that waits for boolean data from tags.

**Targets.** The targets are `NUMBER`, `STRING`, `DURATION` and `BYTECOUNT`, the last two being
new: nothing today converts into either.

- To `STRING`, a number, duration or bytecount converts to the canonical number text `STRING()`
  produces today: a duration as its total seconds, a bytecount as its bytes.
- From a string, a value converts when its text is a literal of the target type: a number literal
  for `NUMBER`; a duration literal in either form for `DURATION`; a bytecount literal for
  `BYTECOUNT`. For the last two a plain number literal is also accepted and taken as total seconds
  or bytes, on the same terms as the numeric literal exception: never negative, and a whole number
  for a duration. So `"4:05"`, `"4m5s"` and `"245"` all convert to the same duration, and every
  value survives `AS STRING` followed by a conversion back to its own type.
- From a number, a value converts to a duration as that many seconds and to a bytecount as that
  many bytes, under the same terms; a duration or a bytecount converts to a number as its total
  seconds or bytes.
- A duration and a bytecount do not convert into each other; that is an error.

A constant operand that does not convert is an error when the predicate is read, as
[cardinality-and-constants](cardinality-and-constants.md) makes every failing conversion of a
constant.

The mechanism admits targets that are interpretations rather than types, each with a result type
of its own -- `AS YEAR` for date-shaped text, `AS TRACK` for tracknumber-shaped text. Adding them
belongs to [sources-and-fields](sources-and-fields.md), where they answer its question about
writing concepts in predicate syntax without a function named like a concept.

**Grammar.** `AS` has a precedence level of its own, binding more tightly than every operator but
looser than a primary expression, so that `x AS NUMBER > 5` is `(x AS NUMBER) > 5`. It may be
chained, and chains read left to right: `x AS NUMBER AS STRING`. `AS` becomes a reserved word.

If arithmetic is ever added, `a + b AS STRING` will need to be read with the precedence table in
hand. That is accepted as a minor inconvenience: where it matters, the misreading usually ends in a
type error, and the diagnostic can say what to write.

**Modifiers.** An operand of `AS` is not an operand of a comparison, so the existing rule forbids a
modifier on it: `ALL(x) AS NUMBER > 5` is an error, with a diagnostic offering
`ALL(x AS NUMBER) > 5`. This reads more naturally than `NUMBER(ALL(x))` did and is no less wrong;
it is the price of a language that is both simple and expressive. Nothing decisive was found for
letting quantifiers pass through `AS`, which would be well defined, since `AS` keeps the number of
occurrences, but would reopen what the rationale settled by putting modifiers on the outside. The
rule stands unless the line finds such an argument.

## Questions

- **Target names as reserved words.** The grammar places a target only after `AS`, so the names
  need not be reserved, but the state names after `IS` are reserved so that their misuse can be
  answered, and the same reasoning may apply.
- **Where the grammar admits a misplaced modifier.** Whether `ALL(x) AS NUMBER` parses and is
  rejected by the binder, as `COUNT(ALL(x))` is, so that the diagnostic can say what was meant.
- **Diagnostics for the old spellings.** `NUMBER(x)` and `STRING(x)` will be written from habit and
  from other languages, and SQL's `CAST(x AS NUMBER)` too; each can be answered with `x AS NUMBER`.

## Done when

The predicate documentation describes conversion under Operators with `NUMBER()` and `STRING()`
gone from Functions; the evaluation document, the static judgements and the grammar include `AS`;
the rationale records why conversion became an operator, and its question on why `STRING(120s)` is
`"120"` is restated for `AS STRING`; the testing document's scenarios use the new spelling and
cover the new targets, the round trip through `AS STRING`, chaining and the misplaced modifier; and
the code and tests are changed to match.

## Log

- 2026-10-05 -- Proposed in discussion. Decided there: the operator replaces `NUMBER()` and
  `STRING()` and never changes cardinality; it has a precedence level of its own and may be chained;
  text converts to a duration or a bytecount through the type's literal syntax, with a plain number
  taken as total seconds or bytes; the possible ambiguity with future arithmetic is accepted; and a
  modifier on the operand of `AS` stays an error for want of a decisive argument either way.
  Everything under Questions is open. Next step: pick up the line.
