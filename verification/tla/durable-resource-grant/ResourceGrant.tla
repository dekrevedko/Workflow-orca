----------------------------- MODULE ResourceGrant -----------------------------
EXTENDS Naturals

CONSTANT ReleaseOnConflict
Workers == {"A", "B"}
VARIABLES phase, winner, journal, owner, rival, rollbackDuringActivation
vars == <<phase, winner, journal, owner, rival, rollbackDuringActivation>>

\* Both workers have loaded the same pending lexical obligation and workflow version.
\* "workflow" names one immutable instance/holder/ticket; "rival" names a distinct grant.
Init == /\ phase = [w \in Workers |-> "loaded"]
        /\ winner = "none" /\ journal = "pending" /\ owner = "free"
        /\ rival = "absent" /\ rollbackDuringActivation = FALSE

Reserve(w) ==
    /\ phase[w] = "loaded" /\ owner \in {"free", "workflow"}
    /\ owner' = "workflow"
    /\ phase' = [phase EXCEPT ![w] = "reserved"]
    /\ UNCHANGED <<winner, journal, rival, rollbackDuringActivation>>

Commit(w) ==
    \* This experiment releases the two workers from the reservation barrier.
    /\ \A n \in Workers : phase[n] = "reserved"
    /\ journal = "pending"
    /\ phase' = [phase EXCEPT ![w] = "committed"]
    /\ winner' = w /\ journal' = "acquired"
    /\ UNCHANGED <<owner, rival, rollbackDuringActivation>>

Activate ==
    /\ journal = "acquired" /\ owner = "workflow"
    /\ journal' = "active"
    /\ UNCHANGED <<phase, winner, owner, rival, rollbackDuringActivation>>

QueueRival ==
    /\ owner = "workflow" /\ rival = "absent"
    /\ rival' = "queued"
    /\ UNCHANGED <<phase, winner, journal, owner, rollbackDuringActivation>>

ReleaseHolder ==
    /\ owner' = IF rival = "queued" THEN "rival" ELSE "free"
    /\ rival' = IF rival = "queued" THEN "granted" ELSE rival

LoseAppend(w) ==
    /\ phase[w] = "reserved" /\ winner \in Workers /\ w # winner
    /\ phase' = [phase EXCEPT ![w] = "conflict"]
    /\ UNCHANGED <<winner, journal, owner, rival, rollbackDuringActivation>>

Rollback(w) ==
    /\ phase[w] = "conflict"
    /\ phase' = [phase EXCEPT ![w] = "rolledBack"]
    /\ rollbackDuringActivation' = (journal = "active")
    /\ IF ReleaseOnConflict /\ owner = "workflow"
          THEN ReleaseHolder ELSE UNCHANGED <<owner, rival>>
    /\ UNCHANGED <<winner, journal>>

ExitScope ==
    /\ journal = "active" /\ owner = "workflow"
    /\ journal' = "released" /\ ReleaseHolder
    /\ UNCHANGED <<phase, winner, rollbackDuringActivation>>

Next == (\E w \in Workers : Reserve(w) \/ Commit(w) \/ LoseAppend(w) \/ Rollback(w))
        \/ QueueRival \/ Activate \/ ExitScope \/ UNCHANGED vars


CommittedGrantRetained == journal \in {"acquired", "active"} => owner = "workflow"
ActiveGrantRetained == journal = "active" => owner = "workflow"
NoPrematureTransfer == ~(journal = "active" /\ rival = "granted")
NoLostTicketWitness == ~(winner = "A" /\ journal = "active" /\ phase["B"] = "rolledBack"
                         /\ rollbackDuringActivation /\ rival = "granted" /\ owner = "rival")
NoRetainedTicketWitness == ~(winner = "A" /\ journal = "active" /\ phase["B"] = "rolledBack"
                             /\ rollbackDuringActivation /\ rival = "queued" /\ owner = "workflow")
NoPreActivationSurvival == ~(winner = "A" /\ journal = "active" /\ phase["B"] = "rolledBack"
                             /\ ~rollbackDuringActivation /\ rival = "queued" /\ owner = "workflow")
NoLegitimateTransferWitness == ~(journal = "released" /\ owner = "rival" /\ rival = "granted")

\* Independent one-occurrence recovery experiment; "reserved" always denotes the
\* original ticket. Both the workflow commit and governance close can lose their reply.
CONSTANT FenceClosed, RecoverOrphan, GuardAdmission
VARIABLES rec, ticket, stopped, receipt, waiting, late, adopted, entered, history
rv == <<rec, ticket, stopped, receipt, waiting, late, adopted, entered, history>>
RInit == /\ rec = "pending" /\ ticket = "free" /\ receipt = "none"
         /\ stopped = FALSE /\ waiting = FALSE /\ late = FALSE /\ adopted = FALSE /\ entered = FALSE

         /\ history = [reserved |-> FALSE, atCancel |-> FALSE, journalled |-> FALSE, preflight |-> FALSE]

