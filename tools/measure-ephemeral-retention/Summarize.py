"""Validate both retention measurement matrices and print the comparison tables."""
import argparse
import json
import math
import statistics
import sys
from pathlib import Path

MIXES = {"active": (90, 5), "terminal": (10, 80), "pinned": (10, 10), "disabled": (10, 80)}
SIZES = (1_000, 10_000, 100_000)
PHASES = ("stable-work", "stable-unpin", "expire-eligible")
TIMINGS = ("cycleTicks", "acquisitionTicks", "releaseTicks", "holdTicks", "selectionWaitTicks", "admissionTicks")
COUNTERS = ("selections", "visits", "eligibility", "sortKeys", "ageChecks", "retirementChecks",
            "selected", "indexAdds", "indexRemoves", "indexComparisons", "ageRanges", "expiredKeys")


def read(path, mode):
    rows = [json.loads(line) for line in path.read_text(encoding="utf-8-sig").splitlines()]
    end = rows.pop()
    assert end["completed"] and end["mode"] == mode and all(row["mode"] == mode for row in rows)
    return rows, (end["revision"], end["indexed"])


def load(folder, indexed):
    counts, meta = read(folder / "counts-1.jsonl", "counts")
    assert meta[1] == indexed
    partial_phases = {"expire-all-but-one": 79_999, "expire-last": 1}
    partial_counts = [r for r in counts if r["phase"] in partial_phases]
    assert len(partial_counts) == 2 and {r["phase"] for r in partial_counts} == partial_phases.keys()
    for r in partial_counts:
        assert (r["size"], r["mix"], r["selected"]) == (100_000, "terminal", partial_phases[r["phase"]])
        if indexed:
            assert r["successfulRemoves"] == 0 and r["bulkClears"] == 1 and r["indexRemoves"] == r["selected"]
            assert (r["denseChecks"], r["expiredKeys"], r["indexAdds"]) == (
                (80_000, 40_001, 1) if r["phase"] == "expire-all-but-one" else (1, 0, 0))
    counts = [r for r in counts if r["phase"] not in partial_phases]
    expected = {(size, mix, phase, not indexed)
                for size in SIZES for mix in MIXES for phase in PHASES}
    expected.update((100, mix, phase, deferred) for mix in ("active", "terminal", "pinned")
                    for phase in PHASES for deferred in ([False] if indexed else [False, True]))
    count_rows = {(r["size"], r["mix"], r["phase"], r["deferSeed"]): r for r in counts}
    assert len(count_rows) == len(counts) and count_rows.keys() == expected
    for (size, mix, phase, deferred), row in count_rows.items():
        on = mix != "disabled"
        a, e = MIXES[mix]
        active, eligible = size * a // 100, size * e // 100
        assert (row["active"], row["eligible"], row["pinned"]) == (active, eligible, size-active-eligible)
        assert row["selections"] == int(on)
        assert row["selected"] == (eligible if on and phase == "expire-eligible" else 0)
        if indexed:
            assert row["indexAdds"] == int(on and phase == "stable-unpin")
            assert row["indexRemoves"] == row["indexAdds"] + row["selected"]
            assert row["visits"] == 0 and row["expiredKeys"] in (0, row["selected"])
            if row["selected"]:
                assert row["bulkClears"] == 1 and row["successfulRemoves"] == 0
                assert row["denseChecks"] == row["selected"] and row["expiredKeys"] == 0
            else:
                assert row["bulkClears"] == 0 and row["successfulRemoves"] == row["indexRemoves"]
                assert row["denseChecks"] == 0
            assert 0 <= row["ageRanges"] <= 2 * int(on)
            if phase == "stable-work":
                assert row["indexComparisons"] == 0
            if phase == "stable-unpin":
                assert row["indexComparisons"] <= (8 * math.ceil(math.log2(eligible + 1)) + 8) * int(on)
        else:
            assert row["visits"] == size * int(on) and row["eligibility"] == (size-active) * int(on)
            assert all(row[k] == eligible * int(on) for k in ("sortKeys", "ageChecks", "retirementChecks"))
            if size == 100 and deferred:
                reference = count_rows[size, mix, phase, False]
                assert all(row[k] == reference[k] for k in COUNTERS)

    groups, expiry = {}, {}
    for run in (1, 2):
        rows, timing_meta = read(folder / f"timing-{run}.jsonl", "timing")
        assert meta == timing_meta
        observed, partial_observed = set(), set()
        for row in rows:
            size, mix = row["size"], row["mix"]
            assert size in SIZES and mix in MIXES and row["timestampFrequency"] > 0
            workers = row.get("workers", 0)
            if row.get("phase") in partial_phases:
                phase = row["phase"]
                assert phase not in partial_observed and workers == 0
                partial_observed.add(phase)
                assert (size, mix, row["selected"]) == (100_000, "terminal", partial_phases[phase])
                assert row["selectionTicks"] <= row["cycleTicks"]
                expiry.setdefault((size, phase), []).append(row)
                continue
            key = size, mix, workers
            assert key not in observed
            observed.add(key)
            if workers == 0:
                assert row["phase"] == "expire-eligible" and row["selectionTicks"] <= row["cycleTicks"]
                assert row["selected"] == count_rows[size, mix, "expire-eligible", not indexed]["selected"]
                expiry.setdefault((size, mix), []).append(row)
                continue
            assert workers in (1, 4)
            n = 48 if workers == 1 else 96
            assert all(len(row[k]) == n for k in (*TIMINGS, "unpin"))
            assert sum(row["unpin"]) == n // 2 and row["elapsedMs"] > 0
            assert all(c >= max(a + r + d, h) for c, a, r, d, h in
                       zip(*(row[k] for k in ("cycleTicks", "acquisitionTicks", "releaseTicks", "admissionTicks", "holdTicks"))))
            groups.setdefault(key, []).append(row)
        assert observed == {(s, m, w) for s in SIZES for m in MIXES for w in (0, 1, 4)}
        assert partial_observed == partial_phases.keys()
    return groups, expiry, count_rows, meta[0]


