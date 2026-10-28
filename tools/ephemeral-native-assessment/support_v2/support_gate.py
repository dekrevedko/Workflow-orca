"""Fail-closed issue #13 qualification; historical assessment-v1 stays unchanged."""
import json
from pathlib import Path
import sys

FLAVORS = ("jit", "trimmed", "aot")
HOSTS = ("windows-2025", "ubuntu-24.04")
NATIVE_CASES = (
    "strong-metadata", "strong-default", "product-converter-options-rejected",
    "public-factory-no-seal", "graph-members-generated", "foreach-array-missing",
    "closed-typed-ownership", "closed-typed-dispatch", "registration-parity",
    "baked-enum-rejected", "handwritten-rejected", "configuration-failures",
    "authoring-seals", "pending-registration-conflict", "f1-shape-rejection",
    "product-contracts", "idempotence-and-provider-ownership",
    "missing-metadata-seals", "same-context-race", "reentrant-registration",
    "native-missing-install", "static-bootstrap", "f1-positive-generated",
    "f1-member-metadata", "unsupported-before-missing", "codec-negative-boundaries",
    "completed-constructor-context", "golden-generated", "failure-and-size-boundaries",
    "explicit-empty-converters",
    "native-first-use-race", "retention-pins-key-expiry",
    "typed-wait-and-public-values", "transient-pool-grant", "throttle-capacity-boundaries",
)
LEGACY_CASES = (
    "strong-legacy", "graph-members-legacy", "first-use-wins-race",
    "legacy-success-seals", "f1-positive-legacy", "golden-legacy", "failure-and-size-legacy",
    "legacy-failure-seals",
)
AOT_IDS = tuple(f"AOT-{n:02d}" for n in range(1, 17))
SELECTED = {"OrcaCore", "OrcaCore.Core", "OrcaCore.Engine.Ephemeral"}
GROUPS = {
    "NS-01": ["static-bootstrap", "native-missing-install"],
    "NS-02": ["idempotence-and-provider-ownership", "baked-enum-rejected", "handwritten-rejected", "product-converter-options-rejected"],
    "NS-03": ["authoring-seals", "missing-metadata-seals"],
    "NS-04": ["same-context-race", "pending-registration-conflict", "native-first-use-race", "reentrant-registration"],
    "NS-05": ["configuration-failures"],
    "NS-06": ["golden-generated", "strong-metadata", "strong-default", "f1-shape-rejection", "f1-positive-generated", "f1-member-metadata"],
    "NS-07": ["missing-metadata-seals", "foreach-array-missing", "unsupported-before-missing", "typed-wait-and-public-values"],
    "NS-08": ["codec-negative-boundaries", "failure-and-size-boundaries", "configuration-failures", "typed-wait-and-public-values"],
    "NS-09": ["closed-typed-dispatch", "throttle-capacity-boundaries"],
    "NS-10": ["AOT-08", "AOT-09", "AOT-10", "AOT-11", "typed-wait-and-public-values", "transient-pool-grant"],
    "NS-11": ["closed-typed-ownership", "retention-pins-key-expiry", "AOT-06", "AOT-07", "typed-wait-and-public-values"],
    "NS-12": ["static-bootstrap", "idempotence-and-provider-ownership"],
}
SUBCASES = {
    "NS-02a": ["baked-enum-rejected"], "NS-02b": ["handwritten-rejected"],
    "NS-02c": ["baked-enum-rejected", "handwritten-rejected"],
    "NS-02d": ["product-converter-options-rejected"],
    "NS-06a": ["f1-shape-rejection"], "NS-06b": ["f1-shape-rejection"],
    "NS-06c": ["f1-positive-generated"], "NS-06d": ["f1-positive-generated"],
    "NS-06e": ["f1-member-metadata"], "NS-06f": ["f1-shape-rejection"],
    "NS-06g": ["strong-metadata", "strong-default"],
    "NS-06h": ["public-factory-no-seal", "strong-metadata", "strong-default"],
    "NS-09a": ["closed-typed-dispatch", "throttle-capacity-boundaries"],
}
BOUNDARY = "PublicValuesTypedWaitParkingTimersRetryPoolCompositionOnly"


def require(condition, message):
    if not condition:
        raise ValueError(message)


