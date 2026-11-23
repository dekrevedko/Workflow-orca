## Why

The same declared workflow value can serialize as `{"Value":17}` and detach as `{"Value":0}`. This silently changes workflow input and attempt state. DAG run creation already checks the submitted representation. Its rule belongs in the shared fixed codec.

## What Changes

- **BREAKING**. Values whose encoded representation cannot be reconstructed under their declared type are unsupported. This includes values omitted by a serializer attribute when a constructor or initializer restores a different representation.
- Apply the check at submission, state replacement and detachment. Keep failure attribution at the boundary that owns the value.
- Preserve the DAG facade's distinction between a mismatched installed contract and damaged stored framing. Reuse its byte-stability rule for workflow and mapped DAG inputs.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `workflow-contracts`: define representation fidelity and the effect of conditional omission under both metadata modes.
- `workflow-authoring`: apply the rule to attempt-local snapshots and replacement state.
- `developer-facing-surface`: extend the landed DAG facade rule to the shared codec.
- `quality-and-verification`: update the native omission controls and require regression coverage at public boundaries.

## Impact

The change builds on master at `499b9018` and requires matched binaries. The fixed JSON format, type keys and bytes of faithful values retain their existing meaning. Applications using a lossy model can add a JSON-bindable constructor or a writable member, or remove an omission policy that cannot reproduce its input. Workflow code must return state that the declared codec contract can reconstruct.
