interface DetectedBarcode {
  boundingBox: DOMRectReadOnly
  cornerPoints: { x: number; y: number }[]
  format: string
  rawValue: string
}

interface BarcodeDetector {
  detect(image: ImageBitmapSource): Promise<DetectedBarcode[]>
}

declare var BarcodeDetector: {
  prototype: BarcodeDetector
  new (options?: { formats?: string[] }): BarcodeDetector
  getSupportedFormats(): Promise<string[]>
}
