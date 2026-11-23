---------------------------- MODULE DagChildJoin ----------------------------
EXTENDS Naturals, Sequences
CONSTANT Mode
Unread == [value |-> "unread", provenance |-> "none"]
Origins == {"Independent", "OwningDag"}
CancelledJoins == {"failedChildCancelled", "nodeCancelled"}

\* requests is a ghost log of caller identities in request order. Production reads
\* only origin, the evidence stored with the first accepted cancellation request.
VARIABLES requests, origin, child, barrier, admitted, history, row, attempt,
          lostAck, changedOutput, faulted, process, observed, crashAt
childState == <<requests, origin, child>>
parentState == <<barrier, admitted, history>>
transport == <<row, attempt, lostAck, changedOutput, faulted>>
worker == <<process, observed, crashAt>>
vars == <<childState, parentState, transport, worker>>

Winner == IF Len(requests) = 0 THEN "none" ELSE requests[1]
Deliverable == child \in {"cancelled", "success"}
InitialWorker == /\ process = "original" /\ observed = Unread /\ crashAt = "none"
Init ==
    /\ requests = <<>> /\ origin = "none" /\ child = "running"
    /\ barrier = "none" /\ admitted = {"A", "B"} /\ history = <<>>
    /\ row = "absent" /\ attempt = 0 /\ lostAck = FALSE /\ changedOutput = FALSE /\ faulted = FALSE
    /\ InitialWorker

