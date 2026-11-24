------------------------ MODULE DurableStepOutcome ------------------------
EXTENDS Naturals, Sequences

CONSTANTS MaxAttempts, Mutation
ASSUME MaxAttempts \in {1, 2}

Workers == {"A", "B"}
Operation == "visit"
Empty == "original"
VARIABLES now, row, journal, local, abandoned, lateAfterTimeout
vars == <<now, row, journal, local, abandoned, lateAfterTimeout>>

Blank == [phase |-> "Idle", op |-> "none", ordinal |-> 0, deadline |-> 0,
          version |-> 0, readAt |-> 0, afterLoss |-> FALSE, base |-> Empty]

\* Admission version 1 is durable before any invocation. Each later append
\* stands for a winning policy command, regardless of its provider event count.
Init ==
    /\ now = 0
    /\ row = [phase |-> "InFlight", op |-> Operation, ordinal |-> 1,
              deadline |-> 1, version |-> 1, state |-> Empty]
    /\ journal = <<>>
    /\ local = [w \in Workers |-> Blank]
    /\ abandoned = [w \in Workers |-> {}]
    /\ lateAfterTimeout = FALSE

Tick ==
    /\ now < row.deadline
    /\ now' = now + 1
    /\ UNCHANGED <<row, journal, local, abandoned, lateAfterTimeout>>

\* Each worker takes its own snapshot. Expired recovery prepares a timeout
\* without invoking the body. "Body" represents a physical, detached invocation.
ReadAttempt(w) ==
    LET cap == IF Mutation = "RefreshDeadline" /\ w = "B"
               THEN row.deadline + 1 ELSE row.deadline
    IN  /\ row.phase = "InFlight"
        /\ local[w].phase \in {"Idle", "ConflictSuccess", "ConflictTimeout"}
        /\ local' = [local EXCEPT ![w] =
              [phase |-> IF now < cap THEN "Body" ELSE "Timeout",
               op |-> row.op, ordinal |-> row.ordinal, deadline |-> cap,
               version |-> row.version, readAt |-> now,
               afterLoss |-> local["A"].phase = "Gone", base |-> row.state]]
        /\ UNCHANGED <<now, row, journal, abandoned, lateAfterTimeout>>

SelectSuccess(w) ==
    /\ local[w].phase = "Body"
    /\ now < local[w].deadline
    /\ local' = [local EXCEPT ![w].phase = "Success"]
    /\ UNCHANGED <<now, row, journal, abandoned, lateAfterTimeout>>

SelectTimeout(w) ==
    /\ local[w].phase = "Body"
    /\ now >= local[w].deadline
    /\ local' = [local EXCEPT ![w].phase = "Timeout"]
    /\ abandoned' = [abandoned EXCEPT ![w] = @ \cup {local[w].ordinal}]
    /\ UNCHANGED <<now, row, journal, lateAfterTimeout>>

\* The workers produce distinguishable bytes even for the same invocation
\* coordinate. A timeout must preserve the snapshot's original committed bytes.
Bytes(w, a) == <<w, a.ordinal, a.base>>

Submit(w) ==
    LET a == local[w]
        success == a.phase = "Success"
        retry == ~success /\ (a.ordinal < MaxAttempts \/ Mutation = "ExtraRetry")
        result == IF success THEN Bytes(w, a) ELSE a.base
        entry == [worker |-> w, kind |-> a.phase, op |-> a.op,
                  ordinal |-> a.ordinal, deadline |-> a.deadline,
                  expected |-> a.version, at |-> now, bytes |-> result]
    IN  /\ a.phase \in {"Success", "Timeout"}
        /\ IF a.version = row.version \/ Mutation = "SkipCAS"
           THEN /\ journal' = Append(journal, entry)
                /\ row' = [phase |-> IF retry THEN "InFlight"
                                     ELSE IF success THEN "Completed" ELSE "Failed",
                           op |-> a.op, ordinal |-> IF retry THEN a.ordinal + 1 ELSE a.ordinal,
                           deadline |-> IF retry THEN now + 1 ELSE a.deadline,
                           version |-> row.version + 1, state |-> result]
                /\ local' = [local EXCEPT ![w].phase = "AwaitAck"]
           ELSE /\ UNCHANGED <<journal, row>>
                /\ local' = [local EXCEPT ![w].phase =
                        IF success THEN "ConflictSuccess"
                        ELSE IF local["A"].phase = "LostAck" THEN "ConflictAfterLoss"
                        ELSE "ConflictTimeout"]
        /\ UNCHANGED <<now, abandoned, lateAfterTimeout>>

Acknowledge(w) ==
    /\ local[w].phase = "AwaitAck"
    /\ local' = [local EXCEPT ![w] = Blank]
    /\ UNCHANGED <<now, row, journal, abandoned, lateAfterTimeout>>

\* A dies with either an uncertain in-flight invocation or no acknowledgement
\* for a committed success. B has independent access to the durable stream.
CrashBeforeWrite ==
    /\ local["A"].phase \in {"Body", "Success", "Timeout"}
    /\ local' = [local EXCEPT !["A"].phase = "Gone"]
    /\ UNCHANGED <<now, row, journal, abandoned, lateAfterTimeout>>

LoseSuccessAck ==
    /\ local["A"].phase = "AwaitAck"
    /\ row.phase = "Completed"
    /\ journal[Len(journal)].worker = "A"
    /\ local' = [local EXCEPT !["A"].phase = "LostAck"]
    /\ UNCHANGED <<now, row, journal, abandoned, lateAfterTimeout>>

