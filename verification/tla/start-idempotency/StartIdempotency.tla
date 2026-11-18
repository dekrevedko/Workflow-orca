------------------------- MODULE StartIdempotency -------------------------
EXTENDS Naturals, FiniteSets
CONSTANT Fault
Callers == 1..4
Keys == {1, 2}
Key(c) == IF c = 4 THEN 2 ELSE 1
Fingerprint(c) == IF c = 3 THEN 2 ELSE 1

VARIABLES phase, pending, binding, observedOwner, result, independentCompletion
vars == <<phase, pending, binding, observedOwner, result, independentCompletion>>
Init ==
    /\ independentCompletion = FALSE
    /\ phase = [c \in Callers |-> "new"]
    /\ pending = [k \in Keys |-> 0]
    /\ binding = [k \in Keys |-> 0]
    /\ observedOwner = [c \in Callers |-> 0]
    /\ result = [c \in Callers |-> 0]

\* One inspection under the facade gate. A retained binding takes precedence
\* even between its owner's publication and release of the in-flight reservation.
Inspect(c) ==
    /\ phase[c] = "new"
    /\ UNCHANGED independentCompletion
    /\ LET k == Key(c)
           b == binding[k]
       IN IF b # 0 THEN
              /\ phase' = [phase EXCEPT ![c] =
                    IF Fingerprint(b) = Fingerprint(c) \/ Fault = "ignore-fingerprint"
                    THEN "replayed" ELSE "conflict"]
              /\ result' = [result EXCEPT ![c] = b]
              /\ UNCHANGED <<pending, binding, observedOwner>>
          ELSE IF pending[k] # 0 /\ Fault # "skip-reservation" THEN
              /\ phase' = [phase EXCEPT ![c] = "waiting"]
              /\ observedOwner' = [observedOwner EXCEPT ![c] = pending[k]]
              /\ UNCHANGED <<result, pending, binding>>
          ELSE
              /\ (Fault # "global-lock" \/ (\A key \in Keys : pending[key] = 0))
              /\ phase' = [phase EXCEPT ![c] = "starting"]
              /\ pending' = [pending EXCEPT ![k] = c]
              /\ UNCHANGED <<result, binding, observedOwner>>

Publish(c) ==
    /\ UNCHANGED independentCompletion
    /\ phase[c] = "starting"
    /\ phase' = [phase EXCEPT ![c] = "published"]
    /\ binding' = [binding EXCEPT ![Key(c)] = c]
    /\ result' = [result EXCEPT ![c] = c]
    /\ UNCHANGED <<pending, observedOwner>>

Abort(c) ==
    /\ UNCHANGED independentCompletion
    /\ phase[c] = "starting"
    /\ phase' = [phase EXCEPT ![c] = "aborting"]
    /\ UNCHANGED <<pending, binding, observedOwner, result>>

Release(c) ==
    /\ independentCompletion' = (independentCompletion \/
          (c = 4 /\ phase[c] = "published" /\ phase[1] = "starting"))
    /\ phase[c] \in {"published", "aborting"}
    /\ phase' = [phase EXCEPT ![c] = IF @ = "published" THEN "accepted" ELSE "failed"]
    /\ pending' = [pending EXCEPT ![Key(c)] = 0]
    /\ UNCHANGED <<binding, observedOwner, result>>

Wake(c) ==
    /\ UNCHANGED independentCompletion
    /\ phase[c] = "waiting"
    /\ phase[observedOwner[c]] \in {"accepted", "failed"}
    /\ phase' = [phase EXCEPT ![c] = "new"]
    /\ UNCHANGED <<pending, binding, observedOwner, result>>

Cancel(c) ==
    /\ UNCHANGED independentCompletion
    /\ phase[c] \in {"new", "waiting"}
    /\ phase' = [phase EXCEPT ![c] = "cancelled"]
    /\ UNCHANGED <<pending, binding, observedOwner, result>>

Finished(c) == phase[c] \in {"accepted", "failed", "cancelled", "replayed", "conflict"}
Step(c) == Inspect(c) \/ Publish(c) \/ Abort(c) \/ Release(c) \/ Wake(c)
Next == (\E c \in Callers : Step(c) \/ Cancel(c)) \/ ((\A c \in Callers : Finished(c)) /\ UNCHANGED vars)
Spec == Init /\ [][Next]_vars
FairSpec == Spec /\ (\A c \in Callers : WF_vars(Step(c)))

UniqueOwner == \A c \in Callers :
    phase[c] \in {"starting","published","aborting"} => pending[Key(c)] = c
ConsistentBinding == \A c \in Callers :
    phase[c] \in {"published","accepted"} => binding[Key(c)] = c
ReplayIdentity == \A c \in Callers :
    phase[c] \in {"replayed", "conflict"} =>
        /\ result[c] = binding[Key(c)]
        /\ Key(result[c]) = Key(c)
        /\ (phase[c] = "replayed" <=> Fingerprint(result[c]) = Fingerprint(c))
Settlement == <> (\A c \in Callers : Finished(c))
NoIndependentCompletion == ~independentCompletion
NoWaiterRetry == ~(observedOwner[3] = 1 /\ phase[1] = "failed" /\ phase[3] = "accepted")
NoWaiterReplay == ~(phase[2] = "replayed" /\ observedOwner[2] = result[2])
=============================================================================
