Read everything under /docs. Compare it against the current state of /src and /tests.
Identify what's new or changed since the last sync (check docs/sessions/ for the last
session's summary if present).

Propose a plan for what skeleton, interfaces, and tests need to be added or updated to
reflect the docs — don't implement yet. Wait for my approval of the plan.

Once approved: implement the skeleton and tests (tests can be stubs/failing where full
behavior isn't specified yet), run `dotnet test`, fix anything that doesn't build, then
stop and let me review before committing.
