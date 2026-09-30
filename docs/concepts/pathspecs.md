# Pathspecs

## NAME

pathspec - file, directory, and glob arguments accepted by commands

## SYNOPSIS

*command* *pathspec* [*pathspec*...]

## DESCRIPTION

Commands that operate on files accept one or more **pathspecs** on the
command line. A pathspec is one of the following:

**file**
: A path to a single file. The file is processed directly.

**directory**
: A path to a directory. Every file contained within, at any depth, is
processed recursively.

**glob**
: A pattern containing wildcard characters. Only `?` (matches any single
character) and `*` (matches any sequence of characters) are supported, and
at most one `*` may appear in a single pathspec. This is a restricted
single-star glob, closer to classic shell filename wildcards than to full
shell globbing (no character classes, brace expansion, or recursive `**`).
Each path the glob resolves to is then processed according to the file or
directory rules above.

Multiple pathspecs may be given in a single invocation, and any
combination of files, directories, and globs may be mixed.

## RESOLUTION

Each pathspec is resolved independently into a set of matching files, and
the results are combined into a single set before any processing occurs.

Because the combined result is a set, **each file is processed at most
once**, regardless of how many pathspecs resolve to it. Passing the same
path twice (`foo . .`), passing overlapping directories, or passing a glob
that matches a file also named explicitly, all produce the same result as
passing it once.

This guarantee applies to distinct filenames. It does not extend across
symbolic or hard links: a symlink and its target, or two hard links to the
same underlying file, are different filenames and are treated as separate
entries even though they resolve to the same content.

## FAILURES

**A pathspec that cannot be resolved is an error.** Where a pathspec names a file or directory
that does not exist, or that cannot be accessed at all, the command is rejected before any file
is processed and nothing is done; see [Exit codes](exit-codes.md). This is deliberate. A pathspec
is written by hand, a mistyped one is far more likely than a deliberate reference to something
absent, and finding out at the start costs nothing while finding out halfway through a run over a
large collection costs the run.

A **glob** is treated differently, because a pattern matching nothing is an ordinary result rather
than a mistake. A glob that resolves to no paths contributes no files and is not an error; if no
pathspec contributes any file at all, the run has nothing to do and reports that through its exit
code instead.

**Failures met while walking are not errors.** A subdirectory that cannot be listed, or a file
that cannot be read once processing reaches it, produces one warning and the walk continues with
the next entry, exactly as for any other file that cannot be read; see
[Warnings](warnings.md). The line being drawn is the same one every other part of Goro draws:
what can be known before the run starts is an error, and what can only be discovered during it is
a warning.

## SEE ALSO

Individual command documentation may note exceptions to this behavior
where applicable.
