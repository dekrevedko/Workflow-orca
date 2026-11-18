## Context

The serializer's type and metadata checks answer whether it can process a declared contract. They cannot establish that a particular constructor, setter or getter preserves a particular submitted value. The existing DAG facade checks the representation by decoding and re-encoding it with the declared type.

## Decisions

Use the first encoded bytes as the authority for a submission. Decode them using the same fixed metadata, then compare the bytes produced from the decoded value with the original bytes. Where a boundary returns a detached value, use the decoded instance that passed the check. A proven fixed scalar may take an equivalent fast path. Retain existing graph, metadata, size, null and polymorphism checks. Do not consult application equality or hash codes. A computed property or immutable constructor-bound value is supported whenever the check succeeds.

On DAG reopen, check the frame, embedded key, codec format, fingerprint and JSON syntax first. With intact storage, incompatibility under an installed type remains local to that binding. If a callback produces a bad mapped input, the node owns the mapping failure. A workflow attempt owns its invalid replacement. External callers submitting an unstable representation receive a value rejection before work is accepted. An author callback can already have performed its own side effects; the guarantee covers engine-owned admission, delivery and state commits.

Definition validation cannot invoke arbitrary application getters or constructors to predict their results. Keep existing declared-closure restrictions, especially the generated synthetic-initializer restriction. For an otherwise supported shape, check the actual value when it is available. With conditional default omission and an initializer of 42, submitted zero encodes as `{}` and would decode to 42, which encodes as `{"Count":42}`. This value fails in both modes whenever it reaches the value check. Submitted seven remains valid for an ordinary mutable property. Generated mode still rejects the omittable synthetic init-only closure even when seven is supplied.

## Trade-offs

The comparison costs a decode and a second encode for values without a proven fast path. Measure detaching small state and nested collections and record the observed time and allocation cost. Rejecting all getter-only properties would also reject valid computed and constructor-bound models. JSON-tree equivalence would erase order that participates in idempotency fingerprints. The contract compares the actual wire bytes. Hidden CLR state and arbitrary changes to the meaning of application types are outside that guarantee.
