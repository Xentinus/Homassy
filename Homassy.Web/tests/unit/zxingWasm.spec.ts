import { describe, expect, it } from 'vitest'
import { locateZXingModule } from '~/utils/zxingWasm'

/**
 * The point of this override is that no request leaves for jsDelivr, so the assertion that
 * matters is the negative one: whatever comes back for the `.wasm` is same-origin.
 */
describe('locateZXingModule', () => {
  it('serves the WebAssembly module from this origin', () => {
    expect(locateZXingModule('/', 'zxing_reader.wasm', 'https://fastly.jsdelivr.net/npm/zxing-wasm@1.1.3/dist/reader/'))
      .toBe('/zxing/zxing_reader.wasm')
  })

  it('keeps the app base URL when the deployment is under a sub-path', () => {
    expect(locateZXingModule('/app/', 'zxing_reader.wasm', 'https://fastly.jsdelivr.net/'))
      .toBe('/app/zxing/zxing_reader.wasm')
  })

  it('leaves anything that is not the WebAssembly module to the library', () => {
    expect(locateZXingModule('/', 'zxing_reader.js', 'https://fastly.jsdelivr.net/npm/zxing-wasm@1.1.3/dist/reader/'))
      .toBe('https://fastly.jsdelivr.net/npm/zxing-wasm@1.1.3/dist/reader/zxing_reader.js')
  })
})
