import { useMutation } from '@tanstack/react-query'

import * as backupApi from './api'

export function useDownloadBackup() {
  return useMutation({ mutationFn: backupApi.downloadBackup })
}
