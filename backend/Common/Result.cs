using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace IsoDocument.Api.Common;

public enum ResultStatus
{
    Success,
    NotFound,
    Conflict,
    Forbidden,
    ValidationFailed,
    Unauthorized
}

public class Result
{
    protected Result(
        ResultStatus status,
        string? title = null,
        string? detail = null,
        Dictionary<string, string[]>? errors = null)
    {
        Status = status;
        Title = title;
        Detail = detail;
        Errors = errors;
    }

    public ResultStatus Status { get; }

    public bool IsSuccess => Status == ResultStatus.Success;

    public string? Title { get; }

    public string? Detail { get; }

    public Dictionary<string, string[]>? Errors { get; }

    internal virtual bool HasValue => false;

    internal virtual object? UntypedValue => null;

    public static Result Success() => new(ResultStatus.Success);

    public static Result NotFound(string? detail = null, string title = "找不到資源") =>
        new(ResultStatus.NotFound, title, detail);

    public static Result Conflict(string? detail = null, string title = "資料衝突") =>
        new(ResultStatus.Conflict, title, detail);

    public static Result Forbidden(string? detail = null, string title = "沒有權限") =>
        new(ResultStatus.Forbidden, title, detail);

    public static Result ValidationFailed(
        Dictionary<string, string[]> errors,
        string? detail = null,
        string title = "輸入資料有誤") =>
        new(ResultStatus.ValidationFailed, title, detail, errors);

    public static Result Unauthorized(string? detail = null, string title = "尚未登入") =>
        new(ResultStatus.Unauthorized, title, detail);
}

public sealed class Result<T> : Result
{
    private Result(
        ResultStatus status,
        T? value = default,
        string? title = null,
        string? detail = null,
        Dictionary<string, string[]>? errors = null)
        : base(status, title, detail, errors)
    {
        Value = value;
    }

    public T? Value { get; }

    internal override bool HasValue => IsSuccess;

    internal override object? UntypedValue => Value;

    public static Result<T> Success(T value) => new(ResultStatus.Success, value);

    public new static Result<T> NotFound(string? detail = null, string title = "找不到資源") =>
        new(ResultStatus.NotFound, title: title, detail: detail);

    public new static Result<T> Conflict(string? detail = null, string title = "資料衝突") =>
        new(ResultStatus.Conflict, title: title, detail: detail);

    public new static Result<T> Forbidden(string? detail = null, string title = "沒有權限") =>
        new(ResultStatus.Forbidden, title: title, detail: detail);

    public new static Result<T> ValidationFailed(
        Dictionary<string, string[]> errors,
        string? detail = null,
        string title = "輸入資料有誤") =>
        new(ResultStatus.ValidationFailed, title: title, detail: detail, errors: errors);

    public new static Result<T> Unauthorized(string? detail = null, string title = "尚未登入") =>
        new(ResultStatus.Unauthorized, title: title, detail: detail);
}

public static class ResultActionExtensions
{
    public static IActionResult ToOkResult(this Result result, ControllerBase controller) =>
        result.IsSuccess
            ? result.HasValue ? new OkObjectResult(result.UntypedValue) : new OkResult()
            : ToFailureResult(result, controller);

    public static IActionResult ToCreatedResult(
        this Result result,
        ControllerBase controller,
        string? location = null) =>
        result.IsSuccess
            ? new CreatedResult(location, result.UntypedValue)
            : ToFailureResult(result, controller);

    public static IActionResult ToNoContentResult(this Result result, ControllerBase controller) =>
        result.IsSuccess
            ? new NoContentResult()
            : ToFailureResult(result, controller);

    private static IActionResult ToFailureResult(Result result, ControllerBase controller)
    {
        var status = result.Status.ToHttpStatusCode();

        var factory = controller.HttpContext.RequestServices
            .GetRequiredService<ProblemDetailsFactory>();
        var problem = ApiProblem.Create(
            factory,
            controller.HttpContext,
            status,
            result.Title ?? GetDefaultTitle(result.Status),
            result.Detail,
            result.Errors);

        return new ObjectResult(problem)
        {
            StatusCode = status
        };
    }

    private static string GetDefaultTitle(ResultStatus status) => status switch
    {
        ResultStatus.NotFound => "找不到資源",
        ResultStatus.Conflict => "資料衝突",
        ResultStatus.Forbidden => "沒有權限",
        ResultStatus.ValidationFailed => "輸入資料有誤",
        ResultStatus.Unauthorized => "尚未登入",
        _ => "發生錯誤"
    };
}

internal static class ResultStatusExtensions
{
    public static int ToHttpStatusCode(this ResultStatus status) => status switch
    {
        ResultStatus.NotFound => StatusCodes.Status404NotFound,
        ResultStatus.Conflict => StatusCodes.Status409Conflict,
        ResultStatus.Forbidden => StatusCodes.Status403Forbidden,
        ResultStatus.ValidationFailed => StatusCodes.Status400BadRequest,
        ResultStatus.Unauthorized => StatusCodes.Status401Unauthorized,
        _ => throw new InvalidOperationException(
            $"Cannot convert result status '{status}' to a failure response.")
    };
}
