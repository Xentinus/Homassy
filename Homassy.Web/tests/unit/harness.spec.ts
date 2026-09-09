import { describe, expect, it } from 'vitest'

describe('vitest harness', () => {
  it('resolves the ~ alias to app/', async () => {
    const mod = await import('~/types/activity')
    expect(mod.ActivityType.ProductCreate).toBe(1)
  })
})
