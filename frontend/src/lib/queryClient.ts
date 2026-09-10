import { QueryClient } from "@tanstack/react-query";

import { ApiError } from "@/api/apiError";

const NON_RETRYABLE_STATUS_CODES = new Set([401, 403, 404]);

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: (failureCount, error) => {
        if (
          error instanceof ApiError &&
          NON_RETRYABLE_STATUS_CODES.has(error.status)
        ) {
          return false;
        }

        return failureCount < 1;
      },
    },
    mutations: {
      retry: false,
    },
  },
});
