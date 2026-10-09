# Frequently asked questions

Practical answers to questions that come up when using Goro. The documents under `commands/` and
`concepts/` say what Goro does; this one is about getting things done with it.

## Output

### How do I get the output in a repeatable order?

Sort it afterwards. Goro processes files concurrently and makes no promise about the order of its
output, so two runs over the same files can list them differently.

```sh
goro list /music | sort
goro list -o json /music | jq 'sort_by(.file)'
```

This matters most for `goro hash`, whose output is most useful when compared with a later run's:
sort both before diffing them.

### Why does `goro list` leave out a file it could not read, when `goro hash` keeps it?

Each command answers the question it was asked. `goro list` with a filter is asked which files
satisfy a condition, and a condition that could not be evaluated has not been satisfied -- whether
the file could not be read at all, or its tags could be read but not interpreted.

`goro hash` is asked for a value for every file, and is mostly used to compare one run's output
with a later one's. A row missing from that output would look exactly like a file that had been
deleted, whereas a row whose hash is absent says what actually happened.

Neither command is silent about it: both emit a warning for the file.

## Predicates

### How do I select files by type, such as every MP3 in a folder tree?

Filter on the extension:

```
goro list --filter='file::extension == "mp3"' /music
```

A glob cannot do this, since it matches within one directory only. The comparison ignores case like
any other, so `MP3` and `mp3` are both found. `file::path` works the same way for anything else
about where a file lives: `file::path =~ r"/live/"` finds the files somewhere under a directory
called `live`. Paths are written with `/` on every platform.

### How do I select the files that are not tagged with some genre?

Say which files you mean, since there are two candidates. `NOT genre == "metal"` selects every
file with no metal genre, including those that record no genre at all, while
`ALL(genre) != "metal"` selects only the files that record at least one genre, none of which is
metal. `genre != "metal"` on its own is an error for exactly this reason. Written that way, it
would have asked whether _some_ genre is something other than metal, which is true of a file
tagged both "metal" and "rock"; if that really is the question, `ANY(genre) != "metal"` asks it.

### How can I make a filter faster?

Put the cheap conditions first. `AND` and `OR` evaluate their operands left to right, stop as soon
as the result is settled, and are never reordered. `file::path`, `file::name` and
`file::extension` need nothing read at all, `file::size` needs nothing but file system metadata, a
concept qualified by a tag source needs that tag read, and a concept written without a source,
such as `artist`, may consult several tag formats before it answers. So

```
file::size > 10mb AND artist == "metallica"
```

reads tags only for the files larger than 10 MB, while the same two conditions the other way round
read tags for every file.

### Some files were left out because their tags could not be read. How do I include them?

When a condition depends on data that cannot be interpreted -- a year field holding
`last tuesday`, say -- the condition has no answer, and `goro list` leaves the file out. You can
decide for each condition what an unanswered one should mean, by wrapping it in `FALLBACK()`:

- `FALLBACK(year < 2000, TRUE)` treats an unreadable year as satisfying the condition
- `FALLBACK(year < 2000, FALSE)` treats it as not satisfying it
- `NOT FALLBACK(year >= 2000, FALSE)` selects every file not known to have a year from 2000
  onwards, including those whose year is missing or unreadable

### How do I filter on a value without getting warnings about junk in it?

Deal with the junk before the comparison sees it:

- `FALLBACK(year, 0) < 2000` treats an unreadable year as zero, silently
- `ALL(year) IS USABLE AND year < 2000` skips any file with an unreadable year, silently

Wrapping the comparison itself, as in `FALLBACK(year < 2000, FALSE)`, decides what an unanswered
condition means but does not silence it: by then the comparison has already looked at the junk
and warned.

If you have accepted the junk across the whole collection, `--no-warn=data` silences all such
warnings at once.

### `artist` (or `year`, ...) picked a value I did not want. What now?

Do not fight it. A concept written without a source guesses well for most files and will
occasionally guess against what you want. Address the tag you mean instead, as in
`vorbis::artist`, or through a source function such as `vorbis::field("ARTIST")` if you want the
datum exactly as recorded.

