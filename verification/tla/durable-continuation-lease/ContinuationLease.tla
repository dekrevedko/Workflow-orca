-------------------------- MODULE ContinuationLease --------------------------
EXTENDS Naturals
CONSTANTS CheckState, CheckAttempt, FenceAppend
Drivers == {"B", "C"}
VARIABLES row, attempt, expired, late, phase, commits, winner, replayed
vars == <<row, attempt, expired, late, phase, commits, winner, replayed>>

Init ==
    /\ row = "pending" /\ attempt = 0 /\ expired = FALSE /\ late = "none"
    /\ phase = [w \in Drivers |-> "idle"] /\ commits = 0 /\ winner = "none" /\ replayed = FALSE

ClaimA ==
    /\ row = "pending" /\ row' = "claimed" /\ attempt' = 1
    /\ UNCHANGED <<expired, late, phase, commits, winner, replayed>>
ExpireA ==
    /\ attempt = 1 /\ ~expired /\ expired' = TRUE
    /\ UNCHANGED <<row, attempt, late, phase, commits, winner, replayed>>
Claim(w) ==
    /\ phase[w] = "idle"
    /\ IF w = "B" THEN attempt = 1 /\ expired /\ row = "claimed"
                    ELSE attempt = 2 /\ row = "retryable"
    /\ row' = "claimed" /\ attempt' = attempt + 1
    /\ phase' = [phase EXCEPT ![w] = "read"]
    /\ UNCHANGED <<expired, late, commits, winner, replayed>>

CanDispose(held) == /\ (~CheckState \/ row = "claimed")
                    /\ (~CheckAttempt \/ (held > 0 /\ held = attempt))
\* A keeps its first claim even after B acquires the second attempt.
LateA(op) ==
    /\ late = "none" /\ attempt = 2
    /\ late' = IF phase["B"] = "done"
               THEN IF op = "retry" THEN "retry-after" ELSE "release-after"
               ELSE IF op = "retry" THEN "retry-before" ELSE "release-before"
    /\ row' = IF CanDispose(1)
              THEN "retryable" ELSE row
    /\ UNCHANGED <<attempt, expired, phase, commits, winner, replayed>>

\* One runnable stream position. Read and append are separate so that two
\* drivers can read the same expected version before either commits.
Read(w) ==
    /\ phase[w] = "read"
    /\ phase' = [phase EXCEPT ![w] = IF commits = 0 THEN "append" ELSE "mark"]
    /\ UNCHANGED <<row, attempt, expired, late, commits, winner, replayed>>
Append(w) ==
    /\ phase[w] = "append"
    /\ commits' = IF commits = 0 \/ ~FenceAppend THEN commits + 1 ELSE commits
    /\ winner' = IF commits = 0 THEN w ELSE winner
    \* On conflict reload; at the completed position the drive is a no-op.
    /\ phase' = [phase EXCEPT ![w] = IF commits' = commits THEN "read" ELSE "mark"]
    /\ UNCHANGED <<row, attempt, expired, late, replayed>>
Dispatch(w) ==
    /\ phase[w] = "mark"
    /\ row' = IF CanDispose(IF w = "B" THEN 2 ELSE 3)
              THEN "dispatched" ELSE row
    /\ phase' = [phase EXCEPT ![w] = "done"]
    /\ UNCHANGED <<attempt, expired, late, commits, winner, replayed>>

\* A duplicate disposition by B carries the right ordinal but targets a terminal row.
ReplayB ==
    /\ phase["B"] = "done" /\ late # "none" /\ attempt = 2 /\ ~replayed
    /\ row' = IF CanDispose(2) THEN "retryable" ELSE row
    /\ replayed' = TRUE
    /\ UNCHANGED <<attempt, expired, late, phase, commits, winner>>

Step == ClaimA \/ ExpireA \/ ReplayB \/ (\E op \in {"retry","release"} : LateA(op))
        \/ (\E w \in Drivers : Claim(w) \/ Read(w) \/ Append(w) \/ Dispatch(w))
Settled == late # "none" /\ row = "dispatched" /\ phase["B"] = "done"
           /\ phase["C"] \in {"idle","done"}
Next == Step \/ (Settled /\ UNCHANGED vars)
Spec == Init /\ [][Next]_vars
FairSpec == Spec /\ WF_vars(Step)

SingleCommit == commits <= 1
DispatchedHasProgress == row = "dispatched" => commits > 0
EventuallySettled == <> (Settled /\ commits = 1)
\* The replacement retains its lease until it settles its own claim.
LiveClaimPreserved == (attempt = 2 /\ phase["B"] # "done") => row = "claimed"
DispatchPreserved == phase["B"] = "done" => row = "dispatched"

\* Fenced schedules must actually execute each late call at the named boundary.
NoLateRetryNoOp == ~(late = "retry-after" /\ attempt = 2 /\ winner = "B" /\ Settled)
NoLateReleaseNoOp == ~(late = "release-after" /\ attempt = 2 /\ winner = "B" /\ Settled)
NoEarlyRetryNoOp == ~(late = "retry-before" /\ row = "claimed" /\ attempt = 2 /\ phase["B"] # "done")
NoEarlyReleaseNoOp == ~(late = "release-before" /\ row = "claimed" /\ attempt = 2 /\ phase["B"] # "done")
NoTerminalReplay == ~(replayed /\ Settled /\ commits = 1 /\ attempt = 2)
=============================================================================
