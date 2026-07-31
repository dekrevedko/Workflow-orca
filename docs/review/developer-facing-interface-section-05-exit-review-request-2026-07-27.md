# Section 5 structured fan-out exit: independent review request

**Date:** 2026-07-27  
**Requested verdict:** approve or reject Section 5 exit after independently reproducing this
target and auditing the implementation against the frozen contract.

The controlling prerequisite is the immutable
[Section 4 detached-snapshot codec approval](developer-facing-interface-section-04-detached-snapshot-codec-closure-independent-rereview-2026-07-27.md).
That approval authorized task 5.0. Tasks 5.0 through 5.9 are now checked. Section 6 and task 6.0
remain blocked: this request does not authorize Section-6 source work.

## Review snapshot

- Baseline: `8c2dd712284f3b638f9bf812ad2172f24d0a8863`.
- `HEAD`: `d76192f089dd07f68e310c21fe4e5a38dd93cf7f`; tree
  `2264e670493ecc76359d42ee5273028eb287a566`.
- The baseline is an ancestor of `HEAD`; the stable commit is unchanged.
- Review target: `HEAD` plus every entry in the
  [Section 5 dirty manifest](developer-facing-interface-section-05-exit-review-dirty-manifest-2026-07-27.txt).
- Frozen self-inclusive manifest: 370 entries (301 modified, 9 deleted, 60 untracked);
  SHA-256 `F001016F92CF056AA1CF6E99203353112504500C1C5E582625AE3B607A41BBAD`.
  It includes this request and the manifest itself. It matched `git status --short` with zero
  differences when frozen.
- OpenSpec task state: 55 done, 57 pending, 112 total.
  Tasks 5.0 through 5.9 are checked; task 6.0 remains open and independently gated.
- All prior review files, requests, and manifests are immutable inputs. None was edited.

## Section 5 implementation to review

| Contract area | Implementation/evidence to verify |
|---|---|
| Closed ordered values | `BranchResult`, `ForEachItemResult`, `BranchOutcome`, `ForEachItemOutcome`, and detached recursive `WorkflowFailure` expose stable authored/index identity. Outcome families contain only runtime-created `Succeeded` and `Failed`; causes are copied into a read-only collection. |
| Fixed root parallel | Both modes expose root-only staged `Parallel<TResult>`, typed branch-local state, exactly one typed `Return`, and one required join returning the exact selected root builder. Empty fixed parallel fails graph validation with `SFE-AUTH-BRANCH-004`; branch state never aliases mutable parent state. |
| Join semantics | `WhenAll` waits for every child, does not cancel siblings after a failure, suppresses the success merge, preserves one failure, and produces ordered `SFE-JOIN-FAILED` causes for multiple failures. `WhenAllOutcomes` waits for every child, merges one ordered success/failure value per child exactly once, and resumes successfully. |
| Bounded dynamic items | `ForEachOptions.Create` validates positive `MaxItems` and optional `MaxConcurrency`. Both root builders select one finite list, reject count/codec problems before item admission, detach the snapshot through the fixed value codec, project item state from item plus zero-based index only, accept an empty list, and require one typed join. |
| Durable replay | The durable envelope persists the detached item descriptors, stable scope/fiber/index identity, item state/results, next admission position, and merge inputs. Replacement hosts reuse the committed snapshot, do not re-run the selector, retain terminal items, and re-admit unfinished indexes in order without persisting host-local path tokens. |
| Admission/path accounting | Effective item admission is the lower of node and host limits. Parked items release execution-path tokens but retain admitted-item slots. Fixed branches queue in authored order. A ceiling of one releases the parent before child admission and reacquires only for merge/continuation. Every checkpoint now reconciles dynamic admission and reapplies the host path ceiling before serialization. |
| Placement defense | Public nested, branch, item, and leased builders expose no `Parallel`, `ForEach`, or `While`; root fixed `Parallel`/`While`/`ForEach` remain available. Product compiler defenses and freshly packed forbidden-consumer fixtures reject hand-built or leaked nested fan-out. |
| Terminal fencing | Direct engine regressions cover cancellation and termination across both joins and both child kinds in both engines: active work is fenced, waits are cleared, merge is suppressed, and no cancellation outcome is fabricated. The workflow-deadline leg is intentionally Section 6.2. |
| Reference model | The shared generated reducer model covers internal schedules, duplicate delivery, failure/cancellation, replay, nested scopes, and dynamic items. Both actual engines additionally execute three generated completion orders for fixed branches and items; durable replaces the host before every completion and verifies no residual owned obligation. |
| Code-quality closure | The durable executor was split along scope/utility partial boundaries after the full suite detected a 1,000-line guard violation. The former monolith and helper are now 966 and 967 lines, with behavior preserved by the durable suite. |

