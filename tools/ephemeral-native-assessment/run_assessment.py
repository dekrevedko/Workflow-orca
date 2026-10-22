"""Publish and execute candidate packages outside repository props discovery.

No findings are automatically approved. An unmatched diagnostic or runtime finding
remains unclassified, and the consistency gate fails until individually reviewed.
"""
import argparse
import collections
import hashlib
import json
import os
from pathlib import Path
import platform
import queue
import re
import shutil
import subprocess
import threading
import time
import xml.sax.saxutils

from gate import FLAVORS, IDS, MEASURED, error_identity, validate_platform


def digest(data): return hashlib.sha256(data).hexdigest()
def save(path, value): path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def host_identity():
    result = {"architecture": "x64" if platform.machine().lower() in ["amd64", "x86_64"] else platform.machine(),
              "platform": platform.platform(), "python": platform.python_version(), "qualified": False,
              "cpuCount": os.cpu_count(), "container": False, "os": "unqualified",
              "clockResolutionSeconds": time.get_clock_info("perf_counter").resolution,
              "allocationNote": "Observed native host/VM allocation; no exclusive hardware or cold-file-cache control"}
    result["runnerImage"] = {key: os.environ.get(key) for key in ["ImageOS", "ImageVersion", "RUNNER_OS", "RUNNER_ARCH"]}
    if os.name == "nt":
        caption = subprocess.check_output(["powershell", "-NoProfile", "-Command", "(Get-CimInstance Win32_OperatingSystem).Caption"], text=True).strip()
        result["caption"] = caption
        result["memoryBytes"] = int(subprocess.check_output(["powershell", "-NoProfile", "-Command", "(Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory"], text=True).strip())
        if "Server" in caption and "2025" in caption: result.update(os="windows-2025", qualified=True)
    else:
        release = dict(line.split("=", 1) for line in Path("/etc/os-release").read_text(encoding="utf-8").splitlines() if "=" in line)
        result["release"] = release
        result["memoryBytes"] = os.sysconf("SC_PHYS_PAGES") * os.sysconf("SC_PAGE_SIZE")
        limit = Path("/sys/fs/cgroup/memory.max")
        result["cgroupMemoryMax"] = limit.read_text(encoding="utf-8").strip() if limit.exists() else "not available"
        result["container"] = Path("/.dockerenv").exists() or Path("/run/.containerenv").exists()
        probe = subprocess.run(["systemd-detect-virt", "--container"], capture_output=True, text=True)
        result["container"] |= probe.returncode == 0
        wsl = "microsoft" in platform.release().lower() or "wsl" in platform.release().lower()
        if release.get("ID", "").strip('"') == "ubuntu" and release.get("VERSION_ID", "").strip('"') == "24.04" and not wsl and not result["container"]:
            result.update(os="ubuntu-24.04", qualified=True)
    result["qualified"] &= result["architecture"] == "x64"
    return result


def command(args, log, env, timeout=1200):
    with log.open("w", encoding="utf-8") as output:
        result = subprocess.run(args, stdout=output, stderr=subprocess.STDOUT, env=env, timeout=timeout)
    return result.returncode


def diagnostics(log, app, cache):
    rows = collections.OrderedDict()
    pattern = re.compile(r"^(.*?)\b(warning|error)\s+([A-Za-z]+\d+)?\s*:\s*(.*)$")
    for original in log.read_text(encoding="utf-8", errors="replace").splitlines():
        match = pattern.search(original)
        if not match: continue
        location, severity, code, message = match.groups()
        location = location.rstrip(" :") or "native-tool"
        code = code or "NATIVE-ERROR"
        # Normalize only explicitly named filesystem roots, retaining the original line.
        normalized = original.replace(str(app), "<consumer>").replace(str(cache), "<nuget>")
        identity = digest(normalized.encode("utf-8"))
        if identity not in rows:
            analysis = re.match(r"(?:Trim|AOT) analysis ([^:]+):", message)
            member = analysis.group(1) if analysis else "not separately supplied; exact diagnostic message retained"
            # The codec/converters live in assembly OrcaCore despite their Core.Internal namespace.
            assembly = ("OrcaCore" if member.startswith(("OrcaCore.Core.Internal.FixedWorkflowValueCodec", "OrcaCore.Core.Internal.WorkflowFailureJsonConverter"))
                        else "OrcaCore.Engine.Ephemeral" if member.startswith("OrcaCore.Engine.Ephemeral.")
                        else "OrcaCore.Core" if member.startswith("OrcaCore.Core.")
                        else Path(location).stem if location.lower().endswith(".dll") else "NativeConsumer" if ".cs(" in location else "native tool / exact original retained")
            referenced = re.search(r"(?:Using member |call to |Call to )'([^']+)'", message)
            rows[identity] = dict(identity=identity, code=code, severity=severity, message=message,
                                  location=location, normalized=normalized, originals=[], count=0,
                                  emittingAssembly=assembly, member=member,
                                  referencedMember=referenced.group(1) if referenced else "see exact message")
        rows[identity]["count"] += 1
        rows[identity]["originals"].append(original)
    return list(rows.values())


