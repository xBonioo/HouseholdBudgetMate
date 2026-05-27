using System.Globalization;
using System.Text.Json;
using HouseholdBudgetMate.Abstractions.Contracts.Audit.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Audit.Requests;
using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Domain.Infrastructure;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Application.Services;

public sealed class AuditService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    CurrentUserContext currentUserContext) : IAuditService
{
    private static readonly CultureInfo PolishCulture = new("pl-PL");

    public async Task<IReadOnlyList<AuditLogDto>> SearchAsync(
        SearchAuditLogsRequest request,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureCurrentUserIsAdminAsync(dbContext, cancellationToken);

        if (string.IsNullOrWhiteSpace(currentUserContext.BudgetOwnerUserId))
        {
            throw new ForbiddenException("Admin permissions are required.");
        }

        var budgetOwnerUserId = currentUserContext.BudgetOwnerUserId;

        var query = dbContext.AuditLogs
            .AsNoTracking()
            .Include(x => x.User)
            .Where(x => x.BudgetOwnerUserId == budgetOwnerUserId);

        if (!string.IsNullOrWhiteSpace(request.EntityType))
        {
            query = query.Where(x => x.EntityType == request.EntityType);
        }

        if (!string.IsNullOrWhiteSpace(request.Operation))
        {
            query = query.Where(x => x.Operation == request.Operation);
        }

        if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            query = query.Where(x => x.UserId == request.UserId);
        }

        if (request.FromUtc is not null)
        {
            query = query.Where(x => x.ChangedAtUtc >= request.FromUtc);
        }

        if (request.ToUtc is not null)
        {
            query = query.Where(x => x.ChangedAtUtc <= request.ToUtc);
        }

        var logs = await query
            .OrderByDescending(x => x.ChangedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(500)
            .ToListAsync(cancellationToken);

        var dtos = logs.Select(MapToDto).ToList();
        await EnrichEntityContextsAsync(dbContext, dtos, cancellationToken);
        return dtos;
    }

    private static AuditLogDto MapToDto(AuditLog log)
    {
        return new AuditLogDto
        {
            Id = log.Id,
            EntityType = log.EntityType,
            EntityId = log.EntityId,
            UserId = log.UserId,
            UserName = log.User?.Username ?? log.UserId,
            BudgetOwnerUserId = log.BudgetOwnerUserId,
            Operation = log.Operation,
            EntityContext = $"{log.EntityType} #{log.EntityId}",
            ChangedAtUtc = log.ChangedAtUtc,
            DiffItems = BuildDiff(log.OldValuesJson, log.NewValuesJson, log.EntityType)
        };
    }

    private static async Task EnrichEntityContextsAsync(
        ApplicationDbContext dbContext,
        IReadOnlyList<AuditLogDto> logs,
        CancellationToken cancellationToken)
    {
        if (logs.Count == 0)
        {
            return;
        }

        var contextByKey = new Dictionary<(string EntityType, int EntityId), string>();

        await AddExpenseContextsAsync(dbContext, logs, contextByKey, cancellationToken);
        await AddExpenseLineItemContextsAsync(dbContext, logs, contextByKey, cancellationToken);
        await AddIncomeContextsAsync(dbContext, logs, contextByKey, cancellationToken);
        await AddAccountContextsAsync(dbContext, logs, contextByKey, cancellationToken);
        await AddAccountMonthBalanceContextsAsync(dbContext, logs, contextByKey, cancellationToken);
        await AddCategoryContextsAsync(dbContext, logs, contextByKey, cancellationToken);
        await AddLoanInstallmentContextsAsync(dbContext, logs, contextByKey, cancellationToken);
        await AddMonthSavingsTransferContextsAsync(dbContext, logs, contextByKey, cancellationToken);
        await AddRegularExpenseDefinitionContextsAsync(dbContext, logs, contextByKey, cancellationToken);
        await AddRegularIncomeDefinitionContextsAsync(dbContext, logs, contextByKey, cancellationToken);
        await EnrichDiffDisplayValuesAsync(dbContext, logs, cancellationToken);

        foreach (var log in logs)
        {
            if (contextByKey.TryGetValue((log.EntityType, log.EntityId), out var context))
            {
                log.EntityContext = context;
            }
        }
    }

