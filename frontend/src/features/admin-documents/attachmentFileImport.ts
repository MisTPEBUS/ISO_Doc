import { ALLOWED_ATTACHMENT_EXTENSIONS } from './attachmentSchemas'

export interface DraftAttachmentFile {
  id: string
  file: File
  attachmentNo: string
  name: string
}

function createId(): string {
  return typeof crypto.randomUUID === 'function'
    ? crypto.randomUUID()
    : `attachment-file-${Date.now()}-${Math.random().toString(16).slice(2)}`
}

export function hasAllowedAttachmentExtension(fileName: string): boolean {
  const lowered = fileName.toLowerCase()
  return ALLOWED_ATTACHMENT_EXTENSIONS.some((extension) => lowered.endsWith(extension))
}

export function fileIdentity(file: File): string {
  return `${file.name}|${file.size}|${file.lastModified}`
}

/** 檔名去掉副檔名後，正規化成合法的表單及附件編號（英數與連字號，開頭結尾須為英數）。 */
export function suggestAttachmentNo(baseName: string): string {
  const upper = baseName.toUpperCase().replace(/[^A-Z0-9-]+/g, '-').replace(/-+/g, '-')
  const trimmed = upper.replace(/^-+/, '').replace(/-+$/, '')
  return trimmed.slice(0, 50)
}

function baseFileName(fileName: string): string {
  const dotIndex = fileName.lastIndexOf('.')
  return dotIndex > 0 ? fileName.slice(0, dotIndex) : fileName
}

export function draftAttachmentFromFile(file: File): DraftAttachmentFile {
  const base = baseFileName(file.name).trim()
  return {
    id: createId(),
    file,
    attachmentNo: suggestAttachmentNo(base),
    name: base.slice(0, 255) || file.name.slice(0, 255),
  }
}
