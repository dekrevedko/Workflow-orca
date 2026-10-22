"""Synthetic tamper controls, never native execution evidence."""
import copy
import unittest
from gate import validate_platform

IDS = [f"AOT-{n:02d}" for n in range(1, 17)]
FLAVORS = ["jit", "trimmed", "aot", "trimmed-json-reflection", "aot-json-reflection"]


def fixture():
    return {
        "schemaVersion": 1, "testFixture": True,
        "host": {"qualified": True, "os": "ubuntu-24.04", "architecture": "x64"},
        "sourceSha": "a" * 40,
        "cells": [{
            "flavor": flavor,
            "switch": True if flavor.endswith("-json-reflection") else None,
            "effectiveReflection": flavor == "jit" or flavor.endswith("-json-reflection"),
            "outcome": "AssessedConditional" if flavor.endswith("-json-reflection") else "AssessedCompatible",
            "publishExitCode": 0, "diagnostics": [], "runtimeFindings": [],
            "localRequirements": ([{"name": "JsonSerializerIsReflectionEnabledByDefault", "value": True, "disposition": "LocalRequirement",
                                    "proof": "synthetic control", "replacesDefault": False}] if flavor.endswith("-json-reflection") else []),
            "cases": [{"id": key, "outcome": "Passed", "assertions": 20, "exceptions": [], "observations": []} for key in IDS],
            "measurements": {"warmups": [{"passed": True} for _ in range(3)],
                "samples": [{"passed": True, "startupMs": 4, "quiescentCounters": {
                    "phase": "physical join complete, bootstrap provider alive, before disposal",
                    "cumulativeUserPlusKernelCpuMs": 3, "workingSetBytes": 100,
                    "peakWorkingSetBytes": 120, "linuxPrivateResidentBytes": 80,
                    "estimatedManagedBytesNoForcedGc": 20, "lastCollectionHeapBytes": 0,
                    "collectionCounts": [0, 0, 0]}} for _ in range(20)]},
        } for flavor in FLAVORS],
    }


class GateTests(unittest.TestCase):
    def accept(self, report):
        return validate_platform(report, allow_test_fixture=True)

    def reject(self, mutate):
        report = fixture(); mutate(report)
        with self.assertRaises(ValueError): self.accept(report)

    def warning(self, report, *, disposition="LocalRequirement", count=1, expected_count=1):
        cell = report["cells"][1]
        row = {"identity": "b" * 64, "code": "IL2026", "message": "synthetic warning",
               "location": "fixture/PublicApi.dll", "rationale": "bounded synthetic fixture", "proof": "unit fixture only",
               "affectedCases": ["AOT-01"], "count": count, "expectedCount": expected_count, "disposition": disposition}
        cell.update(diagnostics=[row], observedDiagnosticCounts={row["identity"]: count}, outcome="AssessedConditional")
        return cell

    def test_valid_synthetic_record(self):
        self.assertTrue(self.accept(fixture()))

    def test_fixture_is_never_execution_evidence(self):
        with self.assertRaises(ValueError): validate_platform(fixture())

    def test_missing_scenario(self):
        self.reject(lambda r: r["cells"][0]["cases"].pop())

    def test_broken_jit_control(self):
        self.reject(lambda r: r["cells"][0]["cases"][0].update(outcome="FailedBehavior"))

    def test_extra_cannot_be_unconditional(self):
        self.reject(lambda r: r["cells"][-1].update(outcome="AssessedCompatible"))

    def test_default_cannot_enable_switch(self):
        self.reject(lambda r: r["cells"][1].update(switch=True))

    def test_unclassified_warning(self):
        report = fixture(); self.warning(report, disposition=None)
        with self.assertRaisesRegex(ValueError, "unclassified diagnostic"): self.accept(report)

    def test_changed_warning_count(self):
        report = fixture(); self.warning(report, count=2, expected_count=1)
        with self.assertRaisesRegex(ValueError, "diagnostic count drift"): self.accept(report)

    def test_missing_native_host(self):
        self.reject(lambda r: r["host"].update(qualified=False))

    def test_shortened_measurements(self):
        self.reject(lambda r: r["cells"][0]["measurements"]["samples"].pop())

    def test_false_managed_equals_process_metric(self):
        self.reject(lambda r: r["cells"][0]["measurements"]["samples"][0]["quiescentCounters"].update(phase="managed heap equals RSS"))

    def test_pass_without_assertions(self):
        self.reject(lambda r: r["cells"][0]["cases"][0].update(assertions=0))

    def test_dropped_diagnostic(self):
        report = fixture(); self.warning(report)["diagnostics"].clear()
        with self.assertRaisesRegex(ValueError, "dropped or invented"): self.accept(report)

    def test_extra_loses_its_feature_condition(self):
        self.reject(lambda r: r["cells"][-1].update(localRequirements=[]))

    def test_unsupported_diagnostic_cannot_be_claimed_compatible(self):
        report = fixture(); self.warning(report, disposition="Unsupported")["outcome"] = "AssessedCompatible"
        with self.assertRaisesRegex(ValueError, "unsupported finding hidden"): self.accept(report)


if __name__ == "__main__": unittest.main()
