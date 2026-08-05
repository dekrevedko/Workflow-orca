# Section 7B task 7.24 remediation — independent rereview request

**Date:** 2026-08-05

**Requested verdict:** `APPROVE` or `REJECT`

**Review scope:** the completed task 7.24 guard retarget plus its review-driven test-scaffolding,
determinism, and durable deadline-winner remediation; no task 7.25 product-surface implementation
is authorized by this request

## 1. Gate under review

Task `7.24` is already checked after its guard retarget and the complete remediation chain received
an independent pre-refreeze approval. This request freezes the resulting exact tree so the
remediation evidence itself can be reviewed reproducibly before task `7.25` begins.

The review chain closed:

1. the missing expected-red `EventEnvelope` descriptor migration;
2. vacuous package-symbol and source-`Publish` anchors;
3. incomplete durable non-sequential `Publish` absence coverage;
4. misleading direct-source fixture naming and incomplete invocation provenance;
5. fixture-local namespace-shim false-green and removal risks;
6. nondeterministic Section 6 wall-clock spin polling;
7. the AC-108 accounting mismatch; and
8. the initially non-discriminating deadline-winner regression artifact.

The durable-driver correction belongs to the already approved Section 6 tasks `6.2`–`6.3`
deadline/attempt-fencing requirement. It is not a Section 7B product-interface addition. The task
7.24 guard target remains expected-red until tasks `7.25`–`7.32` implement the approved surface.

## 2. Provenance and frozen target

- HEAD: `ac46d99543daf85c0fa3234272997ba40f47f96b`
- HEAD tree: `28f4033c4773ea7761afa905c9836fd25866f1c0`
- exact LF-terminated manifest:
  `docs/review/developer-facing-interface-section-07b-task-7-24-remediation-rereview-dirty-manifest-2026-08-05.txt`

Final self-inclusive Decision 6 anchors:

- porcelain entries: `459`
- raw ordered LF SHA-256: `8531cde609dde455baa324a149f77e1cc0792c642addc2dd7068e82e23933414`
- NUL-expanded normalized records: `489`
- normalized LF SHA-256: `038258f1406d21bfb475a1397a9e63478e99b26fdbedba4853034bdc60fc49f0`
- capability directories: `16`
- capability-directory SHA-256:
  `5e8a9725979d702ff2f639fef587828958c92533139602ab13f1401d6df7ebd5`
- capability directories lacking `spec.md`: `0`

Use the exact byte pipelines in Decision 6 of
`openspec/changes/harmonize-downstream-capability-specs/design.md`. Reproduce all three anchors
before validation and again afterward. Reject any drift.

## 3. Review-critical artifact anchors

The manifest freezes the complete dirty tree. These are the files most directly involved in task
7.24 and this remediation chain:

| Artifact | Lines | SHA-256 |
|---|---:|---|
| reshape `tasks.md` | 236 | `157A8BD07005A1D2DCB18860AEC50F18DA8BFCE444042B62C160AE93D400FBA7` |
| `FacadeHostingContractGuards.cs` | 466 | `D4C347C249EF9F1EFCE36242222EB1F0BE996D654A2EFB8977C81E80F9F67043` |
| `NormativeContractGuards.cs` | 366 | `0E3098055D54D1F37BEF32F1DE20C92438FDD7D3D372DD533B0DF24C5C5458DE` |
| `PackageConsumerGuards.cs` | 182 | `CC7AA15B59E418809BBFA86016C6A93C425D7F1C76EA8F13F3169BA96ECE9A6E` |
| `run-package-fixtures.ps1` | 135 | `A01F749DF8488411293232BD1411C4E97FBD2CF1FCD583712AF4268C36A872E1` |
| `package-consumer-fixtures.json` | 10 | `CE79C0B0FADB24CC8E4D1F118F533DA52ABF3BA9934BB73D6AD7768A6CB965D7` |
| `section-07b-surface-scenarios.json` | 15 | `91E5DFE1517E65D00A604F010DA27528985C81261CA7B8415BA44DF862792C17` |
| `Section7BSourceSurface.cs` | 90 | `35EA633C92FAA8FA7E43F89CCE9670723C1C6C060B2DAD52789E325EF5DAAB24` |
| `Section7BSourceSurface.csproj` | 10 | `05546A7D238796C40104CA479B6530D58617CB5A9FDCAC469F3E79B339E6A833` |
| callback `MissingNamespaceShim.cs` | 3 | `ED85A62633F07DCD65B8D836BCA495909D9847FC6AAEC7708DB84E4924F7F9FC` |
| PostgreSQL `MissingNamespaceShim.cs` | 3 | `ED85A62633F07DCD65B8D836BCA495909D9847FC6AAEC7708DB84E4924F7F9FC` |
| `DeadlineRetryScenarioHost.cs` | 641 | `157310E8A80664EDB5A403402CCDDD86B5E6214FD37001B95A7D50DF2AF6B947` |
| `StructuredFanoutScenarioHost.cs` | 1127 | `088CF6EFDACCDCC29A2462E6540C9B2CBA1589FEF8345E33A5CF95038104FB8F` |
| `RepositoryGuardTests.cs` | 576 | `D8D04446CD8E4B7E91B53C7CCB8580C43F5CDBB166324DC8DA34B43672B22C8F` |
| `DurableFiberDriverExecutor.Helpers.cs` | 1046 | `9043DD474DEAE770290F4C37A738EB2C326E00A70E0AE2FEBB7F456D7242C201` |
| `DurablePolicyWinnerSelector.cs` | 26 | `BE6874B4D5809B323C5519C58DDF25CBCC2464E9C306629BDBADEC9803F379DD` |
| `DurableDeadlineWinnerRaceTests.cs` | 136 | `127451F07DA5F37435F2C0113EEBCAEC1FE05B7E3919D437FEC4E19751BBF5C2` |