def child(executable, args, destination):
    events = queue.Queue()
    started = time.perf_counter_ns()
    stderr_path = destination.with_suffix(".stderr.log")
    with stderr_path.open("w", encoding="utf-8") as stderr:
        process = subprocess.Popen([str(executable), *args], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                   stderr=stderr, text=True, encoding="utf-8", errors="replace")
        def read():
            for line in process.stdout: events.put(line)
            events.put(None)
        reader = threading.Thread(target=read, daemon=True); reader.start()
        lines, report, ready, quiescent = [], None, None, None
        deadline = time.monotonic() + 90
        try:
            while True:
                line = events.get(timeout=max(0.01, deadline - time.monotonic()))
                if line is None: break
                lines.append(line)
                if line.strip() == "ORCA_READY": ready = (time.perf_counter_ns() - started) / 1_000_000
                if line.strip() == "ORCA_QUIESCENT": quiescent = (time.perf_counter_ns() - started) / 1_000_000
                if line.startswith("ORCA_REPORT "):
                    report = json.loads(line[len("ORCA_REPORT "):])
                    if "--measure" in args:
                        process.stdin.write("release\n"); process.stdin.flush()
            exit_code = process.wait(timeout=max(0.01, deadline - time.monotonic()))
        except (queue.Empty, subprocess.TimeoutExpired):
            process.kill(); process.wait()
            raise RuntimeError("Harness watchdog expired; this is not an unsupported compatibility outcome")
        finally:
            process.stdin.close(); process.stdout.close(); reader.join(timeout=2)
    destination.with_suffix(".stdout.log").write_text("".join(lines), encoding="utf-8")
    if report is None:
        raise RuntimeError("Consumer did not emit a structured report; preserve raw output and diagnose the harness/toolchain")
    save(destination.with_suffix(".json"), report)
    return dict(exitCode=exit_code, report=report, startupMs=ready, quiescentElapsedMs=quiescent)


def native_closure(executable, output, env):
    if os.name != "nt":
        for tool, args in [("ldd", [str(executable)]), ("file", [str(executable)])]:
            command([tool, *args], output / (tool + ".log"), env)
    else:
        vswhere = Path(os.environ.get("ProgramFiles(x86)", "C:/Program Files (x86)")) / "Microsoft Visual Studio/Installer/vswhere.exe"
        found = subprocess.check_output([str(vswhere), "-latest", "-products", "*", "-requires", "Microsoft.VisualStudio.Component.VC.Tools.x86.x64", "-property", "installationPath"], text=True).strip()
        dumpbins = sorted(Path(found).glob("VC/Tools/MSVC/*/bin/Hostx64/x64/dumpbin.exe"))
        if not dumpbins: raise RuntimeError("Native import inventory requires installed x64 VC tools")
        command([str(dumpbins[-1]), "/DEPENDENTS", str(executable)], output / "native-imports.log", env)


