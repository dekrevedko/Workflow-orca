-------------------------- MODULE ContinuationLease --------------------------
EXTENDS Naturals
CONSTANTS FenceDisposition, FenceAppend
Drivers == {"B", "C"}
VARIABLES row, attempt, expired, late, phase, commits, winner
vars == <<row, attempt, expired, late, phase, commits, winner>>

Init ==
    /\ row = "pending" /\ attempt = 0 /\ expired = FALSE /\ late = "none"
    /\ phase = [w \in Drivers |-> "idle"] /\ commits = 0 /\ winner = "none"

ClaimA ==
    /\ row = "pending" /\ row' = "claimed" /\ attempt' = 1
    /\ UNCHANGED <<expired, late, phase, commits, winner>>
ExpireA ==
    /\ attempt = 1 /\ ~expired /\ expired' = TRUE
    /\ UNCHANGED <<row, attempt, late, phase, commits, winner>>
Claim(w) ==
    /\ phase[w] = "idle"
    /\ IF w = "B" THEN attempt = 1 /\ expired /\ row = "claimed"
                    ELSE attempt = 2 /\ row = "retryable"
    /\ row' = "claimed" /\ attempt' = attempt + 1
    /\ phase' = [phase EXCEPT ![w] = "read"]
    /\ UNCHANGED <<expired, late, commits, winner>>

\* A retained attempt 1. Its cancellation/conflict marks Retryable by ID.
\* Its backoff release tests the current row state, also by ID.
LateA(op) ==
    /\ late = "none" /\ attempt = 2
    /\ late' = IF phase["B"] = "done"
               THEN IF op = "retry" THEN "retry-after" ELSE "release-after"
               ELSE IF op = "retry" THEN "retry-before" ELSE "release-before"
    /\ row' = IF ~FenceDisposition /\ (op = "retry" \/ row = "claimed")
              THEN "retryable" ELSE row
    /\ UNCHANGED <<attempt, expired, phase, commits, winner>>

\* One runnable stream position. Read and append are separate so that two
\* drivers can read the same expected version before either commits.
Read(w) ==
    /\ phase[w] = "read"
    /\ phase' = [phase EXCEPT ![w] = IF commits = 0 THEN "append" ELSE "mark"]
    /\ UNCHANGED <<row, attempt, expired, late, commits, winner>>
Append(w) ==
    /\ phase[w] = "append"
    /\ commits' = IF commits = 0 \/ ~FenceAppend THEN commits + 1 ELSE commits
    /\ winner' = IF commits = 0 THEN w ELSE winner
    \* On conflict reload; at the completed position the drive is a no-op.
    /\ phase' = [phase EXCEPT ![w] = IF commits' = commits THEN "read" ELSE "mark"]
    /\ UNCHANGED <<row, attempt, expired, late>>
Dispatch(w) ==
    /\ phase[w] = "mark"
    /\ row' = IF ~FenceDisposition \/ (row = "claimed" /\ attempt = (IF w = "B" THEN 2 ELSE 3))
              THEN "dispatched" ELSE row
    /\ phase' = [phase EXCEPT ![w] = "done"]
    /\ UNCHANGED <<attempt, expired, late, commits, winner>>

Step == ClaimA \/ ExpireA \/ (\E op \in {"retry","release"} : LateA(op))
        \/ (\E w \in Drivers : Claim(w) \/ Read(w) \/ Append(w) \/ Dispatch(w))
Settled == late # "none" /\ row = "dispatched" /\ phase["B"] = "done"
           /\ phase["C"] \in {"idle","done"}
Next == Step \/ (Settled /\ UNCHANGED vars)
Spec == Init /\ [][Next]_vars
FairSpec == Spec /\ WF_vars(Step)

SingleCommit == commits <= 1
DispatchedHasProgress == row = "dispatched" => commits > 0
EventuallySettled == <> (Settled /\ commits = 1)
\* Candidate guarantees of the fenced design, separately from DR-034.
LiveClaimPreserved == (attempt = 2 /\ phase["B"] # "done") => row = "claimed"
DispatchPreserved == phase["B"] = "done" => row = "dispatched"

\* Each witness includes a replacement drive and an observable poll outcome.
NoLateRetryReclaim == ~(late = "retry-after" /\ attempt = 3 /\ winner = "B" /\ Settled)
NoLateReleaseNoOp == ~(late = "release-after" /\ attempt = 2 /\ winner = "B" /\ Settled)
NoEarlyRetryReclaim == ~(late = "retry-before" /\ attempt = 3 /\ phase["B"] # "done")
NoEarlyReleaseReclaim == ~(late = "release-before" /\ attempt = 3 /\ phase["B"] # "done")
=============================================================================