RPreflight == /\ rec = "pending" /\ ~history.preflight
              /\ history' = [history EXCEPT !.preflight = TRUE]
              /\ UNCHANGED <<rec,ticket,stopped,receipt,waiting,late,adopted,entered>>
\* An acquisition/queued promotion read the live owner checkpoint, then raced a newer closing checkpoint.
RStaleWrite == /\ rec = "closing" /\ history.preflight /\ ~history.reserved /\ receipt = "none"
               /\ ~GuardAdmission /\ ticket' = "original" /\ history' = [history EXCEPT !.reserved = TRUE]
               /\ UNCHANGED <<rec,stopped,receipt,waiting,late,adopted,entered>>
RReserve == /\ rec = "pending" /\ ~stopped /\ ticket = "free" /\ history.preflight
            /\ history' = [history EXCEPT !.reserved = TRUE] /\ ticket' = "original" /\ UNCHANGED <<rec, stopped, receipt, waiting, late, adopted, entered>>
RQueue == /\ UNCHANGED history /\ ticket = "original" /\ ~waiting
          /\ waiting' = TRUE /\ UNCHANGED <<rec, ticket, stopped, receipt, late, adopted, entered>>
RStop == /\ UNCHANGED history /\ ~stopped /\ rec \in {"pending","journalled"}
         /\ stopped' = TRUE /\ UNCHANGED <<rec,ticket,receipt,waiting,late,adopted,entered>>
RCommit == /\ history' = [history EXCEPT !.journalled = TRUE] /\ rec = "pending" /\ ticket = "original"
           /\ rec' = "journalled" /\ adopted' = stopped
           /\ UNCHANGED <<ticket,stopped,receipt,waiting,late,entered>>
RActivate == /\ UNCHANGED history /\ rec = "journalled" /\ ticket = "original" /\ ~entered
             /\ rec' = "held" /\ entered' = TRUE
             /\ UNCHANGED <<ticket,stopped,receipt,waiting,late,adopted>>
RCancel == /\ history' = [history EXCEPT !.atCancel = history.reserved] /\ rec \in {"pending","journalled"} /\ rec' = "closing"
           /\ UNCHANGED <<ticket,stopped,receipt,waiting,late,adopted,entered>>
RCausalRelease == /\ receipt' = IF ticket = "original" THEN "released" ELSE "zero"
                  /\ ticket' = (IF waiting THEN "rival" ELSE "free") /\ waiting' = FALSE
RClose == /\ UNCHANGED history /\ rec = "closing" /\ receipt = "none" /\ RecoverOrphan
          /\ RCausalRelease /\ UNCHANGED <<rec,stopped,late,adopted,entered>>
RPublish == /\ UNCHANGED history /\ rec = "closing" /\ receipt # "none" /\ rec' = receipt
            /\ UNCHANGED <<ticket,stopped,receipt,waiting,late,adopted,entered>>
RExit == /\ UNCHANGED history /\ rec = "held" /\ rec' = "released" /\ RCausalRelease
         /\ UNCHANGED <<stopped,late,adopted,entered>>
RLate == /\ UNCHANGED history /\ receipt # "none" /\ ~late /\ late' = TRUE
         /\ ticket' = IF FenceClosed THEN ticket ELSE "original"
         /\ UNCHANGED <<rec,stopped,receipt,waiting,adopted,entered>>
RNext == RPreflight \/ RStaleWrite \/ RReserve \/ RQueue \/ RStop \/ RCommit \/ RActivate \/ RCancel \/ RClose \/ RPublish \/ RExit \/ RLate
Spec == Init /\ RInit /\ [][Next /\ UNCHANGED rv]_<<vars,rv>>
RecoverySpec == Init /\ RInit /\ [][(RNext \/ UNCHANGED rv) /\ UNCHANGED vars]_<<vars,rv>>
                /\ WF_rv(RClose) /\ WF_rv(RPublish)
RecoverySafety == /\ (rec \in {"journalled","held"} => ticket = "original")
                  /\ (receipt = "zero" => ~history.reserved)
                  /\ (receipt = "released" => history.reserved)
                  /\ (rec = "closing" => history.reserved = history.atCancel)
                  /\ (receipt # "none" => ticket # "original")
                  /\ (rec = "zero" => receipt = "zero" /\ ~entered)
                  /\ (rec = "released" => receipt = "released")
ClosureProgress == (rec = "closing") ~> (rec \in {"zero","released"})
NoAdoption == ~(adopted /\ entered /\ ticket = "rival")
NoRecoveredOrphan == ~(stopped /\ ~entered /\ rec = "released" /\ late /\ ticket = "rival")
NoRecoveredJournalled == ~(stopped /\ ~adopted /\ history.journalled /\ ~entered /\ rec = "released" /\ ticket = "rival")
NoRecoveredEmpty == ~(stopped /\ ~entered /\ rec = "zero" /\ late /\ ticket = "free")
=============================================================================
