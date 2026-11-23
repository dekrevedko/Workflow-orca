----------------------- MODULE EphemeralWaitOwnership -----------------------
EXTENDS Naturals, FiniteSets, Sequences, TLC, WaitOwnershipContract
CONSTANTS Fault, Isolation, Witness, RollbackMode
Instances == {1,2}
Slots == {1,2}
Events == {1,2}
Occ == Instances \X Slots
Key(i) == <<IF Isolation = "definition" THEN i ELSE 1,
            IF Isolation = "name" THEN i ELSE 1,
            IF Isolation = "version" THEN i ELSE 1,
            IF Isolation = "correlation" THEN i ELSE 1>>
Keys == {Key(i) : i \in Instances}
VARIABLE s
vars == <<s>>
Active(i) == {o \in Occ : o[1] = i /\ s.phase[o] = "active"}
Busy(i) == {o \in Occ : o[1] = i /\ s.phase[o] \in {"event","timer","settled"}}
Live(i) == {o \in s.held : o[1] = i}
Available(o) == s.phase[o] = "new" /\ ~s.cancel[o[1]] /\
  s.index[Key(o[1])] \in {0,o[1]} /\
  (Active(o[1]) = {} \/ Fault = "same-instance")
Hit(tag) == s.seen \/ Witness = tag

InitialState == [phase |-> [o \in Occ |-> "new"], held |-> {},
  index |-> [k \in Keys |-> 0], count |-> [k \in Keys |-> 0],
  wins |-> [o \in Occ |-> 0], event |-> [o \in Occ |-> 0],
  consumed |-> [i \in Instances |-> [e \in Events |-> 0]],
  attempts |-> [i \in Instances |-> [e \in Events |-> 0]],
  failures |-> [o \in Occ |-> 0], restored |-> {},
  timer |-> [o \in Occ |-> TRUE], matchKey |-> [o \in Occ |-> Key(o[1])],
  route |-> [i \in Instances |-> "idle"], offered |-> [i \in Instances |-> 0],
  offerKey |-> [i \in Instances |-> Key(i)],
  observed |-> [i \in Instances |-> FALSE], published |-> [i \in Instances |-> FALSE],
  queue |-> [i \in Instances |-> <<>>], cancel |-> [i \in Instances |-> FALSE],
  cancelStage |-> [i \in Instances |-> "none"], cleaned |-> [i \in Instances |-> FALSE],
  due |-> {}, dispatched |-> {}, callbackBefore |-> <<>>, callbackAfter |-> <<>>,
  returned |-> [i \in Instances |-> FALSE],
  seen |-> FALSE]

Init == s = InitialState

\* Register includes the per-instance active guard and the routingGate reserve.
\* The physical index update is a single lock operation, not a global engine turn.
Register(o) ==
  /\ Available(o) /\ Busy(o[1]) = {}
  /\ s' = [s EXCEPT !.phase[o] = "active", !.held = @ \cup {o},
       !.index[Key(o[1])] = o[1], !.count[Key(o[1])] = @ + 1,
       !.published[o[1]] = TRUE,
       !.seen = Hit(IF \E a \in Occ : s.wins[a] = 1 /\ Key(a[1]) = Key(o[1])
                    THEN "reuse" ELSE IF Isolation # "none" /\ s.held # {}
                    THEN "isolation" ELSE "registered")]

Reject(o) ==
  /\ s.phase[o] = "new" /\ ~Available(o) /\ ~s.cancel[o[1]]
  /\ Busy(o[1]) = {}
  /\ s' = [s EXCEPT !.cancel[o[1]] = TRUE,
       !.seen = Hit(IF Active(o[1]) # {} THEN "same-instance" ELSE "contention")]

\* Separate lookup/snapshot and enqueue stages; published observation may go stale.
Lookup(i,e,k) ==
  /\ s.route[i] = "idle" /\ s.attempts[i][e] < 2
  /\ s' = [s EXCEPT !.route[i] = "lookup", !.offered[i] = e,
       !.offerKey[i] = k, !.observed[i] = s.index[k] = i,
       !.attempts[i][e] = @ + 1]
Observe(i) ==
  /\ s.route[i] = "lookup"
  /\ s' = [s EXCEPT !.route[i] = "observed", !.observed[i] = @ /\ s.published[i]]
Enqueue(i) ==
  /\ s.route[i] = "observed"
  /\ s' = [s EXCEPT !.route[i] = "idle",
       !.queue[i] = IF s.observed[i] THEN Append(@, <<s.offered[i],s.offerKey[i]>>) ELSE @]

