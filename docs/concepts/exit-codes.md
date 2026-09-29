# Exit codes

## NAME

exit code - the status a Goro command returns to its caller

## SYNOPSIS

*command* [--strict-exit-code]

## DESCRIPTION

### Overview

Every Goro command returns an exit code describing how the run ended. By default that code
answers one question only: did the run complete? A run that completed returns `0`, whatever it
found and whatever it had to say about the data it read.

This default exists because Goro is built for large collections of imperfect files. A run over a
whole library will frequently have something to report -- a tag that cannot be interpreted, a
file whose data is malformed -- and that is the normal state of such a collection rather than a
malfunction. Since a shell treats any non-zero code as a failure, a command that returned
non-zero whenever it had a remark to make would break every pipeline it was put into, more or
less permanently, while producing entirely correct output. Warnings are therefore written to
standard error, where a person sees them, and left out of the exit code, where a script would
trip over them.

### Making the outcome distinguishable

Some callers do want the finer distinctions, and the global option `--strict-exit-code`, accepted
by every command, provides them. With it, outcomes that would otherwise all be `0` return
distinct codes instead.

The option never changes how failures are reported. A command that fails returns the same code
whether or not the option was given, so adding it to an existing invocation cannot disturb that
invocation's error handling.

### The codes

| Code | Strict only | Meaning |
|:----:|:-----------:|---------|
| `0`  | no          | The run completed. With `--strict-exit-code`, it also means the run had nothing further to report
| `1`  | no          | The run started but could not be completed
| `2`  | no          | The run never started: the command line or the predicate was rejected, and nothing was processed
| `10` | yes         | The run completed, and at least one warning was emitted
| `11` | yes         | The run completed; files were examined, but none of them matched
| `12` | yes         | The run completed; the pathspecs matched no files at all

A run that is **interrupted**, by Ctrl-C or by the console window being closed, does not appear in
this table, because the code is not Goro's to choose. The process is terminated rather than
returning, so the value a caller observes is whatever the platform reports for a process ended
that way, and it differs between platforms; consult the platform's own documentation for what to
expect. Goro does not fabricate a code of its own here, since on some platforms doing so would
discard the very information that tells a caller the process was killed rather than finished.

### Ranges

The codes divide by how many digits they have, which is the quickest way to remember them:

- **one digit** -- the run failed and produced no usable result. `1` through `9` are Goro's, of
  which two are assigned above
- **two digits** -- the informational range. The run completed, and something is worth looking at.
  Only ever returned when `--strict-exit-code` is given
- **three digits** -- not Goro's. Values from 126 upwards are spoken for by shells and by the
  operating system, most commonly for a command that could not be executed or a process that was
  killed by a signal

Further codes may be added within Goro's two ranges as further outcomes prove worth
distinguishing, so a caller that wants to treat a whole category alike should test the range
rather than enumerate the values it happens to know about:

```sh
goro list --strict-exit-code --filter='year < 1970' /music
code=$?
if [ "$code" -eq 0 ]; then
    : # completed, with nothing further to report
elif [ "$code" -ge 10 ]; then
    : # completed, but something is worth looking at
else
    : # did not produce a usable result
fi
```

So a code of `0` means the run completed cleanly, a code of `10` or greater means the run
completed but something is worth looking at, and any other non-zero code means the run did not
produce a usable result.

### When more than one applies

A single run can qualify for several informational codes at once -- it may both emit warnings and
match nothing. **Warnings take precedence**, so such a run returns `10`. This is deliberate: when
a predicate matched nothing and warnings were also emitted, the warnings are quite possibly the
reason it matched nothing, since a value that cannot be interpreted makes the comparison using it
false. Reporting the empty result while staying silent about the warnings would hide the more
useful of the two facts.

Codes `11` and `12` cannot both apply, since either files were examined or none were found.

### Relationship to warnings

The conditions that produce warnings are described under
[Warnings](predicates.md#warnings). Note that `--strict-exit-code` reports only *whether* a run
warned, not how many times or about what; the warnings themselves are the record of that, and a
run over a large collection may emit a great many of them while still returning a single `10`.
