// Builds the Phase 6 handoff document (DOCX) from phase6/manifest.json and the expected-path plots.
const fs = require("fs");
const path = require("path");
const {
  Document, Packer, Paragraph, TextRun, HeadingLevel, Table, TableRow, TableCell, WidthType, ShadingType,
  AlignmentType, ImageRun, LevelFormat, BorderStyle, Footer, PageNumber, PageBreak,
} = require("docx");

const P6DIR = process.argv[2];
const OUT = process.argv[3];
const M = JSON.parse(fs.readFileSync(path.join(P6DIR, "manifest.json"), "utf8"));

const FONT = "Calibri", ACCENT = "1F4E79", PAGE_W = 9026;
function runs(text, base = {}) {
  const out = []; const re = /(\*\*[^*]+\*\*|`[^`]+`)/g; let last = 0, m;
  while ((m = re.exec(text))) {
    if (m.index > last) out.push(new TextRun({ text: text.slice(last, m.index), ...base }));
    const t = m[0];
    out.push(t.startsWith("**") ? new TextRun({ text: t.slice(2, -2), bold: true, ...base })
      : new TextRun({ text: t.slice(1, -1), font: "Consolas", size: 19, ...base }));
    last = m.index + t.length;
  }
  if (last < text.length) out.push(new TextRun({ text: text.slice(last), ...base }));
  return out;
}
const P = (t, o = {}) => new Paragraph({ children: runs(t, o.run || {}), spacing: { after: 120 }, ...o.para });
const H1 = (t) => new Paragraph({ heading: HeadingLevel.HEADING_1, children: [new TextRun(t)] });
const H2 = (t) => new Paragraph({ heading: HeadingLevel.HEADING_2, children: [new TextRun(t)] });
const B = (t) => new Paragraph({ numbering: { reference: "bullets", level: 0 }, children: runs(t), spacing: { after: 60 } });
const N = (t, ref = "numbers") => new Paragraph({ numbering: { reference: ref, level: 0 }, children: runs(t), spacing: { after: 60 } });
const CHECK = (t) => new Paragraph({ numbering: { reference: "checks", level: 0 }, children: runs(t), spacing: { after: 80 } });
const CODE = (t) => new Paragraph({ children: [new TextRun({ text: t, font: "Consolas", size: 18 })],
  shading: { type: ShadingType.CLEAR, fill: "F2F2F2", color: "auto" }, indent: { left: 200 } });
const spacer = () => new Paragraph({ children: [], spacing: { after: 120 } });
const caption = (t) => new Paragraph({ children: runs(t, { size: 19, italics: true }), alignment: AlignmentType.CENTER, spacing: { before: 80, after: 200 } });

const border = { style: BorderStyle.SINGLE, size: 4, color: "BFBFBF" };
const borders = { top: border, bottom: border, left: border, right: border };
function table(headers, rows, widths) {
  const total = widths.reduce((a, b) => a + b, 0);
  const cell = (text, hdr, w, fill) => new TableCell({
    width: { size: w, type: WidthType.DXA }, borders,
    shading: fill ? { type: ShadingType.CLEAR, fill, color: "auto" } : undefined,
    margins: { top: 60, bottom: 60, left: 100, right: 100 },
    children: String(text).split("\n").map((l) => new Paragraph({ children: runs(l, hdr ? { bold: true, color: "FFFFFF", size: 20 } : { size: 20 }) })),
  });
  return new Table({ width: { size: total, type: WidthType.DXA }, columnWidths: widths, rows: [
    new TableRow({ tableHeader: true, children: headers.map((h, i) => cell(h, true, widths[i], ACCENT)) }),
    ...rows.map((r, ri) => new TableRow({ children: r.map((c, i) => cell(c, false, widths[i], ri % 2 ? "F7F9FC" : undefined)) })),
  ] });
}
function callout(title, lines, fill = "FFF4E5", edge = "C55A11") {
  const e = { style: BorderStyle.SINGLE, size: 12, color: edge };
  return new Table({ width: { size: PAGE_W, type: WidthType.DXA }, columnWidths: [PAGE_W], rows: [new TableRow({ children: [new TableCell({
    width: { size: PAGE_W, type: WidthType.DXA }, borders: { top: e, bottom: e, left: e, right: e },
    shading: { type: ShadingType.CLEAR, fill, color: "auto" }, margins: { top: 100, bottom: 100, left: 160, right: 160 },
    children: [new Paragraph({ children: [new TextRun({ text: title, bold: true, color: edge })], spacing: { after: 80 } }),
      ...lines.map((l) => new Paragraph({ children: runs(l), spacing: { after: 60 } }))],
  })] })] });
}
function image(file, widthPx) {
  const b = fs.readFileSync(file);
  const w = b.readUInt32BE(16), h = b.readUInt32BE(20);
  return new Paragraph({ alignment: AlignmentType.CENTER, spacing: { before: 120, after: 60 },
    children: [new ImageRun({ type: "png", data: b, transformation: { width: widthPx, height: Math.round(widthPx * h / w) } })] });
}

const m2 = (v) => (Math.abs(v) < 0.005 ? 0 : v).toFixed(2);
const children = [];

children.push(
  new Paragraph({ children: [new TextRun({ text: "WRCV402 Assignment 5", size: 28, color: "595959" })], spacing: { after: 60 } }),
  new Paragraph({ children: [new TextRun({ text: "Phase 6 handoff", bold: true, size: 48, color: ACCENT })], spacing: { after: 60 } }),
  new Paragraph({ children: [new TextRun({ text: "Command files from Phase 5 for real-world testing", size: 28 })], spacing: { after: 200 } }),
  new Paragraph({ children: [new TextRun({ text: "Prepared 15 October 2026 by the Phase 5 team · Assignment due 20 October 2026", size: 20, color: "595959" })],
    border: { bottom: { style: BorderStyle.SINGLE, size: 8, color: ACCENT, space: 6 } }, spacing: { after: 240 } }),
  P("This document goes with the `phase6/` folder of the Phase 5 repository (`Assignment5_Phase5`). It lists the three command files to drive on the real car, what to confirm with Mr Mulder before the slot, how to set up each run, and how to compare the real paths with the simulation afterwards."),
);

// 1. Files
children.push(
  H1("1. The files"),
  table(["Task", "File", "Commands", "Duration", "Free floor space needed"], M.map((e) => [
    `${e.task}. ${e.title}`, e.file, String(e.rows), `${e.duration_s.toFixed(1)} s`,
    `${m2(e.free_space_size_m[0])} m × ${m2(e.free_space_size_m[1])} m`,
  ]), [2426, 2100, 1100, 1100, 2300]),
  caption("**Table 1.** Command files. Each is the best of 5 evolved solutions for its task."),
  P("Each file has the same layout as the lecturer's `TestingFormatExample.csv`:"),
  B("**No header row.** One row per command, sent every **0.2 s**, starting with the first row."),
  B("**Columns x, y, θ, τ, σ**: the simulator's predicted pose (x, y in metres, θ in radians), then **throttle τ**, then **steering σ**. Throttle comes before steering."),
  B("**The pose on each row is where the car is predicted to be after that row's command.** Poses are **relative to the car's start pose**: the car starts at (0, 0) facing along +x, with +y to its left."),
  B(`Throttle stays within ${Math.min(...M.map((e) => e.throttle_range[0])).toFixed(2)}–${Math.max(...M.map((e) => e.throttle_range[1])).toFixed(2)} (forward only), and steering within ±0.6, where positive means left.`),
  B("The predictions assume the car **starts at rest**."),
  P("The folder also contains an expected-path plot for each task (`taskN_expected_path.png`, reproduced in Section 3) and `manifest.json` with the numbers in this document."),
);

// 2. Confirm with Mr Mulder
children.push(
  H1("2. Confirm with Mr Mulder before the slot"),
  P("These points decide whether the files are run as intended. Please check each one when booking or at the start of the slot:"),
  CHECK("**Coordinate frame.** Our x, y, θ columns are relative to the start pose, like the lecturer's example. Does the lab software expect this, or lab coordinates? If it needs lab coordinates, tell the Phase 5 team the start pose you will use and we will regenerate the files in minutes."),
  CHECK("**Are the x, y, θ columns used at all?** If the software only sends τ and σ, the frame question above does not affect the run, only the plots."),
  CHECK("**Timing.** The first row is sent immediately and each later row 0.2 s after the previous one."),
  CHECK("**Steering sign.** Positive σ turns left (as in the brief). If the car is set up the other way, every path will be mirrored."),
  CHECK("**Start from rest.** The car should be stationary when the first command is sent."),
  CHECK("**Telemetry export.** We need the run's log in the same format as the Phase 1 data (with `obs_pose_position_x`, `obs_pose_position_y`, `obs_pose_yaw`, `cmd_motor_throttle`, `cmd_steering_servo`) to compare against the simulation."),
  spacer(),
);

// 3. Per-task sheets
children.push(H1("3. Task sheets"));
M.forEach((e, i) => {
  const b = e.free_space_box_m;
  children.push(
    ...(i > 0 ? [new Paragraph({ children: [new PageBreak()] })] : []),
    H2(`Task ${e.task}: ${e.title}`),
    image(path.join(P6DIR, e.plot), 470),
    caption(`**Figure ${i + 1}.** Expected path relative to the start. The car starts at the green dot facing right (+x). The dotted box is the clear floor space to leave.`),
    table(["Item", "Value"], [
      ["File", e.file],
      ["Commands / duration", `${e.rows} commands, ${e.duration_s.toFixed(1)} s`],
      ["Predicted end", `x = ${m2(e.predicted_end.x)} m, y = ${m2(e.predicted_end.y)} m, θ = ${m2(e.predicted_end.theta)} rad (${e.end_distance_from_start_m.toFixed(2)} m from the start)`],
      ["Farthest point from the start", `${e.max_distance_from_start_m.toFixed(2)} m`],
      ["Clear floor space", `${m2(b.max_x)} m ahead, ${m2(-b.min_x)} m behind, ${m2(b.max_y)} m to the left, ${m2(-b.min_y)} m to the right of the start (includes a 0.3 m margin)`],
      ["Throttle / steering used", `τ ${e.throttle_range[0].toFixed(2)}–${e.throttle_range[1].toFixed(2)}, σ ${e.steering_range[0].toFixed(2)} to ${e.steering_range[1].toFixed(2)}`],
      ["Simulated accuracy", `mean waypoint error ${(e.mean_waypoint_error_m * 100).toFixed(1)} cm, end error ${(e.end_error_m * 100).toFixed(1)} cm`],
    ], [2600, 6426]),
    spacer(),
  );
});

// 4. On the day
children.push(
  new Paragraph({ children: [new PageBreak()] }),
  H1("4. On the day"),
  N("Choose a start position where the task's clear floor space (Section 3) fits on the track, and point the car along the plot's +x direction."),
  N("Make sure the car is stationary, then run the file."),
  N("Export and save the telemetry log for the run. Name it after the task and attempt, e.g. `task3_run1.csv`."),
  N("Note anything unusual: wheel slip, the car leaving the track, a crash, or a delay before the car moved."),
  N("If time allows, repeat each task 2–3 times. The spread between repeats shows how much of the difference from the simulation is noise."),
  spacer(),
  callout("If something goes wrong", [
    "**The path is mirrored** (turns right where the plot turns left): the steering sign is reversed. Ask Mr Mulder, or tell the Phase 5 team to flip σ in the files.",
    "**The car would leave the track:** stop the run. Choose a start with more room, or ask the Phase 5 team to regenerate the task for your start position.",
    "**The car barely moves or is much faster than expected:** the throttle scaling differs from the training data. Record a log anyway; it is useful evidence for the report.",
  ]),
);

// 5. After the slot
children.push(
  H1("5. After the slot: compare real and simulated paths"),
  P("From the `Assignment5_Phase5` folder (needs Python with matplotlib), run one command per task:"),
  CODE("python scripts/compare_real.py phase6/task3_solution.csv task3_run1.csv -o task3_run1.png"),
  spacer(),
  P("The script finds the first command in the log (the first row with throttle above zero; override with `--start-row N`), takes the logged pose just before it as the real start, and expresses the real path relative to it. Both paths then start at the same point, like the lecturer's \"anchored to start\" plot. It prints the mean, maximum and end position error, and saves a plot of predicted vs actual path."),
  P("It also checks that the logged commands match the file. If it reports a large difference, the start row is probably wrong, or the throttle and steering columns were swapped."),
  P("**For the final report** the brief asks Phase 6 for plots of the real paths for each task. The comparison plots provide these, and the error figures quantify the gap between simulation and reality."),
);

const doc = new Document({
  creator: "WRCV402 group", title: "WRCV402 Assignment 5 — Phase 6 handoff",
  styles: {
    default: { document: { run: { font: FONT, size: 22 } } },
    paragraphStyles: [
      { id: "Heading1", name: "Heading 1", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 32, bold: true, font: FONT, color: ACCENT }, paragraph: { spacing: { before: 320, after: 140 }, outlineLevel: 0, keepNext: true, keepLines: true } },
      { id: "Heading2", name: "Heading 2", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 26, bold: true, font: FONT, color: ACCENT }, paragraph: { spacing: { before: 200, after: 100 }, outlineLevel: 1, keepNext: true, keepLines: true } },
    ],
  },
  numbering: { config: [
    { reference: "bullets", levels: [{ level: 0, format: LevelFormat.BULLET, text: "•", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 540, hanging: 270 } } } }] },
    { reference: "numbers", levels: [{ level: 0, format: LevelFormat.DECIMAL, text: "%1.", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 540, hanging: 300 } } } }] },
    { reference: "checks", levels: [{ level: 0, format: LevelFormat.BULLET, text: "☐", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 540, hanging: 320 } }, run: { font: "Segoe UI Symbol" } } }] },
  ] },
  sections: [{
    properties: { page: { size: { width: 11906, height: 16838 }, margin: { top: 1440, bottom: 1440, left: 1440, right: 1440 } } },
    footers: { default: new Footer({ children: [new Paragraph({ alignment: AlignmentType.CENTER, children: [
      new TextRun({ text: "WRCV402 Assignment 5 · Phase 6 handoff · page ", size: 18, color: "7F7F7F" }),
      new TextRun({ children: [PageNumber.CURRENT], size: 18, color: "7F7F7F" })] })] }) },
    children,
  }],
});
Packer.toBuffer(doc).then((buf) => { fs.writeFileSync(OUT, buf); console.log("wrote", OUT); });
