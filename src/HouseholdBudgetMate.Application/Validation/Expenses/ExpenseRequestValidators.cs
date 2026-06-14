using FluentValidation;
using FluentValidation.Results;
using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;

namespace HouseholdBudgetMate.Application.Validation.Expenses;

public sealed class CreateMonthSavingsTransferItemRequestValidator : AbstractValidator<CreateMonthSavingsTransferItemRequest>
{
    public CreateMonthSavingsTransferItemRequestValidator()
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 3000)
            .WithMessage("Year is out of allowed range.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12)
            .WithMessage("Month must be in range 1..12.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Income amount must be greater than zero.");

        RuleFor(x => x)
            .Must(x => x.TransferDate.Year == x.Year && x.TransferDate.Month == x.Month)
            .WithMessage("Savings transfer date must belong to selected month and year.");
    }
}

public sealed class UpdateMonthSavingsTransferItemRequestValidator : AbstractValidator<UpdateMonthSavingsTransferItemRequest>
{
    public UpdateMonthSavingsTransferItemRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Income amount must be greater than zero.");
    }
}

public sealed class DeleteMonthSavingsTransferItemRequestValidator : AbstractValidator<DeleteMonthSavingsTransferItemRequest>
{
    public DeleteMonthSavingsTransferItemRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public sealed class CreateExpenseRequestValidator : AbstractValidator<CreateExpenseRequest>
{
    public CreateExpenseRequestValidator()
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 3000)
            .WithMessage("Year is out of allowed range.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12)
            .WithMessage("Month must be in range 1..12.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Expense name is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x))
            .WithMessage("Expense name is required.")
            .MaximumLength(200)
            .WithMessage("Expense name cannot exceed 200 characters.");

        RuleFor(x => x.CategoryId).GreaterThan(0);

        RuleFor(x => x.TagId)
            .GreaterThan(0)
            .When(x => x.TagId.HasValue);

        RuleFor(x => x.PlannedAmount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Planowana kwota wydatku musi być większa lub równa zero.");

        RuleFor(x => x.ActualAmount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Faktyczna kwota wydatku musi być większa lub równa zero.");
    }

    protected override bool PreValidate(ValidationContext<CreateExpenseRequest> context, ValidationResult result)
    {
        if (context.InstanceToValidate is not null)
        {
            context.InstanceToValidate.Name = (context.InstanceToValidate.Name ?? string.Empty).Trim();
        }

        return base.PreValidate(context, result);
    }
}

public sealed class UpdateExpenseRequestValidator : AbstractValidator<UpdateExpenseRequest>
{
    public UpdateExpenseRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Expense name is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x))
            .WithMessage("Expense name is required.")
            .MaximumLength(200)
            .WithMessage("Expense name cannot exceed 200 characters.");

        RuleFor(x => x.CategoryId).GreaterThan(0);

        RuleFor(x => x.TagId)
            .GreaterThan(0)
            .When(x => x.TagId.HasValue);

        RuleFor(x => x.PlannedAmount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Planowana kwota wydatku musi być większa lub równa zero.");

        RuleFor(x => x.ActualAmount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Faktyczna kwota wydatku musi być większa lub równa zero.");
    }

    protected override bool PreValidate(ValidationContext<UpdateExpenseRequest> context, ValidationResult result)
    {
        if (context.InstanceToValidate is not null)
        {
            context.InstanceToValidate.Name = (context.InstanceToValidate.Name ?? string.Empty).Trim();
        }

        return base.PreValidate(context, result);
    }
}

public sealed class DeleteExpenseRequestValidator : AbstractValidator<DeleteExpenseRequest>
{
    public DeleteExpenseRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public sealed class ReorderExpensesRequestValidator : AbstractValidator<ReorderExpensesRequest>
{
    public ReorderExpensesRequestValidator()
    {
        RuleForEach(x => x.ExpenseIds).GreaterThan(0);

        RuleFor(x => x.ExpenseIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Expense ids must be unique.");
    }
}

public sealed class CopySelectedExpensesToNextMonthRequestValidator : AbstractValidator<CopySelectedExpensesToNextMonthRequest>
{
    public CopySelectedExpensesToNextMonthRequestValidator()
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 3000)
            .WithMessage("Year is out of allowed range.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12)
            .WithMessage("Month must be in range 1..12.");

        RuleFor(x => x.ExpenseIds)
            .NotEmpty()
            .WithMessage("At least one expense must be selected.");

        RuleForEach(x => x.ExpenseIds)
            .GreaterThan(0);

        RuleFor(x => x.ExpenseIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Expense ids must be unique.");
    }
}

public sealed class ApplyMonthPlanSuggestionItemRequestValidator : AbstractValidator<ApplyMonthPlanSuggestionItemRequest>
{
    public ApplyMonthPlanSuggestionItemRequestValidator()
    {
        RuleFor(x => x.SourceExpenseId).GreaterThan(0);

        RuleFor(x => x.PlannedAmount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Planowana kwota musi być większa lub równa zero.");
    }
}

public sealed class ApplyMonthPlanSuggestionsRequestValidator : AbstractValidator<ApplyMonthPlanSuggestionsRequest>
{
    public ApplyMonthPlanSuggestionsRequestValidator()
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 3000)
            .WithMessage("Year is out of allowed range.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12)
            .WithMessage("Month must be in range 1..12.");

        RuleFor(x => x.Suggestions)
            .NotEmpty()
            .WithMessage("Wybierz co najmniej jedną propozycję.");

        RuleForEach(x => x.Suggestions)
            .SetValidator(new ApplyMonthPlanSuggestionItemRequestValidator());

        RuleFor(x => x.Suggestions)
            .Must(items => items.Select(x => x.SourceExpenseId).Distinct().Count() == items.Count)
            .WithMessage("Propozycje wydatków nie mogą się powtarzać.");
    }
}

public sealed class CopySelectedExpensesToMonthRequestValidator : AbstractValidator<CopySelectedExpensesToMonthRequest>
{
    public CopySelectedExpensesToMonthRequestValidator()
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 3000)
            .WithMessage("Year is out of allowed range.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12)
            .WithMessage("Month must be in range 1..12.");

        RuleFor(x => x.TargetYear)
            .InclusiveBetween(2000, 3000)
            .WithMessage("Target year is out of allowed range.");

        RuleFor(x => x.TargetMonth)
            .InclusiveBetween(1, 12)
            .WithMessage("Target month must be in range 1..12.");

        RuleFor(x => x)
            .Must(x => x.Year != x.TargetYear || x.Month != x.TargetMonth)
            .WithMessage("Source and target month must be different.");

        RuleFor(x => x.ExpenseIds)
            .NotEmpty()
            .WithMessage("At least one expense must be selected.");

        RuleForEach(x => x.ExpenseIds)
            .GreaterThan(0);

        RuleFor(x => x.ExpenseIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Expense ids must be unique.");
    }
}

public sealed class UpsertAnnualPlanRequestValidator : AbstractValidator<UpsertAnnualPlanRequest>
{
    public UpsertAnnualPlanRequestValidator()
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 3000)
            .WithMessage("Year is out of allowed range.");

        RuleFor(x => x.ExpectedIncomeAmount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Expected income amount must be zero or greater.");

        RuleFor(x => x.ExpectedSavingsAmount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Expected savings amount must be zero or greater.");
    }
}

public sealed class CreateExpenseLineItemRequestValidator : AbstractValidator<CreateExpenseLineItemRequest>
{
    public CreateExpenseLineItemRequestValidator()
    {
        RuleFor(x => x.ExpenseId).GreaterThan(0);

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Line item description is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x))
            .WithMessage("Line item description is required.");

        RuleFor(x => x.TagId)
            .GreaterThan(0)
            .When(x => x.TagId.HasValue);

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Kwota pozycji wydatku musi być większa lub równa zero.");
    }

    protected override bool PreValidate(ValidationContext<CreateExpenseLineItemRequest> context, ValidationResult result)
    {
        if (context.InstanceToValidate is not null)
        {
            context.InstanceToValidate.Description = (context.InstanceToValidate.Description ?? string.Empty).Trim();
        }

        return base.PreValidate(context, result);
    }
}

public sealed class UpdateExpenseLineItemRequestValidator : AbstractValidator<UpdateExpenseLineItemRequest>
{
    public UpdateExpenseLineItemRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Line item description is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x))
            .WithMessage("Line item description is required.");