ReadTerminal ==
    /\ local["A"].phase = "LostAck"
    /\ local["B"].phase \in {"Idle", "ConflictTimeout", "ConflictSuccess", "ConflictAfterLoss"}
    /\ row.phase = "Completed"
    /\ local' = [local EXCEPT !["B"] =
            [@ EXCEPT !.phase = IF @ = "ConflictAfterLoss" THEN "SeenAfterConflict" ELSE "Seen",
                      !.base = row.state, !.version = row.version]]
    /\ UNCHANGED <<now, row, journal, abandoned, lateAfterTimeout>>

\* A token-ignoring body can return after its timeout selection, including
\* after another worker has already admitted or finished the next attempt.
LateReturn(w) ==
    \E ordinal \in abandoned[w] :
        /\ abandoned' = [abandoned EXCEPT ![w] = @ \ {ordinal}]
        /\ lateAfterTimeout' =
               (lateAfterTimeout \/ (row.phase = "Completed" /\ row.ordinal = 2 /\ ordinal = 1
                                      /\ journal[1].kind = "Timeout"))
        /\ row' = IF Mutation = "PublishLateBytes"
                  THEN [row EXCEPT !.state = <<w, ordinal, "discarded">>] ELSE row
        /\ UNCHANGED <<now, journal, local>>

Service(w) == ReadAttempt(w) \/ SelectTimeout(w) \/ Submit(w) \/ Acknowledge(w)
Next == Tick \/ CrashBeforeWrite \/ LoseSuccessAck \/ ReadTerminal
        \/ \E w \in Workers : Service(w) \/ SelectSuccess(w) \/ LateReturn(w)
Spec == Init /\ [][Next]_vars
Recovery == Service("B") \/ ReadTerminal
FairSpec == Spec /\ WF_vars(Tick) /\ WF_vars(Recovery)
NoClockSpec == Spec /\ WF_vars(Recovery)
NoRecoverySpec == Spec /\ WF_vars(Tick)

\* Derive the reference attempt from the preceding winning timeout's commit
\* time. The expected revision is the number of prior appends plus admission.
DeadlineFor(i) == IF i = 1 THEN 1 ELSE journal[i-1].at + 1
Latest == journal[Len(journal)]
IsTerminal == row.phase \in {"Completed", "Failed"}

UniqueWinner ==
    /\ row.version = 1 + Len(journal)
    /\ \A i \in 1..Len(journal) :
        /\ journal[i].ordinal = i /\ journal[i].expected = i
        /\ journal[i].op = Operation /\ journal[i].deadline = DeadlineFor(i)
        /\ (i < Len(journal) => journal[i].kind = "Timeout")

PolicyBudget ==
    /\ row.ordinal \in 1..MaxAttempts
    /\ Len(journal) <= MaxAttempts
    /\ IF Len(journal) = 0 THEN row.phase = "InFlight" /\ row.ordinal = 1
       ELSE IF Latest.kind = "Success" THEN row.phase = "Completed" /\ row.ordinal = Len(journal)
       ELSE IF Len(journal) = MaxAttempts THEN row.phase = "Failed" /\ row.ordinal = MaxAttempts
       ELSE row.phase = "InFlight" /\ row.ordinal = Len(journal) + 1

AttemptIdentity ==
    /\ row.op = Operation /\ row.deadline = DeadlineFor(row.ordinal)
    /\ \A w \in Workers :
        LET a == local[w] IN
          a.phase \notin {"Idle", "Seen", "SeenAfterConflict"} =>
              /\ a.op = Operation /\ a.ordinal = a.version
              /\ a.deadline = DeadlineFor(a.ordinal)
              /\ a.base = Empty
              /\ (a.phase \in {"Body", "Success"} => a.readAt < a.deadline)

CommittedState ==
    /\ row.state = IF Len(journal) > 0 /\ Latest.kind = "Success"
                   THEN <<Latest.worker, Latest.ordinal, Empty>> ELSE Empty
    /\ \A i \in 1..Len(journal) :
          journal[i].bytes = IF journal[i].kind = "Success"
                             THEN <<journal[i].worker, i, Empty>> ELSE Empty
    /\ (local["B"].phase \in {"Seen", "SeenAfterConflict"} =>
          row.phase = "Completed" /\ local["B"].base = row.state /\ local["B"].version = row.version)

Settles == <>IsTerminal
Reconciles == (local["A"].phase = "LostAck") ~> (local["B"].phase \in {"Seen", "SeenAfterConflict"})

\* Negated invariants below produce reachability traces.
NoReplayBefore == ~(local["B"].afterLoss /\ local["B"].phase = "Body"
                    /\ local["A"].ordinal = local["B"].ordinal)
NoExpiredRead == ~(local["B"].afterLoss /\ local["B"].phase = "Timeout"
                   /\ local["A"].readAt < local["A"].deadline
                   /\ local["B"].readAt >= local["B"].deadline
                   /\ local["A"].ordinal = local["B"].ordinal)
NoLateReturn == ~(lateAfterTimeout /\ row.phase = "Completed" /\ row.ordinal = 2)
NoLostAckRead == ~(local["A"].phase = "LostAck" /\ local["B"].phase = "SeenAfterConflict"
                   /\ local["B"].ordinal = row.ordinal /\ local["B"].readAt >= local["B"].deadline)
NoStaleSuccess == ~(row.phase = "Failed" /\ local["A"].phase = "ConflictSuccess"
                    /\ journal[1].worker = "B")
=============================================================================
