# Issue #16 task 2.1 transition — independent author review

Date: 2026-11-01, America/Los_Angeles.

Verdict: **APPROVE. No remaining P1 or P2 findings.**
This is a separate adversarial pass in the current chat, not a claim of a second
human or external reviewer. Owner approval of this exact transition is still
required. The target remains uncommitted and authorizes no product implementation.

## Exact target

- Base/HEAD: `7697c7981c02d03408cbccf9a2e0b9bade33ca14`.
- Branch: `codex/issue-16-ephemeral-failure-settlement`.
- Manifest: `task-2-1-manifest-2026-11-01.txt`, **54 paths**, self-inclusive.
- Candidate tree: `ae97c26e5c088c3c5430d9fd6644379984d30922`.
- Content SHA-256: `c96ecaa3faf205452bc638b71e8030aa3a48c21e1857500ca1ba346ebf728fee`.
- Porcelain SHA-256, excluding this verdict:
  `c11f72a11eb60c5890445199e7966f3a5fecd2232d77b2a02ecf3036862f05d7`.

The real index is empty. This verdict is outside the candidate for a future
verdict-only child. No frozen file changed during the final review.

## Approved contract chain

`0cae922a1d68f4c55f9bfc19b50060bfcdbaaa83` has sole parent
`0a8e375fbcf5005a2a2f491cf24dfbbd9d64c580` and exact approved tree
`cf054886e39c20d9294b3baac55e58fa19c6bbdf`. It contains exactly the 26 contract paths.
Its direct child `7697c7981c02d03408cbccf9a2e0b9bade33ca14` adds only the external
verdict, SHA-256 `3e7fc94ef23e6c0130e23aecc349a71248b5da3f6c5992d178d2da78a6f12fd8`.
The new guard verifies parent/tree/child/single-file scope and verdict bytes through
Git, failing closed on lookup failure. The original proposed-stage artifact is
also compared with its committed copy. Both earlier Codex reports are retained
unchanged as transparency evidence in this transition.

## Findings resolved

- **External F1:** the structured-fiber delta now permits detached type capture
  only for ephemeral failure production. Durable engine-produced fiber/checkpoint/
  outbox/projection/provider failure fields stay null-type and persisted failure
  bytes must match base `0a8e375`. The quality requirement and numbered acceptance
  require an actual stored-byte witness through InMemory and shared provider
  certification, including PostgreSQL/SQL Server. Projecting away a new field is
  explicitly insufficient. The existing shared-codec allowance for typed
  application payload data is preserved; it does not change durable error production.
- **Declaration P3:** the normative companion changes with this canonical target;
  executable assembly API, compile and package baselines wait for source. This is
  explicit in the delta, plan, matrix, companion and numbered owner. The companion
  contains only self-contained member fragments for the two existing types, with
  internal throwing construction and no new public constructor or setter.
- **Other external P3s:** ExceptionType is diagnostic only and never a matching
  key; Code remains identity. The guide recommends ExceptionDispatchInfo/wrapping
  for shared originals. Every future native cell must prove type-name parity with
  JIT rather than assume trimming/AOT preserves it.
- The reverse sweep found and reconciled the existing blanket retired-operation
  rejection and post-Completed allowlists in numbered requirements, the matrix,
  acceptance and guide. Only the passive issued receipt is excepted; ordinary
  output/query/command/ingress restrictions remain.

## Contract consistency

The proposed public addition remains exactly the nullable WorkflowFailure getter
and the nonvirtual base ValueTask settlement method. Resultful handles inherit
the method. The existing wrapper uses inherited InnerException; no new public
exception, option, helper, package, provider port or friend is introduced.

Both output-wait forms retain the exact elected business-step object, separately
from authoritative detached data. The final actual attempt, rather than equal
type/message/path values, identifies it. Returned errors, engine-owned versus
user-thrown invocation wrappers, single branch/item propagation, true detached
aggregates, recovery and late timeout/cancel losers are covered. The live graph
never enters snapshots, codecs or Core/provider records; retirement drops engine
references and already delivered originals remain caller-owned.

The legacy/v2 failure forms remain explicit, recursive and closed. Unchanged
all-null-type graphs keep their exact old bytes; typed descendants use the specified
seven-field version-2 form. Type strings cannot resolve CLR types. Old readers
reject typed nodes, so no old-binary forward compatibility or unqualified support
claim is implied by retaining the codec ID.

