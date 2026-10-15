// Builds the Phase 5 report (DOCX) from the run statistics and figures.
const fs = require("fs");
const path = require("path");
const {
  Document, Packer, Paragraph, TextRun, HeadingLevel, Table, TableRow, TableCell, WidthType, ShadingType,
  AlignmentType, ImageRun, LevelFormat, BorderStyle, Footer, PageNumber, TableOfContents, PageBreak,
} = require("docx");

const FIG = process.argv[2];
const OUT = process.argv[3];
const S = JSON.parse(fs.readFileSync(path.join(FIG, "stats.json"), "utf8"));

const f3 = (v) => v.toFixed(3);
const pm = (k, key, d = 3) => `${S[k][key][0].toFixed(d)} ± ${S[k][key][1].toFixed(d)}`;

// ---------- helpers ----------
const FONT = "Calibri";
const ACCENT = "1F4E79";
const PAGE_W = 9026; // A4 width minus 2 × 1440 margins (DXA)

// Inline markup: **bold**, `code`
function runs(text, base = {}) {
  const out = [];
  const re = /(\*\*[^*]+\*\*|`[^`]+`)/g;
  let last = 0, m;
  while ((m = re.exec(text))) {
    if (m.index > last) out.push(new TextRun({ text: text.slice(last, m.index), ...base }));
    const t = m[0];
    if (t.startsWith("**")) out.push(new TextRun({ text: t.slice(2, -2), bold: true, ...base }));
    else out.push(new TextRun({ text: t.slice(1, -1), font: "Consolas", size: 19, ...base }));
    last = m.index + t.length;
  }
  if (last < text.length) out.push(new TextRun({ text: text.slice(last), ...base }));
  return out;
}
const P = (text, opts = {}) => new Paragraph({ children: runs(text, opts.run || {}), spacing: { after: 120 }, ...opts.para });
const H1 = (t) => new Paragraph({ heading: HeadingLevel.HEADING_1, children: [new TextRun(t)] });
const H2 = (t) => new Paragraph({ heading: HeadingLevel.HEADING_2, children: [new TextRun(t)] });
const H3 = (t) => new Paragraph({ heading: HeadingLevel.HEADING_3, children: [new TextRun(t)] });
const B = (text, level = 0) => new Paragraph({ numbering: { reference: "bullets", level }, children: runs(text), spacing: { after: 60 } });
const N = (text, ref = "numbers") => new Paragraph({ numbering: { reference: ref, level: 0 }, children: runs(text), spacing: { after: 60 } });
const CODE = (text) => new Paragraph({
  children: [new TextRun({ text, font: "Consolas", size: 18 })],
  shading: { type: ShadingType.CLEAR, fill: "F2F2F2", color: "auto" },
  spacing: { after: 0 }, indent: { left: 200 },
});

const border = { style: BorderStyle.SINGLE, size: 4, color: "BFBFBF" };
const borders = { top: border, bottom: border, left: border, right: border };

function table(headers, rows, widths, opts = {}) {
  const total = widths.reduce((a, b) => a + b, 0);
  const cell = (text, isHeader, w, fill) => new TableCell({
    width: { size: w, type: WidthType.DXA },
    borders,
    shading: fill ? { type: ShadingType.CLEAR, fill, color: "auto" } : undefined,
    margins: { top: 60, bottom: 60, left: 100, right: 100 },
    children: String(text).split("\n").map((line) => new Paragraph({
      children: runs(line, isHeader ? { bold: true, color: "FFFFFF", size: 20 } : { size: 20 }),
    })),
  });
  return new Table({
    width: { size: total, type: WidthType.DXA },
    columnWidths: widths,
    rows: [
      new TableRow({ tableHeader: true, children: headers.map((h, i) => cell(h, true, widths[i], ACCENT)) }),
      ...rows.map((r, ri) => new TableRow({
        children: r.map((c, i) => cell(c, false, widths[i], (opts.highlightCol === i) ? "E2EFDA" : (ri % 2 ? "F7F9FC" : undefined))),
      })),
    ],
  });
}

// A shaded box used for each decision.
function callout(title, lines, fill = "FFF4E5", edge = "C55A11") {
  const e = { style: BorderStyle.SINGLE, size: 12, color: edge };
  return new Table({
    width: { size: PAGE_W, type: WidthType.DXA },
    columnWidths: [PAGE_W],
    rows: [new TableRow({ children: [new TableCell({
      width: { size: PAGE_W, type: WidthType.DXA },
      borders: { top: e, bottom: e, left: e, right: e },
      shading: { type: ShadingType.CLEAR, fill, color: "auto" },
      margins: { top: 100, bottom: 100, left: 160, right: 160 },
      children: [
        new Paragraph({ children: [new TextRun({ text: title, bold: true, color: edge })], spacing: { after: 80 } }),
        ...lines.map((l) => new Paragraph({ children: runs(l), spacing: { after: 60 } })),
      ],
    })] })],
  });
}

function pngSize(file) {
  const b = fs.readFileSync(file);
  return { w: b.readUInt32BE(16), h: b.readUInt32BE(20) };
}
let figNo = 0;
function figure(file, caption, widthPx = 600) {
  const { w, h } = pngSize(file);
  figNo++;
  return [
    new Paragraph({
      alignment: AlignmentType.CENTER,
      children: [new ImageRun({ type: "png", data: fs.readFileSync(file), transformation: { width: widthPx, height: Math.round(widthPx * h / w) } })],
      spacing: { before: 120, after: 60 },
    }),
    new Paragraph({ alignment: AlignmentType.CENTER, children: runs(`**Figure ${figNo}.** ${caption}`, { size: 19, italics: true }), spacing: { after: 200 } }),
  ];
}
const spacer = () => new Paragraph({ children: [], spacing: { after: 120 } });

// ---------- numbers used in the text ----------
const t1 = S["task1/baseline"], t2 = S["task2/baseline"];
const gd = S["task3/gerono_default"], gm = S["task3/gerono_mirror"], gs = S["task3/gerono_scratch"];
const bd = S["task3/bernoulli_default"], bm = S["task3/staged"], bs = S["task3/bernoulli_scratch"];
const ns = { 1: S["task1/no_smoothing"], 2: S["task2/no_smoothing"], 3: S["task3/no_smoothing"] };
const base = { 1: t1, 2: t2, 3: bm };
const P6 = JSON.parse(fs.readFileSync(process.argv[5], "utf8"));

// ---------- content ----------
const children = [];

// Title block
children.push(
  new Paragraph({ children: [new TextRun({ text: "WRCV402 Assignment 5", size: 28, color: "595959" })], spacing: { after: 60 } }),
  new Paragraph({ children: [new TextRun({ text: "Phase 5: Evolutionary Algorithm", bold: true, size: 48, color: ACCENT })], spacing: { after: 60 } }),
  new Paragraph({ children: [new TextRun({ text: "Implementation report and decisions for the group", size: 28 })], spacing: { after: 200 } }),
  new Paragraph({
    children: [new TextRun({ text: "Status as of 15 October 2026 · Assignment due 20 October 2026", size: 20, color: "595959" })],
    border: { bottom: { style: BorderStyle.SINGLE, size: 8, color: ACCENT, space: 6 } }, spacing: { after: 240 },
  }),
);

// Summary
children.push(
  H1("1. Summary"),
  P("Phase 5 evolves sequences of `<steering, throttle>` commands that make the racecar drive three tasks in simulation. The code is complete and runs end to end on the real Phase 3/4 neural-network simulator. It produces the Phase 6 command files in the lecturer's exact format, and 25 unit tests pass."),
  P(`All three tasks now work in simulation. Task 1 reaches its target in every seed (mean waypoint error ${f3(t1.w[0])} m). Task 2 follows the sine to within ${f3(t2.w[0])} m on average. Task 3 drives a closed figure eight that returns to the start (mean waypoint error ${f3(bm.w[0])} m).`),
  P(`Task 3 did not work as first planned: in all 5 seeds the car looped back to the start without driving the second lobe (mean waypoint error ${f3(gd.w[0])} m). **The group accepted two fixes on 15 October** (decisions D1 and D2): a figure-eight shape the car can physically drive, and starting stage 2 from a mirrored copy of the first lobe. Section 6 gives the evidence.`),
  P("**The Phase 6 files are ready** in the repository's `phase6/` folder, with a separate handoff document for the Phase 6 team. **Four smaller decisions remain open** (Table 1). None of them blocks Phase 6, but D4 must be confirmed with Mr Mulder before the testing slot."),
);

children.push(table(
  ["#", "Decision", "Status", "Choice / recommendation"],
  [
    ["D1", "Figure-eight shape", "**Accepted**", "Bernoulli lemniscate, a = 1.2 m (instead of the Gerono curve, whose 0.17 m bend the car cannot drive)"],
    ["D2", "How stage 2 of the figure eight starts", "**Accepted**", `Extend with the first lobe mirrored. Waypoint error ${f3(bd.w[0])} → ${f3(bm.w[0])} m`],
    ["D3", "Start of the sine wave", "Open", "Recommend starting at a crest (cosine) or adding a short lead-in. Low priority; files use the current sine"],
    ["D4", "Coordinate frame in the Phase 6 files", "Open: confirm", "Relative to the start pose, as in the lecturer's example. Phase 6 team to confirm with Mr Mulder"],
    ["D5", "Throttle range the EA may use", "Open", "Recommend keeping 0.25–0.40 (used for the current files)"],
    ["D6", "Smoothness weight λs", "Open", "Experiment 1 done (Section 8). Recommend keeping 0.1; revisit after Phase 6"],
  ],
  [500, 2300, 1400, 4826],
));
children.push(new Paragraph({ children: runs("**Table 1.** Decisions for the group.", { size: 19, italics: true }), alignment: AlignmentType.CENTER, spacing: { before: 80, after: 200 } }));

// Context
children.push(
  H1("2. Goal and context"),
  P("The brief asks for an evolutionary algorithm (EA) that finds command sequences for three tasks, evaluated in simulation, which Phase 6 then drives on the real car:"),
  table(["Task", "Description", "Requirement from the brief"], [
    ["1", "Drive to a target point", "Target at least 1 m from the start"],
    ["2", "Follow a sine-wave path", "Ends at least 2 m from the start"],
    ["3", "Drive a figure eight", "Returns to the start position"],
  ], [900, 3400, 4726]),
  spacer(),
  P("Commands are sent every 0.2 s. Steering ranges from 0.6 (hard left) to −0.6 (hard right), and throttle from 0 to 0.4. The simulator is the Phase 3 neural network (4 → 64 → 32 → 3), wrapped by the Phase 4 `Simulator` class. It takes the previous and current commands and predicts the car's movement (Δx, Δy, Δθ) in the car's own frame."),
  P("The code is in C# (.NET 8) to match the TorchSharp network and the Phase 4 class. Plots are made with small Python/matplotlib scripts. The repository is `Assignment5_Phase5`; Appendix A lists the commands."),
);

// Simulator
children.push(
  H1("3. The simulator"),
  H2("3.1 Integration with Phase 4"),
  P("The Phase 4 `Simulator.cs` and the trained model are copied unchanged into the repository (Phase 4 commit `d3fc22c`). The model file is byte-for-byte identical to Phase 3's `Results/Seed_42/best_model.dat`. The EA only ever talks to a small `ISimulator` interface, so the network can be swapped for a simple kinematic stand-in (\"stub\") by changing one config value."),
  P("A thin adapter converts between the two projects' conventions. Two details matter:"),
  B("**Field order.** Phase 4's command is (throttle, steering); ours is (steering, throttle), as in the brief's \"<steering value, throttle> tuple\". The adapter converts explicitly, and a unit test checks that hard-left steering turns the car left."),
  B("**The first step.** Phase 4's `StepSequence` reuses the first command as the \"previous\" command for the first step. We start from rest instead, so the previous command for the first step is (0, 0)."),
  H2("3.2 Measured behaviour"),
  P("A `measure` command drives the network through straight lines and circles. Table 2 shows the results. They set the chromosome lengths and the feasibility checks in Section 4."),
  table(["Quantity", "Value", "Notes"], [
    ["Speed at throttle 0.25 / 0.325 / 0.40", "0.43 / 0.745 / 1.01 m/s", "0.745 m/s at mid throttle is used as v_nominal"],
    ["Tightest turning radius (full lock, mid throttle)", "0.35 m left, 0.33 m right", "0.33 m used as the car's minimum radius"],
    ["Steering ±0.1", "2.4 m left, 1.6 m right", "The network is not symmetric"],
    ["Straight driving (steering 0)", "Drifts 7–16° over 6 s", "Small bias learned from the training data"],
    ["Time per evaluation (40 steps)", "≈5 ms serial, ≈1.3 ms parallel", "One full run of 30 000 evaluations: 10–35 s"],
  ], [3300, 2300, 3426]),
  new Paragraph({ children: runs("**Table 2.** Behaviour of the neural-network simulator.", { size: 19, italics: true }), alignment: AlignmentType.CENTER, spacing: { before: 80, after: 200 } }),
  P("The network also predicts a slight backwards movement (about 1.6 cm) on the very first step from rest, which the real car will not do. It is too small to matter for the tasks, but worth knowing when comparing simulated and real paths in Phase 6."),
);

// Design
children.push(
  H1("4. Design"),
  H2("4.1 Chromosome"),
  B("A chromosome is a list of genes. **Each gene is one command held for 2 timesteps (0.4 s)**. This halves the search space and builds smoothness into the representation, while 0.4 s is still fine control at these speeds."),
  B("Before simulation each gene is repeated twice, so the simulator and the Phase 6 file always see one command per 0.2 s."),
  B("Steering is clipped to ±0.6 and throttle to **0.25–0.40** (decision D5). Outside the range it was trained on, the network makes confident but wrong predictions, and the EA would exploit them."),
  B("Chromosome length follows from the measured speed: N = ceil(1.2 × path length / (0.745 m/s × 0.4 s)). The 20% slack gives the car time without letting it wander. This gives **6 genes for Task 1, 10 for Task 2 and 26 for Task 3** (20 for the original, smaller Gerono eight)."),
  H2("4.2 Waypoints"),
  P("Waypoints are generated relative to the start (start at the origin, heading along +x), then placed in the lab at the start pose (1.5 m, 2.0 m, heading 0). Moving the car for Phase 6 only changes the start pose. Waypoints are spaced about 0.18 m apart, and the program refuses to run if any waypoint is within 0.25 m of the track edge."),
  B("**Task 1:** one waypoint at (1.2, 0.6) m from the start, 1.34 m away and offset sideways so the car must steer as well as drive."),
  B("**Task 2:** y = 0.25 · sin(2πx / 2.0) for x from 0 to 2 m: one full period ending 2 m away. Its tightest bend has a radius of 0.41 m, just above the car's 0.33 m minimum."),
  B("**Task 3:** a lemniscate of Bernoulli with half-width a = 1.2 m (2.4 m × 0.85 m), starting and ending at its crossing point and rotated so the car starts along the curve. Its tightest bend has a radius of a/3 = 0.40 m. The first lobe and the full eight are used as two stages (Section 4.5). Decision D1 explains why this replaced the originally planned Gerono curve."),
  H2("4.3 Fitness function (lower is better)"),
  CODE("F = W + w_end · E_end + λs · S + p_oob · (fraction of path out of bounds)"),
  spacer(),
  table(["Term", "Meaning", "Weight", "Why"], [
    ["W", "Mean distance (m) from each waypoint to its nearest path point, searched in order", "1", "Ordering stops a path scoring well by visiting waypoints in the wrong order"],
    ["E_end", "Distance (m) from the end of the path to the final waypoint", "2.0", "Each task is defined by where it finishes; stops paths that drift at the end"],
    ["S", "Mean change between consecutive genes, scaled by each range", "0.1", "Jerky commands are unlike the training data and transfer badly to the real car"],
    ["P_oob", "Fraction of path points outside the track", "10", "A path off the driving surface is useless and would crash the car"],
  ], [900, 3300, 900, 3926]),
  spacer(),
  P("An optional heading term for the figure eight is implemented but switched off. There is deliberately no time or throttle penalty, because the fixed chromosome length already bounds the duration."),
  H2("4.4 Evolutionary algorithm"),
  table(["Component", "Choice", "Why"], [
    ["Population / generations", "100 / 300", "Conventional baseline; a run takes under a minute"],
    ["Selection", "Tournament of 3", "Moderate pressure, no fitness scaling needed"],
    ["Elitism", "Best 2 copied unchanged", "Best fitness never gets worse"],
    ["Crossover", "Two-point, rate 0.9", "Swaps whole runs of genes, so manoeuvres stay intact"],
    ["Mutation", "Gaussian on 10% of genes", "Noise on both steering and throttle, then clipped"],
    ["Mutation size", "8% → 1.5% of each range, linear over each stage", "Explore early, fine-tune late"],
    ["Initial population", "80% straight at mid throttle, 20% gentle constant turns", "Random starts give chaotic paths and slow convergence"],
    ["Randomness", "One seeded generator per run", "Every result can be reproduced exactly"],
  ], [2300, 3300, 3426]),
  spacer(),
  P("Fitness evaluations run in parallel. A run with the same seed gives the same result whether it runs in parallel or serially; a unit test checks this."),
  H2("4.5 Staged evolution (Task 3)"),
  P("Evolving a whole figure eight at once is a hard search, so it is done in two stages. **Stage 1** evolves only the first lobe (13 genes, 150 generations). **Stage 2** takes the best 20% of stage 1 and lengthens each to the full 26 genes by appending **its own genes with the steering reversed**: the second lobe of an eight is the mirror image of the first (decision D2). The rest of the population is filled with mutated copies, and the whole eight is evolved for another 150 generations. Mutation in stage 2 applies to the whole chromosome, so the first lobe can still adjust to link smoothly into the second. The total budget is the same as 300 generations from scratch, so the two can be compared fairly."),
  P("Mirroring only provides the starting population. Evolution still has to correct it, because the network is not symmetric (left and right turns differ, Table 2)."),
);

// Results
children.push(
  H1("5. Results"),
  P("Every configuration was run with **5 seeds**. Table 3 gives the mean ± standard deviation. F is the total fitness; W, E_end and S are its terms. No run had any path point out of bounds."),
  table(["Run", "F", "W (m)", "E_end (m)", "S", "Run time"], [
    ["Task 1", pm("task1/baseline", "final_fitness"), pm("task1/baseline", "w"), pm("task1/baseline", "e_end"), pm("task1/baseline", "s"), `${t1.seconds[0].toFixed(0)} s`],
    ["Task 2", pm("task2/baseline", "final_fitness"), pm("task2/baseline", "w"), pm("task2/baseline", "e_end"), pm("task2/baseline", "s"), `${t2.seconds[0].toFixed(0)} s`],
    ["**Task 3 (adopted: Bernoulli, staged, mirror)**", pm("task3/staged", "final_fitness"), pm("task3/staged", "w"), pm("task3/staged", "e_end"), pm("task3/staged", "s"), `${bm.seconds[0].toFixed(0)} s`],
    ["Task 3, Bernoulli, staged, straight extension", pm("task3/bernoulli_default", "final_fitness"), pm("task3/bernoulli_default", "w"), pm("task3/bernoulli_default", "e_end"), pm("task3/bernoulli_default", "s"), `${bd.seconds[0].toFixed(0)} s`],
    ["Task 3, Bernoulli, from scratch", pm("task3/bernoulli_scratch", "final_fitness"), pm("task3/bernoulli_scratch", "w"), pm("task3/bernoulli_scratch", "e_end"), pm("task3/bernoulli_scratch", "s"), `${bs.seconds[0].toFixed(0)} s`],
    ["Task 3, Gerono, staged, straight extension (original plan)", pm("task3/gerono_default", "final_fitness"), pm("task3/gerono_default", "w"), pm("task3/gerono_default", "e_end"), pm("task3/gerono_default", "s"), `${gd.seconds[0].toFixed(0)} s`],
    ["Task 3, Gerono, staged, mirror", pm("task3/gerono_mirror", "final_fitness"), pm("task3/gerono_mirror", "w"), pm("task3/gerono_mirror", "e_end"), pm("task3/gerono_mirror", "s"), `${gm.seconds[0].toFixed(0)} s`],
    ["Task 3, Gerono, from scratch", pm("task3/gerono_scratch", "final_fitness"), pm("task3/gerono_scratch", "w"), pm("task3/gerono_scratch", "e_end"), pm("task3/gerono_scratch", "s"), `${gs.seconds[0].toFixed(0)} s`],
  ], [2826, 1300, 1300, 1300, 1300, 1000]),
  new Paragraph({ children: runs("**Table 3.** Final results, mean ± standard deviation over 5 seeds. Gerono and Bernoulli rows are measured against different curves, so compare shapes visually as well as by number. Run times grew during long batches, so treat them as rough.", { size: 19, italics: true }), alignment: AlignmentType.CENTER, spacing: { before: 80, after: 200 } }),
  ...figure(path.join(FIG, "tasks12_paths.png"), "Tasks 1 and 2: simulated paths of the best seed. Orange circles are waypoints; the dashed line is the reference sine.", 600),
  P(`**Task 1** reaches the target exactly (end error ${t1.e_end[0].toFixed(4)} m on average). **Task 2** follows the sine and ends exactly 2 m from the start. Most of its error is at the start, where the sine leaves at 38° to the car's heading (decision D3).`),
  ...figure(path.join(FIG, "task3_variants.png"), "Task 3: best seed of each figure-eight variant. Only the two \"mirror\" runs produce a proper eight. With the original straight stage-2 extension, the Gerono run collapses into one big loop and the Bernoulli run loses the second lobe. Bottom right is the adopted design.", 560),
  ...figure(path.join(FIG, "fitness_curves.png"), "Best fitness against generation (mean of 5 seeds). Left: each task with the adopted settings. Right: Task 3 (Bernoulli) staged vs from-scratch evolution; the red line marks the start of stage 2.", 620),
  P("The jump at the stage boundary is expected. Stage 2 is scored against the whole eight, so the stage-1 solutions start with a large endpoint error."),
);

