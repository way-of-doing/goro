# Deferred features

This file contains a list of features that were considered and thought potentially worthy of inclusion, but not immediately. Its purpose is to hold these interesting thoughts until their time arrives.

## Postponed features

**Warning deduplication clarification**
This part is not in the spec exactly because it cannot yet be demonstrated:

> Note that two _different_ conversion functions applied to the same identifier are different sources and warn separately, since the symbol at the top of the source differs. The current function set has only one conversion that can fail, so this cannot yet be demonstrated.

**Redundant grouping parentheses around a modified operand**
Because a modifier may only appear within an `operand`, and a parenthesized expression is a `primary` rather than an `operand`, `(ALL(genre)) == "x"` does not parse even though `ALL(genre) == "x"` does. Adding parentheses that ought to be redundant therefore changes a valid predicate into an invalid one, which is mildly surprising. Fixing it means admitting a parenthesized operand into the `operand` production, which introduces an ambiguity with the parenthesized expression of a `primary` that the parser would have to resolve by type. Not worth doing unless somebody actually trips over it.

**Accepting `--filter` on `goro hash`**
Only `goro list` currently accepts a predicate, although the predicate documentation describes the option generically and filtering is a core feature rather than a listing feature. Hashing a subset of a collection is an obvious thing to want. This is deliberately postponed rather than overlooked: once predicates are implemented for `list`, extending another command to accept one is a triviality compared with the work of getting predicates right, and doing `list` alone first keeps that work from spreading across commands before the shape of it has settled.

**Exposing the tag revision to predicates**
The revision of a file's Id3v2 tag is known while it is being read, and an identifier reporting it would be trivial to add. It is worth more than it looks. Goro hides the differences between revisions, but a handful of quirks remain that cannot be hidden, and they are all confined to specific revisions. Being able to write a predicate against the revision would let a user both locate the files that a given quirk can affect and exclude them from a run, which in turn means the remaining files are known to be free of it. That is the difference between a documented caveat a user has to reason about and one they can mechanically rule out. The open questions are what the identifier should be called, whether it belongs in the `id3v2` namespace or alongside `file::size`, what it resolves to for a file with no Id3v2 tag at all, and what type it has, given that `2.3` as a number would compare unhelpfully against `2.4`.

**Should a warning name the tag format behind a global identifier?**
A global identifier such as `artist` resolves on a best-effort basis across several tag formats, so a warning naming `artist` as its source tells you that a file has bad data but not which of its tags holds it. Since the purpose of the warning is to send somebody to fix the data, naming the format that actually produced the taint would be more useful. The source machinery tracks the identifier that was written rather than the format it delegated to, so this needs the warning to carry something beyond its source, and it should not be allowed to make two files with the same problem report differently.

**Distinguishing tag data that is not text from tag data that is absent**
A frame or item that carries text resolves to that text whether or not the tag library recognizes it, which covers the non-standard text frames that appear in the wild. Data that is genuinely binary, such as an attached picture in an Id3v2 `APIC` frame or an APE `Cover Art (Front)` item, has no sensible string and currently resolves to an untainted null -- the same value as data that is not there at all. It is worth deciding whether the two deserve to be told apart, since a predicate asking whether a file has cover art cannot currently be written.

**Addressing a single `TXXX`, `COMM` or `WXXX` frame by its description**
These frames are distinguished from one another by a description rather than by their identifier, so Goro currently surfaces them as a multivalue of strings that carry the description inside them. Selecting one by description needs a name made of two parts, the frame and the description, where the rest of the language only ever names one thing. A further `::` part would be the obvious spelling, but it would mean an identifier whose parts are not all namespaces, which wants thinking about before it is committed to.

**What is the scope of the regex timeout -- single match?**
Potentially just fold this into a timeout for evaluating the whole predicate per-file? Also make it configurable?

**Set inclusion operator (`x IN (a, b)`)**:
Interesting but not must-have; in case array types appear in our grammar, there are potential interops to consider. Can also be implemented as simple syntactic sugar around OR.

**Using STRING() on durations, bytecounts etc to produce a human-readable representation**:
STRING() is intended as a type conversion function only, if formatting is required for presentation reasons that concern should be handled entirely separately

**Multi-candidate priority fallback (COALESCE)**
Removed prior to first publication. The originally-intended use case (keep working when data is missing or invalid) is covered by FALLBACK(). A true priority-select-among-several-candidates construct is not reducible to FALLBACK() + AND/OR in general — the natural rewrite using guarded disjunction is value-equivalent but cannot replicate exhaustive taint-auditing of unused candidates without abandoning AND/OR short-circuiting, which was deliberately kept for ISNULL()'s sake. Revisit only if a real need for that specific behavior (auditing candidates that end up unused) emerges.

Initial draft text was:
  > **COALESCE(expr1, expr2, ...)**
  > : the result of this function is equal to the first of its arguments that has a non-null value. This function can accept any number of arguments. All arguments are evaluated regardless of whether an earlier one already produced a non-null result. Note that, in contrast to `FALLBACK()` and `ISNULL()`, this function will not suppress warnings if one of its inputs is a tainted null value.
  >
  > Examples:
  >
  > - `COALESCE("foo", "bar")` results in `"foo"`
  > - `COALESCE(genre, "foo")` results in the value of `genre`, or `"foo"` if that is null
  > - `COALESCE(id3v2::COMM, "no comments")` results in the value of the Id3v2 COMM frame, or "no comments" if such a frame does not exist (in which case `id3v2::COMM` will be null)
