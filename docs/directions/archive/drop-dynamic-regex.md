---
status: landed
size: focused
touches: concepts/predicates.md, concepts/evaluation.md, concepts/warnings.md, design/rationale.md, design/deferred.md, implementation.md, testing.md, faq.md
after:
branch: line/drop-dynamic-regex
---
# Drop regex patterns that come from tag data

## Intent

Require the pattern of `~=` to be a literal, as the endpoints of a range and the default of
`FALLBACK()` already are, and then remove everything that existed only to support a pattern
arriving from tag data. The same reasons apply as for those two restrictions: a literal can be
checked before any file is opened, nobody has asked for the alternative, and restricting now
while relaxing later is the direction that breaks nothing.

It is quite possible that eliminating this feature allows simplification of other parts of the
spec which had become bloated or unwieldy just to support this. If these simplifications open
the door for advantageous redesign of other spec parts, the outcome of this work should clearly
describe such potential follow-ups.

## Done when

The specification says the pattern is a literal, every rule and passage that served a non-literal
pattern is gone or rewritten, the rationale records the reason next to the other two literal-only
restrictions, and the tests no longer cover the removed behaviour.

## Log

- 2026-10-04 -- Restriction made and the supporting passages removed; ready for review. The
  pattern of `~=` must be a string literal, to which modifiers may still be applied, so
  `artist ~= LITERALLY("^met")` stays valid. It remains an operand; whether it should is the
  first follow-up below, deliberately kept out of this line. No code or tests for `~=` exist yet,
  so nothing under `src/` or `tests/` changed.
  - predicates: the rule stated in the operator's section, the error list and the static
    constraints; "Invalid patterns" folded into one sentence under "Supported constructs"; the
    tag-data clauses gone from the ways an unusable occurrence arises, the per-file conditions,
    consumption, and the definition of a source, which is now only ever an identifier or a
    conversion; the multivalue sentence now speaks of the subject alone.
  - evaluation: the pattern-validity step gone from `test()`; nothing else there was specific
    to a non-literal pattern.
  - warnings: the second kind of data warning gone.
  - rationale: a Q&A after the range one, giving the reasons and the "Therapy?" argument against
    the one plausible use, `title ~= artist`; the `sometag` example gone from the unusable-boolean
    answer; the engine answer no longer mentions a runtime warning.
  - deferred: the memory-bound question removed, its only threat having been a pattern from tag
    data; the entry retitled for the one question left, unsupported constructs.
  - implementation: the regex cache question removed. It was the only open question, so the
    section went too; that a literal pattern is compiled once follows from `architecture.md`,
    which already says anything derivable from the invocation is computed once at planning.
  - testing: the two tag-data scenarios replaced by one for the new error.

  Open for review: the testing row treats `artist ~= ("^a")` as an error, a parenthesized
  literal not being a `literal` in the grammar's terms, as with range endpoints. The spec does
  not say so in prose, and the default of `FALLBACK()` has the same unanswered question; if PJ
  prefers parentheses to be transparent there, that is a change to all three positions.

  Possible follow-ups, none started:
  - Treat the pattern as part of the test rather than an operand, as `BETWEEN` treats its range.
    `~=` would then have one value operand; a quantifier on the pattern, valid and meaningless
    today, would become an error; `LITERALLY` would go on the subject only (the
    `artist ~= LITERALLY("^met")` examples would be rewritten); the grammar could say
    `"~=" string` in place of a static constraint; and the caveat that the operands of `~=` have
    fixed roles would leave the multivalue nesting rule.
  - Every pattern is now known when the predicate is read, so any pattern diagnostic is possible
    for all of them: pointing at the offending construct, or revisiting the diacritic-in-pattern
    warning the rationale rejected (rejected for cost, which this does not change).
  - There are now three literal-only positions -- range endpoints, the `FALLBACK()` default and
    the pattern -- each stated separately. Whether they deserve one shared statement is worth a
    look if a fourth appears.
  - A data warning now has exactly one cause, an unusable occurrence from an identifier or a
    conversion, and pattern validation and compilation belong entirely to static analysis; the
    evaluator never builds a matcher.

  Next step: review, then merge and set the status to `landed`.

- 2026-10-04 -- After review PJ settled the open point and took up the first follow-up, and
  went further: the pattern is now a raw string that is part of the operator. Three decisions:
  - **A literal in parentheses is a literal.** Wherever a rule asks for a literal -- the
    `FALLBACK()` default, the numeric literal exception, the `NUMBER()` string-literal check --
    `(0)` serves as `0` does, as definiteness already sees through parentheses. Where the grammar
    itself places a literal, in a range or a pattern, parentheses are not part of it. Stated
    once under Values and once in the static constraints.
  - **The pattern is part of `~=`, as the range is part of `BETWEEN`.** `~=` has one operand,
    the subject. No expression can reach the pattern, so future string expressions (constants,
    concatenation) never reopen what counts as a literal; a quantifier on it is now a syntax
    error rather than valid and meaningless; and `LITERALLY` goes on the subject only, so
    `artist ~= LITERALLY("^met")` became `LITERALLY(artist) ~= r"^met"`.
  - **The pattern must be a raw string** (`pattern = raw_string`). The engine is then the only
    reader of a backslash, and the string grammar can grow -- a `\b` escape, interpolation --
    without silently changing what a pattern means; today's error on an unknown escape would
    otherwise be the only guard, and it erodes with every escape added. PJ: the `r` is the only
    cost. Restricting now and admitting quoted strings later breaks nothing.

  Spec consequences, all done: grammar (`"~=" pattern`, `pattern = raw_string`), the operator
  section, the modifier and comparison-mode sections and their examples, every pattern example
  across the docs given its `r`, the multivalue caveat that the operands of `~=` have fixed roles
  removed, evaluation's operand sentence aligned with `BETWEEN`'s, and two rationale Q&As (why
  the pattern is part of the operator; why raw) in place of the earlier one. The normalization
  Q&A no longer says every other operator normalizes "both operands", and the modifiers Q&A's
  hypothetical `REPLACE()` became a generic function. The FAQ entry on backslashes in patterns
  is gone, there being only one way left to write one; testing loses the quoted-versus-raw
  pattern scenario and gains rows for the new syntax errors and their diagnostics, for
  parenthesized literals, and for `(1)..2`.

  Still open, for later lines if wanted: the remaining follow-ups from the previous entry
  (pattern diagnostics now possible for every pattern; one shared statement of the literal-only
  positions, now that a literal in parentheses is defined once). The deferred entry on redundant
  parentheses around a modified operand stands; parentheses are now transparent around a literal
  but still not around a modifier.

  Next step: review, then merge and set the status to `landed`.

- 2026-10-04 -- Parentheses made fully transparent, at PJ's go-ahead. With a literal in
  parentheses now a literal, the only rule that still saw parentheses was the modifier
  position, which rejected `(ALL(genre)) == "x"`; `deferred.md` had kept that open only because
  nobody had tripped over it, and the rationale for restricting modifiers (distance from the
  operator) says nothing about parentheses. One rule now covers every case: a parenthesized
  expression is in every respect but grouping the expression it encloses. The parser can
  therefore drop parentheses once it has built the tree, with nothing left to check. Done:
  the rule stated under Grouping, the grammar note and the static constraint rewritten, the
  deferred entry removed, and the testing row turned into a valid case plus
  `COUNT((ALL(genre)))` as the rejected one. Grammar positions are unaffected: `(1)..2` and
  `artist ~= (r"^a")` remain syntax errors. A rationale Q&A for this was drafted and dropped
  by PJ as saying nothing interesting. Reviewed and landed the same day.
