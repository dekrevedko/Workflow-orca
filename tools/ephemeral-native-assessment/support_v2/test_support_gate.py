import copy
import unittest
from support_gate import validate_cell, validate_matrix, sample_record

class SupportGateTests(unittest.TestCase):
    def test_complete_native_matrix(self):
        validate_matrix([sample_record(os, flavor) for os in ("windows-2025", "ubuntu-24.04") for flavor in ("jit", "trimmed", "aot")])

    def test_rejects_missing_or_duplicate_cell(self):
        records = [sample_record(os, flavor) for os in ("windows-2025", "ubuntu-24.04") for flavor in ("jit", "trimmed", "aot")]
        for changed in (records[:-1], records + [copy.deepcopy(records[0])]):
            with self.assertRaises(ValueError): validate_matrix(changed)

    def test_rejects_each_invalid_native_record(self):
        mutations = [
            lambda r: r.update(protocol=1),
            lambda r: r["host"].update(qualified=False),
            lambda r: r["host"].update(os="windows-11"),
            lambda r: r["host"].update(container=True),
            lambda r: r.update(sourceDirty=True),
            lambda r: r.update(packageDllsVerified=False),
            lambda r: r.update(consumerBuildPropsIsolated=False),
            lambda r: r.update(jsonReflectionEnabled=True),
            lambda r: r.update(dynamicCodeSupported=True),
            lambda r: r["cases"].pop(),
            lambda r: r["cases"][0].update(outcome="Skipped"),
            lambda r: r["cases"][0].update(outcome="AssessedUnsupported"),
            lambda r: r["cases"][0].update(exitCode=1),
            lambda r: r["cases"][0].update(assertions=0),
            lambda r: r["packageHashes"].pop(next(iter(r["packageHashes"]))),
            lambda r: r["selectedPackages"].append("OrcaCore.Engine.Durable"),
            lambda r: r["publishProperties"].update(JsonSerializerIsReflectionEnabledByDefault=True),
            lambda r: r["diagnostics"].append({"identity":"unreviewed", "count":1}),
            lambda r: r["coverage"]["NS-10"].update(outcome="Incomplete"),
            lambda r: r["coverage"].pop("NS-10"),
            lambda r: r["coverage"]["NS-10"].update(witnesses=["static-bootstrap"]),
            lambda r: r["subcases"].pop("NS-06e"),
            lambda r: r.update(liveEventDeliveryQualified=True),
            lambda r: r.update(outgoingPublishingQualified=True),
            lambda r: r["cases"][-1]["runtimeFeatures"].update(reflection=True),
            lambda r: r["cases"][-1]["runtimeFeatures"].update(dynamic=True),
            lambda r: r["commands"].append(["dotnet", "publish", "-p:NoWarn=IL3050"]),
            lambda r: r["commands"].append(["dotnet", "publish", "-p:TrimmerRootAssembly=OrcaCore"]),
        ]
        for mutate in mutations:
            record = sample_record("ubuntu-24.04", "aot"); mutate(record)
            with self.subTest(mutation=mutations.index(mutate)):
                with self.assertRaises(ValueError): validate_cell(record)

    def test_rejects_cross_cell_identity_and_parity_drift(self):
        for field in ("sourceSha", "sourceTree", "sourceFiles", "harnessFiles", "packageHashes", "golden", "strong"):
            records = [sample_record(os, flavor) for os in ("windows-2025", "ubuntu-24.04") for flavor in ("jit", "trimmed", "aot")]
            records[0][field] = "wrong"
            with self.subTest(field=field):
                with self.assertRaises(ValueError): validate_matrix(records)

if __name__ == "__main__": unittest.main()
