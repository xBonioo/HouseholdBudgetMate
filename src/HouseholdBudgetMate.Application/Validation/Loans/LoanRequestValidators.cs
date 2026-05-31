using FluentValidation;
using FluentValidation.Results;
using HouseholdBudgetMate.Abstractions.Contracts.Loans.Requests;
using HouseholdBudgetMate.Abstractions.Enums;

namespace HouseholdBudgetMate.Application.Validation.Loans;

public sealed class CreateLoanRequestValidator : AbstractValidator<CreateLoanRequest>
{
    public CreateLoanRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Name is required.")
            .MaximumLength(160).WithMessage("Name cannot exceed 160 characters.");

        RuleFor(x => x.LoanType)
            .Must(x => Enum.IsDefined(typeof(LoanType), x))
            .WithMessage("Loan type is invalid.");

        RuleFor(x => x.InterestMode)
            .Must(x => Enum.IsDefined(typeof(LoanInterestMode), x))
            .WithMessage("Loan interest mode is invalid.");

        RuleFor(x => x.Principal)
            .GreaterThan(0)
            .WithMessage("Principal must be greater than zero.");

        RuleFor(x => x.OriginalPrincipal)
            .GreaterThan(0)
            .When(x => x.OriginalPrincipal.HasValue)
            .WithMessage("Original principal must be greater than zero.");

        RuleFor(x => x.GracePeriodMonths)
            .GreaterThanOrEqualTo(0)
            .When(x => x.GracePeriodMonths.HasValue)
            .WithMessage("Grace period months cannot be negative.");

        RuleFor(x => x.InterestRate)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Interest rate cannot be negative.");

        RuleFor(x => x.MarginRate)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MarginRate.HasValue)
            .WithMessage("Margin cannot be negative.");

        RuleFor(x => x.RepaymentDayOfMonth)
            .InclusiveBetween(1, 31)
            .WithMessage("Repayment day must be in range 1..31.");

        RuleFor(x => x.TagId)
            .GreaterThan(0)
            .When(x => x.TagId.HasValue)
            .WithMessage("Tag id must be greater than zero.");

        RuleFor(x => x)
            .Must(x => x.EndDate >= x.StartDate)
            .WithMessage("End date must be greater than or equal to start date.");

        RuleFor(x => x)
            .Must(LoanValidationRules.BeConsistentWithLoanTypeAndInterestMode)
            .WithMessage("Loan configuration is invalid for selected type and interest mode.");
    }

    protected override bool PreValidate(ValidationContext<CreateLoanRequest> context, ValidationResult result)
    {
        if (context.InstanceToValidate is not null)
        {
            context.InstanceToValidate.Name = (context.InstanceToValidate.Name ?? string.Empty).Trim();
        }

        return base.PreValidate(context, result);
    }
}

public sealed class UpdateLoanRequestValidator : AbstractValidator<UpdateLoanRequest>
{
    public UpdateLoanRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Name is required.")
            .MaximumLength(160).WithMessage("Name cannot exceed 160 characters.");

        RuleFor(x => x.LoanType)
            .Must(x => Enum.IsDefined(typeof(LoanType), x))
            .WithMessage("Loan type is invalid.");

        RuleFor(x => x.InterestMode)
            .Must(x => Enum.IsDefined(typeof(LoanInterestMode), x))
            .WithMessage("Loan interest mode is invalid.");

        RuleFor(x => x.Principal)
            .GreaterThan(0)
            .WithMessage("Principal must be greater than zero.");

        RuleFor(x => x.OriginalPrincipal)
            .GreaterThan(0)
            .When(x => x.OriginalPrincipal.HasValue)
            .WithMessage("Original principal must be greater than zero.");

        RuleFor(x => x.GracePeriodMonths)
            .GreaterThanOrEqualTo(0)
            .When(x => x.GracePeriodMonths.HasValue)
            .WithMessage("Grace period months cannot be negative.");

        RuleFor(x => x.InterestRate)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Interest rate cannot be negative.");

        RuleFor(x => x.MarginRate)
            .GreaterThanOrEqualTo(0)
            .When(x => x.MarginRate.HasValue)
            .WithMessage("Margin cannot be negative.");

        RuleFor(x => x.RepaymentDayOfMonth)
            .InclusiveBetween(1, 31)
            .WithMessage("Repayment day must be in range 1..31.");

        RuleFor(x => x.TagId)
            .GreaterThan(0)
            .When(x => x.TagId.HasValue)
            .WithMessage("Tag id must be greater than zero.");

        RuleFor(x => x)
            .Must(x => x.EndDate >= x.StartDate)
            .WithMessage("End date must be greater than or equal to start date.");

        RuleFor(x => x)
            .Must(LoanValidationRules.BeConsistentWithLoanTypeAndInterestMode)
            .WithMessage("Loan configuration is invalid for selected type and interest mode.");
    }

    protected override bool PreValidate(ValidationContext<UpdateLoanRequest> context, ValidationResult result)
    {
        if (context.InstanceToValidate is not null)
        {
            context.InstanceToValidate.Name = (context.InstanceToValidate.Name ?? string.Empty).Trim();
        }

        return base.PreValidate(context, result);
    }
}

