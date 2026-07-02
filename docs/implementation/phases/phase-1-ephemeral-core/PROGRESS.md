# Phase 1 Progress

T1-01 | done | 2026-07-02 | deviations: none
T1-02 | done | 2026-07-02 | deviations: included assertion-style correction to use AwesomeAssertions across existing tests
T1-03 | done | 2026-07-02 | deviations: none
T1-04 | done | 2026-07-02 | deviations: none
T1-05 | done | 2026-07-02 | deviations: Core grants InternalsVisibleTo to OrcaCore.Engine.Ephemeral so the engine can walk the internal definition tree without making it public
T1-05a | done | 2026-07-02 | reviewer: Codex implementation agent; deviations: AC-006 ownership moved from T1-06 to T1-08 because wait/resume behavior starts there; task paths use v3-gpt override
T1-06 | done | 2026-07-02 | deviations: none
T1-07 | done | 2026-07-02 | deviations: none
T1-08 | done | 2026-07-02 | deviations: updated the old unsupported-result guard from WaitForEvent to Yield because WaitForEvent is now implemented in this task; stayed at 10 files but exceeded the rough 500-line budget due wait API and acceptance coverage
T1-09 | done | 2026-07-02 | deviations: out-of-order event tests model "before its wait exists" as the second wait's event arriving while the first wait is active because no start handle exists yet
T1-10 | done | 2026-07-02 | deviations: correlation index is derived from current in-memory instance state rather than maintained as a separate materialized multi-map
T1-11 | done | 2026-07-02 | deviations: stale loop events are filtered by consumed wait signature rather than a serialized execution-frame identity
T1-12 | done | 2026-07-02 | deviations: parallel branch start is deterministic in definition order; racing branch resumes are serialized by the per-instance lane
T1-13 | done | 2026-07-02 | deviations: state copies use System.Text.Json serialization for this baseline
