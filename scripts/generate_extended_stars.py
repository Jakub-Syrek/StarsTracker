"""
Generate StarsTracker.Api/Resources/stars-extended.json from the HYG csv.

Extracts every entry with apparent magnitude <= 6.0 (naked-eye limit under a
dark sky) and trims the schema down to the fields the API exposes:
  id, name (proper or bayer/flam), ra (hours), dec (deg), mag, dist_ly.

Usage:
    python scripts/generate_extended_stars.py path/to/hygdata_v41.csv

Output is written to StarsTracker.Api/Resources/stars-extended.json — about
3000 entries / ~250 KB, small enough to embed in the API image.
"""

from __future__ import annotations
import csv
import json
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
OUTPUT = REPO_ROOT / "StarsTracker.Api" / "Resources" / "stars-extended.json"

PC_TO_LY = 3.2616
MAG_LIMIT = 6.0


def naming(row: dict) -> str:
    """Pick the best label available: proper name → Bayer → Flamsteed → HIP."""
    proper = (row.get("proper") or "").strip()
    if proper:
        return proper
    bayer = (row.get("bayer") or "").strip()
    con = (row.get("con") or "").strip()
    if bayer and con:
        return f"{bayer} {con}"
    flam = (row.get("flam") or "").strip()
    if flam and con:
        return f"{flam} {con}"
    hip = (row.get("hip") or "").strip()
    if hip:
        return f"HIP {hip}"
    return f"id {row.get('id', '?')}"


def round_distance(ly: float) -> float:
    if ly < 10:
        return round(ly, 2)
    if ly < 100:
        return round(ly, 1)
    if ly < 1000:
        return round(ly)
    return round(ly / 10) * 10


def main(argv: list[str]) -> int:
    if len(argv) < 2:
        print("usage: generate_extended_stars.py <hygdata.csv>", file=sys.stderr)
        return 2

    csv_path = Path(argv[1])
    if not csv_path.is_file():
        print(f"file not found: {csv_path}", file=sys.stderr)
        return 1

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)

    out: list[dict] = []
    with csv_path.open(newline="", encoding="utf-8") as fh:
        reader = csv.DictReader(fh)
        for row in reader:
            try:
                mag = float(row["mag"])
                ra = float(row["ra"])
                dec = float(row["dec"])
                dist_pc = float(row["dist"]) if row["dist"] else 0.0
            except (ValueError, KeyError):
                continue
            if mag > MAG_LIMIT:
                continue
            if mag < -30:        # bogus row (used to mark "no measurement")
                continue
            # HYG row 0 is "Sol" (the Sun) at (0,0,0) — we expose the Sun via
            # /api/v1/planets instead, so skip it here.
            if row.get("id") == "0":
                continue
            entry: dict = {
                "id": int(row["id"]),
                "name": naming(row),
                "ra": round(ra, 5),
                "dec": round(dec, 4),
                "mag": round(mag, 2),
            }
            if dist_pc and dist_pc > 0:
                entry["dist_ly"] = round_distance(dist_pc * PC_TO_LY)
            out.append(entry)

    out.sort(key=lambda e: e["mag"])

    # Compact one-line-per-star layout — keeps diffs readable and Java's
    # default JSON pretty-printers don't change anything.
    lines = ["["]
    for i, e in enumerate(out):
        comma = "," if i < len(out) - 1 else ""
        parts = [
            f'"id":{e["id"]}',
            f'"name":{json.dumps(e["name"], ensure_ascii=False)}',
            f'"ra":{e["ra"]}',
            f'"dec":{e["dec"]}',
            f'"mag":{e["mag"]}',
        ]
        if "dist_ly" in e:
            parts.append(f'"dist_ly":{e["dist_ly"]}')
        lines.append("  {" + ",".join(parts) + "}" + comma)
    lines.append("]")

    OUTPUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"Wrote {len(out)} stars (mag <= {MAG_LIMIT}) -> {OUTPUT}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
