------------------------------ MODULE WaitOwnershipByTarget --------------------------------
EXTENDS EphemeralWaitOwnership
CONSTANT Observer
ASSUME Observer \in Instances /\ Fault = "none" /\ Witness = "none"
Peer == 3 - Observer
Here == {Observer} \X Slots
There == {Peer} \X Slots

LocalWork == ActorWork(Observer)

PeerReserve(o) ==
  /\ s.phase[o] = "new" /\ s.index[Key(Peer)] \in {0,Peer}
  /\ s' = [s EXCEPT !.phase[o] = "settled", !.held = @ \cup {o},
       !.index[Key(Peer)] = Peer, !.count[Key(Peer)] = @ + 1]
PeerDrop(drop) ==
  /\ drop # {} /\ drop \subseteq Live(Peer)
  /\ s' = [s EXCEPT !.phase = [o \in Occ |-> IF o \in drop THEN "done" ELSE s.phase[o]],
       !.held = @ \ drop, !.count[Key(Peer)] = @ - Cardinality(drop),
       !.index[Key(Peer)] = IF drop = Live(Peer) THEN 0 ELSE @]
PeerWork == (\E o \in There : PeerReserve(o)) \/ (\E d \in SUBSET There : PeerDrop(d))
Rest == Busy(Observer) = {} /\ s.queue[Observer] = <<>> /\ s.route[Observer] = "idle" /\ UNCHANGED s
FactorNext == LocalWork \/ PeerWork \/ Rest
FactorSpec == Init /\ [][FactorNext]_vars

\* Discard dead diagnostic history and idle routing scratch. Include the actual
\* comparison of both complete callback observations even for a corrupted callback.
FactorView == <<Observer, [s EXCEPT !.callbackBefore = <<>>, !.callbackAfter = <<>>,
    !.seen = FALSE, !.cancelStage = [i \in Instances |-> "none"],
    !.offered = [i \in Instances |-> IF s.route[i] # "idle" /\ s.observed[i] THEN s.offered[i] ELSE 0],
    !.offerKey = [i \in Instances |-> IF s.route[i] # "idle" /\ s.observed[i] THEN s.offerKey[i] ELSE Key(i)],
    !.observed = [i \in Instances |-> s.route[i] # "idle" /\ s.observed[i]]],
    s.callbackBefore = s.callbackAfter>>
FactorFair ==
  /\ \A o \in Here : WF_vars(Settle(o)) /\ WF_vars(Release(o)) /\ WF_vars(Register(o))
  /\ WF_vars(Cleanup(Observer)) /\ WF_vars(Deliver(Observer)) /\ WF_vars(Return(Observer))
FactorProgress == FactorSpec /\ FactorFair
Settlement == SettlementOf(Observer)
Reuse == ReuseOf(Observer)

LocalCanAdvance == (Busy(Observer) # {} \/ s.queue[Observer] # <<>> \/ s.route[Observer] # "idle")
                   => ENABLED <<LocalWork>>_s

\* Check the interface as an assertion on actual transitions. A source action
\* that writes the wrong count or owner must fail, even if a peer could stutter.
Used(t) == {o \in Occ : t.phase[o] # "new"}
InterfaceChange(i) ==
  LET add == s'.held \ s.held
      drop == s.held \ s'.held
  IN /\ Used(s') = Used(s) \cup add
     /\ \/ /\ add = {} /\ drop = {} /\ s'.index = s.index /\ s'.count = s.count
        \/ \E o \in {i} \X Slots :
             /\ add = {o} /\ drop = {} /\ o \notin Used(s)
             /\ s.index[Key(o[1])] \in {0,o[1]}
             /\ s'.index = [s.index EXCEPT ![Key(o[1])] = o[1]]
             /\ s'.count = [s.count EXCEPT ![Key(o[1])] = @ + 1]
        \/ /\ add = {} /\ drop # {} /\ drop \subseteq Live(i)
           /\ s'.count = [s.count EXCEPT ![Key(i)] = @ - Cardinality(drop)]
           /\ s'.index = [s.index EXCEPT ![Key(i)] = IF drop = Live(i) THEN 0 ELSE @]
InterfacePreserved == [][IF LocalWork THEN InterfaceChange(Observer) ELSE InterfaceChange(Peer)]_s
=============================================================================
