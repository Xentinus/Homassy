/**
 * Renders the manifest shortcut icons (#118) into `public/shortcuts/`.
 *
 * The generated PNGs are committed — this script exists so they can be regenerated
 * and so there is a written record of where they came from, not as a build step.
 *
 * Why generated rather than drawn by hand:
 *
 * - The glyphs are the *same* Lucide paths the in-app UI uses for these four
 *   destinations (`i-lucide-shopping-cart`, `i-lucide-plus`, `i-lucide-scan-barcode`,
 *   `i-lucide-calendar`), read straight out of `@iconify-json/lucide`. A hand-traced
 *   copy would drift from the icon set on its next upgrade and nobody would notice.
 * - Android's launcher shortcut menu asks for a raster icon at 96×96 (48dp @2x) and
 *   does not render SVG, so a PNG is the only portable answer. 192×192 is emitted
 *   alongside it for higher-density launchers.
 *
 * Run: node scripts/generate-shortcut-icons.mjs
 */
import { mkdir, writeFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { createRequire } from 'node:module'
import process from 'node:process'
import sharp from 'sharp'

const require = createRequire(import.meta.url)
const lucide = require('@iconify-json/lucide/icons.json')

const root = join(dirname(fileURLToPath(import.meta.url)), '..')
const outDir = join(root, 'public', 'shortcuts')

/** Brand tan, the same value `pwa.manifest.theme_color` carries in nuxt.config.ts. */
const BACKGROUND = '#c9b8a0'
/** The glyph colour. Near-black rather than pure white: the tan ground is light. */
const FOREGROUND = '#2b2118'

/** The sizes emitted per shortcut. 96 is what Android asks for; 192 covers xxhdpi. */
const SIZES = [96, 192]

/** Shortcut key → the Lucide icon name whose path is drawn on it. */
const ICONS = {
  'shopping-list': 'shopping-cart',
  'add-item': 'plus',
  'scan-barcode': 'scan-barcode',
  calendar: 'calendar'
}

/**
 * Wraps a Lucide icon body in a rounded-square badge on a 24-unit grid scaled up to
 * `size`. The glyph is inset to ~62% so it keeps a margin inside the badge — a
 * full-bleed glyph reads as clipped once the launcher rounds the corners itself.
 */
function badgeSvg(iconName, size) {
  const icon = lucide.icons[iconName]
  if (!icon) throw new Error(`Unknown Lucide icon: ${iconName}`)

  const inset = size * 0.19
  const glyph = size - inset * 2
  const radius = size * 0.22

  return `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 ${size} ${size}">`
    + `<rect width="${size}" height="${size}" rx="${radius}" fill="${BACKGROUND}"/>`
    + `<svg x="${inset}" y="${inset}" width="${glyph}" height="${glyph}" viewBox="0 0 ${lucide.width ?? 24} ${lucide.height ?? 24}" color="${FOREGROUND}">`
    + icon.body
    + '</svg>'
    + '</svg>'
}

await mkdir(outDir, { recursive: true })

for (const [key, iconName] of Object.entries(ICONS)) {
  for (const size of SIZES) {
    const svg = badgeSvg(iconName, size)
    const png = await sharp(Buffer.from(svg)).png({ compressionLevel: 9 }).toBuffer()
    const file = join(outDir, `${key}-${size}x${size}.png`)
    await writeFile(file, png)
    console.log(`wrote public/shortcuts/${key}-${size}x${size}.png (${png.length} bytes)`)
  }
}

console.log(`\n${Object.keys(ICONS).length * SIZES.length} shortcut icons generated.`)
process.exitCode = 0
