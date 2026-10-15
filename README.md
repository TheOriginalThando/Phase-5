# WRCV402 Assignment 5 — Phase 5: Evolutionary Algorithm

Evolves `<steering, throttle>` command sequences for the CPM Lab racecar, using the Phase 3/4 neural-network simulator.
The design, and the reasons behind it, are in [CLAUDE.md](CLAUDE.md).

## Requirements

- .NET 8 SDK
- Python 3 with matplotlib (`pip install -r scripts/requirements.txt`), for the plots only

## Commands

Run everything from the repository root.

```bash
dotnet build
dotnet test
```

Evolve a task. Outputs go to `results/<task>/<experiment>/seed<k>/`:

```bash
dotnet run --project src -- run --task 1
dotnet run --project src -- run --task 3 --seeds 1-5
dotnet run --project src -- run --task 3 --config configs/task3_scratch.json --seeds 1-5
dotnet run --project src -- run --task 2 --experiment no_smoothing --set fitness.lambda_s=0 --seeds 1-5
dotnet run --project src -- run --task 1 --simulator stub
```

`--set key=value` overrides any config value. Use dots for nested keys, for example `--set ea.population=200`.

To sanity-check the simulator and measure speed, turning radius and evaluation time (CLAUDE.md §12):

```bash
dotnet run --project src -- measure
```

To make plots:

```bash
python scripts/plot_path.py results/task3/staged/seed1
python scripts/plot_fitness.py results/task3/staged/seed1 results/task3/staged/seed2 --log
```

## Output files

| File | Contents |
|---|---|
| `solution.csv` | **Phase 6 deliverable.** `x,y,θ,τ,σ` per timestep, no header, pose after the command and relative to the start pose (CLAUDE.md §9) |
| `fitness_log.csv` | `generation,stage,best,mean,std` plus the best individual's W, E_end, S and out-of-bounds fraction |
| `best_path.csv` | `x,y,theta` of the best path in the lab frame, start included |
| `waypoints.csv`, `reference_curve.csv` | Task geometry in the lab frame, used by the plots |
| `config.json` | The exact config used, including overrides |
| `summary.json` | Final fitness terms, run time and distances |

## Layout

`src/Phase5.csproj` is a single console project. Its folders follow CLAUDE.md §11. `src/Simulation/Phase4/Simulator.cs` and `models/` are copied unchanged from the Phase 4 repo (commit `d3fc22c`). The model is identical to the Phase 3 `Results/Seed_42/best_model.dat`.
