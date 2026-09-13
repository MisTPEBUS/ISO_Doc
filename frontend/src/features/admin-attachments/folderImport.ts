export const FILE_ROLE = {
  Main: 'MAIN',
  Attachment: 'ATTACHMENT',
  MainCandidate: 'MAIN_CANDIDATE',
  Unresolved: 'UNRESOLVED',
} as const

export type FileRole = (typeof FILE_ROLE)[keyof typeof FILE_ROLE]

export const PARSE_STATUS = {
  Ok: 'OK',
  Warning: 'WARNING',
} as const

export type ParseStatus = (typeof PARSE_STATUS)[keyof typeof PARSE_STATUS]

export const GROUP_STATUS = {
  All: 'ALL',
  Ok: 'OK',
  MissingMain: 'MISSING_MAIN',
  Warning: 'WARNING',
} as const

export type GroupStatus = Exclude<
  (typeof GROUP_STATUS)[keyof typeof GROUP_STATUS],
  typeof GROUP_STATUS.All
>
export type GroupStatusFilter = (typeof GROUP_STATUS)[keyof typeof GROUP_STATUS]

export interface ParsedAttachmentFile {
  id: string
  file: File
  relativePath: string
  sourceFolder: string
  documentCode: string
  role: FileRole
  attachmentCode: string
  attachmentSequence: string
  displayName: string
  extension: string
  parseStatus: ParseStatus
}

export interface AttachmentFileGroup {
  key: string
  documentCode: string
  sourceFolder: string
  files: ParsedAttachmentFile[]
  status: GroupStatus
}

export interface ExportedAttachmentFile {
  originalFileName: string
  relativePath: string
  size: number
  role: FileRole
  documentCode: string
  attachmentCode: string
  displayName: string
  extension: string
  parseStatus: ParseStatus
}

function createId(): string {
  return typeof crypto.randomUUID === 'function'
    ? crypto.randomUUID()
    : `attachment-file-${Date.now()}-${Math.random().toString(16).slice(2)}`
}

function withoutExtension(fileName: string): string {
  const separatorIndex = fileName.lastIndexOf('.')
  return separatorIndex < 0 ? fileName : fileName.slice(0, separatorIndex)
}

function extensionOf(fileName: string): string {
  const separatorIndex = fileName.lastIndexOf('.')
  return separatorIndex < 0 ? '' : fileName.slice(separatorIndex + 1).toLowerCase()
}

function cleanDisplayName(value: string): string {
  return value.replace(/^[\s_\-－—.]+/, '').trim()
}

export function findDocumentCode(value: string): string {
  const match = value.match(/\b([A-Za-z]+-[A-Za-z]+-\d+)\b/i)
  return match?.[1]?.toUpperCase() ?? ''
}

export function parseAttachmentFile(file: File): ParsedAttachmentFile {
  const relativePath = file.webkitRelativePath || file.name
  const sourceFolder = relativePath.includes('/') ? relativePath.split('/')[0] ?? '' : ''
  const baseName = withoutExtension(file.name)
  const folderDocumentCode = findDocumentCode(sourceFolder)
  const fileDocumentCode = findDocumentCode(baseName)
  const attachmentMatch = baseName.match(
    /^([A-Za-z]+-[A-Za-z]+-\d+)-(\d+[A-Za-z]?)(?=[^A-Za-z0-9]|[\u4e00-\u9fff]|$)/i,
  )
  const documentCode = (folderDocumentCode || fileDocumentCode).toUpperCase()

  let role: FileRole = FILE_ROLE.Unresolved
  let attachmentCode = ''
  let attachmentSequence = ''
  let displayName = baseName
  let parseStatus: ParseStatus = PARSE_STATUS.Warning

  if (attachmentMatch?.[1] && attachmentMatch[2]) {
    attachmentSequence = attachmentMatch[2].toUpperCase()
    attachmentCode = `${attachmentMatch[1].toUpperCase()}-${attachmentSequence}`
    role = FILE_ROLE.Attachment
    parseStatus = PARSE_STATUS.Ok
    displayName = cleanDisplayName(baseName.slice(attachmentMatch[0].length))
  } else if (
    documentCode !== ''
    && fileDocumentCode !== ''
    && baseName.toUpperCase().startsWith(fileDocumentCode)
  ) {
    role = FILE_ROLE.MainCandidate
    displayName = cleanDisplayName(baseName.slice(fileDocumentCode.length)) || baseName
  } else if (folderDocumentCode !== '') {
    role = FILE_ROLE.Attachment
    displayName = baseName
  }

  return {
    id: createId(),
    file,
    relativePath,
    sourceFolder,
    documentCode,
    role,
    attachmentCode,
    attachmentSequence,
    displayName,
    extension: extensionOf(file.name),
    parseStatus,
  }
}

