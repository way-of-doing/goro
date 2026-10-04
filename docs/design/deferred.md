# Deferred features

This file contains a list of features that were considered and thought potentially worthy of inclusion, but not immediately. Its purpose is to hold these interesting thoughts until their time arrives.

## Postponed features

**Warning deduplication clarification**
This part is not in the spec exactly because it cannot yet be demonstrated:

> Note that two _different_ conversion functions applied to the same identifier are different sources and warn separately, since the symbol at the top of the source differs. The current function set has only one conversion that can fail, so this cannot yet be demonstrated.

**Redundant grouping parentheses around a modified operand**
`(ALL(genre)) == "x"` parses, but is rejected, its modifier being applied inside a parenthesized expression rather than to an operand of `==`, while `ALL(genre) == "x"` is accepted. Adding parentheses that ought to be redundant therefore turns a valid predicate into an invalid one, which is mildly surprising. Allowing it needs no change to the grammar, since static analysis would only have to see through the parentheses, but it is not worth doing unless somebody actually trips over it.

**Accepting `--filter` on `goro hash`**
Only `goro list` currently accepts a predicate, although the predicate documentation describes the option generically and filtering is a core feature rather than a listing feature. Hashing a subset of a collection is an obvious thing to want. This is deliberately postponed rather than overlooked: once predicates are implemented for `list`, extending another command to accept one is a triviality compared with the work of getting predicates right, and doing `list` alone first keeps that work from spreading across commands before the shape of it has settled. One decision will have to be made with it: what `hash` does with a file whose predicate could not be answered. `list` leaves such a file out, but `hash` exists so that no file goes unaccounted for, and the opposite choice -- hashing the file, so that it appears in the output -- is probably the right one there. That the decision belongs to each command, rather than to the predicate, is what makes the opposite choice possible.

**Exposing the tag revision to predicates**
The revision of a file's Id3v2 tag is known while it is being read, and an identifier reporting it would be trivial to add. It is worth more than it looks. Goro hides the differences between revisions, but a handful of quirks remain that cannot be hidden, and they are all confined to specific revisions. Being able to write a predicate against the revision would let a user both locate the files that a given quirk can affect and exclude them from a run, which in turn means the remaining files are known to be free of it. That is the difference between a documented caveat a user has to reason about and one they can mechanically rule out. The open questions are what the identifier should be called, whether it belongs in the `id3v2` namespace or alongside `file::size`, what it resolves to for a file with no Id3v2 tag at all, and what type it has, given that `2.3` as a number would compare unhelpfully against `2.4`.

**Should a warning name the tag format behind a global identifier?**
A global identifier such as `artist` resolves on a best-effort basis across several tag formats, so a warning naming `artist` as its source tells you that a file has bad data but not which of its tags holds it. Since the purpose of the warning is to send somebody to fix the data, naming the format that actually produced the unusable occurrence would be more useful. The source machinery tracks the identifier that was written rather than the format it delegated to, so this needs the warning to carry something beyond its source, and it should not be allowed to make two files with the same problem report differently.

**Addressing a single `TXXX`, `COMM` or `WXXX` frame by its description**
These frames are distinguished from one another by a description rather than by their identifier, so Goro currently surfaces them as a multivalue of strings that carry the description inside them. Selecting one by description needs a name made of two parts, the frame and the description, where the rest of the language only ever names one thing. A further `::` part would be the obvious spelling, but it would mean an identifier whose parts are not all namespaces, which wants thinking about before it is committed to.

**Resource bounds on regular expression matching**
Matching uses the non-backtracking engine, so a match is linear in the length of the subject and there is no timeout to scope, to configure, or to make a result depend on machine speed and load. Two questions are parked here rather than answered.

The first is memory. Linear time does not mean cheap: the engine's state is bounded but not small, and a pathological pattern arriving from tag data could in principle make a run expensive without making it non-terminating. If that ever proves real, the shape of the fix is a budget for the whole run rather than a limit on one match, since a per-match limit is exactly what reintroduces the nondeterminism the engine was chosen to remove. Nothing should be built for this before somebody has a collection that exhibits it.

The second is the constructs the engine does not support -- lookaround, backreferences, atomic groups, conditionals and balancing groups -- which are rejected rather than matched by some slower route. Supporting them would mean keeping a backtracking engine alongside for the patterns that need it, and a timeout with it, which gives back the nondeterminism for the subset of patterns using those constructs. That may eventually be the right trade, but it is worth making only for a concrete want, and the first candidate that comes to mind, negative lookahead for "does not begin with", is already better said as `NOT (x ~= "^...")`.

**Finer-grained warning suppression**
`--no-warn` selects by category and by nothing finer. Silencing warnings for one identifier, one tag format or one file would need warnings to carry stable identities to name, and they carry none: a data warning is identified today by the file and the sub-expression that produced it, both facts about a single run rather than names. Choosing what those names are is the work, not the option, and it is far better done against a body of warnings from real collections than in the abstract.

**Machine-readable warnings**
Warnings are prose on standard error, so a caller who wants to act on them across a large collection has to parse English. A structured form is the obvious want, and two decisions about it are already made.

It will not be attached to `-o`. That option selects the format of the answer, and warnings are not part of the answer; coupling them would delete the two combinations people actually want -- JSON records with prose warnings, for a pipeline watched from a terminal, and plain records with structured warnings -- and would put a global effect on a per-command option. A separate global switch, `--warn-format` or similar, keeps the two axes independent. There is good precedent for structured diagnostics on standard error kept behind their own flag, `rustc --error-format=json` being the closest.

And warnings will not be folded into the output stream on standard output either. Standard output carries the answer and standard error carries remarks about it, and mixing them would make every consumer filter record types for a feature most do not want, would fill a saved `goro hash` run with remarks that vary between runs and are not about content, and would stop `wc -l` counting files. The interleaving that a single stream would preserve is worth nothing here in any case, since the order of the output is unspecified.