The settlement receipt still requires accepted terminality, final physical work
and handoffs, and a seal against new owned admission. A reusable zero-work epoch
or logical output result is insufficient. Observers hold no payload pin or host
obligation, cancel locally and do not poll. Original receipts survive retirement,
key reuse, Completed and correct owner disposal without lookup or dependency use.
Durable noncancelled calls reject without I/O; pre-cancelled tokens take precedence.
No-handle aborts, unjoined processes, self-referential waits and shared/root DI
ownership have explicit limits. No existing output timeout or host drain meaning
is changed.

## Provenance, scope and historical guards

An independent reconstruction verifies all **14** canonical files equal the prior
files transformed only by the **12** delta blocks: nine modifications and three
additions across five capabilities. Every current block equals its delta, and no
unowned requirement, order or preamble changed. The numbered additions use unique
AC-619–638 after the prior maximum 618 and map all twenty FS rows.

The registry is `ContractApprovedCanonicalPreparedSourcePending`, with canonical
review pending, source authorization false and both approval paths null. All
sixteen current documentation before/after hashes, the complete canonical-file
inventory, nine predecessor pairs, their historical Git blocks and all current
successors are checked. A third owner or premature task activation is rejected.
The historical adapter exposes the prior canonical layer only after verifying
the current whole-file transform. Earlier approval tests and global approval
records are preserved rather than waived or rewritten.

There are **218 rows / 58,043 bytes**, SHA-256
`f6e5ed0ad28a597fbbea17a62a10c9cfd6e05f580e0051f62f51d558d7a24783`:
198 synchronized, 17 superseded, 3 declared outside canonical and zero pending.
All 218 requirement identities and all **28** predecessor artifact bytes remain.
Structural semantic-approval eligibility is true because synchronization has no
pending text; that is not canonical/source review approval.

Product `src/`, dependencies, friends, runtime API/compile/package fixtures and the
prior global post-gate registry are unchanged. The two process tests explicitly
verify the pending members remain absent from compiled OrcaCore. The only current
test-inventory maintenance is one new process-test file/two declarations and its
derived count/provenance note: 369 files / 1,514 declarations. All 866 retired
dispositions and their historical/relocation records remain exact, with no new
runtime acceptance credit.

## Occurrence review and checks

- The declared full scope has **298 base hits / 492 current hits** and **502**
  individually source-linked rows, with **107 explicit owner decisions** and zero
  unclassified. Independent ripgrep replay agrees. Retain rows have exact matching
  base/current lines; ten edited old hits have no fabricated current mapping.
  Missing-row, false-Retain and altered-hash controls are all rejected.
- Strict OpenSpec: **26/26**. Release warnings-as-errors: **0 warnings / 0 errors**.
  Full Infrastructure: **311/311**, no skips. Final packet-copy controls: **4/4**
  after all 54 frozen paths were copied byte-for-byte into the disposable checkout.
- Six semantic mutations turn the relevant guard red: unowned canonical preamble
  despite re-pinning the outer registry, forged source approval with re-pinning,
  a third owner, early task 3.1, altered verdict bytes, and a removed documentation
  pin with re-pinning. Exact inputs were restored; controls pass **2/2**.
- ReSharper 2026.2.3.1, Release / Any CPU / net10.0 on all six changed process
  source files: **0 errors, 58 warnings, 62 notes; no introduced warning**. Existing
  findings newly in scope and the one demonstrated unused-member false positive
  are documented in the triage artifact. No suppression was added.
- Runtime/provider/integration/stress/coverage and new consumer/native execution
  were not run. Product implementation is absent; those remain task 3 obligations.
  Current green validation is not evidence the proposed API already exists.

Initial preparation failures and the probe-report wording correction are retained
and classified in the validation record. They were resolved before this freeze;
no product failure is hidden or counted as a green lane.

## Boundary and cleanup

The contract and verdict commits are complete. This 54-path transition remains
uncommitted, with the real index empty. Task 1.4 is ticked; 2.1/2.2 and all source
tasks stay open pending transition approval/checkpoint. No product work, push,
issue change, merge, archive or release was started.

Both owned validation worktrees, `E:/oc16t1` and `E:/oc16t2`, and their build/probe/
ReSharper caches were removed after preserving compact evidence. The prior
`E:/oc16c` residue was verified to contain only empty directories and removed
nonrecursively. The active issue checkout, review deliverables and unrelated
worktrees remain. Cleanup is verified in the ignored dated receipt.
