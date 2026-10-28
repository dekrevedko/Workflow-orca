# Issue #13 task 3.1 — extended review verdict, 2026-10-28

**Verdict: REJECT publication readiness. Two P2 findings.**

This is a fresh adversarial self-review requested by the user, including regression
and documentation/requirement consistency analysis. It is not an external reviewer
approval. It covers the task 3.1 frozen target and the underlying issue #13 source.
It supersedes the earlier self-review's “no open finding” conclusion for advancing
to publication. Earlier artifacts, direct user approval and commits remain intact.
No implementation fix, commit, push, PR, issue update, merge or release is made.

## Reviewed target and preservation

- Branch: `codex/issue-13-native-aot-trimming`.
- Base/HEAD: `1ad2a5946b2874fd709571376a67799f5c7f9cd1`.
- Implementation checkpoint: `8566986850dc377f5a69e165dd58936be50ad722`,
  parent `10aec46e4a1f2f0771f6a5418b4dc6ef3ff3b5fd`, tree
  `6f697b1374800a2b5d7b4196f9c841f0330d6247`, exactly 327 paths.
  HEAD remains its sole direct user-verdict child.
- Frozen publication target: exactly the 30 paths in
  `task-3-1-review-manifest-2026-10-27.txt`, tree
  `3596bfa07556e6da81f64633d0e65bf3734d1c61`, content SHA256
  `6ba5c0ba2576471039e72991f3698764b8f5ad189757d62375f079f8f46014ae`.
- All 30 original file hashes match before and after the review, and matched the
  independently built scratch copy. Product source, canonical/delta specs,
  executable baselines and the real index are unchanged. This new verdict is
  excluded from the frozen manifest; the extra untracked verdict changes the
  whole-worktree status hash, not the approved target's bytes/tree.

## F1 — P2: another Generated-mode admission difference is unapproved

Location: `src/OrcaCore.Abstractions/Serialization/FixedWorkflowValueCodec.cs:102–107`,
particularly the setter-only-member inclusion at line 104. Legacy member selection
is at lines 177–186 and 230–240.

The generated shape walk treats a metadata property with a setter and no getter
as a member to validate. The legacy walk excludes it from serialization. Two
ordinary, source-generated-context DTOs reproduce the difference:

```csharp
public sealed class WriteOnlyMap
{
    public int Value { get; set; }
    public Dictionary<int, int> Hidden { set { } }
}
public sealed class WriteOnlyCustom
{
    public int Value { get; set; }
    [JsonConverter(typeof(CustomIntConverter))]
    public int Hidden { set { } }
}
```

Neither hidden member is emitted by the fixed serializer. With `Value = 7`, the
old-master public codec and current legacy mode both successfully detach/return
Value=7. Current legacy mode also builds, registers, starts and completes both
workflows successfully. With the ordinary Metadata context registered, current
Generated mode rejects both Build/TryBuild with SFE-TYPE-002 and rejects direct
materialization with NotSupportedException. All relevant roots are registered;
there is no F1 initializer shape, missing schema, nondefault context option or
handwritten resolver. The compiler/source generator produces zero warnings/errors.

This is a new difference introduced by opting into Generated mode, not a regression
in unconfigured JIT. Design §5 (line 75) says there is a single Generated-mode
admission exception; the approved protocol's NS-06 row explicitly calls F1 the sole
new rejection-parity exception. The additional restriction is not recorded there.
The claim of preserved admission/rejection behavior is therefore too broad.

Required resolution: reconcile the generated member selection with the fixed
legacy serialization semantics and add these positive parity controls. If rejecting
these otherwise legacy-admitted DTOs is intentional, it requires a reviewed contract
amendment and explicit documentation/test coverage rather than silent admission
narrowing. Preserve the already approved F1 and customization negatives.

## F2 — P2: included members bypass runtime rejection and silently lose data

Location: the same codec, lines 366–369; legacy runtime traversal at lines 391–401.

The Generated runtime graph walk only validates metadata backed by PropertyInfo
with a public getter or setter. It skips FieldInfo and an included property whose
accessors are both internal. Legacy traversal inspects only public properties.
Examples (ExtraItem is explicitly registered in the generated context):

```csharp
public class Item { public int Number { get; set; } }
public sealed class ExtraItem : Item { public int Extra { get; set; } }
public sealed class FieldPolymorphism
{
    [JsonInclude] public Item Value = new();
}
public sealed class InternalPropertyPolymorphism
{
    [JsonInclude] internal Item Value { get; set; } = new();
}
```

Submitting ExtraItem(Number=7, Extra=99) through either model is accepted in both
current modes. The supported public Build → Register → StartOrGetAsync →
WaitForOutputAsync path completes successfully with plain Item(Number=7), losing
Extra=99. A normal public-property version instead correctly throws the fixed
codec's unapproved-polymorphism NotSupportedException before acceptance.

A companion included-field `IReadOnlyList<int>` holding an application CustomList
also succeeds and becomes List<int>, whereas the same ordinary public property
correctly rejects CustomList as outside the runtime collection allowlist.

