-------------------------- MODULE StartKeyRetirement --------------------------
EXTENDS Naturals, FiniteSets
CONSTANT Fault
Old == 1
Sweeper == 1  \* Selector identities are disjoint from the two caller identities.
Callers == {2,3}  \* Also the fresh IDs they would mint if they win the reservation.
VARIABLES request, phase, raw, owner, published, oldState, expired, sweep,
          selector, lateCleanup, inspected, result, visibleAtReturn, access
vars == <<request, phase, raw, owner, published, oldState, expired, sweep,
          selector, lateCleanup, inspected, result, visibleAtReturn, access>>
FP(g) == IF g = Old THEN "x" ELSE request[g]
Visible == IF raw = Old /\ oldState # "live" THEN 0 ELSE raw
OldPins == {c \in Callers : phase[c] = "pinned" /\ inspected[c] = Old}
Replay(c,g) == IF FP(g) = request[c] THEN <<"existing",g>> ELSE <<"conflict",FP(g)>>
Init ==
    /\ request \in [Callers -> {"x","y"}]
    /\ phase = [c \in Callers |-> "new"] /\ raw = Old /\ owner = 0 /\ published = {}
    /\ oldState = "live" /\ expired = FALSE /\ sweep = "pending"
    /\ selector = 0 /\ lateCleanup = FALSE
    /\ inspected = [c \in Callers |-> 0] /\ result = [c \in Callers |-> <<>>]
    /\ visibleAtReturn = [c \in Callers |-> 0] /\ access = [c \in Callers |-> <<>>]

\* One admission under the gate, including discarding a physically stale binding.
Inspect(c) ==
    /\ phase[c] = "new"
    /\ LET b == Visible IN
       /\ phase' = [phase EXCEPT ![c] = IF b # 0 THEN "pinned"
                                      ELSE IF owner = 0 THEN "starting" ELSE "waiting"]
       /\ inspected' = [inspected EXCEPT ![c] = b]
       /\ owner' = IF b = 0 /\ owner = 0 THEN c ELSE owner
       /\ raw' = b
    /\ UNCHANGED <<request, published, oldState, expired, sweep, selector, lateCleanup,
                   result, visibleAtReturn, access>>
Publish(c) ==
    /\ phase[c] = "starting" /\ owner = c
    /\ raw' = c /\ published' = published \cup {c} /\ owner' = 0
    /\ phase' = [phase EXCEPT ![c] = "done"] /\ result' = [result EXCEPT ![c] = <<"created",c>>]
    /\ visibleAtReturn' = [visibleAtReturn EXCEPT ![c] = c]
    /\ UNCHANGED <<request, oldState, expired, sweep, selector, lateCleanup, inspected, access>>
Wake(c) ==
    /\ phase[c] = "waiting" /\ owner = 0
    /\ phase' = [phase EXCEPT ![c] = "new"]
    /\ UNCHANGED <<request, raw, owner, published, oldState, expired, sweep, selector,
                   lateCleanup, inspected, result, visibleAtReturn, access>>
DropPin(c) ==
    /\ phase[c] = "pinned" /\ phase' = [phase EXCEPT ![c] = "released"]
    /\ UNCHANGED <<request, raw, owner, published, oldState, expired, sweep, selector,
                   lateCleanup, inspected, result, visibleAtReturn, access>>
Age ==
    /\ ~expired /\ expired' = TRUE
    /\ UNCHANGED <<request, phase, raw, owner, published, oldState, sweep, selector,
                   lateCleanup, inspected, result, visibleAtReturn, access>>
SelectIfEligible(who) ==
    IF expired /\ oldState = "live" /\ (OldPins = {} \/ Fault = "ignore-pin")
    THEN /\ oldState' = "selected" /\ selector' = who
    ELSE UNCHANGED <<oldState, selector>>
Sweep ==
    /\ expired /\ sweep = "pending"
    /\ sweep' = IF OldPins # {} THEN "sawPin" ELSE "ran"
    /\ SelectIfEligible(Sweeper)
    /\ UNCHANGED <<request, phase, raw, owner, published, expired, lateCleanup,
                   inspected, result, visibleAtReturn, access>>
