using System.Globalization;
using System.Reflection;
using HouseholdBudgetMate.Abstractions.Contracts.Backup;
using HouseholdBudgetMate.Abstractions.Contracts.Backup.Dto;
using HouseholdBudgetMate.Application.Kernel.Timing;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Domain.Infrastructure;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Application.Services.Backup;

internal sealed class BackupSnapshotBuilder(IDateTimeProvider dateTimeProvider)
{
    private const string SensitiveProfileWarning =
        "This plain JSON backup contains household financial data and profile authentication secrets.";

    public async Task<BackupEnvelopeDto> BuildAsync(
        ApplicationDbContext dbContext,
        BackupSection requestedSections,
        bool includeAllBudgetOwners,
        BackupPeriodRange? budgetPeriodRange,
        CancellationToken cancellationToken)
    {
        var includedSections = NormalizeSections(requestedSections);
        var warnings = includedSections.HasFlag(BackupSection.Profiles)
            ? [SensitiveProfileWarning]
            : Array.Empty<string>();

        var payload = new BackupPayloadDto();

        if (includedSections.HasFlag(BackupSection.Taxonomy))
        {
            payload.Taxonomy = new BackupRecordSectionDto
            {
                Records = await BuildTaxonomyRecordsAsync(dbContext, cancellationToken)
            };
        }

        if (includedSections.HasFlag(BackupSection.Budget))
        {
            payload.Budget = new BackupRecordSectionDto
            {
                Records = await BuildBudgetRecordsAsync(dbContext, includeAllBudgetOwners, budgetPeriodRange, cancellationToken)
            };
        }

        if (includedSections.HasFlag(BackupSection.Profiles))
        {
            payload.Profiles = new BackupRecordSectionDto
            {
                Records = await BuildProfileRecordsAsync(dbContext, cancellationToken)
            };
        }

        if (includedSections.HasFlag(BackupSection.Audit))
        {
            payload.Audit = new BackupRecordSectionDto
            {
                Records = await BuildAuditRecordsAsync(dbContext, cancellationToken)
            };
        }

        if (includedSections.HasFlag(BackupSection.Logs))
        {
            payload.Logs = new BackupRecordSectionDto
            {
                Records = await BuildLogRecordsAsync(dbContext, cancellationToken)
            };
        }

        if (includedSections.HasFlag(BackupSection.SettingsMetadata))
        {
            payload.SettingsMetadata = new BackupSettingsMetadataSectionDto();
        }

        var createdByUser = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == dbContext.CurrentUserId, cancellationToken);

