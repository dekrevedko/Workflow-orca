------------------------------- MODULE GrantWake -----------------------------
EXTENDS Naturals

CONSTANT Mutation
ASSUME Mutation \in {"None", "DropConsume", "UnfencedDirect", "UnfencedAttempt"}

Executors == {"Ingress", "Redrive"}
NoTicket == [id |-> "None", holder |-> "None", token |-> "None",
             pool |-> "None", units |-> 0, generation |-> 0]
Ticket == [id |-> "promoted-ticket", holder |-> "B", token |-> "B-lease",
           pool |-> "slot", units |-> 1, generation |-> 2]
Idle == [op |-> "Idle", version |-> 0, ticket |-> NoTicket]

VARIABLES provider, inbox, wf, plan, overlapLosers, lateReceipt
vars == <<provider, inbox, wf, plan, overlapLosers, lateReceipt>>

Init ==
    /\ provider = NoTicket
    /\ inbox = "Absent"
    \* Version zero denotes B's existing lease-queued checkpoint.
    /\ wf = [v |-> 0, stage |-> "Queued", active |-> TRUE, pending |-> FALSE,
             matches |-> 0, cancels |-> 0, consumes |-> 0, acquisitions |-> 0,
             ticket |-> NoTicket, acquiredBy |-> "None", admissions |-> 0]
    /\ plan = [a \in Executors |-> Idle]
    /\ overlapLosers = {}
    /\ lateReceipt = FALSE

\* One governance append replaces the old hold and promotes B. It does not
\* publish a correlated event or mutate B's workflow stream.
Promote ==
    /\ provider = NoTicket
    /\ provider' = Ticket
    /\ UNCHANGED <<inbox, wf, plan, overlapLosers, lateReceipt>>

AcceptPublicEvent ==
    /\ provider = Ticket /\ inbox = "Absent"
    /\ inbox' = "Received"
    /\ lateReceipt' = (wf.cancels > 0)
    /\ UNCHANGED <<provider, wf, plan, overlapLosers>>

Operation(a) ==
    IF a = "Ingress" /\ inbox # "Applied" THEN
        IF inbox = "Received" /\ wf.active THEN "Match" ELSE "Idle"
    ELSE CASE wf.stage = "Queued" /\ wf.active -> "DirectAcquire"
           [] wf.stage = "Queued" /\ wf.pending -> "ResumeAcquire"
           [] wf.stage = "Acquired" -> "Activate"
           [] wf.stage = "Held" -> "Admit"
           [] OTHER -> "Idle"

Load(a) ==
    /\ provider = Ticket /\ plan[a] = Idle /\ Operation(a) # "Idle"
    /\ plan' = [plan EXCEPT ![a] =
                   [op |-> Operation(a), version |-> wf.v, ticket |-> NoTicket]]
    /\ UNCHANGED <<provider, inbox, wf, overlapLosers, lateReceipt>>

AcquireOps == {"DirectAcquire", "ResumeAcquire"}

\* The scoped provider call returns the ticket already held by this occurrence.
\* Both hosts may finish it before either workflow append.
ReadTicket(a) ==
    /\ plan[a].op \in AcquireOps /\ plan[a].ticket = NoTicket
    /\ plan[a].version = wf.v
    /\ plan' = [plan EXCEPT ![a].ticket = provider]
    /\ UNCHANGED <<provider, inbox, wf, overlapLosers, lateReceipt>>

VersionAllows(a) ==
    plan[a].version = wf.v
    \/ (Mutation = "UnfencedDirect" /\ plan[a].op = "DirectAcquire" /\
        plan[a].ticket = Ticket)
    \/ (Mutation = "UnfencedAttempt" /\ plan[a].op = "Admit")

RejectStale(a) ==
    /\ plan[a] # Idle /\ ~VersionAllows(a)
    /\ plan' = [plan EXCEPT ![a] = Idle]
    \* Record the losing *prepared* command from the original queued version.
    /\ overlapLosers' =
        IF plan[a].version = 0 /\
           (plan[a].op = "Match" \/
            (plan[a].op = "DirectAcquire" /\ plan[a].ticket = Ticket))
        THEN overlapLosers \cup {a} ELSE overlapLosers
    /\ UNCHANGED <<provider, inbox, wf, lateReceipt>>

CommitMatch(a) ==
    /\ plan[a].op = "Match" /\ VersionAllows(a)
    /\ inbox' = "Applied"
    /\ wf' = [wf EXCEPT !.v = @ + 1, !.active = FALSE,
                         !.pending = TRUE, !.matches = @ + 1]
    /\ plan' = [plan EXCEPT ![a] = Idle]
    /\ UNCHANGED <<provider, overlapLosers, lateReceipt>>

