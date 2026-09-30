# Warnings

## NAME

warning - a remark about the data a run met, which does not stop the run

## DESCRIPTION

### Overview

A warning reports something about the data Goro was asked to work with that deserves a person's
attention and that Goro cannot settle on their behalf: a tag field holding something that is not
what it should be, or a file that cannot be read at all. A warning is not an error. **It never
interrupts a run, and it never changes what the run produces for any other file.**

That is a deliberate constraint rather than a convenience. A command may be operating on a
hundred thousand files, and it is not acceptable for one malformed or damaged file among them to
abort work that was correct for all the others.

Every condition Goro can detect is therefore one of two things. An **error** is reported before
any file is processed and stops the run before it starts; everything that can be known from the
command line and the predicate alone is an error. A **warning** is reported as the run proceeds
and stops nothing; everything that depends on the contents of a particular file is a warning.
There is no third category, and nothing about one file is ever allowed to become an error.

### What warns

Every warning belongs to exactly one of two categories. They are named here because `--no-warn`
selects by them, and because the exit code reports them separately.

**Data that cannot be interpreted -- the `data` category.** A tag field holding something that cannot be read as the kind
of value it should hold yields an unusable occurrence, and using one warns. On a real collection
this is much the most common warning, and it is the reason the whole mechanism exists. The rules
for exactly when it fires -- which constructs consume an unusable occurrence and which pass it
along, how a warning is attributed, and how warnings are deduplicated within a file -- belong to
predicates and are described under [Warnings](predicates.md#warnings) there.

**A file that cannot be read -- the `file` category.** Where Goro must read a file to do what was asked and cannot -- the
file has gone since it was discovered, permission is refused, or its contents are damaged or not
in a form Goro understands -- that file yields **one warning** and the run continues with the next
file.

Which files this can happen to depends on what the command needs from each one. A predicate
mentioning only `file::size` needs nothing but the file's metadata; one mentioning a tag
identifier needs the tag read; `goro hash` needs the audio itself. A file that a run never had to
open cannot fail to be read.

A file that warned this way has not been processed, and what a command's output shows for it
follows from what that command was asked. `goro list` with a predicate was asked which files
satisfy a condition, so a file whose predicate could not be evaluated is not listed, a condition
that could not be evaluated not having been satisfied. `goro hash` was asked for a value per file,
so the file appears with its hash reported as absent rather than being dropped, since dropping it
would be indistinguishable from the file having been deleted. The two look inconsistent and are
not: each command answers the question it was given. Neither is silent about it either way.

Unlike a warning about unusable data, this one is deduplicated against nothing, there being no
sub-expression to attribute it to. It is one warning for one file, so a run that could read none
of a thousand files emits a thousand warnings -- each of which names a file somebody may want to
go and look at, which is exactly the information that makes them worth having.

### Where warnings go

Warnings are written to standard error and never to standard output, so that they cannot interfere
with the machine-readable output of commands such as `goro list -o json`. Each one names the file
being processed, together with whatever identifies the cause: for unusable data, the
sub-expression responsible as it was written in the predicate.

### Suppressing warnings

The global option `--no-warn` stops warnings being produced. It takes an optional list of
categories, and with none given it means all of them:

```
--no-warn                suppress every warning
--no-warn=data           suppress data warnings only
--no-warn=file           suppress file warnings only
--no-warn=data,file      both, which is the same as the bare form
--no-warn=all            an explicit spelling of the bare form
```

Category names are case-insensitive, as are the other option values Goro takes. A name it does not recognise is a command line error, rejected before any file is processed, on the same terms as any other mistake in the invocation.

**A suppressed warning was not produced.** It is written nowhere, it contributes nothing to the
exit code, and nothing reports that it was suppressed. Goro will never knowingly give a caller a
way to tell a condition that did not occur from one that was suppressed: there is no count of
suppressed warnings and no residual trace of them, so a run under `--no-warn=data` is
indistinguishable from the same run over a collection whose tags are all clean.

That is what the option means rather than an artefact of how it works. Suppressing a category
says the condition is not a problem, not that it should be reported more quietly, and a run cannot
both disregard a condition and report it.

**Be deliberate about `file`.** Suppressing data warnings on a library whose tags are known to be
imperfect is ordinary housekeeping; nobody needs to be told twice a week that a tag they have
decided not to fix is still unfixed. Suppressing file warnings is a different matter, because a
file that cannot be read is the condition Goro exists to find, and silencing it in a scheduled run
removes the one signal that would report a disc going bad. The two categories exist separately
precisely so that the noisy condition somebody has accepted need not bury the quiet one they have
not.

Suppression is by category and by nothing finer. There is no way to silence warnings for one
identifier, one tag format or one file, and this is not a first step towards one: selecting an
individual warning would require warnings to carry stable identities to name, which they do not,
whereas selecting a category requires only the two names above. Anything finer is
[deferred](../design/deferred.md) until something concrete asks for it.

### Warnings and the exit code

A warning does not by itself make a command fail. A run that warned is still a run that completed,
and reporting that fact through the exit code is opt-in. With `--strict-exit-code` a run that
emitted at least one data warning returns `10`, and one that emitted at least one file warning
returns `11`. See [Exit codes](exit-codes.md) for the codes, for which of them takes precedence,
and for what suppressing a category does to them.

This is the reason the distinction between the two kinds of warning is worth drawing at all.
Unusable data means the run answered every question it was asked and disregarded some data while
doing so. A file that could not be read means the run did not answer one of the questions, so its
output is incomplete -- a materially different thing to learn, and one a script may reasonably
want to branch on separately.
