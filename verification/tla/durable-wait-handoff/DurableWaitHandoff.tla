------------------------- MODULE DurableWaitHandoff -------------------------
EXTENDS Naturals
CONSTANT Fault
Workers == {1,2}
VARIABLES active, inbox, matches, pending, completions, local, crashed
vars == <<active,inbox, matches, pending, completions, local, crashed>>
Init ==
    /\ active = TRUE /\ inbox = "received" /\ matches = 0 /\ pending = FALSE /\ completions = 0
    /\ local = [w \in Workers |-> "idle"] /\ crashed = {}
Read(w) ==
    /\ local[w] = "idle" /\ ((active /\ inbox = "received") \/ pending)
    /\ local' = [local EXCEPT ![w] = IF active THEN "match" ELSE "drive"]
    /\ UNCHANGED <<active,inbox,matches,pending,completions,crashed>>
CommitMatch(w) ==
    /\ local[w] = "match"
    /\ IF active /\ inbox = "received" THEN
          /\ active' = FALSE /\ inbox' = "applied" /\ matches' = 1
          /\ pending' = (Fault # "lost-handoff")
       ELSE UNCHANGED <<active,inbox,matches,pending>>
    /\ local' = [local EXCEPT ![w] = IF matches = 0 THEN "match-reply" ELSE "idle"]
    /\ UNCHANGED <<completions,crashed>>
CommitResume(w) ==
    /\ local[w] = "drive"
    /\ completions' = IF pending \/ Fault = "stale-write" THEN completions+1 ELSE completions
    /\ pending' = FALSE
    /\ local' = [local EXCEPT ![w] = "drive-reply"]
    /\ UNCHANGED <<active,inbox,matches,crashed>>
Reply(w) ==
    /\ local[w] \in {"match-reply","drive-reply"}
    /\ local' = [local EXCEPT ![w] = "idle"]
    /\ UNCHANGED <<active,inbox,matches,pending,completions,crashed>>
Crash(w) ==
    /\ local[w] # "idle" /\ crashed = {}
    /\ crashed' = {IF local[w] = "match" /\ matches = 0 THEN "before"
                    ELSE IF local[w] = "match-reply" /\ pending THEN "after" ELSE "other"}
    /\ local' = [local EXCEPT ![w] = "idle"]
    /\ UNCHANGED <<active,inbox,matches,pending,completions>>
Advance(w) == Read(w) \/ CommitMatch(w) \/ CommitResume(w) \/ Reply(w)
Next == (\E w \in Workers : Advance(w) \/ Crash(w))
        \/ ((\A w \in Workers : local[w] = "idle") /\ completions = 1 /\ UNCHANGED vars)
Spec == Init /\ [][Next]_vars
FairSpec == Spec /\ (\A w \in Workers : WF_vars(Advance(w)))
Ownership == /\ (active <=> matches = 0)
             /\ (matches = 1 <=> inbox = "applied")
             /\ (matches = 1 <=> (pending \/ completions > 0))
             /\ ~(pending /\ completions > 0)
SingleCompletion == completions <= 1
Recovered == <> (completions = 1)
NoRecoveryBeforeCommit == ~(crashed = {"before"} /\ completions = 1)
NoRecoveryAfterCommit == ~(crashed = {"after"} /\ completions = 1)
=============================================================================
