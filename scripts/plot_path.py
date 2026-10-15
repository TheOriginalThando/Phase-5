"""Plot a run's simulated path with the waypoints, start/end and track bounds (Phase 5 report §7).

Usage: python scripts/plot_path.py results/task3/staged/seed1 [more run dirs ...] [-o out.png]
Several run directories are drawn on the same axes (e.g. several seeds).
"""
import argparse
import csv
import json
import os

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402


def read_csv(path):
    with open(path, newline="") as f:
        return [{k: float(v) for k, v in row.items()} for row in csv.DictReader(f)]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("runs", nargs="+", help="run directories (results/<task>/<experiment>/seed<k>)")
    ap.add_argument("-o", "--out", help="output image (default: <first run>/path.png)")
    args = ap.parse_args()

    first = args.runs[0]
    with open(os.path.join(first, "config.json")) as f:
        cfg = json.load(f)
    b = cfg["bounds"]

    fig, ax = plt.subplots(figsize=(7, 7))
    ax.add_patch(plt.Rectangle((b["min_x"], b["min_y"]), b["max_x"] - b["min_x"], b["max_y"] - b["min_y"],
                               fill=False, color="0.4", lw=1.5, label="track bounds"))

    ref = os.path.join(first, "reference_curve.csv")
    if os.path.exists(ref):
        pts = read_csv(ref)
        if pts:
            ax.plot([p["x"] for p in pts], [p["y"] for p in pts], color="0.7", lw=1, ls="--", label="reference curve")

    wps = read_csv(os.path.join(first, "waypoints.csv"))
    ax.scatter([w["x"] for w in wps], [w["y"] for w in wps], s=30, facecolors="none", edgecolors="tab:orange",
               zorder=3, label="waypoints")

    for i, run in enumerate(args.runs):
        path = read_csv(os.path.join(run, "best_path.csv"))
        xs, ys = [p["x"] for p in path], [p["y"] for p in path]
        label = "simulated path" if len(args.runs) == 1 else os.path.basename(os.path.normpath(run))
        ax.plot(xs, ys, marker=".", ms=4, lw=1.5, label=label)
        if i == 0:
            ax.plot(xs[0], ys[0], "o", color="tab:green", ms=9, label="start", zorder=4)
        ax.plot(xs[-1], ys[-1], "s", color="tab:red", ms=7, label="end" if i == 0 else None, zorder=4)

    ax.set_aspect("equal")
    ax.set_xlabel("x [m]")
    ax.set_ylabel("y [m]")
    ax.set_title(f"Task {cfg['task']} - {cfg['experiment']}")
    ax.grid(alpha=0.3)
    ax.legend(loc="best", fontsize=8)
    out = args.out or os.path.join(first, "path.png")
    fig.savefig(out, dpi=150, bbox_inches="tight")
    print(out)


if __name__ == "__main__":
    main()
