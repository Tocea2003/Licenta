// Renders build/report.html to docs/Proiect_TURSIB_Digital_Twin.pdf with Chromium (Playwright).
import { createRequire } from 'node:module'
import { fileURLToPath, pathToFileURL } from 'node:url'
import path from 'node:path'

const here = path.dirname(fileURLToPath(import.meta.url))
// Playwright is resolved like require() (also from a global install), so the report does not pin its own browser.
const { chromium } = createRequire(import.meta.url)('playwright')
const input = path.join(here, 'build', 'report.html')
const output = process.argv[2] ?? path.join(here, '..', '..', 'docs', 'Proiect_TURSIB_Digital_Twin.pdf')

const browser = await chromium.launch()
const page = await browser.newPage()
page.on('pageerror', (e) => console.error('page error:', e.message))
await page.goto(pathToFileURL(input).href, { waitUntil: 'networkidle' })
await page.waitForFunction(() => window.mermaidDone !== undefined)
await page.evaluate(() => window.mermaidDone)
await page.evaluate(() => document.fonts.ready)
const errors = await page.$$eval('.mermaid', (els) => els.filter((e) => !e.querySelector('svg')).length)
if (errors) throw new Error(`${errors} Mermaid diagram(s) did not render`)

await page.pdf({
  path: output,
  format: 'A4',
  printBackground: true,
  displayHeaderFooter: true,
  headerTemplate: '<div></div>',
  footerTemplate: `<div style="width:100%;font-family:sans-serif;font-size:7.5pt;color:#6b7280;padding:0 18mm;display:flex;justify-content:space-between">
    <span>TURSIB Tracker · event-driven platform and digital twin</span><span class="pageNumber"></span></div>`,
  margin: { top: '18mm', bottom: '18mm', left: '18mm', right: '18mm' },
})
await browser.close()
console.log(`PDF: ${path.relative(process.cwd(), output)}`)
