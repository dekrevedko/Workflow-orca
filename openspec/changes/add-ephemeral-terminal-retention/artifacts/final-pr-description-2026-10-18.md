Resident ephemeral hosts retained completed runtimes, state/output payloads and
start keys for their entire lifetime. This adds an optional immutable
`EphemeralTerminalRetentionPolicy` that retires settled terminal ownership by
count and monotonic age. Omitting the policy preserves accepted-instance retention.
Aborted provisional starts are fenced and released after physical settlement in
both modes.

Queries and output waits pin a coherent unit while detaching results. Retirement
expires its start binding atomically; stale handles return the existing not-found
error for their original ID. A later start can reuse the key and repeat effects,
including when count pressure retires a completed result before wait admission.

Background cleanup errors are contained and diagnosed; cancellation is delivered
before timeout completion is signalled. Timer tests use commit notifications.
The branch incorporates master through a merge commit; preserve its checkpoint
and review ancestry rather than squashing.

Validation and the final review request record strict OpenSpec, Release builds,
Infrastructure, unit/provider/integration results and engine coverage. Fifty
consecutive unfiltered `dotnet test` runs pass. The fresh package-only experiment
runs 1,024 jobs across two definitions: runtime/facade/start ownership stays at
16 with the policy, versus 1,024 without it; payload reachability, allocation and
process memory are reported separately. Real idle expiry and physical-work
protection also pass. These local candidate packages do not update NuGet.org.

Addresses #1. Issues #2 and #3 remain separate work.
