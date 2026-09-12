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
    IValidator<UpdateDocumentPermissionMatrixRequest> matrixValidator,
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

    public async Task<Result<UpdateDocumentPermissionMatrixResponse>> UpdateMatrixAsync(
        UpdateDocumentPermissionMatrixRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await matrixValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<UpdateDocumentPermissionMatrixResponse>.ValidationFailed(
                ToMatrixErrors(validation));
        }

        if (currentUser.UserId is not { } operatorUserId)
        {
            return Result<UpdateDocumentPermissionMatrixResponse>.Unauthorized("請先登入後再操作。");
        }

        var items = request.Items;
        var documentIds = items.Select(item => item.DocumentId).ToArray();

        // 2. 文件存在
        var documentsById = await permissionStore.FindDocumentsAsync(documentIds, cancellationToken);
        var missingDocumentIds = documentIds.Where(id => !documentsById.ContainsKey(id)).ToArray();
        if (missingDocumentIds.Length > 0)
        {
            return Result<UpdateDocumentPermissionMatrixResponse>.NotFound(
                $"找不到指定的文件：{missingDocumentIds[0]}。");
        }

        // 3. 公司範圍：COMPANY_ADMIN 限自己公司；SYSTEM_ADMIN 不限，但下一步的部門檢查仍會鎖住各自公司。
        var inaccessibleDocument = documentsById.Values
            .FirstOrDefault(document => !currentUser.CanAccessCompany(document.CompanyId));
        if (inaccessibleDocument is not null)
        {
            return Result<UpdateDocumentPermissionMatrixResponse>.Forbidden(
                $"您沒有修改文件 {inaccessibleDocument.Id} 權限的權限。");
        }

        // 4. 部門合法性：每個 item 的 departmentIds 都必須屬於「該 item 文件所屬公司」。
        //    同一批次通常集中在同一間公司，因此按 companyId 分組、每間公司只查一次，避免逐 item 查詢。
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var validDeptIdsByCompany = new Dictionary<Guid, IReadOnlySet<Guid>>();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var companyId = documentsById[item.DocumentId].CompanyId;
            var requestedDeptIds = item.DepartmentIds.Distinct().ToArray();
            if (requestedDeptIds.Length == 0)
            {
                continue;
            }

            if (!validDeptIdsByCompany.TryGetValue(companyId, out var companyDeptIds))
            {
                var requestedForCompany = items
                    .Where(candidate => documentsById[candidate.DocumentId].CompanyId == companyId)
                    .SelectMany(candidate => candidate.DepartmentIds)
                    .Distinct()
                    .ToArray();
                companyDeptIds = await permissionStore.FindCompanyDeptIdsAsync(
                    companyId, requestedForCompany, cancellationToken);
                validDeptIdsByCompany[companyId] = companyDeptIds;
            }

            if (requestedDeptIds.Any(deptId => !companyDeptIds.Contains(deptId)))
            {
                errors[$"items[{index}].departmentIds"] =
                    ["所有部門必須存在，且屬於文件所屬的公司。"];
            }
        }

        if (errors.Count > 0)
        {
            return Result<UpdateDocumentPermissionMatrixResponse>.ValidationFailed(errors);
        }

        // 5-6. 撈現況、逐 item 計算 diff（toAdd / toRemove），彙總成整批的寫入清單。
        var currentPermissionsByDocument = await permissionStore.ListPermissionsAsync(
            documentIds, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var toAddAll = new List<DocumentDeptPermission>();
        var toRemoveAll = new List<DocumentDeptPermission>();
        var results = new List<DocumentPermissionMatrixUpdateResult>(items.Count);

        foreach (var item in items)
        {
            var document = documentsById[item.DocumentId];
            var requestedDeptIds = item.DepartmentIds.Distinct().Order().ToArray();
            var currentPermissions = currentPermissionsByDocument.GetValueOrDefault(
                item.DocumentId, Array.Empty<DocumentDeptPermission>());
            var currentDeptIds = currentPermissions.Select(p => p.DeptId).ToHashSet();
            var requestedSet = requestedDeptIds.ToHashSet();

            var toRemove = currentPermissions
                .Where(p => !requestedSet.Contains(p.DeptId))
                .ToArray();
            var toAdd = requestedDeptIds
                .Where(deptId => !currentDeptIds.Contains(deptId))
                .Select(deptId => new DocumentDeptPermission
                {
                    Id = Guid.NewGuid(),
                    DocumentId = item.DocumentId,
                    DeptId = deptId,
                    GrantedBy = operatorUserId,
                    CreatedAt = now
                })
                .ToArray();

            toAddAll.AddRange(toAdd);
            toRemoveAll.AddRange(toRemove);
            results.Add(new DocumentPermissionMatrixUpdateResult(
                document.Id,
                document.DocumentNo,
                requestedDeptIds,
                toAdd.Select(p => p.DeptId).ToArray(),
                toRemove.Select(p => p.DeptId).ToArray()));
        }

        var updatedBy = new UpdatedByResponse(operatorUserId, currentUser.Name ?? currentUser.Empno ?? string.Empty);

        // 沒有任何一份文件實際變動：不開 transaction、不寫 audit（比照單文件端點的既有行為）。
        if (toAddAll.Count == 0 && toRemoveAll.Count == 0)
        {
            return Result<UpdateDocumentPermissionMatrixResponse>.Success(
                new UpdateDocumentPermissionMatrixResponse(results, updatedBy, now));
        }

        // 7-9. 整批寫入，全部在同一個 transaction 內；任一步驟失敗即整批 rollback。
        await using var transaction = await permissionStore.BeginTransactionAsync(cancellationToken);
        permissionStore.RemoveRange(toRemoveAll);
        permissionStore.AddRange(toAddAll);
        await permissionStore.SaveChangesAsync(cancellationToken);

        foreach (var result in results)
        {
            if (result.AddedDepartmentIds.Count == 0 && result.RemovedDepartmentIds.Count == 0)
            {
                continue;
            }

            var document = documentsById[result.DocumentId];
            var oldDeptIds = currentPermissionsByDocument
                .GetValueOrDefault(result.DocumentId, Array.Empty<DocumentDeptPermission>())
                .Select(p => p.DeptId)
                .ToArray();
            await auditLogService.WriteDocumentDeptPermissionsChangedAsync(
                document.CompanyId,
                result.DocumentId,
                oldDeptIds,
                result.DepartmentIds,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return Result<UpdateDocumentPermissionMatrixResponse>.Success(
            new UpdateDocumentPermissionMatrixResponse(results, updatedBy, now));
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

    private static Dictionary<string, string[]> ToMatrixErrors(ValidationResult validation) =>
        validation.Errors
            .GroupBy(error => ToCamelCasePath(error.PropertyName), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray(),
                StringComparer.Ordinal);

    private static string ToCamelCasePath(string path) => path
        .Replace("Items", "items", StringComparison.Ordinal)
        .Replace("DocumentId", "documentId", StringComparison.Ordinal)
        .Replace("DepartmentIds", "departmentIds", StringComparison.Ordinal);
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