## Guard disposition and mapped later-section boundaries

Seven Section-5 scenario drivers execute and pass in the infrastructure lane against the current
physical product assembly. They perform runtime-recorded exact calls and assertions for:

- empty-parallel diagnostic parity;
- success/failure-only outcomes;
- ordered multi-failure joins;
- empty `ForEach`;
- bound plus detached-selector replay;
- ceiling-one parent/child/merge token flow;
- lower-of host/node admission with parked-item slots.

The final consumer contract intentionally names assembly `OrcaCore`, while the staged builder types
currently have namespace `OrcaCore` but physical assembly `OrcaCore.Core`. Decision 17 assigns that
package/assembly relocation to Section 7. Therefore final-owner exact-call guards remain
ExpectedRed; the Section-5 infrastructure complement must stay green and must not silently weaken
the final owner contract.

Two frozen scenarios also span mapped later sections and remain ExpectedRed:

- `ancestor-terminal-suppresses-merge` includes `CompleteWithin`, owned by task 6.2. Cancellation
  and termination are green through direct both-engine regressions; deadline behavior is not
  claimed here.
- `restart-readmits-unfinished-items` includes final
  `WorkflowInstanceHandle.GetSnapshotAsync`, owned by Section 7. The durable engine restart,
  re-admission, committed-selector, and residual-obligation behavior is green directly.

The ExpectedRed lane is therefore deliberately unchanged at 104 individually named failures.
Reject any newly green final-owner/later-section result, setup/discovery failure, or coarse shortcut;
do not treat the known mapped boundaries as Section-5 product failures when their direct
Section-5 behavior complements reproduce.

## Reproduced evidence

| Lane | Result |
|---|---:|
| `dotnet build OrcaCore.slnx --no-restore -v minimal` | succeeded, 0 warnings, 0 errors |
| Core | 418 passed, 0 failed, 0 skipped |
| Ephemeral | 162 passed, 0 failed, 0 skipped |
| Durable | 301 passed, 0 failed, 0 skipped |
| Hosting | 15 passed, 0 failed, 0 skipped |
| Guard infrastructure | 66 passed, 0 failed, 0 skipped; repeated three consecutive times with the same result and no `CS2012` |
| Expected-red guard lane | 0 passed, 104 intentional individually named failures, 0 skipped |
| Section-5 current-physical behavior drivers | 7 passed, 0 failed |
| Green compile fixtures | fresh current-source package compiled; exact consumer compiled; 26 source and 26 package forbidden-member diagnostics verified; incomplete package rejected |
| Expected-red compile fixtures | 0 remaining; package authoring proof reports green |
| Strict OpenSpec validation | `reshape-developer-facing-interfaces` and `add-runtime-concurrency-limits` valid |
| Whitespace | `git diff --check` exited 0; only benign LF-to-CRLF notices appeared when stderr was shown |

The ExpectedRed test command returns nonzero by design. Review every named failure rather than
crediting the nonzero exit as evidence by itself.

## Reproduction commands

