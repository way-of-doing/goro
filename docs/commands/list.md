# Name

`goro list` -- discover audio files satisfying certain criteria and list them

# Synopsis

```
goro list [--filter=<predicate>] [--strict-exit-code] [--] [<pathspec>...]
```

# Description

Discover files matching pathspec(s) and output information for each.

# Pathspecs

This command accepts one or more pathspecs. See [Pathspecs](../concepts/pathspecs.md) for resolution and deduplication rules.

# Filtering

By default, every file discovered from the pathspecs is listed. When `--filter` is given, a file is listed only if it additionally satisfies the predicate: the pathspecs decide which files are considered, and the predicate decides which of those are kept. See [Predicates](../concepts/predicates.md) for the expression syntax.

# Options

```
--filter=<predicate>
```

Restricts the listing to files satisfying `<predicate>`. The predicate is evaluated once for each discovered file, and only files for which it evaluates to true are listed. See [Predicates](../concepts/predicates.md) for the expression syntax.

The predicate must be given as a single command line argument. Because predicate syntax uses double quotes to delimit string values, the argument normally has to be wrapped in single quotes so that the shell passes it through unchanged:

```
goro list --filter='artist == "metallica" AND year < 2000' /music
```

Errors in the predicate, such as a misspelled identifier or a comparison between mismatched types, are reported before any file is processed.

```
-o <format>, --output=<format>
```

Selects the output format. Format names are case-insensitive. Valid options for `<format>` are:

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

# Exit code

This command accepts the global option `--strict-exit-code`. See [Exit codes](../concepts/exit-codes.md) for the codes it returns and what the option changes.
