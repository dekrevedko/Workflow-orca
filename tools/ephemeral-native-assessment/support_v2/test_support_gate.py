import copy
import unittest
import tempfile
from pathlib import Path
import xml.etree.ElementTree as ET
from run_support import materialize
from support_gate import validate_cell, validate_matrix, sample_record

class SupportGateTests(unittest.TestCase):
    def test_release_package_version_is_bound_to_every_file(self):
        record = sample_record("ubuntu-24.04", "aot")
        record["packageVersion"] = "0.1.0-preview.3"
        record["packageHashes"] = {f"Package{number}.0.1.0-preview.3.nupkg": "fixture" for number in range(12)}
        validate_cell(record)
        wrong = copy.deepcopy(record)
        wrong["packageVersion"] = "0.1.0-preview.2"
        with self.assertRaises(ValueError): validate_cell(wrong)
        wrong["packageVersion"] = "../invalid"
        with self.assertRaises(ValueError): validate_cell(wrong)

    def test_release_matrix_requires_explicit_version_in_every_cell(self):
        records = [sample_record(os, flavor) for os in ("windows-2025", "ubuntu-24.04") for flavor in ("jit", "trimmed", "aot")]
        with self.assertRaises(ValueError): validate_matrix(records, expected_package_version="0.1.0-preview.3")
        for record in records:
            record["packageVersion"] = "0.1.0-preview.3"
            record["packageHashes"] = {f"Package{number}.0.1.0-preview.3.nupkg": "fixture" for number in range(12)}
        validate_matrix(records, expected_package_version="0.1.0-preview.3")
        with self.assertRaises(ValueError): validate_matrix(records, expected_package_version="0.1.0-preview.2")
        del records[-1]["packageVersion"]
        with self.assertRaises(ValueError): validate_matrix(records, expected_package_version="0.1.0-preview.3")

    def test_release_consumers_reference_only_the_requested_candidate_version(self):
        repository = Path(__file__).resolve().parents[3]
        with tempfile.TemporaryDirectory(prefix="orcacore-release-version-") as folder:
            for kind in ("journey", "metadata"):
                app = Path(folder) / kind
                materialize(repository, app, kind, "0.1.0-preview.3")
                references = [row for row in ET.parse(app / "NativeConsumer.csproj").findall(".//PackageReference")
                              if row.attrib["Include"].startswith("OrcaCore")]
                self.assertEqual({row.attrib["Include"] for row in references},
                                 {"OrcaCore", "OrcaCore.Engine.Ephemeral"})
                self.assertEqual({row.attrib["Version"] for row in references}, {"0.1.0-preview.3"})

    def test_complete_native_matrix(self):
        validate_matrix([sample_record(os, flavor) for os in ("windows-2025", "ubuntu-24.04") for flavor in ("jit", "trimmed", "aot")])

    def test_rejects_missing_or_duplicate_cell(self):
        records = [sample_record(os, flavor) for os in ("windows-2025", "ubuntu-24.04") for flavor in ("jit", "trimmed", "aot")]
        for changed in (records[:-1], records + [copy.deepcopy(records[0])]):
            with self.assertRaises(ValueError): validate_matrix(changed)

    def test_old_matrix_requires_explicit_history_validation(self):
        records = [sample_record(os, flavor, protocol=2) for os in ("windows-2025", "ubuntu-24.04") for flavor in ("jit", "trimmed", "aot")]
        validate_matrix(records, minimum_protocol=2)
        with self.assertRaises(ValueError): validate_matrix(records)

    def test_member_boundary_cases_and_witnesses_are_required(self):
        for name in ("write-only-generated", "write-only-default", "included-members-generated", "included-members-default"):
            record = sample_record("ubuntu-24.04", "aot")
            record["cases"] = [case for case in record["cases"] if case["id"] != name]
            with self.subTest(case=name):
                with self.assertRaises(ValueError): validate_cell(record)
        record = sample_record("windows-2025", "jit")
        record["cases"] = [case for case in record["cases"] if case["id"] != "included-members-legacy"]
        with self.assertRaises(ValueError): validate_cell(record)
        record = sample_record("ubuntu-24.04", "trimmed")
        record["coverage"]["NS-08"]["witnesses"].remove("included-members-generated")
        with self.assertRaises(ValueError): validate_cell(record)

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
