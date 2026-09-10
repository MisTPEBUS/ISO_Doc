using FluentValidation;
using FluentValidation.Results;
using IsoDocument.Api.Common;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.AuditLogs;
using IsoDocument.Api.Features.Permissions.Dtos;
using IsoDocument.Api.Security;

namespace IsoDocument.Api.Features.Permissions;

public sealed class DocumentPermissionService(
    IDocumentPermissionStore permissionStore,
    IAuditLogService auditLogService,
    ICurrentUser currentUser,
    IValidator<UpdateDocumentDeptPermissionsRequest> validator,
    TimeProvider timeProvider) : IDocumentPermissionService
{
    public async Task<Result<DocumentDeptPermissionsResponse>> GetAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var documentResult = await FindAccessibleDocumentAsync(documentId, cancellationToken);
        if (!documentResult.IsSuccess)
        {
            return documentResult.ToResponseFailure();
        }

        var permissions = await permissionStore.ListPermissionsAsync(
            documentId, cancellationToken);
        return Result<DocumentDeptPermissionsResponse>.Success(new(
            permissions.Select(permission => permission.DeptId).Order().ToArray()));
    }

    public async Task<Result<DocumentDeptPermissionsResponse>> UpdateAsync(
        Guid documentId,
        UpdateDocumentDeptPermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<DocumentDeptPermissionsResponse>.ValidationFailed(ToErrors(validation));
        }

        var documentResult = await FindAccessibleDocumentAsync(documentId, cancellationToken);
        if (!documentResult.IsSuccess)
        {
            return documentResult.ToResponseFailure();
        }

        var document = documentResult.Value!;
        var requestedDeptIds = request.DeptIds!.Distinct().Order().ToArray();
        var validDeptIds = await permissionStore.FindCompanyDeptIdsAsync(
            document.CompanyId, requestedDeptIds, cancellationToken);
        if (validDeptIds.Count != requestedDeptIds.Length)
        {
            return Result<DocumentDeptPermissionsResponse>.ValidationFailed(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["deptIds"] =
                    ["所有部門都必須存在，且屬於文件所屬的公司。"]
                });
        }

        var currentPermissions = await permissionStore.ListPermissionsAsync(
            documentId, cancellationToken);
        var currentDeptIds = currentPermissions
            .Select(permission => permission.DeptId)
            .ToHashSet();
        var requestedSet = requestedDeptIds.ToHashSet();
        var toRemove = currentPermissions
            .Where(permission => !requestedSet.Contains(permission.DeptId))
            .ToArray();
        var toAdd = requestedDeptIds
            .Where(deptId => !currentDeptIds.Contains(deptId))
            .Select(deptId => new DocumentDeptPermission
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                DeptId = deptId,
                GrantedBy = currentUser.UserId!.Value,
                CreatedAt = timeProvider.GetUtcNow()
            })
            .ToArray();

        if (toRemove.Length == 0 && toAdd.Length == 0)
        {
            return Result<DocumentDeptPermissionsResponse>.Success(new(requestedDeptIds));
        }

        await using var transaction = await permissionStore.BeginTransactionAsync(cancellationToken);
        permissionStore.RemoveRange(toRemove);
        permissionStore.AddRange(toAdd);
        await permissionStore.SaveChangesAsync(cancellationToken);
        await auditLogService.WriteDocumentDeptPermissionsChangedAsync(
            document.CompanyId,
            documentId,
            currentDeptIds.Order().ToArray(),
            requestedDeptIds,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<DocumentDeptPermissionsResponse>.Success(new(requestedDeptIds));
    }

    private async Task<Result<Document>> FindAccessibleDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var document = await permissionStore.FindDocumentAsync(documentId, cancellationToken);
        if (document is null)
        {
            return Result<Document>.NotFound("找不到指定的文件。");
        }

        if (!currentUser.CanAccessCompany(document.CompanyId))
        {
            return Result<Document>.Forbidden(
                "您沒有管理此文件部門權限的權限。");
        }

        if (currentUser.UserId is null)
        {
            return Result<Document>.Unauthorized("請先登入後再操作。");
        }

        return Result<Document>.Success(document);
    }

    private static Dictionary<string, string[]> ToErrors(ValidationResult validation) =>
        validation.Errors
            .GroupBy(error => error.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray(),
                StringComparer.Ordinal);
}

internal static class DocumentResultExtensions
{
    public static Result<DocumentDeptPermissionsResponse> ToResponseFailure(
        this Result<Document> result) => result.Status switch
    {
        ResultStatus.NotFound => Result<DocumentDeptPermissionsResponse>.NotFound(
            result.Detail, result.Title ?? "找不到資源"),
        ResultStatus.Forbidden => Result<DocumentDeptPermissionsResponse>.Forbidden(
            result.Detail, result.Title ?? "沒有權限"),
        ResultStatus.Unauthorized => Result<DocumentDeptPermissionsResponse>.Unauthorized(
            result.Detail, result.Title ?? "尚未登入"),
        _ => throw new InvalidOperationException("The document result was not a supported failure.")
    };
}