CommitAcquire(a) ==
    /\ plan[a].op \in AcquireOps /\ plan[a].ticket = Ticket /\ VersionAllows(a)
    /\ LET direct == plan[a].op = "DirectAcquire"
           consume == ~direct /\ Mutation # "DropConsume"
       IN wf' = [wf EXCEPT !.v = @ + (IF direct \/ consume THEN 2 ELSE 1),
                     !.active = FALSE, !.pending = IF direct \/ consume THEN FALSE ELSE @,
                     !.cancels = @ + (IF direct THEN 1 ELSE 0),
                     !.consumes = @ + (IF consume THEN 1 ELSE 0),
                     !.acquisitions = @ + 1, !.stage = "Acquired",
                     !.ticket = plan[a].ticket, !.acquiredBy = a]
    /\ plan' = [plan EXCEPT ![a] = Idle]
    /\ UNCHANGED <<provider, inbox, overlapLosers, lateReceipt>>

CommitHeld(a) ==
    /\ plan[a].op = "Activate" /\ VersionAllows(a)
    /\ wf.ticket = provider
    /\ wf' = [wf EXCEPT !.v = @ + 1, !.stage = "Held"]
    /\ plan' = [plan EXCEPT ![a] = Idle]
    /\ UNCHANGED <<provider, inbox, overlapLosers, lateReceipt>>

CommitAttempt(a) ==
    /\ plan[a].op = "Admit" /\ VersionAllows(a)
    /\ wf' = [wf EXCEPT !.v = @ + 1, !.stage = "Admitted", !.admissions = @ + 1]
    /\ plan' = [plan EXCEPT ![a] = Idle]
    /\ UNCHANGED <<provider, inbox, overlapLosers, lateReceipt>>

Next ==
    \/ Promote \/ AcceptPublicEvent
    \/ \E a \in Executors :
        Load(a) \/ ReadTicket(a) \/ RejectStale(a) \/ CommitMatch(a) \/
        CommitAcquire(a) \/ CommitHeld(a) \/ CommitAttempt(a)

Spec == Init /\ [][Next]_vars
\* All non-stuttering paths are finite. A live executor, the input publisher
\* and the stores must continue their enabled work, including fresh reloads.
ProgressSpec == Spec /\ WF_vars(Next)

TypeOK ==
    /\ provider \in {NoTicket, Ticket} /\ inbox \in {"Absent", "Received", "Applied"}
    /\ wf.stage \in {"Queued", "Acquired", "Held", "Admitted"}
    /\ wf.v \in 0..6 /\ wf.active \in BOOLEAN /\ wf.pending \in BOOLEAN
    /\ wf.matches \in 0..1 /\ wf.consumes \in 0..1 /\ wf.cancels \in 0..1
    /\ wf.acquisitions \in 0..2 /\ wf.admissions \in 0..2
    /\ wf.ticket \in {NoTicket, Ticket} /\ wf.acquiredBy \in Executors \cup {"None"}
    /\ plan \in [Executors -> [op : AcquireOps \cup {"Idle","Match","Activate","Admit"},
                               version : 0..6, ticket : {NoTicket,Ticket}]]
    /\ overlapLosers \subseteq Executors /\ lateReceipt \in BOOLEAN

WakeOwnership ==
    /\ wf.matches + wf.cancels <= 1
    /\ wf.active = (wf.matches + wf.cancels = 0)
    /\ (inbox = "Applied") = (wf.matches = 1)
    /\ wf.consumes <= wf.matches
    /\ wf.pending = (wf.matches > wf.consumes)

NoOrphanResume == wf.acquisitions > 0 => ~wf.pending /\ ~wf.active
SingleAcquisition == wf.acquisitions <= 1
TicketOwnership ==
    /\ wf.acquisitions = wf.cancels + wf.consumes
    /\ (wf.acquisitions > 0 => wf.ticket = provider /\ wf.ticket = Ticket)
    /\ (wf.stage \in {"Held", "Admitted"} => wf.acquisitions = 1)
SingleAdmission == wf.admissions <= 1 /\ wf.admissions <= wf.acquisitions

Settled ==
    /\ wf.stage = "Admitted" /\ wf.admissions = 1 /\ wf.acquisitions = 1
    /\ ~wf.active /\ ~wf.pending /\ plan = [a \in Executors |-> Idle]
    /\ ((inbox = "Applied" /\ wf.consumes = 1) \/
        (inbox = "Received" /\ wf.cancels = 1))
EventualSettlement == <>Settled
ResumeEventuallyConsumed == wf.pending ~> (wf.consumes = 1)

NoMatchFirstIngress ==
    ~(Settled /\ "Redrive" \in overlapLosers /\ wf.acquiredBy = "Ingress")
NoMatchFirstRedrive ==
    ~(Settled /\ "Redrive" \in overlapLosers /\ wf.acquiredBy = "Redrive")
NoDirectFirst ==
    ~(Settled /\ "Ingress" \in overlapLosers /\ inbox = "Received")
NoLateIngress == ~(Settled /\ lateReceipt /\ inbox = "Received")
=============================================================================