def apply_classifications(cell, known):
    diagnostic_map = {row["identity"]: row for row in known.get("diagnostics", [])}
    for row in cell["diagnostics"]:
        classification = diagnostic_map.get(row["identity"], {})
        for key in ["disposition", "rationale", "proof", "expectedCount", "affectedCases"]:
            row[key] = classification.get(key)
    cell["observedDiagnosticCounts"] = {row["identity"]: row["count"] for row in cell["diagnostics"]}
    # Keep a missing prior identity in the ledger with its actual zero count.
    # Only an explicit Corrected/expectedCount=0 decision can accept its absence.
    observed_ids = set(cell["observedDiagnosticCounts"])
    for identity, prior in diagnostic_map.items():
        if identity not in observed_ids:
            row = dict(prior)
            row.update(count=0, originals=[])
            cell["diagnostics"].append(row)
    runtime_map = {row["id"]: row for row in known.get("runtimeFindings", [])}
    cell["runtimeFindings"] = []
    for case in cell["cases"]:
        if case["outcome"] != "FailedBehavior": continue
        observed = {"id": case["id"], "identity": error_identity(case)}
        prior = runtime_map.get(case["id"], {})
        for key in ["disposition", "rationale", "proof"]:
            observed[key] = prior.get(key) if prior.get("identity") == observed["identity"] else None
        cell["runtimeFindings"].append(observed)
    failures = any(case["outcome"] != "Passed" for case in cell["cases"])
    unsupported = failures or any(row.get("disposition") == "Unsupported" for row in cell["diagnostics"])
    conditional = cell["switch"] is True or any(row.get("disposition") == "LocalRequirement" for row in cell["diagnostics"])
    cell["outcome"] = "AssessedUnsupported" if unsupported else "AssessedConditional" if conditional else "AssessedCompatible"