If what you want is a different order of preference, write one: every concept written without a
source is a `PREFERRED()` of the same concept from each source, and
`PREFERRED(id3v2::artist, vorbis::artist)` is the same idea with Id3v2 trusted first. The same goes
for anything no concept covers, such as
`PREFERRED(vorbis::field("ALBUMARTIST"), ape::field("Album Artist"))`.

A concept written without a source also skips silently over a preferred format whose data cannot be read, when a
lesser one holds something usable, so no warning tells you about the junk it routed around. To
find it, ask the format directly: `ANY(vorbis::year) IS UNUSABLE` finds the files whose Vorbis
date fields need attention.

### Why does `file::duration` warn for some files?

Some files, mostly MP3s without the summary frame an encoder writes ahead of the audio, do not
record their playing time in a form Goro can verify, and their `file::duration` cannot be read. A
comparison with it then warns and has no answer, as for any other data that cannot be read. To
pass over those files silently, guard the comparison:

```
file::duration IS USABLE AND file::duration > 10m
```

The run's `incomplete` warning still counts them.

### How do I find Id3v1 tags with a junk genre?

`id3v1::genre IS UNUSABLE` finds the files whose genre byte is an index the genre table never
defined.

### How do I tell a genuine Blues file from a zero-filled Id3v1 tag?

Entry 0 of the genre table is Blues, so an Id3v1 tag filled with zero bytes claims to be Blues.
The rest of such a tag is blank as well, which a genuine one usually is not:

```
id3v1::genre == "blues" AND id3v1::artist IS ABSENT AND id3v1::title IS ABSENT
```

finds the zero-filled tags without catching the deliberate ones.

## Warnings

### Should I suppress warnings?

Suppressing data warnings on a library whose tags are known to be imperfect is ordinary
housekeeping: nobody needs to be told twice a week that a tag they have decided not to fix is
still unfixed. It costs you nothing you need, because when junk actually changes what a run did --
a file left out because its predicate could not be answered -- the `unanswered` warning says so
on its own, once per run. Suppressing file warnings is a different matter. A file that cannot be
read is the condition Goro exists to find, and silencing it in a scheduled run removes the one
signal that would report a disc going bad. The categories are separate so that the noisy condition
you have accepted need not bury the quiet ones you have not.

## Scripting

### How do I act on the exit code in a script?

Test ranges rather than individual codes. New codes may be added within each range, and a range
test keeps working when they are:

```sh
goro list --strict-exit-code --filter='year < 1970' /music
code=$?
if [ "$code" -eq 0 ]; then
    : # completed, with nothing further to report
elif [ "$code" -ge 20 ]; then
    : # completed; the query came up empty
elif [ "$code" -ge 10 ]; then
    : # completed, but something about the data read is worth looking at
else
    : # did not produce a usable result
fi
```

See [Exit codes](concepts/exit-codes.md) for what each code means.

### Why did my run return `10` or `11` when nothing matched?

Because something wrong with the data a run read is reported ahead of what the query found: it
may well be the reason the query found nothing. `10` means some data could not be interpreted;
if you have accepted the junk in your tags, `--no-warn=data` removes the data warnings, and with
them `10`, so that an empty result shows up as `20`. `11` means more than that: the junk left the
predicate without an answer for some files, and `goro list` left them out. Suppressing data
warnings does not hide that, since it is a different thing to be told; `--no-warn=unanswered`
does, if you really do not mind.

### Why are the codes from `10` to `13` in that order?

Because each says more about how far to trust the result. A data warning (`10`) means some data
was disregarded, which may have changed nothing. An unanswered predicate (`11`) means it did change
something: `goro list` decided for itself to leave some files out. An incomplete file (`12`) was
processed without part of what it holds, and that can change an answer with no other warning to
show for it. A file warning (`13`) means some file was not processed at all, so the output is
incomplete and anything concluded from it may be wrong for a reason it does not show. That is the
most serious thing to learn, and one a script may well want to branch on separately.

### Why does an interrupted run not return a code of Goro's own?

The process is killed rather than finishing, and the platform reports that in its own way; a code
invented by Goro would, on some platforms, hide the very information that tells you the process was
killed. What you see therefore depends on the platform, whose documentation says what to expect.
