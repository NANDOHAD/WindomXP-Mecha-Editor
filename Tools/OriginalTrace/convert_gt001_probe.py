#!/usr/bin/env python3
"""Convert a debugger-produced GT-001 CSV probe into Phase 6 observation JSONL."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
from pathlib import Path
from typing import Iterable


EXPECTED_EXE_SHA256 = "ec5a09973cd00c1bab7ad7fe284293c06a415c65378410e31e4534327ccc20c1"
EXPECTED_GT001_DIRECTIONS = [5] * 2 + [8] * 12 + [5] * 8
FLOAT32_MIN_NORMAL = 1.1754943508222875e-38


def normalize_direction(raw_direction: int) -> int:
    """Map the original runtime code to the Unity trace input convention."""
    return 0 if raw_direction == 5 else raw_direction


def normalize_float32_subnormal(value: float) -> float:
    """Treat a non-zero float32 subnormal residue as stopped for comparison."""
    return 0.0 if 0.0 < abs(value) < FLOAT32_MIN_NORMAL else value


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def required_text(row: dict[str, str], name: str, line: int) -> str:
    value = (row.get(name) or "").strip()
    if not value:
        raise ValueError(f"CSV row {line}: {name} is required")
    return value


def integer(row: dict[str, str], name: str, line: int) -> int:
    return int(required_text(row, name, line), 0)


def number(row: dict[str, str], name: str, line: int) -> float:
    value = float(required_text(row, name, line))
    if value != value or value in (float("inf"), float("-inf")):
        raise ValueError(f"CSV row {line}: {name} must be finite")
    return value


def has_columns(fieldnames: Iterable[str] | None, names: Iterable[str]) -> bool:
    available = set(fieldnames or ())
    return all(name in available for name in names)


def vector(row: dict[str, str], prefix: str, line: int) -> list[float]:
    return [number(row, f"{prefix}_{axis}", line) for axis in ("x", "y", "z")]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Convert a 0x004cd840 GT-001 probe CSV to original-observation JSONL."
    )
    parser.add_argument("csv", type=Path, help="Debugger probe CSV with 22 zero-based ticks")
    parser.add_argument("output", type=Path, help="Output .jsonl path")
    parser.add_argument("--exe", type=Path, required=True, help="Exact observed WindomXP_orig.exe")
    parser.add_argument("--ani", type=Path, required=True, help="Observed mech Script.ani")
    parser.add_argument("--spt", type=Path, required=True, help="Observed mech Script.spt")
    parser.add_argument("--mech-id", default="ガンダムTR-1ヘイズル改")
    parser.add_argument(
        "--normalization-profile",
        default="original-direction5-idle-to0-subnormal-to0-v1",
        help="Recorded coordinate/value normalization identifier",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    exe_hash = sha256(args.exe)
    if exe_hash.lower() != EXPECTED_EXE_SHA256:
        raise ValueError(
            "Observed EXE hash does not match the decompiled snapshot: "
            f"expected={EXPECTED_EXE_SHA256} actual={exe_hash}"
        )

    with args.csv.open("r", encoding="utf-8-sig", newline="") as stream:
        reader = csv.DictReader(stream)
        rows = list(reader)
        fieldnames = reader.fieldnames

    if len(rows) != len(EXPECTED_GT001_DIRECTIONS):
        raise ValueError(f"GT-001 requires exactly 22 ticks; CSV contains {len(rows)}")

    vector_groups = {
        "velocity_before": "velocityBefore",
        "force": "force",
        "velocity_after": "velocityAfter",
        "scripted_velocity": "scriptedVelocity",
        "requested_displacement": "requestedDisplacement",
    }
    observed_fields = ["tick", "input.direction", "logicalAction", "runtime.grounded"]
    enabled_vectors: dict[str, str] = {}
    for csv_prefix, json_name in vector_groups.items():
        columns = [f"{csv_prefix}_{axis}" for axis in ("x", "y", "z")]
        if has_columns(fieldnames, columns):
            enabled_vectors[csv_prefix] = json_name
            observed_fields.append(json_name)

    records: list[dict[str, object]] = []
    for tick, row in enumerate(rows):
        line = tick + 2
        observed_tick = integer(row, "tick", line)
        direction = integer(row, "direction", line)
        if observed_tick != tick:
            raise ValueError(f"CSV row {line}: expected tick {tick}, got {observed_tick}")
        if direction != EXPECTED_GT001_DIRECTIONS[tick]:
            raise ValueError(
                f"CSV row {line}: GT-001 direction at tick {tick} must be "
                f"{EXPECTED_GT001_DIRECTIONS[tick]}, got {direction}"
            )

        record: dict[str, object] = {
            # The CSV index is zero based. Existing Phase 5 Unity traces record
            # the first simulated frame as canonical tick 1.
            "tick": tick + 1,
            "logicalAction": integer(row, "logical_action", line),
            "input": {
                "direction": normalize_direction(direction),
                "rawDirection": direction,
            },
            "runtime": {"grounded": integer(row, "airborne", line) == 0},
        }
        for csv_prefix, json_name in enabled_vectors.items():
            raw_vector = vector(row, csv_prefix, line)
            if csv_prefix == "scripted_velocity":
                record[json_name] = [normalize_float32_subnormal(value) for value in raw_vector]
                record["rawScriptedVelocity"] = raw_vector
            else:
                record[json_name] = raw_vector
        records.append(record)

    header = {
        "session": {
            "schemaVersion": 1,
            "source": "original-observation",
            "scenario": "GT-001",
            "mechId": args.mech_id,
            "exeHash": exe_hash,
            "aniHash": sha256(args.ani),
            "sptHash": sha256(args.spt),
            "tickRate": 60,
            "tickOrigin": 1,
            "normalizationProfile": args.normalization_profile,
            "observedFields": observed_fields,
        }
    }

    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("w", encoding="utf-8", newline="\n") as stream:
        stream.write(json.dumps(header, ensure_ascii=False, separators=(",", ":")))
        for record in records:
            stream.write("\n")
            stream.write(json.dumps(record, ensure_ascii=False, separators=(",", ":")))
    print(f"wrote {args.output} ticks={len(records)} fields={','.join(observed_fields)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
