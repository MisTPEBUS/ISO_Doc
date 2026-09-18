import { useMutation } from '@tanstack/react-query'

import * as aiImportApi from './api'

export function useAnalyzeImport() {
  return useMutation({
    mutationFn: aiImportApi.analyze,
  })
}

export function useCommitImport() {
  return useMutation({
    mutationFn: aiImportApi.commit,
  })
}
