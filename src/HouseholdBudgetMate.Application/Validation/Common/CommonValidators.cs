using FluentValidation;

namespace HouseholdBudgetMate.Application.Validation.Common;

public sealed class YearMonthRequestValidator : AbstractValidator<YearMonthRequest>
{
    public YearMonthRequestValidator()
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 3000)
            .WithMessage("Year is out of allowed range.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12)
            .WithMessage("Month must be in range 1..12.");
    }
}

public sealed class DateInMonthRequestValidator : AbstractValidator<DateInMonthRequest>
{
    public DateInMonthRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.Date.Year == x.Year && x.Date.Month == x.Month)
            .WithMessage(x => x.ErrorMessage);
    }
}