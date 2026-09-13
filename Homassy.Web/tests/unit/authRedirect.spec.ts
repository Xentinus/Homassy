import { describe, expect, it } from 'vitest'
import { LOGIN_PATH, loginRedirectFor } from '~/utils/authRedirect'
import { safeReturnTo } from '~/utils/shareText'

describe('loginRedirectFor', () => {
  it('carries the route the user was on', () => {
    expect(loginRedirectFor('/products')).toEqual({
      path: LOGIN_PATH,
      query: { return_to: '/products' }
    })
  })

  it('keeps the query string, which is where a deep link puts its intent', () => {
    expect(loginRedirectFor('/shopping-lists?action=add-custom')).toEqual({
      path: LOGIN_PATH,
      query: { return_to: '/shopping-lists?action=add-custom' }
    })
  })

  it('refuses to bounce from an auth route, which would be a loop', () => {
    // The 401 path can fire while the login page itself is calling the API.
    expect(loginRedirectFor('/auth/login')).toBeNull()
    expect(loginRedirectFor('/auth/login?return_to=/products')).toBeNull()
    expect(loginRedirectFor('/auth/register')).toBeNull()
  })

  it('goes to a bare login page when there is no usable destination', () => {
    expect(loginRedirectFor(undefined)).toEqual({ path: LOGIN_PATH })
    expect(loginRedirectFor('')).toEqual({ path: LOGIN_PATH })
    // Protocol-relative: never carried, so nobody can push the destination off-site.
    expect(loginRedirectFor('//evil.example/products')).toEqual({ path: LOGIN_PATH })
    expect(loginRedirectFor('https://evil.example/products')).toEqual({ path: LOGIN_PATH })
  })

  it('produces a destination the login page will actually honour', () => {
    // The two halves have to agree: a value safeReturnTo rejects would land on the
    // calendar anyway, which is the outcome this whole path exists to avoid.
    for (const path of ['/products', '/shopping-lists?action=add-custom', '/calendar?day=2026-09-13']) {
      const redirect = loginRedirectFor(path)

      expect(redirect?.query?.return_to).toBe(path)
      expect(safeReturnTo(redirect?.query?.return_to)).toBe(path)
    }
  })
})
