# Quirks

This file records what Goro does on purpose against a format's specification, because files in
the world are written that way and reading them as their writers meant serves the people who own
them. It is the counterpart of [limitations](limitations.md), where Goro does less than it could,
and of the [implementation notes](../implementation.md)' choices, where a specification is silent
and Goro has to pick. Each entry says what the specification says, what Goro does instead, and
what that costs. `goro audit` reports each quirk where a file needs it.

## Several values in an Id3v2.3 or v2.2 text frame

**The specifications:** v2.3 gives a text frame one value, and says that if the text "is followed by
a termination ($00 (00)) all the following information should be ignored and not be displayed".
v2.2 is the same. Only v2.4 separates several values with a terminator.

**What Goro does:** reads a terminator inside the text of any revision as separating values, as
v2.4 does, so `TPE1` holding `A`, NUL, `B` in a v2.3 tag is two artists. Taggers write v2.3 frames
this way, and mutagen reads them so; other projects' test data has a v2.3 `TXXX` holding two
artists written exactly like that. In UTF-16 text a later value may lack its own byte order mark,
and then has the first value's, which is also what v2.4 means when it says all strings in a frame
"SHALL have the same byteorder".

**What it costs:** a writer that followed v2.3 to the letter and put something after a terminator
meaning it to be ignored has that read as a further value. No such file has been seen.

## Apple's text frames `GRP1`, `MVNM` and `MVIN`

**The specifications:** no revision of Id3v2 defines them, and Goro's identifier documentation
otherwise reads text only from frames whose identifier begins with `T` or `W`, and `COMM` and
`USLT`.

**What Goro does:** reads `GRP1` (grouping), `MVNM` (movement name) and `MVIN` (movement number) as
the text frames they are laid out as, so `field()` takes them. They are Apple's, introduced with
iTunes 12.5 when grouping moved out of `TIT1` (by Apple's documented use; not verified here), and
other tools now write them too: the `GRP1` frame in music-metadata's test data was written by
ffmpeg.

**What it costs:** nothing that has been seen; the list is closed, and grows only by decision.

## An Id3v2 frame with an illegal identifier

**The specifications:** a frame identifier is made of the capital letters `A` to `Z` and the digits
`0` to `9`. Both v2.3 and v2.4 give every frame a size so that software can skip frames it does not
know.

**What Goro does:** steps over a frame whose identifier breaks that rule, by its size, when the
size lands on another frame, the padding, or the end of the tag. The frames after it are read as
usual. The frame itself cannot be named by `field()` or `bytes()`, since Goro's identifier check
rejects its name. Real files carry such frames; one in other projects' test data opens with a
frame named `Date`, and without this its six other frames would be lost.

**What it costs:** a damaged identifier whose size happens to land on a frame boundary is taken for
an odd frame rather than a break in the structure, and its frame is not reported as damage, only by
audit.

## Id3v2.4 frame sizes written as plain integers

**The specification:** v2.4 frame sizes are syncsafe integers, seven bits to a byte.

**What Goro does:** when the frames do not line up read as syncsafe and do read as plain
integers, it reads the whole tag that way, as iTunes once wrote it, and as TagLib and mutagen
do.

**What it costs:** nothing that has been seen; the two readings disagree only on frames of 128
bytes or more, and only one of them lines up.

## Id3v2.4's text encodings in an Id3v2.3 or v2.2 tag

**The specifications:** v2.3 and v2.2 define two text encodings, `$00` ISO-8859-1 and `$01`
Unicode, which is UTF-16 with a byte order mark. v2.4 adds `$02`, UTF-16 big-endian without a mark,
and `$03`, UTF-8.

**What Goro does:** reads all four in a tag of any revision, so a v2.3 frame whose encoding byte is
`$03` is read as UTF-8. Only an encoding byte that no revision defines makes a frame unusable.
Taggers are reported to write UTF-8 into v2.3 tags, which are still the most widely read revision
(not verified here).

**What it costs:** nothing that has been seen. No revision gives `$02` or `$03` another meaning,
so the only text read differently is text that a strict reader would have refused.

## Genre names as they are spelled today

**The specifications:** Id3v2.3's Appendix A lists the 80 original Id3v1 genres, and Winamp's
extensions as far as entry 125; later Winamp releases added entries to 147. Several names are
misspelt or written in the abbreviations of the time, and entry 133 is an ethnic slur.

**What Goro does:** names every entry of the [genre table](../features/builtins/genres.md) as
TagLib names it today, so that a genre one file records by number compares equal to the same genre
another file records as text. The names that differ from the original lists by more than letter
case are:

| Index | Original      | Goro
|-------|---------------|-----------------
| 29    | `Jazz+Funk`   | `Jazz-Funk`
| 40    | `AlternRock`  | `Alternative Rock`
| 67    | `Psychadelic` | `Psychedelic`
| 81    | `Folk-Rock`   | `Folk Rock`
| 85    | `Bebob`       | `Bebop`
| 90    | `Avantgarde`  | `Avant-garde`
| 125   | `Dance Hall`  | `Dancehall`
| 129   | `Hardcore`    | `Hardcore Techno`
| 133   | the slur      | `Worldbeat`

Entries 123 (`A cappella`) and 132 (`BritPop`) differ only in case, which comparisons ignore. A
refinement after a reference that repeats the original name, as in `(67)Psychadelic`, is still
recognised as a repeat (see the identifier documentation's genre rules).

**What it costs:** a predicate written with an original spelling, `genre == "alternrock"`, no
longer matches a file that records the genre as number 40, though it still matches one that
records the text.
