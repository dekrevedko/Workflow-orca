# Issue 16 source scenario coverage — 2026-11-02

Base: 5960f0b. This is a source-review candidate, not source approval or native
qualification. Existing ownership assertions were retained. Owning-runtime cases
exercise retention off and bounded retention; retirement-specific weak-reference
controls necessarily use bounded retention.

| Contract row | Actual evidence | Status |
|---|---|---|
| FS-01 | OriginalStepFailureTests.ThrownRootFailure_PreservesTheActualObject: IOException and InvalidDataException, both policies; identity, message, stack, code, type and root occurrence | Local green |
| FS-02 | ReturnedOrThrownKnownError_RetainsCodeTypeAndObject, thrown/returned × both policies | Local green |
| FS-03 | FinalRetry_SelectsActualObjectAndPreservesUserInvocationWrapper: root/branch/item; private business-boundary wrapper excludes DI, codecs and merge | Local green |
| FS-04 | FinalRetry: distinct equal-message objects on two attempts, repeated output observations retain the final actual object | Local green |
| FS-05 | PhysicalOwnershipTests' 36 timeout cases plus existing cancel/terminate controls; LogicalTimeout_DoesNotSettleBlockedPhysicalWork covers all three scopes; unrelated root/branch/item cancellation controls preserve prior classification | Local green |
| FS-06 | FinalRetry single branch/item failures and TrueAggregate_HasOnlyOrderedDetachedTypedCauses: ordered typed causes, no invented aggregate original | Local green |
| FS-07 | RecoveredBranch_OnlySuppliesDetachedType_AndDoesNotRetainOriginal: branch and ForEach recovery, weak references collected while still Waiting | Local green |
| FS-08 | WorkflowFailureCodecAmendmentTests exact legacy/mixed recursive golden bytes; NativeMetadata CodecCases compares generated and legacy fingerprints; stored durable JSON slice equals the independently captured base witness | Local green |
| FS-09 | 15 malformed codec cases, exact blank-message fallback and verbatim nonblank messages; malformed property sets reject duplicates, missing names and wrong versions/types | Local green |
| FS-10 | PhysicalOwnershipTests and LogicalTimeout: output/terminal status cannot settle blocked physical work; cancellation callback remains owned after body exit | Local green |
| FS-11 | ContinuationOwnershipTests' retry/resource-grant/timer completion barriers, commit observer controls, idle receipt and final admission-seal assertions | Local green |
| FS-12 | IdleIsNotSettlement_ObserversCancelLocally and public ResourceRecipe: one cancelled observer leaves the workflow and another observer untouched | Local green |
| FS-13 | SettlementReceiptTests' concurrent observer/cancellation race and synchronous final seal; one monotonic asynchronous-continuation completion, no polling | Local green |
| FS-14 | Count/age/key-reuse controls retain an original-ID receipt after retirement; normal operations remain original-ID not-found | Local green |
| FS-15 | RetiredFailure_DropsTheEngineReference and Retirement_ClearsTheRuntimeSlot: engine references clear while a caller-owned wrapper remains valid; four resultless/resultful owner-graph weak controls | Local green |
| FS-16 | ShutdownDrainTests, ShutdownOwnershipTests and Receipt_DoesNotWaitForQueryPin: Closed/Stopping observation, callback joins, Completed/DI-disposal passive path | Local green |
| FS-17 | Existing provisional cancellation/exception-after-Init controls now assert no accepted receipt; public NoHandleResourceRecipe keeps resources until host-wide Completed | Local green |
| FS-18 | Two-member compiled API baseline and MinimalEphemeral compile fixture; armed durable provider proxy records zero access, token-first and disposed-owner controls; fresh seven-package durable consumer | Local green |
| FS-19 | Fresh PackageReference-only consumer: generated and legacy ResourceRecipe, NoHandleResourceRecipe and ExternalChildRemainsApplicationOwned; exact restored package and DLL identities | 84/84 local JIT preflight; not native qualification |
| FS-20 | Protocol 4 preserves every original journey/group/subcase, adds failure/settlement witnesses and hard captured-type parity. Default gate rejects the Windows 11 dirty preflight as unqualified | Pending actual six-cell hosted matrix |

The final ephemeral suite passes 405/405 and twenty consecutive paired unfiltered
dotnet test runs. The former 19/20 stress result is retained: the failing existing
shutdown assertion raced legitimate cooperative completion. A test-only
cancellation-exit gate now makes physical incompleteness an explicit prerequisite,
without relaxing its Incomplete, pending-work, abort, removal or Completed checks.

The speculative nested-aggregate recovery probe was withdrawn: selected branch
builders do not expose nested Parallel. Its compile failure is not a red behavior
test or evidence of a runtime defect. No API or product change was made for it.
