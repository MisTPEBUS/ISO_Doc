import { getFileResponse } from './httpClient'

export interface FileDownloadResult {
  fileName: string
}

function decodeFileName(value: string): string | undefined {
  try {
    return decodeURIComponent(value)
  } catch {
    return undefined
  }
}

function fileNameFromContentDisposition(header: string | undefined): string | undefined {
  if (header === undefined) return undefined

  const encodedMatch = /filename\*\s*=\s*(?:UTF-8'')?([^;]+)/i.exec(header)
  if (encodedMatch?.[1] !== undefined) {
    const decoded = decodeFileName(encodedMatch[1].trim().replace(/^"|"$/g, ''))
    if (decoded !== undefined && decoded.length > 0) return decoded
  }

  const plainMatch = /filename\s*=\s*(?:"([^"]+)"|([^;]+))/i.exec(header)
  return plainMatch?.[1]?.trim() ?? plainMatch?.[2]?.trim()
}

function safeFileName(fileName: string): string {
  return fileName.split(/[\\/]/).pop() || 'download'
}

export async function downloadFile(
  url: string,
  fallbackFileName: string,
): Promise<FileDownloadResult> {
  const response = await getFileResponse(url)
  const fileName = safeFileName(
    fileNameFromContentDisposition(response.contentDisposition) ?? fallbackFileName,
  )
  const objectUrl = URL.createObjectURL(response.blob)
  const anchor = document.createElement('a')

  anchor.href = objectUrl
  anchor.download = fileName
  anchor.style.display = 'none'
  document.body.append(anchor)
  anchor.click()
  anchor.remove()
  window.setTimeout(() => URL.revokeObjectURL(objectUrl), 1000)

  return { fileName }
}
