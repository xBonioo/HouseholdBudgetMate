using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Application.Kernel.Timing;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HouseholdBudgetMate.Application.Helpers;

public sealed class CoreDataSeedService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IDateTimeProvider dateTimeProvider,
    ILogger<CoreDataSeedService> logger)
{
    private static readonly IReadOnlyList<CategorySeedDefinition> DefaultCategories =
    [
        new("Spożywcze", "#4CAF50", true),
        new("Samochód", "#1E88E5", true),
        new("Zdrowie", "#E53935", false),
        new("Rozrywka", "#8E24AA", true),
        new("Dom", "#FB8C00", false)
    ];

    private static readonly IReadOnlyList<TagSeedDefinition> DefaultTags =
    [
        new("Spożywcze", "Supermarket"),
        new("Spożywcze", "Warzywniak"),
        new("Spożywcze", "Pieczywo"),
        new("Samochód", "Paliwo"),
        new("Samochód", "Serwis"),
        new("Samochód", "Ubezpieczenie"),
        new("Zdrowie", "Leki"),
        new("Zdrowie", "Lekarz"),
        new("Rozrywka", "Kino"),
        new("Rozrywka", "Restauracja"),
        new("Dom", "Czynsz"),
        new("Dom", "Media")
    ];

    private static readonly IReadOnlyList<AccountSeedDefinition> DefaultAccounts =
    [
        new("Konto osobiste", AccountType.Bank, 1),
        new("Portfel", AccountType.Cash, 2),
        new("Oszczednosci", AccountType.Savings, 3)
    ];

    private static readonly IReadOnlyList<AccountBalanceSeedDefinition> DefaultAccountBalances =
    [
        new("Konto osobiste", 3500m),
        new("Portfel", 450m),
        new("Oszczednosci", 12000m)
    ];

    private static readonly IReadOnlyList<RegularIncomeSeedDefinition> DefaultRegularIncomes =
    [
        new("Wynagrodzenie", 7000m, 10, "Konto osobiste"),
        new("Dodatkowe zlecenia", 900m, 20, "Konto osobiste")
    ];

    public async Task SeedOnStartupAsync(bool seedDataToDatabase, CancellationToken cancellationToken)
    {
        if (seedDataToDatabase)
        {
            await SeedDefaultsAsync(cancellationToken);
        }

        await EnsureCurrentMonthPlanAsync(cancellationToken);
    }

    public async Task SeedDefaultsAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        await SeedDefaultCategoriesAsync(dbContext, cancellationToken);
        await SeedDefaultTagsAsync(dbContext, cancellationToken);
        await RepairInconsistentTagReferencesAsync(dbContext, cancellationToken);
        await SeedDefaultAccountsAsync(dbContext, cancellationToken);
        await SeedDefaultAccountMonthBalancesAsync(dbContext, cancellationToken);
        await SeedDefaultRegularIncomesAsync(dbContext, cancellationToken);
    }

    public async Task EnsureCurrentMonthPlanAsync(CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.GetLocalDateTime();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var exists = await dbContext.MonthPlans
            .AnyAsync(x => x.Year == now.Year && x.Month == now.Month, cancellationToken);

        if (exists)
        {
            return;
        }

        dbContext.MonthPlans.Add(new MonthPlan
        {
            Year = now.Year,
            Month = now.Month,
            IsClosed = false
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Current month plan seeded.");
    }

    private async Task SeedDefaultCategoriesAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var existingNames = await dbContext.Categories
            .IgnoreQueryFilters()
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);

        var existingNamesSet = existingNames
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var categories = DefaultCategories.Select(x => new Category
        {
            Name = x.Name,
            Color = x.Color,
            SupportsLineItems = x.SupportsLineItems,
            IsDeleted = false
        })
            .Where(x => !existingNamesSet.Contains(x.Name))
            .ToList();

        if (categories.Count == 0)
        {
            return;
        }

        await dbContext.Categories.AddRangeAsync(categories, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Default categories seeded.");
    }

    private async Task SeedDefaultTagsAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var categoriesByName = await dbContext.Categories
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);
        var categoriesByNameDictionary = categoriesByName
            .ToDictionary(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase);

        var existingTagKeys = (await dbContext.Tags
                .IgnoreQueryFilters()
                .Select(x => new { x.CategoryId, x.Name })
                .ToListAsync(cancellationToken))
            .Select(x => $"{x.CategoryId}:{x.Name}".ToUpperInvariant())
            .ToHashSet();

        var tagsToCreate = DefaultTags
            .Where(x => categoriesByNameDictionary.ContainsKey(x.CategoryName))
            .Select(x => new Tag
            {
                CategoryId = categoriesByNameDictionary[x.CategoryName],
                Name = x.TagName,
                IsDeleted = false
            })
            .Where(x => !existingTagKeys.Contains($"{x.CategoryId}:{x.Name}".ToUpperInvariant()))
            .ToList();

        if (tagsToCreate.Count == 0)
        {
            logger.LogWarning("Skipping tag seed because required categories are missing.");
            return;
        }

        await dbContext.Tags.AddRangeAsync(tagsToCreate, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Default tags seeded.");
    }

    private async Task SeedDefaultAccountsAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var existingAccountNames = await dbContext.Accounts
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);

        var existingAccountNamesSet = existingAccountNames
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var accounts = DefaultAccounts.Select(x => new Account
        {
            Name = x.Name,
            Type = (int)x.Type,
            Order = x.Order,
            IsArchived = false
        })
            .Where(x => !existingAccountNamesSet.Contains(x.Name))
            .ToList();

        if (accounts.Count == 0)
        {
            return;
        }

        await dbContext.Accounts.AddRangeAsync(accounts, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Default accounts seeded.");
    }

    private async Task SeedDefaultAccountMonthBalancesAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.GetLocalDateTime();

        var accountsByName = await dbContext.Accounts
            .ToListAsync(cancellationToken);
        var accountsByNameDictionary = accountsByName
            .ToDictionary(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase);

        var existingAccountIdsForCurrentMonth = await dbContext.AccountMonthBalances
            .Where(x => x.Year == now.Year && x.Month == now.Month)
            .Select(x => x.AccountId)
            .ToListAsync(cancellationToken);

        var existingAccountIdsSet = existingAccountIdsForCurrentMonth.ToHashSet();

        var balances = DefaultAccountBalances
            .Where(x => accountsByNameDictionary.ContainsKey(x.AccountName))
            .Select(x => new AccountMonthBalance
            {
                AccountId = accountsByNameDictionary[x.AccountName],
                Year = now.Year,
                Month = now.Month,
                ClosingBalance = x.Balance
            })
            .Where(x => !existingAccountIdsSet.Contains(x.AccountId))
            .ToList();

        if (balances.Count == 0)
        {
            logger.LogWarning("Skipping account balance seed because accounts are missing.");
            return;
        }

        await dbContext.AccountMonthBalances.AddRangeAsync(balances, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Default account balances seeded.");
    }

    private async Task SeedDefaultRegularIncomesAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var accountsByName = await dbContext.Accounts
            .ToListAsync(cancellationToken);
        var accountsByNameDictionary = accountsByName
            .ToDictionary(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase);

        var existingDefinitions = (await dbContext.RegularIncomeDefinitions
                .Select(x => new { x.Name, x.AccountId, x.DayOfMonth })
                .ToListAsync(cancellationToken))
            .Select(x => $"{x.AccountId}:{x.DayOfMonth}:{x.Name}".ToUpperInvariant())
            .ToHashSet();

        var definitions = DefaultRegularIncomes
            .Where(x => accountsByNameDictionary.ContainsKey(x.AccountName))
            .Select(x => new RegularIncomeDefinition
            {
                Name = x.Name,
                Amount = x.Amount,
                DayOfMonth = x.DayOfMonth,
                AccountId = accountsByNameDictionary[x.AccountName],
                IsActive = true
            })
            .Where(x => !existingDefinitions.Contains($"{x.AccountId}:{x.DayOfMonth}:{x.Name}".ToUpperInvariant()))
            .ToList();

        if (definitions.Count == 0)
        {
            logger.LogWarning("Skipping regular income seed because accounts are missing.");
            return;
        }

        await dbContext.RegularIncomeDefinitions.AddRangeAsync(definitions, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Default regular incomes seeded.");
    }

    private async Task RepairInconsistentTagReferencesAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var tagCategoryById = await dbContext.Tags
            .IgnoreQueryFilters()
            .ToDictionaryAsync(x => x.Id, x => x.CategoryId, cancellationToken);

        var expenseTagRefs = await dbContext.Expenses
            .Where(x => x.TagId.HasValue)
            .Select(x => new { x.Id, x.CategoryId, TagId = x.TagId!.Value })
            .ToListAsync(cancellationToken);

        var invalidExpenseIds = expenseTagRefs
            .Where(x => !tagCategoryById.TryGetValue(x.TagId, out var tagCategoryId) || tagCategoryId != x.CategoryId)
            .Select(x => x.Id)
            .ToList();

        var lineItemTagRefs = await dbContext.ExpenseLineItems
            .Where(x => x.TagId.HasValue)
            .Select(x => new { x.Id, ExpenseCategoryId = x.Expense.CategoryId, TagId = x.TagId!.Value })
            .ToListAsync(cancellationToken);

        var invalidLineItemIds = lineItemTagRefs
            .Where(x => !tagCategoryById.TryGetValue(x.TagId, out var tagCategoryId) || tagCategoryId != x.ExpenseCategoryId)
            .Select(x => x.Id)
            .ToList();

        if (invalidExpenseIds.Count == 0 && invalidLineItemIds.Count == 0)
        {
            return;
        }

        var expensesToFix = await dbContext.Expenses
            .Where(x => invalidExpenseIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var expense in expensesToFix)
        {
            expense.TagId = null;
        }

        var lineItemsToFix = await dbContext.ExpenseLineItems
            .Where(x => invalidLineItemIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var lineItem in lineItemsToFix)
        {
            lineItem.TagId = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Fixed inconsistent tag references. Expenses: {ExpensesCount}, LineItems: {LineItemsCount}", expensesToFix.Count, lineItemsToFix.Count);
    }

    private readonly record struct CategorySeedDefinition(string Name, string Color, bool SupportsLineItems);
    private readonly record struct TagSeedDefinition(string CategoryName, string TagName);
    private readonly record struct AccountSeedDefinition(string Name, AccountType Type, int Order);
    private readonly record struct AccountBalanceSeedDefinition(string AccountName, decimal Balance);
    private readonly record struct RegularIncomeSeedDefinition(string Name, decimal Amount, int DayOfMonth, string AccountName);
}