// Decisions
children.push(
  H1("6. Decisions"),
  P("D1 and D2 were accepted by the group on 15 October 2026. Their evidence is kept here for the final report. D3–D6 are still open."),
  H2("D1. Figure-eight shape (accepted: Bernoulli)"),
  P("The plan used a lemniscate of Gerono, x = a·sin t, y = (b/2)·sin 2t, with a = b = 0.8 m. It assumed the tightest bend was at the lobe tips (radius 0.8 m). **That is not the case**: the tightest bend is at the \"shoulders\" of each lobe, with a radius of only **0.17 m**, about half the car's 0.33 m minimum. The car cannot drive it, so the EA cuts those corners (visible in Figure 2). Resizing does not help within the track: Table 4 shows that the radius only reaches 0.375 m at a = 1.5 m (3 m wide)."),
  table(["Gerono a, b (m)", "Tightest radius (m)", "", "Bernoulli a (m)", "Tightest radius (m)", "Size (m)"], [
    ["0.8, 0.8 (planned)", "0.17", "", "1.0", "0.33", "2.0 × 0.7"],
    ["1.2, 0.8", "0.30", "", "1.2 (proposed)", "0.40", "2.4 × 0.85"],
    ["1.5, 1.0", "0.375", "", "1.5", "0.50", "3.0 × 1.06"],
  ], [1700, 1500, 300, 1700, 1700, 2126]),
  new Paragraph({ children: runs("**Table 4.** Tightest bend of the two figure-eight shapes. The car's minimum radius is 0.33 m.", { size: 19, italics: true }), alignment: AlignmentType.CENTER, spacing: { before: 80, after: 160 } }),
  callout("Decision D1 — accepted: B", [
    "**A. Keep the Gerono curve** (a = b = 0.8 m). The EA still produces a recognisable eight with mirror seeding, but cuts the corners it cannot drive.",
    "**B. Switch to a Bernoulli lemniscate** with a = 1.2 m. Its tightest bend is at the tips, a/3 = 0.4 m, which the car can drive. It is 2.4 m × 0.85 m and fits the track.",
    "**Chosen: B.** The car can physically follow it, so Phase 6 is more likely to match the simulation. Trade-offs: the margin over the car's 0.33 m minimum radius is small (it holds at mid throttle), and the path is longer (6.3 m vs 4.9 m), so it needs 26 genes and more track time.",
  ], "E2EFDA", "548235"),
  spacer(),
  H2("D2. How stage 2 of the figure eight starts (accepted: mirror)"),
  P(`As planned, stage 2 lengthens the stage-1 solutions with "drive straight at mid throttle" genes. Stage 1 works well (first lobe within about 0.05 m). But after the straight extension the car ends about 3 m from the finish, and the endpoint term (weight 2.0) dominates. The quickest way for the EA to reduce it is to sacrifice the second lobe and simply loop back to the start, which it does in all 5 seeds of both shapes (Figure 2, left column).`),
  P(`An alternative extension is implemented (\`ea.extend_mode = "mirror"\`). The second lobe of a figure eight is the mirror image of the first, so each seed is lengthened with **its own genes with the steering reversed**, plus a little noise. Stage 2 then starts from an approximate eight and only needs to refine it. With the same evaluation budget, the mean waypoint error falls from ${f3(gd.w[0])} m to ${f3(gm.w[0])} m (Gerono) and from ${f3(bd.w[0])} m to ${f3(bm.w[0])} m (Bernoulli).`),
  callout("Decision D2 — accepted: B", [
    "**A. Keep the straight extension.** Fails on this task as shown.",
    "**B. Use the mirror extension.** Big improvement at no extra cost.",
    "**Chosen: B.** Trade-offs to state in the final report: it only suits symmetric tasks, and it builds domain knowledge into the starting population. It is seeding only; evolution still refines the whole chromosome and has to correct the asymmetry of the network.",
  ], "E2EFDA", "548235"),
  spacer(),
  H2("D3. Start of the sine wave"),
  P("A sine starting at zero leaves the start at 38° to the car's heading (its slope at x = 0). The car starts pointing along +x, so it cannot follow the first ~0.3 m exactly, and this inflates Task 2's error."),
  callout("Decision D3 — options", [
    "**A. Keep it.** Accept the start error and explain it in the report.",
    "**B. Start at a crest** (a cosine): the path then starts parallel to the car's heading. Still a sine wave; needs a small code change.",
    "**C. Keep it, but add a short straight lead-in** before the sine starts.",
    "**Recommendation: B or C.** Low priority compared with D1 and D2.",
  ]),
  spacer(),
  H2("D4. Coordinate frame in the Phase 6 files"),
  P("The lecturer's example file starts at (0, 0) with heading 0, so its poses look relative to the start, not lab coordinates. Our files do the same: poses are converted back to the start frame before writing, whatever start pose is used in the lab."),
  callout("Decision D4 — options", [
    "**A. Poses relative to the start** (current, matches the example).",
    "**B. Lab coordinates.**",
    "**Recommendation: A, but the Phase 6 team should confirm with Mr Mulder** before the testing slot, because the lab software might expect lab coordinates.",
  ]),
  spacer(),
  H2("D5. Throttle range"),
  P("Throttle is limited to 0.25–0.40. In the Phase 3 training data the 5th–95th percentile of throttle is 0.23–0.41, and below 0.2 there are very few samples, so the network's predictions there are unreliable. The brief's 0.4 maximum caps the top. The lecturer's example file also stays within 0.3–0.4."),
  callout("Decision D5 — options", [
    "**A. 0.25–0.40** (current).",
    "**B. 0–0.40** (the brief's full range). Gives the EA more freedom, but lets it exploit parts of the network that were barely trained.",
    "**Recommendation: A.**",
  ]),
  spacer(),
  H2("D6. Smoothness weight λs"),
  P("λs controls how strongly jerky command sequences are penalised. It is the main lever on the gap between simulation and reality, but too high a value stops the car turning sharply enough for the figure eight. It is currently 0.1, and experiment 1 (Section 8.1) compares it with 0."),
  callout("Decision D6 — options", [
    "**Keep 0.1** for the Phase 6 files (current). Smoother commands, and in simulation also more accurate and more reliable than λs = 0.",
    "**Change it** (e.g. 0 or 0.2) and regenerate the files.",
    "**Recommendation: keep 0.1** for testing. If the Phase 6 slot allows, also drive one λs = 0 solution to see whether smooth commands track better on the real car.",
  ]),
);

