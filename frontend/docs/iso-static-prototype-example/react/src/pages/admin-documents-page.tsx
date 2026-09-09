import type { IsoDocument, SelectOption } from "../features/documents/types";
import { HomePage } from "./home-page";
import { AppSidebar } from "../components/layout/app-sidebar";

export interface AdminDocumentsPageProps {
  documents: IsoDocument[];
  categories: SelectOption[];
  companies: SelectOption[];
  onDownload: (id: string) => Promise<void>;
}

export function AdminDocumentsPage(props: AdminDocumentsPageProps) {
  return (
    <div className="grid min-h-[calc(100vh-48px)] grid-cols-[224px_minmax(0,1fr)] max-xl:grid-cols-[56px_minmax(0,1fr)] max-md:block">
      <AppSidebar activePath="/admin/documents" />

      <div className="min-w-0 px-4">
        <HomePage {...props} />
      </div>
    </div>
  );
}