        RuleFor(x => x.TagId)
            .GreaterThan(0)
            .When(x => x.TagId.HasValue);

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Kwota pozycji wydatku musi być większa lub równa zero.");
    }

    protected override bool PreValidate(ValidationContext<UpdateExpenseLineItemRequest> context, ValidationResult result)
    {
        if (context.InstanceToValidate is not null)
        {
            context.InstanceToValidate.Description = (context.InstanceToValidate.Description ?? string.Empty).Trim();
        }

        return base.PreValidate(context, result);
    }
}

public sealed class DeleteExpenseLineItemRequestValidator : AbstractValidator<DeleteExpenseLineItemRequest>
{
    public DeleteExpenseLineItemRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public sealed class CreateRegularExpenseDefinitionRequestValidator : AbstractValidator<CreateRegularExpenseDefinitionRequest>
{
    public CreateRegularExpenseDefinitionRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Name is required.")
            .MaximumLength(160).WithMessage("Name cannot exceed 160 characters.");

        RuleFor(x => x.CategoryId).GreaterThan(0);

        RuleFor(x => x.TagId)
            .GreaterThan(0)
            .When(x => x.TagId.HasValue);

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than zero.");
    }

    protected override bool PreValidate(ValidationContext<CreateRegularExpenseDefinitionRequest> context, ValidationResult result)
    {
        if (context.InstanceToValidate is not null)
        {
            context.InstanceToValidate.Name = (context.InstanceToValidate.Name ?? string.Empty).Trim();
        }

        return base.PreValidate(context, result);
    }
}