// Phase 6 format
children.push(
  H1("7. Phase 6 deliverable"),
  P("Each run writes `solution.csv` in the layout of the lecturer's example (`TestingFormatExample.csv`):"),
  B("No header row; one row per 0.2 s timestep; columns `x, y, θ, τ, σ` (throttle before steering)."),
  B("Each pose is the predicted pose **after** that row's command. The start pose is not a row."),
  B("Poses relative to the start pose (decision D4); θ in radians in [−π, π]."),
  B("Numbers written as 32-bit floats with a decimal point (never a comma, whatever the Windows region settings), UTF-8 without a BOM, Windows line endings."),
  P("A unit test checks all of this. Example (first rows of a Task 1 solution):"),
  ...fs.readFileSync(path.join(process.argv[4], "task1", "baseline", S["task1/baseline"].best_seed, "solution.csv"), "utf8")
    .split("\r\n").slice(0, 3).map(CODE),
  spacer(),
  P("**The files for the testing slot are in the repository's** `phase6/` **folder**: the best of 5 seeds for each task, with the adopted settings. The separate **Phase 6 handoff document** explains how to use them, how much floor space each task needs, what to confirm with Mr Mulder, and how to compare the real runs with the simulation (`scripts/compare_real.py`)."),
  table(["Task", "File", "Commands", "Duration", "Predicted end"], P6.map((e) => [
    `${e.task}. ${e.title}`, e.file, String(e.rows), `${e.duration_s.toFixed(1)} s`,
    `${e.end_distance_from_start_m.toFixed(2)} m from start`,
  ]), [2626, 2200, 1300, 1300, 1600]),
  new Paragraph({ children: runs("**Table 5.** Files handed to Phase 6.", { size: 19, italics: true }), alignment: AlignmentType.CENTER, spacing: { before: 80, after: 200 } }),
);

