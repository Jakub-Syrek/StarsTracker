"""
One-off script that enriches Resources/Raw/stars.json with distance data
from the HYG Database (CURRENT/hygdata_v41.csv).

Strategy:
1. Load the curated 300-entry stars.json shipped with the app.
2. Load the full HYG csv into a dict keyed by `proper` name.
3. For each entry in stars.json, look up by exact name, then by RA/Dec
   proximity (< 0.25°) as a fallback.
4. Convert parsecs → light years (x 3.2616).
5. Write back stars.json with a new `dist_ly` field (rounded sensibly).

Usage:
    python scripts/merge_hyg_distances.py path/to/hygdata.csv

The HYG csv lives outside the repo because it is huge (~32 MB);
download it from
https://raw.githubusercontent.com/astronexus/HYG-Database/main/hyg/CURRENT/hygdata_v41.csv
"""

from __future__ import annotations
import csv
import json
import math
import sys
from pathlib import Path
from typing import Optional


REPO_ROOT = Path(__file__).resolve().parent.parent
STARS_JSON = REPO_ROOT / "Resources" / "Raw" / "stars.json"

PC_TO_LY = 3.2616


def load_hyg(csv_path: Path) -> tuple[dict[str, dict], list[dict]]:
    """Return (by_name, all_rows) parsed from HYG csv."""
    by_name: dict[str, dict] = {}
    rows: list[dict] = []
    with csv_path.open(newline="", encoding="utf-8") as fh:
        reader = csv.DictReader(fh)
        for row in reader:
            try:
                row["ra"] = float(row["ra"]) if row["ra"] else None
                row["dec"] = float(row["dec"]) if row["dec"] else None
                row["dist"] = float(row["dist"]) if row["dist"] else None
            except ValueError:
                continue
            rows.append(row)
            proper = (row.get("proper") or "").strip()
            if proper and row["dist"] and row["dist"] > 0:
                by_name[proper.casefold()] = row
    return by_name, rows


def find_by_name(name: str, by_name: dict[str, dict]) -> Optional[dict]:
    return by_name.get(name.casefold())


def find_by_position(
    ra_h: float, dec_deg: float, rows: list[dict], tolerance_deg: float = 0.25
) -> Optional[dict]:
    """Closest HYG entry within tolerance, by angular distance."""
    target_ra_deg = ra_h * 15.0
    cos_target_dec = math.cos(math.radians(dec_deg))
    best = None
    best_dist = tolerance_deg
    for row in rows:
        if row["ra"] is None or row["dec"] is None or not row["dist"]:
            continue
        if row["dist"] <= 0:
            continue
        ra_diff = (row["ra"] * 15.0 - target_ra_deg + 540) % 360 - 180
        dec_diff = row["dec"] - dec_deg
        ang = math.hypot(ra_diff * cos_target_dec, dec_diff)
        if ang < best_dist:
            best_dist = ang
            best = row
    return best


def round_distance(ly: float) -> float:
    """Round to two significant figures under 100, integers otherwise."""
    if ly < 10:
        return round(ly, 2)
    if ly < 100:
        return round(ly, 1)
    if ly < 1000:
        return round(ly)
    return round(ly / 10) * 10


def main(argv: list[str]) -> int:
    if len(argv) < 2:
        print("usage: merge_hyg_distances.py <hygdata.csv>", file=sys.stderr)
        return 2

    csv_path = Path(argv[1])
    if not csv_path.is_file():
        print(f"file not found: {csv_path}", file=sys.stderr)
        return 1

    print(f"Loading HYG from {csv_path} ...")
    by_name, all_rows = load_hyg(csv_path)
    print(f"  {len(by_name)} HYG entries with proper name + distance")
    print(f"  {len(all_rows)} HYG entries total")

    stars = json.loads(STARS_JSON.read_text(encoding="utf-8"))
    print(f"Loaded {len(stars)} entries from {STARS_JSON.name}")

    matched_by_name = matched_by_pos = unmatched = 0
    for star in stars:
        name = star.get("name", "")
        ra_h = star.get("ra")
        dec_d = star.get("dec")
        if not isinstance(ra_h, (int, float)) or not isinstance(dec_d, (int, float)):
            continue

        hyg = find_by_name(name, by_name)
        if hyg is None:
            hyg = find_by_position(ra_h, dec_d, all_rows)
            if hyg is not None:
                matched_by_pos += 1
        else:
            matched_by_name += 1

        if hyg is None:
            unmatched += 1
            continue

        dist_pc = hyg.get("dist")
        if not dist_pc or dist_pc <= 0:
            unmatched += 1
            continue

        ly = dist_pc * PC_TO_LY
        star["dist_ly"] = round_distance(ly)

    print(
        f"Match summary: by-name {matched_by_name}, "
        f"by-position {matched_by_pos}, unmatched {unmatched}"
    )

    # Pretty print mirroring the original layout: one star per line.
    out_lines = ["["]
    for i, star in enumerate(stars):
        comma = "," if i < len(stars) - 1 else ""
        # Field order: id, name, ra, dec, mag, dist_ly (when present)
        parts = [
            f'"id":{star["id"]}',
            f'"name":{json.dumps(star["name"], ensure_ascii=False)}',
            f'"ra":{star["ra"]}',
            f'"dec":{star["dec"]}',
            f'"mag":{star["mag"]}',
        ]
        if "dist_ly" in star:
            parts.append(f'"dist_ly":{star["dist_ly"]}')
        out_lines.append("  {" + ",".join(parts) + "}" + comma)
    out_lines.append("]")

    STARS_JSON.write_text("\n".join(out_lines) + "\n", encoding="utf-8")
    print(f"Wrote {STARS_JSON}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
