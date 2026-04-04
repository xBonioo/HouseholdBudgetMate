using System.Globalization;
using HouseholdBudgetMate.Abstractions.Contracts.Accounts.Dto;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Application.Helpers;
using HouseholdBudgetMate.Domain.Entities;

namespace HouseholdBudgetMate.Application.Mapping;

public static class AccountExtensionMapping
{
    public static AccountDto MapToDto(this Account account)
    {
        var orderedBalances = account.MonthBalances
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .ToList();

        var currentBalance = orderedBalances.FirstOrDefault()?.ClosingBalance ?? 0;

        return new AccountDto
        {
            Id = account.Id,
            Name = account.Name,
            Type = ParseType(account.Type),
            Order = account.Order,
            CurrentBalance = currentBalance,
            IsArchived = account.IsArchived,
            ArchivedAtUtc = account.ArchivedAtUtc,
            MonthBalances = orderedBalances.Select(x => new AccountMonthBalanceDto
            {
                Id = x.Id,
                AccountId = x.AccountId,
                Year = x.Year,
                Month = x.Month,
                MonthName = BudgetHelper.GetMonthName(x.Month),
                ClosingBalance = x.ClosingBalance
            }).ToList()
        };
    }

    private static AccountType ParseType(int value)
    {
        if (!Enum.IsDefined(typeof(AccountType), value))
        {
            return AccountType.Other;
        }

        return (AccountType)value;
    }
}