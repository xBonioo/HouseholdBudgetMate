using FluentValidation;
using FluentValidation.Results;
using HouseholdBudgetMate.Abstractions.Contracts.Accounts.Requests;
using HouseholdBudgetMate.Abstractions.Enums;

namespace HouseholdBudgetMate.Application.Validation.Accounts;

public sealed class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Name is required.");

        RuleFor(x => x.Type)
            .Must(x => Enum.IsDefined(typeof(AccountType), x))
            .WithMessage("Account type is invalid.");
    }

    protected override bool PreValidate(ValidationContext<CreateAccountRequest> context, ValidationResult result)
    {
        if (context.InstanceToValidate is not null)
        {
            context.InstanceToValidate.Name = (context.InstanceToValidate.Name ?? string.Empty).Trim();
        }

        return base.PreValidate(context, result);
    }
}

public sealed class UpdateAccountRequestValidator : AbstractValidator<UpdateAccountRequest>
{
    public UpdateAccountRequestValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Name is required.");

        RuleFor(x => x.Type)
            .Must(x => Enum.IsDefined(typeof(AccountType), x))
            .WithMessage("Account type is invalid.");
    }

    protected override bool PreValidate(ValidationContext<UpdateAccountRequest> context, ValidationResult result)
    {
        if (context.InstanceToValidate is not null)
        {
            context.InstanceToValidate.Name = (context.InstanceToValidate.Name ?? string.Empty).Trim();
        }

        return base.PreValidate(context, result);
    }
}

public sealed class DeleteAccountRequestValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public sealed class SetAccountArchivedRequestValidator : AbstractValidator<SetAccountArchivedRequest>
{
    public SetAccountArchivedRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public sealed class ReorderAccountsRequestValidator : AbstractValidator<ReorderAccountsRequest>
{
    public ReorderAccountsRequestValidator()
    {
        RuleForEach(x => x.AccountIds).GreaterThan(0);

        RuleFor(x => x.AccountIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Account ids must be unique.");
    }
}

public sealed class UpsertAccountMonthBalanceRequestValidator : AbstractValidator<UpsertAccountMonthBalanceRequest>
{
    public UpsertAccountMonthBalanceRequestValidator()
    {
        RuleFor(x => x.AccountId).GreaterThan(0);

        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 3000)
            .WithMessage("Year is out of allowed range.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12)
            .WithMessage("Month must be in range 1..12.");
    }
}

public sealed class UpdateAccountMonthBalanceAmountRequestValidator : AbstractValidator<UpdateAccountMonthBalanceAmountRequest>
{
    public UpdateAccountMonthBalanceAmountRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}