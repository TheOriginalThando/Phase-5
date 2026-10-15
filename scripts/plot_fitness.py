"""Plot best and mean fitness against generation, with a line at each stage boundary (Phase 5 report §7).

Usage: python scripts/plot_fitness.py results/task3/staged/seed1 [more run dirs ...] [-o out.png] [--log]
With several run directories (e.g. 5 seeds) each run's best fitness is drawn, plus the mean across runs.
"""
import argparse
import csv
import os

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402


def read_log(run):
    with open(os.path.join(run, "fitness_log.csv"), newline="") as f:
        rows = list(csv.DictReader(f))
    return {
        "gen": [int(r["generation"]) for r in rows],
        "stage": [int(r["stage"]) for r in rows],
        "best": [float(r["best"]) for r in rows],
        "mean": [float(r["mean"]) for r in rows],
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("runs", nargs="+")
    ap.add_argument("-o", "--out", help="output image (default: <first run>/fitness.png)")
    ap.add_argument("--log", action="store_true", help="logarithmic fitness axis")
    args = ap.parse_args()

    logs = [read_log(r) for r in args.runs]
    fig, ax = plt.subplots(figsize=(8, 4.5))

    if len(logs) == 1:
        lg = logs[0]
        ax.plot(lg["gen"], lg["best"], label="best", lw=2)
        ax.plot(lg["gen"], lg["mean"], label="population mean", lw=1, alpha=0.7)
    else:
        for run, lg in zip(args.runs, logs):
            ax.plot(lg["gen"], lg["best"], lw=1, alpha=0.5, label=os.path.basename(os.path.normpath(run)))
        n = min(len(lg["best"]) for lg in logs)
        avg = [sum(lg["best"][i] for lg in logs) / len(logs) for i in range(n)]
        ax.plot(logs[0]["gen"][:n], avg, color="k", lw=2, label=f"mean best ({len(logs)} runs)")

    # Stage boundaries: wherever the stage number changes.
    lg = logs[0]
    for i in range(1, len(lg["stage"])):
        if lg["stage"][i] != lg["stage"][i - 1]:
            ax.axvline(lg["gen"][i] - 0.5, color="tab:red", ls="--", lw=1, label="stage boundary")

    if args.log:
        ax.set_yscale("log")
    ax.set_xlabel("generation")
    ax.set_ylabel("fitness (lower is better)")
    ax.grid(alpha=0.3)
    ax.legend(fontsize=8)
    out = args.out or os.path.join(args.runs[0], "fitness.png")
    fig.savefig(out, dpi=150, bbox_inches="tight")
    print(out)


if __name__ == "__main__":
    main()
