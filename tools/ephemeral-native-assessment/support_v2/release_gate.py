"""Fail closed unless the release run actually completed every native cell."""
import json
from pathlib import Path


def validate_jobs(document):
    jobs = document["jobs"]
    if document.get("total_count", len(jobs)) != len(jobs):
        raise ValueError("Incomplete workflow job inventory")
    expected = {f"Native {host} {flavor}" for host in ("windows-2025", "ubuntu-24.04")
                for flavor in ("jit", "trimmed", "aot")}
    expected.add("Six native cells bind the same source and packages")
    for name in expected:
        matches = [job for job in jobs if job["name"].rsplit(" / ", 1)[-1] == name]
        if len(matches) != 1 or matches[0].get("status") != "completed" or matches[0].get("conclusion") != "success":
            raise ValueError("Missing, duplicated or unsuccessful native gate: " + name)


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("jobs", type=Path)
    args = parser.parse_args()
    validate_jobs(json.loads(args.jobs.read_text(encoding="utf-8-sig")))
    print("All six native jobs and their record gate completed successfully.")
