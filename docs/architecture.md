# High level architecture

Goro is comprised of the following logical components.

## Input interface

This component owns the definition of command line input that Goro understands and the associated human-readable explanation. Its goal is to read the incoming command line and parse it into a structured object graph or, when appropriate, display usage instructions.

## Pipeline planner

This component reads the structured data describing the invocation of Goro, and dynamically constructs an async execution pipeline that describes the entirety of the operations to be performed on each input file. This pipeline object graph can be invoked concurrently an arbitrary number of times. On each invocation, one absolute file path is provided as input, and one result object is produced as output (the structure of this result object is potentially different for each Goro command).

Operations such as reading file contents and hashing, reading tags, writing tags, etc are all performed as part of each pipeline invocation.

## Executor

The executor is responsible for invoking the pipeline for each input file, managing concurrency, and collecting the pipeline results. These results might be buffered or otherwise organized if the user has selected an execution option that requires it; afterwards, they are passed on to the output renderer.

## Output renderer

This component receives a stream of result objects produced by the pipeline, and produces output to standard out and standard error.
