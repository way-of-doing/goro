# Exit codes

## NAME

exit code - the status a Goro command returns to its caller

## SYNOPSIS

*command* [--strict-exit-code] [--no-warn[=*category*,...]]

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
| `2`  | no          | The run never started: the command line, a pathspec, or the predicate was rejected, and nothing was processed
| `10` | yes         | The run completed, and at least one **data** warning was emitted
| `11` | yes         | The run completed, and at least one **file** warning was emitted: a file could not be read and was therefore not processed
| `20` | yes         | The run completed; files were examined, but none of them matched
| `21` | yes         | The run completed; the pathspecs matched no files at all

A run that is **interrupted**, by Ctrl-C or by the console window being closed, does not appear in
this table, because the code is not Goro's to choose. The process is terminated rather than
returning, so the value a caller observes is whatever the platform reports for a process ended
that way, and it differs between platforms; consult the platform's own documentation for what to
expect. Goro does not fabricate a code of its own here, since on some platforms doing so would
discard the very information that tells a caller the process was killed rather than finished.

An interrupted run also leaves whatever output it had produced by that point, which for `-o json`
is an array that was never closed and therefore not a valid JSON document. See
[Implementation notes](../implementation.md) for why the output is streamed rather than held back.

### Ranges

The codes divide by how many digits they have, which is the quickest way to remember them:

- **one digit** -- the run failed and produced no usable result. `1` through `9` are Goro's, of
  which two are assigned above
- **two digits** -- the informational range. The run completed, and something is worth looking at.
  Only ever returned when `--strict-exit-code` is given. The tens digit names the concern: a code
  in the **`1`x** group says something about the data the run read, and one in the **`2`x** group
  says what the query found. A caller can therefore test for either concern without enumerating
  the codes within it, and each group has room to grow
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
elif [ "$code" -ge 20 ]; then
    : # completed; the query came up empty
elif [ "$code" -ge 10 ]; then
    : # completed, but something about the data read is worth looking at
else
    : # did not produce a usable result
fi
```

So a code of `0` means the run completed cleanly, a code of `10` or greater means the run
completed but something is worth looking at, and any other non-zero code means the run did not
produce a usable result.

### When more than one applies

A single run can qualify for several informational codes at once -- it may emit data warnings,
emit file warnings, and match nothing, all three. Two rules decide which is returned:

- **A code in the `1`x group takes precedence over one in the `2`x group.** Something wrong with
  the data the run read is worth reporting ahead of what the query found, because it may well be
  the reason the query found what it did: a value that cannot be interpreted makes every
  comparison using it false, so an empty result reported on its own would hide the more useful of
  the two facts.
- **Within a group, the higher-numbered code wins.** So `11` takes precedence over `10`: a data
  warning means the output is complete and some data in it was disregarded, whereas a file warning
  means the output is **incomplete**, so anything concluded from it may be wrong for a reason the
  output does not show. That is the more serious thing to learn. Any code added to a group later
  must be numbered to keep this rule true, which is the constraint that keeps the ordering
  derivable instead of tabulated.

So the order today is `11`, then `10`, then `20` or `21`. Codes `20` and `21` cannot both apply,
since either files were examined or none were found, and a file that could not be read does not
count as examined for the purposes of `20`: a run whose only discovered file was unreadable
returns `11` rather than `20`.

### Suppressed warnings and the exit code

A warning suppressed with `--no-warn` was not produced, so it contributes nothing to the exit code
either. Suppressing a whole category removes the corresponding code from the outcomes a run can
return: under `--no-warn=data` a run never returns `10`, and under `--no-warn=file` it never
returns `11`.

This is the point of the option rather than a side effect of it. Suppressing a category is a
statement that the condition is not a problem, not a request to be told about it more quietly, and
a run cannot both disregard a condition and report it. It is also what makes the `2`x codes
reachable on a collection that warns routinely: `--no-warn=data` is how a caller who has accepted
that some tags are junk gets to see whether a query matched anything.

### Relationship to warnings

The conditions that produce warnings are described under [Warnings](warnings.md). Note that
`--strict-exit-code` reports only *whether* a run produced warnings of a given category, not how
many or about what; the warnings themselves are the record of that, and a run over a large
collection may emit a great many of them while still returning a single `10` or `11`.
