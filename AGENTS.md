# Project overview

Goro is a .NET 10 console app intended to be used as a helper for managing large (100K files+) audio file collections. Read `/docs` (start with `docs/vision.md`) before making changes -- that's the source of truth for intended behavior.

This project is intended to produce a practically useful tool, but also to showcase how to implement modern, robust engineering practices and use well-regarded patterns and libraries. The code should serve as an example of well-architected software that can be used as a learning springboard by readers.

# Directory map

```
goro/
├── docs/                   # Important vision and design instructions   
│   └── commands/           # Unix man-style description of each command Goro understands
│   └── concepts/           # Docs that describe cross-cutting concerns etc; linked to by other docs
│   └── design/             # Design rationale, plus deferred features and open questions
│   └── directions/         # One brief per line of development: what it is for, its status, and a log of decisions
│       └── archive/        # Archived briefs for completed or abandoned lines of development
│   └── features/           # Explanatory text around Goro features 
│   └── sessions/           # Archive of past coding agent sessions; for a line of development, follow the links in its brief rather than going by recency
│   └── faq.md              # User-facing practical answers and tips that the specs deliberately leave out
│   └── implementation.md   # Build-level concerns and open questions that need running code to settle
│   └── testing.md          # Classes of test that matter, why, and the scenarios for each
├── scratchpad/
│   └── Goro.Scratchpad/    # Warehouse of experiments worth keeping (benchmarks, candidate implementations); not production code
├── src/
│   └── Goro/               # Project code
├── tests/
│   └── Goro.Tests/         # Test project
```

## Build & test

- Build: `dotnet build`
- Test: `dotnet test`
- Run: `dotnet run --project src/Goro --` followed by relevant arguments
- Scratchpad: `dotnet run -c Release --project scratchpad/Goro.Scratchpad` runs whatever its `Program.cs` currently wires up

The scratchpad project is deliberately not part of `Goro.slnx`, so the build and test commands above never touch it. It is a place to keep code that is useful to refer back to -- benchmark harnesses, candidate implementations compared while making a design decision -- and is not held to the project's architecture, style or testing conventions. Each experiment lives in its own folder; `Program.cs` is edited freely to run whichever one is wanted. Code there is never production code as it stands: anything that graduates to `src/` is rewritten there under the normal rules.

# Workflow

- Read the relevant docs under docs/ before proposing any change.
- Propose a plan — what changes in `src/` and `tests/` — before writing code. Wait for approval.
- Every new feature gets tests in tests/YourApp.Tests. Stubs or failing tests are fine for behavior that isn't fully specified yet.
- If the work touches code, run `dotnet test` yourself before saying something is done. Don't report tests as passing without having actually run them.
- Stop after implementing and let the change be reviewed before committing.

## Lines of development

Work is organised into lines of development, each described by a brief in `docs/directions/`; its README explains the format and lifecycle.

- When a session is about a line ("resume open-namespace-names"), read its brief, the commits on its branch since it left the trunk, and whatever its Log links, before anything else.
- Work on a line belongs on its branch, `line/<brief name>`. A broad line keeps its exploration in its own files and edits normative docs only once a direction is settled.
- Before stopping, append a dated entry to the brief's Log: what was decided and why, what is still open, and the next step.
- Lines of development merge into main always with a merge commit; if the line of development is a single commit itself, use that message unchanged. After successfully merging a line branch into main, delete it.

## Conventions

- Code formatting is not very important when generating code; that should be handled by a dedicated autoformatting tool
- Every new feature gets unit tests in tests/YourApp.Tests before it's considered done
- Don't add NuGet packages without asking first
- If a doc is ambiguous or conflicts with existing code, stop and ask rather than guessing
- Git branches starting with `pj/` should always be completely ignored unless actively directed by the user to do differently 

## Before stopping

Write a proposed commit message, as you would have done if instructed to go ahead and commit the session's changes so far, to `.claude/next-commit-message.txt`.
