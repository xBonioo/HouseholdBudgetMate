using HouseholdBudgetMate.Abstractions.Contracts.Accounts.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Accounts.Requests;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Application.Kernel.Timing;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace HouseholdBudgetMate.Application.Services;

public sealed class AccountService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IDateTimeProvider dateTimeProvider) : IAccountService
{
    public async Task<IReadOnlyList<AccountDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var accounts = await dbContext.Accounts
            .AsNoTracking()
            .Include(x => x.MonthBalances)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return accounts.Select(MapToDto).ToList();
    }

    public async Task<AccountDto> CreateAccountAsync(CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var normalizedName = NormalizeName(request.Name);
        ValidateType(request.Type);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        await EnsureNameUniqueAsync(dbContext, normalizedName, null, cancellationToken);

        var account = new Account
        {
            Order = await dbContext.Accounts.AnyAsync(cancellationToken)
                ? await dbContext.Accounts.MaxAsync(x => x.Order, cancellationToken) + 1
                : 1,
            Name = request.Name.Trim(),
            Type = (int)request.Type
        };

        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);

        var now = dateTimeProvider.GetLocalDateTime();
        dbContext.AccountMonthBalances.Add(new AccountMonthBalance
        {
            AccountId = account.Id,
            Year = now.Year,
            Month = now.Month,
            ClosingBalance = request.OpeningBalance
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        await dbContext.Entry(account).Collection(x => x.MonthBalances).LoadAsync(cancellationToken);

        return MapToDto(account);
    }

    public async Task<AccountDto> UpdateAccountAsync(UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        var normalizedName = NormalizeName(request.Name);
        ValidateType(request.Type);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var account = await dbContext.Accounts
            .Include(x => x.MonthBalances)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Account not found.");

        await EnsureNameUniqueAsync(dbContext, normalizedName, request.Id, cancellationToken);

        account.Name = request.Name.Trim();
        account.Type = (int)request.Type;

        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(account);
    }

    public async Task DeleteAccountAsync(DeleteAccountRequest request, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var account = await dbContext.Accounts
            .Include(x => x.MonthBalances)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Account not found.");

        dbContext.AccountMonthBalances.RemoveRange(account.MonthBalances);
        dbContext.Accounts.Remove(account);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetAccountArchivedAsync(SetAccountArchivedRequest request, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var account = await dbContext.Accounts
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Account not found.");

        account.IsArchived = request.IsArchived;
        account.ArchivedAtUtc = request.IsArchived ? dateTimeProvider.GetUtcDateTime() : null;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderAccountsAsync(ReorderAccountsRequest request, CancellationToken cancellationToken)
    {
        if (request.AccountIds.Count == 0)
        {
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var accounts = await dbContext.Accounts
            .Where(x => request.AccountIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (accounts.Count != request.AccountIds.Count)
        {
            throw new BadRequestException("Some accounts were not found.");
        }

        for (var i = 0; i < request.AccountIds.Count; i++)
        {
            var account = accounts.First(x => x.Id == request.AccountIds[i]);
            account.Order = i + 1;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AccountMonthBalanceDto> UpsertMonthBalanceAsync(UpsertAccountMonthBalanceRequest request, CancellationToken cancellationToken)
    {
        ValidateMonth(request.Year, request.Month);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var accountExists = await dbContext.Accounts.AnyAsync(x => x.Id == request.AccountId, cancellationToken);
        if (!accountExists)
        {
            throw new NotFoundException("Account not found.");
        }

        var balance = await dbContext.AccountMonthBalances
            .FirstOrDefaultAsync(x => x.AccountId == request.AccountId && x.Year == request.Year && x.Month == request.Month, cancellationToken);

        if (balance is null)
        {
            balance = new AccountMonthBalance
            {
                AccountId = request.AccountId,
                Year = request.Year,
                Month = request.Month,
                ClosingBalance = request.ClosingBalance
            };
            dbContext.AccountMonthBalances.Add(balance);
        }
        else
        {
            balance.ClosingBalance = request.ClosingBalance;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AccountMonthBalanceDto
        {
            Id = balance.Id,
            AccountId = balance.AccountId,
            Year = balance.Year,
            Month = balance.Month,
            MonthName = GetMonthName(balance.Month),
            ClosingBalance = balance.ClosingBalance
        };
    }

    public async Task<AccountMonthBalanceDto> UpdateMonthBalanceAmountAsync(UpdateAccountMonthBalanceAmountRequest request, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var balance = await dbContext.AccountMonthBalances
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Month balance not found.");

        balance.ClosingBalance = request.ClosingBalance;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AccountMonthBalanceDto
        {
            Id = balance.Id,
            AccountId = balance.AccountId,
            Year = balance.Year,
            Month = balance.Month,
            MonthName = GetMonthName(balance.Month),
            ClosingBalance = balance.ClosingBalance
        };
    }

    public async Task DeleteMonthBalanceAsync(DeleteAccountMonthBalanceRequest request, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var balance = await dbContext.AccountMonthBalances
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Month balance not found.");

        dbContext.AccountMonthBalances.Remove(balance);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AccountDto MapToDto(Account account)
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
                MonthName = GetMonthName(x.Month),
                ClosingBalance = x.ClosingBalance
            }).ToList()
        };
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BadRequestException("Account name is required.");
        }

        return value.Trim().ToUpperInvariant();
    }

    private static AccountType ParseType(int value)
    {
        if (!Enum.IsDefined(typeof(AccountType), value))
        {
            return AccountType.Other;
        }

        return (AccountType)value;
    }

    private static void ValidateType(AccountType type)
    {
        if (!Enum.IsDefined(typeof(AccountType), type))
        {
            throw new BadRequestException("Account type is invalid.");
        }
    }

    private static void ValidateMonth(int year, int month)
    {
        if (year is < 2000 or > 3000)
        {
            throw new BadRequestException("Year is out of allowed range.");
        }

        if (month is < 1 or > 12)
        {
            throw new BadRequestException("Month must be in range 1..12.");
        }
    }

    private static async Task EnsureNameUniqueAsync(
        ApplicationDbContext dbContext,
        string normalizedName,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.Accounts
            .AnyAsync(x => (!excludeId.HasValue || x.Id != excludeId.Value)
                           && x.Name.ToUpper() == normalizedName,
                cancellationToken);

        if (exists)
        {
            throw new ConflictException("Account name must be unique.");
        }
    }

    private static string GetMonthName(int month)
    {
        return new DateTime(2000, month, 1).ToString("MMMM", new CultureInfo("pl-PL"));
    }
}

