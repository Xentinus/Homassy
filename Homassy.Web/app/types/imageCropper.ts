export interface CropperAspectRatio {
  label: string
  value: number
}

export interface ImageCropResult {
  base64: string
  blob: Blob
  width: number
  height: number
}
