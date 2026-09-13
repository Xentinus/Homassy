import { QrcodeStream, setZXingModuleOverrides } from 'vue-qrcode-reader'
import { locateZXingModule } from '~/utils/zxingWasm'

export default defineNuxtPlugin((nuxtApp) => {
  // Where the WebAssembly module is fetched from, set before anything can fetch it (#164).
  //
  // `setZXingModuleOverrides` is re-exported by `vue-qrcode-reader` from the same
  // `barcode-detector` copy it decodes with, so the override lands on the instance that
  // will actually be used -- importing it from `barcode-detector/pure` directly would set
  // it on a second copy and change nothing. It has to run before the first scan rather
  // than before the component is registered, but doing both here makes the ordering
  // impossible to get wrong.
  const base = useRuntimeConfig().app.baseURL
  setZXingModuleOverrides({
    locateFile: (path: string, prefix: string) => locateZXingModule(base, path, prefix)
  })

  nuxtApp.vueApp.component('QrcodeStream', QrcodeStream)
})
