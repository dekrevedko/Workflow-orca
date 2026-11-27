-------------------------- MODULE LateProtectedStep --------------------------
EXTENDS Naturals

CONSTANT TrustEmptyLookup
VARIABLES journal, tickets, receipt, transfers, replayed,
          worker, request, aWork, bLive, fence, sawEmpty, confirmation, sentDuringB

governance == <<tickets, receipt, transfers, replayed>>
external == <<worker, request, aWork, bLive, fence, sentDuringB>>
vars == <<journal, governance, external, sawEmpty, confirmation>>

\* One real held ticket for A, one queued B, distinct tokens, one admitted
\* first-attempt invocation. B's eventual reservation stays in scope to the
\* end of the experiment. No retries, provider failures, or normal A release.
Init ==
    /\ journal = "Held"
    /\ tickets = {"A"}
    /\ receipt = FALSE
    /\ transfers = 0
    /\ replayed = FALSE
    /\ worker = "paused"
    /\ request = "unsent"
    /\ aWork = "absent"
    /\ bLive = FALSE
    /\ fence = "open"
    /\ sawEmpty = FALSE
    /\ confirmation = "none"
    /\ sentDuringB = FALSE

\* Cancellation publishes a detached obligation for the admitted attempt.
\* The stale worker and a request in transport are outside this journal.
Cancel ==
    /\ journal = "Held"
    /\ journal' = "Quarantined"
    /\ UNCHANGED <<governance, external, sawEmpty, confirmation>>

\* An exact lookup can truthfully return nothing while a future acceptance
\* is possible, even after the worker sent bytes which have not arrived.
ObserveEmpty ==
    /\ journal = "Quarantined" /\ ~sawEmpty /\ fence = "open"
    /\ aWork = "absent" /\ request \in {"unsent", "inFlight"}
    /\ sawEmpty' = TRUE
    /\ UNCHANGED <<journal, governance, external, confirmation>>

\* A cached token can still be submitted after cancellation and release.
\* Neither the stale sender nor the protected resource consults the journal.
ResumeAndSend ==
    /\ journal # "Held" /\ worker = "paused"
    /\ worker' = "sent" /\ request' = "inFlight"
    /\ sentDuringB' = bLive
    /\ UNCHANGED <<journal, governance, aWork, bLive, fence, sawEmpty, confirmation>>

RetireWorker ==
    /\ worker = "sent"
    /\ worker' = "retired"
    /\ UNCHANGED <<journal, governance, request, aWork, bLive, fence,
                   sawEmpty, confirmation, sentDuringB>>

DeliverSubmit ==
    /\ request = "inFlight"
    /\ IF fence = "open"
          THEN /\ request' = "accepted" /\ aWork' = "live"
          ELSE /\ request' = "rejected" /\ UNCHANGED aWork
    /\ UNCHANGED <<journal, governance, worker, bLive, fence,
                   sawEmpty, confirmation, sentDuringB>>

FinishExternalA ==
    /\ aWork = "live"
    /\ aWork' = "terminal"
    /\ UNCHANGED <<journal, governance, worker, request, bLive, fence,
                   sawEmpty, confirmation, sentDuringB>>

\* Optional end-to-end integration capability. The enforcement point
\* irreversibly rejects this token, including submits already in transport.
\* Accepted work continues until it actually finishes.
InstallResourceFence ==
    /\ journal = "Quarantined" /\ fence = "open"
    /\ fence' = IF aWork = "absent" THEN "beforeAcceptance" ELSE "afterAcceptance"
    /\ UNCHANGED <<journal, governance, worker, request, aWork, bLive,
                   sawEmpty, confirmation, sentDuringB>>

CausalStop == worker = "retired" /\ request = "accepted" /\ aWork = "terminal"
EffectiveFence == fence # "open" /\ aWork # "live"
SafeToReuse == CausalStop \/ EffectiveFence

\* The reconciler chooses whether to call. Recording the basis makes
\* caller behaviour observable. The API receives only the exact token/ID.
CallConfirmation ==
    /\ journal = "Quarantined" /\ confirmation = "none"
    /\ \/ /\ CausalStop /\ confirmation' = "causalStop"
       \/ /\ EffectiveFence /\ confirmation' = "resourceFence"
       \/ /\ TrustEmptyLookup /\ sawEmpty /\ confirmation' = "emptyLookup"
    /\ UNCHANGED <<journal, governance, external, sawEmpty>>

