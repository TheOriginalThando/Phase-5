"""Comparison figures and seed statistics for the Phase 5 report."""
import csv
import json
import os
import statistics
import sys

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402

RES, OUT = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)


def rows(p):
    with open(p, newline="") as f:
        return list(csv.DictReader(f))


def summary(run):
    with open(os.path.join(run, "summary.json")) as f:
        return json.load(f)


def seeds(task, exp):
    d = os.path.join(RES, task, exp)
    return sorted(os.path.join(d, s) for s in os.listdir(d) if s.startswith("seed"))


def best_seed(task, exp):
    return min(seeds(task, exp), key=lambda r: summary(r)["final_fitness"])


def draw_path(ax, run, title):
    b = json.load(open(os.path.join(run, "config.json")))["bounds"]
    ref = rows(os.path.join(run, "reference_curve.csv"))
    if ref:
        ax.plot([float(p["x"]) for p in ref], [float(p["y"]) for p in ref], color="0.7", ls="--", lw=1, label="reference")
    wp = rows(os.path.join(run, "waypoints.csv"))
    ax.scatter([float(p["x"]) for p in wp], [float(p["y"]) for p in wp], s=22, facecolors="none",
               edgecolors="tab:orange", zorder=3, label="waypoints")
    pa = rows(os.path.join(run, "best_path.csv"))
    xs, ys = [float(p["x"]) for p in pa], [float(p["y"]) for p in pa]
    ax.plot(xs, ys, marker=".", ms=3, lw=1.4, color="tab:blue", label="simulated path")
    ax.plot(xs[0], ys[0], "o", color="tab:green", ms=7, label="start", zorder=4)
    ax.plot(xs[-1], ys[-1], "s", color="tab:red", ms=5, label="end", zorder=4)
    ax.set_xlim(b["min_x"], b["max_x"])
    ax.set_ylim(b["min_y"], b["max_y"])
    ax.set_aspect("equal")
    ax.grid(alpha=0.3)
    ax.set_title(title, fontsize=10)


# Figure: task 3, four variants (best seed of each).
variants = [("gerono_default", "Gerono, default extension (as specified)"),
            ("gerono_mirror", "Gerono, mirror extension"),
            ("bernoulli_default", "Bernoulli, default extension"),
            ("staged", "Bernoulli, mirror extension (adopted)")]
fig, axes = plt.subplots(2, 2, figsize=(10, 9.5))
for ax, (exp, title) in zip(axes.flat, variants):
    run = best_seed("task3", exp)
    s = summary(run)
    draw_path(ax, run, f"{title}\nbest of 5 seeds: F = {s['final_fitness']:.3f}, W = {s['w']:.3f} m")
    ax.set_xlim(0.1, 3.6)
    ax.set_ylim(0.3, 3.6)
axes[0, 0].legend(fontsize=7, loc="upper right")
fig.tight_layout()
fig.savefig(os.path.join(OUT, "task3_variants.png"), dpi=150)

# Figures: tasks 1 and 2 (best seed).
fig, axes = plt.subplots(1, 2, figsize=(11, 4.6))
for ax, (task, exp, title) in zip(axes, [("task1", "baseline", "Task 1: point target"),
                                         ("task2", "baseline", "Task 2: sine wave")]):
    run = best_seed(task, exp)
    s = summary(run)
    draw_path(ax, run, f"{title} (best of 5 seeds, W = {s['w']:.3f} m)")
    ax.set_xlim(1.2, 3.8)
    ax.set_ylim(1.4, 2.9)
axes[0].legend(fontsize=7, loc="upper left")
fig.tight_layout()
fig.savefig(os.path.join(OUT, "tasks12_paths.png"), dpi=150)


# Figure: fitness vs generation, mean of 5 seeds, for several experiments.
def mean_curve(task, exp):
    logs = [rows(os.path.join(r, "fitness_log.csv")) for r in seeds(task, exp)]
    n = min(len(lg) for lg in logs)
    return [sum(float(lg[i]["best"]) for lg in logs) / len(logs) for i in range(n)]


fig, axes = plt.subplots(1, 2, figsize=(11, 4.2))
for task, exp, label in [("task1", "baseline", "Task 1"), ("task2", "baseline", "Task 2"),
                         ("task3", "staged", "Task 3 (Bernoulli, mirror)")]:
    axes[0].plot(mean_curve(task, exp), label=label)
axes[0].set_yscale("log")
axes[0].set_title("Best fitness, mean of 5 seeds", fontsize=10)
for exp, label in [("bernoulli_scratch", "from scratch (300 gens)"),
                   ("bernoulli_default", "staged, default extension"),
                   ("staged", "staged, mirror extension (adopted)")]:
    axes[1].plot(mean_curve("task3", exp), label=label)
axes[1].axvline(149.5, color="tab:red", ls="--", lw=1, label="stage boundary")
axes[1].set_yscale("log")
axes[1].set_title("Task 3 (Bernoulli): staged vs from scratch, mean of 5 seeds", fontsize=10)
for ax in axes:
    ax.set_xlabel("generation")
    ax.set_ylabel("best fitness (lower is better)")
    ax.grid(alpha=0.3)
    ax.legend(fontsize=7)
fig.tight_layout()
fig.savefig(os.path.join(OUT, "fitness_curves.png"), dpi=150)

# Statistics table.
stats = {}
for task, exp in [("task1", "baseline"), ("task2", "baseline"), ("task3", "gerono_scratch"),
                  ("task3", "gerono_default"), ("task3", "gerono_mirror"),
                  ("task3", "bernoulli_default"), ("task3", "bernoulli_scratch"), ("task3", "staged"),
                  ("task1", "no_smoothing"), ("task2", "no_smoothing"), ("task3", "no_smoothing")]:
    ss = [summary(r) for r in seeds(task, exp)]
    def ms(key):
        v = [s[key] for s in ss]
        return [statistics.mean(v), statistics.pstdev(v), min(v), max(v)]
    stats[f"{task}/{exp}"] = {k: ms(k) for k in ["final_fitness", "w", "e_end", "s", "out_of_bounds_fraction",
                                                  "end_distance_from_start", "seconds"]}
    stats[f"{task}/{exp}"]["commands"] = ss[0]["commands"]
    stats[f"{task}/{exp}"]["best_seed"] = os.path.basename(best_seed(task, exp))
json.dump(stats, open(os.path.join(OUT, "stats.json"), "w"), indent=1)
for k, v in stats.items():
    print(f"{k:28s} F {v['final_fitness'][0]:.3f}±{v['final_fitness'][1]:.3f}  W {v['w'][0]:.3f}±{v['w'][1]:.3f}  "
          f"E {v['e_end'][0]:.4f}  S {v['s'][0]:.3f}  end {v['end_distance_from_start'][0]:.2f}  t {v['seconds'][0]:.0f}s")