function groupingKey(file: ParsedAttachmentFile): string {
  if (file.documentCode !== '') return `DOC:${file.documentCode}`
  if (file.sourceFolder !== '') return `FOLDER:${file.sourceFolder}`
  return 'UNASSIGNED'
}

export function resolveMainCandidates(files: ParsedAttachmentFile[]): ParsedAttachmentFile[] {
  const candidatesByGroup = new Map<string, ParsedAttachmentFile[]>()
  for (const file of files) {
    if (file.role !== FILE_ROLE.MainCandidate) continue
    const key = groupingKey(file)
    const candidates = candidatesByGroup.get(key) ?? []
    candidates.push(file)
    candidatesByGroup.set(key, candidates)
  }

  const singleCandidateIds = new Set(
    [...candidatesByGroup.values()]
      .filter((candidates) => candidates.length === 1)
      .map((candidates) => candidates[0]?.id)
      .filter((id) => id !== undefined),
  )

  return files.map((file) => {
    if (file.role !== FILE_ROLE.MainCandidate) return file
    return singleCandidateIds.has(file.id)
      ? { ...file, role: FILE_ROLE.Main, parseStatus: PARSE_STATUS.Ok }
      : { ...file, parseStatus: PARSE_STATUS.Warning }
  })
}

export function recalculateFile(file: ParsedAttachmentFile): ParsedAttachmentFile {
  if (file.role === FILE_ROLE.Main) {
    return {
      ...file,
      attachmentCode: '',
      attachmentSequence: '',
      parseStatus: file.documentCode === '' ? PARSE_STATUS.Warning : PARSE_STATUS.Ok,
    }
  }

  if (file.role === FILE_ROLE.Attachment) {
    return {
      ...file,
      parseStatus:
        file.documentCode !== '' && file.attachmentCode !== ''
          ? PARSE_STATUS.Ok
          : PARSE_STATUS.Warning,
    }
  }

  return { ...file, parseStatus: PARSE_STATUS.Warning }
}

function roleRank(role: FileRole): number {
  const ranks: Record<FileRole, number> = {
    [FILE_ROLE.Main]: 0,
    [FILE_ROLE.MainCandidate]: 1,
    [FILE_ROLE.Attachment]: 2,
    [FILE_ROLE.Unresolved]: 3,
  }
  return ranks[role]
}

export function attachmentFileGroups(files: ParsedAttachmentFile[]): AttachmentFileGroup[] {
  const grouped = new Map<string, ParsedAttachmentFile[]>()
  for (const file of files) {
    const key = groupingKey(file)
    grouped.set(key, [...(grouped.get(key) ?? []), file])
  }

  return [...grouped.entries()]
    .map(([key, groupedFiles]) => {
      const sortedFiles = [...groupedFiles].sort(
        (left, right) =>
          roleRank(left.role) - roleRank(right.role)
          || left.file.name.localeCompare(right.file.name, 'zh-Hant'),
      )
      const mainCount = sortedFiles.filter((file) => file.role === FILE_ROLE.Main).length
      const hasWarning = sortedFiles.some(
        (file) =>
          file.parseStatus !== PARSE_STATUS.Ok
          || file.role === FILE_ROLE.MainCandidate
          || file.role === FILE_ROLE.Unresolved,
      )
      const status: GroupStatus = mainCount === 0
        ? GROUP_STATUS.MissingMain
        : mainCount > 1 || hasWarning
          ? GROUP_STATUS.Warning
          : GROUP_STATUS.Ok

      return {
        key,
        documentCode: sortedFiles.find((file) => file.documentCode !== '')?.documentCode ?? '',
        sourceFolder: sortedFiles.find((file) => file.sourceFolder !== '')?.sourceFolder ?? '',
        files: sortedFiles,
        status,
      }
    })
    .sort((left, right) =>
      (left.documentCode || left.sourceFolder || 'ZZZ').localeCompare(
        right.documentCode || right.sourceFolder || 'ZZZ',
        'zh-Hant',
      ),
    )
}

export function formatFileSize(bytes: number): string {
  if (bytes === 0) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB']
  const unitIndex = Math.min(
    Math.floor(Math.log(bytes) / Math.log(1024)),
    units.length - 1,
  )
  const value = bytes / 1024 ** unitIndex
  return `${value.toFixed(unitIndex === 0 ? 0 : 2)} ${units[unitIndex]}`
}

export function toExportedFiles(files: ParsedAttachmentFile[]): ExportedAttachmentFile[] {
  return files.map((file) => ({
    originalFileName: file.file.name,
    relativePath: file.relativePath,
    size: file.file.size,
    role: file.role,
    documentCode: file.documentCode,
    attachmentCode: file.attachmentCode,
    displayName: file.displayName,
    extension: file.extension,
    parseStatus: file.parseStatus,
  }))
}
