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
using HouseholdBudgetMate.Application.Helpers;
using HouseholdBudgetMate.Application.Mapping;

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

        return accounts.Select(x => x.MapToDto()).ToList();
    }

    public async Task<AccountDto> CreateAccountAsync(CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var normalizedName = BudgetHelper.NormalizeField(nameof(request.Name), request.Name);
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
            ClosingBalance = request.ClosingBalance
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        await dbContext.Entry(account).Collection(x => x.MonthBalances).LoadAsync(cancellationToken);

        return account.MapToDto();
    }

    public async Task<AccountDto> UpdateAccountAsync(UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        var normalizedName = BudgetHelper.NormalizeField(nameof(request.Name), request.Name);
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

        return account.MapToDto();
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

    public async Task<AccountMonthBalanceDto> UpsertMonthBalanceAsync(UpsertAccountMonthBalanceRequest request,
        CancellationToken cancellationToken)
    {
        BudgetHelper.ValidateMonth(request.Year, request.Month);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var accountExists = await dbContext.Accounts.AnyAsync(x => x.Id == request.AccountId, cancellationToken);
        if (!accountExists)
        {
            throw new NotFoundException("Account not found.");
        }

        var balance = await dbContext.AccountMonthBalances
            .FirstOrDefaultAsync(
                x => x.AccountId == request.AccountId && x.Year == request.Year && x.Month == request.Month,
                cancellationToken);

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
            MonthName = BudgetHelper.GetMonthName(balance.Month),
            ClosingBalance = balance.ClosingBalance
        };
    }

    public async Task<AccountMonthBalanceDto> UpdateMonthBalanceAmountAsync(
        UpdateAccountMonthBalanceAmountRequest request, CancellationToken cancellationToken)
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
            MonthName = BudgetHelper.GetMonthName(balance.Month),
            ClosingBalance = balance.ClosingBalance
        };
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

    private static void ValidateType(AccountType type)
    {
        if (!Enum.IsDefined(typeof(AccountType), type))
        {
            throw new BadRequestException("Account type is invalid.");
        }
    }
}