// Experiments
const expRow = (k) => [`Task ${k}`,
  `${f3(base[k].final_fitness[0])} / ${f3(ns[k].final_fitness[0])}`,
  `${f3(base[k].w[0])} / ${f3(ns[k].w[0])}`,
  `${f3(base[k].s[0])} / ${f3(ns[k].s[0])}`];
children.push(
  H1("8. Experiments"),
  H2("8.1 Experiment 1: smoothness penalty on vs off"),
  P("Each task was run with 5 seeds at λs = 0.1 (adopted) and λs = 0. Table 6 compares the means. F is not directly comparable (it includes λs · S), so the useful columns are W (how closely the path follows the waypoints) and S (how jerky the commands are)."),
  table(["Task", "F (λs 0.1 / 0)", "W in m (λs 0.1 / 0)", "S (λs 0.1 / 0)"], [1, 2, 3].map(expRow), [1800, 2400, 2400, 2426]),
  new Paragraph({ children: runs("**Table 6.** Experiment 1, mean of 5 seeds.", { size: 19, italics: true }), alignment: AlignmentType.CENTER, spacing: { before: 80, after: 160 } }),
  P(`Without the penalty, the commands are ${(ns[1].s[0] / t1.s[0]).toFixed(1)}×, ${(ns[2].s[0] / t2.s[0]).toFixed(1)}× and ${(ns[3].s[0] / bm.s[0]).toFixed(1)}× as jerky (Tasks 1–3). Accuracy does **not** improve in return. Task 1 reaches its target either way. Tasks 2 and 3 are less accurate without the penalty (W ${f3(ns[2].w[0])} vs ${f3(t2.w[0])} m, and ${f3(ns[3].w[0])} vs ${f3(bm.w[0])} m), and Task 3 becomes unreliable: one of the 5 seeds failed to form an eight, and the standard deviation of W rises from ${f3(bm.w[1])} m to ${f3(ns[3].w[1])} m.`),
  P("So in simulation the penalty works as a regulariser: it keeps the search among smooth, sensible manoeuvres, and the EA finds good solutions more consistently. Whether smoother commands also track better on the real car can only be answered by Phase 6."),
  H2("8.2 Experiment 2: staged vs from-scratch evolution (Task 3)"),
  P(`Both use the same budget: 150 + 150 generations staged, or 300 from scratch, with a population of 100. For the adopted Bernoulli eight, staged evolution with mirroring reaches a mean waypoint error of ${f3(bm.w[0])} m, against ${f3(bs.w[0])} m from scratch and ${f3(bd.w[0])} m staged with the straight extension (Table 3, Figure 3). The same pattern holds for the Gerono eight (${f3(gm.w[0])}, ${f3(gs.w[0])} and ${f3(gd.w[0])} m). Staging only helps when stage 2 starts from a sensible extension of stage 1.`),
  H2("8.3 Remaining work"),
  N("**Phase 6 team:** confirm D4 with Mr Mulder, then drive the three files and record the telemetry."),
  N("**After Phase 6:** compare real and simulated paths with `scripts/compare_real.py`. If they differ a lot, revisit λs (D6) or the throttle range (D5) and regenerate."),
  N("**Optional:** decide D3 (sine start) and regenerate Task 2 if it changes."),
  N("**Final report:** the brief asks Phase 5 for fitness-vs-time graphs and an x–y plot of each task's simulated path. Figures 1–3 cover these; `scripts/plot_fitness.py` and `scripts/plot_path.py` can redraw them from any run folder."),
);

