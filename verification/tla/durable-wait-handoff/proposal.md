# Proposal: verify recovery between durable wait matching and continuation

## Why

EV-032 and DR-034 link two commits across a possible host crash. After the match commit, an event can already be applied while a replacement worker still needs to drive the instance. A small model can check that every reachable committed state preserves enough information to recover and that two competing decisions produce one committed resume.

## Scope and acceptance

Begin with one retained inbox event and one eligible wait. Give two workers independent local reads and let one crash before a write or after a write whose reply it has yet to observe. Check wait eligibility, inbox state, pending work and committed completion independently. Require complete safety and conditional progress searches, a trace for each crash window, and controls that detect a missing handoff and a stale commit.

Compare the traces with the durable match handler, commit materializer, expected-version check and replacement-pump tests. Use the existing private TLC/JRE runner and retain distinct model and C# outcomes in the durable CI lane. This verification increment exercises existing product requirements. Its qualification boundary is the abstract commit protocol and the in-memory tests. SQL backend behavior and physical execution counts need their own evidence.

## Outcome

The six profiles and three linked runtime cases meet these criteria locally. See [the source crosswalk and results](README.md). The lease-expiry and timeout candidates are scoped separately in the [alignment review](../alignment-review.md).
