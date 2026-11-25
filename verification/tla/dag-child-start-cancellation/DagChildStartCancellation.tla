---------------------- MODULE DagChildStartCancellation ----------------------
EXTENDS Naturals

CONSTANT Mode
VARIABLES process, parent, child, notices, starts, joins, marker, attempt, attemptAtCancel
vars == <<process, parent, child, notices, starts, joins, marker, attempt, attemptAtCancel>>

TerminalChild == child \in {"Completed", "Cancelled"}

\* One admitted child A, one independently ready sibling B, one admission slot.
\* Hold the original start delivery so the parent's cancel handler materializes A.
Init ==
    /\ process = "seed"
    /\ parent = [state |-> "Running", nodeA |-> "StartAuthorized", nodeB |-> "Ready"]
    /\ child = "Absent"
    /\ notices = [start |-> "pending", cancel |-> "absent", drive |-> "absent", join |-> "absent"]
    /\ starts = 0 /\ joins = 0 /\ marker = FALSE /\ attempt = "none" /\ attemptAtCancel = "unread"

Barrier ==
    /\ parent.state = "Running"
    /\ parent' = [state |-> "CancellationRequested", nodeA |-> "CancellationRequested", nodeB |-> "Cancelled"]
    /\ notices' = [notices EXCEPT !.cancel = "pending"]
    /\ UNCHANGED <<process, child, starts, joins, marker, attempt, attemptAtCancel>>

\* Within the actual cancel delivery: its first SQL transaction creates A and
\* publishes an ordinary continuation. No child cancellation has committed yet.
Materialize ==
    /\ process = "seed" /\ notices.cancel = "pending"
    /\ child = "Absent" /\ child' = "Runnable" /\ starts' = starts + 1
    /\ notices' = [notices EXCEPT !.cancel = "claimed", !.drive = "pending"]
    /\ UNCHANGED <<process, parent, joins, marker, attempt, attemptAtCancel>>

Crash ==
    /\ process = "seed" /\ notices.cancel = "claimed"
    /\ process' = "down"
    /\ UNCHANGED <<parent, child, notices, starts, joins, marker, attempt, attemptAtCancel>>

Restart ==
    /\ process = "down" /\ process' = "replacement"
    /\ UNCHANGED <<parent, child, notices, starts, joins, marker, attempt, attemptAtCancel>>

\* Reclaim includes expiry of the dead worker's claim. Reattaching to A is
\* independent of whether its original start notice has already been acknowledged.
RestoreCancel ==
    /\ process = "replacement" /\ notices.cancel = "claimed"
    /\ attemptAtCancel' = IF TerminalChild THEN "alreadyTerminal" ELSE attempt
    /\ child' = IF TerminalChild THEN child ELSE "Cancelling"
    /\ notices' = [notices EXCEPT !.cancel = "handled"]
    /\ UNCHANGED <<process, parent, starts, joins, marker, attempt>>

AckCancel ==
    /\ notices.cancel = "handled" /\ notices' = [notices EXCEPT !.cancel = "acked"]
    /\ UNCHANGED <<process, parent, child, starts, joins, marker, attempt, attemptAtCancel>>

DeliverStart ==
    /\ process = "replacement" /\ notices.start = "pending"
    /\ notices' = [notices EXCEPT !.start = "acked"]
    /\ starts' = IF Mode = "duplicateStart" THEN starts + 1 ELSE starts
    /\ UNCHANGED <<process, parent, child, joins, marker, attempt, attemptAtCancel>>

\* The driver durably admits an attempt before it enters user code. After
\* admission a worker can carry a stale local attempt across child cancellation.
AdmitAttempt ==
    /\ process = "replacement" /\ notices.drive = "pending" /\ attempt = "none"
    /\ (child = "Runnable" \/ (Mode = "driveCancelled" /\ child = "Cancelling"))
    /\ attempt' = "admitted"
    /\ UNCHANGED <<process, parent, child, notices, starts, joins, marker, attemptAtCancel>>

BeginStep ==
    /\ attempt = "admitted" /\ attempt' = "entered" /\ marker' = TRUE
    /\ UNCHANGED <<process, parent, child, notices, starts, joins, attemptAtCancel>>

DiscardStaleAttempt ==
    /\ attempt = "admitted" /\ child \in {"Cancelling", "Cancelled"}
    /\ attempt' = "discarded"
    /\ UNCHANGED <<process, parent, child, notices, starts, joins, marker, attemptAtCancel>>

