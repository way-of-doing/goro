---
status: active
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