Paths are rooted under their evident project directories in `src/` or `tests/`; reviewers should
use the manifest for the exact full paths.

## 4. Required semantic review

Independently verify:

1. All nine Section 7B expected-red guards fail on the named approved deltas rather than incidental
   compilation or unrelated existing symbols.
2. `EventEnvelope` is no longer pinned green to the superseded `EventName`/parameterless-payload
   shape and has one expected-red guard for the descriptor-checked target.
3. Package diagnostic matching strips source/project paths, keeps the necessary any-one-of behavior
   for compiler short-circuiting, and cannot pass merely on fixture directory names.
4. The callback and PostgreSQL fixture source allowlists are exact and recursive; each temporary
   namespace shim is type-free, contains only the approved namespace after comment removal, and is
   automatically forbidden once task `7.27` is checked.
5. Durable `Publish` presence uses a discriminating workflow-event-contract signature. Reflection
   and companion guards enforce two overloads on all eight durable sequential families and none on
   ephemeral, completion, or join families.
6. The Section 7B compile fixture is honestly labeled direct-source evidence; package consumers
   remain separately classified.
7. Section 6 timer scenarios wait through notification-driven output captured before virtual-time
   advancement. No wall-clock or bounded-`Task.Yield` polling remains.
8. AC-108 has one cataloged task-9.9 waiver and is neither silently omitted nor falsely marked as
   currently covered.
9. Deadline arbitration selects exactly one `Task.WhenAny` winner and does not re-read `Task`
   completion/status properties afterward. The behavioral test states only that post-selection
   abandon work cannot feed back into the winner.
10. Apply the faithful superseded mutation
    `if (!ReferenceEquals(winner, timeoutTask) || executionTask.IsCompleted)` before the callback.
    In both Debug and Release, the behavioral test must remain green while the structural guard
    fails specifically on `Task.get_IsCompleted`. Restore and require 2/2 plus selector SHA-256
    `BE6874B4…F379DD`.
11. The sole driver call site awaits `TimeoutWonAsync` directly and contains no additional
    execution/timeout task-state read. This is a source review item, not a request to widen the
    already approved target with another guard.

## 5. Required mechanical checks

Run and record the exact commands and outcomes:

1. `dotnet build OrcaCore.slnx --no-restore -v minimal` — expected 0 warnings, 0 errors.
2. Core tests — expected `342/342`.
3. Durable tests — expected `63/63`, including restored deadline-winner tests `2/2`.
4. Guards with `--filter "Disposition=Infrastructure"` — expected the paired anchor
   `171/174`, with failures exactly the three known `PublicApiBaselineInfrastructureGuards`.
   The pre-refreeze independent reviewer reproduced this exact membership three consecutive times.
5. `FacadeHostingExpectedRedGuards` — expected `9/9` red on the named Section 7B deltas.
6. `openspec.cmd validate --all --strict` — expected `18` passed, `0` failed.
7. `git diff --check` — expected exit 0; CRLF conversion advisories are not whitespace findings.
8. Task accounting — expected `158` total / `109` done / `49` pending, task `7.24` checked and
   task `7.25` open.
9. Repo-wide test-source scan — every surviving `Task.Delay` must name a `TimeProvider` or
   `Timeout.Infinite`; expected zero other matches.
10. Recompute the manifest, normalized, and capability-directory anchors after all validation and
    reject any mismatch.

Treat the stable baseline as the pair **guards `171/174` + Core `342/342`**, not as one aggregate
number. The three guard failures are existing task-7.22 public-baseline work, not expected-red
substitutes and not newly waived failures.

## 6. Verdict rule

`APPROVE` only if the frozen anchors reproduce, every review finding remains closed, the faithful
mutation makes the structural guard red for the stated reason in both configurations, the restored
tree returns to the exact selector hash and 2/2, and the paired validation anchor is stable.

Approval authorizes beginning task `7.25` in a later turn against this exact reviewed target. It
does not authorize a checkpoint commit, task `7.26` or later work, Section 7 exit, or task `8.0`.

`REJECT` for drift, a non-discriminating mutation, any task-state recheck after arbitration, a
vacuous guard/fixture anchor, nondeterministic Section 6 membership, a changed failure set, or any
other release-blocking defect. Write one new dated immutable verdict and edit no reviewed file.
