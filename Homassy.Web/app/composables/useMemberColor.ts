/**
 * Reactive shell over `utils/memberColors.ts`.
 *
 * All the actual colour logic — the curated palette, the deterministic hash, override validation —
 * lives in that plain module so it stays unit-testable without mounting a component (see
 * `tests/unit/memberColors.spec.ts`). This composable's only job is resolving *which* half of a
 * `MemberColor` applies right now (light or dark) and shaping that into the style object each call
 * site binds with `:style`, so no consumer branches on theme itself.
 *
 * SSR note: `colorMode.value` only reflects the real theme once the color-mode plugin's blocking
 * head script has run, i.e. client-side (see `plugins/theme-color.client.ts`, which is `.client`
 * only for the same reason). Every call site this composable has today renders from data that is
 * itself absent until a client-side fetch or an `onMounted` gate resolves (the calendar's activity
 * list, the settings picker's `v-if="!loading"` block), so none of them are actually present in the
 * server-rendered HTML for this to mismatch against. A future call site that renders eagerly during
 * SSR should wrap the themed bit in `<ClientOnly>`, the way the theme segmented control on the
 * profile page already does.
 */
import { computed } from 'vue'
import type { CSSProperties } from 'vue'
import {
  MEMBER_COLORS,
  resolveMemberColor,
  type MemberColor,
  type MemberColorKey
} from '~/utils/memberColors'

/** `--member-color` plus whatever the caller layers on top — see `ringStyle` / `accentStyle`. */
type MemberAccentStyle = CSSProperties & { '--member-color': string }

export const useMemberColor = () => {
  const colorMode = useColorMode()

  // Anything but exactly 'dark' reads as light — the same default the app already paints before
  // the theme is known (`:root { color-scheme: light }` in main.css).
  const isDark = (): boolean => colorMode.value === 'dark'

  /** The member's effective `MemberColor` entry — see `resolveMemberColor` for the fallback rule. */
  const colorFor = (publicId: string, override?: string | null): MemberColor =>
    resolveMemberColor(publicId, override)

  const accentHex = (publicId: string, override?: string | null): string => {
    const color = colorFor(publicId, override)
    return isDark() ? color.dark : color.light
  }

  /** Inset ring for an avatar — the one ring treatment every avatar-with-an-identity uses. */
  const ringStyle = (publicId: string, override?: string | null): MemberAccentStyle => ({
    '--member-color': accentHex(publicId, override),
    boxShadow: 'inset 0 0 0 2px var(--member-color)'
  })

  /**
   * Just the custom property — for a card's left border or a dot's background, where the caller
   * already owns the border-width/shape and only has to plug the colour into its own CSS property
   * (`borderLeftColor: 'var(--member-color)'` / `background: 'var(--member-color)'`).
   */
  const accentStyle = (publicId: string, override?: string | null): MemberAccentStyle => ({
    '--member-color': accentHex(publicId, override)
  })

  /** The initials-placeholder gradient, stops already resolved to the active theme. */
  const gradientStyle = (publicId: string, override?: string | null): CSSProperties => {
    const [from, to] = colorFor(publicId, override).gradient
    return { backgroundImage: `linear-gradient(135deg, ${from}, ${to})` }
  }

  /**
   * The settings picker's eight swatches plus "Automatic". `auto` has no single colour of its own
   * — it means "whoever's id this ends up hashing for" — so it gets a neutral grey outside the
   * palette rather than standing in for any one member's pick. Reactive (a `computed`, not a plain
   * array) so the swatches themselves follow a live theme toggle while the settings page is open.
   */
  const paletteOptions = computed((): Array<{ value: MemberColorKey | 'auto'; swatch: string }> => [
    { value: 'auto', swatch: '#9ca3af' },
    // MEMBER_COLORS is the fixed eight-entry palette, never a 'custom' pick — the cast narrows
    // back from MemberColor.key's wider type, which exists only for a resolved custom colour.
    ...MEMBER_COLORS.map(c => ({ value: c.key as MemberColorKey, swatch: isDark() ? c.dark : c.light }))
  ])

  return { colorFor, ringStyle, accentStyle, gradientStyle, paletteOptions }
}
