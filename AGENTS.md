# Project overview

Goro is a .NET 10 console app intended to be used as a helper for managing large (100K files+) audio file collections. Read `/docs` (start with `docs/vision.md`) before making changes —
that's the source of truth for intended behavior.

This project is intended to produce a practically useful tool, but also to showcase how to implement modern, robust engineering practices and use well-regarded patterns and libraries. The code should serve as an example of well-architected software that can be used as a learning springboard by readers.

# Directory map

```
goro/
├── docs/                   # Important vision and design instructions   
│   └── commands/           # Unix man-style description of each command Goro understands
│   └── features/           # Explanatory text around Goro features 
│   └── sessions/           # Archive of past coding agent sessions; check recents for context on what was just decided
├── src/
│   └── Goro/               # Project code
├── tests/
│   └── Goro.Tests/         # Test project
```

## Build & test
- Build: `dotnet build`
- Test: `dotnet test`
- Run: `dotnet run --project src/Goro --` followed by relevant arguments

# Workflow
- Read the relevant docs under docs/ before proposing any change.
- Propose a plan — what changes in `src/` and `tests/` — before writing code. Wait for approval.
- Every new feature gets tests in tests/YourApp.Tests. Stubs or failing tests are fine for behavior that isn't fully specified yet.
- Run `dotnet test` yourself before saying something is done. Don't report tests as passing without having actually run them.
- Stop after implementing and let the change be reviewed before committing.

## Conventions
- Code formatting is not very important when generating code; that should be handled by a dedicated autoformatting tool
- Every new feature gets unit tests in tests/YourApp.Tests before it's considered done
- Don't add NuGet packages without asking first
- If a doc is ambiguous or conflicts with existing code, stop and ask rather than guessing
