using System.Linq.Expressions;

namespace IsoDocument.Api.Common;

/// <summary>
/// 列表 API 的排序條件（查詢參數 <c>sortBy</c> / <c>sortDirection</c>，SPEC 第 4 節「列表排序」）。
/// <see cref="Field"/> 一律是白名單內的正規名稱；未帶 sortBy 時不建立此物件，由各 store 套用預設排序。
/// </summary>
public sealed record ListSort(string Field, bool Descending)
{
    public static bool TryParse(
        string? sortBy,
        string? sortDirection,
        IReadOnlyCollection<string> allowedFields,
        out ListSort? sort,
        out Dictionary<string, string[]> errors)
    {
        sort = null;
        errors = new(StringComparer.Ordinal);

        var descending = false;
        if (!string.IsNullOrWhiteSpace(sortDirection))
        {
            switch (sortDirection.Trim().ToLowerInvariant())
            {
                case "asc":
                    break;
                case "desc":
                    descending = true;
                    break;
                default:
                    errors["sortDirection"] = ["排序方向只能是 asc 或 desc。"];
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return errors.Count == 0;
        }

        var field = allowedFields.FirstOrDefault(allowed =>
            string.Equals(allowed, sortBy.Trim(), StringComparison.OrdinalIgnoreCase));
        if (field is null)
        {
            errors["sortBy"] = [$"不支援依「{sortBy.Trim()}」排序。"];
        }

        if (errors.Count > 0)
        {
            return false;
        }

        sort = new ListSort(field!, descending);
        return true;
    }
}

public static class ListSortQueryableExtensions
{
    public static IOrderedQueryable<T> OrderByDirection<T, TKey>(
        this IQueryable<T> source,
        Expression<Func<T, TKey>> keySelector,
        bool descending) =>
        descending ? source.OrderByDescending(keySelector) : source.OrderBy(keySelector);

    public static IOrderedQueryable<T> ThenByDirection<T, TKey>(
        this IOrderedQueryable<T> source,
        Expression<Func<T, TKey>> keySelector,
        bool descending) =>
        descending ? source.ThenByDescending(keySelector) : source.ThenBy(keySelector);

    /// <summary>
    /// 可為 null 的欄位：不論升冪或降冪，null 一律排在最後
    /// （PostgreSQL 預設 DESC 會把 NULL 排在最前面）。
    /// </summary>
    public static IOrderedQueryable<T> OrderByNullsLast<T, TKey>(
        this IQueryable<T> source,
        Expression<Func<T, TKey>> keySelector,
        bool descending)
    {
        var isNull = Expression.Lambda<Func<T, bool>>(
            Expression.Equal(keySelector.Body, Expression.Constant(null, keySelector.Body.Type)),
            keySelector.Parameters);
        return source.OrderBy(isNull).ThenByDirection(keySelector, descending);
    }
}