Enforce(c) ==
    /\ phase[c] = "released" /\ phase' = [phase EXCEPT ![c] = "returning"]
    /\ SelectIfEligible(c)
    /\ UNCHANGED <<request, raw, owner, published, expired, sweep, lateCleanup,
                   inspected, result, visibleAtReturn, access>>
Cleanup ==
    /\ oldState = "selected" /\ oldState' = "cleaned"
    /\ lateCleanup' = (published # {})
    /\ raw' = IF raw = Old \/ Fault = "erase-by-key" THEN 0 ELSE raw
    /\ UNCHANGED <<request, phase, owner, published, expired, sweep, selector,
                   inspected, result, visibleAtReturn, access>>
Return(c) ==
    /\ phase[c] = "returning" /\ (selector # c \/ oldState = "cleaned")
    /\ LET g == IF Fault = "reread" /\ Visible # 0 THEN Visible ELSE inspected[c]
       IN result' = [result EXCEPT ![c] = Replay(c,g)]
    /\ visibleAtReturn' = [visibleAtReturn EXCEPT ![c] = Visible]
    /\ phase' = [phase EXCEPT ![c] = "done"]
    /\ UNCHANGED <<request, raw, owner, published, oldState, expired, sweep, selector,
                   lateCleanup, inspected, access>>
\* First ordinary lookup of a returned old handle after replacement publication.
UseOldHandle(c) ==
    /\ result[c] = <<"existing",Old>> /\ Visible \in Callers /\ access[c] = <<>>
    /\ LET target == IF Fault = "follow-key" THEN Visible ELSE result[c][2]
       IN access' = [access EXCEPT ![c] = IF target = Old /\ oldState # "live"
                          THEN <<"missing",target>> ELSE <<"found",target>>]
    /\ UNCHANGED <<request, phase, raw, owner, published, oldState, expired, sweep,
                   selector, lateCleanup, inspected, result, visibleAtReturn>>
Step == Age \/ Sweep \/ Cleanup \/ (\E c \in Callers :
    Inspect(c) \/ DropPin(c) \/ Enforce(c) \/ Return(c) \/ Publish(c) \/ Wake(c) \/ UseOldHandle(c))
Next == Step \/ (~ENABLED Step /\ UNCHANGED vars)
Spec == Init /\ [][Next]_vars

PinnedBinding == OldPins # {} => (oldState = "live" /\ Visible = Old)
BoundGeneration == /\ (oldState = "live" => raw = Old)
                   /\ (published # {} => Visible \in published)
SingleReplacement == Cardinality(published) <= 1
ResultIdentity == \A c \in Callers : phase[c] = "done" =>
    CASE inspected[c] = 0 -> (c \in published /\ result[c] = <<"created",c>>)
      [] OTHER -> IF request[c] = FP(inspected[c])
                  THEN result[c] = <<"existing",inspected[c]>>
                  ELSE result[c] = <<"conflict",FP(inspected[c])>>
HandleIdentity == \A c \in Callers : access[c] \in {<<>>, <<"missing",Old>>}
Eligible == oldState = "live" /\ expired /\ OldPins = {}
EnforcementSelects == [][(Eligible /\ (Sweep \/ (\E c \in Callers : Enforce(c)))) =>
                         oldState' = "selected"]_vars

NoPinnedReturn == ~(\E c \in Callers : sweep = "sawPin" /\ selector = c
    /\ visibleAtReturn[c] \in Callers /\ result[c] = <<"existing",Old>> /\ access[c] = <<"missing",Old>>)
NoLateConflict == ~(\E c \in Callers : selector = c /\ visibleAtReturn[c] \in Callers
    /\ FP(visibleAtReturn[c]) = request[c] /\ result[c] = <<"conflict","x">>)
NoCleanupAfterPublish == ~lateCleanup
NoReplacementReplay == ~(\E c \in Callers : selector = Sweeper /\ inspected[c] \in Callers
    /\ result[c] = <<"existing",inspected[c]>>)
NoReplacementConflict == ~(\E c \in Callers : selector = Sweeper /\ inspected[c] \in Callers
    /\ result[c] = <<"conflict","y">> /\ request[c] = "x")
=============================================================================
