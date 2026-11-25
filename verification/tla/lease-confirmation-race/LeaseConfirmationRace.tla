----------------------- MODULE LeaseConfirmationRace -----------------------
EXTENDS Naturals

CONSTANT Mode
ASSUME Mode \in {"source", "historical", "doubleGrant", "dropUnit", "noRepair"}
Ids == {"A", "B"}
Other(i) == IF i = "A" THEN "B" ELSE "A"
None == "none"

\* One quarantined holder/token, one occupied unit and one admitted FIFO waiter.
\* Workflow and governance are independent durable streams. The variables for
\* release evidence, ID binding, workflow identity and ticket counts are distinct.
VARIABLES receipt, bindings, releases, holderUnits, waiterUnits, queued, grants,
          workflow, version, caller, repair, peerAtCrash
governance == <<receipt, bindings, releases, holderUnits, waiterUnits, queued, grants>>
checkpoint == <<workflow, version>>
vars == <<governance, checkpoint, caller, repair, peerAtCrash>>

NewCall == [pc |-> "readEvidence", seen |-> None, expected |-> 0, publish |-> None,
            outcome |-> None, path |-> None, restarted |-> FALSE, conflicted |-> FALSE,
            peerFirst |-> FALSE, releasedByThisCall |-> FALSE]
Init ==
    /\ receipt = None /\ bindings = {} /\ releases = 0
    /\ holderUnits = 1 /\ waiterUnits = 0 /\ queued = TRUE /\ grants = 0
    /\ workflow = None /\ version = 0
    /\ caller = [i \in Ids |-> NewCall]
    /\ repair = "idle" /\ peerAtCrash = None

ReadEvidence(i) ==
    /\ caller[i].pc = "readEvidence"
    /\ caller' = [caller EXCEPT ![i].seen = receipt, ![i].pc = "readWorkflow",
                      ![i].peerFirst = (caller[Other(i)].pc = "govern" /\ receipt = None)]
    /\ UNCHANGED <<governance, checkpoint, repair, peerAtCrash>>

ConfirmationResult(i) == IF caller[i].releasedByThisCall THEN "Released" ELSE "AlreadyConfirmed"

ReadWorkflow(i) ==
    /\ caller[i].pc = "readWorkflow"
    /\ caller' =
        IF workflow # None THEN
            [caller EXCEPT ![i].pc = "done", ![i].outcome = ConfirmationResult(i),
                          ![i].path = "closed"]
        ELSE IF caller[i].seen # None THEN
            [caller EXCEPT ![i].pc = "append", ![i].expected = version,
                          ![i].publish = caller[i].seen, ![i].path = "retained"]
        ELSE
            [caller EXCEPT ![i].pc = "govern", ![i].expected = version]
    /\ UNCHANGED <<governance, checkpoint, repair, peerAtCrash>>

\* The serialized provider append binds an unused ID, retains release evidence,
\* releases the exact holder and promotes the admissible waiter in one batch.
WinGovernance(i) ==
    /\ caller[i].pc = "govern" /\ receipt = None /\ holderUnits = 1 /\ queued
    /\ receipt' = i /\ bindings' = bindings \cup {i} /\ releases' = releases + 1
    /\ holderUnits' = holderUnits - 1
    /\ waiterUnits' = IF Mode = "dropUnit" THEN waiterUnits ELSE waiterUnits + 1
    /\ queued' = FALSE /\ grants' = grants + 1
    /\ caller' = [caller EXCEPT ![i].pc = "append", ![i].publish = i,
                                ![i].path = "winner", ![i].releasedByThisCall = TRUE]
    /\ UNCHANGED <<checkpoint, repair, peerAtCrash>>

AlreadyConfirmed(i) ==
    /\ caller[i].pc = "govern" /\ receipt # None
    /\ caller' =
        IF Mode = "historical" THEN
            [caller EXCEPT ![i].path = "duplicate", ![i].pc = "append", ![i].publish = i]
        ELSE
            [caller EXCEPT ![i].path = "duplicate", ![i].pc = "readWinner"]
    /\ grants' = IF Mode = "doubleGrant" THEN grants + 1 ELSE grants
    /\ waiterUnits' = IF Mode = "doubleGrant" THEN waiterUnits + 1 ELSE waiterUnits
    /\ UNCHANGED <<receipt, bindings, releases, holderUnits, queued, checkpoint, repair, peerAtCrash>>

\* Read the exact token's retained governance receipt. Publication still uses
\* the expected workflow version captured before the provider call.
ReadWinner(i) ==
    /\ caller[i].pc = "readWinner"
    /\ caller' = [caller EXCEPT ![i].publish = receipt, ![i].pc = "append"]
    /\ UNCHANGED <<governance, checkpoint, repair, peerAtCrash>>

Append(i) ==
    /\ caller[i].pc = "append"
    /\ IF caller[i].expected = version THEN
           /\ workflow' = caller[i].publish /\ version' = version + 1
           /\ caller' = [caller EXCEPT ![i].pc = "done",
                  ![i].outcome = ConfirmationResult(i)]
       ELSE
           /\ UNCHANGED checkpoint
           /\ caller' = [caller EXCEPT ![i] = [NewCall EXCEPT !.restarted = caller[i].restarted,
                                      !.conflicted = TRUE,
                                      !.releasedByThisCall = Mode # "historical" /\ caller[i].releasedByThisCall]]
    /\ UNCHANGED <<governance, repair, peerAtCrash>>

