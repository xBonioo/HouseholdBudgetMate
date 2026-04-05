using FluentValidation;
using HouseholdBudgetMate.Application.Kernel.Exceptions;

namespace HouseholdBudgetMate.Application.Validation;

public static class ValidationExtensions
{
    public static void ValidateOrThrowBadRequest<T>(this IValidator<T> validator, T instance)
    {
        var result = validator.Validate(instance);
        if (result.IsValid)
        {
            return;
        }

        var message = string.Join(" ", result.Errors
            .Select(x => x.ErrorMessage)
            .Distinct());

        throw new BadRequestException(message);
    }
}