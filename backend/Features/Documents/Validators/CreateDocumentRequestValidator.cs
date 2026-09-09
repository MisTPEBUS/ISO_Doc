using System.Text.RegularExpressions;
using FluentValidation;
using IsoDocument.Api.Features.Documents.Dtos;

namespace IsoDocument.Api.Features.Documents.Validators;

public sealed partial class CreateDocumentRequestValidator
    : AbstractValidator<CreateDocumentRequest>
{
    public CreateDocumentRequestValidator(IDocumentStore documentStore)
    {
        RuleFor(request => request.CompanyId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Company is required.")
            .MustAsync(documentStore.CompanyExistsAsync)
            .WithMessage("The specified company does not exist.")
            .OverridePropertyName("companyId");
        RuleFor(request => request.DocumentNo)
            .Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Document number is required.")
            .MaximumLength(50)
            .WithMessage("Document number must not exceed 50 characters.")
            .Must(value => value is not null && DocumentNoPattern().IsMatch(value.Trim()))
            .WithMessage("Document number may contain only letters, numbers, and hyphens, and must start and end with a letter or number.")
            .OverridePropertyName("documentNo");
        RuleFor(request => request.Name)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Document name is required.")
            .MaximumLength(255)
            .WithMessage("Document name must not exceed 255 characters.")
            .OverridePropertyName("name");
    }

    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,48}[A-Za-z0-9])?$")]
    private static partial Regex DocumentNoPattern();
}
