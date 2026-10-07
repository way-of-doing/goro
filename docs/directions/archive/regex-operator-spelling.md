---
status: landed
size: focused
touches: concepts/predicates.md, concepts/normalization.md, concepts/evaluation.md, design/rationale.md, design/deferred.md, architecture.md, testing.md, faq.md
after:
branch: line/regex-operator-spelling
---
# Spell the regex operator `=~`

## Intent

Rename the regular expression match operator from `~=` to `=~`, the spelling of Perl, Ruby,
Bash, PromQL and MPD's filter syntax, and the one a newcomer is most likely to type. `~=` has
almost no history as a regex operator, and where it is known at all -- Lua, MATLAB, Smalltalk --
it means "not equal", so `artist ~= "Metallica"` would be read as the opposite of what it does.
Nothing about the operator's meaning changes.

Both spellings, and `!~`, are typed by people coming from somewhere else, so each gets a
diagnostic that names what was probably meant: `~=` may have meant either `=~` or `!=`, and
`!~` is answered with `NOT`.

## Done when

Every normative and design document spells the operator `=~`, the rationale records why, the
diagnostics for `~=` and `!~` are stated where the other borrowed spellings are, and the testing
scenarios cover them. No code exists for the operator yet, so `src/` and `tests/` are untouched.

## Log

- 2026-10-04 -- Renamed throughout; ready for review. PJ chose `=~` after a discussion of the
  prior art. The deciding points: `=~` is what Perl, Ruby, Bash, Groovy, Cypher, Prometheus and
  MPD all use, while `~=` means "not equal" in Lua, MATLAB and Smalltalk. That made the old
  quoted-pattern diagnostic a hazard: `artist ~= "Metallica"`, meant as inequality, would have
  been offered `r"Metallica"` and turned into a match. The resemblance of `~=` to `==`, `!=`,
  `<=` and `>=` was judged thin, `<`, `>`, `BETWEEN` and `IS` already standing outside it.
  - every `~=` in concepts, design, architecture, testing and faq is now `=~`; sessions and
    other briefs are history and were left alone.
  - architecture: `=~` leaves the list of borrowed spellings and `!~` joins it; `~=` is the
    example of a spelling answered with more than one candidate, `=~` and `!=`.
  - rationale: a Q&A after the raw-string one, giving the prior art, the "not equal" reading and
    why the family resemblance was not worth keeping.
  - testing: one row for `~=` with a raw and a quoted pattern, and for `!~`, with the
    diagnostics each must offer.
  - predicates: no new text. The spec still says only that there is no negated form; the
    borrowed spellings are a matter of diagnostics, which the spec leaves to architecture.

  Considered and not taken: a keyword, `MATCHES`. It would fit now that the pattern is part of
  the operator as the range is of `BETWEEN`, but Java and Kotlin's `matches` is anchored, which
  Goro's match is not. Picking `=~` also uses up `~=`, which would otherwise be the natural
  spelling of an approximate-equality operator; nothing like that is planned, and having both
  spellings mean different things would be unkind anyway.

  Reviewed and landed the same day.
