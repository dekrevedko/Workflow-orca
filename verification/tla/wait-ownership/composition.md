# Full-bound wait ownership by target

Task 2.5 accepts complete TLC searches for both literal targets for each of the six normal configurations, subject to independent review of their combined coverage. The original joint configurations remain reproducible. The two methods have different state counts.

## Projection from a joint state

Keep the observer's occurrence, reservation, route, queue, timer, consumption, failure and cancellation state. Keep the complete physical held set, index and count for both keys. For the peer, represent each unused slot as new, each held slot as settled, and each retired slot as done. The settled marker denotes an outstanding reservation only. Start with both concrete slots unused on each side. Check observers 1 and 2 independently for contention, each of the four key differences, and progress. Use the original two event identities, two attempts per event per target, two slots per target and one escape per slot. The searches use no symmetry and retain the original progress fairness assumptions for the observer.

The observer executes the source actions, including every lookup key, returning continuations, registration rejection, timer dispatch and stale callback. A peer can reserve either unused slot when the physical index permits, release one held slot, or clear its held group. These transitions retain exact physical identities, counts and owners. The envelope permits extra histories, for example dropping an overlapping successor first. Universal passes transfer through forward simulation. An abstract failing trace needs concrete replay.

## Why two searches cover a joint execution

Map the initial joint state to each initial factor. For an actor step, execute its source action in its own factor. In the opposite factor, the same step must induce a permitted reserve, drop or stutter. Use simultaneous induction on the length of a joint prefix. The first violating transition must be checked using its reachable pre-state. Assuming the desired property of the next state would make this argument circular.

| Source actions | Effect visible to the opposite target |
|---|---|
| Register, Successor | Add one new, exactly identified slot. Register checks the index. Successor already holds the predecessor's reservation. |
| Release, Cleanup | Remove the released slot or the actor's live group. Empty cleanup preserves a competing owner's entry. |
| Other actor actions | Physical holds, index, count and slot availability to the other actor stay fixed. StaleTimer captures two entire tuples. In the ordinary source transition their equality is preserved because the phase map stays fixed. |
| Lookup, Observe, Enqueue | Retain the observer's separate observations of the physical index and publication. The publication flag belongs to the target. Failed or stale observations still consume their delivery attempt. |

All per-occurrence writes address the acting instance. Deliver selects from that instance's active set. Successor checks the two slots have the same instance. Cleanup's whole maps select only its live set, and StaleTimer's victim is drawn from its own active set. Per-target maps are updated only at the acting index. These facts establish the frame for arbitrary values on the hidden side. Reads of the other side in registration diagnostics affect witness bookkeeping only. The exact model and contract are recorded with the evidence so that action changes require this audit again.

The normal view retains the equality of the full before/after callback tuples. No guard reads the tuples. Idle router scratch is overwritten by the next Lookup. A false observation can only remain false at Observe and cannot enqueue at Enqueue. Witness and cancellation-stage diagnostics are discarded only in normal checks. In particular, Reject followed by Cleanup can set the diagnostic bit even in a normal profile.

Each safety property local to an occurrence or target is checked with that target as observer. Both views retain the same physical owner, exact count and held slots. Each actual active occurrence must be held; one index owner per key and local active uniqueness therefore imply global active uniqueness. A nonquiet observer must have an enabled local step. Two quiet targets enable the joint quiescent action.

## Fair progress

For each observer, keep the source weak-fair actions for Register, Settle, Release, Deliver, Cleanup and Return. Their enabledness depends only on the retained local state and shared index. Each successful action changes a retained phase, queue, cleanup bit or return bit. A fair concrete behavior therefore projects to a behavior satisfying those local weak-fairness assumptions. Hidden peer computation may stutter. No peer fairness is imposed in the factor. The two local settlement, cancellation and eventual eligible reuse obligations together give the original universally quantified obligations. No progress is required of an active wait that receives neither a qualifying event nor a timer.

## Reproduction

The original and factored state counts must be reported separately. Keep the original qualification controls on the unfactored source actions and require completed TLC output for every literal target and profile. Keep task 2.5 open until the evidence and source correspondence pass independent review.