def run(args):
    repo, work, output, feed = [p.resolve() for p in [args.repository, args.work, args.output, args.feed]]
    if work.exists(): raise ValueError("Use a fresh, unused disposable work path")
    if output == work or output.is_relative_to(work): raise ValueError("Evidence must be outside disposable work")
    work.mkdir(parents=True); output.mkdir(parents=True, exist_ok=True)
    (work / ".owned-issue-3-work").write_text("disposable native assessment\n", encoding="utf-8")
    env = {**os.environ, "DOTNET_CLI_UI_LANGUAGE": "en-US", "VSLANG": "1033", "DOTNET_CLI_TELEMETRY_OPTOUT": "1"}
    host = host_identity()
    save(output / "host.json", host)
    if not host["qualified"] and not args.preflight: raise ValueError("Required native host is unavailable; use --preflight only for unqualified local development")
    source = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=repo, text=True).strip()
    package_hashes = {p.name: digest(p.read_bytes()) for p in sorted(feed.glob("*.nupkg"))}
    if len(package_hashes) != 12: raise ValueError("Pack the exact twelve candidate packages first")
    shutil.copyfile(repo / "global.json", work / "global.json")
    command(["dotnet", "--info"], output / "dotnet-info.log", env)
    if os.name != "nt":
        for tool in ["clang", "cc", "ld", "ldd"]:
            if shutil.which(tool): command([tool, "--version"], output / (tool + "-version.log"), env)
    known_path = repo / "tools/ephemeral-native-assessment/classifications.json"
    known = json.loads(known_path.read_text(encoding="utf-8")) if known_path.exists() else {}
    report = dict(schemaVersion=1, sourceSha=source, host=host, packageHashes=package_hashes, cells=[],
                  claim="Profile-specific candidate assessment, pending independent review; no product support promise")
    tool_root = repo / "tools/ephemeral-native-assessment"
    report["runtimeEnvironment"] = {key: os.environ.get(key) for key in [
        "DOTNET_gcServer", "DOTNET_GCServer", "COMPlus_gcServer", "DOTNET_gcConcurrent", "DOTNET_GCConcurrent", "COMPlus_gcConcurrent",
        "DOTNET_TieredCompilation", "COMPlus_TieredCompilation", "DOTNET_TieredPGO", "COMPlus_TieredPGO",
        "DOTNET_TC_QuickJit", "COMPlus_TC_QuickJit", "DOTNET_ReadyToRun", "COMPlus_ReadyToRun"]}
    report["workloadIdentity"] = dict(measuredCases=sorted(MEASURED), caseSourceSha256=digest((tool_root / "consumer/Cases.cs").read_bytes()),
        inputScope="Literal DTO/value/item payloads and generic shapes in Cases.cs; fresh workflow/instance/operation identities per process are not equal-input hashes")
    report["harnessFiles"] = {p.relative_to(tool_root).as_posix(): digest(p.read_bytes()) for p in sorted(tool_root.rglob("*"))
                              if p.is_file() and "__pycache__" not in p.parts and p.suffix in [".py", ".cs", ".csproj", ".json"]}
    executables = {}
    for flavor in FLAVORS:
        print("Publishing " + flavor, flush=True)
        extra = flavor.endswith("-json-reflection")
        app = work / flavor / "app"; app.mkdir(parents=True)
        for path in (repo / "tools/ephemeral-native-assessment/consumer").iterdir():
            if path.is_file(): shutil.copyfile(path, app / path.name)
        config = '<configuration><packageSources><clear/><add key="candidate" value="' + xml.sax.saxutils.escape(str(feed), {'"': '&quot;'}) + '"/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources></configuration>\n'
        (app / "NuGet.Config").write_text(config, encoding="utf-8")
        cache = work / flavor / "cache"; publish = work / flavor / "publish"
        cell_output = output / flavor; cell_output.mkdir()
        rid = "win-x64" if os.name == "nt" else "linux-x64"
        native = flavor.startswith("aot"); trimmed = flavor != "jit"
        properties = dict(PublishAot=native, PublishTrimmed=trimmed, TrimMode="full", TrimmerSingleWarn=False,
                          TreatWarningsAsErrors=False, ILLinkTreatWarningsAsErrors=False)
        if extra: properties["JsonSerializerIsReflectionEnabledByDefault"] = True
        invocation = ["dotnet", "publish", str(app / "NativeConsumer.csproj"), "-c", "Release", "-r", rid,
                      "--self-contained", "true", "--nologo", "-v", "minimal", "-clp:NoSummary", "-o", str(publish),
                      "-p:RestorePackagesPath=" + str(cache)]
        invocation += ["-p:" + key + "=" + (str(value).lower() if isinstance(value, bool) else value) for key, value in properties.items()]
        save(cell_output / "publish-command.json", invocation)
        exit_code = command(invocation, cell_output / "publish.log", env)
        observed = diagnostics(cell_output / "publish.log", app, cache)
        cell = dict(flavor=flavor, switch=True if extra else None, publishProperties=properties,
                    publishExitCode=exit_code, diagnostics=observed, cases=[], effectiveReflection=None,
                    localRequirements=([dict(name="JsonSerializerIsReflectionEnabledByDefault", value=True, disposition="LocalRequirement",
                        status="Candidate only; sufficiency is not established if any required journey fails",
                        scope="This exact additional consumer artifact; no generated generic code or private metadata is promised",
                        proof="publish-command.json and functional.json", replacesDefault=False)] if extra else []),
                    measurements={"unavailable": "fixed successful workload is not executable", "warmups": [], "samples": []})
        assets = app / "obj/project.assets.json"
        if assets.exists():
            shutil.copyfile(assets, cell_output / "project.assets.json")
            libraries = json.loads(assets.read_text(encoding="utf-8"))["libraries"]
            selected = {key.split('/')[0] for key in libraries if key.startswith("OrcaCore")}
            if selected != {"OrcaCore", "OrcaCore.Core", "OrcaCore.Engine.Ephemeral"}:
                raise ValueError("Unexpected selected package closure: " + str(selected))
            cell["managedDependencyClosure"] = {key: dict(type=value["type"], sha512=value.get("sha512")) for key, value in libraries.items()}
        if exit_code == 0:
            executable = publish / ("NativeConsumer.exe" if os.name == "nt" else "NativeConsumer")
            artifacts = {p.relative_to(publish).as_posix(): dict(bytes=p.stat().st_size, sha256=digest(p.read_bytes())) for p in sorted(publish.rglob("*")) if p.is_file()}
            save(cell_output / "artifact-closure.json", artifacts)
            native_closure(executable, cell_output, env)
            runtimeconfig = publish / "NativeConsumer.runtimeconfig.json"
            cell["emittedRuntimeConfig"] = json.loads(runtimeconfig.read_text(encoding="utf-8")) if runtimeconfig.exists() else None
            if runtimeconfig.exists(): shutil.copyfile(runtimeconfig, cell_output / runtimeconfig.name)
            cell["runtimeConfigNote"] = "Exact emitted config retained when present; Native AOT may embed configuration instead of emitting a runtimeconfig file"
            result = child(executable, ["--all"], cell_output / "functional")
            cell["cases"] = result["report"]["cases"]
            cell["effectiveReflection"] = result["report"]["jsonReflectionEnabled"]
            cell["runtimeSettings"] = result["report"]["runtimeSettings"]
            loaded = {}
            for module in result["report"]["loadedModulePaths"]:
                path = Path(module)
                loaded[module] = dict(bytes=path.stat().st_size, sha256=digest(path.read_bytes())) if path.is_file() else {"unavailable": "not a regular mapped file"}
            save(cell_output / "loaded-module-closure.json", loaded)
            if all(c["outcome"] == "Passed" for c in cell["cases"] if c["id"] in MEASURED):
                executables[flavor] = executable
                cell["measurements"] = {"warmups": [], "samples": []}
        else:
            text = (cell_output / "publish.log").read_text(encoding="utf-8", errors="replace")
            cell["environmentFailure"] = any(s in text.lower() for s in ["platform linker not found", "visual studio", "unable to load the service index", "nu1301", "clang: command not found"])
            cell["cases"] = [dict(id=key, outcome="PublishFailed" if n == 0 else "BlockedUpstream", cause="publish", assertions=0, exceptions=[]) for n, key in enumerate(IDS)]
        apply_classifications(cell, known.get(host["os"], {}).get(flavor, {}))
        report["cells"].append(cell)
        save(output / "report.json", report)
        print(flavor, "publish exit", exit_code, "passed cases", sum(c["outcome"] == "Passed" for c in cell["cases"]), "diagnostic identities", len(observed), flush=True)
    # Interleave flavors within every warm-up/sample round; preserve every raw run.
    cells = {cell["flavor"]: cell for cell in report["cells"]}
    for phase, count in [("warmups", 3), ("samples", 20)]:
        for round_number in range(count):
            order = list(executables)
            order = order[round_number % max(1, len(order)):] + order[:round_number % max(1, len(order))]
            for flavor in order:
                result = child(executables[flavor], ["--measure"], output / flavor / f"{phase}-{round_number:02d}")
                sample = dict(passed=result["exitCode"] == 0 and all(c["outcome"] == "Passed" for c in result["report"]["cases"]),
                              startupMs=result["startupMs"], quiescentElapsedMs=result["quiescentElapsedMs"],
                              quiescentCounters=result["report"]["quiescentCounters"], runtimeSettings=result["report"]["runtimeSettings"], round=round_number, order=order)
                cells[flavor]["measurements"][phase].append(sample)
                save(output / "report.json", report)
    for cell in report["cells"]:
        measurements = cell["measurements"]
        if measurements.get("samples"):
            import statistics
            summary = {}
            series = {"startupMs": [s["startupMs"] for s in measurements["samples"]]}
            for key in measurements["samples"][0]["quiescentCounters"]:
                if key not in ["phase", "collectionCounts"]:
                    series[key] = [s["quiescentCounters"][key] for s in measurements["samples"]]
            for key, values in series.items(): summary[key] = dict(n=len(values), median=statistics.median(values), minimum=min(values), maximum=max(values))
            measurements["summary"] = summary
    save(output / "report.json", report)
    try: validate_platform(report)
    except ValueError as error:
        (output / "gate-status.txt").write_text(str(error) + "\n", encoding="utf-8")
        print("Assessment is incomplete/unclassified:", error, flush=True)
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ["repository", "feed", "work", "output"]: parser.add_argument("--" + name, required=True, type=Path)
    parser.add_argument("--preflight", action="store_true", help="Unqualified local development; never fills a native matrix cell")
    parser.add_argument("--keep-work", action="store_true")
    arguments = parser.parse_args()
    try:
        result = run(arguments)
        validate_platform(result)
    finally:
        work, repo, output = arguments.work.resolve(), arguments.repository.resolve(), arguments.output.resolve()
        if not arguments.keep_work and (work / ".owned-issue-3-work").is_file():
            assert (work / ".owned-issue-3-work").read_text(encoding="utf-8") == "disposable native assessment\n"
            assert work != repo and not repo.is_relative_to(work) and not output.is_relative_to(work)
            shutil.rmtree(work)
