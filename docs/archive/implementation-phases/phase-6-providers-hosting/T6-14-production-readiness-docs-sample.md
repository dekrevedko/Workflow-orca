# T6-14: Add production readiness docs and sample host

**Difficulty**: Sonnet        **Depends on**: T6-13
**Spec**: NF-021, NF-040, NF-050, PR-040        **AC**: none

## Goal
Add the non-packaging production-readiness materials for Phase 6: versioning and breaking
change policy, security review checklist, benchmark run guidance, and a sample host app that
uses the hosting registrations.

## Read first
- `docs/specs/11-non-functional-requirements.md`
- `docs/specs/13-phasing-and-open-questions.md`
- `docs/implementation/00-stack-decisions.md`
- `src/OrcaCore.Hosting/OrcaCore.Hosting.csproj`
- `benchmarks/OrcaCore.Benchmarks/README.md`
- Spec: `docs/specs/10-provider-model-and-extensibility.md` PR-040

## Deliverables
- Add production readiness docs under `docs/`
- Add sample host project under `samples/OrcaCore.SampleHost/`
- Update `OrcaCore.slnx`
- Add sample host tests or smoke checks if the sample contains executable behavior

## Tests to write FIRST
If the sample host has executable behavior, write a smoke test first:
1. `SampleHost_StartsWithInMemoryProvider_ResolvesHostedServices` - the sample host builds and resolves configured services.

## Implementation notes
Do not add package publishing, package IDs, signing, or per-package release README work.
Security docs must state that host authentication/authorization owns network exposure and
operator permissions.

## Out of scope
Public package publishing, Kubernetes adapters, multi-node lease implementation, and new
provider ports.

## Definition of done
- [ ] Sample host builds with zero warnings
- [ ] `dotnet build OrcaCore.slnx` passes with zero warnings
- [ ] Production docs cover delivery guarantees, security checklist, versioning policy, and benchmark execution
- [ ] PROGRESS.md updated; committed as "T6-14: production readiness docs and sample host"
