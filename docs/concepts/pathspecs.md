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
processed recursively. Symbolic links to directories are followed, except a
link leading back to a directory the walk is already inside -- the one
being listed, or one above it -- which is passed over silently, since
following it would never end.

**glob**
: A pattern containing wildcard characters, matched against the entries of
one directory. Only `?` (matches any single character) and `*` (matches any
sequence of characters) are supported, and at most one `*` may appear in a
single pathspec. Wildcards may appear only in the last component of the
path; the directory before it is taken literally. This is a restricted
single-star glob, closer to classic shell filename wildcards than to full
shell globbing (no character classes, brace expansion, or recursive `**`).
Each entry the glob matches is then processed according to the file or
directory rules above, so a directory it matches is walked in full. Matching
follows the platform's convention for file names: it ignores case on Windows
and macOS, and respects it on Linux.

Multiple pathspecs may be given in a single invocation, and any
combination of files, directories, and globs may be mixed.

## CANDIDATES

Goro considers only the formats it can hash reliably, so that a file's hash
changes if and only if its audio does. Today that means **MP3 files**: files
whose extension is `mp3`, compared without regard to case. The extension is
the one
[`file::extension`](../features/builtins/identifiers.md#namespace-file)
reports, so a file named just `.mp3` has none. Every other file is passed
over silently, whichever kind of pathspec reached it, a file named directly
included. Passing over a file is neither an error nor a warning.

A file is judged by its name alone, and is treated as the format its
extension names. One that does not hold what its extension claims is a file
that cannot be read, should the command need to read it; see
[Warnings](warnings.md).

## RESOLUTION

Each pathspec is resolved independently into a set of candidate files, and
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
is processed and nothing is done; see [Exit codes](exit-codes.md). The same goes for a glob that
breaks either rule above, with more than one `*` or with a wildcard before its last component.

A **glob** that resolves to no paths, by contrast, contributes no files and is not an error.

**Failures met while walking are not errors.** A subdirectory that cannot be listed, or a file
that cannot be read once processing reaches it, produces one warning and the walk continues with
the next entry, exactly as for any other file that cannot be read; see
[Warnings](warnings.md).

## SEE ALSO

Individual command documentation may note exceptions to this behavior
where applicable.
