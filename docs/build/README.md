# Rebuilding the documents

Run from the repository root after new results are in `results/`:

```bash
python scripts/make_phase6_package.py
python docs/build/report_figs.py results docs/build/figs
cd docs/build && npm install && cd ../..
node docs/build/build_report.js docs/build/figs docs/Phase5_Report.docx results phase6/manifest.json
node docs/build/build_handoff.js phase6 phase6/Phase6_Handoff.docx
```

The report text lives in `build_report.js`; numbers and figures come from `results/`.