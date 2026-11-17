---------------- MODULE EphemeralWaitOwnership ----------------
EXTENDS Naturals
VARIABLE x
Init == x = 0
Next == x' = x
Spec == Init /\ [][Next]_x
Check == this is invalid TLA
Expected == [] (x = 0)
Unrelated == <> (x = 1)
===============================================================
