# Task 8.1 — DAG package and hosting-role reconciliation

Date: 2026-09-27. Base: approved Task 8.0 activation `f316d36fbbac706e55ddbe2e5d2a4529b98cbc67`.

The approved Task 8.0 map calls for reconciling, not recreating, the existing `OrcaCore.Dag` and `OrcaCore.Dag.Hosting` projects. Both package IDs and assembly names already match the exact twelve-package manifest. Their direct references remain `OrcaCore.Dag -> OrcaCore` and `OrcaCore.Dag.Hosting -> OrcaCore.Dag + OrcaCore.Durable.Hosting`; no provider, runtime-protocol, Kubernetes, AWS, or scheduler reference was added. The existing single product friend grant from `OrcaCore.Durable.Hosting` to `OrcaCore.Dag.Hosting` is unchanged.

`AddOrcaCoreDag(DagHostOptions)` retains its sole public owner and exact approved signature. Registration validates the positive node limit before mutation, accepts only the durable-engine role, copies the integer limit into its private profile, and adds only internal definition-registry and coordinator service identities. They have no definition-registration or progression methods yet; tasks 8.2–8.6 own those semantics. Repeated identical registration is a no-op; a conflicting limit fails before adding services. Callback-only ingress does not satisfy the durable-engine role. No DAG hosted loop, child-start bridge, public child operation, typed plan, or execution claim is introduced by this task.

The existing compiled `six-hosting-entry-owners` and `role-exclusivity-and-dependencies` scenarios now verify the three exact DAG-owned DI service types, successful resolution, absence of an extra hosted loop, idempotence, conflict nonmutation, and callback-only rejection. The exact public API baseline remains unchanged; the package-source provenance record changes only for `OrcaCore.Dag.Hosting`. The nine Task 3.10 DAG scenarios remain expected-red pending the behavior and acceptance work in tasks 8.2–8.10.

Validation at preparation: Release solution build with `-warnaserror --no-incremental` 0 warnings/0 errors; focused hosting scenarios 4/4; Hosting 24/24; Infrastructure 226/226; `git diff --check` clean. This implementation record is not independent approval or a checkpoint.
