using Microsoft.AspNetCore.Authorization;

namespace IsoDocument.Api.Security.Authorization;

public sealed class DocumentAccessHandler
    : AuthorizationHandler<DocumentAccessRequirement, DocumentAccessResource>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DocumentAccessRequirement requirement,
        DocumentAccessResource resource)
    {
        _ = resource;

        // TODO(milestone Documents): Query document_dept_permissions for resource.DocumentId
        // and the authenticated user's dept_id claim. Succeed only when that department has
        // access to the document (with the role/scope behavior defined by SPEC §5 Permission).
        // Keeping DocumentAccessResource as the stable input means callers will not change.
        return Task.CompletedTask;
    }
}
