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
Spec == Init /\ [][Next]_vars

CommittedGrantRetained == journal \in {"acquired", "active"} => owner = "workflow"
ActiveGrantRetained == journal = "active" => owner = "workflow"
NoPrematureTransfer == ~(journal = "active" /\ rival = "granted")
NoLostTicketWitness == ~(winner = "A" /\ journal = "active" /\ phase["B"] = "rolledBack"
                         /\ rollbackDuringActivation /\ rival = "granted" /\ owner = "rival")
NoRetainedTicketWitness == ~(winner = "A" /\ journal = "active" /\ phase["B"] = "rolledBack"
                             /\ rollbackDuringActivation /\ rival = "queued" /\ owner = "workflow")
NoLegitimateTransferWitness == ~(journal = "released" /\ owner = "rival" /\ rival = "granted")
=============================================================================
