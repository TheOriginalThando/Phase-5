"""Collect the Phase 6 deliverables into phase6/.

Usage: python scripts/make_phase6_package.py [--results results] [--out phase6]
         [--runs task1/baseline task2/baseline task3/staged] [--clearance 0.3]

For each task it picks the seed with the lowest final fitness, copies its solution.csv to
phase6/taskN_solution.csv, draws the expected path relative to the start pose, and writes
phase6/manifest.json with the numbers the Phase 6 team needs (rows, duration, floor space).
"""
import argparse
import csv
import json
import math
import os
import shutil

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402

TASK_NAMES = {1: "Drive to a target point", 2: "Follow a sine wave", 3: "Drive a figure eight"}


def to_start_frame(x, y, sx, sy, sth):
    dx, dy = x - sx, y - sy
    c, s = math.cos(sth), math.sin(sth)
    return dx * c + dy * s, -dx * s + dy * c


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--results", default="results")
    ap.add_argument("--out", default="phase6")
    ap.add_argument("--runs", nargs="+", default=["task1/baseline", "task2/baseline", "task3/staged"])
    ap.add_argument("--clearance", type=float, default=0.3, help="free space to keep around the path (m)")
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)

    manifest = []
    for run in args.runs:
        d = os.path.join(args.results, run)
        seeds = sorted(s for s in os.listdir(d) if s.startswith("seed"))
        summaries = {s: json.load(open(os.path.join(d, s, "summary.json"))) for s in seeds}
        best = min(seeds, key=lambda s: summaries[s]["final_fitness"])
        src = os.path.join(d, best)
        cfg = json.load(open(os.path.join(src, "config.json")))
        task = cfg["task"]
        summ = summaries[best]

        name = f"task{task}_solution.csv"
        shutil.copyfile(os.path.join(src, "solution.csv"), os.path.join(args.out, name))
        with open(os.path.join(src, "solution.csv"), newline="") as f:
            rows = [[float(v) for v in r] for r in csv.reader(f) if r]

        xs = [0.0] + [r[0] for r in rows]
        ys = [0.0] + [r[1] for r in rows]
        sx, sy, sth = cfg["start_x"], cfg["start_y"], cfg["start_theta"]
        with open(os.path.join(src, "waypoints.csv"), newline="") as f:
            wps = [to_start_frame(float(w["x"]), float(w["y"]), sx, sy, sth) for w in csv.DictReader(f)]
        ref = []
        with open(os.path.join(src, "reference_curve.csv"), newline="") as f:
            ref = [to_start_frame(float(w["x"]), float(w["y"]), sx, sy, sth) for w in csv.DictReader(f)]

        c = args.clearance
        box = {"min_x": min(xs) - c, "max_x": max(xs) + c, "min_y": min(ys) - c, "max_y": max(ys) + c}
        entry = {
            "task": task,
            "title": TASK_NAMES[task],
            "file": name,
            "source": f"{run}/{best}",
            "rows": len(rows),
            "duration_s": round(len(rows) * 0.2, 1),
            "final_fitness": summ["final_fitness"],
            "mean_waypoint_error_m": summ["w"],
            "end_error_m": summ["e_end"],
            "end_distance_from_start_m": summ["end_distance_from_start"],
            "max_distance_from_start_m": summ["max_distance_from_start"],
            "predicted_end": {"x": rows[-1][0], "y": rows[-1][1], "theta": rows[-1][2]},
            "throttle_range": [min(r[3] for r in rows), max(r[3] for r in rows)],
            "steering_range": [min(r[4] for r in rows), max(r[4] for r in rows)],
            "free_space_box_m": box,
            "free_space_size_m": [box["max_x"] - box["min_x"], box["max_y"] - box["min_y"]],
            "plot": f"task{task}_expected_path.png",
        }
        manifest.append(entry)

        fig, ax = plt.subplots(figsize=(8, 5))
        ax.add_patch(plt.Rectangle((box["min_x"], box["min_y"]), box["max_x"] - box["min_x"], box["max_y"] - box["min_y"],
                                   fill=False, ls=":", color="0.4", label=f"free space needed ({c:.1f} m margin)"))
        if ref:
            ax.plot([p[0] for p in ref], [p[1] for p in ref], color="0.7", ls="--", lw=1, label="target curve")
        ax.scatter([w[0] for w in wps], [w[1] for w in wps], s=22, facecolors="none", edgecolors="tab:orange",
                   zorder=3, label="waypoints")
        ax.plot(xs, ys, color="tab:red", lw=2, marker=".", ms=4, label="predicted path")
        ax.annotate("", xy=(0.25, 0), xytext=(0, 0), arrowprops=dict(arrowstyle="->", lw=2, color="tab:green"))
        ax.plot(0, 0, "o", color="tab:green", ms=8, label="start (car faces +x)")
        ax.plot(xs[-1], ys[-1], "s", color="k", ms=6, label="predicted end")
        ax.set_aspect("equal")
        ax.grid(alpha=0.3)
        ax.set_xlabel("x relative to start [m]  (car's initial heading)")
        ax.set_ylabel("y relative to start [m]  (car's left)")
        ax.set_title(f"Task {task}: {TASK_NAMES[task]} - {len(rows)} commands, {len(rows) * 0.2:.1f} s")
        ax.legend(fontsize=7, loc="upper left", bbox_to_anchor=(1.02, 1))
        fig.savefig(os.path.join(args.out, entry["plot"]), dpi=150, bbox_inches="tight")
        plt.close(fig)
        print(f"task {task}: {run}/{best} -> {name}, {len(rows)} rows, free space "
              f"{entry['free_space_size_m'][0]:.2f} x {entry['free_space_size_m'][1]:.2f} m")

    with open(os.path.join(args.out, "manifest.json"), "w") as f:
        json.dump(manifest, f, indent=2)


if __name__ == "__main__":
    main()
