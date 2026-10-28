# Issue #13 corrected whole target — checkpoint review request

Base: `1ad2a5946b2874fd709571376a67799f5c7f9cd1`, following implementation
`8566986850dc377f5a69e165dd58936be50ad722`.
Branch: `codex/issue-13-native-aot-trimming`.

This target combines publication preparation with the two whole-AOT codec fixes
and P3 wording. Exactly three production files differ from the implementation
checkpoint. No public catalogue, canonical/delta requirement, API/compile/dependency
baseline or additional engine changes. The corrected source/tests are byte-identical
to the locally validated fix target; this step adds actual native requalification,
its binding, truthful living docs and the revised PR draft.

[Run 40762764617](https://github.com/dekrevedko/Workflow-orca/actions/runs/40762764617)
executes temporary validation commit 7f1034c: 82 JIT, 55 full-trim and 55 AOT cases
per native host, 384/384. All 360 source/build inputs, 20 harness inputs, 12 feed
packages and three selected DLLs bind. The 24 warning records match the approved
handwritten-context negative with only its line positions moved. Original golden
and strong-value corpora are unchanged. Protocol 3 rejects incomplete/history-only
qualification, and current correction approvals remain null.

Fresh checks: Release 0 warnings/errors, OpenSpec 25/25, Infrastructure 309/309,
final documentation/binding guards 25/25 and scoped ReSharper 0 errors/warnings,
two unchanged style notes. Unchanged-source receipts retain unit 969/969,
metadata 41/41, provider/integration 188/188, requirements 60/60, fifty consecutive
CI-path stress runs and 79.3357% engine coverage. The validation record separately
classifies the first fresh-worktree missing-feed setup failure and its green rerun.

The self-inclusive native-review manifest lists the checkpoint paths. Exclude
codec-member-fixes-native-independent-review-verdict-2026-10-28.md so the verdict
can be a separate child when checkpointing is approved. Exact tree, content/status
hashes, per-file bytes and the excluded verdict hash are in the local receipt:
`.artifacts/publishing-validation/issue-13-codec-native-2026-10-28/freeze.json`.

Companions: codec-member-fixes-native-validation-2026-10-28.json,
native-codec-fixes-2026-10-28/index.json, the scoped SARIF and the new PR draft.
The user authorized the temporary validation branch with “Ok continue”; no
issue-branch checkpoint/push, PR, merge, issue closure or release has occurred.
Tasks 3.1/3.2 remain open pending their approval/publication gates.
