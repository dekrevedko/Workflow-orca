import copy
import unittest
from release_gate import validate_jobs


def completed():
    names = [f"native / Native {host} {flavor}" for host in ("windows-2025", "ubuntu-24.04")
             for flavor in ("jit", "trimmed", "aot")]
    names.append("native / Six native cells bind the same source and packages")
    return {"jobs": [dict(name=name, status="completed", conclusion="success") for name in names]}


class ReleaseGateTests(unittest.TestCase):
    def test_six_completed_cells_and_aggregate_are_required(self):
        validate_jobs(completed())

    def test_original_successful_workflow_with_skipped_native_jobs_is_rejected(self):
        # Reproduction from release run 40997094707: the reusable workflow was green
        # while the unexpanded matrix and aggregate were skipped.
        document = {"jobs": [dict(name="native / One exact candidate feed", status="completed", conclusion="success"),
                              dict(name="native / Native ${{ matrix.os }} ${{ matrix.flavor }}", status="completed", conclusion="skipped"),
                              dict(name="native / Six native cells bind the same source and packages", status="completed", conclusion="skipped")]}
        with self.assertRaises(ValueError): validate_jobs(document)

    def test_every_missing_failed_skipped_cancelled_or_running_cell_is_rejected(self):
        for index in range(7):
            document = completed()
            del document["jobs"][index]
            with self.assertRaises(ValueError): validate_jobs(document)
            for conclusion in ("failure", "skipped", "cancelled", None):
                document = completed()
                document["jobs"][index]["conclusion"] = conclusion
                with self.assertRaises(ValueError): validate_jobs(document)
            document = completed()
            document["jobs"][index]["status"] = "in_progress"
            with self.assertRaises(ValueError): validate_jobs(document)

    def test_duplicates_and_truncated_job_inventory_are_rejected(self):
        document = completed()
        document["jobs"].append(copy.deepcopy(document["jobs"][0]))
        with self.assertRaises(ValueError): validate_jobs(document)
        document = completed()
        document["total_count"] = 100
        with self.assertRaises(ValueError): validate_jobs(document)
