# High level architecture

Goro is comprised of the following logical components. A run passes through them in this order: the
input interface reads the command line, the predicate compiler reads the predicate if there is one,
the pipeline planner builds what is to be done to each file, the executor does it to every file
discovery yields, and the output renderer writes the results, while warnings and the exit code are
gathered on the side.

```
command line ─► input interface ─► predicate compiler ─► pipeline planner ─► executor ─► output renderer
                                     │                      │   per file:     │
                                     │ compiled predicate ─►│   predicate     │─► warning sink ─► standard error
                                     │                      │   stage         │─► run tally ──► exit code
                                     ▼                      ▼
                          identifier catalog          file data, loaded as far as needed
```

## Input interface

This component owns the definition of command line input that Goro understands and the associated human-readable explanation. Its goal is to read the incoming command line and parse it into a structured object graph or, when appropriate, display usage instructions.

A rejected command line returns `2` before anything else happens; unknown options are rejected rather than ignored. Two options whose syntax the command line library cannot express are rewritten before it sees them: a `--no-warn` given no categories is given an empty list, so that the mistake is reported for what it is, and a separately written `--filter` value is attached to its option, so that one beginning with a dash is not cut short.

## Predicate compiler

This component owns the reading of predicate expressions. It converts the text of a predicate, as received from the command line by the input interface, into an immutable object graph that can be evaluated against one file at a time. Reading a predicate happens in three stages, each handing the next a structure of its own.

The **lexer** turns the text into tokens, taking every token as long as it can be and discarding insignificant whitespace while treating it as significant inside a literal, since a unit suffix may not be separated from the number it belongs to. A literal leaves the lexer decoded: a string with its escapes processed and a note of whether it was raw, a number, bytecount or duration as the value it denotes. Spellings borrowed from other languages, such as `=` and `~=`, are tokens of their own, so that they can be answered in context rather than rejected as stray characters.

The **parser** builds a syntax tree from those tokens by recursive descent with a single token of lookahead, which is all that is needed to tell an identifier reference from a function call, since the two differ only by a following parenthesis; the lexer therefore never needs to know which names are functions. The syntax tree is the predicate as written: parentheses and modifiers stay where they were, and every node keeps the span of text it was parsed from.

The **binder** performs static analysis. It resolves every identifier against the identifier catalog it is given, assigns a type to every sub-expression bottom-up together with whether it is definite, folds each modifier into the operator that reads it -- a quantifier onto its operand, a comparison mode onto the operator -- and verifies the rules the predicate documentation requires to hold. Its output is a tree of bound nodes, typed in C# by the data they produce, so that past this point an evaluation that confused one type with another would not compile.

Static analysis is where the guarantee that a run never aborts halfway through is earned. Every error that does not depend on the contents of a particular file is reported here, before the executor is given any work; see [Predicates](concepts/predicates.md) for what that covers. The lexer and parser stop at the first error, a broken predicate having no tree to analyse; the binder then reports every independent error it finds, a failed sub-expression taking an error type that silences the errors it would otherwise cause.

Diagnostic quality is a first-class concern of this component rather than an afterthought, because a predicate is written by hand and will frequently be written slightly wrong. An unresolvable identifier suggests the closest defined one, a chained comparison says that comparisons cannot be chained rather than merely reporting an unexpected token, and an operator spelled the way another language spells it, such as `=`, `<>`, `&&`, `!` or `!~`, is answered with the one meant, or with each candidate where more than one is plausible, as for `~=`, which may have been meant as `=~` or as `!=`. A suggestion is a replacement for a span of the predicate, composed from what the user wrote, so that it changes only what was wrong. A diagnostic carries its message as data rather than as text: which of a closed set of messages it is, with the user's text, names, types and numbers it concerns, and never a word of prose. A message provider turns it into a sentence at the edge of the program, and the English one spells out every message whole, one sentence for each case. Composing a message from fragments would build English grammar into the engine, so nothing in it does.

This component also assigns the warning sources used to deduplicate warnings during evaluation, by giving each structurally distinct value sub-expression that can produce an unusable occurrence its own identity, and gives every such node the text it was parsed from, so that a warning can quote what the user actually wrote. It compiles every pattern, and prepares every range endpoint for comparison, once.

The object graph this component produces is immutable and holds no per-file state, so that it can be shared by the arbitrarily many concurrent pipeline invocations that will evaluate it.

## Identifier catalog

This component owns every namespace and identifier a predicate can name. For each identifier it declares a type, whether the identifier is definite, and a binding that resolves it for one file. A closed namespace knows its identifiers in advance; an open one declares a string-typed identifier for whatever name it is asked about. The compiler is given a catalog rather than reaching for the built-in one, which is what lets a test declare identifiers holding whatever a scenario needs.

What a binding reads comes from the file's data, which is loaded only as far as an evaluation asks for it: nothing at all for the path and name, file system metadata for the size, the audio properties for the duration, the tags for a tag identifier. Each kind of data is loaded at most once per file. Failing to load one makes the file unreadable, which abandons its evaluation.

## Evaluator

The evaluator is the bound tree itself: each node evaluates itself against one file, through an evaluation context created for that file and dropped after it. The context holds the file's data as it is loaded and the warning sources reported so far. A comparison, range or regex operator evaluates every operand, is false if any is absent, and otherwise iterates over every combination of occurrences, universally quantified operands outermost, without stopping once the result is settled; see [Evaluation](concepts/evaluation.md). Evaluation is synchronous: the library that reads tags is, and concurrency belongs to the executor.

Every string an operator compares is prepared by normalization in the operator's mode, a pure function that never throws; see [Normalization](concepts/normalization.md).

## Pipeline planner

This component reads the structured data describing the invocation of Goro, and dynamically constructs an async execution pipeline that describes the entirety of the operations to be performed on each input file. This pipeline object graph can be invoked concurrently an arbitrary number of times. On each invocation, one absolute file path is provided as input, and one file outcome is produced as output: the result to render, if any, what the file counts towards in the run's outcome, and the warnings it produced. The structure of the result is potentially different for each Goro command.

Operations such as reading file contents and hashing, reading tags, writing tags, etc are all performed as part of each pipeline invocation.

Where a command accepts a predicate, evaluating it yields one of three outcomes for the file -- true, false, or unusable -- and the pipeline stage that applies it must keep the three apart rather than reducing them to a yes or no, since what an unusable outcome means is decided by each command and not by the predicate. A file found unreadable during evaluation produces its one file warning and nothing else; whatever data warnings its evaluation had met are dropped with it.

Any per-file state that a pipeline stage needs -- for example the set of already-warned warning sources used to deduplicate predicate warnings -- belongs to a single invocation and must not be stored on the pipeline object graph itself, which is shared across concurrent invocations for the whole run. Conversely, anything derivable from the invocation parameters alone, such as the identification of the distinct warning sources a predicate can produce, is computed once when the pipeline is planned and then shared freely.

## Executor

The executor is responsible for invoking the pipeline for each input file, managing concurrency, and collecting the pipeline results. Each file outcome is recorded in the run's tally, which hands the file's warnings on as one batch and counts what the file counts towards; the result, if there is one, is passed on to the output renderer. An exception escaping a pipeline invocation is a defect, not a fact about the file, and fails the run.

## Warning sink

This component receives each file's warnings as one batch, from any number of concurrent invocations, and writes them to standard error, each as a whole line. It drops a suppressed category on arrival, so that a suppressed warning is never written and never counted. What it did produce, together with the tally's counts, is what the exit code is chosen from.

## Output renderer

This component receives a stream of result objects produced by the pipeline, and produces output to standard out.
