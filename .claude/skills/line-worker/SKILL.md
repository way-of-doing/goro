---
name: line-worker
description: How to carry out one delegated piece of a line of development (docs/directions/) as a worker -- what to read first, what stays out of scope, how to verify, and what to report back. Preloaded by the line-worker subagent.
---

# Working one piece of a line

You have been given one piece of a line of development. Another session -- the coordinator --
split the line into pieces, gave you this one, and will integrate what you produce alongside the
work of other workers running at the same time. Everything below follows from that.

## What the task message must give you

- the line, by the name of its brief in `docs/directions/`
- the piece: what to build or write, and which files it lives in
- the contract: the files or types shared with other pieces, which you use but must not change
- what done means for this piece

If any of these is missing or ambiguous, stop and report the gap instead of filling it in. The
task message is the approved plan for your piece; if the work turns out to need a different plan,
stop and say why rather than carrying on with your own.

## Before starting

Read the line's brief, `AGENTS.md`, the documents the brief and the task message point to, and
the contract files. The brief's Log says what has already been decided and why; do not reopen a
decision recorded there without saying so in your report.

## Scope

- Change only the files your piece owns. Other workers are editing other files at the same time,
  and the coordinator merges by file.
- Never change the contract. If the piece cannot be done without changing it, stop and report what
  would have to change and why.
- Do not edit the brief. The coordinator owns its Log, since several workers appending to it would
  conflict; give the coordinator what belongs there in your report instead.
- Do not commit, branch, merge or otherwise write to git. A hook refuses those commands; read-only
  git (`status`, `diff`, `log`, `show` and the like) is available. Your work stays as uncommitted
  changes in your worktree, which is where the coordinator collects it.
- If you create nontrivial experimental code during your work that is not intended to go into Goro
  main but rather as a short-lived experiment, do that by writing to 
  `scratchpad/Goro.Scratchpad/<name>/` inside your worktree, so it is available in the same 
  channel as the business code and all other changes.

## Verifying

Follow the project's conventions in `AGENTS.md`: new behaviour gets tests, and `dotnet build` and
`dotnet test` are run before the piece is called done. Report what the test run actually said. A
failing or skipped test is reported as such, with the output that matters.

## The report

End with a report in this shape, since it is all the coordinator will see of your work:

- **Worktree**: the output of `pwd`
- **Changed**: the output of `git status --short`, and a sentence on each file that is not
  self-explanatory
- **Tests**: the commands run and their result, failures quoted
- **Decisions**: each choice the task message left to you, and why you made it -- these go into
  the brief's Log
- **Open**: questions you could not settle, gaps in the task message, and anything you deliberately
  left undone
