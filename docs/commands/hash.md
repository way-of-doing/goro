# Name

`goro hash` -- compute audio content hashes from audio files

# Synopsis

```
goro hash [-a <algo>] [-o <output>] [--] [<pathspec>...]
```

# Description

Read the audio-relevant part of each input file, calculate its hash, and output that information.

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

Selects the output format. Format names are case-insensitive. Valid options for `<format>` are:

- `plain` (the default if -o is not unspecified)

  This format produces output of the form

  ```
  /absolute/path/to/audio.mp3 md5 0123456789abcdef0123456789abcdef
  ```

  That is: one input file per output line; displays the absolute file path, then a space, then the hash function name, then a space, and the computed audio hash.

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
  - `hash`: the computed audio hash
