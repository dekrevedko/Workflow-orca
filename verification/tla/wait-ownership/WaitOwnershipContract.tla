----------------------- MODULE WaitOwnershipContract -----------------------
EXTENDS Naturals, FiniteSets, Sequences

\* Contract predicates do not call the protocol's guards or transition operators.
UniqueActive(s, Occ, Key(_)) ==
  \A a,b \in Occ : (s.phase[a] = "active" /\ s.phase[b] = "active" /\
                       Key(a[1]) = Key(b[1])) => a = b

Reservations(s, Occ, Keys, Key(_)) ==
  /\ \A o \in Occ : s.phase[o] \in {"active", "event", "accepted", "timer", "settled"}
                         => o \in s.held
  /\ \A k \in Keys :
       LET holders == {o \in s.held : Key(o[1]) = k} IN
       /\ s.count[k] = Cardinality(holders)
       /\ (holders = {} <=> s.index[k] = 0)
       /\ \A o \in holders : s.index[k] = o[1]

Resolutions(s, Occ, Instances, Events) ==
  /\ \A o \in Occ : s.wins[o] <= 1
  /\ \A i \in Instances, e \in Events : s.consumed[i][e] <= 1

ReleaseIntegrity(s, Occ, Keys, Key(_)) ==
  /\ \A o \in Occ : s.phase[o] \in {"done","cancelled"} => o \notin s.held
  /\ \A k \in Keys : s.count[k] = Cardinality({o \in s.held : Key(o[1]) = k})

RollbackIntegrity(s, Occ) ==
  \A o \in s.restored :
    /\ s.phase[o] = "active"
    /\ o \in s.held
    /\ s.wins[o] = 0
    /\ (s.event[o] # 0 => s.consumed[o[1]][s.event[o]] = 0)
    /\ s.timer[o]

IsolationIntegrity(s, Occ, Key(_)) ==
  /\ \A o \in Occ : s.event[o] # 0 => s.matchKey[o] = Key(o[1])
  /\ \A o \in Occ : s.phase[o] = "cancelled" => o \notin s.held
  /\ s.callbackBefore = s.callbackAfter
=============================================================================
