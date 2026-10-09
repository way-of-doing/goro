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

### Reading a file from its edges

Everything Goro learns from inside a file, apart from the audio a hash reads, comes from one
analysis of the file's edges, shared by every command: the tags and an index of their fields,
where the audio starts and ends, and its playing time. Tag values are read from the index only
when a predicate asks for them, and large payloads such as cover art only when `bytes()` does.

The analysis never walks the whole file, and this is enforced rather than hoped for. Its reads go
through one reader that serves the head and the tail from windows read once, and every other read
is charged to its purpose: sniffing a header, reading declared structure, searching. Each purpose
has a budget for the whole file, however many reads make it up, so that a file cannot be walked
from end to end by reads that are each small. The budgets belong to a policy, shared by every
file and immutable; each file's reader takes an allowance from it, which holds what is left and
refuses a read the budget no longer covers. The analysis of any file therefore takes from it at
most the two windows and the three budgets, which a test asserts, damaged files included. The
windows begin at 64 KiB each, and the budgets at 4 KiB for sniffing, 256 KiB for searching and
16 MiB for declared structure, all to become configurable. A search reads in steps of 16 KiB, since
the first frame nearly always follows the tags directly, so a healthy file costs its two windows
and nothing more.

A file's playing time is decided in every analysis, whatever was asked, since a file whose playing
time cannot be had counts as incomplete once it is opened. For an MP3 with a summary header that
costs nothing beyond the head window. For one without, the seven probes that confirm a constant
bitrate are reads in the middle of the file, at most 16 KiB each, charged to the search budget,
under any predicate that opens the file. Deciding it only when asked is recorded in
[deferred](design/deferred.md).

A refusal is never silent. A tag whose structure the budget cuts off is broken off there, as if it
were damaged, and a file whose analysis was refused anything is reported as incomplete. The
structure budget is large because of one case: an Id3v2 tag before v2.4 that is unsynchronised as
a whole, which has to be read whole and undone before its frames can be found. One larger than the
budget is treated as a tag that cannot be read. Padding longer than the budget allows to check for
data is taken as padding without being read.

A value, read as a payload, is held to a limit of its own instead, 4 MiB, and charged nothing. A
budget shared between values would make whether one can be read depend on which others a
predicate read first. The payload limit is also the cap on decompressing an Id3v2 frame: a
compressed frame that would inflate past it is treated as one that cannot be decompressed, and is
unusable.

### The reader never throws over what a file holds

Reading a file can fail over input and output, and over nothing else. Whatever a file contains,
however damaged, the analysis describes it rather than throwing: audio that cannot be found is a
finding, which each command interprets. A fuzz test in the suite, truncating and overwriting the
fixture corpus, holds the reader to this, since passing the corpus alone proved nothing in the
prototype.

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

## Choices the specifications leave open

A format's specification sometimes says less than reading real files needs, or says something
real files do not follow. Goro then has to choose for itself. Each choice is recorded here, with
what it costs, and each is something `goro audit` points out where it applies.

### APEv1 text is read as ISO-8859-1

APEv2 specifies its text as UTF-8. APEv1 specified plain ASCII, and the programs that wrote it
stored whatever their local code page held: a report on the Mp3tag forum shows APEv1 values in
the Windows "ANSI" code page, which Mp3tag shows as empty because it takes all APE text for UTF-8.
Goro decodes APEv1 text as ISO-8859-1, as it decodes Id3v1. Every value then reads as text, and
ASCII, the only text the specification allows, reads exactly. The cost is that a value written in
any other code page reads as the wrong text, usable and without a warning, as an Id3v1 value
written that way already does. `goro audit` flags APEv1 values holding bytes outside ASCII.

### An MP3 summary header is trusted up to an exact frame end

A Xing, Info or VBRI header says how many bytes of audio follow. When more bytes follow than it
says, they are either junk appended to the file or more audio, from a file joined on to this one,
and the format offers no way to tell. Goro trusts the header when a frame ends exactly where the
header says the audio ends, and no frame starts there: the bytes after it are then junk, never
hashed and reported by `goro audit`, and the header's frame count gives the playing time. A frame
starting there means more audio, so the header describes only part of the file and the playing
time is unusable. Either way can be wrong: junk whose first bytes happen to form a matching frame
header makes a file look joined, which only costs it its playing time, and nothing in the file
says whether appended audio was meant to be there.

### A constant-bitrate MP3 without a summary header is counted from the edges

Without a Xing, Info or VBRI header an MP3 says nothing about its length, and only a walk of every
frame header gives the count exactly. Many real files are like this, all of those seen constant
bitrate, written by Apple's encoders among others. Goro trusts a file to be constant bitrate when
three things hold: the first confirmed frame gives a bitrate; a frame of that bitrate is found at
each of seven evenly spaced points, by a bounded search; and a frame of that bitrate ends exactly
where the trailing tags start. It then counts the frames as the audio's length over the average
frame length for that bitrate. Tested against a walk on other projects' test data, that was exact
on 32 of the 33 files it accepted and one frame out on a deliberately corrupt one. A file that
fails any check, a variable-bitrate one in particular, has an unusable `file::duration`. The
logic, these checks and the summary header's own, lives in a single type, so that the one place
that decides an MP3's duration can be read and tested on its own.

### UTF-16 text without a byte order mark is read as little-endian

Id3v2's text encoding `$01` is UTF-16 that begins with a byte order mark: v2.3 says Unicode strings
"must begin with the Unicode BOM", and v2.4 defines `$01` as UTF-16 "with BOM", keeping a separate
`$02` for big-endian text without one. Real files nonetheless hold `$01` text with no mark, the
work of Windows software, where little-endian is native. RFC 2781, the UTF-16 definition v2.4
cites, says such text "SHOULD be interpreted as being big-endian", which turns these files'
text into nonsense. Goro reads it as little-endian, as mutagen, TagLibSharp, TagLibSharp2 and ATL
were all found to, and gets the text the writer meant in every such file seen. The cost is a
big-endian writer that also left the mark out, of which none has been seen. `goro audit` flags
text read this way.

### An MP3 frame is confirmed by the frames after it

Nothing marks where an MP3's audio starts but the frame sync, eleven set bits, which junk and tag
data can hold by chance. A frame header counts only when a header matching it, in version, layer
and sample rate, sits where the frame says it ends. A free-format frame states no bitrate, so its
length is only the distance to the next sync, which junk can fake as easily as the sync itself:
one is confirmed only by a third frame at the same distance again, allowing a byte for padding. In
other projects' test data, 90 bytes of junk after a tag passed for a free-format frame under the
weaker rule, and cost a 201-second file its playing time. A header with nothing after it to confirm
it is not a frame either, so sync near the end of a file that ends inside its tag is not taken for
audio, at the cost that a file of a single frame has none.

### Data after an Id3v2 tag's padding breaks the tag

A zero byte where a frame header should start is the start of the padding, which runs to the end
of the tag. When anything but zero follows it, a block of the tag was zeroed, as bit rot does, and
the frames did not really end there: the structure counts as broken at that point, and the file is
reported as incomplete. Id3v2 says padding is zeroes, so a tagger that left other bytes in it is
reported the same way, which is the cost.

