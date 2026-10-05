# Implementation notes

This document holds concerns that belong to how Goro is built rather than to what
it does. It is distinct from its neighbours in a specific way:
[architecture](architecture.md) describes the components and what each one is
responsible for, and [deferred features](design/deferred.md) records behaviour that
was considered and postponed. What lands here is narrower than either: a
requirement or a question about resource management, data structure choice or
performance. The requirements are settled and the implementation must honour them.
The questions are deliberately unsettled, their right answers depending on how the
finished program actually behaves rather than on anything an armchair can supply.

Nothing in this document changes what a command does or what a predicate means. If
an entry here ever turns out to, it is in the wrong file.

## Requirements

### Streaming the JSON output

`-o json` produces a single JSON array, and that array must be written as results
arrive rather than assembled in memory and serialised at the end. A run may cover a
hundred thousand files, and holding every result to produce a document whose shape
was known before the run started would be waste with nothing to show for it. The
renderer emits the opening bracket, then each record with its separator as the
executor yields it, then the closing bracket.

One externally visible consequence follows, and is accepted: a run interrupted part
way through leaves output that is not a valid JSON document, the closing bracket
never having arrived. Wanting a parseable prefix while also being content with a
partial answer is a narrow combination, and the format that would serve it -- one
JSON object per line, valid at every prefix -- can be added as its own `-o` option
if a real need for it turns up. Streaming the array now forecloses nothing.

### Preparing a string once

Every string datum is prepared for comparison exactly once, by the operator that compares it; range
endpoints, which are known when the predicate is read, are prepared then and never again.
Preparation in normalized mode is not idempotent on unusual input: `क` followed by U+0345 and `ा`
keeps U+0345 the first time, since it follows a Devanagari letter, and case-folds it to Greek `ι`,
after which a second pass removes `ा` as an accent on a Greek letter. A prepared string handed to
preparation again is therefore a defect, however harmless it looks.

### Where the Unicode data comes from

Normalization draws on three sources, which need not agree on the newest characters. The
`Default_Ignorable_Code_Point` table is Goro's own, taken from UCD 16.0; general categories come
from .NET's tables, which are 16.0 in .NET 10; decomposition, composition and case mapping come
from the ICU of the machine Goro runs on. Two machines can therefore prepare a character assigned
after Unicode 16.0 differently. Moving to a newer .NET is the moment to bring Goro's own table up
to the version its tables follow.

### The size of a file reached through a symbolic link

`file::size` is the size of the file a link leads to. `FileInfo` describes a symbolic link itself,
reporting the length of the link, so the link is resolved to its final target before the length is
read; a link whose target is gone makes the file unreadable.

### Damaged tags and `file::duration`

TagLibSharp reads a file's tags while it locates the audio, so the facet behind `file::duration`
parses them too, and a strict tag parser would make a file unreadable for a predicate that never
asked about its tags. TagLibSharp 2.3.0 tolerates every shape of damaged Id3v2 tag tried, and a
test asserts that a file with such a tag still has a duration, so that a stricter release is noticed
rather than shipped.

### Symbolic links in a directory walk

A directory walk follows symbolic links to directories, and must not follow one whose target is the
directory being listed or one above it on the walk. A single such link makes an unguarded walk
descend until the operating system refuses the path, which ends a run over an ordinary collection
with a spurious file warning; two make the number of directories double at every level. Each
directory is therefore queued with its canonical path, with every symbolic link along it resolved
as `realpath(3)` would, and a link leading back onto its own walk is not followed. Links elsewhere
are followed as before, including to a directory the walk also reaches another way.

### The sources a file has reported

The evaluation context keeps, for each warning source the predicate can produce, the origin written
earliest among those reported for it in the file, in an array indexed by source. The array is the deduplication set as
well, an empty slot meaning that the source has not been reported yet, so no separate bitset is
kept, and it is allocated on the first report, so that a file which warns about nothing allocates
nothing.
