export type DocumentStatus = "active" | "review" | "expiring" | "obsolete";

export interface IsoDocument {
  id: string;
  code: string;
  title: string;
  pages: number;
  revision: number;
  status: DocumentStatus;
  categoryId: string;
  categoryName: string;
  departmentId: string;
  departmentName: string;
  companyId: string;
  companyName: string;
  issuedDate: string | null;
  effectiveDate: string | null;
  note: string | null;
  canDownload: boolean;
}

export interface DocumentQuery {
  keyword: string;
  categoryId: string | null;
  companyId: string | null;
  status: DocumentStatus | null;
  page: number;
  pageSize: 25 | 50 | 100;
}

export interface SelectOption {
  value: string;
  label: string;
}

export interface Paged<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}
