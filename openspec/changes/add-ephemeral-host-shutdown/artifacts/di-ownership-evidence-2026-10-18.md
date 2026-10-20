# Framework-only ownership evidence — 2026-10-18

These probes validate the framework constraints used by the proposed contract.
They implement no OrcaCore feature, reference no OrcaCore package/project, and earn
no source or issue-acceptance credit. Actual engine concurrency and package-consumer
qualification remain in tasks 3.1–3.5 after contract/canonical approval.

The source and project templates are `di-framework-probe-2026-10-18.cs.txt` and
`di-framework-probe-2026-10-18.csproj.txt`; their `.txt` suffix keeps them outside
the repository's compiled source/declaration inventory. Copy to an ordinary short
directory outside the repository as Program.cs and FrameworkProbe.csproj, then run:

```powershell
dotnet run --project E:/oc2p/FrameworkProbe.csproj -c Release
```

No Directory.Build.props existed at that scratch parent. Restore resolved Hosting
and DI 10.0.9; the observed runtime was .NET 10.0.13, SDK 10.0.402. Exact dependency
archive SHA-512 values, source SHA-256 values, OS/framework/assembly identities and
observations are in `di-framework-observations-2026-10-18.json`.

| Probe | Observed |
|---|---|
| A disposal hook created before a later dependency | Root disposal had already disposed that dependency before calling the hook |
| Generic Host RunAsync with a throwing IHostedService.StopAsync | Stop threw, and RunAsync still disposed the root dependency |
| Explicit application ownership across a zero-budget incomplete observation | Provider remained alive; actual work joined before disposal; no dependency access after disposal |
| A second CancelAsync while the first handler was blocked | Second call completed while the first task/handler was still outstanding; joining the first task required releasing the handler |
| An ApplicationStopping handler blocks an earlier observer | Its token was already cancelled while the observer callback had not run; admission must consult token state, not callback order |

The design therefore requires a real retained root/scoped/external owner on
Incomplete, rejects an imaginary engine-disposer veto, and joins the first owned
cancellation dispatch rather than trusting repeated cancellation calls. The
registered role adds no hidden provider and cannot undo arbitrary external
disposal. Blocked handlers remain truthful outstanding work, not proof of physical
stop. These are concrete contract choices for review.

Bounded WaitAsync/manual signals protect the probe itself; there is no timing sleep
or simulation of the proposed admission/join implementation. Scratch binaries and
obj output are disposable and excluded from the frozen contract manifest.

Primary framework sources checked:
[Host](https://github.com/dotnet/runtime/blob/v10.0.9/src/libraries/Microsoft.Extensions.Hosting/src/Internal/Host.cs),
[RunAsync](https://github.com/dotnet/runtime/blob/v10.0.9/src/libraries/Microsoft.Extensions.Hosting.Abstractions/src/HostingAbstractionsHostExtensions.cs),
[DI disposal](https://github.com/dotnet/runtime/blob/v10.0.9/src/libraries/Microsoft.Extensions.DependencyInjection/src/ServiceLookup/ServiceProviderEngineScope.cs),
[CancelAsync](https://github.com/dotnet/runtime/blob/v10.0.9/src/libraries/System.Private.CoreLib/src/System/Threading/CancellationTokenSource.cs).
The last source documents the early-return behavior; the executable cancellation
counterexample above independently records the locally installed runtime's behavior.