\* Serialized confirmation of the single bound token/ID frees the A ticket,
\* grants B in the same provider mutation, and records the accepted receipt.
\* Governance does not inspect or attest external work.
ConfirmAndPromote ==
    /\ confirmation # "none" /\ journal = "Quarantined" /\ ~receipt
    /\ tickets = {"A"}
    /\ tickets' = {"B"} /\ receipt' = TRUE /\ transfers' = transfers + 1
    /\ UNCHANGED <<journal, replayed, external, sawEmpty, confirmation>>

PublishReleased ==
    /\ receipt /\ journal = "Quarantined"
    /\ journal' = "Released"
    /\ UNCHANGED <<governance, external, sawEmpty, confirmation>>

\* Same-token, same-ID redelivery sees the retained acceptance. One
\* observable replay represents any number of subsequent identical reads.
ReplayConfirmation ==
    /\ receipt /\ ~replayed
    /\ replayed' = TRUE
    /\ UNCHANGED <<journal, tickets, receipt, transfers, external, sawEmpty, confirmation>>

BeginB ==
    /\ tickets = {"B"} /\ ~bLive
    /\ bLive' = TRUE
    /\ UNCHANGED <<journal, governance, worker, request, aWork, fence,
                   sawEmpty, confirmation, sentDuringB>>

Completed ==
    /\ journal = "Released" /\ replayed /\ bLive /\ worker = "retired"
    /\ request \in {"accepted", "rejected"} /\ aWork # "live"
    /\ UNCHANGED vars

Next == Cancel \/ ObserveEmpty \/ ResumeAndSend \/ RetireWorker \/ DeliverSubmit
        \/ FinishExternalA \/ InstallResourceFence \/ CallConfirmation
        \/ ConfirmAndPromote \/ PublishReleased \/ ReplayConfirmation \/ BeginB \/ Completed
Spec == Init /\ [][Next]_vars

\* Service fairness begins only when sufficient evidence makes the call
\* possible. It never manufactures external evidence. FairSpec additionally
\* assumes eventual sending, delivery, worker return, and job termination.
FairService == WF_vars(Cancel) /\ WF_vars(CallConfirmation)
               /\ WF_vars(ConfirmAndPromote) /\ WF_vars(PublishReleased) /\ WF_vars(BeginB)
ServiceSpec == Spec /\ FairService
FairSpec == ServiceSpec /\ WF_vars(ResumeAndSend) /\ WF_vars(DeliverSubmit)
            /\ WF_vars(RetireWorker) /\ WF_vars(FinishExternalA)

TypeOK ==
    /\ journal \in {"Held", "Quarantined", "Released"}
    /\ tickets \subseteq {"A", "B"} /\ receipt \in BOOLEAN
    /\ transfers \in Nat /\ replayed \in BOOLEAN
    /\ worker \in {"paused", "sent", "retired"}
    /\ request \in {"unsent", "inFlight", "accepted", "rejected"}
    /\ aWork \in {"absent", "live", "terminal"} /\ bLive \in BOOLEAN
    /\ fence \in {"open", "beforeAcceptance", "afterAcceptance"}
    /\ sawEmpty \in BOOLEAN /\ sentDuringB \in BOOLEAN
    /\ confirmation \in {"none", "causalStop", "resourceFence", "emptyLookup"}

OneTransfer ==
    /\ IF receipt THEN tickets = {"B"} /\ transfers = 1
                  ELSE tickets = {"A"} /\ transfers = 0
    /\ (replayed => receipt)
    /\ (journal = "Released" => receipt)
    /\ (bLive => tickets = {"B"})

ReleaseRequiresExternalProof == receipt => SafeToReuse
ProtectedFenceEnforced == fence = "beforeAcceptance" => aWork = "absent"
ExternalExclusion == ~(aWork = "live" /\ bLive)

Reclaimed == journal = "Released" /\ transfers = 1 /\ bLive
AfterProofProgress == (journal = "Quarantined" /\ SafeToReuse) ~> Reclaimed
EventualReuse == (journal = "Quarantined") ~> Reclaimed

\* Safe reachability witnesses. A was admitted before cancellation. In the
\* causal trace, A was accepted while its unit was held. In the fence trace,
\* A had not even sent its first request until B was already using the unit.
NoLateAcceptedWitness ==
    ~(sawEmpty /\ confirmation = "causalStop" /\ fence = "open" /\ ~sentDuringB
      /\ CausalStop /\ Reclaimed /\ replayed)
NoFencedLateSubmitWitness ==
    ~(sawEmpty /\ confirmation = "resourceFence" /\ fence = "beforeAcceptance"
      /\ sentDuringB /\ request = "rejected" /\ Reclaimed /\ replayed)

\* A schedule for the empty-lookup control: the admitted worker stays paused
\* until B physically starts. The safety and progress runs explore all orders.
PausedUntilBStarts == (worker = "paused" \/ bLive)
=============================================================================
