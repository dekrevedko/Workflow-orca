"""Record-consistency checks. A green record is never a product support promise."""
import argparse
import hashlib
import json
from pathlib import Path

IDS = [f"AOT-{n:02d}" for n in range(1, 17)]
FLAVORS = ["jit", "trimmed", "aot", "trimmed-json-reflection", "aot-json-reflection"]
MEASURED = {"AOT-01", "AOT-02", "AOT-03", "AOT-08", "AOT-09", "AOT-10", "AOT-11", "AOT-15", "AOT-16"}
MIN_ASSERTIONS = [7, 6, 9, 7, 9, 8, 8, 7, 9, 16, 9, 5, 6, 8, 6, 11]


def require(condition, reason):
    if not condition: raise ValueError(reason)


def error_identity(case):
    raw = [case.get("phase"), [{k: error.get(k) for k in ["type", "message", "code"]}
                              for error in case.get("exceptions", [])]]
    return hashlib.sha256(json.dumps(raw, ensure_ascii=False, sort_keys=True).encode("utf-8")).hexdigest()


def validate_diagnostic(row):
    count = row.get("count")
    require(type(count) is int and count >= 0 and count == row.get("expectedCount"), "diagnostic count drift")
    disposition = row.get("disposition")
    require(disposition in {"Corrected", "Unsupported", "LocalRequirement"}, "unclassified diagnostic")
    require(all(row.get(key) for key in ["identity", "code", "message", "location", "rationale", "proof"]), "incomplete individual diagnostic")
    require(count == 0 if disposition == "Corrected" else count > 0, "finding disposition contradicts its final count")
    require(isinstance(row.get("affectedCases"), list) and row["affectedCases"], "finding lacks affected cases")
    require(set(row["affectedCases"]).issubset(IDS), "unknown affected scenario")