Deliver(i) ==
  /\ Busy(i) = {} /\ Len(s.queue[i]) > 0
  /\ LET e == Head(s.queue[i])[1]
         k == Head(s.queue[i])[2] IN
     IF s.consumed[i][e] > 0 \/ Active(i) = {} \/ s.cancel[i] \/ Key(i) # k
     THEN s' = [s EXCEPT !.queue[i] = Tail(@),
          !.seen = Hit(IF s.consumed[i][e] > 0 THEN "duplicate"
              ELSE IF \E o \in Occ : o[1] = i /\ s.phase[o] = "done" /\ s.event[o] = 0 /\ s.wins[o] = 1
              THEN "timer-first" ELSE "unmatched")]
     ELSE \E o \in Active(i) :
       s' = [s EXCEPT !.phase[o] = "event", !.event[o] = e, !.matchKey[o] = k,
          !.restored = @ \ {o}, !.queue[i] = Tail(@), !.returned[i] = FALSE,
          !.count[Key(i)] = IF Fault = "early-release" THEN @ - 1 ELSE @,
          !.index[Key(i)] = IF Fault = "early-release" /\ s.count[Key(i)] = 1 THEN 0 ELSE @,
          !.seen = s.seen]

QueueTimer(o) ==
  /\ s.phase[o] = "active" /\ o \notin s.due
  /\ s' = [s EXCEPT !.due = @ \cup {o}]

Timer(o) ==
  /\ s.phase[o] = "active" /\ Busy(o[1]) = {} /\ s.timer[o] /\ ~s.cancel[o[1]]
  /\ o \in s.due /\ o \notin s.dispatched
  /\ s' = [s EXCEPT !.phase[o] = "timer", !.event[o] = 0,
       !.restored = @ \ {o}, !.returned[o[1]] = FALSE, !.dispatched = @ \cup {o}]

\* A callback already claimed by the timer service retains the OLD occurrence identity.
StaleTimer(o) ==
  /\ o \in s.due /\ o \notin s.dispatched /\ s.phase[o] \in {"done","cancelled"}
  /\ Busy(o[1]) = {}
  /\ LET targets == Active(o[1])
         victim == IF targets = {} THEN o ELSE CHOOSE n \in targets : TRUE
         newPhase == IF Fault = "stale-target" /\ targets # {}
                     THEN [s.phase EXCEPT ![victim] = "cancelled"] ELSE s.phase
     IN s' = [s EXCEPT !.dispatched = @ \cup {o}, !.phase = newPhase,
          !.callbackBefore = <<s.phase,s.held,s.index,s.count,s.wins>>,
          !.callbackAfter = <<newPhase,s.held,s.index,s.count,s.wins>>,
          !.seen = Hit(IF s.phase[o] = "done" /\ s.event[o] # 0
                      THEN IF targets # {} THEN "stale-timer" ELSE "event-first"
                      ELSE "stale-cancel")]

\* Same-instance continuation can install its next wait before releasing the old reservation.
Successor(o,n) ==
  /\ s.phase[o] = "event" /\ n[1] = o[1] /\ n # o /\ Available(n)
  /\ s' = [s EXCEPT !.phase[n] = "active", !.held = @ \cup {n},
       !.count[Key(n[1])] = @ + 1, !.published[n[1]] = TRUE,
       !.seen = Hit("handover")]

Return(i) ==
  /\ Busy(i) # {} /\ ~s.returned[i]
  /\ s' = [s EXCEPT !.returned[i] = TRUE]

Escape(o) ==
  /\ s.phase[o] \in {"event","timer"} /\ s.returned[o[1]] /\ s.failures[o] < 1
  /\ LET successors == Active(o[1]) IN
     /\ (RollbackMode = "source" \/ successors = {})
     /\ s' = [s EXCEPT !.phase[o] = "active", !.failures[o] = @ + 1,
          !.restored = @ \cup {o}, !.returned[o[1]] = FALSE,
          !.dispatched = @ \ {o},
          !.consumed[o[1]] = IF Fault = "consume-on-failure" /\ s.event[o] # 0
                            THEN [@ EXCEPT ![s.event[o]] = 1] ELSE @,
          !.seen = Hit(IF successors # {} THEN "successor-failure" ELSE "rollback")]

Settle(o) ==
  /\ s.phase[o] \in {"event","timer"} /\ s.returned[o[1]]
  /\ s' = [s EXCEPT !.phase[o] = "settled", !.wins[o] = @ + 1,
       !.consumed[o[1]] = IF s.event[o] # 0 THEN [@ EXCEPT ![s.event[o]] = @ + 1] ELSE @,
       !.timer[o] = FALSE]
Release(o) ==
  /\ s.phase[o] = "settled"
  /\ s' = [s EXCEPT !.phase[o] = "done", !.held = @ \ {o},
       !.count[Key(o[1])] = IF Fault = "erase-successor" THEN 0 ELSE @ - 1,
       !.index[Key(o[1])] = IF Fault = "erase-successor" \/ s.count[Key(o[1])] = 1 THEN 0 ELSE @,
       !.published[o[1]] = Active(o[1]) # {}, !.returned[o[1]] = FALSE]

RequestCancel(i) ==
  /\ ~s.cancel[i]
  /\ s' = [s EXCEPT !.cancel[i] = TRUE,
       !.cancelStage[i] = IF Busy(i) # {} THEN "cancel-during" ELSE IF Live(i) # {} THEN "cancel-before" ELSE "empty"]
