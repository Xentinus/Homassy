import { joinURL } from 'ufo'

/**
 * Where `zxing-wasm` should look for its WebAssembly module (#164).
 *
 * `barcode-detector` -- the decoder `vue-qrcode-reader` falls back to on a browser with no
 * native `BarcodeDetector`, which is Safari and desktop Firefox -- ships a `locateFile` that
 * returns a hardcoded `https://fastly.jsdelivr.net/npm/zxing-wasm@<version>/dist/<sub>/<file>`.
 * That fetches and compiles an unpinned third-party binary inside this origin, forces
 * `Homassy.Proxy/Caddyfile` to allow jsDelivr in `connect-src`, and makes scanning fail
 * outright when jsDelivr is unreachable -- a dependency nothing else in the app has.
 *
 * The binary is served from `/zxing/` instead, out of `node_modules/zxing-wasm/dist/reader`
 * via `nitro.publicAssets`. Only the `.wasm` is ours to place: anything else the module asks
 * for keeps the library's own prefix, which is what `prefix` carries.
 *
 * @param base `runtimeConfig.app.baseURL`, so a deployment under a sub-path still resolves.
 * @param path the file the module is asking for, e.g. `zxing_reader.wasm`.
 * @param prefix the library's own base, used for anything that is not the WebAssembly module.
 */
export const locateZXingModule = (base: string, path: string, prefix: string): string =>
  path.endsWith('.wasm') ? joinURL(base, 'zxing', path) : prefix + path
