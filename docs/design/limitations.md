# Limitations

This file records lines Goro has decided not to cross: places where it could take on more
responsibility, and has chosen not to, because the size or complexity of doing so outweighs what
it would bring for now. They are not defects, which are mistakes, nor
[deferred features](deferred.md), which Goro means to have. Each entry says what is declined,
why, and what would make it worth reconsidering, so that the line is drawn knowingly and can be
moved the same way.

## Ogg files with more than one logical stream

**Declined: chained files,** several complete streams one after another in one file, and
**multiplexed files,** several streams interleaved. Decided on 2026-10-08.

An Ogg file can hold several logical streams. A chain is what an Internet radio capture produces,
a new link for every track, and what `cat a.ogg b.ogg` produces. Each link carries its own codec
headers, its own sample rate, its own granule count starting from zero, and its own comments.

- **Tags have no single answer.** In a radio capture each link is a different track. Whether the
  file's artist is the first link's, all of them, or none, every choice is wrong for someone.
- **Playing time is not at the edges.** The last page gives the last link's length only. The
  total needs every link's boundary, found by walking every page or by bisecting on the serial
  numbers, which goes against reading only the edges of a file.
- **Nothing else agrees either.** The [file-reading](file-reading.md) survey found TagLibSharp
  throwing on both chained fixtures, NVorbis reading one link, ffprobe claiming the last link's
  length, and ffmpeg decoding only the first link of a Vorbis-then-Opus chain.
- **A multiplexed file has no stream that is "the" audio.**

Both are detected from the edges: a multiplexed file opens with several beginning-of-stream
pages, and a chain ends on a page whose serial differs from the first page's. One case escapes: a
file concatenated with itself repeats its serial, which the format forbids, and looks like a
single stream whose length is the last link's. Only a walk of every page can see it, which is
`goro audit`'s business.

**Worth reconsidering** if users bring collections of radio captures, and with a decision about
whose tags such a file has.

## Playing time of a variable-bitrate MP3 without a valid summary header

**Declined: working out the playing time of an MP3 by walking its frames,** when it has no Xing,
Info or VBRI header, or one that fails its checks, and it cannot be shown to be constant bitrate
from its edges. Its `file::duration` is unusable instead. Decided on 2026-10-08, and narrowed the
same day to exclude constant-bitrate files, which are counted from the edges (see
[implementation](../implementation.md#choices-the-specifications-leave-open)).

MP3 frames carry no timestamps, so without a trustworthy header the only exact answer is to read
every frame header, which means reading the whole file. That is cheap in processor time, about a
millisecond for a ten-minute file once cached, but costly on slow storage across a large
collection, for a predicate that only asked how long a file is. Estimates from the file size are
not exact, and a variable bitrate file can be wrong by a factor of three.

**Worth reconsidering** if `goro audit` shows such files to be common, as an option that allows
the walk. In other projects' test data, 50 of 134 MP3s had no summary header, all of them constant
bitrate, which is why constant bitrate was taken out of this limitation; see
[file-reading](file-reading.md#real-world-test-data). No variable-bitrate MP3 without a header was
found.
