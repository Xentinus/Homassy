import imageCompression from 'browser-image-compression'

/**
 * Extract pure base64 string (remove data:image prefix)
 */
export function extractBase64(dataUrl: string): string {
  return dataUrl.includes(',') ? dataUrl.split(',')[1] : dataUrl
}

/**
 * Convert base64 string to Blob
 */
export async function base64ToBlob(base64: string): Promise<Blob> {
  const response = await fetch(base64)
  return await response.blob()
}

/**
 * Convert Blob to base64 string
 */
export function blobToBase64(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onloadend = () => resolve(reader.result as string)
    reader.onerror = reject
    reader.readAsDataURL(blob)
  })
}

/**
 * Compress image to meet size requirements
 */
export async function compressImage(
  blob: Blob,
  options: { maxSizePx?: number; maxSizeMB?: number } = {}
): Promise<Blob> {
  const compressionOptions = {
    maxWidthOrHeight: options.maxSizePx || 500,
    maxSizeMB: options.maxSizeMB || 0.5,
    useWebWorker: true,
    fileType: 'image/jpeg' as const
  }

  return await imageCompression(blob as File, compressionOptions)
}
