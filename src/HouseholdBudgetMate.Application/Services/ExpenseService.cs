using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;
using HouseholdBudgetMate.Abstractions.Contracts.Facility.Events;
using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Application.Helpers;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Application.Kernel.Timing;
using HouseholdBudgetMate.Application.Mapping;
using HouseholdBudgetMate.Application.Validation;
using HouseholdBudgetMate.Application.Validation.Common;
using HouseholdBudgetMate.Application.Validation.Expenses;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Application.Services;

public sealed class ExpenseService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IDateTimeProvider dateTimeProvider,
    IAppEventPublisher appEventPublisher,
    IIncomeService incomeService) : IExpenseService
{
    private static readonly YearMonthRequestValidator YearMonthValidator = new();
    private static readonly DateInMonthRequestValidator DateInMonthValidator = new();

    private static readonly CreateMonthSavingsTransferItemRequestValidator CreateSavingsTransferItemValidator = new();
    private static readonly CreateExpenseRequestValidator CreateExpenseValidator = new();
    private static readonly CreateExpenseLineItemRequestValidator CreateExpenseLineItemValidator = new();
    private static readonly UpdateMonthSavingsTransferItemRequestValidator UpdateSavingsTransferItemValidator = new();
    private static readonly UpdateExpenseRequestValidator UpdateExpenseValidator = new();
    private static readonly ReorderExpensesRequestValidator ReorderExpensesValidator = new();
    private static readonly UpdateExpenseLineItemRequestValidator UpdateExpenseLineItemValidator = new();
    private static readonly DeleteMonthSavingsTransferItemRequestValidator DeleteSavingsTransferItemValidator = new();
    private static readonly DeleteExpenseRequestValidator DeleteExpenseValidator = new();
    private static readonly DeleteExpenseLineItemRequestValidator DeleteExpenseLineItemValidator = new();
    private static readonly CreateRegularExpenseDefinitionRequestValidator CreateRegularExpenseDefinitionValidator = new();
    private static readonly UpdateRegularExpenseDefinitionRequestValidator UpdateRegularExpenseDefinitionValidator = new();
    private static readonly DeleteRegularExpenseDefinitionRequestValidator DeleteRegularExpenseDefinitionValidator = new();
    private static readonly ReorderRegularExpenseDefinitionsRequestValidator ReorderRegularExpenseDefinitionsValidator = new();

    public async Task<IReadOnlyList<AvailableMonthDto>> GetAvailableMonthsAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var monthPlans = await dbContext.MonthPlans
            .AsNoTracking()
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .Select(x => x.MapAvailableMonthToDto())
            .ToListAsync(cancellationToken);

        return monthPlans;
    }

    public async Task<IReadOnlyList<RegularExpenseDefinitionDto>> GetRegularExpenseDefinitionsAsync(
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var definitions = await dbContext.RegularExpenseDefinitions
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Tag)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Name)
            .Select(x => x.MapRegularExpenseDefinitionToDto())
            .ToListAsync(cancellationToken);

        return definitions;
    }

    public async Task<RegularExpenseDefinitionDto> CreateRegularExpenseDefinitionAsync(
        CreateRegularExpenseDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        CreateRegularExpenseDefinitionValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var normalizedTagId = await EnsureCategoryAndTagValidAsync(
            dbContext,
            request.CategoryId,
            request.TagId,
            cancellationToken);

        var definition = new RegularExpenseDefinition
        {
            Order = await dbContext.RegularExpenseDefinitions
                .Select(x => (int?)x.Order)
                .MaxAsync(cancellationToken) + 1 ?? 1,
            Name = request.Name,
            CategoryId = request.CategoryId,
            TagId = normalizedTagId,
            Amount = request.Amount,
            IsActive = true,
            ShowRemainingInUI = request.ShowRemainingInUI
        };

        dbContext.RegularExpenseDefinitions.Add(definition);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildRegularExpenseDefinitionDtoAsync(dbContext, definition.Id, cancellationToken);
    }

    public async Task<RegularExpenseDefinitionDto> UpdateRegularExpenseDefinitionAsync(
        UpdateRegularExpenseDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        UpdateRegularExpenseDefinitionValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var definition = await dbContext.RegularExpenseDefinitions
                             .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                         ?? throw new NotFoundException("Regular expense definition not found.");

        var normalizedTagId = await EnsureCategoryAndTagValidAsync(
            dbContext,
            request.CategoryId,
            request.TagId,
            cancellationToken);

        definition.Name = request.Name;
        definition.CategoryId = request.CategoryId;
        definition.TagId = normalizedTagId;
        definition.Amount = request.Amount;
        definition.IsActive = request.IsActive;
        definition.ShowRemainingInUI = request.ShowRemainingInUI;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildRegularExpenseDefinitionDtoAsync(dbContext, definition.Id, cancellationToken);
    }

    public async Task DeleteRegularExpenseDefinitionAsync(
        DeleteRegularExpenseDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        DeleteRegularExpenseDefinitionValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var definition = await dbContext.RegularExpenseDefinitions
                             .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                         ?? throw new NotFoundException("Regular expense definition not found.");

        if (!definition.IsActive)
        {
            return;
        }

        definition.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteRegularExpenseDefinitionPermanentlyAsync(
        DeleteRegularExpenseDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        DeleteRegularExpenseDefinitionValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var definition = await dbContext.RegularExpenseDefinitions
                             .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                         ?? throw new NotFoundException("Regular expense definition not found.");

        dbContext.RegularExpenseDefinitions.Remove(definition);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderRegularExpenseDefinitionsAsync(
        ReorderRegularExpenseDefinitionsRequest request,
        CancellationToken cancellationToken)
    {
        ReorderRegularExpenseDefinitionsValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var definitions = await dbContext.RegularExpenseDefinitions
            .Where(x => request.DefinitionIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (definitions.Count != request.DefinitionIds.Count)
        {
            throw new BadRequestException("Some regular expense definitions were not found.");
        }

        for (var i = 0; i < request.DefinitionIds.Count; i++)
        {
            var definition = definitions.First(x => x.Id == request.DefinitionIds[i]);
            definition.Order = i + 1;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CloseMonthAsync(int year, int month, CancellationToken cancellationToken)
    {
        YearMonthValidator.ValidateOrThrowBadRequest(new YearMonthRequest(year, month));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var monthPlan = await GetOrCreateMonthPlanAsync(dbContext, year, month, cancellationToken);
        if (monthPlan.IsClosed)
        {
            return;
        }

        monthPlan.IsClosed = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        var nextMonth = new DateTime(year, month, 1).AddMonths(1);
        await OpenMonthAsync(nextMonth.Year, nextMonth.Month, cancellationToken);
    }

    public async Task OpenMonthAsync(int year, int month, CancellationToken cancellationToken)
    {
        YearMonthValidator.ValidateOrThrowBadRequest(new YearMonthRequest(year, month));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var monthPlan = await GetOrCreateMonthPlanAsync(dbContext, year, month, cancellationToken);
        monthPlan.IsClosed = false;

        await SyncRegularExpensesForMonthAsync(dbContext, monthPlan, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await incomeService.SyncRegularIncomesForMonthAsync(year, month, cancellationToken);
    }

    public async Task<MonthPlanDto> GetMonthAsync(int year, int month, CancellationToken cancellationToken)
    {
        YearMonthValidator.ValidateOrThrowBadRequest(new YearMonthRequest(year, month));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var monthPlan = await GetOrCreateMonthPlanAsync(dbContext, year, month, cancellationToken);

        if (!monthPlan.IsClosed)
        {
            await SyncRegularExpensesForMonthAsync(dbContext, monthPlan, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await incomeService.SyncRegularIncomesForMonthAsync(year, month, cancellationToken);
        }

        var expenseEntities = await dbContext.Expenses
            .AsNoTracking()
            .Where(x => x.MonthPlanId == monthPlan.Id)
            .Include(x => x.Category)
            .Include(x => x.Tag)
            .Include(x => x.LineItems)
            .ThenInclude(x => x.Tag)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var expenses = expenseEntities
            .Select(x => x.MapExpenseToDto())
            .ToList();

        var savingsTransfers = await dbContext.MonthSavingsTransferItems
            .AsNoTracking()
            .Where(x => x.MonthPlanId == monthPlan.Id)
            .OrderBy(x => x.TransferDate)
            .ThenBy(x => x.Id)
            .Select(x => x.MapSavingsTransferToDto())
            .ToListAsync(cancellationToken);

        return BuildMonthPlanDto(monthPlan, expenses, savingsTransfers);
    }

    public async Task<MonthSavingsTransferItemDto> CreateMonthSavingsTransferItemAsync(
        CreateMonthSavingsTransferItemRequest request, CancellationToken cancellationToken)
    {
        CreateSavingsTransferItemValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var monthPlan = await GetOrCreateMonthPlanAsync(dbContext, request.Year, request.Month, cancellationToken);
        BudgetHelper.EnsureMonthIsOpen(monthPlan);

        var item = new MonthSavingsTransferItem
        {
            MonthPlanId = monthPlan.Id,
            Amount = request.Amount,
            TransferDate = request.TransferDate
        };

        dbContext.MonthSavingsTransferItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return item.MapSavingsTransferToDto();
    }

    public async Task<MonthSavingsTransferItemDto> UpdateMonthSavingsTransferItemAsync(
        UpdateMonthSavingsTransferItemRequest request, CancellationToken cancellationToken)
    {
        UpdateSavingsTransferItemValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var item = await dbContext.MonthSavingsTransferItems
                       .Include(x => x.MonthPlan)
                       .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                   ?? throw new NotFoundException("Savings transfer item not found.");

        BudgetHelper.EnsureMonthIsOpen(item.MonthPlan);

        DateInMonthValidator.ValidateOrThrowBadRequest(new DateInMonthRequest(
            request.TransferDate,
            item.MonthPlan.Year,
            item.MonthPlan.Month,
            "Savings transfer date must belong to selected month and year."));

        item.Amount = request.Amount;
        item.TransferDate = request.TransferDate;
        await dbContext.SaveChangesAsync(cancellationToken);

        return item.MapSavingsTransferToDto();
    }

    public async Task DeleteMonthSavingsTransferItemAsync(DeleteMonthSavingsTransferItemRequest request,
        CancellationToken cancellationToken)
    {
        DeleteSavingsTransferItemValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var item = await dbContext.MonthSavingsTransferItems
                       .Include(x => x.MonthPlan)
                       .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                   ?? throw new NotFoundException("Savings transfer item not found.");

        BudgetHelper.EnsureMonthIsOpen(item.MonthPlan);

        dbContext.MonthSavingsTransferItems.Remove(item);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ExpenseDto> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        CreateExpenseValidator.ValidateOrThrowBadRequest(request);

        var normalizedName = request.Name;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var monthPlan = await GetOrCreateMonthPlanAsync(dbContext, request.Year, request.Month, cancellationToken);
        BudgetHelper.EnsureMonthIsOpen(monthPlan);
        var envelopeUsageBefore = await GetEnvelopeUsageSnapshotAsync(
            dbContext,
            monthPlan.Id,
            request.CategoryId,
            cancellationToken);

        var category = await dbContext.Categories
                           .AsNoTracking()
                           .FirstOrDefaultAsync(x => x.Id == request.CategoryId, cancellationToken)
                       ?? throw new NotFoundException("Category not found.");

        var normalizedTagId = await EnsureCategoryAndTagValidAsync(
            dbContext,
            request.CategoryId,
            request.TagId,
            cancellationToken);
        var normalizedTagName = normalizedTagId.HasValue
            ? await dbContext.Tags
                .AsNoTracking()
                .Where(x => x.Id == normalizedTagId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var expense = new Expense
        {
            MonthPlanId = monthPlan.Id,
            Order = await dbContext.Expenses
                .Where(x => x.MonthPlanId == monthPlan.Id)
                .Select(x => (int?)x.Order)
                .MaxAsync(cancellationToken) + 1 ?? 1,
            Name = normalizedName,
            CategoryId = request.CategoryId,
            TagId = normalizedTagId,
            PlannedAmount = request.PlannedAmount,
            ActualAmount = request.ActualAmount,
            ShowRemainingInUI = request.ShowRemainingInUI
        };

        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync(cancellationToken);

        var envelopeUsageAfter = await GetEnvelopeUsageSnapshotAsync(
            dbContext,
            monthPlan.Id,
            request.CategoryId,
            cancellationToken);

        await EmitBudgetExceededEventIfNeededAsync(
            envelopeUsageBefore,
            envelopeUsageAfter,
            monthPlan.Year,
            monthPlan.Month,
            cancellationToken);

        return await BuildExpenseDtoAsync(dbContext, expense.Id, cancellationToken, category.Name, normalizedTagName);
    }

    public async Task ReorderExpensesAsync(ReorderExpensesRequest request, CancellationToken cancellationToken)
    {
        ReorderExpensesValidator.ValidateOrThrowBadRequest(request);

        if (request.ExpenseIds.Count == 0)
        {
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var expenses = await dbContext.Expenses
            .Where(x => request.ExpenseIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (expenses.Count != request.ExpenseIds.Count)
        {
            throw new BadRequestException("Some expenses were not found.");
        }

        var monthPlanIds = expenses.Select(x => x.MonthPlanId).Distinct().ToList();
        if (monthPlanIds.Count != 1)
        {
            throw new BadRequestException("Expenses must belong to one month plan.");
        }

        var monthPlan = await dbContext.MonthPlans
            .FirstOrDefaultAsync(x => x.Id == monthPlanIds[0], cancellationToken)
            ?? throw new NotFoundException("Month plan not found.");
        BudgetHelper.EnsureMonthIsOpen(monthPlan);

        for (var i = 0; i < request.ExpenseIds.Count; i++)
        {
            var expense = expenses.First(x => x.Id == request.ExpenseIds[i]);
            expense.Order = i + 1;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ExpenseLineItemDto> CreateExpenseLineItemAsync(CreateExpenseLineItemRequest request,
        CancellationToken cancellationToken)
    {
        CreateExpenseLineItemValidator.ValidateOrThrowBadRequest(request);

        var normalizedDescription = request.Description;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var expense = await dbContext.Expenses
                          .Include(x => x.Category)
                          .Include(x => x.Tag)
                          .Include(x => x.MonthPlan)
                          .Include(x => x.LineItems)
                          .FirstOrDefaultAsync(x => x.Id == request.ExpenseId, cancellationToken)
                      ?? throw new NotFoundException("Expense not found.");

        BudgetHelper.EnsureMonthIsOpen(expense.MonthPlan);

        var envelopeUsageBefore = await GetEnvelopeUsageSnapshotAsync(
            dbContext,
            expense.MonthPlanId,
            expense.CategoryId,
            cancellationToken);

        var supportsLineItems = expense.Tag?.SupportsLineItemsOverride ?? expense.Category.SupportsLineItems;
        if (!supportsLineItems)
        {
            throw new BadRequestException("Selected category does not support line items.");
        }

        Tag? tag = null;
        if (request.TagId.HasValue)
        {
            tag = await dbContext.Tags
                      .AsNoTracking()
                      .FirstOrDefaultAsync(x => x.Id == request.TagId.Value, cancellationToken)
                  ?? throw new NotFoundException("Tag not found.");

            if (tag.CategoryId != expense.CategoryId)
            {
                throw new BadRequestException("Selected tag does not belong to selected category.");
            }

            if (expense.TagId.HasValue && tag.Id != expense.TagId.Value && tag.ParentTagId != expense.TagId.Value)
            {
                throw new BadRequestException("Selected line item tag must belong to expense main tag.");
            }
        }

        var lineItem = new ExpenseLineItem
        {
            ExpenseId = expense.Id,
            Description = normalizedDescription,
            Amount = request.Amount,
            OccurredAt = request.OccurredAt,
            TagId = request.TagId
        };

        dbContext.ExpenseLineItems.Add(lineItem);

        await dbContext.SaveChangesAsync(cancellationToken);

        await RecalculateActualAmountAsync(dbContext, expense.Id, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        var envelopeUsageAfter = await GetEnvelopeUsageSnapshotAsync(
            dbContext,
            expense.MonthPlanId,
            expense.CategoryId,
            cancellationToken);

        await EmitBudgetExceededEventIfNeededAsync(
            envelopeUsageBefore,
            envelopeUsageAfter,
            expense.MonthPlan.Year,
            expense.MonthPlan.Month,
            cancellationToken);

        return new ExpenseLineItemDto
        {
            Id = lineItem.Id,
            ExpenseId = lineItem.ExpenseId,
            Description = lineItem.Description,
            Amount = lineItem.Amount,
            OccurredAt = lineItem.OccurredAt,
            TagId = lineItem.TagId,
            TagName = tag?.Name
        };
    }

    public async Task<ExpenseLineItemDto> UpdateExpenseLineItemAsync(UpdateExpenseLineItemRequest request,
        CancellationToken cancellationToken)
    {
        UpdateExpenseLineItemValidator.ValidateOrThrowBadRequest(request);

        var normalizedDescription = request.Description;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var lineItem = await dbContext.ExpenseLineItems
                           .Include(x => x.Expense)
                           .ThenInclude(x => x.Category)
                           .Include(x => x.Expense)
                           .ThenInclude(x => x.Tag)
                           .Include(x => x.Expense)
                           .ThenInclude(x => x.MonthPlan)
                           .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                       ?? throw new NotFoundException("Line item not found.");

        BudgetHelper.EnsureMonthIsOpen(lineItem.Expense.MonthPlan);

        var envelopeUsageBefore = await GetEnvelopeUsageSnapshotAsync(
            dbContext,
            lineItem.Expense.MonthPlanId,
            lineItem.Expense.CategoryId,
            cancellationToken);

        var supportsLineItems = lineItem.Expense.Tag?.SupportsLineItemsOverride ?? lineItem.Expense.Category.SupportsLineItems;
        if (!supportsLineItems)
        {
            throw new BadRequestException("Selected category does not support line items.");
        }

        Tag? tag = null;
        if (request.TagId.HasValue)
        {
            tag = await dbContext.Tags
                      .AsNoTracking()
                      .FirstOrDefaultAsync(x => x.Id == request.TagId.Value, cancellationToken)
                  ?? throw new NotFoundException("Tag not found.");

            if (tag.CategoryId != lineItem.Expense.CategoryId)
            {
                throw new BadRequestException("Selected tag does not belong to selected category.");
            }

            if (lineItem.Expense.TagId.HasValue
                && tag.Id != lineItem.Expense.TagId.Value
                && tag.ParentTagId != lineItem.Expense.TagId.Value)
            {
                throw new BadRequestException("Selected line item tag must belong to expense main tag.");
            }
        }

        lineItem.Description = normalizedDescription;
        lineItem.Amount = request.Amount;
        lineItem.OccurredAt = request.OccurredAt;
        lineItem.TagId = request.TagId;

        await dbContext.SaveChangesAsync(cancellationToken);
        await RecalculateActualAmountAsync(dbContext, lineItem.ExpenseId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var envelopeUsageAfter = await GetEnvelopeUsageSnapshotAsync(
            dbContext,
            lineItem.Expense.MonthPlanId,
            lineItem.Expense.CategoryId,
            cancellationToken);

        await EmitBudgetExceededEventIfNeededAsync(
            envelopeUsageBefore,
            envelopeUsageAfter,
            lineItem.Expense.MonthPlan.Year,
            lineItem.Expense.MonthPlan.Month,
            cancellationToken);

        return new ExpenseLineItemDto
        {
            Id = lineItem.Id,
            ExpenseId = lineItem.ExpenseId,
            Description = lineItem.Description,
            Amount = lineItem.Amount,
            OccurredAt = lineItem.OccurredAt,
            TagId = lineItem.TagId,
            TagName = tag?.Name,
        };
    }

    public async Task DeleteExpenseLineItemAsync(DeleteExpenseLineItemRequest request,
        CancellationToken cancellationToken)
    {
        DeleteExpenseLineItemValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var lineItem = await dbContext.ExpenseLineItems
                           .Include(x => x.Expense)
                           .ThenInclude(x => x.MonthPlan)
                           .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                       ?? throw new NotFoundException("Line item not found.");

        BudgetHelper.EnsureMonthIsOpen(lineItem.Expense.MonthPlan);

        var expenseId = lineItem.ExpenseId;
        dbContext.ExpenseLineItems.Remove(lineItem);
        await dbContext.SaveChangesAsync(cancellationToken);

        await RecalculateActualAmountAsync(dbContext, expenseId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ExpenseDto> UpdateExpenseAsync(UpdateExpenseRequest request, CancellationToken cancellationToken)
    {
        UpdateExpenseValidator.ValidateOrThrowBadRequest(request);

        var normalizedName = request.Name;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var expense = await dbContext.Expenses
                          .Include(x => x.MonthPlan)
                          .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                      ?? throw new NotFoundException("Expense not found.");

        BudgetHelper.EnsureMonthIsOpen(expense.MonthPlan);

        var envelopeUsageBefore = await GetEnvelopeUsageSnapshotAsync(
            dbContext,
            expense.MonthPlanId,
            request.CategoryId,
            cancellationToken);

        var category = await dbContext.Categories
                           .AsNoTracking()
                           .FirstOrDefaultAsync(x => x.Id == request.CategoryId, cancellationToken)
                       ?? throw new NotFoundException("Category not found.");

        var normalizedTagId = await EnsureCategoryAndTagValidAsync(
            dbContext,
            request.CategoryId,
            request.TagId,
            cancellationToken);
        var normalizedTagName = normalizedTagId.HasValue
            ? await dbContext.Tags
                .AsNoTracking()
                .Where(x => x.Id == normalizedTagId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        expense.Name = normalizedName;
        expense.CategoryId = request.CategoryId;
        expense.TagId = normalizedTagId;
        expense.PlannedAmount = request.PlannedAmount;
        if (!await dbContext.ExpenseLineItems.AnyAsync(x => x.ExpenseId == expense.Id, cancellationToken))
        {
            expense.ActualAmount = request.ActualAmount;
        }

        expense.ShowRemainingInUI = request.ShowRemainingInUI;

        await dbContext.SaveChangesAsync(cancellationToken);

        await RecalculateActualAmountAsync(dbContext, expense.Id, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var envelopeUsageAfter = await GetEnvelopeUsageSnapshotAsync(
            dbContext,
            expense.MonthPlanId,
            request.CategoryId,
            cancellationToken);

        await EmitBudgetExceededEventIfNeededAsync(
            envelopeUsageBefore,
            envelopeUsageAfter,
            expense.MonthPlan.Year,
            expense.MonthPlan.Month,
            cancellationToken);

        return await BuildExpenseDtoAsync(dbContext, expense.Id, cancellationToken, category.Name, normalizedTagName);
    }

    public async Task DeleteExpenseAsync(DeleteExpenseRequest request, CancellationToken cancellationToken)
    {
        DeleteExpenseValidator.ValidateOrThrowBadRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var expense = await dbContext.Expenses
                          .Include(x => x.MonthPlan)
                          .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                      ?? throw new NotFoundException("Expense not found.");

        BudgetHelper.EnsureMonthIsOpen(expense.MonthPlan);

        expense.IsDeleted = true;
        expense.DeletedAtUtc = dateTimeProvider.GetUtcDateTime();

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<MonthPlan> GetOrCreateMonthPlanAsync(
        ApplicationDbContext dbContext,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        var monthPlan = await dbContext.MonthPlans
            .FirstOrDefaultAsync(x => x.Year == year && x.Month == month, cancellationToken);

        if (monthPlan is not null)
        {
            return monthPlan;
        }

        monthPlan = new MonthPlan
        {
            Year = year,
            Month = month,
            IsClosed = false
        };

        dbContext.MonthPlans.Add(monthPlan);
        await dbContext.SaveChangesAsync(cancellationToken);

        return monthPlan;
    }

    private static MonthPlanDto BuildMonthPlanDto(
        MonthPlan monthPlan,
        IReadOnlyList<ExpenseDto> expenses,
        IReadOnlyList<MonthSavingsTransferItemDto> savingsTransfers)
    {
        return new MonthPlanDto
        {
            Id = monthPlan.Id,
            Year = monthPlan.Year,
            Month = monthPlan.Month,
            IsClosed = monthPlan.IsClosed,
            Kpi = CalculateMonthPlanKpi(expenses),
            SavingsTransfers = savingsTransfers,
            Expenses = expenses
        };
    }

    private static MonthPlanKpiDto CalculateMonthPlanKpi(IReadOnlyList<ExpenseDto> expenses)
    {
        var plannedTotal = expenses.Sum(x => x.PlannedAmount);
        var spentTotal = expenses.Sum(x => x.ActualAmount);

        var remainingFromVisibleExpenses = expenses
            .Where(x => x.ShowRemainingInUI && !IsDerivedUnplanned(x.PlannedAmount))
            .Sum(x => Math.Max(0, x.RemainingAmount));

        // Doliczamy plan tylko dla pozycji bez pokazywania "pozostalo", aby uniknac podwojnego liczenia.
        var plannedForMissingActualHiddenInUi = expenses
            .Where(x => !x.ShowRemainingInUI)
            .Where(x => !IsDerivedUnplanned(x.PlannedAmount))
            .Where(x => x.ActualAmount == 0)
            .Sum(x => x.PlannedAmount);

        var remainingTotal = remainingFromVisibleExpenses + plannedForMissingActualHiddenInUi;
        var remainingPercent = plannedTotal <= 0
            ? 0
            : Math.Clamp((double)(remainingTotal / plannedTotal * 100), 0, 100);

        return new MonthPlanKpiDto
        {
            PlannedTotal = plannedTotal,
            SpentTotal = spentTotal,
            RemainingTotal = remainingTotal,
            RemainingPercent = remainingPercent
        };
    }

    private static bool IsDerivedUnplanned(decimal? plannedAmount)
    {
        return !plannedAmount.HasValue || plannedAmount <= 0;
    }

    private static async Task RecalculateActualAmountAsync(
        ApplicationDbContext dbContext,
        int expenseId,
        CancellationToken cancellationToken)
    {
        var expense = await dbContext.Expenses
                          .Include(x => x.LineItems)
                          .FirstOrDefaultAsync(x => x.Id == expenseId, cancellationToken)
                      ?? throw new NotFoundException("Expense not found.");

        if (expense.LineItems.Count == 0)
        {
            return;
        }

        expense.ActualAmount = expense.LineItems.Sum(x => x.Amount);
    }

    private static async Task<ExpenseDto> BuildExpenseDtoAsync(
        ApplicationDbContext dbContext,
        int expenseId,
        CancellationToken cancellationToken,
        string? categoryNameOverride = null,
        string? tagNameOverride = null)
    {
        var expense = await dbContext.Expenses
                          .AsNoTracking()
                          .Where(x => x.Id == expenseId)
                          .Include(x => x.Category)
                          .Include(x => x.Tag)
                          .Include(x => x.LineItems)
                          .ThenInclude(x => x.Tag)
                          .FirstOrDefaultAsync(cancellationToken)
                      ?? throw new NotFoundException("Expense not found.");

        var dto = expense.MapExpenseToDto();

        if (!string.IsNullOrWhiteSpace(categoryNameOverride))
        {
            dto.CategoryName = categoryNameOverride;
        }

        if (dto.TagId.HasValue)
        {
            dto.TagName = tagNameOverride ?? dto.TagName;
        }

        return dto;
    }

    private static async Task<RegularExpenseDefinitionDto> BuildRegularExpenseDefinitionDtoAsync(
        ApplicationDbContext dbContext,
        int definitionId,
        CancellationToken cancellationToken)
    {
        var definition = await dbContext.RegularExpenseDefinitions
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Tag)
            .FirstOrDefaultAsync(x => x.Id == definitionId, cancellationToken)
            ?? throw new NotFoundException("Regular expense definition not found.");

        return definition.MapRegularExpenseDefinitionToDto();
    }

    private static async Task<int?> EnsureCategoryAndTagValidAsync(
        ApplicationDbContext dbContext,
        int categoryId,
        int? tagId,
        CancellationToken cancellationToken)
    {
        var categoryExists = await dbContext.Categories
            .AsNoTracking()
            .AnyAsync(x => x.Id == categoryId, cancellationToken);

        if (!categoryExists)
        {
            throw new NotFoundException("Category not found.");
        }

        if (!tagId.HasValue)
        {
            return null;
        }

        var tag = await dbContext.Tags
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tagId.Value, cancellationToken)
            ?? throw new NotFoundException("Tag not found.");

        if (tag.CategoryId != categoryId)
        {
            throw new BadRequestException("Selected tag does not belong to selected category.");
        }

        if (!tag.ParentTagId.HasValue)
        {
            return tag.Id;
        }

        var parentTag = await dbContext.Tags
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tag.ParentTagId.Value, cancellationToken)
            ?? throw new BadRequestException("Selected tag parent not found.");

        if (parentTag.CategoryId != categoryId)
        {
            throw new BadRequestException("Selected tag parent does not belong to selected category.");
        }

        return parentTag.Id;
    }

    private static async Task SyncRegularExpensesForMonthAsync(
        ApplicationDbContext dbContext,
        MonthPlan monthPlan,
        CancellationToken cancellationToken)
    {
        var definitions = await dbContext.RegularExpenseDefinitions
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (definitions.Count == 0)
        {
            return;
        }

        var existingDefinitionIds = await dbContext.Expenses
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.MonthPlanId == monthPlan.Id && x.RegularExpenseDefinitionId.HasValue)
            .Select(x => x.RegularExpenseDefinitionId!.Value)
            .ToListAsync(cancellationToken);

        var existingSet = existingDefinitionIds.ToHashSet();

        var maxOrder = await dbContext.Expenses
            .Where(x => x.MonthPlanId == monthPlan.Id)
            .Select(x => (int?)x.Order)
            .MaxAsync(cancellationToken) ?? 0;

        foreach (var definition in definitions)
        {
            if (existingSet.Contains(definition.Id))
            {
                continue;
            }

            maxOrder++;
            dbContext.Expenses.Add(new Expense
            {
                MonthPlanId = monthPlan.Id,
                Order = maxOrder,
                Name = definition.Name,
                CategoryId = definition.CategoryId,
                TagId = definition.TagId,
                RegularExpenseDefinitionId = definition.Id,
                PlannedAmount = definition.Amount,
                ActualAmount = 0,
                ShowRemainingInUI = definition.ShowRemainingInUI
            });
        }
    }

    private async Task EmitBudgetExceededEventIfNeededAsync(
        EnvelopeUsageSnapshot? before,
        EnvelopeUsageSnapshot? after,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        if (after is null)
        {
            return;
        }

        var wasExceeded = before is not null && before.SpentAmount > before.Limit;
        var isExceeded = after.SpentAmount > after.Limit;

        if (wasExceeded || !isExceeded)
        {
            return;
        }

        await appEventPublisher.PublishAsync(new BudgetExceededEvent
        {
            CategoryId = after.CategoryId,
            CategoryName = after.CategoryName,
            Year = year,
            Month = month,
            EnvelopeLimit = after.Limit,
            SpentAmount = after.SpentAmount
        }, cancellationToken);
    }

    private static async Task<EnvelopeUsageSnapshot?> GetEnvelopeUsageSnapshotAsync(
        ApplicationDbContext dbContext,
        int monthPlanId,
        int categoryId,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == categoryId, cancellationToken);

        if (category?.EnvelopeLimit is not > 0)
        {
            return null;
        }

        var spentAmount = await dbContext.Expenses
            .Where(x => x.MonthPlanId == monthPlanId && x.CategoryId == categoryId)
            .SumAsync(x => x.ActualAmount, cancellationToken);

        return new EnvelopeUsageSnapshot(category.Id, category.Name, category.EnvelopeLimit.Value, spentAmount);
    }

    private sealed record EnvelopeUsageSnapshot(int CategoryId, string CategoryName, decimal Limit, decimal SpentAmount);
}