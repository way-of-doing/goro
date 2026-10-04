---
status: landed
size: focused
touches: concepts/pathspecs.md, commands/list.md, commands/hash.md, concepts/warnings.md, testing.md
after:
branch: line/file-discovery-scope
---
# Which files a run considers and how it treats them

## Intent

State which files a pathspec yields, and how Goro will treat each one. The documentation says a
directory yields every file in it, while the code admits only recognised extensions, and the
difference decides how many warnings a run over a real collection produces.

## Decisions

Pathspecs by default (this might be somehow configurable in the future) yield files with the
extension mp3, compared without regard to case. Each file will be treated according to its
extension; we would much prefer to not go into specifying what our format sniffing strategy
is.

Goro considers only formats it can give the audio hash guarantee for. FLAC, Ogg Vorbis and Ogg
Opus were meant to be part of this line, but TagLibSharp cannot hash them to that guarantee (see
the Log), so each has a line of its own that answers the questions and then adds the format:
[flac-support](flac-support.md) and [ogg-support](ogg-support.md). Until they land, the
documentation keeps describing things, such as the `vorbis` namespace, that no file Goro
considers can use.

Globs matching files with other extensions will silently filter them out.

## Done when

The pathspec documentation says which files are candidates and why or why not, the code's
behavior matches, and the commands and tests agree with all of it.

## Log

- 2026-10-05 -- Started the line and checked the dependency, as the Decisions ask. Nothing
  normative has been edited yet, because the check turned up a question that is not this line's
  alone to answer.
  - TagLibSharp 2.3.0 opens all four extensions and picks the reader from the extension: `.mp3`
    goes to `Mpeg.AudioFile`, `.flac` to `Flac.File`, `.ogg` and `.opus` to `Ogg.File`, which
    then finds Vorbis or Opus from the stream itself. So Opus in `.ogg` and Vorbis in `.opus`
    both read correctly, and treatment by extension needs no sniffing of our own.
  - Reading is supported; hashing is not. `TagLibAudioHasher` hashes `[InvariantStartPosition,
    InvariantEndPosition)`, and a probe on synthetic files showed that range survives a tag edit
    only for MP3. For FLAC it starts at byte 0, so the metadata blocks, Vorbis comment and
    pictures included, are hashed: every tag edit changes the hash. For Ogg it holds while the
    comment fits in the pages it already has. Once it does not, as with embedded cover art,
    TagLib renumbers every later page, and each page's sequence number and CRC are hashed. Either
    one breaks the vision's promise that the hash changes if and only if the audio does.
  - The code's glob is recursive (`*.mp3` behaves like `.`, and a test pins that), while
    pathspecs.md and the FAQ say a glob matches within one directory and that a directory it
    matches is walked. A wildcard in a directory component quietly matches nothing, and the
    single-`*` limit is not enforced.
  - Open: what a literal file pathspec with another extension does (silently skipped, as the code
    does now, a warning, or an error, since it is known from the command line); and what `hash`
    does for FLAC and Ogg until it can hash them properly.

  Next: settle those with PJ, then edit pathspecs, list, hash, warnings and testing, and widen
  `FileDiscoveryService`.

- 2026-10-05 -- PJ narrowed the line to MP3, with the extension compared without regard to case,
  and moved FLAC and Ogg to lines of their own: [flac-support](flac-support.md) and
  [ogg-support](ogg-support.md). Each one answers the technical questions behind the audio hash
  guarantee for its format, then adds the format to Goro. The code already admits exactly `.mp3`
  regardless of case, so what is left here is mostly documentation and tests. The probe is kept
  as `scratchpad/Goro.Scratchpad/FormatSupport/HashStabilityProbe.cs`, and the scratchpad now
  references TagLibSharp.

  Checked against the CLI (macOS):
  - A glob matches file names at any depth below its directory part (`music/*.mp3` also lists
    `music/sub/c.mp3`), and never matches directories: `music/A*` did not walk `music/Abba`.
  - Glob matching follows the platform's case rule: on macOS `music/A*` matched `a.mp3`.
  - A wildcard in a directory component matches nothing, silently (`music/*/c.mp3`), and a
    pathspec with two `*`s is accepted.
  - A file named directly with another extension (`music/cover.jpg`) is skipped silently.
  - Two spellings of one file on a case-insensitive file system (`A.mp3`, `a.mp3`) are listed
    twice, each under the spelling given.
  - A subdirectory that cannot be listed ends the run with exit code 255 instead of warning, and
    so does a directory pathspec that cannot be listed. Pathspecs are resolved lazily: in
    `goro list music missing.mp3`, everything under `music` is listed before the run fails on
    `missing.mp3`, again with 255 rather than 2. Goro has no warnings yet, so the first of these
    cannot be fixed in this line.
  - None of the global options exist yet. Spectre accepts `--strict-exit-code` as an unknown
    option and takes the next argument as its value, so `goro list --strict-exit-code music/x`
    lists the current directory.

  PJ's decisions on the rest:
  - A file named directly with another extension is passed over silently, as every other route
    passes one over. That keeps a shell-expanded `goro hash *` quiet about `cover.jpg`.
  - Globs: the documentation wins. A glob matches the entries of one directory, and a directory
    it matches is walked in full. Case follows the platform (insensitive on Windows and macOS,
    sensitive on Linux). The code is to be changed to match.
  - Resolving every pathspec before any file is processed, and rejecting with code 2, is in
    scope. An unreadable subdirectory met while walking still aborts the run; that waits for
    warnings to exist.
  - FLAC and Ogg briefs were committed on main.

  Docs done: pathspecs (a CANDIDATES section; glob semantics and case), list and hash point to
  it, rationale (why only formats that can be hashed reliably, and why by extension), testing
  (rows for candidates, globs and early rejection). Candidates are defined by the extension as
  `file::extension` reports it, which the code gets wrong today: it lists a file named `.mp3`,
  which has no extension.

  PJ decided a glob with a wildcard in a directory component is rejected with code 2, like one
  with two `*`s; pathspecs and testing say so.

  Code done:
  - Discovery is now two steps. `Resolve` checks every pathspec and returns `ResolvedPathSpecs`,
    which only it can create. `DiscoverAsync` takes that type, so a rejection always comes before
    any output.
  - The commands map `PathSpecException` to exit code 2 (new `ExitCodes`).
  - Candidates are decided by `CandidateFiles`, whose `Extension` follows the `file::extension`
    rule. The flac and ogg lines add to its set, and `file::extension` should reuse it.
  - Globs match one directory's entries, walk the directories they match, and leave case to the
    platform.

  The CLI checks above now agree with pathspecs, with two exceptions left for later: an unreadable
  subdirectory still aborts a walk with 255, and two spellings of one file on a case-insensitive
  file system are still listed twice.

  Next: review, then land the line.

  Reviewed and landed the same day.
