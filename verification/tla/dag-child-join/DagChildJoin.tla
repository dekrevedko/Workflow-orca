---------------------------- MODULE DagChildJoin ----------------------------
EXTENDS Naturals, Sequences
CONSTANT Mode
VARIABLES cause, barrier, admitted, history, row, attempt, changedOutput, faulted
vars == <<cause, barrier, admitted, history, row, attempt, changedOutput, faulted>>
Init ==
    /\ cause = "none" /\ barrier = "none" /\ admitted = {"A","B"} /\ history = <<>>
    /\ row = "absent" /\ attempt = 0 /\ changedOutput = FALSE /\ faulted = FALSE

\* Only the reference can use cancellation provenance. The source receives status/output.
ChildTerminal(origin) ==
    /\ cause = "none" /\ (origin # "dag" \/ barrier # "none")
    /\ cause' = origin /\ row' = "ready"
    /\ UNCHANGED <<barrier, admitted, history, attempt, changedOutput, faulted>>
ParentCancel ==
    /\ barrier = "none" /\ ~faulted
    /\ barrier' = CASE cause = "none" -> "beforeChild"
                    [] history = <<>> -> "beforeFirstJoin"
                    [] row = "lostAck" -> "afterLostAck"
                    [] OTHER -> "afterJoin"
    /\ UNCHANGED <<cause, admitted, history, row, attempt, changedOutput, faulted>>
Claim ==
    /\ row \in {"ready","expired"} /\ attempt < 2
    /\ attempt' = attempt + 1 /\ row' = "claimed"
    /\ UNCHANGED <<cause, barrier, admitted, history, changedOutput, faulted>>

Fact == IF cause = "success" THEN IF changedOutput THEN "output43" ELSE "output42"
        ELSE "cancelled"
SourceOutcome == IF Fact # "cancelled" THEN Fact
                 ELSE IF barrier = "none" THEN "failedChildCancelled" ELSE "nodeCancelled"
CausalOutcome == CASE cause = "independent" -> "failedChildCancelled"
                   [] cause = "dag" -> "nodeCancelled"
                   [] OTHER -> Fact
FirstJoin ==
    /\ row = "claimed" /\ history = <<>>
    /\ history' = Append(history, IF Mode = "source" THEN SourceOutcome ELSE CausalOutcome)
    /\ admitted' = admitted \ {"A"} /\ row' = "handled"
    /\ UNCHANGED <<cause, barrier, attempt, changedOutput, faulted>>

\* Both cancellation outcomes correspond to a terminal Cancelled child.
\* Replay validates the fact against the saved join; a different output is a mismatch.
SameFact(saved) == IF saved \in {"failedChildCancelled","nodeCancelled"}
                   THEN Fact = "cancelled" ELSE Fact = saved
Replay ==
    /\ row = "claimed" /\ Len(history) > 0
    /\ LET valid == CASE Mode = "source" -> SourceOutcome = history[1]
                      [] Mode = "trustReplay" -> TRUE
                      [] OTHER -> SameFact(history[1])
       IN /\ row' = IF valid THEN "handled" ELSE "invalid"
          /\ history' = IF valid /\ Mode = "reappend" THEN Append(history,history[1]) ELSE history
    /\ UNCHANGED <<cause, barrier, admitted, attempt, changedOutput, faulted>>
Disposition(before,after) ==
    /\ row = before /\ row' = after
    /\ UNCHANGED <<cause, barrier, admitted, history, attempt, changedOutput, faulted>>
Ack == Disposition("handled","dispatched")
LoseAck == attempt = 1 /\ Disposition("handled","lostAck")
Expire == Disposition("lostAck","expired")
RecordFault ==
    /\ row = "invalid" /\ faulted' = TRUE /\ row' = "faultRecorded"
    /\ UNCHANGED <<cause, barrier, admitted, history, attempt, changedOutput>>
Poison == Disposition("faultRecorded","poisoned")
\* Integrity control only: replace the successful child's terminal output on storage.
Corrupt ==
    /\ cause = "success" /\ row = "lostAck" /\ ~changedOutput
    /\ changedOutput' = TRUE
    /\ UNCHANGED <<cause, barrier, admitted, history, row, attempt, faulted>>

Delivery == Claim \/ FirstJoin \/ Replay \/ Ack \/ Expire \/ RecordFault \/ Poison
Step == Delivery \/ ParentCancel \/ LoseAck \/ Corrupt
        \/ (\E origin \in {"independent","dag","success"} : ChildTerminal(origin))
Next == Step \/ (~ENABLED Step /\ UNCHANGED vars)
Spec == Init /\ [][Next]_vars
FairSpec == Spec /\ WF_vars(Delivery)

\* Immutable cancellation cause is the test oracle, including when join is delayed.
FirstJoinClassified == history # <<>> =>
    CASE cause = "independent" -> history[1] = "failedChildCancelled"
      [] cause = "dag" -> history[1] = "nodeCancelled"
      [] OTHER -> history[1] = "output42"
OneJoin == Len(history) <= 1
AcknowledgedClassification == row = "dispatched" => FirstJoinClassified
Admission == admitted = IF history = <<>> THEN {"A","B"} ELSE {"B"}
ValidReplayDispatchedOrPending == ~changedOutput => row # "poisoned"
FaultRequiresConflict == faulted => changedOutput
ConflictNeverAcknowledged == changedOutput => row # "dispatched"
DispositionOrder == /\ (row = "poisoned" => faulted)
                    /\ (row = "dispatched" => Len(history) = 1)
Settles == (cause # "none") ~> (row \in {"poisoned","dispatched"})

\* Restrict each source diagnostic to the requested ordering for that question.
LostAckOrder == barrier \in {"none","afterLostAck"}
DelayedFirstOrder == barrier \in {"none","beforeFirstJoin"}
NoRecoveredReplay == ~(barrier = "afterLostAck" /\ cause = "independent" /\ attempt = 2
                        /\ row = "dispatched" /\ history = <<"failedChildCancelled">>)
NoDelayedFirst == ~(barrier = "beforeFirstJoin" /\ cause = "independent" /\ attempt = 1
                     /\ row = "dispatched" /\ history = <<"failedChildCancelled">>)
NoDagCausedJoin == ~(cause = "dag" /\ row = "dispatched" /\ history = <<"nodeCancelled">>)
NoRejectedConflict == ~(changedOutput /\ row = "poisoned" /\ faulted /\ history = <<"output42">>)
=============================================================================
