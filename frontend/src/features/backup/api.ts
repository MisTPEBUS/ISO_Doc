import { downloadFile, type FileDownloadResult } from '@/api/fileDownload'

export function downloadBackup(companyId: string): Promise<FileDownloadResult> {
  return downloadFile(`/companies/${companyId}/backup`, 'company_backup.zip')
}
