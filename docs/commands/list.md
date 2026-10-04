# Name

`goro list` -- discover audio files satisfying certain criteria and list them

# Synopsis

```
goro list [--filter=<predicate>] [--strict-exit-code] [--no-warn[=<category>,...]] [--] [<pathspec>...]
```

# Description

Discover files matching pathspec(s) and output information for each.

# Pathspecs

This command accepts one or more pathspecs. See [Pathspecs](../concepts/pathspecs.md) for which files are considered, and for resolution and deduplication rules.

# Filtering

By default, every file discovered from the pathspecs is listed. When `--filter` is given, a file is listed only if it additionally satisfies the predicate: the pathspecs decide which files are considered, and the predicate decides which of those are kept. See [Predicates](../concepts/predicates.md) for the expression syntax.

A file that the predicate needs to read and that cannot be read is not listed. One warning is emitted for that file and the run continues; see [Warnings](../concepts/warnings.md). This differs from `goro hash`, which reports such a file rather than leaving it out.

A file whose predicate evaluates to unusable, because the answer depended on tag data that could not be interpreted, is not listed either. The data warnings emitted while evaluating the predicate say what could not be read.

# Options

```
--filter=<predicate>
```

Restricts the listing to files satisfying `<predicate>`. The predicate is evaluated once for each discovered file, and only files for which it evaluates to true are listed; a file for which it evaluates to false or to unusable is not. See [Predicates](../concepts/predicates.md) for the expression syntax.

The predicate must be given as a single command line argument. Because predicate syntax uses double quotes to delimit string values, the argument normally has to be wrapped in single quotes so that the shell passes it through unchanged:

```
goro list --filter='artist == "metallica" AND year < 2000' /music
```

Errors in the predicate, such as a misspelled identifier or a comparison between mismatched types, are reported before any file is processed.

```
-o <format>, --output=<format>
```

Selects the output format. Format names are case-insensitive. The order in which files appear is **unspecified**, whichever format is chosen; discovery and processing are concurrent.

Valid options for `<format>` are:

- `plain` (the default if -o is not specified)

  This format produces output of the form

  ```
  /absolute/path/to/audio.mp3
  ```

  That is: one input file per output line; displays the absolute file path.

- `json`

  This format produces output of the form

  ```
  [
    {
      "file": "/absolute/path/to/audio.mp3"
    }
  ]
  ```

  That is: an array of objects, where each object has the following properties:
  - `file`: the full absolute file path name

# Exit code and warnings

This command accepts the global options `--strict-exit-code` and `--no-warn`. See [Exit codes](../concepts/exit-codes.md) for the codes it returns and what `--strict-exit-code` changes, and [Warnings](../concepts/warnings.md) for the warning categories and what `--no-warn` suppresses.