public sealed class UpdateRegularExpenseDefinitionRequestValidator : AbstractValidator<UpdateRegularExpenseDefinitionRequest>
{
    public UpdateRegularExpenseDefinitionRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Name is required.")
            .MaximumLength(160).WithMessage("Name cannot exceed 160 characters.");

        RuleFor(x => x.CategoryId).GreaterThan(0);

        RuleFor(x => x.TagId)
            .GreaterThan(0)
            .When(x => x.TagId.HasValue);

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than zero.");
    }

    protected override bool PreValidate(ValidationContext<UpdateRegularExpenseDefinitionRequest> context, ValidationResult result)
    {
        if (context.InstanceToValidate is not null)
        {
            context.InstanceToValidate.Name = (context.InstanceToValidate.Name ?? string.Empty).Trim();
        }

        return base.PreValidate(context, result);
    }
}

public sealed class DeleteRegularExpenseDefinitionRequestValidator : AbstractValidator<DeleteRegularExpenseDefinitionRequest>
{
    public DeleteRegularExpenseDefinitionRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public sealed class ReorderRegularExpenseDefinitionsRequestValidator : AbstractValidator<ReorderRegularExpenseDefinitionsRequest>
{
    public ReorderRegularExpenseDefinitionsRequestValidator()
    {
        RuleFor(x => x.DefinitionIds)
            .NotEmpty()
            .WithMessage("Definition ids are required.");

        RuleForEach(x => x.DefinitionIds)
            .GreaterThan(0);

        RuleFor(x => x.DefinitionIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Definition ids must be unique.");
    }
}

