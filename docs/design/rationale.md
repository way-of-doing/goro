# Rationale

“Simple things should be simple, complex things should be possible.” --Alan Kay

“Success has to be like falling into a BIG PIT. You can't help but win. You try to do it wrong, but nope, you fell in The Pit of Success. You Win Again!” --Rico Mariani

## Predicates initial wishlist

The design of the predicate grammar and specification in Goro was kickstarted by collecting a set of wishes and related observations made when these wishes come in contact with practical reality. This section documents the most important of them.

**We must be able to write `genre == "pop"` because it is most natural and convenient.**
This means that
- comparisons must not be too strict by default (you didn't write Pop with a capital P, it's Motörhead not Motorhead, etc)
- but there must be a reasonable way to take full control of the comparison

**A single value and multiple values must be easily handled with the exact same syntax.**
Data like artist and genre (and technically, almost everything else too) can appear zero, one, or multiple times for the same file. This will vary from one file to another and may vary across time even for the same file. It means that the syntax that answers the question "is this tagged to be pop", must be meaningful and make sense in all of these scenarios. Most importantly, the implication is that any operation on a single value must be syntactically and semantically valid even if the single value is logically replaced with a collection.

**It is not acceptable to stop batch processing midway through because of an error.**
Goro must be able to handle per-file hiccups gracefully.

**Strongly typed expression language.**
In a very constrained setting, perhaps automatic type conversions can work. In real life, and especially if you start adding features such as even simple addition and string concatenation, type juggling becomes a minefield of footguns.

**Multivalues design invariant.**
_Every_ value in a predicate is conceptually a bag: an unordered collection in which the same value may appear more than once and duplicates are significant. A "simple" value is a bag of cardinality one, and everything stated in the documentation about simple values holds unchanged for multivalues of cardinality one. No construct in the language is able to distinguish the two, and any proposed addition that could is for that reason alone the wrong design. A bag of cardinality zero, or one with a single `null` value, cannot appear in the language (plain `null` should be used instead), and if any proposed addition allows it, it is the wrong design.

If in the future this decision ends up being revisited and empty bags are allowed, we should consider the consequences -- among others, it would then be possible to have `ALL()` vacuously match with an empty bag.

## Predicates Q & A

This section is structured as questions-and-answers, where the questions have been carefully selected to reveal the reasoning behind design decisions.

Q: **Why does a failed type conversion result in a tainted null value?**
A: There are basically three options: a) result in some special value that signifies the failure, b) result to some default (e.g. zero or empty string), and c) error out and do not continue. Erroring out is out of the question because it could cause an operation on countless files to be aborted midway because one of them is not correctly tagged. Defaulting to something "neutral" is the classic type conversion footgun that could lead countless evaluations to produce an unintended result. Therefore the decision was made to use a special value, in particular one that will almost certainly result in the evaluation of a predicate to false: this avoids processing files where something unexpected happened while evaluating the predicate, does not stop a lengthy job midway, and due to the taint still allows surfacing the situation to the user.

Q: **Why do comparisons error out on different types instead of converting implicitly?**
A: A combination of reasons: the more types there are, the more complicated the implicit conversion rules become; conversions from string can easily fail and this is surprising when it happens implicitly; and the types of sub-expressions are very easily determined so it's not too much work to have an explicit conversion where needed.

Q: **Why does e.g. `STRING(120s)` result in `"120"` and not something prettier like `"120s"`?**
A: STRING() is a type conversion function; formatting for presentation should not be its concern.

Q: **Why do `ISNULL()` and `FALLBACK()` suppress the tainted null value warning? And why does `COUNT()` not do the same?**
A: `ISNULL(expr)` is the sanctioned way of checking if its argument has a usable value when that value comes from a conversion and is therefore not guaranteed. `FALLBACK()` is the sanctioned way of saying "if this doesn't exist or isn't usable, substitute something else instead". Both therefore have to be able to look at a tainted null without reporting it. `COUNT()` has no such role: it answers a question about its argument and returns a number derived from it, so it consumes the taint exactly as an operator would, and warns. Functions that merely hand a null onwards, such as `NUMBER()` and `STRING()`, are neither suppressors nor consumers, and stay silent because at that point the taint is still propagating rather than being consumed.

Q: **Why does `ISNULL()` return true for a multivalue that merely contains a null, rather than only for a plain null?**
A: Because `ISNULL()`'s job is to be the safe guard in front of a value whose usability is not guaranteed, and almost every tag identifier can turn out to be multivalued. If a multivalue were never considered null, then `NOT ISNULL(NUMBER(x))` would pass for a multivalue holding one good value and one tainted null, the guarded expression would then use the tainted null, and the warning the guard was meant to avoid would fire anyway — the guard would only ever work on data that happened not to be multivalued. Reading `NOT ISNULL(expr)` as "every value of `expr` is usable" is the conservative choice, and conservative is the right default when the consequence of being wrong is silently skipping files.

