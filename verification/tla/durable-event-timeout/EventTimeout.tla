---------------------------- MODULE EventTimeout ----------------------------
EXTENDS Naturals, Sequences, FiniteSets
CONSTANTS BindTimer, Deduplicate
VARIABLES active, armed, claimed, inbox, history, stale
vars == <<active, armed, claimed, inbox, history, stale>>
Waits == {"A","B"}
Events == {"E1","E2"}
Timer(w) == IF w = "A" THEN "TA" ELSE "TB"
Init ==
    /\ active = "A" /\ armed = {"TA"} /\ claimed = {}
    /\ inbox = [e \in Events |-> "new"] /\ history = <<>> /\ stale = <<>>

\* Durable acceptance precedes matching. E2 enters only after B has registered.
Receive(e) ==
    /\ inbox[e] = "new"
    /\ active = (IF e = "E1" THEN "A" ELSE "B")
    /\ inbox' = [inbox EXCEPT ![e] = "received"]
    /\ UNCHANGED <<active, armed, claimed, history, stale>>

\* One stream commit includes the wait match, timer cancellation and Applied row.
Match(e) ==
    /\ history' = Append(history, <<active,e>>)
    /\ inbox' = [inbox EXCEPT ![e] = "applied"]
    /\ active' = "none" /\ armed' = armed \ {Timer(active)}
Deliver(e) ==
    /\ active \in Waits /\ inbox[e] = "received" /\ Match(e)
    /\ UNCHANGED <<claimed, stale>>

RegisterB ==
    /\ active = "none" /\ history = << <<"A","E1">> >>
    /\ active' = "B" /\ armed' = armed \cup {"TB"}
    /\ UNCHANGED <<claimed, inbox, history, stale>>

Claim(t) ==
    /\ t \in armed \ claimed
    /\ claimed' = claimed \cup {t}
    /\ UNCHANGED <<active, armed, inbox, history, stale>>

Seen(x) == \E i \in 1..Len(stale) : stale[i] = x
Dispatch(t) ==
    /\ t \in claimed
    /\ claimed' = claimed \ {t}
    /\ stale' = IF active = "B" /\ t = "TA" THEN Append(stale,"oldTimer") ELSE stale
    /\ IF active \in Waits /\ (t = Timer(active) \/ ~BindTimer)
          THEN /\ history' = Append(history, <<active,t>>)
               /\ active' = "none" /\ armed' = armed \ {Timer(active)}
          ELSE UNCHANGED <<active, armed, history>>
    /\ UNCHANGED inbox

\* A duplicate acknowledgement can also prompt a fresh pending-inbox scan.
ReplayE1 ==
    /\ active = "B" /\ inbox["E1"] = "applied" /\ ~Seen("replay")
    /\ UNCHANGED claimed
    /\ IF ~Deduplicate THEN Match("E1")
       ELSE IF inbox["E2"] = "received" THEN Match("E2")
       ELSE UNCHANGED <<active, armed, inbox, history>>
    /\ stale' = Append(stale, IF history' = Append(history, <<"B","E2">>)
                              THEN "replayAppliedE2" ELSE "replay")

Step == RegisterB \/ ReplayE1 \/ (\E e \in Events : Receive(e) \/ Deliver(e))
        \/ (\E t \in {"TA","TB"} : Claim(t) \/ Dispatch(t))
Next == Step \/ (~ENABLED Step /\ UNCHANGED vars)
Spec == Init /\ [][Next]_vars

Wins(w) == {i \in 1..Len(history) : history[i][1] = w}
Uses(e) == {i \in 1..Len(history) : history[i][2] = e}
OneWinner == \A w \in Waits : Cardinality(Wins(w)) <= 1
TimerOwnership == \A i \in 1..Len(history) :
    history[i][2] \in {"TA","TB"} => history[i][2] = Timer(history[i][1])
EventOwnership == \A e \in Events : Cardinality(Uses(e)) <= 1
Obligations == /\ armed = IF active \in Waits THEN {Timer(active)} ELSE {}
               /\ \A e \in Events : (inbox[e] = "applied") <=> Uses(e) # {}
               /\ active = "B" => history = << <<"A","E1">> >>
               /\ active = "none" => Len(history) > 0
\* If the live B disappears it needs a new winner of its own.
BResolution == (<<"A","E1">> \in {history[i] : i \in 1..Len(history)}
                /\ (Seen("oldTimer") \/ Seen("replay")) /\ active = "none")
               => (Len(history) = 2 /\ history[2] \in {<<"B","E2">>, <<"B","TB">>})
DurableState == <<active, armed, inbox, history>>
OldTimerIsHarmless == [][(active = "B" /\ Dispatch("TA")) => UNCHANGED DurableState]_vars
EmptyReplayIsHarmless == [][(ReplayE1 /\ inbox["E2"] # "received") => UNCHANGED DurableState]_vars
ReplayUsesPending == [][(ReplayE1 /\ inbox["E2"] = "received") =>
    (history' = Append(history, <<"B","E2">>) /\ inbox'["E2"] = "applied") ]_vars

NoTimerFirst == ~(history = << <<"A","TA">> >> /\ inbox["E1"] = "received")
NoTimerThenReplay == ~(stale = <<"oldTimer","replay">> /\ active = "B")
NoReplayThenTimer == ~(stale = <<"replay","oldTimer">> /\ active = "B")
NoFreshWin == ~(stale = <<"oldTimer","replay">> /\ history = << <<"A","E1">>, <<"B","E2">> >>)
NoOwnTimeout == ~(stale = <<"replay","oldTimer">> /\ history = << <<"A","E1">>, <<"B","TB">> >>)
\* A past action witness: ReplayE1 itself must commit the second match.
NoReplayAppliesPending == ~Seen("replayAppliedE2")
=============================================================================