Cleanup(i) ==
  /\ s.cancel[i] /\ Busy(i) = {} /\ ~s.cleaned[i]
  /\ s' = [s EXCEPT !.phase = [o \in Occ |-> IF o \in Live(i) THEN "cancelled" ELSE s.phase[o]],
       !.wins = [o \in Occ |-> IF o \in Live(i) THEN s.wins[o]+1 ELSE s.wins[o]],
       !.timer = [o \in Occ |-> IF o \in Live(i) THEN FALSE ELSE s.timer[o]],
       !.held = @ \ Live(i), !.restored = @ \ Live(i),
       !.count[Key(i)] = IF Live(i) # {} THEN 0 ELSE @,
       !.index[Key(i)] = IF Live(i) # {} THEN 0 ELSE @, !.published[i] = FALSE,
       !.cleaned[i] = TRUE, !.seen = Hit(s.cancelStage[i])]
DualWinner(o) ==
  /\ Fault = "dual-winner" /\ s.phase[o] = "done" /\ s.event[o] # 0 /\ s.wins[o] = 1
  /\ s' = [s EXCEPT !.wins[o] = 2]

Work ==
  \/ \E o \in Occ : Register(o) \/ Reject(o) \/ QueueTimer(o) \/ Timer(o) \/ StaleTimer(o) \/ Escape(o) \/ Settle(o) \/ Release(o) \/ DualWinner(o)
  \/ \E o,n \in Occ : Successor(o,n)
  \/ \E i \in Instances : Observe(i) \/ Enqueue(i) \/ Deliver(i) \/ Return(i) \/ RequestCancel(i) \/ Cleanup(i)
  \/ \E i \in Instances, e \in Events, k \in Keys : Lookup(i,e,k)
\* A quiet state can wait forever for an external event. Busy continuations are not quiescent.
Quiescent == (\A i \in Instances : Busy(i) = {} /\ s.queue[i] = <<>> /\ s.route[i] = "idle") /\ UNCHANGED vars
Next == Work \/ Quiescent
Spec == Init /\ [][Next]_vars
InternalFairness ==
  /\ \A o \in Occ : WF_vars(Settle(o)) /\ WF_vars(Release(o))
  /\ \A i \in Instances : WF_vars(Cleanup(i)) /\ WF_vars(Deliver(i))
  /\ \A o \in Occ : WF_vars(Register(o))
ReturnFairness == \A i \in Instances : WF_vars(Return(i))
ProgressSpec == Spec /\ InternalFairness /\ ReturnFairness
\* Assumption controls start at one admitted continuation, without further arrivals.
\* This explicit control scenario is separate from the full normal profiles.
ControlInit ==
  s = [InitialState EXCEPT !.phase[<<1,1>>] = "event", !.held = {<<1,1>>},
         !.index[Key(1)] = 1, !.count[Key(1)] = 1,
         !.event[<<1,1>>] = 1]
ControlNext == (\E o \in Occ : Settle(o) \/ Release(o)) \/ Return(1) \/ Quiescent
ControlFairness == \A o \in Occ : WF_vars(Settle(o)) /\ WF_vars(Release(o))
ControlProgressSpec == ControlInit /\ [][ControlNext]_vars /\ ControlFairness /\ ReturnFairness
NoReturnSpec == ControlInit /\ [][ControlNext]_vars /\ ControlFairness
NoSchedulingSpec == ControlInit /\ [][ControlNext]_vars /\ ReturnFairness

TypeOK ==
  /\ s.phase \in [Occ -> {"new","active","event","timer","settled","done","cancelled"}]
  /\ s.held \subseteq Occ /\ s.restored \subseteq Occ
  /\ s.count \in [Keys -> 0..4] /\ s.index \in [Keys -> {0,1,2}]
  /\ s.wins \in [Occ -> 0..2] /\ s.event \in [Occ -> {0,1,2}]
  /\ s.failures \in [Occ -> 0..1]
  /\ s.due \subseteq Occ /\ s.dispatched \subseteq s.due
  /\ \A i \in Instances : s.attempts[i] \in [Events -> 0..2] /\ Len(s.queue[i]) <= 4
UniqueActiveWait == UniqueActive(s, Occ, Key)
ReservationOwnership == Reservations(s, Occ, Keys, Key)
SingleResolution == Resolutions(s, Occ, Instances, Events)
ExactRelease == ReleaseIntegrity(s, Occ, Keys, Key)
RollbackConsistency == RollbackIntegrity(s, Occ)
KeyAndOccurrenceIsolation == IsolationIntegrity(s, Occ, Key)
ConditionalSettlement ==
  /\ \A o \in Occ : (s.phase[o] \in {"event","timer","settled"}) ~> (s.phase[o] \notin {"event","timer","settled"})
  /\ \A i \in Instances : s.cancel[i] ~> Live(i) = {}
ConditionalReuse == \A o \in Occ : (<>[](Available(o) /\ Busy(o[1]) = {})) => <> (s.phase[o] # "new")
NotWitness == ~s.seen
NotControlComplete == s.phase[<<1,1>>] # "done"
=============================================================================
