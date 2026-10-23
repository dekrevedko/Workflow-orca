# Public ephemeral native publishing assessment

Status (2026-10-21): task 2 candidate with native Windows/Ubuntu evidence, pending
independent review. Contract checkpoint `eea51c9`, verdict-only child `dbd829e`. Product code,
public API, dependencies, fixed codec and all executable baselines are unchanged.
This is optional research; no Native AOT/trimming support badge is claimed.

The external consumer has only public PackageReferences. The runner copies it
outside repository build-props discovery, restores from an explicit candidate
feed and nuget.org into fresh per-cell caches, and executes published binaries.
Never compile the consumer in place under the repository's central package props.

The required native hosts are Windows Server 2025 x64 and Ubuntu 24.04 LTS x64.
Native VMs qualify; Docker, WSL, cross-publication and Windows 11 preflight do not.
The workflow packs one exact twelve-package feed and downloads the same bytes to
both native jobs; the selected consumer resolves only three OrcaCore packages.

Each OS records five distinct cells, in this order:

| Cell | Publish switches | JSON reflection | Best possible conclusion |
|---|---|---|---|
| jit | self-contained, untrimmed | SDK default, property unset | Profile-specific compatible |
| trimmed | self-contained, full trim | SDK default, property unset | Profile-specific compatible |
| aot | self-contained, Native AOT | SDK default, property unset | Profile-specific compatible |
| trimmed-json-reflection | full trim | explicitly true | Conditional |
| aot-json-reflection | Native AOT | explicitly true | Conditional |

Defaults are never replaced by extras. Exact definition/public errors and inner
exception chains are retained even when JSON metadata failure is reported as an
unsupported declared type. Every compiler/trimmer/AOT/native finding is retained,
with no `NoWarn`, private/whole-product rooting, codec resolver or product fix.
The classification file records each observed diagnostic and runtime failure with
its own exact identity/count, rationale and proof; it began empty for discovery. The
record gate fails for missing native hosts, unknown/changed findings, a broken JIT
control, missing scenario IDs, unsupported claims or incomplete measurements.

The sixteen assertions journeys are defined by the
[task 2 protocol](../../openspec/changes/assess-ephemeral-native-aot/artifacts/task-2-consumer-protocol-2026-10-21.md).
Steps are named public DI classes. Synchronization uses callbacks, explicit signals
and an app-owned TimeProvider, never sleeps. Fanout bodies cooperate by parking at
public Delay nodes; the consumer does not demand simultaneous unfenced bodies in
one instance. The size-limit case exercises the actual 256 KiB ForEach snapshot
boundary (`SFE-LIMIT-011`), before any item body; root-input fingerprinting alone is
not asserted to have that size cap. Strong-value internal spaces survive; invalid
leading/trailing whitespace is rejected by the existing public factory.

Run the negative controls first:

```powershell
python -B -X utf8 -m unittest discover -s tools/ephemeral-native-assessment -p test_gate.py
```

After a Release warnings-as-errors solution build and exact twelve-package pack:

```powershell
python -B -X utf8 tools/ephemeral-native-assessment/run_assessment.py `
  --repository <repository> --feed <exact-candidate-feed> `
  --work <fresh-disposable-directory> --output <retained-evidence-directory>
```

The work path must not exist; evidence must be outside it. Owned build/cache/
publish output is removed in `finally`, retaining compact raw evidence. Add
`--preflight` for local development on an unqualified host; it still fails native
qualification and cannot fill a required cell. `--keep-work` is a debugging option
that deliberately retains disk output and requires later owned-path cleanup.

For executable fixed workloads the runner retains three warm-ups and twenty
measured runs, interleaving flavors by round. Startup is external process creation
to the flushed initialized-engine marker. CPU is cumulative user+kernel CPU at the
quiescent observation, before owner disposal; this endpoint is explicit and is not
claimed to include subsequent disposal/process teardown. Process working set/RSS,
Windows private commit/Linux private resident bytes, no-forced-GC managed estimates
and last-collection heap/counts remain separate. Raw samples and median/range
denominators are retained. An unexecutable fixed workload has unavailable metrics.

The native workflow is `.github/workflows/ephemeral-native-assessment.yml`. It can
run on a separately authorized temporary evidence branch; this candidate does not
authorize a push, PR or issue edit. Initial discovery intentionally failed for missing classifications. The final
classified run must reproduce every identity/count and pass both native records.
Its bounded conclusions and raw receipt catalogue are in
[the assessment report](../../openspec/changes/assess-ephemeral-native-aot/artifacts/task-2-assessment-report-2026-10-21.md).