        return new BackupEnvelopeDto
        {
            SchemaVersion = BackupEnvelopeDto.CurrentSchemaVersion,
            Manifest = new BackupManifestDto
            {
                CreatedAtUtc = dateTimeProvider.GetUtcDateTimeOffset(),
                CreatedByUserId = dbContext.CurrentUserId,
                CreatedByUsername = createdByUser?.Username ?? string.Empty,
                RequestedSections = requestedSections,
                IncludedSections = includedSections,
                BudgetFromYear = budgetPeriodRange?.FromYear,
                BudgetFromMonth = budgetPeriodRange?.FromMonth,
                BudgetToYear = budgetPeriodRange?.ToYear,
                BudgetToMonth = budgetPeriodRange?.ToMonth,
                CountsByTable = CountRecords(payload),
                Warnings = warnings
            },
            Payload = payload
        };
    }

    private static BackupSection NormalizeSections(BackupSection requestedSections)
    {
        if (requestedSections == BackupSection.None)
        {
            return BackupSection.None;
        }

        var sections = requestedSections;
        if (sections.HasFlag(BackupSection.Budget))
        {
            sections |= BackupSection.Taxonomy;
        }

        return sections;
    }

    private static async Task<IReadOnlyList<BackupRecordDto>> BuildTaxonomyRecordsAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var records = new List<BackupRecordDto>();

        var categories = await dbContext.Categories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        records.AddRange(categories.Select(x => ToRecord("categories", x.Id, x)));

        var tags = await dbContext.Tags
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        records.AddRange(tags.Select(x =>
        {
            var references = new Dictionary<string, string>
            {
                ["category"] = PortableId("categories", x.CategoryId)
            };

            if (x.ParentTagId.HasValue)
            {
                references["parentTag"] = PortableId("tags", x.ParentTagId.Value);
            }

            return ToRecord("tags", x.Id, x, references);
        }));

        return records;
    }

    private static async Task<IReadOnlyList<BackupRecordDto>> BuildBudgetRecordsAsync(
        ApplicationDbContext dbContext,
        bool includeAllBudgetOwners,
        BackupPeriodRange? periodRange,
        CancellationToken cancellationToken)
    {
        var ownerUserId = dbContext.CurrentBudgetOwnerUserId;
        var records = new List<BackupRecordDto>();

        var allAccounts = await dbContext.Accounts.IgnoreQueryFilters().AsNoTracking()
            .Where(x => includeAllBudgetOwners || x.UserId == ownerUserId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var balances = (await dbContext.AccountMonthBalances.IgnoreQueryFilters().AsNoTracking()
            .Where(x => includeAllBudgetOwners || x.UserId == ownerUserId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken))
            .Where(x => periodRange is null || periodRange.Value.Contains(x.Year, x.Month))
            .ToList();

        var monthPlans = (await dbContext.MonthPlans.IgnoreQueryFilters().AsNoTracking()
            .Where(x => includeAllBudgetOwners || x.UserId == ownerUserId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken))
            .Where(x => periodRange is null || periodRange.Value.Contains(x.Year, x.Month))
            .ToList();
        var monthPlanIds = monthPlans.Select(x => x.Id).ToHashSet();
        records.AddRange(monthPlans.Select(x => ToRecord("monthPlans", x.Id, x)));

        var expenses = (await dbContext.Expenses.IgnoreQueryFilters().AsNoTracking()
            .Where(x => includeAllBudgetOwners || x.UserId == ownerUserId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken))
            .Where(x => monthPlanIds.Contains(x.MonthPlanId))
            .ToList();
        var expenseIds = expenses.Select(x => x.Id).ToHashSet();
        var expenseRegularDefinitionIds = expenses
            .Select(x => x.RegularExpenseDefinitionId)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToHashSet();
        var expenseLoanInstallmentIds = expenses
            .Select(x => x.LoanInstallmentId)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToHashSet();
        records.AddRange(expenses.Select(x => ToRecord("expenses", x.Id, x, References(
            ("monthPlan", "monthPlans", x.MonthPlanId),
            ("category", "categories", x.CategoryId),
            ("tag", "tags", x.TagId),
            ("regularExpenseDefinition", "regularExpenseDefinitions", x.RegularExpenseDefinitionId),
            ("loanInstallment", "loanInstallments", x.LoanInstallmentId)))));

        var lineItems = (await dbContext.ExpenseLineItems.IgnoreQueryFilters().AsNoTracking()
            .Where(x => includeAllBudgetOwners || x.UserId == ownerUserId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken))
            .Where(x => expenseIds.Contains(x.ExpenseId))
            .ToList();
        records.AddRange(lineItems.Select(x => ToRecord("expenseLineItems", x.Id, x, References(
            ("expense", "expenses", x.ExpenseId),
            ("tag", "tags", x.TagId)))));

        var transfers = (await dbContext.MonthSavingsTransferItems.IgnoreQueryFilters().AsNoTracking()
            .Where(x => includeAllBudgetOwners || x.UserId == ownerUserId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken))
            .Where(x => monthPlanIds.Contains(x.MonthPlanId))
            .ToList();
        records.AddRange(transfers.Select(x => ToRecord("monthSavingsTransferItems", x.Id, x, new Dictionary<string, string>
        {
            ["monthPlan"] = PortableId("monthPlans", x.MonthPlanId)
        })));

        var incomes = (await dbContext.Incomes.IgnoreQueryFilters().AsNoTracking()
            .Where(x => includeAllBudgetOwners || x.UserId == ownerUserId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken))
            .Where(x => periodRange is null || periodRange.Value.Contains(x.Year, x.Month))
            .ToList();
        var incomeRegularDefinitionIds = incomes
            .Select(x => x.RegularIncomeDefinitionId)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToHashSet();
        records.AddRange(incomes.Select(x => ToRecord("incomes", x.Id, x, References(
            ("account", "accounts", x.AccountId),
            ("regularIncomeDefinition", "regularIncomeDefinitions", x.RegularIncomeDefinitionId)))));

        var annualPlans = (await dbContext.AnnualPlans.IgnoreQueryFilters().AsNoTracking()
            .Where(x => includeAllBudgetOwners || x.UserId == ownerUserId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken))
            .Where(x => periodRange is null || periodRange.Value.ContainsYear(x.Year))
            .ToList();
        records.AddRange(annualPlans.Select(x => ToRecord("annualPlans", x.Id, x)));

        var allRegularExpenses = await dbContext.RegularExpenseDefinitions.IgnoreQueryFilters().AsNoTracking()
            .Where(x => includeAllBudgetOwners || x.UserId == ownerUserId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        var regularExpenses = allRegularExpenses
            .Where(x => periodRange is null || expenseRegularDefinitionIds.Contains(x.Id))
            .ToList();
        records.AddRange(regularExpenses.Select(x => ToRecord("regularExpenseDefinitions", x.Id, x, References(
            ("category", "categories", x.CategoryId),
            ("tag", "tags", x.TagId)))));

        var allRegularIncomes = await dbContext.RegularIncomeDefinitions.IgnoreQueryFilters().AsNoTracking()
            .Where(x => includeAllBudgetOwners || x.UserId == ownerUserId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        var regularIncomes = allRegularIncomes
            .Where(x => periodRange is null || incomeRegularDefinitionIds.Contains(x.Id))
            .ToList();
        records.AddRange(regularIncomes.Select(x => ToRecord("regularIncomeDefinitions", x.Id, x, new Dictionary<string, string>
        {
            ["account"] = PortableId("accounts", x.AccountId)
        })));

        var accountIds = balances.Select(x => x.AccountId)
            .Concat(incomes.Select(x => x.AccountId))
            .Concat(regularIncomes.Select(x => x.AccountId))
            .ToHashSet();
        var accounts = allAccounts
            .Where(x => periodRange is null || accountIds.Contains(x.Id))
            .ToList();
        records.InsertRange(0, accounts.Select(x => ToRecord("accounts", x.Id, x)));
        records.AddRange(balances.Select(x => ToRecord("accountMonthBalances", x.Id, x, new Dictionary<string, string>
        {
            ["account"] = PortableId("accounts", x.AccountId)
        })));

        var allLoans = await dbContext.Loans.IgnoreQueryFilters().AsNoTracking()
            .Where(x => includeAllBudgetOwners || x.UserId == ownerUserId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var allInstallments = await dbContext.LoanInstallments.IgnoreQueryFilters().AsNoTracking()
            .Where(x => includeAllBudgetOwners || x.Loan.UserId == ownerUserId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        var installments = allInstallments
            .Where(x => periodRange is null
                        || periodRange.Value.Contains(x.Year, x.Month)
                        || expenseLoanInstallmentIds.Contains(x.Id))
            .ToList();
        var installmentLoanIds = installments.Select(x => x.LoanId).ToHashSet();
        var loans = allLoans
            .Where(x => periodRange is null
                        || installmentLoanIds.Contains(x.Id)
                        || periodRange.Value.Overlaps(x.StartDate, x.EndDate))
            .ToList();
        var loanIds = loans.Select(x => x.Id).ToHashSet();
        records.AddRange(loans.Select(x => ToRecord("loans", x.Id, x, References(
            ("tag", "tags", x.TagId)))));
        records.AddRange(installments.Select(x => ToRecord("loanInstallments", x.Id, x, new Dictionary<string, string>
        {
            ["loan"] = PortableId("loans", x.LoanId)
        })));

        var rateEntries = (await dbContext.LoanRateEntries.IgnoreQueryFilters().AsNoTracking()
            .Where(x => includeAllBudgetOwners || x.Loan.UserId == ownerUserId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken))
            .Where(x => loanIds.Contains(x.LoanId))
            .Where(x => periodRange is null || periodRange.Value.StartsBeforeOrInRange(x.EffectiveFrom))
            .ToList();
        records.AddRange(rateEntries.Select(x => ToRecord("loanRateEntries", x.Id, x, new Dictionary<string, string>
        {
            ["loan"] = PortableId("loans", x.LoanId)
        })));

        var charges = (await dbContext.LoanCharges.IgnoreQueryFilters().AsNoTracking()
            .Where(x => includeAllBudgetOwners || x.Loan.UserId == ownerUserId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken))
            .Where(x => loanIds.Contains(x.LoanId))
            .Where(x => periodRange is null
                        || periodRange.Value.Overlaps(x.StartDate, x.EndDate ?? DateOnly.MaxValue))
            .ToList();
        records.AddRange(charges.Select(x => ToRecord("loanCharges", x.Id, x, new Dictionary<string, string>
        {
            ["loan"] = PortableId("loans", x.LoanId)
        })));

        return records;
    }

    private static async Task<IReadOnlyList<BackupRecordDto>> BuildProfileRecordsAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var users = await dbContext.Users
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return users.Select(x => ToRecord("users", x.Id, x, StringReferences(
            ("budgetOwnerUser", "users", x.BudgetOwnerUserId)))).ToList();
    }

    private static async Task<IReadOnlyList<BackupRecordDto>> BuildAuditRecordsAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var auditLogs = await dbContext.AuditLogs
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return auditLogs.Select(x => ToRecord("auditLogs", x.Id, x, StringReferences(
            ("user", "users", x.UserId),
            ("budgetOwnerUser", "users", x.BudgetOwnerUserId)))).ToList();
    }

    private static async Task<IReadOnlyList<BackupRecordDto>> BuildLogRecordsAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var logs = await dbContext.Logs
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return logs.Select(x => ToRecord("logs", x.Id, x)).ToList();
    }

    private static BackupRecordDto ToRecord(
        string table,
        object id,
        object entity,
        IReadOnlyDictionary<string, string>? references = null)
    {
        return new BackupRecordDto
        {
            Table = table,
            PortableId = PortableId(table, id),
            Fields = ReadScalarFields(entity),
            References = references ?? new Dictionary<string, string>()
        };
    }

    private static IReadOnlyDictionary<string, string?> ReadScalarFields(object entity)
    {
        return entity.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(x => x.GetMethod is not null && IsScalarType(x.PropertyType))
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToDictionary(x => x.Name, x => FormatValue(x.GetValue(entity)), StringComparer.Ordinal);
    }

    private static bool IsScalarType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsEnum
            || underlying == typeof(bool)
            || underlying == typeof(byte)
            || underlying == typeof(short)
            || underlying == typeof(int)
            || underlying == typeof(long)
            || underlying == typeof(decimal)
            || underlying == typeof(double)
            || underlying == typeof(float)
            || underlying == typeof(string)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(DateOnly)
            || underlying == typeof(TimeOnly)
            || underlying == typeof(Guid);
    }

    private static string? FormatValue(object? value)
    {
        return value switch
        {
            null => null,
            DateOnly dateOnly => dateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeOnly timeOnly => timeOnly.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            decimal decimalValue => decimalValue.ToString("0.00############################", CultureInfo.InvariantCulture),
            double doubleValue => doubleValue.ToString("R", CultureInfo.InvariantCulture),
            float floatValue => floatValue.ToString("R", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    private static IReadOnlyDictionary<string, string> References(params (string Name, string Table, int? Id)[] references)
    {
        return references
            .Where(x => x.Id.HasValue)
            .ToDictionary(x => x.Name, x => PortableId(x.Table, x.Id!.Value), StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, string> StringReferences(params (string Name, string Table, string? Id)[] references)
    {
        return references
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .ToDictionary(x => x.Name, x => PortableId(x.Table, x.Id!), StringComparer.Ordinal);
    }

    private static string PortableId(string table, object id)
    {
        return $"{table}:{id}";
    }

    private static IReadOnlyDictionary<string, int> CountRecords(BackupPayloadDto payload)
    {
        return EnumerateRecordSections(payload)
            .SelectMany(x => x.Records)
            .GroupBy(x => x.Table, StringComparer.Ordinal)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
    }

    private static IEnumerable<BackupRecordSectionDto> EnumerateRecordSections(BackupPayloadDto payload)
    {
        if (payload.Taxonomy is not null)
        {
            yield return payload.Taxonomy;
        }

        if (payload.Budget is not null)
        {
            yield return payload.Budget;
        }

        if (payload.Profiles is not null)
        {
            yield return payload.Profiles;
        }

        if (payload.Audit is not null)
        {
            yield return payload.Audit;
        }

        if (payload.Logs is not null)
        {
            yield return payload.Logs;
        }
    }
}
