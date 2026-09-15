import { readFileSync, readdirSync, statSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { join, relative } from 'node:path'
import { describe, it, expect } from 'vitest'

/**
 * Guards the stacking ladder in `app/assets/css/main.css`.
 *
 * The app's floating chrome — header, nav, chat, shopping mode, the undo toast, the lightbox, the
 * tour — has to be ordered against itself, and before the ladder existed each of those carried its
 * own hand-written number. That is how the undo toast ended up under the nav (and therefore under
 * the '+' button that overhangs it), and how the tour and the lightbox ended up tied on z-100.
 *
 * Two rules, both of which a future change can break silently in review:
 *
 *   1. no two rungs share a number, and
 *   2. no `fixed` element names a z-index of its own instead of taking one from the ladder.
 *
 * Rule 2 is deliberately scoped to `position: fixed`. A `z-` on an `absolute` element is local to
 * whatever stacking context encloses it — the nav's sliding indicator, a badge on a nav icon, the
 * FAB inside the nav bar — and ranking those globally would say nothing true.
 */

const appDir = fileURLToPath(new URL('../../app', import.meta.url))
const cssPath = join(appDir, 'assets/css/main.css')

/** Every `--z-*: <n>;` declaration in main.css, in the order it is written. */
const readLadder = (): Array<[string, number]> => {
  const css = readFileSync(cssPath, 'utf8')
  return [...css.matchAll(/^\s*(--z-[a-z-]+):\s*(\d+);/gm)]
    .map(([, name, value]) => [name!, Number(value!)] as [string, number])
}

const vueFiles = (dir: string): string[] =>
  readdirSync(dir).flatMap((entry) => {
    const path = join(dir, entry)
    if (statSync(path).isDirectory()) return vueFiles(path)
    return path.endsWith('.vue') ? [path] : []
  })

/**
 * The values of every `class="…"` / `class='…'` attribute in a template.
 *
 * Attribute values only, never the whole file: "fixed" and "z-index" both turn up in the prose of
 * the comments these components are full of, and a scan over raw text would report those.
 */
const classAttributeValues = (source: string): string[] =>
  [...source.matchAll(/\sclass="([^"]*)"|\sclass='([^']*)'/g)]
    .map(match => match[1] ?? match[2] ?? '')

const Z_UTILITY = /(?:^|\s)(-?z-(?:\[[^\]]*\]|\d+|auto))(?=\s|$)/g

describe('the stacking ladder', () => {
  it('declares every rung exactly once, on its own number', () => {
    const ladder = readLadder()
    expect(ladder.length).toBeGreaterThan(0)

    const byValue = new Map<number, string[]>()
    for (const [name, value] of ladder) {
      byValue.set(value, [...(byValue.get(value) ?? []), name])
    }

    const collisions = [...byValue.entries()]
      .filter(([, names]) => names.length > 1)
      .map(([value, names]) => `${value}: ${names.join(', ')}`)

    expect(collisions).toEqual([])
  })

  it('is written in ascending order, so reading it top to bottom is reading the paint order', () => {
    const values = readLadder().map(([, value]) => value)
    expect(values).toEqual([...values].sort((a, b) => a - b))
  })

  it('is the only source of a z-index on a fixed element', () => {
    const offenders: string[] = []

    for (const file of vueFiles(appDir)) {
      const source = readFileSync(file, 'utf8')

      for (const value of classAttributeValues(source)) {
        if (!/(?:^|\s)fixed(?=\s|$)/.test(value)) continue

        for (const [, utility] of value.matchAll(Z_UTILITY)) {
          offenders.push(`${relative(appDir, file).replace(/\\/g, '/')}: ${utility}`)
        }
      }
    }

    // Use a ladder token instead: `z-(--z-nav)`, `z-(--z-chat-panel)`, … See main.css.
    expect(offenders).toEqual([])
  })
})
