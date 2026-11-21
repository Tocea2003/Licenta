# Documentul proiectului (PDF)

Generează `docs/Proiect_TURSIB_Digital_Twin.pdf`: propunerea de master extinsă cu arhitectura, deciziile și rezultatele.
Textul e în engleză, ca propunerea.

```bash
cd tools/report
npm ci            # mermaid + fonturile (Archivo, Public Sans)
npm run build     # python3 build_report.py && node render-pdf.mjs
```

- `build_report.py` citește `docs/results/*.json`, `docs/results/fault/*.json` și `docs/requirements.md` și scrie
  `build/report.html`. Toate cifrele din text și din grafice vin din aceste fișiere.
- `charts.py` desenează graficele SVG (fără dependențe).
- `render-pdf.mjs` deschide pagina în Chromium (Playwright), așteaptă diagramele Mermaid și fonturile, și scrie PDF-ul.
  Playwright se ia din instalarea existentă (`require('playwright')`); dacă lipsește: `npm i -g playwright && npx playwright install chromium`.
- `assets/jaeger-trace.png` e o captură din Jaeger, făcută în timpul testelor de încărcare.

După ce rulezi din nou un experiment, recitește interpretările din `build_report.py` (secțiunile „Findings” și discuțiile):
cifrele se actualizează singure, concluziile nu. O cerință nouă în `requirements.md` are nevoie de un titlu în engleză în
`REQ_TITLES`, altfel generarea se oprește. Numele autorului se completează în `AUTHOR`.
