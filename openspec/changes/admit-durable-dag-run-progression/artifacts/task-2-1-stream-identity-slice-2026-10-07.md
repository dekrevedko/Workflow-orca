# Task 2.1 first source slice: typed stream identity

Date: 2026-10-07. Base: 89e3b18aed62446583211478f1eb8545e4fb85ed. Pending independent source review; no checkpoint authorization.

## Exact implementation scope

Implements RC-3 section 2 and exactly the seven OrcaCore API keys in the approved catalog. WorkflowStreamId retains its workflow constructor, InstanceId and workflow text. DurableAggregateKind is closed (Workflow=0, DagRun=1); ForDagRun rejects empty GUIDs; DagRunId is nullable and InstanceId throws for DAG/default identities. Record equality/hash include family and identity. New baseline digest a8bb583860fd8744b018273911fe65b1c899926d70e501efb773ccfbf5588f3e preserves every old key and its nullability.

Existing workflow-only append/tail entry points in all three providers reject DAG/default identities before opening connections or touching state. This is validation, not DAG persistence. The same-GUID certification confirms rejected appends cannot consume a workflow version. A second defensive SQL Server state-engine check protects its internal direct entry point. The shared certification fake tail reader enforces the same workflow-only identity contract.

Four Core identity facts and two shared provider facts exercise workflow compatibility, DAG text, wrong-family access, family-separated equality/hash, empty/default values and no-effect rejection. No acceptance trait, expected-red disposition, DG-P exit or Task 8.4/8.5 completion is claimed. Full 2.1 remains open: the other 358 API keys, provider run persistence, codecs, schemas, fencing, discovery and atomic transitions are later reviewed slices. Pumps and the Hosting bridge are out of scope. No friend/package/reference change.

## Review carry-forward

The registry binds 1770b2b / tree 61b5900c / evidence 18bf865 / transition verdict e89e7066 using actual Git parent, addition and bytes, and a unique final APPROVE. Whole-file canonical content is pinned for all fourteen specs, including unowned blocks; the record digest terminates in guard source. Current guide status names the checkpoint without claiming implemented run progression. Old dated records and the RC-3 contract/API/signatures are unchanged. The new provenance artifact states the untrimmed preamble rule; the old artifact is cataloged byte-exact. Archive resolver date/copy tightening remains deferred.

## Freeze protocol

Validate both builds, all product/container lanes, exact 12-package feed, Infrastructure and intentional-red separation, OpenSpec, ReSharper and a committed disposable copy. Freeze all changed/new paths including this artifact, both packet files and provenance/history fixtures. Review the partial source target; do not check 2.1 or activate any later task after this slice alone.