AC-570 / NS-08 and the fixed-codec rules require unapproved polymorphism and
forbidden collections to reject before commit, not complete with truncated data.
The retained controls show the gap is member traversal, not missing metadata or
an intentionally approved polymorphic contract. Direct public value materialization
reproduces the same results as full workflow execution.

**Regression classification:** the original master at
`3b7f98aa0748a4d9c3f54d4604e566ed61f21bad` reproduces the included-member loss and
collection acceptance through its public codec boundary. This is an existing bug
and a missed requirement/qualification case carried into the new mode, not a newly
introduced ordinary-JIT regression. It still prevents claiming that the present
source satisfies the stated rejection rules.

Required resolution: cover every actually serialized included member at the
runtime graph boundary, with public-package regression controls for fields and
internal included properties, unapproved subtypes and custom collections in both
modes. Preserve existing getter-invocation/ownership semantics; a naive additional
getter pass must not weaken the existing getter-count controls. Extend the native
consumer negatives and rerun affected qualification after the reviewed fix.

## P3 — nonblocking wording

`docs/specs/12-acceptance-criteria.md:778` still calls the issue #13 assertions
“mandatory future source/package/native assertions”, while its updated section
status and guides say source-qualified/release-pending. Reword that sentence at the
next target to distinguish completed evidence from ongoing regression obligations.
Earlier dated unsupported assessments and released preview.2 claims correctly
remain historical; those should not be rewritten.

## Fresh verification and limits

Validation ran in a detached worktree at `E:/oc13r28`, with exactly the frozen 30
files over HEAD and only owned review inputs/outputs added outside that target.

| Fresh lane | Result |
|---|---:|
| OpenSpec all/strict | 25/25 |
| Full Release build, warnings as errors | 0 warnings, 0 errors |
| Core unit | 354/354 |
| Ephemeral unit | 352/352 |
| Durable unit | 102/102 |
| Acceptance unit | 37/37 |
| Hosting unit | 24/24 |
| Provider certification unit | 100/100 |
| Six CI unit projects total, dotnet test with coverage | 969/969 |
| Full Infrastructure, CI engine-coverage include | 302/302 |
| Local file links in changed root/docs Markdown, excluding fenced code | 78/78 |
| ReSharper 2026.2.3.1, built Release / Any CPU / net10.0, 52 files | 0 errors, 583 warnings, 343 notes |

ReSharper retained suggestions and completed before SARIF was parsed. Its complete
multiset of rule/severity/file/message matches the previous 52-file inspection,
ignoring lines shifted by guard insertions; prior triage is retained with the
comparison. This is not a claim that its 926 diagnostics disappear or prove
behavioral correctness. The two findings above are executable behavior probes.

The corrected current package probes and historical source comparison all build
with zero warnings/errors and execute successfully. Their successful process exits
mean the observations were collected, not that conformance passed: explicit checks
confirm the mismatches/loss and the rejecting property controls. An initial engine
probe mistakenly called Task.WaitAsync on ValueTask; that harness-only compile error
was corrected to AsTask().WaitAsync() before any finding was recorded.

Real-provider/integration runs, the 50-run stress batch, aggregate CI engine coverage
and the six native cells were not repeated in this review. Their approved task-2.5
records remain unchanged. In particular, native run 40629471843's 356 recorded
cases do not cover these newly discovered cases. No fresh native/AOT claim is made
from local Windows JIT probes. The new guide sample's previous JIT package check
is carried, not relabelled a new native run.

The metadata catalogue/baseline/architecture/provenance checks remain green, and
the publication-only diff changes no product bytes. The two semantic gaps are why
those green checks do not justify advancing. Other engines and live ephemeral
event delivery/publishing still receive no native support credit.

## Evidence, cleanup and next gate

Compact, portable public-package repro inputs, JSON observations, old-master
comparison, package hashes and assertion summary are retained in
`.artifacts/publishing-validation/issue-13-extended-review-2026-10-28/repro/`.
The same review receipt's `raw/` holds commands, build/test logs, TRX, coverage,
full SARIF/triage and corrected/initial harness results. Its freeze/preflight/end
receipts identify original bytes, evidence copies and cleanup. These are local
ignored review deliverables, not additional paths in the frozen contract target.

After byte-verifying 80 raw evidence copies and both sets of 30 frozen files,
`git worktree remove --force E:/oc13r28` succeeded. Its approximately 530 MiB of
owned source/build/package/cache/scratch files are removed. Automatic approval
review rejected compression/removal of redundant copied coverage files as
“blocked by policy”; about 87 MiB of raw review receipts remain intact. Prior
blocked consumer-temp/cache cleanup was not retried or bypassed. No shared cache,
unrelated worktree or existing deliverable was removed.

Keep task 3.1 uncommitted and publication held. Prepare a separately reviewable
fix/contract-reconciliation target for F1/F2 and the P3 wording, then rerun the
required regression/native gates before requesting approval to advance. This
review itself authorizes no source edits or publication action.
