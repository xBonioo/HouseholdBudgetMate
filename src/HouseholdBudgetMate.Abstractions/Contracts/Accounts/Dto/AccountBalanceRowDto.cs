using HouseholdBudgetMate.Abstractions.Enums;

namespace HouseholdBudgetMate.Abstractions.Contracts.Accounts.Dto;

public sealed record AccountBalanceRowDto(
    int Id,
    string Name,
    AccountType Type,
    string TypeLabel,
    int Order,
    decimal Amount,
    bool IsArchived,
    bool HasRecordedBalance);