def validate_cell(record):
    require(record.get("protocol") == 2, "Not support protocol v2")
    host = record["host"]
    require(host.get("qualified") and host.get("os") in HOSTS and
            host.get("architecture") == "x64" and not host.get("container"), "Unqualified native host")
    flavor = record["flavor"]
    require(flavor in FLAVORS, "Unexpected flavor")
    require(len(record["packageHashes"]) == 12 and set(record["selectedPackages"]) == SELECTED,
            "Wrong distribution or selected package closure")
    require(record.get("sourceDirty") is False and record.get("packageDllsVerified") is True and len(record.get("archiveDllHashes", {})) == 3,
            "Dirty native source or unbound candidate DLLs")
    require(record.get("consumerBuildPropsIsolated") is True, "Consumer inherited ambient build props")
    require(record.get("publishExitCode") == 0, "Publish failed")
    properties = record["publishProperties"]
    require(properties.get("PublishAot") == (flavor == "aot") and
            properties.get("PublishTrimmed") == (flavor != "jit") and properties.get("TrimMode") == "full" and
            properties.get("TrimmerSingleWarn") is False, "Wrong publishing profile")
    require("JsonSerializerIsReflectionEnabledByDefault" not in properties, "Reflection switch must remain unset")
    if flavor != "jit":
        require(record.get("jsonReflectionEnabled") is False, "Trimmed/AOT JSON reflection must be false")
    if flavor == "aot":
        require(record.get("dynamicCodeSupported") is False, "AOT must execute a native artifact")
    for command in record["commands"]:
        text = " ".join(command).lower()
        require(not any(token in text for token in ("nowarn", "trimmerroot", "rd.xml", "jsonserializerisreflectionenabledbydefault")),
                "Forbidden warning/root/reflection override")
    cases = record["cases"]
    ids = [case["id"] for case in cases]
    required = set(AOT_IDS) | set(NATIVE_CASES)
    if flavor == "jit":
        required |= {"legacy-" + name for name in AOT_IDS} | set(LEGACY_CASES) | {"jit-missing-engine"}
    require(len(ids) == len(set(ids)) and set(ids) == required, "Missing, duplicated or unexpected assertion case")
    for case in cases:
        proof = (case.get("assertions", 0) > 0 if case["id"] in AOT_IDS or case["id"].startswith("legacy-AOT-")
                 else case.get("passMarker") == "PASS " + case["id"])
        require(case.get("outcome") == "Passed" and case.get("exitCode") == 0 and proof,
                "A required assertion did not pass: " + case["id"])
        if case["id"] not in AOT_IDS and flavor != "jit":
            require(case.get("runtimeFeatures", {}).get("reflection") is False, "Metadata control used JSON reflection")
            if flavor == "aot":
                require(case.get("runtimeFeatures", {}).get("dynamic") is False, "Metadata control was not native AOT")
    approved = record.get("diagnosticDecisions", {})
    for row in record["diagnostics"]:
        decision = approved.get(row["identity"])
        require(decision and decision.get("count") == row["count"] and
                decision.get("disposition") == "ReviewedNonProductCounterexample" and decision.get("proof"),
                "Unreviewed or changed diagnostic: " + row["identity"])
    require(set(approved) == {row["identity"] for row in record["diagnostics"]}, "Stale diagnostic approval")
    require(record.get("publicBoundary") == BOUNDARY and record.get("liveEventDeliveryQualified") is False and
            record.get("outgoingPublishingQualified") is False, "Unapproved event capability claim")
    for table, expected in (("coverage", GROUPS), ("subcases", SUBCASES)):
        require(set(record.get(table, {})) == set(expected), "Missing explicit " + table)
        for name, witnesses in expected.items():
            coverage = record[table][name]
            wanted = witnesses + (["jit-missing-engine"] if name == "NS-12" and flavor == "jit" else [])
            require(coverage.get("outcome") == "Passed" and coverage.get("witnesses") == wanted and
                    set(wanted).issubset(required), "Incomplete required " + table + ": " + name)
    require(record.get("golden") and record.get("strong"), "Missing parity corpus")
    if flavor == "jit":
        require(record["golden"] == record.get("legacyGolden") and record["strong"] == record.get("legacyStrong"),
                "Legacy/generated parity differs")


def validate_matrix(records):
    require(len(records) == 6, "Exactly six cells required")
    require({(r["host"]["os"], r["flavor"]) for r in records} ==
            {(os, flavor) for os in HOSTS for flavor in FLAVORS}, "Missing or duplicate host/profile")
    for record in records:
        validate_cell(record)
        for key in ("sourceSha", "sourceTree", "sourceFiles", "harnessFiles", "packageHashes", "golden", "strong"):
            require(record[key] == records[0][key], "Cross-cell drift: " + key)


def sample_record(os, flavor):
    """Synthetic schema fixture for negative gate controls, never native evidence."""
    ids = list(AOT_IDS) + list(NATIVE_CASES)
    if flavor == "jit":
        ids += ["legacy-" + name for name in AOT_IDS] + list(LEGACY_CASES) + ["jit-missing-engine"]
    return dict(protocol=2, host=dict(os=os, qualified=True, architecture="x64", container=False),
                flavor=flavor, publishExitCode=0,
                publishProperties=dict(PublishAot=flavor == "aot", PublishTrimmed=flavor != "jit", TrimMode="full", TrimmerSingleWarn=False),
                jsonReflectionEnabled=flavor == "jit", dynamicCodeSupported=flavor != "aot",
                sourceSha="fixture", sourceTree="fixture", sourceFiles={}, harnessFiles={},
                sourceDirty=False, packageDllsVerified=True, archiveDllHashes={str(n):"fixture" for n in range(3)},
                consumerBuildPropsIsolated=True,
                packageHashes={str(n): "fixture" for n in range(12)}, selectedPackages=sorted(SELECTED),
                cases=[dict(id=name, outcome="Passed", exitCode=0, assertions=1, passMarker="PASS " + name,
                            runtimeFeatures=dict(reflection=flavor == "jit", dynamic=flavor != "aot")) for name in ids],
                diagnostics=[], diagnosticDecisions={}, commands=[], golden="fixture", strong="fixture",
                publicBoundary=BOUNDARY, liveEventDeliveryQualified=False, outgoingPublishingQualified=False,
                coverage={name:dict(outcome="Passed", witnesses=rows + (["jit-missing-engine"] if name == "NS-12" and flavor == "jit" else [])) for name,rows in GROUPS.items()},
                subcases={name:dict(outcome="Passed", witnesses=rows) for name,rows in SUBCASES.items()},
                legacyGolden="fixture", legacyStrong="fixture")


if __name__ == "__main__":
    records = [json.loads(path.read_text(encoding="utf-8")) for folder in sys.argv[1:] for path in Path(folder).rglob("support-report.json")]
    validate_matrix(records)
    print("Six native support cells agree; independent review is still required.")
