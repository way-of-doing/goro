---
status: proposed
size: focused
touches: concepts/warnings.md, concepts/exit-codes.md, commands/list.md, design/rationale.md, design/deferred.md, architecture.md, faq.md, testing.md, src/Goro/Warnings, src/Goro/Pipeline, src/Goro/Execution, src/Goro/Messages, src/Goro/Cli, tests/Goro.Tests
after:
branch:
---
# A warning for predicates that could not be answered

## Intent

Separate "the data held junk" from "the command had to decide an answer for itself", which today
share one warning category.

A data warning says that an unusable occurrence was used. Whether it changed anything is another
matter: a predicate that met junk may still have been answered, or it may have evaluated to
unusable, leaving the command to apply its own default, as `goro list` does by not listing the
file. Both reach the user as the same data warning, so suppressing data warnings as housekeeping
also hides every file whose answer was defaulted. That makes the rationale's definition of the
categories untrue -- a data warning supposedly means the run answered every question -- and
`--no-warn=data` less harmless than the FAQ says.

**A third category, `unanswered`.** The predicate stage of a command, which already decides what an
unusable answer means for that command, counts the files whose predicate evaluated to unusable,
and at the end of the run, if there were any, the command emits one warning saying how many files
that was, out of how many examined, and what it did with them. Each command words its own warning
whole: `goro list` did not list them, and a `goro hash` that one day accepts a filter would say it
hashed them anyway. The summary is a count and nothing more: an operator's unusable result has no
source, deliberately, and giving it one to break the count down would take that freedom away.

Some consequences, all intended:

- A predicate that settles its own default, as with `FALLBACK(P, TRUE)`, never evaluates to
  unusable, so the warning reports the command's defaults and never the author's.
- The existential default of the state tests still fails loudly where it matters, even with data
  warnings suppressed: the warning fires exactly when junk changed an answer.
- An unreadable file is not counted. It already has a warning of its own naming it, and the `file`
  category is the one that has to stay sharp.
- Which files were defaulted is already expressible: `(P) IS UNUSABLE` selects exactly them, so no
  option to list them one by one is added.

**Shape.** This is the first warning that names a run rather than a file or a directory. It is
written once every file has been processed, after every other warning, and a run that is
interrupted never writes it.

**Suppression and exit codes.** `unanswered` is suppressible like the other categories, as
`--no-warn=unanswered`, and suppressing it removes its exit code. The code belongs in the `1`x
group: the cause is the data read, and the reason that group outranks the `2`x group -- the data may
be why the query found nothing -- describes this case exactly. Within a group the higher code wins,
so the codes are renumbered to follow severity: `10` data, `11` unanswered, `12` unreadable file,
which remains the most serious, nothing having been evaluated for it at all. Nothing has been
released, so the renumbering costs only documents and tests.

Documents that change with it: the warnings document, which says every warning belongs to one of
two categories and names a file or directory; the exit codes; `goro list`'s account of an unusable
predicate; the rationale's question on why there are two categories, and the answers built on it;
the FAQ's answers on suppressing warnings, on a run returning `10` when nothing matched, and on why
`11` outranks `10`; the deferred entry on an exit code for a predicate that could not be answered,
which this reverses, its reason being exactly the coupling removed here; and the testing
document's exit code and suppression scenarios.

## Questions

- **Whether data warnings stay on by default in commands other than a future `goro audit`.** Once
  this lands, a data warning in `goro list` protects no answer; it only reports junk the run
  happened to touch, which a command built to find junk would do better, especially over a closed
  table of concepts (see [sources-and-fields](sources-and-fields.md)). That is a question for when
  such a command exists, and this line makes either answer safe.

## Done when

The warnings and exit codes documents describe three categories and the renumbered codes, `goro
list` says what it reports for a predicate that could not be answered, the rationale records why
"junk was seen" and "an answer was defaulted" are separate, the FAQ and deferred entries listed
above are brought in line, and the warning, its suppression and its exit code are implemented and
tested, including a run under `--no-warn=data` whose only file was defaulted returning `11`.

## Log

- 2026-10-05 -- Proposed in discussion. Decided there: the category and its name, one summary per
  run worded by each command, a count only, unreadable files not counted, no option to list the
  defaulted files since `(P) IS UNUSABLE` already does, and the codes renumbered to `10`, `11` and
  `12`. Next step: pick up the line.
