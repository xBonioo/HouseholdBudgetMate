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
        new("Zakupy", "#4CAF50", true),
        new("Samochód", "#1E88E5", true),
        new("Zdrowie", "#E53935", false),
        new("Rozrywka", "#8E24AA", true),
        new("Dom", "#FB8C00", false),
        new("Inne", "#0473ff", false),
        new("Pies", "#000080", false),
        new("Rozwój", "#ff0000", false),
        new("Hobby", "#DD99F0", false),
    ];

    private static readonly IReadOnlyList<TagSeedDefinition> DefaultTags =
    [
        new("Zakupy", "Spożywcze"),
        new("Zakupy", "Lidl", "Spożywcze"),
        new("Zakupy", "Auchan", "Spożywcze"),
        new("Zakupy", "Biedronka", "Spożywcze"),
        new("Zakupy", "Giełda", "Spożywcze"),
        new("Zakupy", "Żabka", "Spożywcze"),
        new("Zakupy", "Grabówka", "Spożywcze"),
        new("Zakupy", "Allegro", "Spożywcze"),

        new("Zakupy", "Kosmetyki"),
        new("Zakupy", "Ciuchy"),
        new("Zakupy", "Internetowe"),
        new("Zakupy", "Aliexpress"),
        new("Zakupy", "Art. gospodarstwa"),

        new("Rozwój", "Studia"),
        new("Rozwój", "Kurs"),

        new("Inne", "Subskrypcje"),
        new("Inne", "Doładowanie"),
        new("Inne", "Prezent"),

        new("Samochód", "Paliwo"),
        new("Samochód", "Orlen", "Samochód"),
        new("Samochód", "Plus", "Samochód"),
        new("Samochód", "Auchan", "Samochód"),
        
        new("Samochód", "Serwis"),
        new("Samochód", "Ubezpieczenie"),
        new("Samochód", "Mechanik"),
        new("Samochód", "Myjnia"),

        new("Zdrowie", "Suple"),
        new("Zdrowie", "Lekarz"),
        new("Zdrowie", "Inne"),
        
        new("Rozrywka", "Miasto"),
        new("Rozrywka", "Jedzenie na mieście"),
        new("Rozrywka", "Hobby"),

        new("Dom", "Kredyt"),
        new("Dom", "Budowa"),
        new("Dom", "Rachunki")
    ];

    private static readonly IReadOnlyList<AccountSeedDefinition> DefaultAccounts =
    [
        new("ING", AccountType.Bank, 1),
        new("ZEN", AccountType.Bank, 2),
        new("Portfel", AccountType.Cash, 3),
        new("Oszczędności", AccountType.Savings, 4)
    ];

    private static readonly IReadOnlyList<AccountBalanceSeedDefinition> DefaultAccountBalances =
    [
        new("ING", 350m),
        new("Portfel", 100m),
        new("Oszczędności", 12000m)
    ];

    private static readonly IReadOnlyList<RegularIncomeSeedDefinition> DefaultRegularIncomes =
    [
        new("Wynagrodzenie", 7000m, 7, "ING"),
        new("Zasiłek pielegnacyjny", 215.84m, 15, "ING")
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

        if (!dbContext.Database.CanConnect()) return;
        if (dbContext.Accounts.Any()) return;

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

        if (!await dbContext.Database.CanConnectAsync(cancellationToken)) return;

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

        var categories = DefaultCategories
            .Select(x => new Category
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

        var existingTags = await dbContext.Tags
                .IgnoreQueryFilters()
                .ToListAsync(cancellationToken);

        var existingTagsByCategoryAndName = existingTags
            .GroupBy(x => (x.CategoryId, x.Name), new CategoryTagNameKeyComparer())
            .ToDictionary(x => x.Key, x => x.First(), new CategoryTagNameKeyComparer());

        var rootTagsToCreate = DefaultTags
            .Where(x => x.ParentTagName is null)
            .Where(x => categoriesByNameDictionary.ContainsKey(x.CategoryName))
            .Select(x => new Tag
            {
                CategoryId = categoriesByNameDictionary[x.CategoryName],
                Name = x.TagName,
                IsDeleted = false
            })
            .Where(x => !existingTagsByCategoryAndName.ContainsKey((x.CategoryId, x.Name)))
            .ToList();

        if (rootTagsToCreate.Count > 0)
        {
            await dbContext.Tags.AddRangeAsync(rootTagsToCreate, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            existingTags = await dbContext.Tags
                .IgnoreQueryFilters()
                .ToListAsync(cancellationToken);

            existingTagsByCategoryAndName = existingTags
                .GroupBy(x => (x.CategoryId, x.Name), new CategoryTagNameKeyComparer())
                .ToDictionary(x => x.Key, x => x.First(), new CategoryTagNameKeyComparer());
        }

        var childTagsToCreate = new List<Tag>();
        var childTagsToBackfillParent = new List<Tag>();

        foreach (var seed in DefaultTags.Where(x => x.ParentTagName is not null))
        {
            if (!categoriesByNameDictionary.TryGetValue(seed.CategoryName, out var categoryId))
            {
                continue;
            }

            if (!existingTagsByCategoryAndName.TryGetValue((categoryId, seed.ParentTagName!), out var parentTag))
            {
                logger.LogWarning(
                    "Skipping child tag seed because parent tag is missing. Category: {CategoryName}, Parent: {ParentTagName}, Child: {ChildTagName}",
                    seed.CategoryName, seed.ParentTagName, seed.TagName);
                continue;
            }

            if (existingTagsByCategoryAndName.TryGetValue((categoryId, seed.TagName), out var existingTag))
            {
                if (!existingTag.ParentTagId.HasValue && existingTag.Id != parentTag.Id)
                {
                    existingTag.ParentTagId = parentTag.Id;
                    childTagsToBackfillParent.Add(existingTag);
                }

                continue;
            }

            var childTag = new Tag
            {
                CategoryId = categoryId,
                Name = seed.TagName,
                ParentTagId = parentTag.Id,
                IsDeleted = false
            };

            childTagsToCreate.Add(childTag);
            existingTagsByCategoryAndName[(categoryId, seed.TagName)] = childTag;
        }

        if (childTagsToCreate.Count == 0 && childTagsToBackfillParent.Count == 0 && rootTagsToCreate.Count == 0)
        {
            logger.LogWarning("Skipping tag seed because required categories are missing.");
            return;
        }

        if (childTagsToCreate.Count > 0)
        {
            await dbContext.Tags.AddRangeAsync(childTagsToCreate, cancellationToken);
        }

        if (childTagsToCreate.Count > 0 || childTagsToBackfillParent.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Default tags seeded. Roots: {RootCount}, Children: {ChildCount}, ParentBackfilled: {BackfillCount}",
            rootTagsToCreate.Count,
            childTagsToCreate.Count,
            childTagsToBackfillParent.Count);
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

    private async Task SeedDefaultAccountMonthBalancesAsync(ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.GetLocalDateTime();
        var previousMonth = now.AddMonths(-1);

        var accountsByName = await dbContext.Accounts
            .ToListAsync(cancellationToken);
        var accountsByNameDictionary = accountsByName
            .ToDictionary(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase);

        var candidates = DefaultAccountBalances
            .Where(x => accountsByNameDictionary.ContainsKey(x.AccountName))
            .Select(x => new AccountMonthBalance
            {
                AccountId = accountsByNameDictionary[x.AccountName],
                Year = now.Year,
                Month = now.Month,
                ClosingBalance = x.Balance
            })
            .ToList();

        if (accountsByNameDictionary.TryGetValue("ING", out var ingAccountId))
        {
            candidates.Add(new AccountMonthBalance
            {
                AccountId = ingAccountId,
                Year = now.Year-1,
                Month = 12,
                ClosingBalance = 1000m
            });
                
            for (int i = 1; i <= 12; i++)
            {
                candidates.Add(new AccountMonthBalance
                {
                    AccountId = ingAccountId,
                    Year = now.Year,
                    Month = i,
                    ClosingBalance = 1000m
                });
                
                if (i == previousMonth.Month) 
                    break;
            }
        }

        if (candidates.Count == 0)
        {
            logger.LogWarning("Skipping account balance seed because accounts are missing.");
            return;
        }

        var targetAccountIds = candidates
            .Select(x => x.AccountId)
            .Distinct()
            .ToList();

        var targetPeriods = candidates
            .Select(x => new { x.Year, x.Month })
            .Distinct()
            .ToList();

        var targetYears = targetPeriods
            .Select(x => x.Year)
            .Distinct()
            .ToList();

        var existingKeys = await dbContext.AccountMonthBalances
            .Where(x => targetAccountIds.Contains(x.AccountId))
            .Where(x => targetYears.Contains(x.Year))
            .Select(x => new { x.AccountId, x.Year, x.Month })
            .ToListAsync(cancellationToken);

        var targetPeriodsSet = targetPeriods
            .Select(x => (x.Year, x.Month))
            .ToHashSet();

        var existingKeysSet = existingKeys
            .Where(x => targetPeriodsSet.Contains((x.Year, x.Month)))
            .Select(x => (x.AccountId, x.Year, x.Month))
            .ToHashSet();

        var balances = candidates
            .Where(x => !existingKeysSet.Contains((x.AccountId, x.Year, x.Month)))
            .ToList();

        if (balances.Count == 0)
        {
            return;
        }

        await dbContext.AccountMonthBalances.AddRangeAsync(balances, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Default account balances seeded.");
    }

    private async Task SeedDefaultRegularIncomesAsync(ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
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

    private async Task RepairInconsistentTagReferencesAsync(ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
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
            .Where(x => !tagCategoryById.TryGetValue(x.TagId, out var tagCategoryId) ||
                        tagCategoryId != x.ExpenseCategoryId)
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
        logger.LogInformation(
            "Fixed inconsistent tag references. Expenses: {ExpensesCount}, LineItems: {LineItemsCount}",
            expensesToFix.Count, lineItemsToFix.Count);
    }

    private readonly record struct CategorySeedDefinition(string Name, string Color, bool SupportsLineItems);

    private readonly record struct TagSeedDefinition(string CategoryName, string TagName, string? ParentTagName = null);

    private sealed class CategoryTagNameKeyComparer : IEqualityComparer<(int CategoryId, string Name)>
    {
        public bool Equals((int CategoryId, string Name) x, (int CategoryId, string Name) y)
            => x.CategoryId == y.CategoryId && string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((int CategoryId, string Name) obj)
            => HashCode.Combine(obj.CategoryId, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name));
    }

    private readonly record struct AccountSeedDefinition(string Name, AccountType Type, int Order);

    private readonly record struct AccountBalanceSeedDefinition(string AccountName, decimal Balance);

    private readonly record struct RegularIncomeSeedDefinition(
        string Name,
        decimal Amount,
        int DayOfMonth,
        string AccountName);
}