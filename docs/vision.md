# Vision

Goro is a command-line utility intended to assist the administration of large (100K files or more) private music collections.

Goro's headline feature is the ability to hash the _non-metadata_ range of audio files and display, save (as a tag), or compare these hashes with the actual audio contents of the file. In this way, "bit rot" issues that are unavoidable in the long term on very large data sets can be detected and corrected by replacing "spoiled" files with backed up original versions. The term "non-metadata range" refers to the contents of the file after excluding both fixed (e.g. ID3v1) and dynamic (e.g. ID3v2) tag sections and other fixed headers. To summarize: the audio range hash that Goro computes will change if, and only if, the decoded audio bitstream is going to change as well.

# Core features

- Calculation of audio-only hashes from each file
  - Recording this hash allows detection of corrupted files while remaining indifferent to tag edits
  - When faced with two bitwise different copies of the same file, easily allows to determine which version is correct and which is corrupt
- Filtering of input files based on path name and metadata, including tag contents
  - This allows Goro to be used as a powerful, scriptable "library query" tool

# Non-goals

- Tagging: while Goro will ultimately offer tag-writing features (the most obvious example being storing the audio hashes inside the tag data of each file), it is not intended to be a tagging super-multitool.

# Usage

Goro is invoked with a command line of the form

```
goro [COMMAND] [OPTIONS...]
```

Each command that Goro supports is described in more detail in a document under `docs/commands/`, for example `goro hash` is a command described in `docs/commands/hash.md`.

# Constraints & preferences

Target: .NET 10, console app, cross-platform
Dependencies: none without discussion first
Should work non-interactively (scriptable)
Should be able to handle commands that end up operating in tens of thousands of files with good performance
