namespace HouseholdBudgetMate.Abstractions.Contracts.Accounts.Dto;

public sealed record SavingsTransferSummaryDto(
    decimal MonthlyTransfers = 0,
    int TransferCount = 0);
