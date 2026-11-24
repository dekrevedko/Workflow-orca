## Why

The original provider contract specifies a versioned governance stream. Checking only that stream permits a stale owner to acquire or promote a ticket after its workflow has committed cancellation. The two-worker rollback also demonstrated why an append conflict cannot authorize releasing a shared ticket.

## What Changes

Record the additional provider capability used for scoped grants and the observable outcomes of rollback, closure and receipt replay. Align DR-038, AC-527, the provider contract matrix, the architecture decision and the lane-host guide with the implemented repair.

## Impact

Provider authors and decorators that admit scoped resource grants must support a checkpoint-guarded governance append. The original base SPI remains available for ordinary governance writes. The implementation and both SQL certifications are recorded at `2ddc2cf4`.
