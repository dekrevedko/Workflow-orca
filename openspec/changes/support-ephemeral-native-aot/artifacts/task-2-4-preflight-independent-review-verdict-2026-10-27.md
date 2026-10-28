# Task 2.4 preflight independent self-review

Verdict: HOLD task 2.4 qualification. F1 requires a contract decision. This is a
separate self-review, not external approval. No source/native approval, checkbox,
commit, push, PR, issue edit or task 2.5 advancement is authorized by this record.

## F1 (P1): public-only native event requirement has no public execution path

The R1 consumer protocol's NS-10 requires typed incoming/outgoing event execution
on every native cell. The selected public closure exposes no ephemeral event
ingress and no ephemeral Publish method. Direct source inspection, the public API
baseline, the DI registration and the developer guide agree: the ephemeral router
is internal; public ingress/publishing belongs to the excluded durable role.

Consequently a public-package-only consumer cannot prove live event delivery or
outgoing publication by the required recipe. A private bridge, extra friend,
durable package, unapproved public API, or silently omitting these assertions is
not a valid repair. Native support remains incomplete even if all available
journeys pass. The new gate explicitly marks NS-10 Incomplete and rejects it.

The adjacent proposed boundary amendment is suitable for an explicit user
decision: retain every original AOT-01 through AOT-16 assertion and all twelve NS
groups; qualify currently exported operations and explicitly exclude native
credit for nonexported event delivery/publishing. This narrows qualification and
requires approval before canonical text or acceptance records change. Keeping
the original event requirement instead needs a separately reviewed public API
design. Neither alternative may be assumed by the implementation author.

## Preparation examined

- Task 2.3's 140 frozen files remain byte-exact; product source, public baselines,
  canonical requirements, registry approvals and task checkboxes were not edited.
- The original assessment consumer cases/support, runner/classifications and
  historical receipts remain unchanged. The versioned harness uses public
  PackageReferences and fresh mapped candidate caches outside repository props.
- Local generated and legacy journeys both pass all sixteen original assertions.
  The current JIT preflight passes all 73 recorded cases, including forty metadata
  controls and the OrcaCore-only missing-engine process. Windows 11 earns no
  required native-host cell.
- Initial trim preflight exposed two harness expectation errors in legacy-only
  controls; these are not product failures or accepted native outcomes. The
  legacy-only failed-use case now stays in JIT, and the native race asserts the
  existing exact InvalidOperationException for unavailable Reflection metadata.
- ReSharper was run on a built external consumer graph with Release/net10.0 and
  Any CPU. Compact reports retain actual findings; final triage is separate.
- Negative gate controls reject missing/duplicate cells, unqualified/container
  hosts, wrong reflection/AOT settings, dirty native source, unbound DLLs,
  unsupported/skipped/failed cases, missing NS coverage, parity drift, suppressions,
  private roots and unreviewed diagnostics.

## Remaining execution boundary

No GitHub native matrix has been pushed or dispatched. The prepared workflow
names only codex/issue-13-native-evidence for temporary push execution. Task 2.4
explicitly requires separate hosted-validation push authorization. The issue
branch and real index remain uncommitted. Full native execution, supplemental
typed-wait/resource coverage, individual diagnostic decisions, source/package/
harness identity verification and review are still required before 2.4 can close.
