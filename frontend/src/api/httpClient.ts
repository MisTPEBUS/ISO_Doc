import axios from "axios";
import type { AxiosError, AxiosInstance, AxiosRequestConfig } from "axios";

import { env } from "@/config/env";
import type { ProblemDetails } from "@/types/problem";

import { ApiError } from "./apiError";

export { ApiError } from "./apiError";

export interface HttpClient extends Omit<
  AxiosInstance,
  "request" | "get" | "delete" | "head" | "options" | "post" | "put" | "patch"
> {
  request<T>(config: AxiosRequestConfig): Promise<T>;
  get<T>(url: string, config?: AxiosRequestConfig): Promise<T>;
  delete<T>(url: string, config?: AxiosRequestConfig): Promise<T>;
  head<T>(url: string, config?: AxiosRequestConfig): Promise<T>;
  options<T>(url: string, config?: AxiosRequestConfig): Promise<T>;
  post<T, D = unknown>(
    url: string,
    data?: D,
    config?: AxiosRequestConfig<D>,
  ): Promise<T>;
  put<T, D = unknown>(
    url: string,
    data?: D,
    config?: AxiosRequestConfig<D>,
  ): Promise<T>;
  patch<T, D = unknown>(
    url: string,
    data?: D,
    config?: AxiosRequestConfig<D>,
  ): Promise<T>;
}

export const AUTH_UNAUTHORIZED_EVENT = "auth:unauthorized";

const XSRF_COOKIE_NAME = "isodocs.xsrf";
const XSRF_HEADER_NAME = "X-XSRF-TOKEN";
const READ_ONLY_METHODS = new Set(["get", "head"]);

function readCookie(name: string): string | undefined {
  const prefix = `${encodeURIComponent(name)}=`;
  const cookie = document.cookie
    .split(";")
    .map((part) => part.trim())
    .find((part) => part.startsWith(prefix));

  if (cookie === undefined) {
    return undefined;
  }

  const value = cookie.slice(prefix.length);

  try {
    return decodeURIComponent(value);
  } catch {
    return value;
  }
}

function isLoginRequest(url?: string): boolean {
  return url !== undefined && /(?:^|\/)auth\/login\/?(?:[?#].*)?$/.test(url);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function readErrors(value: unknown): Record<string, string[]> | undefined {
  if (!isRecord(value)) {
    return undefined;
  }

  const entries = Object.entries(value);
  if (
    !entries.every(
      (entry): entry is [string, string[]] =>
        Array.isArray(entry[1]) &&
        entry[1].every((message) => typeof message === "string"),
    )
  ) {
    return undefined;
  }

  return Object.fromEntries(entries);
}

function toApiError(error: AxiosError, responseBody?: unknown): ApiError {
  const response = error.response;
  const body = responseBody ?? response?.data;
  const problem = isRecord(body) ? body : {};
  const status =
    typeof problem.status === "number"
      ? problem.status
      : (response?.status ?? 0);
  const title =
    typeof problem.title === "string"
      ? problem.title
      : response?.statusText || "Request failed";
  const detail =
    typeof problem.detail === "string" ? problem.detail : undefined;
  const type = typeof problem.type === "string" ? problem.type : undefined;
  const instance =
    typeof problem.instance === "string" ? problem.instance : undefined;
  const problemDetails: ProblemDetails = {
    status,
    title,
    detail,
    type,
    instance,
  };

  return new ApiError(problemDetails, readErrors(problem.errors));
}

function isProblemResponse(error: AxiosError): boolean {
  const contentType = error.response?.headers["content-type"];
  return (
    typeof contentType === "string" &&
    contentType.toLowerCase().includes("application/problem+json")
  );
}

const axiosInstance = axios.create({
  baseURL: env.apiBaseUrl,
  withCredentials: true,
});

axiosInstance.interceptors.request.use((config) => {
  const method = config.method?.toLowerCase() ?? "get";

  if (!READ_ONLY_METHODS.has(method)) {
    const token = readCookie(XSRF_COOKIE_NAME);
    if (token !== undefined) {
      config.headers.set(XSRF_HEADER_NAME, token);
    }
  }

  return config;
});

axiosInstance.interceptors.response.use(
  (response) => response.data,
  (error: unknown) => {
    if (!axios.isAxiosError(error)) {
      return Promise.reject(error);
    }

    if (error.response?.status === 401 && !isLoginRequest(error.config?.url)) {
      window.dispatchEvent(new CustomEvent(AUTH_UNAUTHORIZED_EVENT));
    }

    return Promise.reject(isProblemResponse(error) ? toApiError(error) : error);
  },
);

export const httpClient = axiosInstance as HttpClient;

export interface HttpFileResponse {
  blob: Blob;
  contentDisposition?: string;
}

export async function getFileResponse(url: string): Promise<HttpFileResponse> {
  try {
    const response = await axios.get<Blob>(url, {
      baseURL: env.apiBaseUrl,
      withCredentials: true,
      responseType: "blob",
    });

    const contentDisposition = response.headers["content-disposition"];
    return {
      blob: response.data,
      contentDisposition:
        typeof contentDisposition === "string" ? contentDisposition : undefined,
    };
  } catch (error: unknown) {
    if (!axios.isAxiosError(error)) {
      throw error;
    }

    if (error.response?.status === 401) {
      window.dispatchEvent(new CustomEvent(AUTH_UNAUTHORIZED_EVENT));
    }

    if (!isProblemResponse(error)) {
      throw error;
    }

    let responseBody: unknown = error.response?.data;
    if (responseBody instanceof Blob) {
      try {
        responseBody = JSON.parse(await responseBody.text());
      } catch {
        throw error;
      }
    }

    throw toApiError(error, responseBody);
  }
}

export default httpClient;
