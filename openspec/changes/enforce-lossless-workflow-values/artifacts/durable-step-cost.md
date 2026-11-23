# Cost of a completed durable root step

A completed root step can hand its verified state bytes directly to the checkpoint writer. The detached state still supplies the next instruction. The byte array lives for one completion and never crosses an author callback. Branch state, failed attempts, yields and checkpoint loads retain their existing codec checks.

## Full-workflow measurements

Each run created a fresh in-memory provider and registered a durable workflow with 48 sequential, DI-resolved root steps, one execution path and no pools or throttles. The state contained an integer counter and an array of either zero or 32 records with an integer index and a string name. Each step incremented the counter. Timing began immediately before `StartOrGetAsync` and ended after `WaitForOutputAsync` returned 48. Host setup and disposal were outside the measurement. Process-wide allocations came from `GC.GetTotalAllocatedBytes(true)`. A counter in the state constructor recorded all constructions during the run, including initialization and checkpoint materialization.

The same Release harness ran in three separate .NET 10.0.15 processes per round with tiered compilation disabled. Each process warmed up with three workflows per shape, then measured nine. Six rounds used all six orderings of the builds. Each table entry is the median of the six process medians. Bytes and elapsed time are per complete 48-step workflow.

| State entries | Build | Elapsed (ms) | Allocated bytes | State constructions |
| ---: | --- | ---: | ---: | ---: |
| 0 | Merged master `499b9018` | 5.113 | 4,185,696 | 97 |
| 0 | Phase B after the Init fix `defa1d8f` | 5.368 | 4,383,704 | 195 |
| 0 | Phase B with completion bytes reused | 5.148 | 4,259,896 | 147 |
| 32 | Merged master `499b9018` | 10.053 | 7,617,248 | 97 |
| 32 | Phase B after the Init fix `defa1d8f` | 14.426 | 9,687,064 | 195 |
| 32 | Phase B with completion bytes reused | 11.397 | 8,445,992 | 147 |

Reusing the capture removed exactly one reconstruction per completed step for both shapes. With 32 entries, it saved 1,241,072 bytes per workflow and improved elapsed time in all six rounds. The median paired time reduction was 22.0%. The final build still took 12.9% longer than master by median paired time and allocated 828,744 more bytes per workflow. The empty-array runs split evenly on which Phase B build finished faster, while reusing bytes saved 123,808 bytes per workflow.

The deterministic regression `CompletedRootStepPersistsTheVerifiedCaptureWithoutReconstructingItAgain` drives with a one-command budget, counts constructions and reads the actual checkpoint bytes before `End`. These timings describe this in-memory workload on one Windows host. Remote storage, contended instances and other payloads have different cost profiles.
