using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;
using HouseholdBudgetMate.Abstractions.Contracts.Facility.Events;
using HouseholdBudgetMate.Abstractions.Contracts.Incomes.Requests;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Application.Services;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Domain.Infrastructure;
using HouseholdBudgetMate.Migrations;
using HouseholdBudgetMate.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class ExpenseServiceTests
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    private ExpenseService CreateService(RecordingAppEventPublisher? eventPublisher = null, DateTime? nowUtc = null)
    {
        var factory = TestDbContextFactory.CreateFactory(_dbName);
        var provider = new StaticDateTimeProvider(nowUtc ?? DateTime.UtcNow);
        return new ExpenseService(
            factory,
            provider,
            eventPublisher ?? new RecordingAppEventPublisher(),
            new NoOpIncomeService(),
            new NoOpLoanService());
    }

    private ExpenseService CreateService(CurrentUserContext currentUserContext, RecordingAppEventPublisher? eventPublisher = null, DateTime? nowUtc = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
        var factory = new CurrentUserDbContextFactory(options, currentUserContext);
        var provider = new StaticDateTimeProvider(nowUtc ?? DateTime.UtcNow);

        return new ExpenseService(
            factory,
            provider,
            eventPublisher ?? new RecordingAppEventPublisher(),
            new NoOpIncomeService(),
            new NoOpLoanService());
    }

    private async Task<int> CreateCategoryAsync(string name, bool supportsLineItems = false)
    {
        await using var context = TestDbContextFactory.CreateDbContext(_dbName);

        var category = new Category
        {
            Name = name,
            Color = "#6D4C41",
            SupportsLineItems = supportsLineItems
        };

        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category.Id;
    }

    private async Task<int> CreateMonthPlanAsync(int year, int month, bool isClosed = false)
    {
        await using var context = TestDbContextFactory.CreateDbContext(_dbName);

        var monthPlan = new MonthPlan
        {
            Year = year,
            Month = month,
            IsClosed = isClosed
        };

        context.MonthPlans.Add(monthPlan);
        await context.SaveChangesAsync();
        return monthPlan.Id;
    }

    private async Task<int> CreateRegularExpenseDefinitionAsync(
        int categoryId,
        string name,
        decimal amount,
        bool showRemainingInUi = true)
    {
        await using var context = TestDbContextFactory.CreateDbContext(_dbName);

        var definition = new RegularExpenseDefinition
        {
            Name = name,
            CategoryId = categoryId,
            Amount = amount,
            ShowRemainingInUI = showRemainingInUi,
            IsActive = true
        };

        context.RegularExpenseDefinitions.Add(definition);
        await context.SaveChangesAsync();
        return definition.Id;
    }

    private sealed class CurrentUserDbContextFactory(
        DbContextOptions<ApplicationDbContext> options,
        CurrentUserContext currentUserContext) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options, currentUserContext);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }

    /// <summary>
    /// Creates two expenses for a category with EnvelopeLimit=500 whose combined actual totals 510.
    /// Verifies that exactly one BudgetExceededEvent is published with the correct CategoryId, SpentAmount, and EnvelopeLimit.
    /// </summary>
    [Fact]
    public async Task CreateExpenseAsync_Should_Emit_BudgetExceededEvent_When_Category_Limit_Is_Crossed()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category
            {
                Name = "Spozywcze",
                Color = "#43A047",
                EnvelopeLimit = 500m
            };

            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var publisher = new RecordingAppEventPublisher();
        var service = CreateService(publisher);

        await service.CreateExpenseAsync(new CreateExpenseRequest
        {
            Year = 2026,
            Month = 4,
            Name = "Zakupy 1",
            CategoryId = categoryId,
            PlannedAmount = 300m,
            ActualAmount = 300m,
            ShowRemainingInUI = true
        }, CancellationToken.None);

        await service.CreateExpenseAsync(new CreateExpenseRequest
        {
            Year = 2026,
            Month = 4,
            Name = "Zakupy 2",
            CategoryId = categoryId,
            PlannedAmount = 210m,
            ActualAmount = 210m,
            ShowRemainingInUI = true
        }, CancellationToken.None);

        var budgetEvents = publisher.Events.OfType<BudgetExceededEvent>().ToList();

        Assert.Single(budgetEvents);
        Assert.Equal(categoryId, budgetEvents[0].CategoryId);
        Assert.Equal(510m, budgetEvents[0].SpentAmount);
        Assert.Equal(500m, budgetEvents[0].EnvelopeLimit);
    }

    /// <summary>
    /// Verifies that manually created expenses cannot carry negative planned or actual amounts.
    /// </summary>
    [Fact]
    public async Task CreateExpenseAsync_Should_Reject_Negative_Amounts()
    {
        var categoryId = await CreateCategoryAsync("Zakupy");
        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateExpenseAsync(new CreateExpenseRequest
        {
            Year = 2026,
            Month = 4,
            Name = "Ujemny plan",
            CategoryId = categoryId,
            PlannedAmount = -1m,
            ActualAmount = 0m
        }, CancellationToken.None));

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateExpenseAsync(new CreateExpenseRequest
        {
            Year = 2026,
            Month = 4,
            Name = "Ujemny faktyczny",
            CategoryId = categoryId,
            PlannedAmount = 0m,
            ActualAmount = -1m
        }, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that edited expenses cannot be saved with negative planned or actual amounts.
    /// </summary>
    [Fact]
    public async Task UpdateExpenseAsync_Should_Reject_Negative_Amounts()
    {
        var categoryId = await CreateCategoryAsync("Zakupy");
        var service = CreateService();
        var created = await service.CreateExpenseAsync(new CreateExpenseRequest
        {
            Year = 2026,
            Month = 4,
            Name = "Zakupy",
            CategoryId = categoryId,
            PlannedAmount = 100m,
            ActualAmount = 50m,
            ShowRemainingInUI = true
        }, CancellationToken.None);

        await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateExpenseAsync(new UpdateExpenseRequest
        {
            Id = created.Id,
            Name = "Ujemny plan",
            CategoryId = categoryId,
            PlannedAmount = -1m,
            ActualAmount = 50m,
            ShowRemainingInUI = true
        }, CancellationToken.None));

        await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateExpenseAsync(new UpdateExpenseRequest
        {
            Id = created.Id,
            Name = "Ujemny faktyczny",
            CategoryId = categoryId,
            PlannedAmount = 100m,
            ActualAmount = -1m,
            ShowRemainingInUI = true
        }, CancellationToken.None));
    }

    /// <summary>
    /// Calls GetMonthAsync for a year/month with no existing MonthPlan.
    /// Verifies that a single MonthPlan row is created in the database with the correct Year and Month.
    /// </summary>
    [Fact]
    public async Task GetMonthAsync_Should_Create_MonthPlan_When_Missing()
    {
        var service = CreateService();

        var result = await service.GetMonthAsync(2026, 4, CancellationToken.None);

        Assert.Equal(2026, result.Year);
        Assert.Equal(4, result.Month);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var monthPlans = await verifyContext.MonthPlans.ToListAsync();
        Assert.Single(monthPlans);
    }

    /// <summary>
    /// Seeds two line items with the same OccurredAt date and verifies that GetMonthAsync returns them
    /// ordered by Id ascending when the date is the same.
    /// </summary>
    [Fact]
    public async Task GetMonthAsync_Should_Order_LineItems_By_Day_Then_By_Id_When_Same_Day()
    {
        int firstLineItemId;
        int secondLineItemId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category
            {
                Name = "Zakupy",
                Color = "#43A047",
                SupportsLineItems = true
            };

            var monthPlan = new MonthPlan
            {
                Year = 2026,
                Month = 4
            };

            context.Categories.Add(category);
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            var expense = new Expense
            {
                MonthPlanId = monthPlan.Id,
                Name = "Paragon",
                CategoryId = category.Id,
                PlannedAmount = 100m,
                ActualAmount = 0m,
                ShowRemainingInUI = true
            };

            context.Expenses.Add(expense);
            await context.SaveChangesAsync();

            var firstLineItem = new ExpenseLineItem
            {
                ExpenseId = expense.Id,
                Description = "Pozycja 1",
                Amount = 10m,
                OccurredAt = new DateOnly(2026, 4, 10)
            };

            var secondLineItem = new ExpenseLineItem
            {
                ExpenseId = expense.Id,
                Description = "Pozycja 2",
                Amount = 20m,
                OccurredAt = new DateOnly(2026, 4, 10)
            };

            context.ExpenseLineItems.Add(firstLineItem);
            context.ExpenseLineItems.Add(secondLineItem);
            await context.SaveChangesAsync();

            firstLineItemId = firstLineItem.Id;
            secondLineItemId = secondLineItem.Id;
        }

        var service = CreateService();
        var month = await service.GetMonthAsync(2026, 4, CancellationToken.None);

        var expenseDto = Assert.Single(month.Expenses);
        Assert.Equal(2, expenseDto.LineItems.Count);
        Assert.Equal(30m, expenseDto.ActualAmount);
        Assert.Equal(firstLineItemId, expenseDto.LineItems[0].Id);
        Assert.Equal(secondLineItemId, expenseDto.LineItems[1].Id);
    }

    /// <summary>
    /// Seeds an expense whose persisted ActualAmount differs from the line-item sum and verifies
    /// that the month projection reports the effective actual amount without rewriting storage.
    /// </summary>
    [Fact]
    public async Task GetMonthAsync_Should_Use_Effective_Actual_Amount_When_LineItems_Exist()
    {
        int expenseId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category
            {
                Name = "Zakupy",
                Color = "#43A047",
                SupportsLineItems = true
            };
            var monthPlan = new MonthPlan
            {
                Year = 2026,
                Month = 4
            };

            context.Categories.Add(category);
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            var expense = new Expense
            {
                MonthPlanId = monthPlan.Id,
                Name = "Paragon",
                CategoryId = category.Id,
                PlannedAmount = 100m,
                ActualAmount = 999m,
                ShowRemainingInUI = true
            };

            expense.LineItems.Add(new ExpenseLineItem
            {
                Description = "Pozycja 1",
                Amount = 40m,
                OccurredAt = new DateOnly(2026, 4, 10)
            });
            expense.LineItems.Add(new ExpenseLineItem
            {
                Description = "Pozycja 2",
                Amount = 30m,
                OccurredAt = new DateOnly(2026, 4, 11)
            });

            context.Expenses.Add(expense);
            await context.SaveChangesAsync();
            expenseId = expense.Id;
        }

        var service = CreateService();
        var month = await service.GetMonthAsync(2026, 4, CancellationToken.None);

        var expenseDto = Assert.Single(month.Expenses);
        Assert.Equal(70m, expenseDto.ActualAmount);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var storedExpense = await verifyContext.Expenses.AsNoTracking().SingleAsync(x => x.Id == expenseId);
        Assert.Equal(999m, storedExpense.ActualAmount);
    }

    /// <summary>
    /// Seeds expenses with a root and child tag, plus a line item with the child tag.
    /// Verifies that GetTagUsageCountsAsync returns count=1 for the root tag and count=2 for the child tag.
    /// </summary>
    [Fact]
    public async Task GetTagUsageCountsAsync_Should_Aggregate_Expense_And_LineItem_Tag_Usage()
    {
        int rootTagId;
        int childTagId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category
            {
                Name = "Zakupy",
                Color = "#43A047",
                SupportsLineItems = true
            };

            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var rootTag = new Tag { Name = "Sklep", CategoryId = category.Id };
            context.Tags.Add(rootTag);
            await context.SaveChangesAsync();

            var childTag = new Tag { Name = "Online", CategoryId = category.Id, ParentTagId = rootTag.Id };
            context.Tags.Add(childTag);
            await context.SaveChangesAsync();

            rootTagId = rootTag.Id;
            childTagId = childTag.Id;

            var monthPlan = new MonthPlan { Year = 2026, Month = 4 };
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            var expenseWithRootTag = new Expense
            {
                MonthPlanId = monthPlan.Id,
                Name = "Zakupy stacjonarne",
                CategoryId = category.Id,
                TagId = rootTagId,
                PlannedAmount = 100m,
                ActualAmount = 90m,
                ShowRemainingInUI = true
            };

            var expenseWithChildTag = new Expense
            {
                MonthPlanId = monthPlan.Id,
                Name = "Zakupy online",
                CategoryId = category.Id,
                TagId = childTagId,
                PlannedAmount = 150m,
                ActualAmount = 120m,
                ShowRemainingInUI = true
            };

            context.Expenses.AddRange(expenseWithRootTag, expenseWithChildTag);
            await context.SaveChangesAsync();

            context.ExpenseLineItems.Add(new ExpenseLineItem
            {
                ExpenseId = expenseWithRootTag.Id,
                Description = "Pozycja online",
                Amount = 10m,
                OccurredAt = new DateOnly(2026, 4, 10),
                TagId = childTagId
            });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var usageCounts = await service.GetTagUsageCountsAsync(CancellationToken.None);

        var usageByTagId = usageCounts.ToDictionary(x => x.TagId, x => x.UsageCount);
        Assert.Equal(1, usageByTagId[rootTagId]);
        Assert.Equal(2, usageByTagId[childTagId]);
    }

    /// <summary>
    /// Calls DeleteExpenseAsync on an existing expense and verifies that IsDeleted=true and
    /// DeletedAtUtc is set (soft-delete via query filter).
    /// </summary>
    [Fact]
    public async Task DeleteExpenseAsync_Should_SoftDelete_Expense()
    {
        int expenseId;
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Transport", Color = "#1E88E5" };
            var monthPlan = new MonthPlan { Year = 2026, Month = 4 };
            context.Categories.Add(category);
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            var expense = new Expense
            {
                MonthPlanId = monthPlan.Id,
                Name = "Paliwo",
                CategoryId = category.Id,
                PlannedAmount = 300,
                ActualAmount = 250
            };

            context.Expenses.Add(expense);
            await context.SaveChangesAsync();
            expenseId = expense.Id;
        }

        var service = CreateService();
        await service.DeleteExpenseAsync(new DeleteExpenseRequest { Id = expenseId }, CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var deleted = await verifyContext.Expenses.IgnoreQueryFilters().SingleAsync(x => x.Id == expenseId);
        Assert.True(deleted.IsDeleted);
        Assert.NotNull(deleted.DeletedAtUtc);
    }

    /// <summary>
    /// Creates an expense with a child tag ID and verifies that the TagId is persisted as the child tag
    /// rather than being cleared or replaced by the parent tag.
    /// </summary>
    [Fact]
    public async Task CreateExpenseAsync_Should_Keep_Selected_SubTagId()
    {
        int categoryId;
        int childTagId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Zakupy", Color = "#6D4C41" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var rootTag = new Tag { Name = "Internetowe", CategoryId = category.Id };
            context.Tags.Add(rootTag);
            await context.SaveChangesAsync();

            var childTag = new Tag { Name = "Aliexpress", CategoryId = category.Id, ParentTagId = rootTag.Id };
            context.Tags.Add(childTag);
            await context.SaveChangesAsync();

            categoryId = category.Id;
            childTagId = childTag.Id;
        }

        var service = CreateService();
        var created = await service.CreateExpenseAsync(new CreateExpenseRequest
        {
            Year = 2026,
            Month = 4,
            Name = "Zakupy online",
            CategoryId = categoryId,
            TagId = childTagId,
            PlannedAmount = 100m,
            ActualAmount = 0m,
            ShowRemainingInUI = true
        }, CancellationToken.None);

        Assert.Equal(childTagId, created.TagId);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var saved = await verifyContext.Expenses.AsNoTracking().SingleAsync(x => x.Id == created.Id);
        Assert.Equal(childTagId, saved.TagId);
    }

    /// <summary>
    /// Updates an expense whose TagId was set to a root tag, changing it to a child tag.
    /// Verifies that the persisted TagId is the child tag and not the root tag.
    /// </summary>
    [Fact]
    public async Task UpdateExpenseAsync_Should_Keep_Selected_SubTagId()
    {
        int expenseId;
        int categoryId;
        int rootTagId;
        int childTagId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Zakupy", Color = "#6D4C41" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var rootTag = new Tag { Name = "Internetowe", CategoryId = category.Id };
            context.Tags.Add(rootTag);
            await context.SaveChangesAsync();

            var childTag = new Tag { Name = "Allegro", CategoryId = category.Id, ParentTagId = rootTag.Id };
            context.Tags.Add(childTag);
            await context.SaveChangesAsync();

            var monthPlan = new MonthPlan { Year = 2026, Month = 4 };
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            var expense = new Expense
            {
                MonthPlanId = monthPlan.Id,
                Name = "Zakupy",
                CategoryId = category.Id,
                TagId = rootTag.Id,
                PlannedAmount = 120m,
                ActualAmount = 80m,
                ShowRemainingInUI = true
            };

            context.Expenses.Add(expense);
            await context.SaveChangesAsync();

            expenseId = expense.Id;
            categoryId = category.Id;
            rootTagId = rootTag.Id;
            childTagId = childTag.Id;
        }

        var service = CreateService();

        var updated = await service.UpdateExpenseAsync(new UpdateExpenseRequest
        {
            Id = expenseId,
            Name = "Zakupy",
            CategoryId = categoryId,
            TagId = childTagId,
            PlannedAmount = 120m,
            ActualAmount = 80m,
            ShowRemainingInUI = true
        }, CancellationToken.None);

        Assert.Equal(childTagId, updated.TagId);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var saved = await verifyContext.Expenses.AsNoTracking().SingleAsync(x => x.Id == expenseId);
        Assert.Equal(childTagId, saved.TagId);
        Assert.NotEqual(rootTagId, saved.TagId);
    }

    /// <summary>
    /// Exercises the full Create/Update/Delete cycle of MonthSavingsTransferItems.
    /// Verifies that after deletion the month's SavingsTransfers collection is empty.
    /// </summary>
    [Fact]
    public async Task SavingsTransferItems_Crud_Should_Work()
    {
        var service = CreateService();

        var created = await service.CreateMonthSavingsTransferItemAsync(new CreateMonthSavingsTransferItemRequest
        {
            Year = 2026,
            Month = 4,
            Amount = 300m,
            TransferDate = new DateOnly(2026, 4, 10)
        }, CancellationToken.None);

        var updated = await service.UpdateMonthSavingsTransferItemAsync(new UpdateMonthSavingsTransferItemRequest
        {
            Id = created.Id,
            Amount = 350m,
            TransferDate = new DateOnly(2026, 4, 12)
        }, CancellationToken.None);

        Assert.Equal(350m, updated.Amount);

        await service.DeleteMonthSavingsTransferItemAsync(new DeleteMonthSavingsTransferItemRequest { Id = created.Id }, CancellationToken.None);

        var month = await service.GetMonthAsync(2026, 4, CancellationToken.None);
        Assert.Empty(month.SavingsTransfers);
    }

    /// <summary>
    /// Seeds four expenses with a mix of ShowRemainingInUI and zero/non-zero actual amounts.
    /// Verifies PlannedTotal=600, SpentTotal=90, RemainingTotal=560, RemainingPercent≈93.33.
    /// </summary>
    [Fact]
    public async Task GetMonthAsync_Should_Calculate_Kpi_With_Remaining_Fallback_Without_Double_Counting()
    {
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Dom", Color = "#43A047" };
            var monthPlan = new MonthPlan { Year = 2026, Month = 4 };
            context.Categories.Add(category);
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            context.Expenses.AddRange(
                new Expense
                {
                    MonthPlanId = monthPlan.Id,
                    Name = "A",
                    CategoryId = category.Id,
                    PlannedAmount = 100m,
                    ActualAmount = 40m,
                    ShowRemainingInUI = true
                },
                new Expense
                {
                    MonthPlanId = monthPlan.Id,
                    Name = "B",
                    CategoryId = category.Id,
                    PlannedAmount = 200m,
                    ActualAmount = 0m,
                    ShowRemainingInUI = false
                },
                new Expense
                {
                    MonthPlanId = monthPlan.Id,
                    Name = "C",
                    CategoryId = category.Id,
                    PlannedAmount = 300m,
                    ActualAmount = 0,
                    ShowRemainingInUI = true
                },
                new Expense
                {
                    MonthPlanId = monthPlan.Id,
                    Name = "D",
                    CategoryId = category.Id,
                    PlannedAmount = 0,
                    ActualAmount = 50m,
                    ShowRemainingInUI = true
                });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var month = await service.GetMonthAsync(2026, 4, CancellationToken.None);

        Assert.Equal(600m, month.Kpi.PlannedTotal);
        Assert.Equal(90m, month.Kpi.SpentTotal);
        Assert.Equal(560m, month.Kpi.RemainingTotal);
        Assert.Equal(93.33d, month.Kpi.RemainingPercent, 2);
    }

    /// <summary>
    /// Creates two expenses sequentially in the same month and verifies that the first gets Order=1
    /// and the second gets Order=2.
    /// </summary>
    [Fact]
    public async Task CreateExpenseAsync_Should_Assign_Last_Order_In_Month()
    {
        int categoryId;
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Rachunki", Color = "#455A64" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();

        var first = await service.CreateExpenseAsync(new CreateExpenseRequest
        {
            Year = 2026,
            Month = 5,
            Name = "A",
            CategoryId = categoryId,
            PlannedAmount = 100m,
            ActualAmount = 10m,
            ShowRemainingInUI = true
        }, CancellationToken.None);

        var second = await service.CreateExpenseAsync(new CreateExpenseRequest
        {
            Year = 2026,
            Month = 5,
            Name = "B",
            CategoryId = categoryId,
            PlannedAmount = 200m,
            ActualAmount = 20m,
            ShowRemainingInUI = true
        }, CancellationToken.None);

        Assert.Equal(1, first.Order);
        Assert.Equal(2, second.Order);
    }

    /// <summary>
    /// Creates two expenses in a month and then calls ReorderExpensesAsync with the IDs reversed.
    /// Verifies that GetMonthAsync returns expenses in the new order.
    /// </summary>
    [Fact]
    public async Task ReorderExpensesAsync_Should_Persist_New_Order()
    {
        int categoryId;
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Zakupy", Color = "#6D4C41" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();
        var first = await service.CreateExpenseAsync(new CreateExpenseRequest
        {
            Year = 2026,
            Month = 6,
            Name = "Pierwszy",
            CategoryId = categoryId,
            PlannedAmount = 50m,
            ActualAmount = 0,
            ShowRemainingInUI = true
        }, CancellationToken.None);

        var second = await service.CreateExpenseAsync(new CreateExpenseRequest
        {
            Year = 2026,
            Month = 6,
            Name = "Drugi",
            CategoryId = categoryId,
            PlannedAmount = 80m,
            ActualAmount = 0,
            ShowRemainingInUI = true
        }, CancellationToken.None);

        await service.ReorderExpensesAsync(new ReorderExpensesRequest
        {
            ExpenseIds = [second.Id, first.Id]
        }, CancellationToken.None);

        var month = await service.GetMonthAsync(2026, 6, CancellationToken.None);
        Assert.Equal(second.Id, month.Expenses[0].Id);
        Assert.Equal(first.Id, month.Expenses[1].Id);
    }

    /// <summary>
    /// Copies two selected expenses from month 8 to month 9 and verifies that copies have
    /// ActualAmount=0 while PlannedAmount and ShowRemainingInUI are preserved.
    /// </summary>
    [Fact]
    public async Task CopySelectedExpensesToNextMonthAsync_Should_Copy_Selected_Items_With_Actual_Set_To_Zero()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Zakupy", Color = "#6D4C41" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();

        var first = await service.CreateExpenseAsync(new CreateExpenseRequest
        {
            Year = 2026,
            Month = 8,
            Name = "A",
            CategoryId = categoryId,
            PlannedAmount = 120m,
            ActualAmount = 70m,
            ShowRemainingInUI = true
        }, CancellationToken.None);

        var second = await service.CreateExpenseAsync(new CreateExpenseRequest
        {
            Year = 2026,
            Month = 8,
            Name = "B",
            CategoryId = categoryId,
            PlannedAmount = 50m,
            ActualAmount = 10m,
            ShowRemainingInUI = false
        }, CancellationToken.None);

        var copiedCount = await service.CopySelectedExpensesToNextMonthAsync(new CopySelectedExpensesToNextMonthRequest
        {
            Year = 2026,
            Month = 8,
            ExpenseIds = [first.Id, second.Id]
        }, CancellationToken.None);

        Assert.Equal(2, copiedCount);

        var september = await service.GetMonthAsync(2026, 9, CancellationToken.None);
        var copiedA = Assert.Single(september.Expenses, x => x.Name == "A");
        var copiedB = Assert.Single(september.Expenses, x => x.Name == "B");

        Assert.Equal(120m, copiedA.PlannedAmount);
        Assert.Equal(0m, copiedA.ActualAmount);
        Assert.True(copiedA.ShowRemainingInUI);

        Assert.Equal(50m, copiedB.PlannedAmount);
        Assert.Equal(0m, copiedB.ActualAmount);
        Assert.False(copiedB.ShowRemainingInUI);
    }

    /// <summary>
    /// Prepares a missing month with one recurring source expense and verifies that the target month
    /// is not created during preview, while the suggestion is marked unavailable for auto-sync.
    /// </summary>
    [Fact]
    public async Task GetMonthPlanPreparationAsync_Should_Not_Create_Target_Month_And_Mark_Recurring_Suggestion_Unavailable()
    {
        var categoryId = await CreateCategoryAsync("Rachunki");
        var regularDefinitionId = await CreateRegularExpenseDefinitionAsync(categoryId, "Prad", 120m);
        var sourceMonthPlanId = await CreateMonthPlanAsync(2025, 7);

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            context.Expenses.Add(new Expense
            {
                MonthPlanId = sourceMonthPlanId,
                Order = 1,
                Name = "Prad",
                CategoryId = categoryId,
                RegularExpenseDefinitionId = regularDefinitionId,
                PlannedAmount = 120m,
                ActualAmount = 120m,
                ShowRemainingInUI = true
            });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.GetMonthPlanPreparationAsync(2026, 7, CancellationToken.None);

        Assert.False(result.MonthExists);
        Assert.Equal(2025, result.SourceYear);
        Assert.Equal(7, result.SourceMonth);

        var suggestion = Assert.Single(result.Suggestions);
        Assert.False(suggestion.IsAvailable);
        Assert.Equal(
            "Wydatek cykliczny zostanie automatycznie zsynchronizowany przy utworzeniu miesiąca.",
            suggestion.UnavailableReason);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var monthPlans = await verifyContext.MonthPlans.ToListAsync();
        Assert.Single(monthPlans);
        Assert.DoesNotContain(monthPlans, x => x.Year == 2026 && x.Month == 7);
    }

    /// <summary>
    /// Verifies that an older manual historical expense is suppressed when an active recurring
    /// definition with the same name/category/tag now covers that future month.
    /// </summary>
    [Fact]
    public async Task GetMonthPlanPreparationAsync_Should_Mark_Manual_History_Unavailable_When_Active_Recurring_Definition_Matches()
    {
        var categoryId = await CreateCategoryAsync("Kosmetyki");
        await CreateRegularExpenseDefinitionAsync(categoryId, "Kosmetyki", 160m);
        var sourceMonthPlanId = await CreateMonthPlanAsync(2025, 7);

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            context.Expenses.Add(new Expense
            {
                MonthPlanId = sourceMonthPlanId,
                Order = 1,
                Name = "Kosmetyki",
                CategoryId = categoryId,
                PlannedAmount = 130m,
                ActualAmount = 118m,
                ShowRemainingInUI = true
            });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.GetMonthPlanPreparationAsync(2026, 7, CancellationToken.None);

        var suggestion = Assert.Single(result.Suggestions);
        Assert.False(suggestion.IsAvailable);
        Assert.Equal(
            "Podobny aktywny wydatek cykliczny zostanie automatycznie dodany przy utworzeniu miesiąca.",
            suggestion.UnavailableReason);
    }

    /// <summary>
    /// Verifies that same-month previous-year suggestions are built from line-item actuals when present
    /// and fall back to planned amounts when actuals are zero.
    /// </summary>
    [Fact]
    public async Task GetMonthPlanPreparationAsync_Should_Suggest_Same_Month_Last_Year_Expenses_Using_Actual_Or_Planned_Basis()
    {
        var categoryId = await CreateCategoryAsync("Zakupy", supportsLineItems: true);
        var sourceMonthPlanId = await CreateMonthPlanAsync(2025, 7);

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var firstExpense = new Expense
            {
                MonthPlanId = sourceMonthPlanId,
                Order = 1,
                Name = "Pozycja z paragonu",
                CategoryId = categoryId,
                PlannedAmount = 120m,
                ActualAmount = 0m,
                ShowRemainingInUI = true
            };

            var secondExpense = new Expense
            {
                MonthPlanId = sourceMonthPlanId,
                Order = 2,
                Name = "Planowana pozycja",
                CategoryId = categoryId,
                PlannedAmount = 200m,
                ActualAmount = 0m,
                ShowRemainingInUI = false
            };

            context.Expenses.AddRange(firstExpense, secondExpense);
            await context.SaveChangesAsync();

            context.ExpenseLineItems.Add(new ExpenseLineItem
            {
                ExpenseId = firstExpense.Id,
                Description = "Pozycja 1",
                Amount = 73m,
                OccurredAt = new DateOnly(2025, 7, 5)
            });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.GetMonthPlanPreparationAsync(2026, 7, CancellationToken.None);

        var firstSuggestion = Assert.Single(result.Suggestions, x => x.Name == "Pozycja z paragonu");
        Assert.Equal(73m, firstSuggestion.SourceActualAmount);
        Assert.Equal(90m, firstSuggestion.SuggestedPlannedAmount);
        Assert.True(firstSuggestion.IsAvailable);

        var secondSuggestion = Assert.Single(result.Suggestions, x => x.Name == "Planowana pozycja");
        Assert.Equal(0m, secondSuggestion.SourceActualAmount);
        Assert.Equal(220m, secondSuggestion.SuggestedPlannedAmount);
        Assert.True(secondSuggestion.IsAvailable);
    }

    /// <summary>
    /// Verifies that the buffered suggestion amount rounds up to the nearest 10 below 500 and the
    /// nearest 100 once the buffered amount reaches 500 or more.
    /// </summary>
    [Theory]
    [InlineData(111, 130)]
    [InlineData(456, 600)]
    public async Task GetMonthPlanPreparationAsync_Should_Round_Suggested_PlannedAmount_By_Scale(
        double sourceAmount,
        double expectedSuggestedAmount)
    {
        var categoryId = await CreateCategoryAsync("Zakupy");
        var sourceMonthPlanId = await CreateMonthPlanAsync(2025, 8);

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            context.Expenses.Add(new Expense
            {
                MonthPlanId = sourceMonthPlanId,
                Order = 1,
                Name = "Pozycja",
                CategoryId = categoryId,
                PlannedAmount = (decimal)sourceAmount,
                ActualAmount = 0m,
                ShowRemainingInUI = true
            });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.GetMonthPlanPreparationAsync(2026, 8, CancellationToken.None);

        var suggestion = Assert.Single(result.Suggestions);
        Assert.Equal((decimal)expectedSuggestedAmount, suggestion.SuggestedPlannedAmount);
    }

    /// <summary>
    /// Applies a selected historical suggestion, edits the planned amount, and verifies the target
    /// month is created with the edited value and zero actual amount.
    /// </summary>
    [Fact]
    public async Task ApplyMonthPlanSuggestionsAsync_Should_Create_Target_Month_With_Edited_PlannedAmount()
    {
        var categoryId = await CreateCategoryAsync("Transport");
        var sourceMonthPlanId = await CreateMonthPlanAsync(2025, 9);

        int sourceExpenseId;
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var expense = new Expense
            {
                MonthPlanId = sourceMonthPlanId,
                Order = 1,
                Name = "Paliwo",
                CategoryId = categoryId,
                PlannedAmount = 120m,
                ActualAmount = 95m,
                ShowRemainingInUI = true
            };

            context.Expenses.Add(expense);
            await context.SaveChangesAsync();
            sourceExpenseId = expense.Id;
        }

        var service = CreateService();
        var createdCount = await service.ApplyMonthPlanSuggestionsAsync(new ApplyMonthPlanSuggestionsRequest
        {
            Year = 2026,
            Month = 9,
            Suggestions =
            [
                new ApplyMonthPlanSuggestionItemRequest
                {
                    SourceExpenseId = sourceExpenseId,
                    PlannedAmount = 133m
                }
            ]
        }, CancellationToken.None);

        Assert.Equal(1, createdCount);

        var targetMonth = await service.GetMonthAsync(2026, 9, CancellationToken.None);
        var copiedExpense = Assert.Single(targetMonth.Expenses);
        Assert.Equal("Paliwo", copiedExpense.Name);
        Assert.Equal(133m, copiedExpense.PlannedAmount);
        Assert.Equal(0m, copiedExpense.ActualAmount);
        Assert.True(copiedExpense.ShowRemainingInUI);
    }

    /// <summary>
    /// Applies a recurring source suggestion alongside a manual one and verifies the recurring row
    /// is suppressed because the month creation path already auto-synced it.
    /// </summary>
    [Fact]
    public async Task ApplyMonthPlanSuggestionsAsync_Should_Skip_Recurring_Duplicates_And_Keep_AutoSynced_Expense()
    {
        var categoryId = await CreateCategoryAsync("Rachunki");
        var regularDefinitionId = await CreateRegularExpenseDefinitionAsync(categoryId, "Prad", 120m);
        var sourceMonthPlanId = await CreateMonthPlanAsync(2025, 10);

        int recurringExpenseId;
        int manualExpenseId;
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var recurringExpense = new Expense
            {
                MonthPlanId = sourceMonthPlanId,
                Order = 1,
                Name = "Prad",
                CategoryId = categoryId,
                RegularExpenseDefinitionId = regularDefinitionId,
                PlannedAmount = 120m,
                ActualAmount = 120m,
                ShowRemainingInUI = true
            };

            var manualExpense = new Expense
            {
                MonthPlanId = sourceMonthPlanId,
                Order = 2,
                Name = "Papier",
                CategoryId = categoryId,
                PlannedAmount = 45m,
                ActualAmount = 30m,
                ShowRemainingInUI = false
            };

            context.Expenses.AddRange(recurringExpense, manualExpense);
            await context.SaveChangesAsync();

            recurringExpenseId = recurringExpense.Id;
            manualExpenseId = manualExpense.Id;
        }

        var service = CreateService();
        var createdCount = await service.ApplyMonthPlanSuggestionsAsync(new ApplyMonthPlanSuggestionsRequest
        {
            Year = 2026,
            Month = 10,
            Suggestions =
            [
                new ApplyMonthPlanSuggestionItemRequest
                {
                    SourceExpenseId = recurringExpenseId,
                    PlannedAmount = 133m
                },
                new ApplyMonthPlanSuggestionItemRequest
                {
                    SourceExpenseId = manualExpenseId,
                    PlannedAmount = 50m
                }
            ]
        }, CancellationToken.None);

        Assert.Equal(1, createdCount);

        var targetMonth = await service.GetMonthAsync(2026, 10, CancellationToken.None);
        Assert.Equal(2, targetMonth.Expenses.Count);

        var autoSyncedRecurring = Assert.Single(targetMonth.Expenses, x => x.Name == "Prad");
        Assert.Equal(regularDefinitionId, autoSyncedRecurring.RegularExpenseDefinitionId);
        Assert.Equal(120m, autoSyncedRecurring.PlannedAmount);

        var copiedManual = Assert.Single(targetMonth.Expenses, x => x.Name == "Papier");
        Assert.Null(copiedManual.RegularExpenseDefinitionId);
        Assert.Equal(50m, copiedManual.PlannedAmount);
        Assert.Equal(0m, copiedManual.ActualAmount);
    }

    /// <summary>
    /// Verifies that applying an older manual historical suggestion does not duplicate an active
    /// recurring definition that now represents the same planned expense.
    /// </summary>
    [Fact]
    public async Task ApplyMonthPlanSuggestionsAsync_Should_Skip_Manual_History_When_Active_Recurring_Definition_Matches()
    {
        var categoryId = await CreateCategoryAsync("Kosmetyki");
        var regularDefinitionId = await CreateRegularExpenseDefinitionAsync(categoryId, "Kosmetyki", 160m);
        var sourceMonthPlanId = await CreateMonthPlanAsync(2025, 11);

        int sourceExpenseId;
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var expense = new Expense
            {
                MonthPlanId = sourceMonthPlanId,
                Order = 1,
                Name = "Kosmetyki",
                CategoryId = categoryId,
                PlannedAmount = 130m,
                ActualAmount = 118m,
                ShowRemainingInUI = true
            };

            context.Expenses.Add(expense);
            await context.SaveChangesAsync();
            sourceExpenseId = expense.Id;
        }

        var service = CreateService();
        var createdCount = await service.ApplyMonthPlanSuggestionsAsync(new ApplyMonthPlanSuggestionsRequest
        {
            Year = 2026,
            Month = 11,
            Suggestions =
            [
                new ApplyMonthPlanSuggestionItemRequest
                {
                    SourceExpenseId = sourceExpenseId,
                    PlannedAmount = 140m
                }
            ]
        }, CancellationToken.None);

        Assert.Equal(0, createdCount);

        var targetMonth = await service.GetMonthAsync(2026, 11, CancellationToken.None);
        var autoSyncedRecurring = Assert.Single(targetMonth.Expenses);
        Assert.Equal("Kosmetyki", autoSyncedRecurring.Name);
        Assert.Equal(regularDefinitionId, autoSyncedRecurring.RegularExpenseDefinitionId);
        Assert.Equal(160m, autoSyncedRecurring.PlannedAmount);
    }

    /// <summary>
    /// Copies selected expenses to a non-adjacent target month and verifies line items are not copied
    /// while planned fields are preserved and actual amounts are reset.
    /// </summary>
    [Fact]
    public async Task CopySelectedExpensesToMonthAsync_Should_Copy_Selected_Items_To_Explicit_Target_And_Strip_LineItems()
    {
        var categoryId = await CreateCategoryAsync("Zakupy", supportsLineItems: true);
        var sourceMonthPlanId = await CreateMonthPlanAsync(2026, 8);

        int sourceExpenseId;
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var expense = new Expense
            {
                MonthPlanId = sourceMonthPlanId,
                Order = 1,
                Name = "Paragon",
                CategoryId = categoryId,
                PlannedAmount = 150m,
                ActualAmount = 0m,
                ShowRemainingInUI = false
            };

            context.Expenses.Add(expense);
            await context.SaveChangesAsync();

            context.ExpenseLineItems.AddRange(
                new ExpenseLineItem
                {
                    ExpenseId = expense.Id,
                    Description = "Chleb",
                    Amount = 45m,
                    OccurredAt = new DateOnly(2026, 8, 2)
                },
                new ExpenseLineItem
                {
                    ExpenseId = expense.Id,
                    Description = "Mleko",
                    Amount = 30m,
                    OccurredAt = new DateOnly(2026, 8, 3)
                });

            await context.SaveChangesAsync();
            sourceExpenseId = expense.Id;
        }

        var service = CreateService();
        var copiedCount = await service.CopySelectedExpensesToMonthAsync(new CopySelectedExpensesToMonthRequest
        {
            Year = 2026,
            Month = 8,
            TargetYear = 2026,
            TargetMonth = 10,
            ExpenseIds = [sourceExpenseId]
        }, CancellationToken.None);

        Assert.Equal(1, copiedCount);

        var targetMonth = await service.GetMonthAsync(2026, 10, CancellationToken.None);
        var copiedExpense = Assert.Single(targetMonth.Expenses);
        Assert.Equal("Paragon", copiedExpense.Name);
        Assert.Equal(150m, copiedExpense.PlannedAmount);
        Assert.Equal(0m, copiedExpense.ActualAmount);
        Assert.False(copiedExpense.ShowRemainingInUI);
        Assert.Empty(copiedExpense.LineItems);
    }

    /// <summary>
    /// Copies a mixed selection containing a loan-backed expense and a manual expense.
    /// Verifies loan-backed rows are skipped so their unique LoanInstallmentId is not duplicated.
    /// </summary>
    [Fact]
    public async Task CopySelectedExpensesToMonthAsync_Should_Skip_LoanBacked_Expenses()
    {
        var categoryId = await CreateCategoryAsync("Raty");
        var sourceMonthPlanId = await CreateMonthPlanAsync(2026, 8);

        int loanExpenseId;
        int manualExpenseId;
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var loan = new Loan
            {
                Name = "Kredyt",
                LoanType = 1,
                InterestMode = 1,
                Principal = 1000m,
                InterestRate = 5m,
                RepaymentDayOfMonth = 10,
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 12, 31),
                IsActive = true
            };

            context.Loans.Add(loan);
            await context.SaveChangesAsync();

            var installment = new LoanInstallment
            {
                LoanId = loan.Id,
                Year = 2026,
                Month = 8,
                DueDate = new DateOnly(2026, 8, 10),
                Amount = 300m,
                PrincipalAmount = 250m,
                InterestAmount = 50m
            };

            context.LoanInstallments.Add(installment);
            await context.SaveChangesAsync();

            var loanExpense = new Expense
            {
                MonthPlanId = sourceMonthPlanId,
                Order = 1,
                Name = "Rata kredytu",
                CategoryId = categoryId,
                LoanInstallmentId = installment.Id,
                PlannedAmount = 300m,
                ActualAmount = 0m,
                ShowRemainingInUI = true
            };

            var manualExpense = new Expense
            {
                MonthPlanId = sourceMonthPlanId,
                Order = 2,
                Name = "Manualna rata",
                CategoryId = categoryId,
                PlannedAmount = 120m,
                ActualAmount = 80m,
                ShowRemainingInUI = true
            };

            context.Expenses.AddRange(loanExpense, manualExpense);
            await context.SaveChangesAsync();

            loanExpenseId = loanExpense.Id;
            manualExpenseId = manualExpense.Id;
        }

        var service = CreateService();
        var copiedCount = await service.CopySelectedExpensesToMonthAsync(new CopySelectedExpensesToMonthRequest
        {
            Year = 2026,
            Month = 8,
            TargetYear = 2026,
            TargetMonth = 10,
            ExpenseIds = [loanExpenseId, manualExpenseId]
        }, CancellationToken.None);

        Assert.Equal(1, copiedCount);

        var targetMonth = await service.GetMonthAsync(2026, 10, CancellationToken.None);
        var copiedExpense = Assert.Single(targetMonth.Expenses);
        Assert.Equal("Manualna rata", copiedExpense.Name);
        Assert.Equal(120m, copiedExpense.PlannedAmount);
        Assert.Equal(0m, copiedExpense.ActualAmount);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var persistedTargetExpense = await verifyContext.Expenses
            .AsNoTracking()
            .SingleAsync(x => x.MonthPlan.Year == 2026 && x.MonthPlan.Month == 10);
        Assert.Null(persistedTargetExpense.LoanInstallmentId);
    }

    /// <summary>
    /// Verifies that copying to the same month is rejected before any data is changed.
    /// </summary>
    [Fact]
    public async Task CopySelectedExpensesToMonthAsync_Should_Throw_When_Target_Equals_Source()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() => service.CopySelectedExpensesToMonthAsync(
            new CopySelectedExpensesToMonthRequest
            {
                Year = 2026,
                Month = 8,
                TargetYear = 2026,
                TargetMonth = 8,
                ExpenseIds = [1]
            }, CancellationToken.None));
    }

    /// <summary>
    /// Closes a month and then attempts to create an expense in that month.
    /// Verifies that a BadRequestException is thrown.
    /// </summary>
    [Fact]
    public async Task CreateExpenseAsync_Should_Throw_When_Month_Is_Closed()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Transport", Color = "#1E88E5" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();
        await service.CloseMonthAsync(2026, 7, CancellationToken.None);

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateExpenseAsync(new CreateExpenseRequest
        {
            Year = 2026,
            Month = 7,
            Name = "Paliwo",
            CategoryId = categoryId,
            PlannedAmount = 150m,
            ActualAmount = 0,
            ShowRemainingInUI = true
        }, CancellationToken.None));
    }

    /// <summary>
    /// Closes month 1 after creating a recurring expense definition and then opens month 2.
    /// Verifies that the recurring expense "Netflix" appears exactly once in month 2 with the correct amount.
    /// </summary>
    [Fact]
    public async Task CloseMonthAsync_Should_Generate_Regular_Expenses_In_Next_Month_And_Be_Idempotent()
    {
        int categoryId;
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Subskrypcje", Color = "#5E35B1" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();
        await service.CreateRegularExpenseDefinitionAsync(new CreateRegularExpenseDefinitionRequest
        {
            Name = "Netflix",
            CategoryId = categoryId,
            Amount = 60m
        }, CancellationToken.None);

        await service.CloseMonthAsync(2026, 1, CancellationToken.None);
        await service.OpenMonthAsync(2026, 2, CancellationToken.None);

        var february = await service.GetMonthAsync(2026, 2, CancellationToken.None);
        var recurringExpenses = february.Expenses.Where(x => x.Name == "Netflix").ToList();

        Assert.Single(recurringExpenses);
        Assert.Equal(60m, recurringExpenses[0].PlannedAmount);
    }

    /// <summary>
    /// Pre-seeds a closed MonthPlan for month 5, then calls OpenMonthAsync.
    /// Verifies that recurring expenses are NOT generated because the plan already existed.
    /// </summary>
    [Fact]
    public async Task OpenMonthAsync_Should_Not_Sync_Recurring_Data_For_Existing_MonthPlan()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Subskrypcje", Color = "#5E35B1" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;

            context.MonthPlans.Add(new MonthPlan
            {
                Year = 2026,
                Month = 5,
                IsClosed = true
            });
            await context.SaveChangesAsync();
        }

        var service = CreateService();
        await service.CreateRegularExpenseDefinitionAsync(new CreateRegularExpenseDefinitionRequest
        {
            Name = "Netflix",
            CategoryId = categoryId,
            Amount = 60m
        }, CancellationToken.None);

        await service.OpenMonthAsync(2026, 5, CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var monthPlan = await verifyContext.MonthPlans.FirstAsync(x => x.Year == 2026 && x.Month == 5);
        Assert.False(monthPlan.IsClosed);

        var expensesCount = await verifyContext.Expenses.CountAsync(x => x.MonthPlanId == monthPlan.Id);
        Assert.Equal(0, expensesCount);
    }

    /// <summary>
    /// Pre-seeds a closed MonthPlan for month 2, then closes month 1 (which would normally open/sync month 2).
    /// Verifies that month 2 remains closed and no recurring expenses or incomes are added.
    /// </summary>
    [Fact]
    public async Task CloseMonthAsync_Should_Not_Reopen_Already_Closed_Next_Month_Or_Sync_Recurring_Data()
    {
        int categoryId;
        int accountId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Subskrypcje", Color = "#5E35B1" };
            var account = new Account { Name = "Bank", Type = (int)AccountType.Bank };
            context.Categories.Add(category);
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
            categoryId = category.Id;
            accountId = account.Id;

            context.MonthPlans.Add(new MonthPlan
            {
                Year = 2026,
                Month = 2,
                IsClosed = true
            });
            await context.SaveChangesAsync();
        }

        var factory = TestDbContextFactory.CreateFactory(_dbName);
        var provider = new StaticDateTimeProvider(DateTime.UtcNow);
        var incomeService = new IncomeService(factory, provider);
        var service = new ExpenseService(
            factory,
            provider,
            new RecordingAppEventPublisher(),
            incomeService,
            new NoOpLoanService());

        await service.CreateRegularExpenseDefinitionAsync(new CreateRegularExpenseDefinitionRequest
        {
            Name = "Netflix",
            CategoryId = categoryId,
            Amount = 60m
        }, CancellationToken.None);

        await incomeService.CreateRegularDefinitionAsync(new CreateRegularIncomeDefinitionRequest
        {
            Name = "Wyplata",
            Amount = 5000m,
            DayOfMonth = 10,
            AccountId = accountId
        }, CancellationToken.None);

        await service.CloseMonthAsync(2026, 1, CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var nextMonthPlan = await verifyContext.MonthPlans.FirstAsync(x => x.Year == 2026 && x.Month == 2);
        Assert.True(nextMonthPlan.IsClosed);

        var nextMonthExpenses = await verifyContext.Expenses
            .AsNoTracking()
            .Where(x => x.MonthPlanId == nextMonthPlan.Id)
            .ToListAsync();
        Assert.Empty(nextMonthExpenses);

        var nextMonthIncomes = await verifyContext.Incomes
            .AsNoTracking()
            .Where(x => x.MonthPlanId == nextMonthPlan.Id)
            .ToListAsync();
        Assert.Empty(nextMonthIncomes);
    }

    /// <summary>
    /// Pre-seeds an open MonthPlan for month 6, then calls GetMonthAsync after creating recurring definitions.
    /// Verifies that recurring expenses and incomes are NOT auto-synced for an already-existing plan.
    /// </summary>
    [Fact]
    public async Task GetMonthAsync_Should_Not_Sync_Recurring_Data_For_Existing_Open_MonthPlan()
    {
        int categoryId;
        int accountId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Subskrypcje", Color = "#5E35B1" };
            var account = new Account { Name = "Bank", Type = (int)AccountType.Bank };
            context.Categories.Add(category);
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
            categoryId = category.Id;
            accountId = account.Id;

            context.MonthPlans.Add(new MonthPlan
            {
                Year = 2026,
                Month = 6,
                IsClosed = false
            });
            await context.SaveChangesAsync();
        }

        var factory = TestDbContextFactory.CreateFactory(_dbName);
        var now = DateTime.UtcNow;
        var provider = new StaticDateTimeProvider(now);
        var incomeService = new IncomeService(factory, provider);
        var service = new ExpenseService(
            factory,
            provider,
            new RecordingAppEventPublisher(),
            incomeService,
            new NoOpLoanService());

        await service.CreateRegularExpenseDefinitionAsync(new CreateRegularExpenseDefinitionRequest
        {
            Name = "Netflix",
            CategoryId = categoryId,
            Amount = 60m
        }, CancellationToken.None);

        await incomeService.CreateRegularDefinitionAsync(new CreateRegularIncomeDefinitionRequest
        {
            Name = "Wyplata",
            Amount = 5000m,
            DayOfMonth = 10,
            AccountId = accountId
        }, CancellationToken.None);

        var month = await service.GetMonthAsync(2026, 6, CancellationToken.None);
        var incomes = await incomeService.GetMonthIncomesAsync(2026, 6, CancellationToken.None);

        Assert.Empty(month.Expenses);
        Assert.Empty(incomes);
    }

    /// <summary>
    /// Creates recurring expense and income definitions then calls GetMonthAsync for a new month.
    /// Verifies that both the "Netflix" expense and the regular income are auto-synced on first access.
    /// </summary>
    [Fact]
    public async Task GetMonthAsync_Should_AutoSync_Recurring_Data_For_Open_Month()
    {
        int categoryId;
        int accountId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Subskrypcje", Color = "#5E35B1" };
            var account = new Account { Name = "Bank", Type = (int)AccountType.Bank };
            context.Categories.Add(category);
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
            categoryId = category.Id;
            accountId = account.Id;
        }

        var factory = TestDbContextFactory.CreateFactory(_dbName);
        var now = DateTime.UtcNow;
        var provider = new StaticDateTimeProvider(now);
        var incomeService = new IncomeService(factory, provider);
        var expenseService = new ExpenseService(
            factory,
            provider,
            new RecordingAppEventPublisher(),
            incomeService,
            new NoOpLoanService());

        await expenseService.CreateRegularExpenseDefinitionAsync(new CreateRegularExpenseDefinitionRequest
        {
            Name = "Netflix",
            CategoryId = categoryId,
            Amount = 60m
        }, CancellationToken.None);

        await incomeService.CreateRegularDefinitionAsync(new CreateRegularIncomeDefinitionRequest
        {
            Name = "Wyplata",
            Amount = 5000m,
            DayOfMonth = 10,
            AccountId = accountId
        }, CancellationToken.None);

        var month = await expenseService.GetMonthAsync(2026, 3, CancellationToken.None);
        var incomes = await incomeService.GetMonthIncomesAsync(2026, 3, CancellationToken.None);

        Assert.Contains(month.Expenses, x => x.Name == "Netflix" && x.PlannedAmount == 60m);
        Assert.Contains(incomes, x => x.Name == "Wyplata" && x.IsRegular);
    }

    /// <summary>
    /// Creates a regular expense definition and then soft-deletes it.
    /// Verifies that IsActive is set to false rather than the row being removed.
    /// </summary>
    [Fact]
    public async Task DeleteRegularExpenseDefinitionAsync_Should_SoftDelete_By_Setting_IsActive_False()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Rachunki", Color = "#455A64" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();
        var created = await service.CreateRegularExpenseDefinitionAsync(new CreateRegularExpenseDefinitionRequest
        {
            Name = "Internet",
            CategoryId = categoryId,
            Amount = 80m
        }, CancellationToken.None);

        await service.DeleteRegularExpenseDefinitionAsync(new DeleteRegularExpenseDefinitionRequest { Id = created.Id }, CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var definition = await verifyContext.RegularExpenseDefinitions.FirstAsync(x => x.Id == created.Id);
        Assert.False(definition.IsActive);
    }

    /// <summary>
    /// Creates a regular expense definition and then permanently deletes it.
    /// Verifies that the row no longer exists in the database.
    /// </summary>
    [Fact]
    public async Task DeleteRegularExpenseDefinitionPermanentlyAsync_Should_Remove_Definition_From_Database()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Rachunki", Color = "#455A64" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();
        var created = await service.CreateRegularExpenseDefinitionAsync(new CreateRegularExpenseDefinitionRequest
        {
            Name = "Internet",
            CategoryId = categoryId,
            Amount = 80m
        }, CancellationToken.None);

        await service.DeleteRegularExpenseDefinitionPermanentlyAsync(
            new DeleteRegularExpenseDefinitionRequest { Id = created.Id },
            CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var exists = await verifyContext.RegularExpenseDefinitions.AnyAsync(x => x.Id == created.Id);
        Assert.False(exists);
    }

    /// <summary>
    /// Creates two regular expense definitions, reverses their order via ReorderRegularExpenseDefinitionsAsync,
    /// then opens a new month and verifies that auto-generated expenses appear in the new order.
    /// </summary>
    [Fact]
    public async Task ReorderRegularExpenseDefinitionsAsync_Should_Drive_Order_Of_AutoGenerated_Expenses()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Rachunki", Color = "#455A64" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();
        var first = await service.CreateRegularExpenseDefinitionAsync(new CreateRegularExpenseDefinitionRequest
        {
            Name = "Internet",
            CategoryId = categoryId,
            Amount = 80m
        }, CancellationToken.None);

        var second = await service.CreateRegularExpenseDefinitionAsync(new CreateRegularExpenseDefinitionRequest
        {
            Name = "Netflix",
            CategoryId = categoryId,
            Amount = 60m
        }, CancellationToken.None);

        await service.ReorderRegularExpenseDefinitionsAsync(new ReorderRegularExpenseDefinitionsRequest
        {
            DefinitionIds = [second.Id, first.Id]
        }, CancellationToken.None);

        await service.OpenMonthAsync(2026, 9, CancellationToken.None);
        var month = await service.GetMonthAsync(2026, 9, CancellationToken.None);

        Assert.Equal("Netflix", month.Expenses[0].Name);
        Assert.Equal("Internet", month.Expenses[1].Name);
    }

    /// <summary>
    /// Deletes a recurring expense from a month and then calls GetMonthAsync again.
    /// Verifies that the expense is not re-created and that the soft-deleted row remains in the database.
    /// </summary>
    [Fact]
    public async Task DeleteRecurringExpense_FromMonth_Should_Not_Recreate_And_Should_Not_Throw_On_Reload()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Subskrypcje", Color = "#5E35B1" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();
        await service.CreateRegularExpenseDefinitionAsync(new CreateRegularExpenseDefinitionRequest
        {
            Name = "Netflix",
            CategoryId = categoryId,
            Amount = 60m
        }, CancellationToken.None);

        var initialMonth = await service.GetMonthAsync(2026, 10, CancellationToken.None);
        var recurringExpense = Assert.Single(initialMonth.Expenses, x => x.Name == "Netflix");

        await service.DeleteExpenseAsync(new DeleteExpenseRequest { Id = recurringExpense.Id }, CancellationToken.None);

        var reloadedMonth = await service.GetMonthAsync(2026, 10, CancellationToken.None);
        Assert.DoesNotContain(reloadedMonth.Expenses, x => x.Name == "Netflix");

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var storedExpenses = await verifyContext.Expenses
            .IgnoreQueryFilters()
            .Where(x => x.MonthPlanId == reloadedMonth.Id)
            .ToListAsync();

        Assert.Single(storedExpenses);
        Assert.True(storedExpenses[0].IsDeleted);
    }

    /// <summary>
    /// Verifies that a soft-deleted generated recurring expense still blocks re-adding the same
    /// regular definition to the month because the duplicate check intentionally ignores filters.
    /// </summary>
    [Fact]
    public async Task AddRegularExpenseDefinitionToMonthAsync_Should_Return_False_When_SoftDeleted_Generated_Row_Exists()
    {
        int categoryId;
        int definitionId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Subskrypcje", Color = "#5E35B1" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();
        var definition = await service.CreateRegularExpenseDefinitionAsync(new CreateRegularExpenseDefinitionRequest
        {
            Name = "Netflix",
            CategoryId = categoryId,
            Amount = 60m
        }, CancellationToken.None);
        definitionId = definition.Id;

        var initialMonth = await service.GetMonthAsync(2026, 11, CancellationToken.None);
        var recurringExpense = Assert.Single(initialMonth.Expenses, x => x.Name == "Netflix");

        await service.DeleteExpenseAsync(new DeleteExpenseRequest { Id = recurringExpense.Id }, CancellationToken.None);

        var addedAgain = await service.AddRegularExpenseDefinitionToMonthAsync(definitionId, 2026, 11, CancellationToken.None);

        Assert.False(addedAgain);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var storedExpenses = await verifyContext.Expenses
            .IgnoreQueryFilters()
            .Where(x => x.MonthPlanId == initialMonth.Id)
            .ToListAsync();

        Assert.Single(storedExpenses);
        Assert.True(storedExpenses[0].IsDeleted);
    }

    /// <summary>
    /// Deletes a recurring expense from month 10 and then opens month 11.
    /// Verifies that the recurring expense is still generated in the next month.
    /// </summary>
    [Fact]
    public async Task DeleteRecurringExpense_FromMonth_Should_Still_Generate_In_Next_Month()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Subskrypcje", Color = "#5E35B1" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();
        await service.CreateRegularExpenseDefinitionAsync(new CreateRegularExpenseDefinitionRequest
        {
            Name = "Netflix",
            CategoryId = categoryId,
            Amount = 60m
        }, CancellationToken.None);

        var october = await service.GetMonthAsync(2026, 10, CancellationToken.None);
        var recurringExpense = Assert.Single(october.Expenses, x => x.Name == "Netflix");
        await service.DeleteExpenseAsync(new DeleteExpenseRequest { Id = recurringExpense.Id }, CancellationToken.None);

        await service.OpenMonthAsync(2026, 11, CancellationToken.None);
        var november = await service.GetMonthAsync(2026, 11, CancellationToken.None);

        Assert.Contains(november.Expenses, x => x.Name == "Netflix" && x.PlannedAmount == 60m);
    }

    /// <summary>
    /// Seeds 12 months of expenses, incomes, and account balances for two categories.
    /// Verifies category totals, averages, months-with-expenses, tag statistics, monthly finance rows,
    /// and account balance rows returned by GetYearStatisticsAsync.
    /// </summary>
    [Fact]
    public async Task GetYearStatisticsAsync_Should_Return_Category_Metrics_And_Tables()
    {
        int foodCategoryId;
        int billsCategoryId;
        int accountId;
        int parentTagId;
        int childTagId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var food = new Category { Name = "Spozywcze", Color = "#43A047" };
            var bills = new Category { Name = "Rachunki", Color = "#455A64" };
            var account = new Account { Name = "Bank", Type = (int)AccountType.Bank, Order = 1 };

            context.Categories.AddRange(food, bills);
            context.Accounts.Add(account);
            await context.SaveChangesAsync();

            foodCategoryId = food.Id;
            billsCategoryId = bills.Id;
            accountId = account.Id;

            var parentTag = new Tag { Name = "Sklep", CategoryId = foodCategoryId };
            context.Tags.Add(parentTag);
            await context.SaveChangesAsync();

            var childTag = new Tag { Name = "Biedronka", CategoryId = foodCategoryId, ParentTagId = parentTag.Id };
            context.Tags.Add(childTag);
            await context.SaveChangesAsync();

            parentTagId = parentTag.Id;
            childTagId = childTag.Id;

            for (var month = 1; month <= 12; month++)
            {
                var monthPlan = new MonthPlan { Year = 2026, Month = month };
                context.MonthPlans.Add(monthPlan);
                await context.SaveChangesAsync();

                context.Expenses.AddRange(
                    new Expense
                    {
                        MonthPlanId = monthPlan.Id,
                        Order = 1,
                        Name = $"Spozywcze {month}",
                        CategoryId = foodCategoryId,
                        TagId = childTagId,
                        PlannedAmount = month * 10m,
                        ActualAmount = month * 10m,
                        ShowRemainingInUI = true
                    },
                    new Expense
                    {
                        MonthPlanId = monthPlan.Id,
                        Order = 2,
                        Name = $"Rachunki {month}",
                        CategoryId = billsCategoryId,
                        PlannedAmount = 50m,
                        ActualAmount = 50m,
                        ShowRemainingInUI = true
                    });

                context.Incomes.Add(new Income
                {
                    MonthPlanId = monthPlan.Id,
                    Name = $"Wyplata {month}",
                    Amount = 2000m,
                    AccountId = accountId,
                    ExpectedDayOfMonth = new DateOnly(2026, month, 10)
                });

                context.AccountMonthBalances.Add(new AccountMonthBalance
                {
                    AccountId = accountId,
                    Year = 2026,
                    Month = month,
                    ClosingBalance = 1000m + month * 100m
                });
            }

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.GetYearStatisticsAsync(2026, CancellationToken.None);

        Assert.Equal(2026, result.Year);
        Assert.Contains(2026, result.AvailableYears);
        Assert.Equal(12, result.PopulatedMonths.Count);
        Assert.Equal(12, result.AccountBalanceMonths.Count);
        Assert.Equal(2, result.CategoryStatistics.Count);

        var foodStats = Assert.Single(result.CategoryStatistics, x => x.CategoryId == foodCategoryId);
        var billsStats = Assert.Single(result.CategoryStatistics, x => x.CategoryId == billsCategoryId);

        Assert.Equal(780m, foodStats.TotalSpent);
        Assert.Equal(65m, foodStats.AverageMonthlySpent);
        Assert.Equal(12, foodStats.MonthsWithExpenses);

        Assert.Equal(600m, billsStats.TotalSpent);
        Assert.Equal(50m, billsStats.AverageMonthlySpent);
        Assert.Equal(12, billsStats.MonthsWithExpenses);

        Assert.True(result.TopCategories.Count >= 2);
        Assert.Equal(foodCategoryId, result.TopCategories[0].CategoryId);
        Assert.Equal(12, result.CategoryBreakdown.Single(x => x.CategoryId == foodCategoryId).MonthlySpent.Count);

        var parentTagStats = Assert.Single(result.CategoryTagStatistics, x => x.TagId == parentTagId);
        var childTagStats = Assert.Single(result.CategoryTagStatistics, x => x.TagId == childTagId);
        Assert.True(parentTagStats.HasChildren);
        Assert.Equal(780m, parentTagStats.TotalSpent);
        Assert.Equal(780m, childTagStats.TotalSpent);

        var january = Assert.Single(result.MonthlyFinance, x => x.Month == 1);
        Assert.Equal(2000m, january.IncomeAmount);
        Assert.Equal(60m, january.PlannedAmount);
        Assert.Equal(60m, january.SpentAmount);

        var accountRow = Assert.Single(result.AccountBalances, x => x.AccountId == accountId);
        Assert.Equal(12, accountRow.MonthlyClosingBalances.Count);
    }

    /// <summary>
    /// Seeds monthly expense history that produces one category deviation over 20 percent,
    /// one category exactly at 20 percent, and one category with no prior history.
    /// Verifies that only the over-threshold category is returned and that the alert
    /// preparation path does not publish any app events.
    /// </summary>
    [Fact]
    public async Task GetYearStatisticsAsync_Should_Return_DeviationAlertCandidates_Only_Above_Twenty_Percent_And_Without_Publishing_Events()
    {
        int stableCategoryId;
        int overCategoryId;
        int freshCategoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var stable = new Category { Name = "Stabilne", Color = "#43A047" };
            var over = new Category { Name = "Nadmiarowe", Color = "#FB8C00" };
            var fresh = new Category { Name = "Nowe", Color = "#1E88E5" };

            context.Categories.AddRange(stable, over, fresh);
            await context.SaveChangesAsync();

            stableCategoryId = stable.Id;
            overCategoryId = over.Id;
            freshCategoryId = fresh.Id;

            for (var month = 1; month <= 3; month++)
            {
                var monthPlan = new MonthPlan
                {
                    Year = 2026,
                    Month = month
                };

                context.MonthPlans.Add(monthPlan);
                await context.SaveChangesAsync();

                if (month <= 2)
                {
                    context.Expenses.AddRange(
                        new Expense
                        {
                            MonthPlanId = monthPlan.Id,
                            Order = 1,
                            Name = $"Stabilne {month}",
                            CategoryId = stableCategoryId,
                            PlannedAmount = 100m,
                            ActualAmount = 100m,
                            ShowRemainingInUI = true
                        },
                        new Expense
                        {
                            MonthPlanId = monthPlan.Id,
                            Order = 2,
                            Name = $"Nadmiarowe {month}",
                            CategoryId = overCategoryId,
                            PlannedAmount = 100m,
                            ActualAmount = 100m,
                            ShowRemainingInUI = true
                        });
                }

                if (month == 3)
                {
                    context.Expenses.AddRange(
                        new Expense
                        {
                            MonthPlanId = monthPlan.Id,
                            Order = 1,
                            Name = "Stabilne 3",
                            CategoryId = stableCategoryId,
                            PlannedAmount = 120m,
                            ActualAmount = 120m,
                            ShowRemainingInUI = true
                        },
                        new Expense
                        {
                            MonthPlanId = monthPlan.Id,
                            Order = 2,
                            Name = "Nadmiarowe 3",
                            CategoryId = overCategoryId,
                            PlannedAmount = 121m,
                            ActualAmount = 121m,
                            ShowRemainingInUI = true
                        },
                        new Expense
                        {
                            MonthPlanId = monthPlan.Id,
                            Order = 3,
                            Name = "Nowe 3",
                            CategoryId = freshCategoryId,
                            PlannedAmount = 500m,
                            ActualAmount = 500m,
                            ShowRemainingInUI = true
                        });
                }
            }

            await context.SaveChangesAsync();
        }

        var publisher = new RecordingAppEventPublisher();
        var service = CreateService(publisher);

        var result = await service.GetYearStatisticsAsync(2026, CancellationToken.None);

        var candidate = Assert.Single(result.DeviationAlertCandidates, x => x.CategoryId == overCategoryId);
        Assert.Equal(2026, candidate.Year);
        Assert.Equal(3, candidate.Month);
        Assert.Equal("Nadmiarowe", candidate.CategoryName);
        Assert.Equal(121m, candidate.CurrentSpentAmount);
        Assert.Equal(100m, candidate.HistoricalAverageAmount);
        Assert.True(candidate.DeviationPercent > 20m);
        Assert.Equal(20m, candidate.ThresholdPercent);
        Assert.DoesNotContain(result.DeviationAlertCandidates, x => x.CategoryId == stableCategoryId);
        Assert.DoesNotContain(result.DeviationAlertCandidates, x => x.CategoryId == freshCategoryId);
        Assert.Empty(publisher.Events);
    }

    /// <summary>
    /// Seeds expenses for months 1 and 3 only, with one active and one inactive account.
    /// Verifies that PopulatedMonths and AccountBalanceMonths contain only those months
    /// and that the inactive account is excluded from AccountBalances.
    /// </summary>
    [Fact]
    public async Task GetYearStatisticsAsync_Should_Use_Only_Populated_Months_And_Active_Accounts()
    {
        int categoryId;
        int activeAccountId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Transport", Color = "#1E88E5" };
            var activeAccount = new Account { Name = "Bank", Type = (int)AccountType.Bank, Order = 1 };
            var inactiveAccount = new Account { Name = "Gotowka", Type = (int)AccountType.Cash, Order = 2 };

            context.Categories.Add(category);
            context.Accounts.AddRange(activeAccount, inactiveAccount);
            await context.SaveChangesAsync();

            categoryId = category.Id;
            activeAccountId = activeAccount.Id;

            foreach (var month in new[] { 1, 3 })
            {
                var monthPlan = new MonthPlan { Year = 2026, Month = month };
                context.MonthPlans.Add(monthPlan);
                await context.SaveChangesAsync();

                context.Expenses.Add(new Expense
                {
                    MonthPlanId = monthPlan.Id,
                    Order = 1,
                    Name = $"Paliwo {month}",
                    CategoryId = categoryId,
                    PlannedAmount = 100m,
                    ActualAmount = month == 1 ? 120m : 180m,
                    ShowRemainingInUI = true
                });

                context.AccountMonthBalances.Add(new AccountMonthBalance
                {
                    AccountId = activeAccountId,
                    Year = 2026,
                    Month = month,
                    ClosingBalance = month == 1 ? 1000m : 900m
                });
            }

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.GetYearStatisticsAsync(2026, CancellationToken.None);

        Assert.Equal([1, 3], result.PopulatedMonths);
        Assert.Equal([1, 3], result.AccountBalanceMonths);
        var categoryStats = Assert.Single(result.CategoryStatistics, x => x.CategoryId == categoryId);
        Assert.Equal(300m, categoryStats.TotalSpent);
        Assert.Equal(150m, categoryStats.AverageMonthlySpent);

        var accountRow = Assert.Single(result.AccountBalances);
        Assert.Equal(activeAccountId, accountRow.AccountId);
        Assert.Equal(2, accountRow.MonthlyClosingBalances.Count);
    }

    /// <summary>
    /// Verifies that AccountBalances uses null for months before an account became active
    /// so the UI can render the cell as not applicable instead of as a zero balance.
    /// </summary>
    [Fact]
    public async Task GetYearStatisticsAsync_Should_Mark_Months_Before_Account_Activation_As_Not_Applicable()
    {
        int restoredAccountId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Transport", Color = "#1E88E5" };
            var baseAccount = new Account { Name = "Bank", Type = (int)AccountType.Bank, Order = 1 };
            var restoredAccount = new Account
            {
                Name = "Millenium",
                Type = (int)AccountType.Bank,
                Order = 2,
                ActiveFromUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
            };

            context.Categories.Add(category);
            context.Accounts.AddRange(baseAccount, restoredAccount);
            await context.SaveChangesAsync();

            restoredAccountId = restoredAccount.Id;

            foreach (var month in new[] { 1, 3 })
            {
                var monthPlan = new MonthPlan { Year = 2026, Month = month };
                context.MonthPlans.Add(monthPlan);
                await context.SaveChangesAsync();

                context.Expenses.Add(new Expense
                {
                    MonthPlanId = monthPlan.Id,
                    Order = 1,
                    Name = $"Paliwo {month}",
                    CategoryId = category.Id,
                    PlannedAmount = 100m,
                    ActualAmount = 100m,
                    ShowRemainingInUI = true
                });
            }

            context.AccountMonthBalances.AddRange(
                new AccountMonthBalance
                {
                    AccountId = baseAccount.Id,
                    Year = 2026,
                    Month = 1,
                    ClosingBalance = 1000m
                },
                new AccountMonthBalance
                {
                    AccountId = baseAccount.Id,
                    Year = 2026,
                    Month = 3,
                    ClosingBalance = 900m
                },
                new AccountMonthBalance
                {
                    AccountId = restoredAccount.Id,
                    Year = 2026,
                    Month = 3,
                    ClosingBalance = 500m
                });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.GetYearStatisticsAsync(2026, CancellationToken.None);

        Assert.Equal([1, 3], result.AccountBalanceMonths);
        var restoredAccountRow = Assert.Single(result.AccountBalances, x => x.AccountId == restoredAccountId);
        Assert.Equal([null, 500m], restoredAccountRow.MonthlyClosingBalances);
    }

    /// <summary>
    /// Seeds an expense with line items tagged to a child tag; the parent expense uses the parent tag.
    /// Verifies that GetYearStatisticsAsync rolls up line-item amounts to both child and parent tag statistics.
    /// </summary>
    [Fact]
    public async Task GetYearStatisticsAsync_Should_Aggregate_Subtags_From_ExpenseLineItems()
    {
        int categoryId;
        int parentTagId;
        int childTagId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Spozywcze", Color = "#43A047", SupportsLineItems = true };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            categoryId = category.Id;

            var parentTag = new Tag { Name = "Internetowe", CategoryId = categoryId };
            context.Tags.Add(parentTag);
            await context.SaveChangesAsync();

            var childTag = new Tag { Name = "Allegro", CategoryId = categoryId, ParentTagId = parentTag.Id };
            context.Tags.Add(childTag);
            await context.SaveChangesAsync();

            parentTagId = parentTag.Id;
            childTagId = childTag.Id;

            var monthPlan = new MonthPlan { Year = 2026, Month = 1 };
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            var expense = new Expense
            {
                MonthPlanId = monthPlan.Id,
                Name = "Zakupy online",
                CategoryId = categoryId,
                TagId = parentTagId,
                PlannedAmount = 200m,
                ActualAmount = 200m,
                ShowRemainingInUI = true,
                Order = 1
            };

            context.Expenses.Add(expense);
            await context.SaveChangesAsync();

            context.ExpenseLineItems.AddRange(
                new ExpenseLineItem
                {
                    ExpenseId = expense.Id,
                    Description = "Zakup 1",
                    Amount = 120m,
                    OccurredAt = new DateOnly(2026, 1, 10),
                    TagId = childTagId
                },
                new ExpenseLineItem
                {
                    ExpenseId = expense.Id,
                    Description = "Zakup 2",
                    Amount = 80m,
                    OccurredAt = new DateOnly(2026, 1, 11),
                    TagId = childTagId
                });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.GetYearStatisticsAsync(2026, CancellationToken.None);

        var parentStats = Assert.Single(result.CategoryTagStatistics, x => x.CategoryId == categoryId && x.TagId == parentTagId);
        var childStats = Assert.Single(result.CategoryTagStatistics, x => x.CategoryId == categoryId && x.TagId == childTagId);

        Assert.Equal(200m, childStats.TotalSpent);
        Assert.Equal(200m, parentStats.TotalSpent);
        Assert.Equal(200m, result.CategoryStatistics.Single(x => x.CategoryId == categoryId).TotalSpent);
    }

    /// <summary>
    /// Seeds January with actual=100 and February with actual=0.
    /// Verifies that only January appears in PopulatedMonths, AccountBalanceMonths, and MonthlyFinance.
    /// </summary>
    [Fact]
    public async Task GetYearStatisticsAsync_Should_Exclude_Months_With_Zero_Spent_Expenses()
    {
        int categoryId;
        int accountId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Spozywcze", Color = "#43A047" };
            var account = new Account { Name = "Bank", Type = (int)AccountType.Bank, Order = 1 };

            context.Categories.Add(category);
            context.Accounts.Add(account);
            await context.SaveChangesAsync();

            categoryId = category.Id;
            accountId = account.Id;

            var january = new MonthPlan { Year = 2026, Month = 1 };
            var february = new MonthPlan { Year = 2026, Month = 2 };

            context.MonthPlans.AddRange(january, february);
            await context.SaveChangesAsync();

            context.Expenses.AddRange(
                new Expense
                {
                    MonthPlanId = january.Id,
                    Order = 1,
                    Name = "Zakupy styczen",
                    CategoryId = categoryId,
                    PlannedAmount = 100m,
                    ActualAmount = 100m,
                    ShowRemainingInUI = true
                },
                new Expense
                {
                    MonthPlanId = february.Id,
                    Order = 1,
                    Name = "Zakupy luty",
                    CategoryId = categoryId,
                    PlannedAmount = 200m,
                    ActualAmount = 0m,
                    ShowRemainingInUI = true
                });

            context.Incomes.AddRange(
                new Income
                {
                    MonthPlanId = january.Id,
                    Name = "Wyplata 1",
                    Amount = 3000m,
                    AccountId = accountId,
                    ExpectedDayOfMonth = new DateOnly(2026, 1, 10)
                },
                new Income
                {
                    MonthPlanId = february.Id,
                    Name = "Wyplata 2",
                    Amount = 3000m,
                    AccountId = accountId,
                    ExpectedDayOfMonth = new DateOnly(2026, 2, 10)
                });

            context.AccountMonthBalances.AddRange(
                new AccountMonthBalance
                {
                    AccountId = accountId,
                    Year = 2026,
                    Month = 1,
                    ClosingBalance = 1000m
                },
                new AccountMonthBalance
                {
                    AccountId = accountId,
                    Year = 2026,
                    Month = 2,
                    ClosingBalance = 1100m
                });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.GetYearStatisticsAsync(2026, CancellationToken.None);

        Assert.Equal([1], result.PopulatedMonths);
        Assert.Equal([1], result.AccountBalanceMonths);
        Assert.Single(result.MonthlyFinance);
        Assert.Equal(1, result.MonthlyFinance[0].Month);

        var accountRow = Assert.Single(result.AccountBalances);
        Assert.Single(accountRow.MonthlyClosingBalances);
        Assert.Equal(1000m, accountRow.MonthlyClosingBalances[0]);
    }

    /// <summary>
    /// Seeds an expense with a root tag whose line items have no TagId set.
    /// Verifies that untagged line item amounts are assigned to the parent expense's tag in the statistics.
    /// </summary>
    [Fact]
    public async Task GetYearStatisticsAsync_Should_Assign_Untagged_LineItems_To_Expense_Tag()
    {
        int categoryId;
        int rootTagId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Suple", Color = "#43A047", SupportsLineItems = true };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            categoryId = category.Id;

            var rootTag = new Tag { Name = "Suple", CategoryId = categoryId };
            context.Tags.Add(rootTag);
            await context.SaveChangesAsync();

            rootTagId = rootTag.Id;

            var monthPlan = new MonthPlan { Year = 2026, Month = 3 };
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            var expense = new Expense
            {
                MonthPlanId = monthPlan.Id,
                Name = "Zakupy suplementów",
                CategoryId = categoryId,
                TagId = rootTagId,
                PlannedAmount = 150m,
                ActualAmount = 150m,
                ShowRemainingInUI = true,
                Order = 1
            };

            context.Expenses.Add(expense);
            await context.SaveChangesAsync();

            context.ExpenseLineItems.AddRange(
                new ExpenseLineItem
                {
                    ExpenseId = expense.Id,
                    Description = "Magnez",
                    Amount = 50m,
                    OccurredAt = new DateOnly(2026, 3, 5),
                    TagId = null
                },
                new ExpenseLineItem
                {
                    ExpenseId = expense.Id,
                    Description = "Omega 3",
                    Amount = 100m,
                    OccurredAt = new DateOnly(2026, 3, 7),
                    TagId = null
                });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.GetYearStatisticsAsync(2026, CancellationToken.None);

        var rootTagStats = Assert.Single(result.CategoryTagStatistics, x => x.CategoryId == categoryId && x.TagId == rootTagId);
        Assert.Equal(150m, rootTagStats.TotalSpent);

        Assert.DoesNotContain(result.CategoryTagStatistics,
            x => x.CategoryId == categoryId && x.TagId is null && x.TotalSpent > 0);
    }

    /// <summary>
    /// Seeds an expense with two line items and searches with query "farba" (case-insensitive).
    /// Verifies that exactly one result is returned with correct Year, Month, ExpenseName, and MatchingDescription.
    /// </summary>
    [Fact]
    public async Task SearchExpenseHistoryAsync_Should_Filter_By_Description_And_Return_Edit_Context()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Dom", Color = "#455A64", SupportsLineItems = true };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;

            var monthPlan = new MonthPlan { Year = 2025, Month = 11 };
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            var expense = new Expense
            {
                MonthPlanId = monthPlan.Id,
                Name = "Zakupy remont",
                CategoryId = categoryId,
                PlannedAmount = 300m,
                ActualAmount = 280m,
                ShowRemainingInUI = true,
                Order = 1
            };

            context.Expenses.Add(expense);
            await context.SaveChangesAsync();

            context.ExpenseLineItems.AddRange(
                new ExpenseLineItem
                {
                    ExpenseId = expense.Id,
                    Description = "Farba scienna",
                    Amount = 200m,
                    OccurredAt = new DateOnly(2025, 11, 10)
                },
                new ExpenseLineItem
                {
                    ExpenseId = expense.Id,
                    Description = "Pedzle",
                    Amount = 80m,
                    OccurredAt = new DateOnly(2025, 11, 11)
                });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.SearchExpenseHistoryAsync(new SearchExpenseHistoryRequest
        {
            CategoryId = categoryId,
            Query = "farba"
        }, CancellationToken.None);

        var found = Assert.Single(result);
        Assert.Equal(2025, found.Year);
        Assert.Equal(11, found.Month);
        Assert.Equal("Zakupy remont", found.ExpenseName);
        Assert.Equal("Farba scienna", found.MatchingDescription);
    }

    [Fact]
    public async Task SearchExpenseHistoryAsync_Should_Filter_Tag_Hierarchy_On_The_Same_Line_Item()
    {
        int foodCategoryId;
        int rootTagId;
        int internetTagId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var food = new Category { Name = "Spożywcze", Color = "#43A047", SupportsLineItems = true };
            context.Categories.Add(food);
            await context.SaveChangesAsync();
            foodCategoryId = food.Id;

            var groceriesTag = new Tag { CategoryId = foodCategoryId, Name = "Sklep" };
            context.Tags.Add(groceriesTag);
            await context.SaveChangesAsync();
            rootTagId = groceriesTag.Id;

            var internetTag = new Tag
            {
                CategoryId = foodCategoryId,
                ParentTagId = rootTagId,
                Name = "Internetowe"
            };
            context.Tags.Add(internetTag);
            await context.SaveChangesAsync();
            internetTagId = internetTag.Id;

            var monthPlan = new MonthPlan { Year = 2026, Month = 4 };
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            var mixedExpense = new Expense
            {
                MonthPlanId = monthPlan.Id,
                Name = "Zakupy spożywcze",
                CategoryId = foodCategoryId,
                TagId = rootTagId,
                PlannedAmount = 120m,
                ActualAmount = 120m,
                Order = 1
            };
            context.Expenses.Add(mixedExpense);
            await context.SaveChangesAsync();

            context.ExpenseLineItems.AddRange(
                new ExpenseLineItem
                {
                    ExpenseId = mixedExpense.Id,
                    Description = "Warzywa",
                    Amount = 80m,
                    OccurredAt = new DateOnly(2026, 4, 3)
                },
                new ExpenseLineItem
                {
                    ExpenseId = mixedExpense.Id,
                    Description = "Internetowe zamówienie kawy",
                    Amount = 40m,
                    OccurredAt = new DateOnly(2026, 4, 4),
                    TagId = internetTagId
                });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.SearchExpenseHistoryAsync(new SearchExpenseHistoryRequest
        {
            CategoryId = foodCategoryId,
            RootTagId = rootTagId,
            SubTagId = internetTagId
        }, CancellationToken.None);

        var found = Assert.Single(result);
        Assert.Equal("Zakupy spożywcze", found.ExpenseName);
        Assert.Equal("Internetowe zamówienie kawy", found.MatchingDescription);
        Assert.Equal(40m, found.ActualAmount);
        Assert.Equal(rootTagId, found.RootTagId);
        Assert.Equal("Sklep", found.RootTagName);
        Assert.Equal(internetTagId, found.SubTagId);
        Assert.Equal("Internetowe", found.SubTagName);
    }

    /// <summary>
    /// Seeds expenses for two categories across three years and requests totals for the home category only.
    /// Verifies TotalSpent=1350, FirstYear=2024, LastYear=2025 for the filtered category.
    /// </summary>
    [Fact]
    public async Task GetCategoryLifetimeExpenseTotalsAsync_Should_Return_Filtered_Category_Sum_For_All_Years()
    {
        int homeCategoryId;
        int foodCategoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var home = new Category { Name = "Dom", Color = "#6D4C41" };
            var food = new Category { Name = "Spozywcze", Color = "#43A047" };
            context.Categories.AddRange(home, food);
            await context.SaveChangesAsync();

            homeCategoryId = home.Id;
            foodCategoryId = food.Id;

            var monthPlans = new[]
            {
                new MonthPlan { Year = 2024, Month = 4 },
                new MonthPlan { Year = 2025, Month = 6 },
                new MonthPlan { Year = 2026, Month = 2 }
            };

            context.MonthPlans.AddRange(monthPlans);
            await context.SaveChangesAsync();

            context.Expenses.AddRange(
                new Expense
                {
                    MonthPlanId = monthPlans[0].Id,
                    Name = "Naprawa",
                    CategoryId = homeCategoryId,
                    PlannedAmount = 500m,
                    ActualAmount = 450m,
                    ShowRemainingInUI = true,
                    Order = 1
                },
                new Expense
                {
                    MonthPlanId = monthPlans[1].Id,
                    Name = "Meble",
                    CategoryId = homeCategoryId,
                    PlannedAmount = 900m,
                    ActualAmount = 900m,
                    ShowRemainingInUI = true,
                    Order = 1
                },
                new Expense
                {
                    MonthPlanId = monthPlans[2].Id,
                    Name = "Zakupy",
                    CategoryId = foodCategoryId,
                    PlannedAmount = 200m,
                    ActualAmount = 200m,
                    ShowRemainingInUI = true,
                    Order = 1
                });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.GetCategoryLifetimeExpenseTotalsAsync([homeCategoryId], null, null, CancellationToken.None);

        var homeTotal = Assert.Single(result);
        Assert.Equal(homeCategoryId, homeTotal.CategoryId);
        Assert.Equal(1350m, homeTotal.TotalSpent);
        Assert.Equal(2024, homeTotal.FirstYear);
        Assert.Equal(2025, homeTotal.LastYear);
    }

    /// <summary>
    /// Seeds two months of expenses, incomes, account balances, and a savings transfer.
    /// Verifies TransactionCount, UnplannedSpentTotal, SavedAmountThisMonth, SavedAmountYearToDate,
    /// AverageMonthlyIncome, AverageMonthlySpent, AverageMonthlySaved, and SavingsTimeline for month 2.
    /// </summary>
    [Fact]
    public async Task GetDashboardSummaryAsync_Should_Calculate_Month_And_Ytd_Metrics()
    {
        int categoryId;
        int accountId;
        int savingsAccountId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Spozywcze", Color = "#43A047" };
            var account = new Account { Name = "Bank", Type = (int)AccountType.Bank };
            var savingsAccount = new Account { Name = "Skarbonka", Type = (int)AccountType.Savings };
            context.Categories.Add(category);
            context.Accounts.Add(account);
            context.Accounts.Add(savingsAccount);
            await context.SaveChangesAsync();
            categoryId = category.Id;
            accountId = account.Id;
            savingsAccountId = savingsAccount.Id;

            var january = new MonthPlan { Year = 2026, Month = 1 };
            var february = new MonthPlan { Year = 2026, Month = 2 };
            context.MonthPlans.AddRange(january, february);
            await context.SaveChangesAsync();

            context.AccountMonthBalances.AddRange(
                new AccountMonthBalance
                {
                    AccountId = accountId,
                    Year = 2025,
                    Month = 12,
                    ClosingBalance = 900m
                },
                new AccountMonthBalance
                {
                    AccountId = accountId,
                    Year = 2026,
                    Month = 1,
                    ClosingBalance = 1000m
                },
                new AccountMonthBalance
                {
                    AccountId = accountId,
                    Year = 2026,
                    Month = 2,
                    ClosingBalance = 1100m
                },
                new AccountMonthBalance
                {
                    AccountId = savingsAccountId,
                    Year = 2025,
                    Month = 12,
                    ClosingBalance = 150m
                },
                new AccountMonthBalance
                {
                    AccountId = savingsAccountId,
                    Year = 2026,
                    Month = 1,
                    ClosingBalance = 200m
                },
                new AccountMonthBalance
                {
                    AccountId = savingsAccountId,
                    Year = 2026,
                    Month = 2,
                    ClosingBalance = 260m
                });

            context.Expenses.AddRange(
                new Expense
                {
                    MonthPlanId = january.Id,
                    Order = 1,
                    Name = "Zakupy styczen",
                    CategoryId = categoryId,
                    PlannedAmount = 100m,
                    ActualAmount = 60m,
                    ShowRemainingInUI = true
                },
                new Expense
                {
                    MonthPlanId = february.Id,
                    Order = 1,
                    Name = "Zakupy luty",
                    CategoryId = categoryId,
                    PlannedAmount = 200m,
                    ActualAmount = 250m,
                    ShowRemainingInUI = true
                },
                new Expense
                {
                    MonthPlanId = february.Id,
                    Order = 2,
                    Name = "Nieplanowany",
                    CategoryId = categoryId,
                    PlannedAmount = 0m,
                    ActualAmount = 30m,
                    ShowRemainingInUI = true
                });

            context.Incomes.AddRange(
                new Income
                {
                    MonthPlanId = january.Id,
                    Name = "Wyplata 1",
                    Amount = 500m,
                    AccountId = accountId,
                    ExpectedDayOfMonth = new DateOnly(2026, 1, 10)
                },
                new Income
                {
                    MonthPlanId = february.Id,
                    Name = "Wyplata 2",
                    Amount = 600m,
                    AccountId = accountId,
                    ExpectedDayOfMonth = new DateOnly(2026, 2, 10)
                });

            context.MonthSavingsTransferItems.Add(new MonthSavingsTransferItem
            {
                MonthPlanId = february.Id,
                Amount = 200m,
                TransferDate = new DateOnly(2026, 2, 12)
            });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var summary = await service.GetDashboardSummaryAsync(2026, 2, CancellationToken.None);

        Assert.Equal(4, summary.TransactionCount);
        Assert.Equal(80m, summary.UnplannedSpentTotal);
        Assert.Equal(160m, summary.SavedAmountThisMonth);
        Assert.Equal(310m, summary.SavedAmountYearToDate);
        Assert.Equal(550m, summary.AverageMonthlyIncome);
        Assert.Equal(170m, summary.AverageMonthlySpent);
        Assert.Equal(155m, summary.AverageMonthlySaved);
        Assert.Equal(2, summary.SavingsTimeline.Count);
        Assert.Equal(150m, summary.SavingsTimeline.Single(x => x.Month == 1).SavedAmount);
        Assert.Equal(160m, summary.SavingsTimeline.Single(x => x.Month == 2).SavedAmount);
    }

    /// <summary>
    /// Verifies that the dashboard YTD savings timeline starts from the first plan month
    /// owned by the current budget instead of rendering empty months from January.
    /// </summary>
    [Fact]
    public async Task GetDashboardSummaryAsync_Should_Start_Ytd_Timeline_From_First_Available_MonthPlan()
    {
        await CreateMonthPlanAsync(2026, 3);
        await CreateMonthPlanAsync(2026, 6);

        var service = CreateService();
        var summary = await service.GetDashboardSummaryAsync(2026, 6, CancellationToken.None);

        Assert.Equal([3, 4, 5, 6], summary.SavingsTimeline.Select(x => x.Month).ToArray());
        Assert.DoesNotContain(summary.SavingsTimeline, x => x.Month is 1 or 2);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_Should_Not_Carry_Archived_Account_Balance_Into_Current_Month()
    {
        int accountId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var account = new Account
            {
                Name = "Archived cash",
                Type = (int)AccountType.Cash,
                IsArchived = true,
                ArchivedAtUtc = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc),
                UpdatedAtUtc = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc)
            };

            context.Accounts.Add(account);
            context.MonthPlans.AddRange(
                new MonthPlan { Year = 2026, Month = 5 },
                new MonthPlan { Year = 2026, Month = 6 });
            await context.SaveChangesAsync();

            accountId = account.Id;
            context.AccountMonthBalances.Add(new AccountMonthBalance
            {
                AccountId = accountId,
                Year = 2026,
                Month = 5,
                ClosingBalance = 200m
            });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var summary = await service.GetDashboardSummaryAsync(2026, 6, CancellationToken.None);

        Assert.Equal(-200m, summary.SavedAmountThisMonth);
        Assert.Equal(200m, summary.SavingsTimeline.Single(x => x.Month == 5).SavedAmount);
        Assert.Equal(-200m, summary.SavingsTimeline.Single(x => x.Month == 6).SavedAmount);
    }

    /// <summary>
    /// Seeds one regular expense and one SupportsLineItems expense that has two line items.
    /// Verifies that TransactionCount=3 (1 regular + 2 line items) instead of 2 (1 regular + 1 parent).
    /// </summary>
    [Fact]
    public async Task GetDashboardSummaryAsync_Should_Count_LineItems_Instead_Of_Parent_Expense_When_SupportsLineItems()
    {
        int regularCategoryId;
        int lineItemsCategoryId;
        int monthPlanId;
        int supportLineExpenseId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var regularCategory = new Category
            {
                Name = "Transport",
                Color = "#43A047",
                SupportsLineItems = false
            };

            var lineItemsCategory = new Category
            {
                Name = "Spozywcze",
                Color = "#2E7D32",
                SupportsLineItems = true
            };

            context.Categories.AddRange(regularCategory, lineItemsCategory);
            await context.SaveChangesAsync();

            regularCategoryId = regularCategory.Id;
            lineItemsCategoryId = lineItemsCategory.Id;

            var monthPlan = new MonthPlan { Year = 2026, Month = 3 };
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();
            monthPlanId = monthPlan.Id;

            context.Expenses.AddRange(
                new Expense
                {
                    MonthPlanId = monthPlanId,
                    Order = 1,
                    Name = "Paliwo",
                    CategoryId = regularCategoryId,
                    PlannedAmount = 120m,
                    ActualAmount = 120m,
                    ShowRemainingInUI = true
                },
                new Expense
                {
                    MonthPlanId = monthPlanId,
                    Order = 2,
                    Name = "Zakupy tygodniowe",
                    CategoryId = lineItemsCategoryId,
                    PlannedAmount = 300m,
                    ActualAmount = 0m,
                    ShowRemainingInUI = true
                });

            await context.SaveChangesAsync();

            supportLineExpenseId = await context.Expenses
                .Where(x => x.MonthPlanId == monthPlanId && x.Name == "Zakupy tygodniowe")
                .Select(x => x.Id)
                .SingleAsync();

            context.ExpenseLineItems.AddRange(
                new ExpenseLineItem
                {
                    ExpenseId = supportLineExpenseId,
                    Description = "Sklep 1",
                    Amount = 55m,
                    OccurredAt = new DateOnly(2026, 3, 10)
                },
                new ExpenseLineItem
                {
                    ExpenseId = supportLineExpenseId,
                    Description = "Sklep 2",
                    Amount = 75m,
                    OccurredAt = new DateOnly(2026, 3, 15)
                });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var summary = await service.GetDashboardSummaryAsync(2026, 3, CancellationToken.None);

        Assert.Equal(3, summary.TransactionCount);
    }

    /// <summary>
    /// Seeds one regular expense and one SupportsLineItems expense with no line items.
    /// Verifies that TransactionCount=1 (only the regular expense) because the parent is excluded when SupportsLineItems is true.
    /// </summary>
    [Fact]
    public async Task GetDashboardSummaryAsync_Should_Not_Count_Parent_Expense_When_SupportsLineItems_But_No_LineItems()
    {
        int regularCategoryId;
        int lineItemsCategoryId;
        int monthPlanId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var regularCategory = new Category
            {
                Name = "Transport",
                Color = "#43A047",
                SupportsLineItems = false
            };

            var lineItemsCategory = new Category
            {
                Name = "Spozywcze",
                Color = "#2E7D32",
                SupportsLineItems = true
            };

            context.Categories.AddRange(regularCategory, lineItemsCategory);
            await context.SaveChangesAsync();

            regularCategoryId = regularCategory.Id;
            lineItemsCategoryId = lineItemsCategory.Id;

            var monthPlan = new MonthPlan { Year = 2026, Month = 4 };
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();
            monthPlanId = monthPlan.Id;

            context.Expenses.AddRange(
                new Expense
                {
                    MonthPlanId = monthPlanId,
                    Order = 1,
                    Name = "Paliwo",
                    CategoryId = regularCategoryId,
                    PlannedAmount = 120m,
                    ActualAmount = 120m,
                    ShowRemainingInUI = true
                },
                new Expense
                {
                    MonthPlanId = monthPlanId,
                    Order = 2,
                    Name = "Zakupy tygodniowe",
                    CategoryId = lineItemsCategoryId,
                    PlannedAmount = 300m,
                    ActualAmount = 0m,
                    ShowRemainingInUI = true
                });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var summary = await service.GetDashboardSummaryAsync(2026, 4, CancellationToken.None);

        Assert.Equal(1, summary.TransactionCount);
    }

    /// <summary>
    /// Seeds a planned expense with ActualAmount=0 and a zero-planned/zero-actual expense.
    /// Verifies that TransactionCount=0 because neither qualifies as a realized transaction.
    /// </summary>
    [Fact]
    public async Task GetDashboardSummaryAsync_Should_Not_Count_Planned_Expense_With_Zero_Actual_When_No_LineItems()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category
            {
                Name = "Dom",
                Color = "#455A64",
                SupportsLineItems = false
            };

            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;

            var monthPlan = new MonthPlan { Year = 2026, Month = 5 };
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            context.Expenses.AddRange(
                new Expense
                {
                    MonthPlanId = monthPlan.Id,
                    Order = 1,
                    Name = "Planowany bez realizacji",
                    CategoryId = categoryId,
                    PlannedAmount = 200m,
                    ActualAmount = 0m,
                    ShowRemainingInUI = true
                },
                new Expense
                {
                    MonthPlanId = monthPlan.Id,
                    Order = 2,
                    Name = "Nieplanowany",
                    CategoryId = categoryId,
                    PlannedAmount = 0m,
                    ActualAmount = 0m,
                    ShowRemainingInUI = true
                });

            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var summary = await service.GetDashboardSummaryAsync(2026, 5, CancellationToken.None);

        Assert.Equal(0, summary.TransactionCount);
    }

    /// <summary>
    /// Seeds one expense, two incomes (one past, one future), and two savings transfers (one past, one future)
    /// with today set to 2026-06-10. Verifies that TransactionCount=3 (1 expense + 1 income + 1 transfer).
    /// </summary>
    [Fact]
    public async Task GetDashboardSummaryAsync_Should_Not_Count_Future_Incomes_And_Transfers_In_Current_Month()
    {
        int categoryId;
        int accountId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category
            {
                Name = "Dom",
                Color = "#455A64",
                SupportsLineItems = false
            };

            var account = new Account
            {
                Name = "Konto glowne",
                Type = (int)AccountType.Bank
            };

            context.Categories.Add(category);
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
            categoryId = category.Id;
            accountId = account.Id;

            var monthPlan = new MonthPlan { Year = 2026, Month = 6 };
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            context.Expenses.Add(new Expense
            {
                MonthPlanId = monthPlan.Id,
                Order = 1,
                Name = "Zaplacony rachunek",
                CategoryId = categoryId,
                PlannedAmount = 100m,
                ActualAmount = 100m,
                ShowRemainingInUI = true
            });

            context.Incomes.AddRange(
                new Income
                {
                    MonthPlanId = monthPlan.Id,
                    Name = "Wyplata przyszla",
                    Amount = 500m,
                    AccountId = accountId,
                    ExpectedDayOfMonth = new DateOnly(2026, 6, 20)
                },
                new Income
                {
                    MonthPlanId = monthPlan.Id,
                    Name = "Wyplata juz doszla",
                    Amount = 700m,
                    AccountId = accountId,
                    ExpectedDayOfMonth = new DateOnly(2026, 6, 5)
                });

            context.MonthSavingsTransferItems.AddRange(
                new MonthSavingsTransferItem
                {
                    MonthPlanId = monthPlan.Id,
                    Amount = 250m,
                    TransferDate = new DateOnly(2026, 6, 25)
                },
                new MonthSavingsTransferItem
                {
                    MonthPlanId = monthPlan.Id,
                    Amount = 300m,
                    TransferDate = new DateOnly(2026, 6, 8)
                });

            await context.SaveChangesAsync();
        }

        var service = CreateService(nowUtc: new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc));
        var summary = await service.GetDashboardSummaryAsync(2026, 6, CancellationToken.None);

        Assert.Equal(3, summary.TransactionCount);
    }

    // ─── UpdateExpenseAsync ─────────────────────────────────────────────────────

    /// <summary>
    /// Calls UpdateExpenseAsync with an ID that does not exist in the database.
    /// Verifies that a NotFoundException is thrown.
    /// </summary>
    [Fact]
    public async Task UpdateExpenseAsync_Should_Throw_NotFoundException_When_Expense_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateExpenseAsync(new UpdateExpenseRequest
        {
            Id = 99999,
            Name = "Ghost",
            CategoryId = 1,
            PlannedAmount = 100m,
            ActualAmount = 0m,
            ShowRemainingInUI = true
        }, CancellationToken.None));
    }

    /// <summary>
    /// Closes a month and then attempts to update an expense in that month.
    /// Verifies that a BadRequestException is thrown.
    /// </summary>
    [Fact]
    public async Task UpdateExpenseAsync_Should_Throw_When_Month_Is_Closed()
    {
        int expenseId;
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Dom", Color = "#455A64" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;

            var monthPlan = new MonthPlan { Year = 2026, Month = 3, IsClosed = true };
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            var expense = new Expense
            {
                MonthPlanId = monthPlan.Id,
                Name = "Zakupy",
                CategoryId = categoryId,
                PlannedAmount = 100m,
                ActualAmount = 50m,
                ShowRemainingInUI = true
            };
            context.Expenses.Add(expense);
            await context.SaveChangesAsync();
            expenseId = expense.Id;
        }

        var service = CreateService();
        await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateExpenseAsync(new UpdateExpenseRequest
        {
            Id = expenseId,
            Name = "Zakupy zmienione",
            CategoryId = categoryId,
            PlannedAmount = 100m,
            ActualAmount = 60m,
            ShowRemainingInUI = true
        }, CancellationToken.None));
    }

    /// <summary>
    /// Updates an expense's name and amounts and verifies that the changes are persisted.
    /// </summary>
    [Fact]
    public async Task UpdateExpenseAsync_Should_Persist_Changes()
    {
        int expenseId;
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Transport", Color = "#1E88E5" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;

            var monthPlan = new MonthPlan { Year = 2026, Month = 2 };
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            var expense = new Expense
            {
                MonthPlanId = monthPlan.Id,
                Name = "Paliwo",
                CategoryId = categoryId,
                PlannedAmount = 200m,
                ActualAmount = 180m,
                ShowRemainingInUI = true
            };
            context.Expenses.Add(expense);
            await context.SaveChangesAsync();
            expenseId = expense.Id;
        }

        var service = CreateService();
        var result = await service.UpdateExpenseAsync(new UpdateExpenseRequest
        {
            Id = expenseId,
            Name = "Paliwo zaktualizowane",
            CategoryId = categoryId,
            PlannedAmount = 220m,
            ActualAmount = 190m,
            ShowRemainingInUI = false
        }, CancellationToken.None);

        Assert.Equal("Paliwo zaktualizowane", result.Name);
        Assert.Equal(220m, result.PlannedAmount);
        Assert.Equal(190m, result.ActualAmount);
        Assert.False(result.ShowRemainingInUI);
    }

    /// <summary>
    /// Updates an expense that already has line items and verifies that request ActualAmount is ignored.
    /// </summary>
    [Fact]
    public async Task UpdateExpenseAsync_Should_Ignore_Request_ActualAmount_When_LineItems_Exist()
    {
        int expenseId;
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Spozywcze", Color = "#43A047", SupportsLineItems = true };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;

            var monthPlan = new MonthPlan { Year = 2026, Month = 4 };
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            var expense = new Expense
            {
                MonthPlanId = monthPlan.Id,
                Name = "Zakupy",
                CategoryId = categoryId,
                PlannedAmount = 200m,
                ActualAmount = 10m,
                ShowRemainingInUI = true
            };
            context.Expenses.Add(expense);
            await context.SaveChangesAsync();
            expenseId = expense.Id;

            context.ExpenseLineItems.AddRange(
                new ExpenseLineItem
                {
                    ExpenseId = expenseId,
                    Description = "Chleb",
                    Amount = 40m,
                    OccurredAt = new DateOnly(2026, 4, 5)
                },
                new ExpenseLineItem
                {
                    ExpenseId = expenseId,
                    Description = "Mleko",
                    Amount = 30m,
                    OccurredAt = new DateOnly(2026, 4, 6)
                });
            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var result = await service.UpdateExpenseAsync(new UpdateExpenseRequest
        {
            Id = expenseId,
            Name = "Zakupy zaktualizowane",
            CategoryId = categoryId,
            PlannedAmount = 220m,
            ActualAmount = 999m,
            ShowRemainingInUI = false
        }, CancellationToken.None);

        Assert.Equal("Zakupy zaktualizowane", result.Name);
        Assert.Equal(220m, result.PlannedAmount);
        Assert.Equal(70m, result.ActualAmount);
        Assert.False(result.ShowRemainingInUI);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var updatedExpense = await verifyContext.Expenses.SingleAsync(x => x.Id == expenseId);
        Assert.Equal(70m, updatedExpense.ActualAmount);
    }

    // ─── DeleteExpenseAsync ─────────────────────────────────────────────────────

    /// <summary>
    /// Calls DeleteExpenseAsync with an ID that does not exist.
    /// Verifies that a NotFoundException is thrown.
    /// </summary>
    [Fact]
    public async Task DeleteExpenseAsync_Should_Throw_NotFoundException_When_Expense_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteExpenseAsync(
            new DeleteExpenseRequest { Id = 99999 }, CancellationToken.None));
    }

    // ─── CreateExpenseLineItemAsync ─────────────────────────────────────────────

    /// <summary>
    /// Creates a line item for an expense that belongs to a SupportsLineItems category.
    /// Verifies that the line item is returned with correct fields and that the parent
    /// expense's ActualAmount is recalculated from line items.
    /// </summary>
    [Fact]
    public async Task CreateExpenseLineItemAsync_Should_Create_LineItem_And_Recalculate_ActualAmount()
    {
        int expenseId;
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Spozywcze", Color = "#43A047", SupportsLineItems = true };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;

            var monthPlan = new MonthPlan { Year = 2026, Month = 1 };
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            var expense = new Expense
            {
                MonthPlanId = monthPlan.Id,
                Name = "Zakupy",
                CategoryId = categoryId,
                PlannedAmount = 200m,
                ActualAmount = 0m,
                ShowRemainingInUI = true
            };
            context.Expenses.Add(expense);
            await context.SaveChangesAsync();
            expenseId = expense.Id;
        }

        var service = CreateService();
        var lineItem = await service.CreateExpenseLineItemAsync(new CreateExpenseLineItemRequest
        {
            ExpenseId = expenseId,
            Description = "Chleb",
            Amount = 45m,
            OccurredAt = new DateOnly(2026, 1, 5)
        }, CancellationToken.None);

        Assert.Equal(expenseId, lineItem.ExpenseId);
        Assert.Equal("Chleb", lineItem.Description);
        Assert.Equal(45m, lineItem.Amount);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var updatedExpense = await verifyContext.Expenses.SingleAsync(x => x.Id == expenseId);
        Assert.Equal(45m, updatedExpense.ActualAmount);
    }

    /// <summary>
    /// Attempts to create a line item for an expense in a category with SupportsLineItems=false.
    /// Verifies that a BadRequestException is thrown.
    /// </summary>
    [Fact]
    public async Task CreateExpenseLineItemAsync_Should_Throw_When_Category_Does_Not_Support_LineItems()
    {
        int expenseId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Transport", Color = "#1E88E5", SupportsLineItems = false };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var monthPlan = new MonthPlan { Year = 2026, Month = 1 };
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            var expense = new Expense
            {
                MonthPlanId = monthPlan.Id,
                Name = "Paliwo",
                CategoryId = category.Id,
                PlannedAmount = 200m,
                ActualAmount = 150m,
                ShowRemainingInUI = true
            };
            context.Expenses.Add(expense);
            await context.SaveChangesAsync();
            expenseId = expense.Id;
        }

        var service = CreateService();
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateExpenseLineItemAsync(
            new CreateExpenseLineItemRequest
            {
                ExpenseId = expenseId,
                Description = "Benzyna",
                Amount = 80m,
                OccurredAt = new DateOnly(2026, 1, 10)
            }, CancellationToken.None));
    }

    /// <summary>
    /// Calls CreateExpenseLineItemAsync with an ExpenseId that does not exist.
    /// Verifies that a NotFoundException is thrown.
    /// </summary>
    [Fact]
    public async Task CreateExpenseLineItemAsync_Should_Throw_NotFoundException_When_Expense_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateExpenseLineItemAsync(
            new CreateExpenseLineItemRequest
            {
                ExpenseId = 99999,
                Description = "Ghost",
                Amount = 10m,
                OccurredAt = new DateOnly(2026, 1, 1)
            }, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that negative line item amounts are rejected at the service boundary.
    /// </summary>
    [Fact]
    public async Task CreateExpenseLineItemAsync_Should_Reject_Negative_Amount()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateExpenseLineItemAsync(
            new CreateExpenseLineItemRequest
            {
                ExpenseId = 1,
                Description = "Ujemna pozycja",
                Amount = -1m,
                OccurredAt = new DateOnly(2026, 1, 1)
            }, CancellationToken.None));
    }

    // ─── UpdateExpenseLineItemAsync ─────────────────────────────────────────────

    /// <summary>
    /// Updates an existing line item's description and amount and verifies the changes are persisted
    /// and the parent expense ActualAmount is recalculated.
    /// </summary>
    [Fact]
    public async Task UpdateExpenseLineItemAsync_Should_Update_Fields_And_Recalculate_ActualAmount()
    {
        int lineItemId;
        int expenseId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Spozywcze", Color = "#43A047", SupportsLineItems = true };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var monthPlan = new MonthPlan { Year = 2026, Month = 2 };
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            var expense = new Expense
            {
                MonthPlanId = monthPlan.Id,
                Name = "Tygodniowe zakupy",
                CategoryId = category.Id,
                PlannedAmount = 200m,
                ActualAmount = 30m,
                ShowRemainingInUI = true
            };
            context.Expenses.Add(expense);
            await context.SaveChangesAsync();
            expenseId = expense.Id;

            var lineItem = new ExpenseLineItem
            {
                ExpenseId = expenseId,
                Description = "Mleko",
                Amount = 30m,
                OccurredAt = new DateOnly(2026, 2, 5)
            };
            context.ExpenseLineItems.Add(lineItem);
            await context.SaveChangesAsync();
            lineItemId = lineItem.Id;
        }

        var service = CreateService();
        var result = await service.UpdateExpenseLineItemAsync(new UpdateExpenseLineItemRequest
        {
            Id = lineItemId,
            Description = "Mleko zmienione",
            Amount = 55m,
            OccurredAt = new DateOnly(2026, 2, 6)
        }, CancellationToken.None);

        Assert.Equal("Mleko zmienione", result.Description);
        Assert.Equal(55m, result.Amount);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var updatedExpense = await verifyContext.Expenses.SingleAsync(x => x.Id == expenseId);
        Assert.Equal(55m, updatedExpense.ActualAmount);
    }

    /// <summary>
    /// Calls UpdateExpenseLineItemAsync with an ID that does not exist.
    /// Verifies that a NotFoundException is thrown.
    /// </summary>
    [Fact]
    public async Task UpdateExpenseLineItemAsync_Should_Throw_NotFoundException_When_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateExpenseLineItemAsync(
            new UpdateExpenseLineItemRequest
            {
                Id = 99999,
                Description = "Ghost",
                Amount = 10m,
                OccurredAt = new DateOnly(2026, 1, 1)
            }, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that editing a line item cannot introduce a negative amount.
    /// </summary>
    [Fact]
    public async Task UpdateExpenseLineItemAsync_Should_Reject_Negative_Amount()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateExpenseLineItemAsync(
            new UpdateExpenseLineItemRequest
            {
                Id = 1,
                Description = "Ujemna pozycja",
                Amount = -1m,
                OccurredAt = new DateOnly(2026, 1, 1)
            }, CancellationToken.None));
    }

    // ─── DeleteExpenseLineItemAsync ─────────────────────────────────────────────

    /// <summary>
    /// Deletes a line item from an expense and verifies the row is removed.
    /// Note: when all line items are deleted, ActualAmount retains its last calculated value
    /// (recalculation is skipped when LineItems.Count == 0).
    /// </summary>
    [Fact]
    public async Task DeleteExpenseLineItemAsync_Should_Remove_LineItem_And_Recalculate_ActualAmount()
    {
        int lineItemId;
        int expenseId;
        int secondLineItemId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Spozywcze", Color = "#43A047", SupportsLineItems = true };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var monthPlan = new MonthPlan { Year = 2026, Month = 3 };
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            var expense = new Expense
            {
                MonthPlanId = monthPlan.Id,
                Name = "Tygodniowe zakupy",
                CategoryId = category.Id,
                PlannedAmount = 150m,
                ActualAmount = 70m,
                ShowRemainingInUI = true
            };
            context.Expenses.Add(expense);
            await context.SaveChangesAsync();
            expenseId = expense.Id;

            var lineItem = new ExpenseLineItem
            {
                ExpenseId = expenseId,
                Description = "Herbata",
                Amount = 40m,
                OccurredAt = new DateOnly(2026, 3, 5)
            };
            var secondLineItem = new ExpenseLineItem
            {
                ExpenseId = expenseId,
                Description = "Kawa",
                Amount = 30m,
                OccurredAt = new DateOnly(2026, 3, 6)
            };
            context.ExpenseLineItems.AddRange(lineItem, secondLineItem);
            await context.SaveChangesAsync();
            lineItemId = lineItem.Id;
            secondLineItemId = secondLineItem.Id;
        }

        var service = CreateService();
        await service.DeleteExpenseLineItemAsync(
            new DeleteExpenseLineItemRequest { Id = lineItemId }, CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var lineItemExists = await verifyContext.ExpenseLineItems.AnyAsync(x => x.Id == lineItemId);
        Assert.False(lineItemExists);

        // After deleting one of two line items, ActualAmount is recalculated to remaining line item sum.
        var updatedExpense = await verifyContext.Expenses.SingleAsync(x => x.Id == expenseId);
        Assert.Equal(30m, updatedExpense.ActualAmount);
    }

    /// <summary>
    /// Deletes the final line item and verifies the parent expense keeps its last calculated ActualAmount.
    /// </summary>
    [Fact]
    public async Task DeleteExpenseLineItemAsync_Should_Preserve_Last_Calculated_ActualAmount_When_Last_LineItem_Is_Removed()
    {
        int lineItemId;
        int expenseId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Spozywcze", Color = "#43A047", SupportsLineItems = true };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var monthPlan = new MonthPlan { Year = 2026, Month = 4 };
            context.MonthPlans.Add(monthPlan);
            await context.SaveChangesAsync();

            var expense = new Expense
            {
                MonthPlanId = monthPlan.Id,
                Name = "Tygodniowe zakupy",
                CategoryId = category.Id,
                PlannedAmount = 150m,
                ActualAmount = 70m,
                ShowRemainingInUI = true
            };
            context.Expenses.Add(expense);
            await context.SaveChangesAsync();
            expenseId = expense.Id;

            var lineItem = new ExpenseLineItem
            {
                ExpenseId = expenseId,
                Description = "Herbata",
                Amount = 70m,
                OccurredAt = new DateOnly(2026, 4, 5)
            };
            context.ExpenseLineItems.Add(lineItem);
            await context.SaveChangesAsync();
            lineItemId = lineItem.Id;
        }

        var service = CreateService();
        await service.DeleteExpenseLineItemAsync(
            new DeleteExpenseLineItemRequest { Id = lineItemId }, CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var lineItemExists = await verifyContext.ExpenseLineItems.AnyAsync(x => x.Id == lineItemId);
        Assert.False(lineItemExists);

        var updatedExpense = await verifyContext.Expenses.SingleAsync(x => x.Id == expenseId);
        Assert.Equal(70m, updatedExpense.ActualAmount);
    }

    /// <summary>
    /// Calls DeleteExpenseLineItemAsync with an ID that does not exist.
    /// Verifies that a NotFoundException is thrown.
    /// </summary>
    [Fact]
    public async Task DeleteExpenseLineItemAsync_Should_Throw_NotFoundException_When_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteExpenseLineItemAsync(
            new DeleteExpenseLineItemRequest { Id = 99999 }, CancellationToken.None));
    }

    // ─── UpdateRegularExpenseDefinitionAsync ────────────────────────────────────

    /// <summary>
    /// Updates an existing regular expense definition and verifies that the new name and amount
    /// are persisted correctly.
    /// </summary>
    [Fact]
    public async Task UpdateRegularExpenseDefinitionAsync_Should_Persist_Changes()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Rachunki", Color = "#455A64" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var service = CreateService();
        var created = await service.CreateRegularExpenseDefinitionAsync(new CreateRegularExpenseDefinitionRequest
        {
            Name = "Prąd",
            CategoryId = categoryId,
            Amount = 120m
        }, CancellationToken.None);

        var updated = await service.UpdateRegularExpenseDefinitionAsync(new UpdateRegularExpenseDefinitionRequest
        {
            Id = created.Id,
            Name = "Prąd zaktualizowany",
            CategoryId = categoryId,
            Amount = 135m,
            IsActive = true,
            ShowRemainingInUI = false
        }, CancellationToken.None);

        Assert.Equal("Prąd zaktualizowany", updated.Name);
        Assert.Equal(135m, updated.Amount);
    }

    /// <summary>
    /// Calls UpdateRegularExpenseDefinitionAsync with an ID that does not exist.
    /// Verifies that a NotFoundException is thrown.
    /// </summary>
    [Fact]
    public async Task UpdateRegularExpenseDefinitionAsync_Should_Throw_NotFoundException_When_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateRegularExpenseDefinitionAsync(
            new UpdateRegularExpenseDefinitionRequest
            {
                Id = 99999,
                Name = "Ghost",
                CategoryId = 1,
                Amount = 50m,
                IsActive = true
            }, CancellationToken.None));
    }

    // ─── MonthSavingsTransferItem not-found edge cases ──────────────────────────

    /// <summary>
    /// Calls UpdateMonthSavingsTransferItemAsync with an ID that does not exist.
    /// Verifies that a NotFoundException is thrown.
    /// </summary>
    [Fact]
    public async Task UpdateMonthSavingsTransferItemAsync_Should_Throw_NotFoundException_When_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateMonthSavingsTransferItemAsync(
            new UpdateMonthSavingsTransferItemRequest
            {
                Id = 99999,
                Amount = 100m,
                TransferDate = new DateOnly(2026, 1, 10)
            }, CancellationToken.None));
    }

    /// <summary>
    /// Calls DeleteMonthSavingsTransferItemAsync with an ID that does not exist.
    /// Verifies that a NotFoundException is thrown.
    /// </summary>
    [Fact]
    public async Task DeleteMonthSavingsTransferItemAsync_Should_Throw_NotFoundException_When_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteMonthSavingsTransferItemAsync(
            new DeleteMonthSavingsTransferItemRequest { Id = 99999 }, CancellationToken.None));
    }

    // ─── BudgetExceededEvent NOT emitted when under limit ───────────────────────

    /// <summary>
    /// Creates an expense whose actual amount stays below the category's EnvelopeLimit.
    /// Verifies that no BudgetExceededEvent is published.
    /// </summary>
    [Fact]
    public async Task CreateExpenseAsync_Should_Not_Emit_BudgetExceededEvent_When_Under_Limit()
    {
        int categoryId;

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category { Name = "Odzież", Color = "#8E24AA", EnvelopeLimit = 500m };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            categoryId = category.Id;
        }

        var publisher = new RecordingAppEventPublisher();
        var service = CreateService(publisher);

        await service.CreateExpenseAsync(new CreateExpenseRequest
        {
            Year = 2026,
            Month = 5,
            Name = "Koszula",
            CategoryId = categoryId,
            PlannedAmount = 100m,
            ActualAmount = 100m,
            ShowRemainingInUI = true
        }, CancellationToken.None);

        var budgetEvents = publisher.Events.OfType<BudgetExceededEvent>().ToList();
        Assert.Empty(budgetEvents);
    }

    // ─── GetYearStatisticsAsync empty year ──────────────────────────────────────

    /// <summary>
    /// Saves annual plan targets for two different budget owners in the same database.
    /// Verifies that each owner sees only their own row and that updates overwrite the
    /// prior values instead of creating duplicates.
    /// </summary>
    [Fact]
    public async Task UpsertAnnualPlanAsync_Should_Create_Update_And_Respect_UserScope()
    {
        var ownerA = new CurrentUserContext { UserId = "owner-a", BudgetOwnerUserId = "owner-a" };
        var ownerB = new CurrentUserContext { UserId = "owner-b", BudgetOwnerUserId = "owner-b" };

        var serviceA = CreateService(ownerA);
        var serviceB = CreateService(ownerB);

        var createdA = await serviceA.UpsertAnnualPlanAsync(new UpsertAnnualPlanRequest
        {
            Year = 2027,
            ExpectedIncomeAmount = 50000m,
            ExpectedSavingsAmount = 12000m
        }, CancellationToken.None);

        Assert.Equal(2027, createdA.Year);
        Assert.Equal(50000m, createdA.ExpectedIncomeAmount);
        Assert.Equal(12000m, createdA.ExpectedSavingsAmount);

        var updatedA = await serviceA.UpsertAnnualPlanAsync(new UpsertAnnualPlanRequest
        {
            Year = 2027,
            ExpectedIncomeAmount = 51000m,
            ExpectedSavingsAmount = 13000m
        }, CancellationToken.None);

        Assert.Equal(51000m, updatedA.ExpectedIncomeAmount);
        Assert.Equal(13000m, updatedA.ExpectedSavingsAmount);

        var createdB = await serviceB.UpsertAnnualPlanAsync(new UpsertAnnualPlanRequest
        {
            Year = 2027,
            ExpectedIncomeAmount = 70000m,
            ExpectedSavingsAmount = 20000m
        }, CancellationToken.None);

        Assert.Equal(2027, createdB.Year);
        Assert.Equal(70000m, createdB.ExpectedIncomeAmount);
        Assert.Equal(20000m, createdB.ExpectedSavingsAmount);

        var statsA = await serviceA.GetYearStatisticsAsync(2027, CancellationToken.None);
        var statsB = await serviceB.GetYearStatisticsAsync(2027, CancellationToken.None);

        Assert.Contains(2027, statsA.AvailableYears);
        Assert.Contains(2027, statsB.AvailableYears);
        Assert.Equal(51000m, statsA.AnnualPlan.ExpectedIncomeAmount);
        Assert.Equal(13000m, statsA.AnnualPlan.ExpectedSavingsAmount);
        Assert.Equal(70000m, statsB.AnnualPlan.ExpectedIncomeAmount);
        Assert.Equal(20000m, statsB.AnnualPlan.ExpectedSavingsAmount);
        Assert.NotEqual(statsA.AnnualPlan.ExpectedIncomeAmount, statsB.AnnualPlan.ExpectedIncomeAmount);
        Assert.NotEqual(statsA.AnnualPlan.ExpectedSavingsAmount, statsB.AnnualPlan.ExpectedSavingsAmount);
    }

    /// <summary>
    /// Verifies that annual plan targets cannot be negative.
    /// </summary>
    [Fact]
    public async Task UpsertAnnualPlanAsync_Should_Reject_Negative_Targets()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() => service.UpsertAnnualPlanAsync(new UpsertAnnualPlanRequest
        {
            Year = 2027,
            ExpectedIncomeAmount = -1m,
            ExpectedSavingsAmount = 0m
        }, CancellationToken.None));

        await Assert.ThrowsAsync<BadRequestException>(() => service.UpsertAnnualPlanAsync(new UpsertAnnualPlanRequest
        {
            Year = 2027,
            ExpectedIncomeAmount = 0m,
            ExpectedSavingsAmount = -1m
        }, CancellationToken.None));
    }

    /// <summary>
    /// Calls GetYearStatisticsAsync for a year with no expenses or account data.
    /// Verifies that empty collections are returned without errors.
    /// </summary>
    [Fact]
    public async Task GetYearStatisticsAsync_Should_Return_Empty_Collections_For_Year_With_No_Data()
    {
        var service = CreateService();
        var result = await service.GetYearStatisticsAsync(2099, CancellationToken.None);

        Assert.Empty(result.PopulatedMonths);
        Assert.Empty(result.CategoryStatistics);
        Assert.Empty(result.MonthlyFinance);
        Assert.Empty(result.AccountBalances);
        Assert.Equal(2099, result.AnnualPlan.Year);
        Assert.Equal(0m, result.AnnualPlan.ExpectedIncomeAmount);
        Assert.Equal(0m, result.AnnualPlan.ExpectedSavingsAmount);
    }

    // ─── CloseMonthAsync idempotency ─────────────────────────────────────────────

    /// <summary>
    /// Calls CloseMonthAsync twice on the same month.
    /// Verifies that the second call is a no-op and does not throw.
    /// </summary>
    [Fact]
    public async Task CloseMonthAsync_Should_Be_Idempotent_When_Already_Closed()
    {
        var service = CreateService();

        await service.CloseMonthAsync(2026, 4, CancellationToken.None);

        var ex = await Record.ExceptionAsync(() => service.CloseMonthAsync(2026, 4, CancellationToken.None));
        Assert.Null(ex);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var monthPlan = await verifyContext.MonthPlans.FirstAsync(x => x.Year == 2026 && x.Month == 4);
        Assert.True(monthPlan.IsClosed);
    }
}