**A summary instead of warnings**
Collapsing repeats into one line at the end of a run -- "500 files had uninterpretable tag data" -- would keep the signal while losing the volume, which is often what somebody actually wants when they reach for suppression. It would not replace `--no-warn`, since a summary still reports the condition and therefore still belongs in the exit code; the two compose. It waits with everything else that is really about output capabilities. There is a great deal to do there and none of it is why anybody would want this tool: the audio hashing and the query language are what have to be good first.

**Reading options from somewhere other than the command line**
A scheduled run that wants a standing `--no-warn=data` would rather say so once. An environment variable is the cheap version and needs no file format at all; a configuration file is its own feature, with discovery, precedence and syntax to settle. Neither is worth building before somebody is using Goro often enough to be inconvenienced by its absence.

**A misspelled identifier in an open namespace matches nothing, silently**
`vorbis::artistt` is not an error and never can be, an open namespace admitting any name by design, so it resolves to absent for every file and the predicate quietly selects nothing. This is the one hole in the promise that every error is reported before a file is opened, and it is not obvious what should be done about it. Accepting it is defensible, since the alternative is to constrain the thing that makes open namespaces useful. The other candidates are a notice at the end of a run for any identifier that was absent from every file examined, an `--explain` mode showing what each identifier resolved to, and a strict flag that rejects unknown names in open namespaces. Each has a different cost and none is clearly right, so the question is left open rather than answered badly. Checking names against each format's own naming rules was considered and rejected; see the rationale.

**Boolean data from tags**
No identifier yet reads a boolean from tag data, though several formats record flags that would make natural ones -- the iTunes compilation flag, recorded in Id3v2's `TCMP` frame and in similarly named fields of other formats, being the obvious example. The predicate language is ready for one: a boolean identifier would be absent, unusable or a multivalue like any other, would not be definite, and would therefore have to be compared, as in `compilation == TRUE`, before being combined with a logical operator. What is not settled is what tag text such an identifier accepts as true and as false, and whether `NUMBER()` and `STRING()` should then accept booleans -- `1` and `0`, `"true"` and `"false"`, or something else -- together with a conversion in the other direction. Both are left until the first such identifier gives them something concrete to answer to.

**Listing files whose predicate could not be answered**
`goro list` leaves out a file whose predicate evaluated to unusable. A predicate can already reverse that for any condition it chooses, with `FALLBACK(condition, TRUE)`, but there is no switch on the command to reverse it wholesale. One would be cheap to add, since the distinction between false and unusable survives to the command; it waits until somebody wants it often enough that rewriting the predicate is a nuisance.

**An exit code for a predicate that could not be answered**
Considered and not added. Every file whose predicate could not be answered was the subject of at least one data warning, so `--strict-exit-code` already reports it as `10` -- unless data warnings were suppressed, in which case the caller has said that uninterpretable data is not a problem for them. Whether a script needs to tell "some tag data was junk" from "some junk tag data changed the answer" is the question that would reopen this, and it wants a real script to answer it.

**Should the global `year` consult nonstandard fields?**
`year` reads a Vorbis comment's `DATE` field, which is the standard one, but some files record the year in a field called `YEAR` instead, and for such a file the global namespace finds no Vorbis year at all; it falls through to the other formats, or is absent. Whether `year` should also consult `YEAR` when `DATE` is missing -- and whether other global identifiers have similar nonstandard but common fields behind them -- is better decided against a real collection than in the abstract.

**Set inclusion operator (`x IN (a, b)`)**:
Interesting but not must-have; in case array types appear in our grammar, there are potential interops to consider. Note that it cannot be implemented as syntactic sugar around OR, tempting as that looks: an operator is a single quantifier scope, so while `x IN (a, b)` and `x == a OR x == b` agree under the default existential quantifier, they part company under `ALL()`, where the first asks that every occurrence match one of the two and the second asks that every occurrence match `a` or that every occurrence match `b`.

**Using STRING() on durations, bytecounts etc to produce a human-readable representation**:
STRING() is intended as a type conversion function only, if formatting is required for presentation reasons that concern should be handled entirely separately

**Multi-candidate priority fallback (COALESCE)**
Removed prior to first publication. The originally-intended use case (keep working when data is missing or invalid) is covered by FALLBACK(). A true priority-select-among-several-candidates construct is not reducible to FALLBACK() + AND/OR in general — the natural rewrite using guarded disjunction is value-equivalent but cannot replicate exhaustive auditing of unused candidates for unusable data without abandoning AND/OR short-circuiting, which was deliberately kept so that a state test can guard a value before an operator reaches it. Revisit only if a real need for that specific behavior (auditing candidates that end up unused) emerges.

Initial draft text was:
  > **COALESCE(expr1, expr2, ...)**
  > : the result of this function is equal to the first of its arguments that has a non-null value. This function can accept any number of arguments. All arguments are evaluated regardless of whether an earlier one already produced a non-null result. Note that, in contrast to `FALLBACK()` and `ISNULL()`, this function will not suppress warnings if one of its inputs is a tainted null value.
  >
  > (Quoted as drafted, in the vocabulary of the time: what it calls a tainted null is now an unusable occurrence, and `ISNULL()` has been replaced by the `IS` operator.)
  >
  > Examples:
  >
  > - `COALESCE("foo", "bar")` results in `"foo"`
  > - `COALESCE(genre, "foo")` results in the value of `genre`, or `"foo"` if that is null
  > - `COALESCE(id3v2::COMM, "no comments")` results in the value of the Id3v2 COMM frame, or "no comments" if such a frame does not exist (in which case `id3v2::COMM` will be null)