    private static async Task AddExpenseContextsAsync(
        ApplicationDbContext dbContext,
        IReadOnlyList<AuditLogDto> logs,
        Dictionary<(string EntityType, int EntityId), string> contextByKey,
        CancellationToken cancellationToken)
    {
        var ids = GetIds(logs, nameof(Expense));
        if (ids.Count == 0)
        {
            return;
        }

        var expenses = await dbContext.Expenses
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Tag)
            .Include(x => x.MonthPlan)
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var expense in expenses)
        {
            contextByKey[(nameof(Expense), expense.Id)] =
                JoinContext(
                    $"Wydatek: {expense.Name}",
                    $"kategoria: {expense.Category.Name}",
                    expense.Tag is null ? null : $"tag: {expense.Tag.Name}",
                    $"miesiąc: {expense.MonthPlan.Year}-{expense.MonthPlan.Month:D2}");
        }
    }

    private static async Task AddExpenseLineItemContextsAsync(
        ApplicationDbContext dbContext,
        IReadOnlyList<AuditLogDto> logs,
        Dictionary<(string EntityType, int EntityId), string> contextByKey,
        CancellationToken cancellationToken)
    {
        var ids = GetIds(logs, nameof(ExpenseLineItem));
        if (ids.Count == 0)
        {
            return;
        }

        var lineItems = await dbContext.ExpenseLineItems
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.Tag)
            .Include(x => x.Expense)
            .ThenInclude(x => x.Category)
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var lineItem in lineItems)
        {
            contextByKey[(nameof(ExpenseLineItem), lineItem.Id)] =
                JoinContext(
                    $"Pozycja: {lineItem.Description}",
                    $"wydatek: {lineItem.Expense.Name}",
                    $"kategoria: {lineItem.Expense.Category.Name}",
                    lineItem.Tag is null ? null : $"tag: {lineItem.Tag.Name}",
                    $"data: {lineItem.OccurredAt:yyyy-MM-dd}");
        }
    }

    private static async Task AddIncomeContextsAsync(
        ApplicationDbContext dbContext,
        IReadOnlyList<AuditLogDto> logs,
        Dictionary<(string EntityType, int EntityId), string> contextByKey,
        CancellationToken cancellationToken)
    {
        var ids = GetIds(logs, nameof(Income));
        if (ids.Count == 0)
        {
            return;
        }

        var incomes = await dbContext.Incomes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.Account)
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var income in incomes)
        {
            contextByKey[(nameof(Income), income.Id)] =
                JoinContext(
                    $"Wpływ: {income.Name}",
                    $"konto: {income.Account.Name}",
                    $"miesiąc: {income.Year}-{income.Month:D2}");
        }
    }

    private static async Task AddAccountContextsAsync(
        ApplicationDbContext dbContext,
        IReadOnlyList<AuditLogDto> logs,
        Dictionary<(string EntityType, int EntityId), string> contextByKey,
        CancellationToken cancellationToken)
    {
        var ids = GetIds(logs, nameof(Account));
        if (ids.Count == 0)
        {
            return;
        }

        var accounts = await dbContext.Accounts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var account in accounts)
        {
            contextByKey[(nameof(Account), account.Id)] = $"Konto: {account.Name}";
        }
    }

    private static async Task AddAccountMonthBalanceContextsAsync(
        ApplicationDbContext dbContext,
        IReadOnlyList<AuditLogDto> logs,
        Dictionary<(string EntityType, int EntityId), string> contextByKey,
        CancellationToken cancellationToken)
    {
        var ids = GetIds(logs, nameof(AccountMonthBalance));
        if (ids.Count == 0)
        {
            return;
        }

        var balances = await dbContext.AccountMonthBalances
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.Account)
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var balance in balances)
        {
            contextByKey[(nameof(AccountMonthBalance), balance.Id)] =
                JoinContext(
                    $"Saldo: {balance.Account.Name}",
                    $"miesiąc: {balance.Year}-{balance.Month:D2}");
        }
    }

    private static async Task AddCategoryContextsAsync(
        ApplicationDbContext dbContext,
        IReadOnlyList<AuditLogDto> logs,
        Dictionary<(string EntityType, int EntityId), string> contextByKey,
        CancellationToken cancellationToken)
    {
        var ids = GetIds(logs, nameof(Category));
        if (ids.Count == 0)
        {
            return;
        }

        var categories = await dbContext.Categories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var category in categories)
        {
            contextByKey[(nameof(Category), category.Id)] = $"Kategoria: {category.Name}";
        }
    }

    private static async Task AddLoanInstallmentContextsAsync(
        ApplicationDbContext dbContext,
        IReadOnlyList<AuditLogDto> logs,
        Dictionary<(string EntityType, int EntityId), string> contextByKey,
        CancellationToken cancellationToken)
    {
        var ids = GetIds(logs, nameof(LoanInstallment));
        if (ids.Count == 0)
        {
            return;
        }

        var installments = await dbContext.LoanInstallments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.Loan)
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var installment in installments)
        {
            contextByKey[(nameof(LoanInstallment), installment.Id)] =
                JoinContext(
                    $"Rata: {installment.Loan.Name}",
                    $"termin: {installment.DueDate:yyyy-MM-dd}",
                    $"kwota: {installment.Amount.ToString("N2", PolishCulture)}");
        }
    }

    private static async Task AddMonthSavingsTransferContextsAsync(
        ApplicationDbContext dbContext,
        IReadOnlyList<AuditLogDto> logs,
        Dictionary<(string EntityType, int EntityId), string> contextByKey,
        CancellationToken cancellationToken)
    {
        var ids = GetIds(logs, nameof(MonthSavingsTransferItem));
        if (ids.Count == 0)
        {
            return;
        }

        var transfers = await dbContext.MonthSavingsTransferItems
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.MonthPlan)
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var transfer in transfers)
        {
            contextByKey[(nameof(MonthSavingsTransferItem), transfer.Id)] =
                JoinContext(
                    "Oszczędności",
                    $"miesiąc: {FormatMonthPlan(transfer.MonthPlan.Year, transfer.MonthPlan.Month)}",
                    $"kwota: {transfer.Amount.ToString("N2", PolishCulture)}",
                    $"data: {transfer.TransferDate:yyyy-MM-dd}");
        }
    }

    private static async Task AddRegularExpenseDefinitionContextsAsync(
        ApplicationDbContext dbContext,
        IReadOnlyList<AuditLogDto> logs,
        Dictionary<(string EntityType, int EntityId), string> contextByKey,
        CancellationToken cancellationToken)
    {
        var ids = GetIds(logs, nameof(RegularExpenseDefinition));
        if (ids.Count == 0)
        {
            return;
        }

        var definitions = await dbContext.RegularExpenseDefinitions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Tag)
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var definition in definitions)
        {
            contextByKey[(nameof(RegularExpenseDefinition), definition.Id)] =
                JoinContext(
                    $"Cykliczny wydatek: {definition.Name}",
                    $"kategoria: {definition.Category.Name}",
                    definition.Tag is null ? null : $"tag: {definition.Tag.Name}",
                    $"kwota: {definition.Amount.ToString("N2", PolishCulture)}");
        }
    }

    private static async Task AddRegularIncomeDefinitionContextsAsync(
        ApplicationDbContext dbContext,
        IReadOnlyList<AuditLogDto> logs,
        Dictionary<(string EntityType, int EntityId), string> contextByKey,
        CancellationToken cancellationToken)
    {
        var ids = GetIds(logs, nameof(RegularIncomeDefinition));
        if (ids.Count == 0)
        {
            return;
        }

        var definitions = await dbContext.RegularIncomeDefinitions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.Account)
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var definition in definitions)
        {
            contextByKey[(nameof(RegularIncomeDefinition), definition.Id)] =
                JoinContext(
                    $"Cykliczny wpływ: {definition.Name}",
                    $"konto: {definition.Account.Name}",
                    $"dzień miesiąca: {definition.DayOfMonth}",
                    $"kwota: {definition.Amount.ToString("N2", PolishCulture)}");
        }
    }

    private static async Task EnrichDiffDisplayValuesAsync(
        ApplicationDbContext dbContext,
        IReadOnlyList<AuditLogDto> logs,
        CancellationToken cancellationToken)
    {
        var accountIds = CollectDiffIds(logs, "AccountId");
        var categoryIds = CollectDiffIds(logs, "CategoryId");
        var tagIds = CollectDiffIds(logs, "TagId");
        var monthPlanIds = CollectDiffIds(logs, "MonthPlanId");
        var expenseIds = CollectDiffIds(logs, "ExpenseId");
        var loanIds = CollectDiffIds(logs, "LoanId");
        var loanInstallmentIds = CollectDiffIds(logs, "LoanInstallmentId");

        var accountNames = await LoadAccountNamesAsync(dbContext, accountIds, cancellationToken);
        var categoryNames = await LoadCategoryNamesAsync(dbContext, categoryIds, cancellationToken);
        var tagNames = await LoadTagNamesAsync(dbContext, tagIds, cancellationToken);
        var monthPlanNames = await LoadMonthPlanNamesAsync(dbContext, monthPlanIds, cancellationToken);
        var expenseNames = await LoadExpenseNamesAsync(dbContext, expenseIds, cancellationToken);
        var loanNames = await LoadLoanNamesAsync(dbContext, loanIds, cancellationToken);
        var installmentNames = await LoadLoanInstallmentNamesAsync(dbContext, loanInstallmentIds, cancellationToken);

        foreach (var diff in logs.SelectMany(x => x.DiffItems))
        {
            switch (diff.PropertyName)
            {
                case "AccountId":
                    ReplaceIdValues(diff, accountNames);
                    break;
                case "CategoryId":
                    ReplaceIdValues(diff, categoryNames);
                    break;
                case "TagId":
                    ReplaceIdValues(diff, tagNames);
                    break;
                case "MonthPlanId":
                    ReplaceIdValues(diff, monthPlanNames);
                    break;
                case "ExpenseId":
                    ReplaceIdValues(diff, expenseNames);
                    break;
                case "LoanId":
                    ReplaceIdValues(diff, loanNames);
                    break;
                case "LoanInstallmentId":
                    ReplaceIdValues(diff, installmentNames);
                    break;
            }
        }
    }

    private static HashSet<int> CollectDiffIds(IReadOnlyList<AuditLogDto> logs, string propertyName)
    {
        return logs
            .SelectMany(x => x.DiffItems)
            .Where(x => x.PropertyName == propertyName)
            .SelectMany(x => new[] { x.OldValue, x.NewValue })
            .Select(ParseInt)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToHashSet();
    }

    private static async Task<Dictionary<int, string>> LoadAccountNamesAsync(
        ApplicationDbContext dbContext,
        HashSet<int> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await dbContext.Accounts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
    }

    private static async Task<Dictionary<int, string>> LoadCategoryNamesAsync(
        ApplicationDbContext dbContext,
        HashSet<int> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await dbContext.Categories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
    }

    private static async Task<Dictionary<int, string>> LoadTagNamesAsync(
        ApplicationDbContext dbContext,
        HashSet<int> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await dbContext.Tags
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
    }

    private static async Task<Dictionary<int, string>> LoadMonthPlanNamesAsync(
        ApplicationDbContext dbContext,
        HashSet<int> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await dbContext.MonthPlans
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => FormatMonthPlan(x.Year, x.Month), cancellationToken);
    }

    private static async Task<Dictionary<int, string>> LoadExpenseNamesAsync(
        ApplicationDbContext dbContext,
        HashSet<int> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await dbContext.Expenses
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
    }

    private static async Task<Dictionary<int, string>> LoadLoanNamesAsync(
        ApplicationDbContext dbContext,
        HashSet<int> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await dbContext.Loans
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
    }

    private static async Task<Dictionary<int, string>> LoadLoanInstallmentNamesAsync(
        ApplicationDbContext dbContext,
        HashSet<int> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await dbContext.LoanInstallments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.Loan)
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                x => $"{x.Loan.Name}, {x.DueDate:yyyy-MM-dd}",
                cancellationToken);
    }

    private static void ReplaceIdValues(AuditDiffItemDto diff, IReadOnlyDictionary<int, string> names)
    {
        diff.OldValue = ReplaceIdValue(diff.OldValue, names);
        diff.NewValue = ReplaceIdValue(diff.NewValue, names);
    }

    private static string ReplaceIdValue(string value, IReadOnlyDictionary<int, string> names)
    {
        var id = ParseInt(value);
        if (!id.HasValue || !names.TryGetValue(id.Value, out var name))
        {
            return value;
        }

        return name;
    }

    private static HashSet<int> GetIds(IReadOnlyList<AuditLogDto> logs, string entityType)
    {
        return logs
            .Where(x => x.EntityType == entityType)
            .Select(x => x.EntityId)
            .ToHashSet();
    }

    private static string JoinContext(params string?[] parts)
    {
        return string.Join("; ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static IReadOnlyList<AuditDiffItemDto> BuildDiff(
        string oldValuesJson,
        string newValuesJson,
        string entityType)
    {
        var oldValues = ParseJsonObject(oldValuesJson);
        var newValues = ParseJsonObject(newValuesJson);
        var propertyNames = oldValues.Keys
            .Union(newValues.Keys)
            .OrderBy(x => x)
            .ToList();

        return propertyNames.Select(propertyName => new AuditDiffItemDto
        {
            PropertyName = propertyName,
            DisplayName = GetPropertyLabel(entityType, propertyName),
            OldValue = oldValues.TryGetValue(propertyName, out var oldValue) ? FormatValue(propertyName, oldValue) : "-",
            NewValue = newValues.TryGetValue(propertyName, out var newValue) ? FormatValue(propertyName, newValue) : "-"
        }).ToList();
    }

    private static Dictionary<string, JsonElement> ParseJsonObject(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string FormatValue(string propertyName, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number
            && IntegerLikeProperties.Contains(propertyName)
            && value.TryGetInt32(out var intValue))
        {
            return propertyName == "Month" && intValue is >= 1 and <= 12
                ? GetMonthName(intValue)
                : intValue.ToString(CultureInfo.InvariantCulture);
        }

        return value.ValueKind switch
        {
            JsonValueKind.Null => "-",
            JsonValueKind.Undefined => "-",
            JsonValueKind.True => "Tak",
            JsonValueKind.False => "Nie",
            JsonValueKind.Number when value.TryGetDecimal(out var decimalValue)
                => decimalValue.ToString("N2", PolishCulture),
            JsonValueKind.String => value.GetString() ?? "-",
            _ => value.ToString()
        };
    }

    private static readonly HashSet<string> IntegerLikeProperties =
    [
        "AccountId",
        "CategoryId",
        "DayOfMonth",
        "ExpenseId",
        "LoanId",
        "LoanInstallmentId",
        "Month",
        "MonthPlanId",
        "Order",
        "RegularExpenseDefinitionId",
        "RegularIncomeDefinitionId",
        "TagId",
        "Year"
    ];

    private static int? ParseInt(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "-")
        {
            return null;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var invariantValue))
        {
            return invariantValue;
        }

        if (decimal.TryParse(value, NumberStyles.Number, PolishCulture, out var decimalValue))
        {
            return decimal.ToInt32(decimalValue);
        }

        return null;
    }

    private static string FormatMonthPlan(int year, int month)
    {
        return $"{GetMonthName(month)} {year}";
    }

    private static string GetMonthName(int month)
    {
        return PolishCulture.DateTimeFormat.GetMonthName(month);
    }

    private static string GetPropertyLabel(string entityType, string propertyName)
    {
        return propertyName switch
        {
            "AccountId" => "Konto",
            "ActualAmount" => "Kwota rzeczywista",
            "ArchivedAtUtc" => "Data archiwizacji",
            "CategoryId" => "Kategoria",
            "ClosingBalance" => "Saldo zamknięcia",
            "Color" => "Kolor",
            "DeletedAtUtc" => "Data usunięcia",
            "EnvelopeLimit" => "Limit koperty",
            "ExpectedDayOfMonth" => "Planowana data",
            "ExpenseId" => "Wydatek",
            "DayOfMonth" => "Dzień miesiąca",
            "IsArchived" => "Archiwalne",
            "IsDeleted" => "Usunięte",
            "IsPaid" => "Spłacona",
            "IsRegular" => "Cykliczne",
            "InterestAmount" => "Część odsetkowa",
            "LoanInstallmentId" => "Rata kredytu",
            "LoanId" => "Kredyt",
            "Month" => "Miesiąc",
            "MonthPlanId" => "Plan miesiąca",
            "Name" => "Nazwa",
            "OccurredAt" => "Data pozycji",
            "Order" => "Kolejność",
            "PaidAtUtc" => "Data spłaty",
            "PlannedAmount" => "Kwota planowana",
            "PrincipalAmount" => "Część kapitałowa",
            "RegularExpenseDefinitionId" => "Definicja cykliczna",
            "RegularIncomeDefinitionId" => "Definicja cykliczna",
            "ShowRemainingInUI" => "Pokazuj pozostało",
            "SupportsLineItems" => "Pozycje szczegółowe",
            "TagId" => "Tag",
            "TransferDate" => "Data przelewu",
            "Type" => entityType == nameof(Account) ? "Typ konta" : "Typ",
            "Year" => "Rok",
            _ => propertyName
        };
    }

    private async Task EnsureCurrentUserIsAdminAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentUserContext.UserId))
        {
            throw new ForbiddenException("Admin permissions are required.");
        }

        var isAdmin = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == currentUserContext.UserId
                     && x.Id != User.DefaultUserId
                     && x.IsAdmin,
                cancellationToken);

        if (!isAdmin)
        {
            throw new ForbiddenException("Admin permissions are required.");
        }
    }
}
