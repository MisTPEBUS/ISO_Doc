using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace IsoDocument.Api.Common;

public static class ApiProblem
{
    public static ProblemDetails Create(
        ProblemDetailsFactory factory,
        HttpContext httpContext,
        int status,
        string title,
        string? detail = null,
        Dictionary<string, string[]>? errors = null)
    {
        if (errors is null)
        {
            return factory.CreateProblemDetails(
                httpContext,
                statusCode: status,
                title: title,
                detail: detail);
        }

        var modelState = new ModelStateDictionary();
        foreach (var (field, messages) in errors)
        {
            foreach (var message in messages)
            {
                modelState.AddModelError(field, message);
            }
        }

        return factory.CreateValidationProblemDetails(
            httpContext,
            modelState,
            statusCode: status,
            title: title,
            detail: detail);
    }
}