def quantile(values, p):
    return sorted(values)[math.ceil(len(values) * p) - 1]


def samples(rows, key, unpin=None):
    return [ticks * 1e6 / row["timestampFrequency"] for row in rows
            for ticks, pin in zip(row[key], row["unpin"], strict=True) if unpin is None or pin == unpin]


def rate(rows):
    return sum(len(row["unpin"]) for row in rows) * 1e3 / sum(row["elapsedMs"] for row in rows)


def compare(baseline, candidate):
    before, after = load(baseline, False), load(candidate, True)
    print(f"Sources {before[3]} and {after[3]}\n")
    print("| Entries | Mix | One-caller cycle mean / P95, µs | One-caller pin acquisition P95, µs | Cycles/s, one / four callers | Four-caller cycle P95, µs |")
    print("| ---: | --- | --- | --- | --- | --- |")
    for size in SIZES:
        for mix in MIXES:
            cols = []
            for groups, _, _, _ in (before, after):
                solo, many = groups[size, mix, 1], groups[size, mix, 4]
                cycle = samples(solo, "cycleTicks")
                cols.append([f"{statistics.mean(cycle):,.2f} / {quantile(cycle,.95):,.2f}",
                             f"{quantile(samples(solo,'acquisitionTicks',True), .95):,.2f}", f"{rate(solo):,.0f} / {rate(many):,.0f}",
                             f"{quantile(samples(many,'cycleTicks'),.95):,.2f}"])
            print(f"| {size:,} | {mix} | " + " | ".join(" → ".join(pair) for pair in zip(*cols)) + " |")
    print("\n| 10,000 entries | One-caller selection hold mean / P95, µs | Active work / unpin release P95, µs | Four-caller admission P99, µs | Modeled gate demand at 500 cycles/s |")
    print("| --- | --- | --- | --- | --- |")
    for mix in MIXES:
        columns = []
        for groups, _, _, _ in (before, after):
            row = groups[10_000, mix, 1]
            hold, release, unpin = samples(row,"holdTicks"), samples(row,"releaseTicks",False), samples(row,"releaseTicks",True)
            bound = math.ceil(statistics.mean(samples(row, "cycleTicks")) * 5) / 100
            admission = samples(groups[10_000, mix, 4], "admissionTicks")
            columns.append([f"{statistics.mean(hold):,.2f} / {quantile(hold,.95):,.2f}",
                            f"{quantile(release,.95):,.2f} / {quantile(unpin,.95):,.2f}",
                            f"{quantile(admission,.99):,.2f}", f"≤{bound:.2f}%"])
        print(f"| {mix} | " + " | ".join(" → ".join(pair) for pair in zip(*columns)) + " |")
    print("\n| 100,000 entries | Expired | Selection hold range, ms | Whole cycle range, ms |")
    print("| --- | ---: | --- | --- |")
    for mix in (*MIXES, "expire-all-but-one", "expire-last"):
        columns = []
        for _, expiry, _, _ in (before, after):
            row = expiry[100_000, mix]
            def span(key):
                values = [r[key] * 1e3 / r["timestampFrequency"] for r in row]
                return f"{min(values):.2f}–{max(values):.2f}"
            columns.append([span("selectionTicks"), span("cycleTicks")])
        print(f"| {mix} | {row[0]['selected']:,} | " + " | ".join(" → ".join(pair) for pair in zip(*columns)) + " |")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    here = Path(__file__).parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("baseline", nargs="?", type=Path, default=here / "samples" / "baseline")
    parser.add_argument("candidate", nargs="?", type=Path, default=here / "samples" / "indexed")
    args = parser.parse_args()
    compare(args.baseline, args.candidate)
