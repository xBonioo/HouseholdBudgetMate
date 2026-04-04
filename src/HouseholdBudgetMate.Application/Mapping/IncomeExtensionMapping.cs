using HouseholdBudgetMate.Abstractions.Contracts.Incomes.Dto;
using HouseholdBudgetMate.Domain.Entities;

namespace HouseholdBudgetMate.Application.Mapping;

public static class IncomeExtensionMapping
{
    public static IncomeDto MapToDto(this Income income)
    {
        return new IncomeDto
        {
            Id = income.Id,
            Year = income.Year,
            Month = income.Month,
            Name = income.Name,
            Amount = income.Amount,
            ExpectedDayOfMonth = income.ExpectedDayOfMonth,
            AccountId = income.AccountId,
            AccountName = income.Account.Name,
            IsRegular = income.IsRegular
        };
    }

    public static RegularIncomeDefinitionDto MapDefinitionToDto(this RegularIncomeDefinition definition)
    {
        return new RegularIncomeDefinitionDto
        {
            Id = definition.Id,
            Name = definition.Name,
            Amount = definition.Amount,
            DayOfMonth = definition.DayOfMonth,
            AccountId = definition.AccountId,
            AccountName = definition.Account.Name,
            IsActive = definition.IsActive
        };
    }
}