public sealed class DeleteLoanRequestValidator : AbstractValidator<DeleteLoanRequest>
{
    public DeleteLoanRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public sealed class SetLoanInstallmentPaidRequestValidator : AbstractValidator<SetLoanInstallmentPaidRequest>
{
    public SetLoanInstallmentPaidRequestValidator()
    {
        RuleFor(x => x.LoanInstallmentId).GreaterThan(0);
    }
}

public sealed class AddLoanRateEntryRequestValidator : AbstractValidator<AddLoanRateEntryRequest>
{
    public AddLoanRateEntryRequestValidator()
    {
        RuleFor(x => x.LoanId).GreaterThan(0);
        RuleFor(x => x.ReferenceRate)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Reference rate cannot be negative.");
    }
}

public sealed class ApplyLoanPrepaymentRequestValidator : AbstractValidator<ApplyLoanPrepaymentRequest>
{
    public ApplyLoanPrepaymentRequestValidator()
    {
        RuleFor(x => x.LoanInstallmentId).GreaterThan(0);
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Prepayment amount must be greater than zero.");
        RuleFor(x => x.Strategy)
            .Must(x => Enum.IsDefined(typeof(LoanPrepaymentStrategyType), x))
            .WithMessage("Prepayment strategy is invalid.");
    }
}

public sealed class CreateLoanChargeRequestValidator : AbstractValidator<CreateLoanChargeRequest>
{
    public CreateLoanChargeRequestValidator()
    {
        RuleFor(x => x.LoanId).GreaterThan(0);
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Name is required.")
            .MaximumLength(160).WithMessage("Name cannot exceed 160 characters.");
        RuleFor(x => x.ChargeType)
            .Must(x => Enum.IsDefined(typeof(LoanChargeType), x))
            .WithMessage("Charge type is invalid.");
        RuleFor(x => x.FrequencyType)
            .Must(x => Enum.IsDefined(typeof(LoanChargeFrequencyType), x))
            .WithMessage("Frequency type is invalid.");
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than zero.");
        RuleFor(x => x)
            .Must(x => !x.EndDate.HasValue || x.EndDate.Value >= x.StartDate)
            .WithMessage("End date must be greater than or equal to start date.");
    }

    protected override bool PreValidate(ValidationContext<CreateLoanChargeRequest> context, ValidationResult result)
    {
        if (context.InstanceToValidate is not null)
        {
            context.InstanceToValidate.Name = (context.InstanceToValidate.Name ?? string.Empty).Trim();
        }

        return base.PreValidate(context, result);
    }
}

public sealed class UpdateLoanChargeRequestValidator : AbstractValidator<UpdateLoanChargeRequest>
{
    public UpdateLoanChargeRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Name is required.")
            .MaximumLength(160).WithMessage("Name cannot exceed 160 characters.");
        RuleFor(x => x.ChargeType)
            .Must(x => Enum.IsDefined(typeof(LoanChargeType), x))
            .WithMessage("Charge type is invalid.");
        RuleFor(x => x.FrequencyType)
            .Must(x => Enum.IsDefined(typeof(LoanChargeFrequencyType), x))
            .WithMessage("Frequency type is invalid.");
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than zero.");
        RuleFor(x => x)
            .Must(x => !x.EndDate.HasValue || x.EndDate.Value >= x.StartDate)
            .WithMessage("End date must be greater than or equal to start date.");
    }

    protected override bool PreValidate(ValidationContext<UpdateLoanChargeRequest> context, ValidationResult result)
    {
        if (context.InstanceToValidate is not null)
        {
            context.InstanceToValidate.Name = (context.InstanceToValidate.Name ?? string.Empty).Trim();
        }

        return base.PreValidate(context, result);
    }
}

public sealed class DeleteLoanChargeRequestValidator : AbstractValidator<DeleteLoanChargeRequest>
{
    public DeleteLoanChargeRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

internal static class LoanValidationRules
{
    public static bool BeConsistentWithLoanTypeAndInterestMode(CreateLoanRequest request)
    {
        return request.LoanType switch
        {
            LoanType.Cash => request.InterestMode == LoanInterestMode.Fixed
                             && !request.WiborPeriodType.HasValue
                             && !request.MarginRate.HasValue
                             && !request.InitialReferenceRate.HasValue,
            LoanType.Mortgage => (request.InterestMode == LoanInterestMode.VariableWibor || request.InterestMode == LoanInterestMode.Fixed)
                                 && request.WiborPeriodType.HasValue
                                 && Enum.IsDefined(typeof(WiborPeriodType), request.WiborPeriodType.Value)
                                 && request.MarginRate.HasValue
                                 && request.InitialReferenceRate.HasValue,
            _ => false
        };
    }

    public static bool BeConsistentWithLoanTypeAndInterestMode(UpdateLoanRequest request)
    {
        return request.LoanType switch
        {
            LoanType.Cash => request.InterestMode == LoanInterestMode.Fixed
                             && !request.WiborPeriodType.HasValue
                             && !request.MarginRate.HasValue
                             && !request.InitialReferenceRate.HasValue,
            LoanType.Mortgage => (request.InterestMode == LoanInterestMode.VariableWibor || request.InterestMode == LoanInterestMode.Fixed)
                                 && request.WiborPeriodType.HasValue
                                 && Enum.IsDefined(typeof(WiborPeriodType), request.WiborPeriodType.Value)
                                 && request.MarginRate.HasValue
                                 && request.InitialReferenceRate.HasValue,
            _ => false
        };
    }
}