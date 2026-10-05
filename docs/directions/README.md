# Directions

This directory holds the lines of development Goro has decided to pursue, or at least to
explore. Each line is one file, a _brief_, which carries everything a person or an agent needs
to pick the work up cold: what it is for, what finishing means, and a log of what has been
decided so far and why.

A direction is work on the project. It is not the same thing as an entry in
[deferred features](../design/deferred.md), which records product behaviour that was considered
and postponed. A brief belongs here only once it has been decided that the line is worth
spending time on; an idea that has not reached that point does not need a file.

## The menu

Every brief states its status in its front matter, and there is deliberately no index file to
keep in step with them, since one shared file would be edited by every concurrent branch. To see
what there is:

```sh
grep -H '^status:' --exclude=README.md docs/directions/*.md
```

The briefs whose status is `proposed` are the menu to pick from.

## Briefs

A brief is a Markdown file named for its line, such as `open-namespace-names.md`, with this
shape:

```markdown
---
status: proposed
size: broad
touches: concepts/predicates.md, features/builtins/identifiers.md
after: drop-dynamic-regex
branch:
---
# Identifiers in open namespaces

## Intent

What the line is for, in a few sentences. This is the short description the work starts from,
and it changes only if the line itself changes.

## Done when

What finishing means: a decision recorded in the rationale, the spec edits landed, a design
document accepted, code and tests passing.

## Log

- 2026-10-02 -- What was decided and why, what is still open, and what the next step is.
```

The front matter fields are:

| Field     | Holds |
|-----------|-------|
| `status`  | one of the statuses below
| `size`    | `focused`, `broad` or `sweeping`; a rough guide to how long the line is likely to run
| `touches` | the documents and code the line expects to change, relative to `docs/` where they are docs
| `after`   | the briefs, by file name without extension, that should land before this one starts; may be empty
| `branch`  | the branch the work lives on, once there is one

`touches` is what predicts collisions: two active lines touching the same normative document is
a reason to run them one after the other rather than side by side.

The Log is the part that grows. Entries are dated, newest last, and say why as well as what. An
entry that ends a session names the next step, so that whoever resumes the line does not have to
reconstruct it. Where a session transcript in `docs/sessions/` matters to the line, the Log links
it.

## Lifecycle

| Status      | Meaning |
|-------------|---------|
| `proposed`  | on the menu; nobody is working on it
| `active`    | has a branch, and work is under way
| `parked`    | work started and stopped; the branch and the Log say where it stopped
| `landed`    | merged; the brief stays as the record of how the decisions were reached
| `abandoned` | dropped; the last Log entry says why

A brief is added on the trunk, which is how something goes on the menu. Picking it up means a
small commit on the trunk setting it to `active` and naming its branch, then a branch named
`line/<brief name>` for the work. A git worktree per active line is worth having, so that a line
can sit untouched for months without stashing and separate sessions never share a checkout:

```sh
git worktree add ../goro-<brief name> line/<brief name>
```

The Log grows on the branch and reaches the trunk when the line lands. Parking a line costs a Log
entry and a commit. An abandoned brief is kept, since the reasoning behind dropping something is
exactly what would otherwise be lost.

## Keeping long lines alive

A line that runs for months survives only if its branch stays easy to rebase, and two habits keep
it so.

**Explore in new files, integrate in a short final burst.** A broad line does its thinking in its
own brief or in a design document of its own, and edits the normative documents only once the
direction is settled. A branch that has been rewriting `concepts/predicates.md` for three months
will not rebase; one that has been writing its own file will.

**Land decisions as soon as they are made.** A broad line is split into steps that can each land
on their own, listed in its Log, and every step that lands shrinks the surface left to conflict.

## Working on a line with an agent

A session on a line starts by naming it -- "resume open-namespace-names" -- and the agent reads
the brief, the commits on the line's branch since it left the trunk, and whatever the Log links.
Nothing else is needed for context, and nothing else should be relied on: an agent's memory does
not reliably follow a line from one checkout to another, and the brief does.

A session ends by appending to the Log, and by writing the proposed commit message as every
session does.

## Parallel phases

Most lines are design work, where each decision shapes the next, and are best run by one session
at a time. Some reach a phase where the work splits into pieces that never touch each other -- a
lexer and an evaluator, say, which meet only through the syntax tree between them. Such a phase
can be fanned out to `line-worker` subagents (`.claude/agents/line-worker.md`), each running in a
worktree of its own.

Three things make a phase safe to split:

- **The contract lands first.** Whatever the pieces share -- types, interfaces, a document they
  both build on -- is written, reviewed and committed on the line's branch before any worker
  starts, since a worker's worktree starts from the coordinator's last commit and never sees its
  uncommitted changes.
- **Each piece owns its files.** Workers are given disjoint sets of files, so that collecting
  their work is mechanical rather than a negotiation.
- **The task message is the plan.** It names the line, the piece, the contract and what done
  means, as the `line-worker` skill expects; a worker that finds the plan wanting stops and says
  so rather than improvising.

Workers do not commit. The coordinator collects each worker's changes from its worktree, runs the
whole test suite, records the workers' decisions and open questions in the brief's Log, and stops
for review before anything is committed, as every session does.

### Choosing each worker's model

Before choosing a model, the coordinator asks whether a piece is worth fanning out at all. Every
worker starts cold, reading `AGENTS.md`, the brief, the contract and the documents they point to,
and for a small piece that costs more than the piece itself; such a piece is better done by the
coordinator.

A worker runs on the coordinator's model unless the coordinator names another when it starts the
worker, and the choice is made piece by piece, by what the piece risks rather than by how large it
is. Sonnet is the default. A piece goes to Opus when any of these holds:

- **Its mistakes would be silent.** It implements behaviour where a plausible wrong version still
  gives well-formed answers and passes plausible tests: the areas [testing](../testing.md) gives
  classes of their own, such as quantifiers, absent against unusable, normalization, and what the
  tag library actually does.
- **Nothing independent checks it.** A refactor judged by an unchanged test suite has its check; a
  worker writing both the code and the tests that judge it does not, since a misunderstanding lands
  in both.
- **It leaves judgement to the worker.** The task message is the plan, and a worker is trusted to
  notice when the plan is wanting and stop. The more latitude a piece leaves, the more rests on that.
- **It writes normative prose.** Specification and rationale text stays with Opus, or with the
  coordinator.

Haiku is worth considering only for a purely mechanical piece that the build and the test suite
check completely.

The plan presented before a wave gives each piece's model with a line saying why, so the choice is
reviewed with the rest of the plan. A worker's model cannot change while it runs. Where a piece has
an independent check, a worker that stops with questions it should not have needed, or whose work
needs substantial rework, can be started again on Opus; where a piece has none, starting cheap is
not an option, since its failure would go unnoticed. The Log entry for each wave records the model
each piece ran on and how much rework it needed, so that this guidance is adjusted against
experience rather than expectation.