```powershell
dotnet build OrcaCore.slnx --no-restore -v minimal

.\tests\OrcaCore.Core.Tests\bin\Debug\net10.0\OrcaCore.Core.Tests.exe -noColor
.\tests\OrcaCore.Engine.Ephemeral.Tests\bin\Debug\net10.0\OrcaCore.Engine.Ephemeral.Tests.exe -noColor
.\tests\OrcaCore.Engine.Durable.Tests\bin\Debug\net10.0\OrcaCore.Engine.Durable.Tests.exe -noColor
.\tests\OrcaCore.Hosting.Tests\bin\Debug\net10.0\OrcaCore.Hosting.Tests.exe -noColor

.\tests\OrcaCore.DeveloperSurface.Guards\bin\Debug\net10.0\OrcaCore.DeveloperSurface.Guards.exe -trait "Disposition=Infrastructure" -noColor
.\tests\OrcaCore.DeveloperSurface.Guards\bin\Debug\net10.0\OrcaCore.DeveloperSurface.Guards.exe -trait "Disposition=ExpectedRed" -noColor
.\tests\OrcaCore.DeveloperSurface.Guards\bin\Debug\net10.0\OrcaCore.DeveloperSurface.Guards.exe -method "*Section5Scenario_CurrentPhysicalOwnerHasOneRuntimeRecordedExactDriverAndPassingAssertion*" -noColor

powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition Green
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition ExpectedRed

openspec.cmd validate reshape-developer-facing-interfaces --strict
openspec.cmd validate add-runtime-concurrency-limits --strict
git diff --check
```

## Independent review questions

1. Does the 370-entry frozen manifest reproduce exactly before validation and again after every
   command, with no concurrent actor or generated package artifact changing the target?
2. Are the public result/outcome/failure types immutable and detached, with exactly success and
   failure variants, no public construction path, no cancellation variant, stable branch/index
   identity, and recursively read-only ordered causes?
3. Do both public root builders enforce nonempty fixed `Parallel`, branch-local state, one typed
   `Return`, authored-order output, one join, exact-root-builder return, and
   `SFE-AUTH-BRANCH-004` parity between `TryBuild` and `Build`?
4. Does `WhenAll` genuinely wait for all children without sibling cancellation, suppress merge on
   failure, and aggregate multiple failures by authored ordinal/item index? Does
   `WhenAllOutcomes` merge every ordered success/failure exactly once and resume successfully?
5. Is `ForEach` selected once from a finite `IReadOnlyList`, count-checked and fixed-codec-detached
   before partial admission, with item state derived only from detached item plus index, valid
   empty merge, and immutable index-ordered results?
6. Does durable replay persist and restore every descriptor, item state/result, scope/fiber/index,
   admission offset, and merge input without re-enumerating or re-running the selector? After a
   host-ceiling change, are only unfinished indexes re-admitted in order?
7. Are host and node limits composed by their lower value, do parked items keep node slots while
   releasing path tokens, and can fixed `Parallel` and `ForEach` make progress at a host ceiling
   of one without deadlock or checkpoint drift?
8. Can cancellation or termination ever invoke either merge or fabricate a merge-visible
   cancellation outcome? Is the deadline portion honestly left to task 6.2 rather than hidden in
   a false-green driver?
9. Are nested/branch/item/leased `Parallel`, `ForEach`, and `While` absent from both the exported
   static surface and compiler acceptance, with all 26 forbidden calls rejected from a fresh
   package?
10. Do the generated reference comparisons and both real engines prove completion-order
    independence, single merge, canonical ordered state, replay stability, and exact residual
    cleanup rather than merely checking type names?
11. Are the seven current-physical scenario drivers strong executable complements while final
    assembly-owner checks remain unchanged for Section 7? Are the two cross-section scenarios
    still red only for their explicit Section-6/7 calls, not a missing Section-5 behavior?
12. Are all 104 ExpectedRed failures individually named, semantically later-owned, and unchanged;
    are all Section-6 deadline/retry/lease/governance behaviors still blocked?
13. Is Section 5 free of release blockers, so and only so may task 6.0 be authorized?

Record provenance, exact manifest/checksum, every command/result, findings, and verdict in one new
immutable dated review file. Only an approval with no release blocker may authorize task 6.0.
Do not edit reviewed source, tests, tasks, specs, documentation, manifests, requests, or existing
review artifacts.