Request(from) ==
    /\ process # "down" /\ child # "success"
    /\ (from # "OwningDag" \/ barrier # "none")
    /\ \A i \in 1..Len(requests) : requests[i] # from
    /\ requests' = Append(requests, from)
    /\ origin' = IF child = "running" \/ (Mode = "overwriteOrigin" /\ child = "cancelling")
                 THEN from ELSE origin
    /\ child' = IF child = "running" THEN "cancelling" ELSE child
    /\ UNCHANGED <<parentState, transport, worker>>

ChildTerminal ==
    /\ process # "down" /\ child \in {"running", "cancelling"}
    /\ child' = IF child = "running" THEN "success" ELSE "cancelled"
    /\ row' = "ready"
    /\ UNCHANGED <<requests, origin, parentState, attempt, lostAck, changedOutput, faulted, worker>>

ParentCancel ==
    /\ barrier = "none" /\ ~faulted
    /\ barrier' = CASE ~Deliverable -> "beforeTerminal"
                    [] history = <<>> -> "beforeFirstJoin"
                    [] row = "lostAck" -> "afterLostAck"
                    [] OTHER -> "afterJoin"
    /\ UNCHANGED <<childState, admitted, history, transport, worker>>

\* A crash drops the local observation. An in-flight claim must expire, including
\* a join committed before the worker could acknowledge it. Bound to one crash.
Crash ==
    /\ process = "original" /\ row \in {"absent", "ready", "claimed", "handled", "lostAck"}
    /\ (row = "handled" => ~lostAck)
    /\ process' = "down" /\ observed' = Unread
    /\ crashAt' = CASE child = "cancelling" -> "pendingRequest"
                    [] row = "claimed" /\ observed # Unread -> "afterRead"
                    [] OTHER -> row
    /\ row' = CASE row = "claimed" -> "lostClaim"
               [] row = "handled" -> "lostAck"
               [] OTHER -> row
    /\ lostAck' = (lostAck \/ row = "handled")
    /\ UNCHANGED <<childState, parentState, attempt, changedOutput, faulted>>

Restart ==
    /\ process = "down" /\ process' = "restarted"
    /\ UNCHANGED <<childState, parentState, transport, observed, crashAt>>

Claim ==
    /\ process # "down" /\ row \in {"ready", "expired"}
    /\ attempt' = attempt + 1 /\ row' = "claimed"
    /\ UNCHANGED <<childState, parentState, lostAck, changedOutput, faulted, worker>>

CurrentFact == [value |-> IF child = "success"
                          THEN IF changedOutput THEN "output43" ELSE "output42"
                          ELSE "cancelled",
                provenance |-> origin]
ReadChild ==
    /\ process # "down" /\ row = "claimed" /\ observed = Unread
    /\ observed' = CurrentFact
    /\ UNCHANGED <<childState, parentState, transport, process, crashAt>>

\* The independent reference knows the actual request order. Neither the first
\* source join nor the source replay inspector reads that ghost information.
ReferenceOutcome == CASE child = "success" -> "output42"
                      [] Winner = "Independent" -> "failedChildCancelled"
                      [] OTHER -> "nodeCancelled"
BarrierOutcome == IF observed.value # "cancelled" THEN observed.value
                  ELSE IF barrier = "none" THEN "failedChildCancelled" ELSE "nodeCancelled"
SourceOutcome == CASE observed.value # "cancelled" -> observed.value
                   [] observed.provenance = "Independent" -> "failedChildCancelled"
                   [] observed.provenance = "OwningDag" -> "nodeCancelled"
                   [] barrier = "none" -> "failedChildCancelled"
                   [] OTHER -> "unknown"
FirstJoin ==
    /\ row = "claimed" /\ observed # Unread /\ history = <<>>
    /\ LET outcome == CASE Mode = "reference" -> ReferenceOutcome
                        [] Mode = "barrierFirst" -> BarrierOutcome
                        [] OTHER -> SourceOutcome
       IN /\ history' = IF outcome = "unknown" THEN history ELSE Append(history, outcome)
          /\ admitted' = IF outcome = "unknown" THEN admitted ELSE admitted \ {"A"}
          /\ row' = IF outcome = "unknown" THEN "invalid" ELSE "handled"
    /\ observed' = Unread
    /\ UNCHANGED <<childState, barrier, attempt, lostAck, changedOutput, faulted, process, crashAt>>

ReplayMatches(saved) ==
    CASE Mode = "trustReplay" -> TRUE
      [] Mode = "barrierReplay" -> saved = BarrierOutcome
      [] Mode = "reference" -> /\ saved = ReferenceOutcome
                                /\ (child # "success" \/ ~changedOutput)
      [] observed.value = "cancelled" /\ observed.provenance = "none" -> saved \in CancelledJoins
      [] OTHER -> saved = SourceOutcome
Replay ==
    /\ row = "claimed" /\ observed # Unread /\ history # <<>>
    /\ LET valid == ReplayMatches(history[1])
       IN /\ row' = IF valid THEN "handled" ELSE "invalid"
          /\ history' = IF valid /\ Mode = "reappend" THEN Append(history, history[1]) ELSE history
    /\ observed' = Unread
    /\ UNCHANGED <<childState, barrier, admitted, attempt, lostAck, changedOutput, faulted, process, crashAt>>

Disposition(before, after) ==
    /\ row = before /\ row' = after
    /\ UNCHANGED <<childState, parentState, attempt, lostAck, changedOutput, faulted, worker>>
Ack == process # "down" /\ Disposition("handled", "dispatched")
LoseAck ==
    /\ row = "handled" /\ ~lostAck /\ row' = "lostAck" /\ lostAck' = TRUE
    /\ UNCHANGED <<childState, parentState, attempt, changedOutput, faulted, worker>>
Expire == Disposition("lostAck", "expired") \/ Disposition("lostClaim", "expired")
RecordFault ==
    /\ row = "invalid" /\ faulted' = TRUE /\ row' = "faultRecorded"
    /\ UNCHANGED <<childState, parentState, attempt, lostAck, changedOutput, worker>>
Poison == Disposition("faultRecorded", "poisoned")
Corrupt ==
    /\ child = "success" /\ row = "lostAck" /\ ~changedOutput
    /\ changedOutput' = TRUE
    /\ UNCHANGED <<childState, parentState, row, attempt, lostAck, faulted, worker>>

Delivery == Restart \/ Claim \/ ReadChild \/ FirstJoin \/ Replay \/ Ack \/ Expire \/ RecordFault \/ Poison
Step == Delivery \/ ParentCancel \/ Crash \/ LoseAck \/ Corrupt \/ ChildTerminal \/ (\E from \in Origins : Request(from))
Next == Step \/ (~ENABLED Step /\ UNCHANGED vars)
Spec == Init /\ [][Next]_vars
FairSpec == Spec /\ WF_vars(Delivery)

WinningRequestPersisted == origin = Winner
FirstJoinClassified == history # <<>> => history[1] = ReferenceOutcome
OneJoin == Len(history) <= 1
AcknowledgedClassification == row = "dispatched" => FirstJoinClassified
Admission == admitted = IF history = <<>> THEN {"A", "B"} ELSE {"B"}
ValidReplayDispatchedOrPending == ~changedOutput => row # "poisoned"
FaultRequiresConflict == faulted => changedOutput
ConflictNeverAcknowledged == changedOutput => row # "dispatched"
DispositionOrder == /\ (row = "poisoned" => faulted)
                    /\ (row = "dispatched" => Len(history) = 1)
Settles == Deliverable ~> (row \in {"poisoned", "dispatched"})

LostAckOrder == barrier \in {"none", "afterLostAck"}
BothRequestsBeforeTerminal == Deliverable => Len(requests) = 2
DelayedFirstOrder == barrier \in {"none", "beforeFirstJoin"}
NoRecoveredReplay == ~(barrier = "afterLostAck" /\ Winner = "Independent" /\ attempt = 2
                        /\ crashAt = "lostAck" /\ row = "dispatched" /\ history = <<"failedChildCancelled">>)
NoDelayedFirst == ~(barrier = "beforeFirstJoin" /\ Winner = "Independent" /\ attempt = 1
                     /\ crashAt = "ready" /\ row = "dispatched" /\ history = <<"failedChildCancelled">>)
NoDagCausedJoin == ~(requests = <<"OwningDag", "Independent">> /\ crashAt = "pendingRequest"
                      /\ row = "dispatched" /\ history = <<"nodeCancelled">>)
NoIndependentWinner == ~(requests = <<"Independent", "OwningDag">> /\ crashAt = "pendingRequest"
                          /\ row = "dispatched" /\ history = <<"failedChildCancelled">>)
NoFreshRead == ~(crashAt = "afterRead" /\ attempt = 2 /\ process = "restarted"
                  /\ row = "dispatched" /\ history = <<"failedChildCancelled">>)
NoRejectedConflict == ~(changedOutput /\ row = "poisoned" /\ faulted /\ history = <<"output42">>)

\* A legacy terminal has a winning request but no recoverable origin. Independently
\* seed old joined records: either legal cancellation shape can already be durable.
LegacyFirstInit ==
    /\ requests = <<"Independent">> /\ origin = "none" /\ child = "cancelled"
    /\ barrier \in {"none", "beforeFirstJoin"}
    /\ admitted = {"A", "B"} /\ history = <<>>
    /\ row = "ready" /\ attempt = 0 /\ lostAck = FALSE /\ changedOutput = FALSE /\ faulted = FALSE
    /\ InitialWorker
LegacyReplayInit ==
    /\ requests = <<"Independent">> /\ origin = "none" /\ child = "cancelled"
    /\ barrier = "afterLostAck" /\ admitted = {"B"}
    /\ history \in {<<"failedChildCancelled">>, <<"nodeCancelled">>, <<"output42">>}
    /\ row = "lostAck" /\ attempt = 1 /\ lostAck = TRUE /\ changedOutput = FALSE /\ faulted = FALSE
    /\ InitialWorker
LegacyFirstSpec == LegacyFirstInit /\ [][Next]_vars /\ WF_vars(Delivery)
LegacyReplaySpec == LegacyReplayInit /\ [][Next]_vars /\ WF_vars(Delivery)
LegacyFirstDisposition ==
    /\ (barrier = "beforeFirstJoin" => history = <<>>)
    /\ (faulted => barrier = "beforeFirstJoin" /\ history = <<>>)
    /\ (row = "dispatched" => history = <<"failedChildCancelled">>)
LegacyReplayDisposition ==
    /\ (faulted => history = <<"output42">>)
    /\ (row = "dispatched" => history[1] \in CancelledJoins)
=============================================================================
