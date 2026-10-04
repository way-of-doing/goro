# Warnings

## NAME

warning - a remark about the data a run met, which does not stop the run

## DESCRIPTION

### Overview

A warning reports something about the data Goro was asked to work with that deserves a person's
attention and that Goro cannot settle on their behalf: a tag field holding something that is not
what it should be, or a file that cannot be read at all. A warning is not an error. **It never
interrupts a run, and it never changes what the run produces for any other file.**

Every condition Goro can detect is one of two things. An **error** is reported before
any file is processed and stops the run before it starts; everything that can be known from the
command line and the predicate alone is an error. A **warning** is reported as the run proceeds
and stops nothing; everything that depends on the contents of a particular file is a warning.
There is no third category, and nothing about one file is ever allowed to become an error.

### What warns

Every warning belongs to exactly one of two categories, which `--no-warn` and the exit code
refer to.

**Data that cannot be interpreted -- the `data` category.** A tag field holding something that cannot be read as the kind
of value it should hold yields an unusable occurrence, and using one warns. The rules for
exactly when it fires -- which constructs consume an unusable occurrence and which pass it along,
how a warning is attributed, and how warnings are deduplicated within a file -- belong to
predicates and are described under [Warnings](predicates.md#warnings) there.

Unusable data can leave a predicate without an answer for a file, where the answer depended on
the data that could not be read. What a command does with such a file is the command's to say,
and each command that accepts a predicate documents it; `goro list` does not list the file. The
warning is emitted where the data was used, whether or not the answer turned out to depend on it.

**A file that cannot be read -- the `file` category.** Where Goro must read a file to do what was asked and cannot -- the
file has gone since it was discovered, permission is refused, or its contents are damaged or not
in a form Goro understands -- that file yields **one warning** and the run continues with the next
file.

Which files this can happen to depends on what the command needs from each one. A predicate
mentioning only `file::path`, `file::name`, `file::extension` or `file::size` needs nothing but the
file's metadata; one mentioning `file::duration` needs the audio's properties read; one mentioning
a tag identifier needs the tags read; `goro hash` needs the audio itself. A file that a run never
had to open cannot fail to be read. A file whose tags cannot be parsed, even if only one of them
is damaged, is a file that cannot be read, and so is a file whose audio properties cannot be
parsed when `file::duration` needs them.

A file that warned this way has not been processed. What a command's output shows for it is
specified by each command: `goro list` with a predicate leaves it out, and `goro hash` includes it
with its hash reported as absent.

Unlike a data warning, this one is not deduplicated: it is one warning for one file, so a run that
could read none of a thousand files emits a thousand warnings.

### Where warnings go

Warnings are written to standard error and never to standard output. Each one names the file
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

Suppression is by category only. There is no way to silence warnings for one identifier, one tag
format or one file.

### Warnings and the exit code

A warning does not by itself make a command fail. A run that warned is still a run that completed,
and reporting that fact through the exit code is opt-in. With `--strict-exit-code` a run that
emitted at least one data warning returns `10`, and one that emitted at least one file warning
returns `11`. See [Exit codes](exit-codes.md) for the codes, for which of them takes precedence,
and for what suppressing a category does to them.
