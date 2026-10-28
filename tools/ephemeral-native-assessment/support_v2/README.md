# Issue #13 support protocol v2

Candidate support tooling; six native cells passed in run 40629471843 (2026-10-27).
Whole-target source/native approval and publication remain pending. The directly approved
2026-10-27 bounded recipe resolves the earlier NS-10 conflict without adding an
API. Live event delivery/resumption and outgoing publishing receive no support
credit. The
historical assessment-v1 consumer, scripts, classifications and records remain
unchanged. An Unsupported outcome cannot complete this protocol.

The workflow packs twelve exact distributions once and runs the selected public
three-package closure on windows-2025 and ubuntu-24.04, in self-contained JIT,
full-trim JIT and Native AOT. Each cell uses its own fresh cache with explicit
candidate package mapping. No JSON reflection property is set. Trimmed/AOT must
observe disabled reflection; AOT must observe unsupported dynamic code.

Three external PackageReference-only executables use that same cache/feed.
Empty local MSBuild boundary files stop ambient/repository imports; the hosted
work directory is physically outside the checkout:

* Journey: original Cases.cs and Support.cs are copied byte-for-byte. The only
  Program.cs adaptation installs the static role and the source-generated
  JourneyContext before the original sixteen assertions. Both generated and
  unconfigured-legacy JIT controls execute; the latter skips metadata installation.
* Metadata: the five reviewed NativeMetadata fixture files are copied. Program.cs
  gains a post-assertion runtime-feature marker, static role installation except
  for the missing-install negative, and two supplemental case dispatches. NativeExtras
  adds the registration/first-use race, public getter-barrier retention/key
  control, typed wait/value/physical-join, queued pool grant and throttle-capacity
  boundary assertions. The pool test uses a public MeterListener queue-depth
  barrier and an accepted timer-started holder; the follower starts directly so
  serialized provisional starts/timer dispatch cannot masquerade as pool queuing.
  The app clock is an exact renamed copy of the historical public clock.
  All original assertions remain. Configuration cases run in separate processes.
  Eight legacy-only controls run under JIT.
* Isolation: an OrcaCore-only consumer in each profile proves missing native
  installation without Core's AOT module initializer having already installed the
  boundary. Its second JIT process preserves the existing missing-engine
  diagnostic. Assets must contain no Core/engine package.

The report records original journey assertion counts. Metadata cases retain their
exact PASS marker and stdout/stderr, not an invented business-assertion count.
Golden and eleven-strong-value fingerprint receipts must agree with the legacy
JIT control across all six cells. Publish commands, actual materialized consumer
hashes, source/harness/package hashes, complete assets, cached product DLLs,
published closure, native imports and all diagnostic lines are retained.

No current diagnostic receives automatic approval. A previously unseen warning
fails the gate; historical issue #3 classifications grant no support credit.
Individual non-product counterexample decisions, if needed, require exact
identity/count/cause/proof and independent review.

Run negative controls with:

```powershell
python -B -X utf8 -m unittest discover -s tools/ephemeral-native-assessment/support_v2 -p test_support_gate.py
```

The scoped workflow is `.github/workflows/ephemeral-native-support.yml`.
Its temporary push trigger names only `codex/issue-13-native-evidence`. Creating
and pushing that validation-only commit still requires separate authorization;
the issue branch and author index remain uncommitted. Remove the temporary
trigger at the publication gate. No PR or issue edit is part of this run.

`--preflight` allows local development but deliberately fails native qualification.
The runner never recursively deletes command-line paths. Hosted cleanup verifies
the exact temporary path and ownership marker after preserving raw evidence;
local cleanup belongs to the disposable validation-worktree owner.

Final execution: https://github.com/dekrevedko/Workflow-orca/actions/runs/40629471843
Both native JIT cells pass 76/76; each full-trim/AOT cell passes 51/51. The
24 exact non-product counterexample warning records are independently reviewed
and count-pinned; no product-path warning or live-event capability is credited.
