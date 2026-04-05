using FluentValidation;
using FluentValidation.Results;
using HouseholdBudgetMate.Abstractions.Contracts.Categories.Requests;

namespace HouseholdBudgetMate.Application.Validation.Categories;

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Name is required.");

        RuleFor(x => x.Color)
            .NotEmpty().WithMessage("Color is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Color is required.");
    }

    protected override bool PreValidate(ValidationContext<CreateCategoryRequest> context, ValidationResult result)
    {
        if (context.InstanceToValidate is not null)
        {
            context.InstanceToValidate.Name = (context.InstanceToValidate.Name ?? string.Empty).Trim();
            context.InstanceToValidate.Color = (context.InstanceToValidate.Color ?? string.Empty).Trim();
        }

        return base.PreValidate(context, result);
    }
}

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Name is required.");

        RuleFor(x => x.Color)
            .NotEmpty().WithMessage("Color is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Color is required.");
    }

    protected override bool PreValidate(ValidationContext<UpdateCategoryRequest> context, ValidationResult result)
    {
        if (context.InstanceToValidate is not null)
        {
            context.InstanceToValidate.Name = (context.InstanceToValidate.Name ?? string.Empty).Trim();
            context.InstanceToValidate.Color = (context.InstanceToValidate.Color ?? string.Empty).Trim();
        }

        return base.PreValidate(context, result);
    }
}

public sealed class DeleteCategoryRequestValidator : AbstractValidator<DeleteCategoryRequest>
{
    public DeleteCategoryRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public sealed class CreateTagRequestValidator : AbstractValidator<CreateTagRequest>
{
    public CreateTagRequestValidator()
    {
        RuleFor(x => x.CategoryId).GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Name is required.");
    }

    protected override bool PreValidate(ValidationContext<CreateTagRequest> context, ValidationResult result)
    {
        if (context.InstanceToValidate is not null)
        {
            context.InstanceToValidate.Name = (context.InstanceToValidate.Name ?? string.Empty).Trim();
        }

        return base.PreValidate(context, result);
    }
}

public sealed class UpdateTagRequestValidator : AbstractValidator<UpdateTagRequest>
{
    public UpdateTagRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.CategoryId).GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Name is required.");
    }

    protected override bool PreValidate(ValidationContext<UpdateTagRequest> context, ValidationResult result)
    {
        if (context.InstanceToValidate is not null)
        {
            context.InstanceToValidate.Name = (context.InstanceToValidate.Name ?? string.Empty).Trim();
        }

        return base.PreValidate(context, result);
    }
}

public sealed class DeleteTagRequestValidator : AbstractValidator<DeleteTagRequest>
{
    public DeleteTagRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}