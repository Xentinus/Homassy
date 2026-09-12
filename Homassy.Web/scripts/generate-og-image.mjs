/**
 * Generates `public/og-image.png` — the 1200×630 card a shared Homassy link renders as (#123).
 *
 * Committed as a PNG rather than produced at request time: Open Graph and Twitter crawlers
 * fetch it as a plain static file, no JavaScript runs for them, and neither format accepts SVG.
 * This script is how it is regenerated when the wordmark or the palette changes:
 *
 *   cd Homassy.Web && node scripts/generate-og-image.mjs
 *
 * The colours are the mocha palette from `app/assets/css/main.css`. The card is deliberately
 * one fixed light design: a crawler has no theme to match, and a preview that guesses wrong is
 * worse than one that always looks the same.
 */
import { writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import sharp from 'sharp'

const here = dirname(fileURLToPath(import.meta.url))
const output = resolve(here, '../public/og-image.png')

const WIDTH = 1200
const HEIGHT = 630

// Straight from the `@theme` block in main.css.
const MOCHA = {
  50: '#F5F0EB',
  100: '#EFE9E0',
  200: '#E0D5C7',
  400: '#C9B8A0',
  500: '#B8956A',
  700: '#8B7355',
  800: '#6F5A44',
  950: '#3F3027'
}

/** One product tile in the little inventory grid on the right. */
const tile = (x, y, accent, width) => `
  <g transform="translate(${x} ${y})">
    <rect width="${width}" height="104" rx="16" fill="#FFFFFF" stroke="${MOCHA[200]}" />
    <rect x="16" y="16" width="${width - 32}" height="34" rx="10" fill="${MOCHA[100]}" />
    <rect x="16" y="62" width="${width - 72}" height="10" rx="5" fill="${MOCHA[400]}" />
    <rect x="16" y="80" width="${Math.round((width - 32) * 0.45)}" height="8" rx="4" fill="${accent}" />
  </g>`

const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${WIDTH}" height="${HEIGHT}" viewBox="0 0 ${WIDTH} ${HEIGHT}">
  <defs>
    <linearGradient id="bg" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0%" stop-color="${MOCHA[50]}" />
      <stop offset="100%" stop-color="${MOCHA[200]}" />
    </linearGradient>
    <radialGradient id="glow" cx="0.78" cy="0.18" r="0.6">
      <stop offset="0%" stop-color="${MOCHA[400]}" stop-opacity="0.75" />
      <stop offset="100%" stop-color="${MOCHA[400]}" stop-opacity="0" />
    </radialGradient>
  </defs>

  <rect width="${WIDTH}" height="${HEIGHT}" fill="url(#bg)" />
  <rect width="${WIDTH}" height="${HEIGHT}" fill="url(#glow)" />

  <g font-family="Public Sans, Segoe UI, Arial, Helvetica, sans-serif">
    <!-- Wordmark + tagline -->
    <g transform="translate(80 196)">
      <rect x="0" y="-52" width="228" height="42" rx="21" fill="${MOCHA[500]}" fill-opacity="0.16" />
      <text x="20" y="-22" font-size="20" font-weight="600" fill="${MOCHA[800]}" letter-spacing="0.6">
        FREE &amp; OPEN SOURCE
      </text>

      <text x="0" y="72" font-size="88" font-weight="700" fill="${MOCHA[950]}" letter-spacing="-1.5">Homassy</text>
      <text x="0" y="134" font-size="34" font-weight="500" fill="${MOCHA[800]}">
        Your household inventory, shared with
      </text>
      <text x="0" y="180" font-size="34" font-weight="500" fill="${MOCHA[800]}">
        the people you live with.
      </text>
    </g>

    <!-- Three proof points, the same ones the hero shows -->
    <g transform="translate(80 500)" font-size="22" font-weight="600" fill="${MOCHA[700]}">
      <circle cx="9" cy="-7" r="9" fill="${MOCHA[500]}" />
      <text x="30" y="0">Expiry tracking</text>
      <circle cx="239" cy="-7" r="9" fill="${MOCHA[500]}" />
      <text x="260" y="0">Live shopping lists</text>
      <circle cx="519" cy="-7" r="9" fill="${MOCHA[500]}" />
      <text x="540" y="0">Barcode scanner</text>
    </g>
  </g>

  <!-- A hint of the product, cropped by the right edge the way a device would be -->
  <g transform="translate(806 96)">
    <rect width="330" height="470" rx="42" fill="#FFFFFF" stroke="${MOCHA[200]}" stroke-width="2" />
    <rect x="121" y="0" width="88" height="20" rx="10" fill="${MOCHA[100]}" />

    <rect x="28" y="52" width="150" height="18" rx="9" fill="${MOCHA[400]}" />
    ${tile(28, 92, '#DC2626', 130)}
    ${tile(172, 92, '#D97706', 130)}
    ${tile(28, 212, MOCHA[400], 130)}
    ${tile(172, 212, MOCHA[400], 130)}

    <rect x="28" y="340" width="274" height="56" rx="18" fill="${MOCHA[50]}" stroke="${MOCHA[200]}" />
    <circle cx="56" cy="368" r="12" fill="${MOCHA[500]}" />
    <rect x="80" y="360" width="150" height="16" rx="8" fill="${MOCHA[400]}" />

    <rect x="28" y="416" width="274" height="2" fill="${MOCHA[200]}" />
    <g fill="${MOCHA[400]}">
      <circle cx="66" cy="442" r="11" />
      <circle cx="142" cy="442" r="11" fill="${MOCHA[500]}" />
      <circle cx="218" cy="442" r="11" />
      <circle cx="288" cy="442" r="11" />
    </g>
  </g>
</svg>`

const png = await sharp(Buffer.from(svg)).png({ compressionLevel: 9 }).toBuffer()
writeFileSync(output, png)
console.log(`Wrote ${output} (${WIDTH}×${HEIGHT}, ${(png.length / 1024).toFixed(1)} KB)`)
