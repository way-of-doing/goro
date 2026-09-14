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

## SEE ALSO

Individual command documentation may note exceptions to this behavior
where applicable.
