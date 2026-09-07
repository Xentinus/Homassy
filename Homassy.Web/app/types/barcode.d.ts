/**
 * The Barcode Detection API, which lib.dom does not ship types for.
 *
 * On this project it is always reached through `window.BarcodeDetector` — Chrome
 * and Android implement it natively, and vue-qrcode-reader polyfills it
 * elsewhere — so it is declared as an optional property of `Window` rather than
 * as a bare global. That keeps the feature check (`if (window.BarcodeDetector)`)
 * type-safe instead of an `any` cast, and avoids the `declare var` a global
 * would need.
 */

interface DetectedBarcode {
  boundingBox: DOMRectReadOnly
  cornerPoints: { x: number, y: number }[]
  format: string
  rawValue: string
}

interface BarcodeDetector {
  detect(image: ImageBitmapSource): Promise<DetectedBarcode[]>
}

interface BarcodeDetectorConstructor {
  new (options?: { formats?: string[] }): BarcodeDetector
  getSupportedFormats(): Promise<string[]>
}

interface Window {
  BarcodeDetector?: BarcodeDetectorConstructor
}
