--------------------------- MODULE ResourcePoolResize ---------------------------
EXTENDS Naturals, Sequences

CONSTANT Fault
VARIABLES layout, target, pool, version, phase, proposal, retried, released,
          cancelled, history
vars == <<layout, target, pool, version, phase, proposal, retried, released,
          cancelled, history>>

Members(s) == {s[i] : i \in 1..Len(s)}
Open == "close" \notin Members(history)
Units(w, p) ==
    CASE w = "first"  -> IF p = "X" THEN 2 ELSE 0
      [] w = "holder" -> IF p = "X" THEN 2 ELSE 0
      [] OTHER        -> IF p = (IF layout = "loaded" THEN "X" ELSE "Y") THEN 1 ELSE 0

RECURSIVE Used(_, _)
Used(tickets, p) ==
    IF tickets = <<>> THEN 0 ELSE Units(Head(tickets), p) + Used(Tail(tickets), p)

EmptyProposal == [write |-> [grants |-> <<>>, after |-> [capacity |-> 0, queue |-> <<>>, tickets |-> <<>>]],
                  version |-> 0, open |-> FALSE]

Init ==
    /\ layout \in {"disjoint", "loaded"}
    /\ target \in IF layout = "disjoint" THEN {2} ELSE {3, 4, 5}
    /\ pool = [capacity |-> IF layout = "disjoint" THEN 1 ELSE 2,
               queue |-> <<"first", "second">>,
               tickets |-> IF layout = "disjoint" THEN <<>> ELSE <<"holder">>]
    /\ version = 0 /\ phase = "pending" /\ proposal = EmptyProposal /\ retried = {}
    /\ released = FALSE /\ cancelled = FALSE /\ history = <<>>

\* Selected tickets are computed against a detached read. Pool Y always has one unit.
RECURSIVE SelectGrants(_, _, _, _)
SelectGrants(q, x, y, open) ==
    IF q = <<>> THEN <<>>
    ELSE LET w == Head(q) IN
        IF w = "first" /\ ~open THEN SelectGrants(Tail(q), x, y, open)
        ELSE IF Units(w, "X") <= x /\ Units(w, "Y") <= y
             THEN <<w>> \o SelectGrants(Tail(q), x - Units(w, "X"), y - Units(w, "Y"), open)
             ELSE IF Fault = "bypass" THEN SelectGrants(Tail(q), x, y, open) ELSE <<>>

Plan(g, open, drain) ==
    LET grants == IF drain THEN SelectGrants(g.queue, g.capacity - Used(g.tickets, "X"),
                                               1 - Used(g.tickets, "Y"), open)
                  ELSE <<>>
    IN [grants |-> grants,
        after |-> [g EXCEPT !.tickets = @ \o grants,
                            !.queue = SelectSeq(@, LAMBDA w : w \notin Members(grants))]]

ReadResize ==
    /\ phase = "pending"
    /\ proposal' = [write |-> Plan([pool EXCEPT !.capacity = target], Open, Fault # "noDrain"),
                    version |-> version, open |-> Open]
    /\ phase' = "prepared"
    /\ UNCHANGED <<layout, target, pool, version, retried, released, cancelled, history>>

\* Only promotion of the scoped first waiter requires its workflow checkpoint guard.
Conflicts ==
    {reason \in {"governance", "owner"} :
        IF reason = "governance" THEN proposal.version # version /\ Fault # "noVersion"
        ELSE proposal.open # Open /\ "first" \in Members(proposal.write.grants)
             /\ Fault # "noOwner"}

FinishResize ==
    /\ phase = "prepared"
    /\ IF Conflicts # {}
       THEN /\ phase' = "pending" /\ retried' = retried \cup Conflicts
            /\ UNCHANGED <<pool, version, history>>
       ELSE /\ phase' = "done" /\ pool' = proposal.write.after
            /\ version' = version + 1 /\ history' = history \o proposal.write.grants
            /\ UNCHANGED retried
    /\ proposal' = EmptyProposal
    /\ UNCHANGED <<layout, target, released, cancelled>>

CloseOwner ==
    /\ Open /\ history' = Append(history, "close")
    /\ UNCHANGED <<layout, target, pool, version, phase, proposal, retried, released, cancelled>>

RemoveAndDrain(w) ==
    LET freed == [pool EXCEPT !.tickets = SelectSeq(@, LAMBDA t : t # w),
                              !.queue = SelectSeq(@, LAMBDA t : t # w)]
        write == Plan(freed, Open, TRUE)
    IN /\ pool' = write.after /\ history' = history \o write.grants /\ version' = version + 1

ReleaseHolder ==
    /\ "holder" \in Members(pool.tickets) /\ ~released
    /\ released' = TRUE /\ RemoveAndDrain("holder")
    /\ UNCHANGED <<layout, target, phase, proposal, retried, cancelled>>

CancelFirst ==
    /\ ~Open /\ ~cancelled /\ cancelled' = TRUE /\ RemoveAndDrain("first")
    /\ UNCHANGED <<layout, target, phase, proposal, retried, released>>

ResizeStep == ReadResize \/ FinishResize
Next == ResizeStep \/ CloseOwner \/ CancelFirst \/ ReleaseHolder
FairSteps == [][Next]_vars /\ WF_vars(ResizeStep) /\ WF_vars(CancelFirst)
Spec == Init /\ FairSteps
DisjointSpec == Init /\ layout = "disjoint" /\ FairSteps

\* These properties inspect committed history, reservations and the live queue.
AtMostOnce == \A i,j \in 1..Len(history) : i # j => history[i] # history[j]
NoBypass == \A i \in 1..Len(history) : history[i] = "second" =>
    \E j \in 1..(i-1) : history[j] \in {"first", "close"}
NoClosedGrant == \A i,j \in 1..Len(history) :
    history[i] = "close" /\ history[j] = "first" => j < i
Reservations ==
    /\ Used(pool.tickets, "X") <= pool.capacity /\ Used(pool.tickets, "Y") <= 1
    /\ Members(pool.queue) \intersect Members(pool.tickets) = {}
    /\ \A i,j \in 1..Len(pool.tickets) : i # j => pool.tickets[i] # pool.tickets[j]
NoResurrection ==
    /\ released => "holder" \notin Members(pool.tickets)
    /\ cancelled => "first" \notin (Members(pool.tickets) \cup Members(pool.queue))

EligibleHead ==
    LET live == SelectSeq(pool.queue, LAMBDA w : w # "first" \/ Open)
    IN IF live = <<>> THEN FALSE
       ELSE \A p \in {"X","Y"} : Used(pool.tickets, p) + Units(Head(live), p)
                                  <= (IF p = "X" THEN pool.capacity ELSE 1)
DrainedOnResize == phase = "done" /\ (Open \/ cancelled) => ~EligibleHead
ResizeFinishes == <>(phase = "done")
EventuallyDrained == (phase = "done" /\ EligibleHead) ~> ~EligibleHead

NoDisjointWitness == ~(layout = "disjoint" /\ phase = "done" /\ history = <<"first", "second">>)
NoBlockedWitness == ~(layout = "loaded" /\ target = 3 /\ phase = "done" /\ Open
                      /\ pool.tickets = <<"holder">> /\ pool.queue = <<"first", "second">>)
NoReleaseRetryWitness == ~("governance" \in retried /\ released /\ phase = "done"
                            /\ history = <<"first", "second">>)
NoCancelRetryWitness == ~("owner" \in retried /\ layout = "disjoint" /\ phase = "done"
                           /\ ~cancelled /\ pool.queue = <<"first">>
                           /\ history = <<"close", "second">>)
=============================================================================