CommitTerminal ==
    /\ process = "replacement" /\ (child = "Cancelling" \/ (child = "Runnable" /\ marker))
    /\ child' = IF child = "Runnable" \/ (Mode = "staleSuccess" /\ marker)
                THEN "Completed" ELSE "Cancelled"
    /\ notices' = [notices EXCEPT !.join = "pending"]
    /\ UNCHANGED <<process, parent, starts, joins, marker, attempt, attemptAtCancel>>

DrainContinuation ==
    /\ process = "replacement" /\ TerminalChild /\ notices.drive = "pending" /\ attempt # "admitted"
    /\ notices' = [notices EXCEPT !.drive = "acked"]
    /\ UNCHANGED <<process, parent, child, starts, joins, marker, attempt, attemptAtCancel>>

PublishJoin ==
    /\ notices.join = "pending" /\ TerminalChild
    /\ parent' = [parent EXCEPT !.state = "Cancelled",
                   !.nodeA = IF child = "Completed" THEN "Succeeded" ELSE "Cancelled"]
    /\ joins' = joins + 1 /\ notices' = [notices EXCEPT !.join = "handled"]
    /\ UNCHANGED <<process, child, starts, marker, attempt, attemptAtCancel>>

AckJoin ==
    /\ notices.join = "handled" /\ notices' = [notices EXCEPT !.join = "acked"]
    /\ UNCHANGED <<process, parent, child, starts, joins, marker, attempt, attemptAtCancel>>

\* Independent negative controls for publishing terminality early and for
\* letting a pre-barrier admission observation introduce B after the barrier.
PrematureTerminal ==
    /\ Mode = "earlyTerminal" /\ parent.state = "CancellationRequested" /\ joins = 0
    /\ parent' = [parent EXCEPT !.state = "Cancelled"]
    /\ UNCHANGED <<process, child, notices, starts, joins, marker, attempt, attemptAtCancel>>

AdmitStaleSibling ==
    /\ Mode = "admitB" /\ parent.state = "CancellationRequested" /\ parent.nodeB = "Cancelled"
    /\ parent' = [parent EXCEPT !.nodeB = "StartAuthorized"]
    /\ UNCHANGED <<process, child, notices, starts, joins, marker, attempt, attemptAtCancel>>

Settled == /\ parent.state = "Cancelled"
           /\ notices = [start |-> "acked", cancel |-> "acked", drive |-> "acked", join |-> "acked"]

Advance == Barrier \/ Materialize \/ Crash \/ Restart \/ RestoreCancel \/ AckCancel \/
           DeliverStart \/ AdmitAttempt \/ BeginStep \/ DiscardStaleAttempt \/ CommitTerminal \/ DrainContinuation \/
           PublishJoin \/ AckJoin \/ PrematureTerminal \/ AdmitStaleSibling
Next == Advance \/ (Settled /\ UNCHANGED vars)
Spec == Init /\ [][Next]_vars /\ WF_vars(Advance)

ChildCancelStopsNewAttempt == attemptAtCancel = "none" => (attempt = "none" /\ ~marker)
ChildCancellationWins == child = "Completed" => attemptAtCancel \in {"unread", "alreadyTerminal"}
OneStart == starts = IF child = "Absent" THEN 0 ELSE 1
SiblingClosed == parent.state # "Running" => parent.nodeB = "Cancelled"
AdmissionUntilJoin == joins = 0 => parent.nodeA \in {"StartAuthorized", "CancellationRequested"}
ParentWaits == parent.state = "Cancelled" => (TerminalChild /\ joins = 1)
AuthoritativeJoin ==
    /\ joins \in 0..1
    /\ joins = 1 => ((child = "Completed" /\ parent.nodeA = "Succeeded") \/
                     (child = "Cancelled" /\ parent.nodeA = "Cancelled"))
    /\ notices.join = "acked" => joins = 1
Recovers == <>Settled

\* Reachability probes. First-step suppression after the parent barrier is
\* intentionally a question; the child-stream cancellation is a separate commit.
NoPostBarrierBody == ~marker
NoContinuationFirst == ~(Settled /\ marker /\ child = "Completed")
NoCancellationFirst == ~(Settled /\ ~marker /\ attemptAtCancel = "none" /\ child = "Cancelled")
NoCancellationInBody == ~(Settled /\ attemptAtCancel = "entered" /\ child = "Cancelled")
NoLateBodyEntry == ~(Settled /\ attemptAtCancel = "admitted" /\ marker /\ child = "Cancelled")
=============================================================================
