"""Versioned native support run. No assessment-v1 classifications grant support."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import xml.sax.saxutils
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from run_assessment import host_identity, command, diagnostics, child, native_closure
from support_gate import AOT_IDS, NATIVE_CASES, LEGACY_CASES, SELECTED, GROUPS, SUBCASES, BOUNDARY, validate_cell


def digest(data):
    return hashlib.sha256(data).hexdigest()


def save(path, value):
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")


def identities(repo):
    names = subprocess.check_output(["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"], cwd=repo).decode("utf-8").split("\0")
    source, harness = {}, {}
    for name in sorted(set(filter(None, names))):
        path = repo / name
        if name.startswith("src/") or name in ("global.json", "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "NuGet.Config"):
            source[name] = digest(path.read_bytes())
        if (name.startswith("tools/ephemeral-native-assessment/") and "__pycache__" not in name and path.suffix in (".py", ".cs", ".csproj")) or \
                name.startswith("tests/OrcaCore.DeveloperSurface.Guards/CompileFixtures/NativeMetadata/") and path.suffix == ".cs" or \
                name == ".github/workflows/ephemeral-native-support.yml":
            harness[name] = digest(path.read_bytes())
    return source, harness


def runtime_features(stdout):
    lines = [line for line in stdout.splitlines() if line.startswith("ORCA_FEATURES ")]
    if len(lines) != 1:
        return {}
    return {key: value == "true" for key, value in (part.split("=", 1) for part in lines[0][len("ORCA_FEATURES "):].split(";"))}


def materialize(repo, app, kind):
    app.mkdir(parents=True)
    if kind == "journey":
        for path in (repo / "tools/ephemeral-native-assessment/consumer").glob("*"):
            if path.is_file():
                shutil.copyfile(path, app / path.name)
        shutil.copyfile(Path(__file__).with_name("SupportContext.cs"), app / "SupportContext.cs")
        path = app / "Program.cs"
        text = path.read_text(encoding="utf-8")
        before = 'var mode = args.Length == 0 ? "--all" : args[0];'
        assert text.count(before) == 1
        text = text.replace(before, 'var legacy = args.Contains("--legacy");\nargs = args.Where(arg => arg != "--legacy").ToArray();\nSupportBootstrap.Install(legacy);\n' + before)
        path.write_text(text, encoding="utf-8", newline="\n")
    else:
        for path in (repo / "tests/OrcaCore.DeveloperSurface.Guards/CompileFixtures/NativeMetadata").glob("*.cs"):
            shutil.copyfile(path, app / path.name)
        path = app / "Program.cs"
        text = path.read_text(encoding="utf-8")
        assert text.count("\ntry\n{") == 1
        prefix = '''
if (args.Single() != "native-missing-install" &&
    (args.Single() != "authoring-seals" || !JsonSerializer.IsReflectionEnabledByDefault))
    new ServiceCollection().AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
    {
        StructuredExecution = new() { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] },
        TransientPools = []
    });
'''
        text = text.replace("\ntry\n{", prefix + "\ntry\n{", 1)
        text = text.replace('        default: throw new InvalidOperationException("Unknown scenario");',
                            '        case "native-first-use-race": await NativeExtras.FirstUseRace(); break;\n'
                            '        case "retention-pins-key-expiry": await NativeExtras.Retention(); break;\n'
                            '        case "typed-wait-and-public-values": await NativeExtras.TypedWaitAndPublicValues(); break;\n'
                            '        case "transient-pool-grant": await NativeExtras.TransientPoolGrant(); break;\n'
                            '        case "throttle-capacity-boundaries": NativeExtras.ThrottleCapacities(); break;\n'
                            '        default: throw new InvalidOperationException("Unknown scenario");')
        text = text.replace('    Console.WriteLine("PASS " + args.Single());',
                            '    Console.WriteLine("ORCA_FEATURES " + (JsonSerializer.IsReflectionEnabledByDefault ? "reflection=true" : "reflection=false") +\n'
                            '        (System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported ? ";dynamic=true" : ";dynamic=false"));\n'
                            '    Console.WriteLine("PASS " + args.Single());')
        path.write_text(text, encoding="utf-8", newline="\n")
        shutil.copyfile(Path(__file__).with_name("NativeExtras.cs"), app / "NativeExtras.cs")
        clock = (repo / "tools/ephemeral-native-assessment/consumer/Support.cs").read_text(encoding="utf-8")
        clock = clock[clock.index("public sealed class ManualClock"):].replace("ManualClock", "SupportClock")
        (app / "SupportClock.cs").write_text(clock, encoding="utf-8", newline="\n")
        shutil.copyfile(repo / "tools/ephemeral-native-assessment/consumer/NativeConsumer.csproj", app / "NativeConsumer.csproj")


def run(args):
    repo, feed, work, output = [p.resolve() for p in (args.repository, args.feed, args.work, args.output)]
    if work.exists() or output.is_relative_to(work) or repo.is_relative_to(work) or work == repo:
        raise ValueError("Use a fresh owned work directory outside source/evidence")
    work.mkdir(parents=True)
    output.mkdir(parents=True, exist_ok=False)
    (work / ".owned-issue-13-support").write_text("disposable support-v2 work\n", encoding="utf-8")
    # Stop ambient/repository MSBuild imports even during a local preflight.
    for name in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props"):
        (work / name).write_text("<Project />\n", encoding="utf-8", newline="\n")
    shutil.copyfile(repo / "global.json", work / "global.json")
    host = host_identity()
    save(output / "host.json", host)
    if not host["qualified"] and not args.preflight:
        raise ValueError("Native Server 2025/Ubuntu 24.04 x64 required")
    env = {**os.environ, "DOTNET_CLI_UI_LANGUAGE": "en-US", "VSLANG": "1033", "DOTNET_CLI_TELEMETRY_OPTOUT": "1"}
    source, harness = identities(repo)
    properties = dict(PublishAot=args.flavor == "aot", PublishTrimmed=args.flavor != "jit", TrimMode="full", TrimmerSingleWarn=False,
                      TreatWarningsAsErrors=False, ILLinkTreatWarningsAsErrors=False)
    report = dict(protocol=2, sourceSha=subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=repo, text=True).strip(),
                  sourceTree=subprocess.check_output(["git", "rev-parse", "HEAD^{tree}"], cwd=repo, text=True).strip(),
                  sourceDirty=bool(subprocess.check_output(["git", "status", "--porcelain=v1", "--untracked-files=all"], cwd=repo)),
                  consumerBuildPropsIsolated=True,
                  sourceFiles=source, harnessFiles=harness, host=host, flavor=args.flavor,
                  packageHashes={p.name: digest(p.read_bytes()) for p in sorted(feed.glob("*.nupkg"))},
                  selectedPackages=[], publishProperties=properties, publishExitCode=0, cases=[], commands=[], diagnostics=[], diagnosticDecisions={},
                  claim="Candidate execution evidence only; pending independent review and whole-target approval")
    archived = {}
    for package in sorted(SELECTED):
        with zipfile.ZipFile(feed / (package + ".0.0.0-phase0.nupkg")) as archive:
            for name in archive.namelist():
                if name.startswith("lib/net10.0/") and name.endswith(".dll"):
                    archived[Path(name).name] = digest(archive.read(name))
    report["archiveDllHashes"] = archived
    report["packageDllsVerified"] = False
    command(["dotnet", "--info"], output / "dotnet-info.log", env)
    if os.name != "nt":
        for tool in ("clang", "cc", "ld", "ldd"):
            if shutil.which(tool):
                command([tool, "--version"], output / (tool + "-version.log"), env)
    cache = work / "cache"
    env["NUGET_PACKAGES"] = str(cache)
    for kind in ("journey", "metadata"):
        app = work / kind / "app"
        published = work / kind / "publish"
        receipt = output / kind
        receipt.mkdir()
        materialize(repo, app, kind)
        config = '<configuration><packageSources><clear/><add key="candidate" value="' + xml.sax.saxutils.escape(str(feed), {'"': '&quot;'}) + '"/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources><packageSourceMapping><packageSource key="candidate"><package pattern="OrcaCore*"/></packageSource><packageSource key="nuget.org"><package pattern="*"/></packageSource></packageSourceMapping></configuration>\n'
        (app / "NuGet.Config").write_text(config, encoding="utf-8", newline="\n")
        save(receipt / "materialized-inputs.json", {p.name: digest(p.read_bytes()) for p in sorted(app.iterdir()) if p.is_file()})
        rid = "win-x64" if os.name == "nt" else "linux-x64"
        invocation = ["dotnet", "publish", str(app / "NativeConsumer.csproj"), "-c", "Release", "-r", rid,
                      "--self-contained", "true", "--nologo", "-v", "minimal", "-clp:NoSummary", "-o", str(published),
                      "-p:RestorePackagesPath=" + str(cache)]
        invocation += ["-p:" + key + "=" + (str(value).lower() if isinstance(value, bool) else value) for key, value in properties.items()]
        report["commands"].append(invocation)
        save(receipt / "publish-command.json", invocation)
        code = command(invocation, receipt / "publish.log", env)
        observed = diagnostics(receipt / "publish.log", app, cache)
        for row in observed:
            row["consumer"] = kind
        report["diagnostics"].extend(observed)
        report["publishExitCode"] = max(report["publishExitCode"], code)
        assets = app / "obj/project.assets.json"
        if assets.exists():
            shutil.copyfile(assets, receipt / "project.assets.json")
            libraries = json.loads(assets.read_text(encoding="utf-8"))["libraries"]
            selected = {key.split("/")[0] for key in libraries if key.startswith("OrcaCore")}
            if selected != SELECTED:
                raise ValueError("Unexpected selected package closure")
            report["selectedPackages"] = sorted(selected)
            cached = {str(p.relative_to(cache)): digest(p.read_bytes()) for p in cache.glob("orcacore*/0.0.0-phase0/lib/net10.0/*.dll")}
            save(receipt / "cached-product-dlls.json", cached)
            if {Path(name).name: value for name, value in cached.items()} != archived:
                raise ValueError("Restored product DLLs differ from the exact candidate archives")
            report["packageDllsVerified"] = True
        save(output / "support-report.json", report)
        if code != 0:
            continue
        executable = published / ("NativeConsumer.exe" if os.name == "nt" else "NativeConsumer")
        native_closure(executable, receipt, env)
        save(receipt / "artifact-closure.json", {p.relative_to(published).as_posix(): dict(bytes=p.stat().st_size, sha256=digest(p.read_bytes()))
                                               for p in sorted(published.rglob("*")) if p.is_file()})
        if kind == "journey":
            for legacy in ([False, True] if args.flavor == "jit" else [False]):
                key = "legacy" if legacy else "generated"
                result = child(executable, ["--all"] + (["--legacy"] if legacy else []), receipt / key)
                if not legacy:
                    report["jsonReflectionEnabled"] = result["report"]["jsonReflectionEnabled"]
                    report["dynamicCodeSupported"] = result["report"]["runtimeSettings"]["dynamicCodeSupported"]
                for case in result["report"]["cases"]:
                    report["cases"].append({**case, "id": ("legacy-" if legacy else "") + case["id"], "exitCode": 0 if case["outcome"] == "Passed" else result["exitCode"]})
        else:
            for name in NATIVE_CASES + (LEGACY_CASES if args.flavor == "jit" else ()):
                if name == "native-missing-install":
                    # Core's AOT module initializer is already installed in this
                    # multi-case executable. Exercise real absence in the isolated
                    # OrcaCore-only artifact below, never by undoing installation.
                    continue
                invocation = [str(executable), name]
                report["commands"].append(invocation)
                try:
                    result = subprocess.run(invocation, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=90)
                    code, stdout, stderr = result.returncode, result.stdout, result.stderr
                except subprocess.TimeoutExpired as error:
                    code, stdout, stderr = -1, str(error.stdout or ""), "HARNESS WATCHDOG: " + str(error)
                (receipt / (name + ".stdout.log")).write_text(stdout, encoding="utf-8", newline="\n")
                (receipt / (name + ".stderr.log")).write_text(stderr, encoding="utf-8", newline="\n")
                marker = "PASS " + name
                report["cases"].append(dict(id=name, exitCode=code, passMarker=marker if marker in stdout.splitlines() else None,
                                            outcome="Passed" if code == 0 and marker in stdout.splitlines() else "FailedBehavior", runtimeFeatures=runtime_features(stdout)))
                for tag, field in (("GOLDEN ", "legacyGolden" if name == "golden-legacy" else "golden"),
                                   ("STRONG ", "legacyStrong" if name == "strong-legacy" else "strong")):
                    for line in stdout.splitlines():
                        if line.startswith(tag):
                            value = line[len(tag):]
                            if field in report and report[field] != value:
                                raise ValueError("Within-cell parity mismatch: " + field)
                            report[field] = value
                save(output / "support-report.json", report)
                print(name, report["cases"][-1]["outcome"], flush=True)
        save(output / "support-report.json", report)
    app = work / "missing-engine" / "app"
    app.mkdir(parents=True)
    published = work / "missing-engine" / "publish"
    receipt = output / "missing-engine"
    receipt.mkdir()
    project = '''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup><ItemGroup><PackageReference Include="OrcaCore" Version="0.0.0-phase0" /></ItemGroup></Project>'''
    (app / "NativeConsumer.csproj").write_text(project, encoding="utf-8", newline="\n")
    (app / "NuGet.Config").write_text(config, encoding="utf-8", newline="\n")
    program = '''using OrcaCore;
using System.Text.Json;
var native = args.Single() == "native-missing-install";
if (native) System.AppContext.SetSwitch("System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault", false);
if (native && JsonSerializer.IsReflectionEnabledByDefault) throw new Exception("ASSERTION: bootstrap negative requires disabled reflection");
try { _ = Workflow.Ephemeral<int>(DefinitionId.New(), DefinitionVersion.Initial); throw new Exception("ASSERTION: missing installation accepted"); }
catch (InvalidOperationException error) when (native && error.Message.Contains("AddOrcaCoreEphemeralEngine", StringComparison.Ordinal) && error.Message.Contains("before authoring", StringComparison.Ordinal))
{ Console.WriteLine("PASS native-missing-install"); }
catch (InvalidOperationException error) when (!native && error.InnerException is FileNotFoundException && error.Message.Contains("explicit OrcaCore engine package", StringComparison.Ordinal) && error.Message.Contains("OrcaCore.Engine.Ephemeral", StringComparison.Ordinal))
{ Console.WriteLine("PASS jit-missing-engine"); }
Console.WriteLine("ORCA_FEATURES " + (JsonSerializer.IsReflectionEnabledByDefault ? "reflection=true" : "reflection=false") +
(System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported ? ";dynamic=true" : ";dynamic=false"));
'''
    (app / "Program.cs").write_text(program, encoding="utf-8", newline="\n")
    invocation = ["dotnet", "publish", str(app / "NativeConsumer.csproj"), "-c", "Release", "-r", rid,
                  "--self-contained", "true", "--nologo", "-v", "minimal", "-o", str(published), "-p:RestorePackagesPath=" + str(cache)]
    invocation += ["-p:" + key + "=" + (str(value).lower() if isinstance(value, bool) else value) for key, value in properties.items()]
    report["commands"].append(invocation)
    code = command(invocation, receipt / "publish.log", env)
    report["publishExitCode"] = max(report["publishExitCode"], code)
    report["diagnostics"].extend(diagnostics(receipt / "publish.log", app, cache))
    if code == 0:
        executable = published / ("NativeConsumer.exe" if os.name == "nt" else "NativeConsumer")
        shutil.copyfile(app / "obj/project.assets.json", receipt / "project.assets.json")
        missing_libraries = json.loads((app / "obj/project.assets.json").read_text(encoding="utf-8"))["libraries"]
        if {key.split("/")[0] for key in missing_libraries if key.startswith("OrcaCore")} != {"OrcaCore"}:
            raise ValueError("The missing-install control accidentally contains Core/engine packages")
        save(receipt / "materialized-inputs.json", {p.name: digest(p.read_bytes()) for p in sorted(app.iterdir()) if p.is_file()})
        for name in (["native-missing-install", "jit-missing-engine"] if args.flavor == "jit" else ["native-missing-install"]):
            invocation = [str(executable), name]
            report["commands"].append(invocation)
            result = subprocess.run(invocation, capture_output=True, text=True, encoding="utf-8", timeout=90)
            (receipt / (name + ".stdout.log")).write_text(result.stdout, encoding="utf-8", newline="\n")
            (receipt / (name + ".stderr.log")).write_text(result.stderr, encoding="utf-8", newline="\n")
            marker = "PASS " + name
            report["cases"].append(dict(id=name, exitCode=result.returncode,
                                        outcome="Passed" if result.returncode == 0 and marker in result.stdout.splitlines() else "FailedBehavior",
                                        passMarker=marker if marker in result.stdout.splitlines() else None, runtimeFeatures=runtime_features(result.stdout)))
    outcomes = {case["id"]: case["outcome"] for case in report["cases"]}
    for table, expected in (("coverage", GROUPS), ("subcases", SUBCASES)):
        report[table] = {}
        for name, witnesses in expected.items():
            rows = witnesses + (["jit-missing-engine"] if name == "NS-12" and args.flavor == "jit" else [])
            report[table][name] = dict(outcome="Passed" if all(outcomes.get(w) == "Passed" for w in rows) else "Incomplete", witnesses=rows)
    report.update(publicBoundary=BOUNDARY, liveEventDeliveryQualified=False, outgoingPublishingQualified=False)
    decisions = Path(__file__).with_name("diagnostic-decisions.json")
    if decisions.exists():
        report["diagnosticDecisions"] = json.loads(decisions.read_text(encoding="utf-8")).get(host["os"], {}).get(args.flavor, {})
    save(output / "support-report.json", report)
    validate_cell(report)
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("repository", "feed", "work", "output"):
        parser.add_argument("--" + name, required=True, type=Path)
    parser.add_argument("--flavor", choices=("jit", "trimmed", "aot"), required=True)
    parser.add_argument("--preflight", action="store_true", help="Local development only; cannot satisfy native host gate")
    arguments = parser.parse_args()
    # Disposable work is removed by the workflow's explicit owned-path cleanup after
    # all compact evidence is uploaded, or by the local git-worktree owner. This
    # runner never recursively deletes a path supplied on its command line.
    run(arguments)
