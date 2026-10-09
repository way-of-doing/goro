# Exit codes

## NAME

exit code - the status a Goro command returns to its caller

## SYNOPSIS

*command* [--strict-exit-code] [--no-warn=*category*,...]

## DESCRIPTION

### Overview

Every Goro command returns an exit code describing how the run ended. By default that code
answers one question only: did the run complete? A run that completed returns `0`, whatever it
found and whatever it had to say about the data it read. Warnings go to standard error and do not
affect the default exit code.

### Making the outcome distinguishable

Some callers do want the finer distinctions, and the global option `--strict-exit-code`, accepted
by every command, provides them. With it, outcomes that would otherwise all be `0` return
distinct codes instead.

The option never changes how failures are reported: a command that fails returns the same code
whether or not the option was given.

### The codes

| Code | Strict only | Meaning |
|:----:|:-----------:|---------|
| `0`  | no          | The run completed. With `--strict-exit-code`, it also means the run had nothing further to report
| `1`  | no          | The run started but could not be completed
| `2`  | no          | The run never started: the command line, a pathspec, or the predicate was rejected, and nothing was processed
| `10` | yes         | The run completed, and at least one **data** warning was emitted
| `11` | yes         | The run completed, and the predicate could not be answered for at least one file: the **unanswered** warning was emitted
| `12` | yes         | The run completed, and part of at least one file could not be read: the **incomplete** warning was emitted
| `13` | yes         | The run completed, and at least one **file** warning was emitted: a file could not be read and was therefore not processed
| `20` | yes         | The run completed; files were examined, but none of them matched
| `21` | yes         | The run completed; the pathspecs matched no files at all

A run that is **interrupted**, by Ctrl-C or by the console window being closed, does not appear in
this table. The process is terminated rather than returning, and the code a caller observes is
whatever the platform reports for a process ended that way.

### Ranges

The codes divide by how many digits they have, which is the quickest way to remember them:

- **one digit** -- the run failed and produced no usable result. `1` through `9` are Goro's, of
  which two are assigned above
- **two digits** -- the informational range. The run completed, and something is worth looking at.
  Only ever returned when `--strict-exit-code` is given. The tens digit names the concern: a code
  in the **`1`x** group says something about the data the run read, and one in the **`2`x** group
  says what the query found
- **three digits** -- not Goro's. Values from 126 upwards are spoken for by shells and by the
  operating system, most commonly for a command that could not be executed or a process that was
  killed by a signal

Further codes may be added within Goro's two ranges as further outcomes prove worth
distinguishing, so a caller that wants to treat a whole category alike should test the range
rather than enumerate the values it happens to know about.

### When more than one applies

A single run can qualify for several informational codes at once -- it may emit data warnings,
leave predicates unanswered, read only part of some files, emit file warnings, and match nothing,
all at once. Two rules decide which is returned:

- **A code in the `1`x group takes precedence over one in the `2`x group.**
- **Within a group, the higher-numbered code wins.**

So the order today is `13`, then `12`, then `11`, then `10`, then `20` or `21`. The `1`x codes are
numbered by how much they tell a caller to distrust the result: data that was disregarded, then
answers that the command had to decide for itself, then files of which only part could be read,
then files that were not processed at all. Codes `20` and `21` cannot both apply, since either
files were examined or none were found, and a file that could not be read does not count as
examined for the purposes of `20`: a run whose only discovered file was unreadable returns `13`
rather than `20`. A file whose predicate could not be answered, or only part of which could be
read, by contrast, was read and examined, and counts towards `20` like any other file that was not
listed.

### Suppressed warnings and the exit code

A warning suppressed with `--no-warn` was not produced, so it contributes nothing to the exit code
either. Suppressing a whole category removes the corresponding code from the outcomes a run can
return: under `--no-warn=data` a run never returns `10`, under `--no-warn=unanswered` it never
returns `11`, under `--no-warn=incomplete` it never returns `12`, and under `--no-warn=file` it
never returns `13`. The categories are independent: a
run whose data warnings are suppressed still returns `11` if that data left a predicate without an
answer.

### Relationship to warnings

The conditions that produce warnings are described under [Warnings](warnings.md).
`--strict-exit-code` reports only *whether* a run produced warnings of a given category, not how
many or about what.