\* Lose the winner after the governance transaction and before its workflow
\* append. A retry uses the same request ID with fresh local state.
KillWinner(i) ==
    /\ caller[i].pc = "append" /\ caller[i].path = "winner"
    /\ caller' = [caller EXCEPT ![i] = [NewCall EXCEPT !.pc = "killed"]]
    /\ peerAtCrash' = IF caller[i].peerFirst /\ caller[Other(i)].pc = "govern"
                      THEN "prechecked" ELSE caller[Other(i)].pc
    /\ UNCHANGED <<governance, checkpoint, repair>>

Restart(i) ==
    /\ caller[i].pc = "killed"
    /\ caller' = [caller EXCEPT ![i] = [NewCall EXCEPT !.restarted = TRUE]]
    /\ UNCHANGED <<governance, checkpoint, repair, peerAtCrash>>

\* Recovery scans the nonreleased workflow, then obtains its token's retained
\* receipt. A concurrent append advances version and makes the recovery CAS fail.
\* Version zero is the only open checkpoint in this bound.
RepairRead ==
    /\ repair = "idle" /\ workflow = None /\ receipt # None
    /\ repair' = receipt
    /\ UNCHANGED <<governance, checkpoint, caller, peerAtCrash>>
RepairAppend ==
    /\ repair \in Ids
    /\ IF version = 0 THEN /\ workflow' = repair /\ version' = version + 1
       ELSE UNCHANGED checkpoint
    /\ repair' = "done"
    /\ UNCHANGED <<governance, caller, peerAtCrash>>
Repair == Mode # "noRepair" /\ (RepairRead \/ RepairAppend)

Next == (\E i \in Ids : ReadEvidence(i) \/ ReadWorkflow(i) \/ WinGovernance(i) \/
                         AlreadyConfirmed(i) \/ ReadWinner(i) \/ Append(i) \/ KillWinner(i) \/ Restart(i))
        \/ Repair
Spec == Init /\ [][Next]_vars /\ WF_vars(Repair)

TypeOK ==
    /\ receipt \in Ids \cup {None} /\ bindings \subseteq Ids /\ releases \in Nat
    /\ holderUnits \in Nat /\ waiterUnits \in Nat /\ queued \in BOOLEAN /\ grants \in Nat
    /\ workflow \in Ids \cup {None} /\ version \in 0..1
    /\ (version = 0) = (workflow = None)
    /\ repair \in Ids \cup {"idle", "done"}

CapacityConserved == holderUnits + waiterUnits = 1
ExactlyOneTransfer ==
    /\ releases <= 1 /\ grants <= 1 /\ grants = releases
    /\ (receipt = None) = (releases = 0)
    /\ (queued = (grants = 0)) /\ holderUnits = 1 - releases /\ waiterUnits = grants
BindingMatchesReceipt == bindings = (IF receipt = None THEN {} ELSE {receipt})
WorkflowAgrees == workflow # None => workflow = receipt /\ workflow \in bindings
ResponseMatches ==
    \A i \in Ids : caller[i].outcome # None =>
        /\ workflow # None
        /\ (caller[i].outcome = "Released" => receipt = i)
        /\ (caller[i].outcome = "AlreadyConfirmed" => receipt # None)
\* The durable winner and crash history independently determine which invocation
\* can report Released. A new call with the same winning ID is a replay.
CompletedCallStatus ==
    \A i \in Ids : caller[i].pc = "done" =>
        caller[i].outcome = (IF receipt = i /\ ~caller[i].restarted THEN "Released" ELSE "AlreadyConfirmed")
GapCloses == (receipt # None) ~> (workflow = receipt)

\* Focus the historical invariant on the requested schedule without restricting
\* the transition relation. Other profiles explore both callers and all orders.
CrashedStaleAgreement ==
    (peerAtCrash = "prechecked" /\ caller["B"].path = "duplicate" /\ caller["B"].pc = "done")
        => WorkflowAgrees
PeerPublishedWinner(i) ==
    /\ caller[i].pc = "killed" /\ caller[Other(i)].outcome = "AlreadyConfirmed"
    /\ workflow = i /\ receipt = i /\ bindings = {i}
    /\ releases = 1 /\ grants = 1 /\ holderUnits = 0 /\ waiterUnits = 1 /\ ~queued
StaleRepair(i) ==
    peerAtCrash = "prechecked" /\ caller[Other(i)].path = "duplicate" /\ PeerPublishedWinner(i)
NoStaleRepair == ~StaleRepair("A")
NoStaleBWinnerRepair == ~StaleRepair("B")
NoFreshRepair == ~(peerAtCrash = "readEvidence" /\ caller["B"].path = "retained" /\
                   PeerPublishedWinner("A"))
NoSameIdRetry == ~(caller["A"].restarted /\ caller["A"].path = "retained" /\
                    caller["A"].outcome = "AlreadyConfirmed" /\ workflow = "A")
NoGapRecovery == ~(caller["A"].pc = "killed" /\ caller["B"].pc = "readEvidence" /\
                    repair = "done" /\ workflow = "A")
NoCasRetry == ~(caller["B"].conflicted /\ caller["B"].outcome = "AlreadyConfirmed")
NoLiveWinnerCasRetry == ~(receipt = "A" /\ workflow = "A" /\ caller["B"].path = "duplicate" /\
                          caller["B"].outcome = "AlreadyConfirmed" /\ caller["A"].conflicted /\
                          ~caller["A"].restarted /\ caller["A"].outcome = "Released")
NoBWinner == ~(receipt = "B" /\ workflow = "B" /\ caller["B"].outcome = "Released")
NoPersistentSplit == ~(peerAtCrash = "prechecked" /\ workflow = "B" /\ receipt = "A" /\
                       caller["A"].restarted /\ caller["A"].outcome = "AlreadyConfirmed")
=============================================================================