Q: **Why are tainted null warnings deduplicated per file and per source, rather than once per run or once per occurrence?**
A: Once per run would be nearly useless, since it would only tell you that something somewhere in a hundred thousand files is malformed, when the whole point of the warning is to let you go and fix the data. Once per occurrence would be noise: a predicate that mentions the same tag three times has one bad tag to report, not three. Deduplicating by the _source_ of the datum rather than by the place in the predicate that touched it is what makes the number of warnings match the number of things that are actually wrong.

The intended implementation is that every value which can be tainted carries a source, and that a source is the sub-expression which produced it: a symbol together with the sources of its arguments, in order of appearance, compared recursively. Note that the sources of the arguments are those of the argument sub-expressions themselves and do not depend on which value happened to flow out of them at runtime, so a source is a static property of the predicate. Since a source is therefore precisely the shape of the predicate's own syntax tree with positions erased, the distinct sources can all be identified once when the predicate is read, by assigning each structurally distinct sub-expression a small integer; a taint then carries that integer and the per-file deduplication set is a bitset indexed by it. Interning the sub-expressions bottom-up keeps the comparison cheap, because by the time a parent is considered each of its children has already been reduced to an integer.

Two consequences are worth keeping in mind. The set of warnings a predicate is capable of emitting is enumerable before any file is read, which is directly testable. And the modifiers `ALL()`, `ANY()` and `LITERALLY()` must be transparent when sources are assigned, since they do not produce values of their own: treating them as distinct symbols would report a single bad tag twice.

Note that a warning's identity and its presentation come from different places. The interned source decides whether a warning has already been emitted and is deliberately position-independent, which is why the modifiers are transparent in it. What the warning quotes comes instead from the syntax node the value was actually born at, together with the span of predicate text that node was parsed from. Keeping the two separate is what allows the message to reproduce what the user wrote while the deduplication still treats two differently-written but equivalent fragments as one thing. The distinction also explains why transparency needs no special handling at the top of a source: a modifier can never produce a tainted null in the first place, so it is never the birth node of one.

Q: **Why are durations whole seconds, when a file's actual playing time is not?**
A: Because the alternative is that `file::duration == 4m5s` silently matches nothing, which looks exactly like a collection that genuinely has no such file. Answering "you are not supposed to use equality on durations" would leave a loaded footgun in the language for the sake of precision nobody wants: nobody writes `4:40.123456`, and if sub-second literals existed the same person would meet the ordinary floating-point equality trap in a new place instead. Truncating makes the precision of a value equal to the precision the syntax can express, which is the property that makes `==` trustworthy. As a side effect it also makes results reproducible, since decoders disagree about the length of a VBR file by a few milliseconds and a boundary comparison would otherwise wobble between runs and between machines.

Q: **Why must the period in a number literal be followed by a digit, so that `1.` is invalid?**
A: A trailing period was never intended to be meaningful in the first place, since it expresses nothing that `1` does not. It also causes trouble: allowing it makes `1..100` ambiguous with a pair of adjacent number literals instead of a range.

Q: **Why may `ALL()`, `ANY()` and `LITERALLY()` only be applied to the operand of a comparison, when they would be harmless no-ops almost everywhere else?**
A: The initial intent was indeed to allow them anywhere and accept that `COUNT(ANY(artist))` means nothing in particular, on the grounds that a permissive rule is a shorter rule. Two things argued the other way.

The first is action at a distance. A modifier only takes effect when an operator consumes the value it was applied to, so the further a modifier can sit from that operator, the less the comparison says about how it will be performed. Today the distance is bounded by the predicate text, but a future in which a value can be given a name, or held between statements, would allow a value to be annotated arbitrarily far from every comparison that eventually reads the annotation. That is materially worse than the same problem for tainted nulls, which also travel: a taint announces itself with a warning when it is finally used, whereas a modifier silently changes the answer.

The second is that the two directions are not equally reversible. Restricting now and relaxing later is backward compatible, because predicates that were valid stay valid and a construct merely stops being an error -- a future `REPLACE()` that wanted to read the normalization property of its pattern argument could simply document that it does. Being permissive now and restricting later breaks predicates people have already written. Since the expressiveness is recoverable and the compatibility is not, the restriction is the choice that keeps both options open.

The restriction also removes several questions that would otherwise have to be answered, and answered durably: what modification a function's result carries when its arguments were modified, whether that is the union over the arguments or comes from a designated one, whether modifications attach to a whole multivalue or to its individual values, and whether `ISNULL()` should respect the quantifier. The last of these is the sharpest, because `NOT ISNULL(ALL(x))` reads like "none of them are null" while it would mean "at least one is usable" -- a guard weakened by the very construct that makes it read stronger.

