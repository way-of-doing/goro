# High level architecture

Goro is comprised of the following logical components.

## Input interface

This component owns the definition of command line input that Goro understands and the associated human-readable explanation. Its goal is to read the incoming command line and parse it into a structured object graph or, when appropriate, display usage instructions.

## Predicate parser

This component owns the reading of predicate expressions. It converts the text of a predicate, as received from the command line by the input interface, into an immutable object graph that can be evaluated against one file at a time.

Reading a predicate happens in distinct stages. A lexer turns the text into tokens, discarding insignificant whitespace while treating it as significant inside a literal, since a unit suffix may not be separated from the number it belongs to. A parser then builds a syntax tree from those tokens using a single token of lookahead, which is all that is needed to tell an identifier reference from a function call, since the two differ only by a following parenthesis; the lexer therefore never needs to know which names are functions. Finally, a static analysis pass resolves every identifier against the table of built-in identifiers, assigns a type to every sub-expression bottom-up together with whether it is definite, and verifies the rules the predicate documentation requires to hold.

Static analysis is where the guarantee that a run never aborts halfway through is earned. Every error that does not depend on the contents of a particular file is reported here, before the executor is given any work; see [Predicates](concepts/predicates.md) for what that covers. Diagnostic quality is a first-class concern of this component rather than an afterthought, because a predicate is written by hand and will frequently be written slightly wrong: an unresolvable identifier should suggest the closest defined one, a chained comparison should say that comparisons cannot be chained rather than merely reporting an unexpected token, and an operator spelled the way another language spells it, such as `=`, `<>`, `&&`, `!` or `!~`, should be answered with the one meant, or with each candidate where more than one is plausible, as for `~=`, which may have been meant as `=~` or as `!=`.

This component also assigns the warning sources used to deduplicate warnings during evaluation, by giving each structurally distinct value sub-expression its own identity, and records for every node the span of predicate text it was parsed from, so that a warning can quote what the user actually wrote.

The object graph this component produces is immutable and holds no per-file state, so that it can be shared by the arbitrarily many concurrent pipeline invocations that will evaluate it.

## Pipeline planner

This component reads the structured data describing the invocation of Goro, and dynamically constructs an async execution pipeline that describes the entirety of the operations to be performed on each input file. This pipeline object graph can be invoked concurrently an arbitrary number of times. On each invocation, one absolute file path is provided as input, and one result object is produced as output (the structure of this result object is potentially different for each Goro command).

Operations such as reading file contents and hashing, reading tags, writing tags, etc are all performed as part of each pipeline invocation.

Where a command accepts a predicate, evaluating it yields one of three outcomes for the file -- true, false, or unusable -- and the pipeline stage that applies it must keep the three apart rather than reducing them to a yes or no, since what an unusable outcome means is decided by each command and not by the predicate.

Any per-file state that a pipeline stage needs -- for example the set of already-warned warning sources used to deduplicate predicate warnings -- belongs to a single invocation and must not be stored on the pipeline object graph itself, which is shared across concurrent invocations for the whole run. Conversely, anything derivable from the invocation parameters alone, such as the identification of the distinct warning sources a predicate can produce, is computed once when the pipeline is planned and then shared freely.

## Executor

The executor is responsible for invoking the pipeline for each input file, managing concurrency, and collecting the pipeline results. These results might be buffered or otherwise organized if the user has selected an execution option that requires it; afterwards, they are passed on to the output renderer.

## Output renderer

This component receives a stream of result objects produced by the pipeline, and produces output to standard out and standard error.
