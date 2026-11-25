import json
import math
import statistics
import sys
from pathlib import Path

def percentile(values, q):
    return sorted(values)[math.ceil(len(values) * q) - 1]

def describe(values):
    return dict(count=len(values), median=round(statistics.median(values), 3),
                p95=round(percentile(values, .95), 3),
                minimum=round(min(values), 3), maximum=round(max(values), 3))

def summarize(paths):
    cohorts = [[json.loads(l) for l in path.read_text('utf-8').splitlines() if l] for path in paths]
    if not cohorts:
        raise ValueError("No samples")
    if all(r["scenario"] == "roots" and "retryCounts" in r for c in cohorts for r in c):
        measured = [r for c in cohorts for r in c if r["round"] > 0]
        assert len(measured) == len(paths)*3
        metrics = {key:describe([r[key] for r in measured]) for key in ("mappedThreads","toLastAdmissionMs")}
        metrics["nextAdmissionMs"] = describe([v for r in measured for v in r["nextAdmissionMs"]])
        for reason in sorted({k for r in measured for k in r["retryCounts"]}):
            counts = [r["retryCounts"].get(reason,0) for r in measured]
            metrics[reason] = {**describe(counts), "total":sum(counts)}
        return {"contention":metrics}
    if all(r["scenario"] == "burst" for c in cohorts for r in c):
        measured = [r for c in cohorts for r in c if r["round"] > 0]
        assert len(measured) == len(paths)*10
        output = {k:describe([r[k] for r in measured]) for k in ("mappedThreads","firstToLastMs")}
        output.update({k:describe([v for r in measured for v in r[k]]) for k in ("admissionMs","nextAdmissionMs")})
        return {"burst":output}
    idle = []
    for path, cohort in zip(paths, cohorts):
        before = next(x for x in cohort if x.get("phase") == "empty")
        after = next(x for x in cohort if x.get("phase") == "idle10s")
        idle.append(dict(file=path.name, workers=after["active"], retired=after["retired"],
                         threads=after["threads"], threadsGrowth=after["threads"]-before["threads"],
                         privateGrowthMiB=(after["privateBytes"]-before["privateBytes"])/2**20,
                         residentGrowthMiB=(after["workingSetBytes"]-before["workingSetBytes"])/2**20,
                         virtualGrowthMiB=(after["virtualBytes"]-before["virtualBytes"])/2**20))
    out = {"idleRuns": idle, "idle":{k:describe([r[k] for r in idle]) for k in idle[0] if k != "file"}}
    for shape in ("chain","roots"):
        runs = [r for c in cohorts for r in c if r["scenario"] == shape and r["round"] > 0]
        assert len(runs) == 3 * len(paths)
        out[shape] = {key:describe([r[key] for r in runs]) for key in ["mappedThreads","toLastAdmissionMs"]}
        for key in ["admissionMs","nextAdmissionMs"] + (["predecessorJoinToAdmissionMs"] if shape=="chain" else []):
            out[shape][key]=describe([v for r in runs for v in r[key]])
    return out

if __name__ == "__main__":
    print(json.dumps(summarize([Path(p) for p in sys.argv[1:]]), indent=2))
