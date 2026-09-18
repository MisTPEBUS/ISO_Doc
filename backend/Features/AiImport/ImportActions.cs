namespace IsoDocument.Api.Features.AiImport;

public static class ImportActions
{
    public const string NewDocument = "NEW_DOCUMENT";
    public const string UploadDraftFile = "UPLOAD_DRAFT_FILE";
    public const string NewVersion = "NEW_VERSION";
    public const string SkipUnchanged = "SKIP_UNCHANGED";
    public const string NewAttachment = "NEW_ATTACHMENT";
}

public static class ImportSkipReasons
{
    public const string Unchanged = "UNCHANGED";
    public const string ParentDocumentFailed = "PARENT_DOCUMENT_FAILED";
}
