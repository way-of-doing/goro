---
status: proposed
size: focused
touches: concepts/pathspecs.md, commands/list.md, commands/hash.md, concepts/warnings.md, testing.md
after:
branch:
---
# Which files a run considers and how it treats them

## Intent

State which files a pathspec yields, and how Goro will treat each one. The documentation says a
directory yields every file in it, while the code admits only recognised extensions, and the
difference decides how many warnings a run over a real collection produces.

## Decisions

This is what we want to do, but of course our dependencies must still support it; this needs
to be confirmed, not assumed.

Pathspecs by default (this might be somehow configurable in the future) yield files with the
following extensions: mp3, ogg, flac, opus. Each file will be treated according to its
extension; we would much prefer to not go into specifying what our format sniffing strategy
is.

Globs matching files with other extensions will silently filter them out.

## Done when

The pathspec documentation says which files are candidates and why or why not, the code's
behavior matches, and the commands and tests agree with all of it.

## Log
