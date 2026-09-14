# Name

`goro list` -- discover audio files satisfying certain criteria and list them

# Synopsis

```
goro list [--] [<pathspec>...]
```

# Description

Discover files matching pathspec(s) and output information for each.

# Options

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
