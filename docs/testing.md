# Testing

This document records the classes of test that Goro considers important, why each one earns
dedicated attention, and the scenarios that must be covered for each. It is not a list of every
test in the suite: ordinary unit tests for ordinary code need no entry here. A class of test
belongs in this document when getting it wrong would produce **silently wrong results rather
than a visible failure**, or when correctness rests on behaviour Goro does not control.

## Predicate evaluation over tag data read through TagLibSharp

### Why this earns its own class of tests

Goro does not read tag structures itself; it reads them through TagLibSharp and then interprets
what it gets. That places a third-party library in the middle of every predicate that touches a
tag, and it means two kinds of breakage that no ordinary unit test would notice:

- **The library's behaviour differs by tag version in ways that are not obvious.** Reading an
  Id3v2.3 tag does not yield the same shape of data as reading an Id3v2.4 tag holding identical
  content. Goro's job is to hide that difference, so the tests must assert that it is hidden.
- **Some of what Goro relies on is emergent rather than contractual.** Recovering the text of a
  frame as it appears on disk depends on implementation details of the library rather than on a
  documented guarantee. A dependency upgrade can change it without any compilation error, and
  the symptom is a predicate quietly matching the wrong files.

A failure in this area does not crash and does not show up in output. It silently changes which
files a command operates on, which for a tool whose purpose is to decide what to touch is the
worst failure mode available.

### What the tests must establish

**Version independence.** For each identifier Goro defines, tag content that is semantically
identical must produce an identical value whether it was stored as Id3v2.2, v2.3 or v2.4. This
is the property that the documentation promises, so it is the property that must be tested
directly rather than inferred from the parts.

**Fidelity of the raw namespaces.** An identifier in a `::raw` namespace must yield the text as
recorded in the file. Where that is not achievable, the test must pin the exact discrepancy so
that it is a known and documented deviation rather than a surprise.

### Known deviations are recorded as skipped tests, not as prose

Goro obtains the recorded text of a frame by a route that is faithful in almost every case and
inexactly faithful in a small, enumerable set of cases. That set is not documented only in prose:
each case gets a real test that asserts the *correct* behaviour and is **skipped**, grouped under
a category the test runner can exclude wholesale so that a normal run neither fails nor reports
noise.

This is deliberate, and it is preferred over the alternative of reading library internals to
achieve exactness. A route through internals would have to be guarded by tests that prove the
internals still behave as assumed, and no realistic suite can exercise the code paths a large
collection in the wild will; a dependency upgrade could therefore ship a silently broken release.
Choosing the inexact but supported route means the failure mode is a known, bounded, documented
near-miss with an obvious workaround, rather than an unbounded unknown.

Skipped tests are the right home for these cases because they state the correct expectation
precisely, they are impossible to lose track of, and the day a bug report arrives the work begins
by removing a skip rather than by reconstructing what the problem was.

### Canary tests guard the defects we work around

Where Goro compensates for a defect in a dependency, the compensation is invisible from outside:
the documentation describes correct behaviour and says nothing about the defect. That leaves a
hazard, because if the dependency is fixed the compensation becomes a second, opposite defect.

Every such workaround therefore carries a **canary test that asserts the defect is still
present** in the dependency. A canary failing is not a regression; it is notice that the
dependency changed and that the compensating logic and its tests must be revisited. Canaries are
grouped so that this intent is unmistakable to whoever sees one fail.

### Scenarios to cover

Tests must construct tag bytes directly rather than writing tags through TagLibSharp. Writing
through the library and reading back conceals defects, because the writer and the reader share
assumptions that a real-world file does not: a value written through the library arrives back
looking correct even when the same bytes produced by another tagger would not. The test project
already builds Id3v2 tag bytes by hand for other purposes, and that is the approach to extend.

Every scenario below is to be exercised for **each of Id3v2.2, v2.3 and v2.4**, and for both the
interpreted and the raw namespace.

| Scenario | What it is there to catch |
|---|---|
| A value containing a forward slash, such as an artist named `AC/DC` | The library splits some v2.3 frames on `/`; a single value must not become two |
| A value containing a semicolon, such as `Rock; Metal` | Goro's own split applies to genres only, and every resulting value must be trimmed |
| A genre written as a bare number, `17` | Must expand to the named genre on every version |
| A genre written as a reference, `(17)` | The parenthesised form is legal in v2.2 and v2.3 and out of spec in v2.4, but occurs there after a version migration |
| A genre reference with a refinement, `(17)Post-Rock` | Must yield both the referenced name and the refinement, on every version |
| Several genre references, `(51)(39)` | Every reference must be expanded, not only the first |
| A genre reference with an escaped parenthesis, `(17)((weird)` | The doubled parenthesis must be unescaped, on every version |
| `(RX)` and a bare `RX`, and the same for `CR` | Indistinguishable on v2.3, distinguishable on v2.4; both must map to the same result |
| A track number written `3/12` | Must yield the track and not be mistaken for two values |
| A date in each accepted timestamp form, and one in no accepted form | The latter must produce a tainted null, not a wrong date |
| A field that is present but empty, and one that is present but only whitespace | Must yield an untainted null rather than a tainted one |
| The same frame repeated, with and without distinguishing descriptions | Must yield a multivalue of the expected cardinality |
| A single v2.4 text frame holding several null-separated values | The only spec-sanctioned source of multiple values in one frame |
| A frame the library has no class for, holding text | Must be readable through the raw namespace |
| A v2.2 frame whose identifier the dependency maps to the wrong v2.4 name | Canary: Goro corrects the mapping internally, so the test asserts the underlying defect is still there |
| A `TCON` frame on v2.3 whose genres are separated by a forward slash, and one holding `(RX)` | Skipped: the recorded text is reported with a semicolon in place of the slash, and without the parentheses. Affects the raw namespace only |

### Scenarios deliberately not covered

Genuinely binary frames, such as attached pictures, are out of scope: no identifier is defined
for them and there is no sensible string for them to resolve to. Should that change, the
scenario list changes with it.