Note that the restriction is directional. A modifier may be applied to a function call, so `LITERALLY(FALLBACK(genre, "pop"))` is valid; only the reverse, a modifier inside a function's arguments, is forbidden. Modifiers go on the outside.

Q: **Why are the iteration loops over two multivalue operands nested by quantifier rather than in the order the operands were written?**
A: Nesting by written position is the more obvious rule and the more expressive one, since it can produce both the "for every ... there is some ..." and the "there is some ... for every ..." readings, where nesting by quantifier can only produce the first. It was rejected because of what it costs in exchange.

Nesting by position makes an operator's result depend on which side each operand was written on. The visible casualty is that `ALL(a) == b` and `b == ALL(a)` stop meaning the same thing, but the more dangerous one is that the identity `x < y` if and only if `y > x` stops holding: `a < ALL(b)` and `ALL(b) > a` would be different propositions. Flipping a comparison and swapping its operands is a rewrite people perform without registering it as a transformation at all, so that edge is one readers would walk into unaware, whereas moving an `ALL()` across an `==` at least looks like a change.

What nesting by quantifier gives up is the "there is some value of X that compares thus to every value of Y" reading, and for the operators where anyone would want it, it is available in a better form anyway. For equality it is degenerate: it can only be true when every value of the second operand is the same value. For the ordering operators it is really a statement about aggregates -- `a > ALL(b)` is "the largest value of `a` exceeds the largest value of `b`" -- which a `MAX()` or `MIN()` function would say far more legibly than a quantifier ever could, and which can be added later without changing the meaning of any predicate written in the meantime. The reverse is not true: adopting nesting by position and later wanting order-independence would change what already-written predicates mean.

There is also a plain simplicity argument. Nesting by quantifier supports the flat statement that the order of an operator's operands never affects its result, which is what a reader's intuition already assumes; nesting by position requires the reader to hold an extra rule and needs a warning attached to it. The reading that nesting by quantifier keeps is also the one with a name: `ALL(a) == b` is a containment test.

Q: **Why is a name an identifier cannot spell written as a quoted string, rather than with a syntax of its own?**
A: Because it was the option with the least hassle attached, and nothing forced a more elaborate one. A dedicated delimiter would have meant choosing characters, and the obvious candidates are the ones a future addition would want: braces are what a set literal would use, and square brackets are what indexing would use. Reusing the string syntax spends no characters at all, and it inherits escaping rather than requiring an escape mechanism to be invented alongside it.

It is worth noting why this was cheap, because it was not luck. The restrictions Goro places on itself are what made it possible: identifiers are resolved statically, a quoted part may only follow `::`, and the first part of an identifier is never quoted. Those three together mean a string at the start of an expression is unambiguously a value and a string after `::` is unambiguously a name, so no lookahead and no new token are needed.

A function taking the namespace and name as arguments was the alternative, and it generalizes better to names made of several parts, such as addressing one `TXXX` frame by its description. It was not chosen because that case does not exist yet. Further escape hatches can be opened whenever something concrete needs them, and designing one now for a requirement nobody has stated would mean the common case paying for the rare one indefinitely.

Q: **Why does a run that emitted warnings still return an exit code of zero by default?**
A: Because for the collections Goro is built for, warning is the normal case rather than the exceptional one. The whole tainted null design exists on the premise that a library of a hundred thousand files contains malformed tags, so a run across one will frequently have something to report while producing entirely correct output. A shell treats every non-zero code as a failure, so returning non-zero whenever there was a remark to make would break every pipeline Goro was put into, near-permanently, for a condition that is expected. The convention is not unheard of -- Borg reports warnings as exit code 1 -- but its own issue tracker carries reports from users who could not tell whether a successful-looking run had worked, which is the failure mode in question.

The Pit of Success argument does cut the other way, since the person who most needs the signal is the one who never thought to ask for it. The costs are asymmetric, though: a strict default silently breaks working pipelines again and again, whereas a lenient default costs somebody who wanted strictness a single line of documentation. And warnings go to standard error either way, so nothing is hidden from a person watching the run; only the exit code stays quiet.

Q: **Why is "nothing matched" worth an exit code when warnings are not?**
A: Because it is unambiguous and it never fires spuriously. An empty result is a fact about the query rather than about the state of the collection, it is exactly the distinction that `grep` and `diff` spend an exit code on, and a script branching on it is doing something sensible rather than working around a quirk. Warnings, by contrast, say that some file somewhere holds data that could not be interpreted, which on a large collection is almost always true and therefore almost never actionable as a yes-or-no signal.

