using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;
using HouseholdBudgetMate.Abstractions.Contracts.Facility.Events;
using HouseholdBudgetMate.Abstractions.Contracts.Incomes.Requests;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Application.Services;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class ExpenseServiceTests
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    private ExpenseService CreateService(RecordingAppEventPublisher? eventPublisher = null)
    {
        var factory = TestDbContextFactory.CreateFactory(_dbName);
        var provider = new StaticDateTimeProvider(DateTime.UtcNow);
        return new ExpenseService(
            factory,
            provider,
            eventPublisher ?? new RecordingAppEventPublisher(),
            new NoOpIncomeService(),
            new NoOpLoanService());
    }

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
                    Year = 2026,
                    Month = month,
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
                    Year = 2026,
                    Month = 1,
                    Name = "Wyplata 1",
                    Amount = 3000m,
                    AccountId = accountId,
                    ExpectedDayOfMonth = new DateOnly(2026, 1, 10)
                },
                new Income
                {
                    Year = 2026,
                    Month = 2,
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
        var result = await service.GetCategoryLifetimeExpenseTotalsAsync([homeCategoryId], CancellationToken.None);

        var homeTotal = Assert.Single(result);
        Assert.Equal(homeCategoryId, homeTotal.CategoryId);
        Assert.Equal(1350m, homeTotal.TotalSpent);
        Assert.Equal(2024, homeTotal.FirstYear);
        Assert.Equal(2025, homeTotal.LastYear);
    }

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
                    Year = 2026,
                    Month = 1,
                    Name = "Wyplata 1",
                    Amount = 500m,
                    AccountId = accountId,
                    ExpectedDayOfMonth = new DateOnly(2026, 1, 10)
                },
                new Income
                {
                    Year = 2026,
                    Month = 2,
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
        Assert.Equal(30m, summary.UnplannedSpentTotal);
        Assert.Equal(160m, summary.SavedAmountThisMonth);
        Assert.Equal(310m, summary.SavedAmountYearToDate);
        Assert.Equal(550m, summary.AverageMonthlyIncome);
        Assert.Equal(170m, summary.AverageMonthlySpent);
        Assert.Equal(155m, summary.AverageMonthlySaved);
        Assert.Equal(2, summary.SavingsTimeline.Count);
        Assert.Equal(150m, summary.SavingsTimeline.Single(x => x.Month == 1).SavedAmount);
        Assert.Equal(160m, summary.SavingsTimeline.Single(x => x.Month == 2).SavedAmount);
    }
}
