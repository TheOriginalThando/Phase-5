"""Compare a real-world run (Phase 6) with the path predicted by the simulator.

Usage:
  python scripts/compare_real.py phase6/task1_solution.csv real_log.csv [-o task1_real_vs_sim.png]
                                 [--start-row N]

real_log.csv is the lab's telemetry export, in the same format as the Phase 1 log
(columns obs_pose_position_x, obs_pose_position_y, obs_pose_yaw, cmd_motor_throttle, cmd_steering_servo).

The real start pose is the logged pose on the row just before the first command (by default the first row
whose throttle is above zero; override with --start-row, a 0-based data row). The real path is expressed
relative to that pose, the same frame as solution.csv, so the two paths start at the same point
("anchored to start", like the lecturer's plot). Rows are matched by timestep (one row per 0.2 s).
"""
import argparse
import csv
import math

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402


def wrap(a):
    return math.atan2(math.sin(a), math.cos(a))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("solution", help="solution.csv sent to the car (x,y,theta,throttle,steering; no header)")
    ap.add_argument("real_log", help="telemetry CSV from the lab")
    ap.add_argument("--start-row", type=int, help="0-based data row of the first command (default: first throttle > 0)")
    ap.add_argument("-o", "--out", default="real_vs_sim.png")
    args = ap.parse_args()

    with open(args.solution, newline="") as f:
        sim = [[float(v) for v in row] for row in csv.reader(f) if row]
    with open(args.real_log, newline="") as f:
        log = list(csv.DictReader(f))

    start = args.start_row
    if start is None:
        start = next(i for i, r in enumerate(log) if float(r["cmd_motor_throttle"] or 0) > 0)
    if start < 1:
        raise SystemExit("Need at least one logged row before the first command to get the start pose.")

    s = log[start - 1]
    x0, y0, th0 = float(s["obs_pose_position_x"]), float(s["obs_pose_position_y"]), float(s["obs_pose_yaw"])
    c, sn = math.cos(th0), math.sin(th0)

    real = []
    for r in log[start:start + len(sim)]:
        dx, dy = float(r["obs_pose_position_x"]) - x0, float(r["obs_pose_position_y"]) - y0
        real.append((dx * c + dy * sn, -dx * sn + dy * c, wrap(float(r["obs_pose_yaw"]) - th0),
                     float(r["cmd_motor_throttle"]), float(r["cmd_steering_servo"])))
    n = min(len(real), len(sim))
    if n < len(sim):
        print(f"Warning: the log has only {n} rows after the start; the solution has {len(sim)}.")

    errs = [math.hypot(real[i][0] - sim[i][0], real[i][1] - sim[i][1]) for i in range(n)]
    cmd_diff = max(max(abs(real[i][3] - sim[i][3]), abs(real[i][4] - sim[i][4])) for i in range(n))
    print(f"Start row {start}, real start pose ({x0:.3f}, {y0:.3f}, {th0:.3f} rad), {n} steps compared")
    print(f"Position error: mean {sum(errs) / n:.3f} m, max {max(errs):.3f} m, at the end {errs[-1]:.3f} m")
    print(f"Largest difference between logged and planned commands: {cmd_diff:.3f}"
          + ("  <- check the alignment (--start-row) or the command order" if cmd_diff > 0.05 else ""))

    fig, ax = plt.subplots(figsize=(7, 7))
    ax.plot([0] + [p[0] for p in sim[:n]], [0] + [p[1] for p in sim[:n]], color="tab:red", lw=2,
            label="predicted (simulator)")
    ax.plot([0] + [p[0] for p in real[:n]], [0] + [p[1] for p in real[:n]], color="tab:blue", lw=2,
            label="actual (tracking system)")
    ax.plot(0, 0, "o", color="k", ms=8, label="start")
    ax.set_aspect("equal")
    ax.grid(alpha=0.3)
    ax.set_xlabel("x relative to start [m]")
    ax.set_ylabel("y relative to start [m]")
    ax.set_title(f"Real vs simulated path (mean error {sum(errs) / n:.2f} m, end error {errs[-1]:.2f} m)")
    ax.legend()
    fig.savefig(args.out, dpi=150, bbox_inches="tight")
    print(args.out)


if __name__ == "__main__":
    main()
