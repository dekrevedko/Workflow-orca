# Issue #13 task 2.1 ReSharper inspection — 2026-10-24

Addendum to the completed transition self-review. ReSharper InspectCode
**2026.2.3.1** completed successfully (exit 0), with default building enabled,
`Release / Any CPU / net10.0`, the real `OrcaCore.slnx`, repository EditorConfig,
and no disabled solution-wide analysis. Reporting includes suggestions; it is
explicitly filtered to the six changed C# files in the Guards project. No claim
is made about excluded files/projects or editor/runtime validation.

## Findings

**0 errors, 55 warnings, 60 suggestions** (SARIF note level). All 115 result
locations map to retained unchanged lines in `OpenSpecCorpusGuards.cs` or
`RecoveryCrosswalkGuards.cs`. None points at newly added/modified code, and the
new `NativeSupportTransitionGuards.cs` has no reported findings. This is a
location/diff triage, not a separate before/after ReSharper baseline run.

| Rule | Severity | Count | Disposition |
|---|---|---:|---|
| `CSharp14OverloadResolutionWithSpanBreakingChange` | note | 1 | Retained direct compiled test assertion; no expression interpreter added by this slice. |
| `ClassNeverInstantiated.Local` | note | 17 | Retained records consumed by JSON deserialization, not direct construction. |
| `ConvertToUsingDeclaration` | note | 1 | Retained style/redundancy suggestion; no broad cleanup of existing guard code in this reviewed slice. |
| `MergeIntoPattern` | note | 2 | Retained style/redundancy suggestion; no broad cleanup of existing guard code in this reviewed slice. |
| `MoveVariableDeclarationInsideLoopCondition` | note | 1 | Retained style/redundancy suggestion; no broad cleanup of existing guard code in this reviewed slice. |
| `RedundantExplicitParamsArrayCreation` | note | 1 | Retained style/redundancy suggestion; no broad cleanup of existing guard code in this reviewed slice. |
| `RedundantVerbatimStringPrefix` | note | 12 | Retained style/redundancy suggestion; no broad cleanup of existing guard code in this reviewed slice. |
| `UseCollectionExpression` | note | 15 | Retained style/redundancy suggestion; no broad cleanup of existing guard code in this reviewed slice. |
| `UseUtf8StringLiteral` | note | 9 | Retained style/redundancy suggestion; no broad cleanup of existing guard code in this reviewed slice. |
| `UseWithExpressionToCopyTuple` | note | 1 | Retained style/redundancy suggestion; no broad cleanup of existing guard code in this reviewed slice. |
| `MemberHidesStaticFromOuterClass` | warning | 6 | Retained nested JSON record/member names; no task-2.1 declaration changes. |
| `NotAccessedPositionalProperty.Local` | warning | 38 | Retained fixture/record data; removing fields would broaden the provenance schema scope. |
| `PossibleMultipleEnumeration` | warning | 2 | Retained in-memory provenance inventory query; no task-2.1 query/data-shape change. |
| `RedundantEnumerableCastCall` | warning | 1 | Retained style/redundancy suggestion; no broad cleanup of existing guard code in this reviewed slice. |
| `RedundantSuppressNullableWarningExpression` | warning | 7 | Retained style/redundancy suggestion; no broad cleanup of existing guard code in this reviewed slice. |
| `UnusedMember.Local` | warning | 1 | Retained style/redundancy suggestion; no broad cleanup of existing guard code in this reviewed slice. |

## Evidence and scope

- The scoped files are DagStreamIdentitySliceGuards, EphemeralShutdownTransitionGuards, NativePublishingAssessmentGuards, NativeSupportTransitionGuards, OpenSpecCorpusGuards and RecoveryCrosswalkGuards.
- Raw SARIF, exact invocation/version/settings scope, build/inspection log and all 115 per-location dispositions are retained under ignored `.artifacts/publishing-validation/issue-13-task-2/resharper/`.
- The initial invocation stopped before inspection because `AnyCPU` is not this solution’s platform spelling. The successful run uses `Any CPU`; that setup failure is recorded separately and receives no inspection credit.
- All 38 pre-inspection author files and the empty index/status remain unchanged. No CleanupCode operation or source rewrite was performed. The reviewed 37-path slice hash remains intact; this addendum is additional evidence.
- Full test lanes were not repeated for a read-only inspection. The existing green validation remains the task 2.1 receipt; this report does not establish Native AOT or product acceptance.
- Future source-slice reviews include scoped ReSharper inspection and findings triage before advancing. Product baseline/historical findings are preserved unless a scoped change warrants a fix.
- The disposable inspection checkout owns all build/cache output and is removed after retaining these compact receipts.