// Appendix A
children.push(
  new Paragraph({ children: [new PageBreak()] }),
  H1("Appendix A. How to run"),
  P("From the `Assignment5_Phase5` folder (requires the .NET 8 SDK; plots need Python with matplotlib):"),
  ...[
    "dotnet test",
    "dotnet run --project src -- measure",
    "dotnet run --project src -- run --task 1",
    "dotnet run --project src -- run --task 3 --seeds 1-5",
    "dotnet run --project src -- run --task 3 --config configs/task3_scratch.json --seeds 1-5",
    "dotnet run --project src -- run --task 2 --experiment no_smoothing --set fitness.lambda_s=0 --seeds 1-5",
    "python scripts/plot_path.py results/task3/staged/seed1",
    "python scripts/plot_fitness.py results/task3/staged/seed1 --log",
    "python scripts/make_phase6_package.py",
    "python scripts/compare_real.py phase6/task1_solution.csv real_log.csv",
  ].map(CODE),
  spacer(),
  P("Results go to `results/<task>/<experiment>/seed<k>/`: `solution.csv` (Phase 6), `fitness_log.csv`, `best_path.csv`, `waypoints.csv`, `config.json` (exact settings used) and `summary.json`. `--set key=value` overrides any config value."),
  H1("Appendix B. Code layout"),
  table(["Folder", "Contents"], [
    ["src/Shared", "Command and Location types, the ISimulator interface, angle and frame maths"],
    ["src/Simulation", "Adapter around the Phase 4 simulator, the stub simulator, and the Phase 4 code itself"],
    ["src/Tasks", "Config loading, waypoint generation for the three tasks, feasibility checks"],
    ["src/Fitness", "The fitness function and its terms"],
    ["src/Evolution", "Chromosome, operators, the (staged) EA loop"],
    ["src/Output", "Writers for the logs, paths and the Phase 6 file"],
    ["src/Runner", "Command-line program (run, measure)"],
    ["configs", "task1.json, task2.json, task3.json, task3_scratch.json"],
    ["scripts", "plot_fitness.py, plot_path.py, make_phase6_package.py, compare_real.py"],
    ["phase6", "Solution files, expected-path plots and manifest for the Phase 6 team"],
    ["tests", "25 unit tests"],
  ], [2300, 6726]),
  spacer(),
  H1("Appendix C. Unit tests"),
  P("The tests cover the points that are easy to get subtly wrong and hard to spot in a plot:"),
  B("Frame conversion, using the worked example from the brief: from (10, 10, 45°), the local movement (10.606, −3.536, −0.7853) lands at (20, 15, 0)."),
  B("Angle wrapping across ±π in both directions."),
  B("The ordered waypoint error scores a path that visits waypoints out of order worse than one that visits them in order."),
  B("Gene expansion, clipping under extreme mutation, and crossover."),
  B("The neural-network adapter: field order, starting from rest, and a full left circle under constant left steering."),
  B("The Phase 6 file format, written under a culture that uses a decimal comma."),
  B("Reproducibility, including parallel vs serial evaluation, and that best fitness never gets worse within a stage."),
);

