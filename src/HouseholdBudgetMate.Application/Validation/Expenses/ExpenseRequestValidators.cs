using FluentValidation;
using FluentValidation.Results;
using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;

namespace HouseholdBudgetMate.Application.Validation.Expenses;

public sealed class UpdateMonthSavingsTransferRequestValidator : AbstractValidator<UpdateMonthSavingsTransferRequest>
{
    public UpdateMonthSavingsTransferRequestValidator()
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 3000)
            .WithMessage("Year is out of allowed range.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12)
            .WithMessage("Month must be in range 1..12.");

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Savings transfer amount cannot be negative.");

        When(x => x.Amount > 0, () =>
        {
            RuleFor(x => x.TransferDate)
                .NotNull()
                .WithMessage("Transfer date is required when amount is greater than zero.");

            RuleFor(x => x)
                .Must(x => x.TransferDate.HasValue
                           && x.TransferDate.Value.Year == x.Year
                           && x.TransferDate.Value.Month == x.Month)
                .WithMessage("Transfer date must belong to selected month and year.");
        });
    }
}

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