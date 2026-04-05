using FluentValidation;
using FluentValidation.Results;
using HouseholdBudgetMate.Abstractions.Contracts.Incomes.Requests;

namespace HouseholdBudgetMate.Application.Validation.Incomes;

public sealed class CreateRegularIncomeDefinitionRequestValidator : AbstractValidator<CreateRegularIncomeDefinitionRequest>
{
    public CreateRegularIncomeDefinitionRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Name is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Income amount must be greater than zero.");

        RuleFor(x => x.DayOfMonth)
            .InclusiveBetween(1, 31)
            .WithMessage("Day of month must be in range 1..31.");

        RuleFor(x => x.AccountId).GreaterThan(0);
    }

    protected override bool PreValidate(ValidationContext<CreateRegularIncomeDefinitionRequest> context, ValidationResult result)
    {
        if (context.InstanceToValidate is not null)
        {
            context.InstanceToValidate.Name = (context.InstanceToValidate.Name ?? string.Empty).Trim();
        }

        return base.PreValidate(context, result);
    }
}

public sealed class UpdateRegularIncomeDefinitionRequestValidator : AbstractValidator<UpdateRegularIncomeDefinitionRequest>
{
    public UpdateRegularIncomeDefinitionRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Name is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Income amount must be greater than zero.");

        RuleFor(x => x.DayOfMonth)
            .InclusiveBetween(1, 31)
            .WithMessage("Day of month must be in range 1..31.");

        RuleFor(x => x.AccountId).GreaterThan(0);
    }

    protected override bool PreValidate(ValidationContext<UpdateRegularIncomeDefinitionRequest> context, ValidationResult result)
    {
        if (context.InstanceToValidate is not null)
        {
            context.InstanceToValidate.Name = (context.InstanceToValidate.Name ?? string.Empty).Trim();
        }

        return base.PreValidate(context, result);
    }
}

public sealed class DeleteRegularIncomeDefinitionRequestValidator : AbstractValidator<DeleteRegularIncomeDefinitionRequest>
{
    public DeleteRegularIncomeDefinitionRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public sealed class CreateIncomeRequestValidator : AbstractValidator<CreateIncomeRequest>
{
    public CreateIncomeRequestValidator()
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 3000)
            .WithMessage("Year is out of allowed range.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12)
            .WithMessage("Month must be in range 1..12.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Name is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Income amount must be greater than zero.");

        RuleFor(x => x.AccountId).GreaterThan(0);

        RuleFor(x => x)
            .Must(x => x.ExpectedDayOfMonth.Year == x.Year && x.ExpectedDayOfMonth.Month == x.Month)
            .WithMessage("Expected day must belong to selected month and year.");
    }

    protected override bool PreValidate(ValidationContext<CreateIncomeRequest> context, ValidationResult result)
    {
        if (context.InstanceToValidate is not null)
        {
            context.InstanceToValidate.Name = (context.InstanceToValidate.Name ?? string.Empty).Trim();
        }

        return base.PreValidate(context, result);
    }
}

public sealed class UpdateIncomeRequestValidator : AbstractValidator<UpdateIncomeRequest>
{
    public UpdateIncomeRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Name is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Income amount must be greater than zero.");

        RuleFor(x => x.AccountId).GreaterThan(0);
    }

    protected override bool PreValidate(ValidationContext<UpdateIncomeRequest> context, ValidationResult result)
    {
        if (context.InstanceToValidate is not null)
        {
            context.InstanceToValidate.Name = (context.InstanceToValidate.Name ?? string.Empty).Trim();
        }

        return base.PreValidate(context, result);
    }
}

public sealed class DeleteIncomeRequestValidator : AbstractValidator<DeleteIncomeRequest>
{
    public DeleteIncomeRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}