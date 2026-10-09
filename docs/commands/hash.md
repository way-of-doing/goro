# Name

`goro hash` -- compute audio content hashes from audio files

# Synopsis

```
goro hash [-a <algo>] [-o <output>] [--strict-exit-code] [--no-warn=<category>,...] [--] [<pathspec>...]
```

# Description

Read the audio-relevant part of each input file, calculate its hash, and output that information.

A file whose audio cannot be read has no hash. It still appears in the output, with its hash
reported as absent, and one warning is emitted for it; see [Warnings](../concepts/warnings.md).
This differs from `goro list`, which leaves out a file it cannot read.

# What is hashed

The hash covers a file's encoded audio stream and nothing the container around it adds, so that
no edit to a file's tags changes it. For an MP3 that is its audio frames, from the first to the
end of the last whole frame. Left out are the tags at either end, the frame some encoders write
ahead of the audio to describe it (Xing, Info or VBRI), and anything else before the first frame
or after the last. A file in which no audio frame can be found cannot be read, and has no hash.

# Pathspecs

This command accepts one or more pathspecs. See [Pathspecs](../concepts/pathspecs.md) for which files are considered, and for resolution and deduplication rules.

# Options

```
-a <algorithm>, --algo=<algorithm>, --algorithm=<algorithm>
```

Selects the hash function to be used. Hash function names are case-insensitive. Valid options for `<algorithm>` are:

- `md5`: use MD5 (the default if -a is not specified)
- `sha1`: use SHA-1

```
-o <format>, --output=<format>
```

Selects the output format. Format names are case-insensitive. The order in which files appear is **unspecified**, whichever format is chosen; discovery and processing are concurrent.

Valid options for `<format>` are:

- `plain` (the default if -o is not specified)

  This format produces output of the form

  ```
  /absolute/path/to/audio.mp3 md5 0123456789abcdef0123456789abcdef
  ```

  That is: one input file per output line; displays the absolute file path, then a space, then the hash function name, then a space, and the computed audio hash.

  A file whose audio could not be read carries `-` in place of the hash:

  ```
  /absolute/path/to/damaged.mp3 md5 -
  ```

- `json`

  This format produces output of the form

  ```
  [
    {
      "file": "/absolute/path/to/audio.mp3",
      "algo": "md5",
      "hash": "0123456789abcdef0123456789abcdef"
    }
  ]
  ```

  That is: an array of objects, where each object has the following properties:
  - `file`: the full absolute file path name
  - `algo`: the name of the function used to compute the hash
  - `hash`: the computed audio hash, or `null` where the audio could not be read

  A file whose audio could not be read therefore appears as:

  ```
  [
    {
      "file": "/absolute/path/to/damaged.mp3",
      "algo": "md5",
      "hash": null
    }
  ]
  ```

# Exit code and warnings

This command accepts the global options `--strict-exit-code` and `--no-warn`. See [Exit codes](../concepts/exit-codes.md) for the codes it returns and what `--strict-exit-code` changes, and [Warnings](../concepts/warnings.md) for the warning categories and what `--no-warn` suppresses.
