---
status: proposed
size: focused
touches: concepts/predicates.md, concepts/evaluation.md, design/rationale.md, testing.md, src/Goro/Predicates
after: sources-and-fields
branch:
---
# Parameters on conversion targets

## Intent

Let a conversion target take parameters, so that a blob can be decoded as text in an encoding the
predicate names: `ape::bytes("Artist") AS STRING(iso-8859-1)`. The
[sources-and-fields](sources-and-fields.md) line introduces `blob`, the type of recorded data that
is not text. A blob is never promoted to another type, so conversions out of it can be added at
any time, and this line adds the first.

**What a parameter is for.** A parameter supplies what the data cannot tell, and never tests what
it can. An encoding qualifies: nothing in a run of bytes says which encoding wrote it. An image
format does not, images identifying their own, so `AS IMAGE(png)` would be a conversion and a
filter in one; the format is a question for the result.

**Spelling.** A target is optionally followed by parameters in parentheses, and a parameter is an
unquoted _word_: a letter, then letters, digits, `_` or `-`, matched case-insensitively.
`AS STRING(iso-8859-3)`, `AS STRING(utf-8)`, `AS STRING(windows-1252)`. Encoding labels contain
hyphens, which a name cannot, so this takes one token rule that depends on position: inside the
parentheses directly after a conversion target, a word may contain hyphens. The position is known
from the tokens alone -- `AS` is reserved and only ever followed by a target -- so the lexer
recognises it from the three tokens it has just made, as it already looks back one token to join
the clock form of a duration. Hyphens in names everywhere were rejected: `year-1` would then be a
name, which would cost any future arithmetic its minus, or make it the trap CSS `calc()` is for
requiring spaces around one. Gluing touching tokens back together in the parser was rejected too:
`x-mac-roman` does not lex at all, a hyphen before a letter beginning no token.

**What parameters each conversion takes** depends on the source type, statically:

- `blob AS STRING(encoding)` decodes, and the encoding is required. A default of UTF-8 would be
  the implicit guess the rationale rejects for conversions in general, and the encoding is
  exactly what the bytes cannot say. Bytes that do not decode in that encoding give an unusable
  occurrence whose source is the conversion; a single-byte encoding decodes everything.
- Every other conversion takes none, so `5 AS STRING(utf-8)` is an error.

An unknown encoding is an error when the predicate is read, with the closest supported name
suggested; so is a missing one, and a parameter the conversion does not take. The rare labels
holding `:` or `.` are not supported. Two conversions differing only in a parameter are different
warning sources.

**Cost.** Every predicate valid today keeps its meaning, since `AS target (` is a syntax error
today. The spec gains a paragraph and a table row under the conversion operator, the `target` and
`word` productions, a sentence beside the longest-token rule, a constraint bullet and a
deduplication row; `evaluation.md` turns `convert(d, T)` into `convert(d, T, p)`. In code: the
lexer's position rule, parameters on `AsSyntax`, a lookup of conversions by source type, target
and parameters, three diagnostics, and one conversion. The code-page encodings ship with .NET 10,
`System.Text.Encoding.CodePages` being pruned from restore as part of the framework, so no package
is needed.

## Questions

- **Which encodings.** The WHATWG labels are the obvious vocabulary, being what people type; .NET
  names some of them differently.
- **Whether a quoted string is accepted as well as a word**, for symmetry with `field()`'s
  arguments. Nothing needs it; literals can be admitted later without changing a valid predicate.

## Done when

The rationale records why parameters exist, why the encoding is required and why parameters are
unquoted; the predicate documentation, grammar and evaluation document describe them; and the
lexer, parser, binder and conversions implement them, with tests.

## Log

- 2026-10-07 -- Written up from the sources-and-fields line, where the spelling was weighed and
  agreed in discussion. It waits there because nothing produces a blob until the tag sources are
  read.