// ---------- document ----------
const doc = new Document({
  creator: "WRCV402 group",
  title: "WRCV402 Assignment 5 — Phase 5 report",
  styles: {
    default: { document: { run: { font: FONT, size: 22 } } },
    paragraphStyles: [
      { id: "Heading1", name: "Heading 1", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 32, bold: true, font: FONT, color: ACCENT }, paragraph: { spacing: { before: 360, after: 140 }, outlineLevel: 0, keepNext: true, keepLines: true } },
      { id: "Heading2", name: "Heading 2", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 26, bold: true, font: FONT, color: ACCENT }, paragraph: { spacing: { before: 240, after: 100 }, outlineLevel: 1, keepNext: true, keepLines: true } },
      { id: "Heading3", name: "Heading 3", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 23, bold: true, font: FONT }, paragraph: { spacing: { before: 200, after: 80 }, outlineLevel: 2, keepNext: true, keepLines: true } },
    ],
  },
  numbering: {
    config: [
      { reference: "bullets", levels: [{ level: 0, format: LevelFormat.BULLET, text: "•", alignment: AlignmentType.LEFT,
        style: { paragraph: { indent: { left: 540, hanging: 270 } } } }] },
      { reference: "numbers", levels: [{ level: 0, format: LevelFormat.DECIMAL, text: "%1.", alignment: AlignmentType.LEFT,
        style: { paragraph: { indent: { left: 540, hanging: 300 } } } }] },
    ],
  },
  sections: [{
    properties: { page: { size: { width: 11906, height: 16838 }, margin: { top: 1440, bottom: 1440, left: 1440, right: 1440 } } },
    footers: { default: new Footer({ children: [new Paragraph({ alignment: AlignmentType.CENTER,
      children: [new TextRun({ text: "WRCV402 Assignment 5 · Phase 5 report · page ", size: 18, color: "7F7F7F" }),
        new TextRun({ children: [PageNumber.CURRENT], size: 18, color: "7F7F7F" })] })] }) },
    children,
  }],
});

Packer.toBuffer(doc).then((buf) => { fs.writeFileSync(OUT, buf); console.log("wrote", OUT); });