def validate_platform(report, *, allow_test_fixture=False):
    require(report.get("schemaVersion") == 1, "unknown report schema")
    require(not report.get("testFixture") or allow_test_fixture, "synthetic records are not execution evidence")
    host = report.get("host", {})
    require(host.get("qualified") is True and host.get("architecture") == "x64", "missing native host")
    require(host.get("os") in {"ubuntu-24.04", "windows-2025"}, "unapproved OS profile")
    require(len(report.get("sourceSha", "")) == 40, "missing source commit identity")
    cells = report.get("cells", [])
    require([cell.get("flavor") for cell in cells] == FLAVORS, "missing, reordered or duplicate cell")
    for cell in cells:
        flavor = cell["flavor"]
        extra = flavor.endswith("-json-reflection")
        require(cell.get("switch") is True if extra else cell.get("switch") is None, "wrong explicit feature switch")
        require(type(cell.get("effectiveReflection")) is bool or cell.get("publishExitCode") != 0, "missing effective feature value")
        if cell.get("publishExitCode") == 0:
            require(cell["effectiveReflection"] == (extra or flavor == "jit"), "runtime feature differs from recorded SDK-default/explicit setting")
        outcome = cell.get("outcome")
        require(outcome in {"AssessedCompatible", "AssessedConditional", "AssessedUnsupported"}, "incomplete cell")
        require(not extra or outcome != "AssessedCompatible", "extra cell cannot be unconditional")
        if extra:
            conditions = cell.get("localRequirements", [])
            require(len(conditions) == 1 and conditions[0].get("name") == "JsonSerializerIsReflectionEnabledByDefault"
                    and conditions[0].get("value") is True and conditions[0].get("disposition") == "LocalRequirement"
                    and conditions[0].get("proof") and conditions[0].get("replacesDefault") is False,
                    "extra cell lacks its explicit bounded switch condition")
        require(not cell.get("environmentFailure"), "toolchain/environment failure cannot qualify")
        diagnostics = cell.get("diagnostics", [])
        require(len({d.get("identity") for d in diagnostics}) == len(diagnostics), "duplicate finding identity")
        require(cell.get("observedDiagnosticCounts", {}) == {d.get("identity"): d.get("count") for d in diagnostics if d.get("count", 0) > 0},
                "diagnostic ledger dropped or invented an observed finding")
        for diagnostic in diagnostics: validate_diagnostic(diagnostic)
        cases = cell.get("cases", [])
        require([case.get("id") for case in cases] == IDS, "missing or duplicate scenario")
        runtime = {item.get("id"): item for item in cell.get("runtimeFindings", [])}
        all_pass = True
        for case, minimum in zip(cases, MIN_ASSERTIONS):
            result = case.get("outcome")
            if result == "Passed":
                require(case.get("assertions", 0) >= minimum and not case.get("exceptions"), "pass without the required assertions")
                continue
            all_pass = False
            require(flavor != "jit", "broken untrimmed JIT control")
            require(outcome == "AssessedUnsupported", "failed scenario cannot qualify")
            if cell.get("publishExitCode") != 0:
                require(result in {"PublishFailed", "BlockedUpstream"} and case.get("cause") == "publish", "missing publish cause")
                require(any(d["disposition"] == "Unsupported" for d in diagnostics), "unclassified publication failure")
            else:
                require(result == "FailedBehavior" and case.get("exceptions"), "missing runtime failure")
                finding = runtime.get(case["id"], {})
                require(finding.get("disposition") == "Unsupported" and finding.get("identity") == error_identity(case), "unclassified or changed runtime failure")
                require(finding.get("rationale") and finding.get("proof"), "runtime finding lacks individual evidence")
                require(not any(e.get("type") == "System.TimeoutException" or e.get("message", "").startswith("ASSERTION:") for e in case["exceptions"]), "harness failure is not an unsupported profile")
        unsupported = any(d["disposition"] == "Unsupported" for d in diagnostics) or not all_pass
        require(not unsupported or outcome == "AssessedUnsupported", "unsupported finding hidden by a claim")
        if outcome == "AssessedCompatible":
            require(not extra and all(d["disposition"] == "Corrected" for d in diagnostics), "conditional finding hidden by compatible label")
        measurable = cell.get("publishExitCode") == 0 and all(c["outcome"] == "Passed" for c in cases if c["id"] in MEASURED)
        measurements = cell.get("measurements", {})
        if not measurable:
            require(measurements.get("unavailable") and not measurements.get("samples"), "unexecutable workload needs unavailable metrics, never zeroes")
            continue
        warmups, samples = measurements.get("warmups", []), measurements.get("samples", [])
        require(len(warmups) == 3 and len(samples) == 20, "wrong warm-up/sample denominator")
        for sample in warmups + samples: require(sample.get("passed") is True, "failed/censored sample prevents complete measurements")
        for sample in samples:
            counters = sample.get("quiescentCounters", {})
            require(counters.get("phase") == "physical join complete, bootstrap provider alive, before disposal", "invalid memory sampling phase")
            keys = ["cumulativeUserPlusKernelCpuMs", "workingSetBytes", "peakWorkingSetBytes", "estimatedManagedBytesNoForcedGc", "lastCollectionHeapBytes"]
            keys.append("windowsPrivateCommitBytes" if host["os"] == "windows-2025" else "linuxPrivateResidentBytes")
            require(type(sample.get("startupMs")) in [int, float] and sample["startupMs"] >= 0, "missing external startup time")
            require(all(type(counters.get(k)) in [int, float] and counters[k] >= 0 for k in keys), "missing or mislabeled counters")
            require(len(counters.get("collectionCounts", [])) == 3, "missing GC observation counts")
    return True


def validate_assessment(reports):
    require(len(reports) == 2, "both required native platforms must be present")
    for report in reports: validate_platform(report)
    require({r["host"]["os"] for r in reports} == {"windows-2025", "ubuntu-24.04"}, "native platform missing/duplicated")
    require(len({r["sourceSha"] for r in reports}) == 1, "platforms used different source identities")
    require(reports[0].get("packageHashes") and reports[0]["packageHashes"] == reports[1].get("packageHashes"), "platforms used different candidate package bytes")
    require(reports[0].get("harnessFiles") and reports[0]["harnessFiles"] == reports[1].get("harnessFiles"), "platforms used different consumer/harness bytes")
    return True


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("reports", nargs=2, type=Path)
    arguments = parser.parse_args()
    validate_assessment([json.loads(path.read_text(encoding="utf-8")) for path in arguments.reports])
    print("Assessment records consistent. Compatibility conclusions remain profile-specific and independently reviewed.")
