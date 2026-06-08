using System.Globalization;
using System.Reflection;
using HouseholdBudgetMate.Abstractions.Contracts.Backup.Dto;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Application.Services.Backup;

internal sealed class BackupRestoreExecutor
{
    public async Task<BackupRestoreResultDto> RestoreAsync(
        ApplicationDbContext dbContext,
        BackupEnvelopeDto envelope,
        CancellationToken cancellationToken)
    {
        var state = new RestoreState();
        var restoredCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var unhandledRecords = new List<string>();

        await ClearExistingDataAsync(dbContext, cancellationToken);

        await RestoreTaxonomyAsync(dbContext, envelope, state, restoredCounts, cancellationToken);
        await RestoreProfilesAsync(dbContext, envelope, state, restoredCounts, cancellationToken);
        await RestoreBudgetAsync(dbContext, envelope, state, restoredCounts, cancellationToken);
        await RestoreAuditAndLogsAsync(dbContext, envelope, state, restoredCounts, cancellationToken);

        unhandledRecords.AddRange(GetUnhandledRecords(envelope, state.HandledPortableIds));

        if (unhandledRecords.Count > 0)
        {
            throw new BadRequestException($"Unsupported backup record(s): {string.Join(", ", unhandledRecords)}");
        }

        return new BackupRestoreResultDto
        {
            IsSuccess = true,
            Message = "Backup restored successfully.",
            RestoredCounts = restoredCounts
        };
    }

    private static async Task ClearExistingDataAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        await DeleteAndSaveAsync(dbContext, dbContext.ExpenseLineItems, cancellationToken);
        await DeleteAndSaveAsync(dbContext, dbContext.Expenses, cancellationToken);
        await DeleteAndSaveAsync(dbContext, dbContext.MonthSavingsTransferItems, cancellationToken);
        await DeleteAndSaveAsync(dbContext, dbContext.LoanCharges, cancellationToken);
        await DeleteAndSaveAsync(dbContext, dbContext.LoanRateEntries, cancellationToken);
        await DeleteAndSaveAsync(dbContext, dbContext.LoanInstallments, cancellationToken);
        await DeleteAndSaveAsync(dbContext, dbContext.RegularExpenseDefinitions, cancellationToken);
        await DeleteAndSaveAsync(dbContext, dbContext.RegularIncomeDefinitions, cancellationToken);
        await DeleteAndSaveAsync(dbContext, dbContext.AccountMonthBalances, cancellationToken);
        await DeleteAndSaveAsync(dbContext, dbContext.Incomes, cancellationToken);
        await DeleteAndSaveAsync(dbContext, dbContext.AnnualPlans, cancellationToken);
        await DeleteAndSaveAsync(dbContext, dbContext.Loans, cancellationToken);
        await DeleteAndSaveAsync(dbContext, dbContext.MonthPlans, cancellationToken);
        await DeleteAndSaveAsync(dbContext, dbContext.Accounts, cancellationToken);
        await DeleteAndSaveAsync(dbContext, dbContext.Tags, cancellationToken);
        await DeleteAndSaveAsync(dbContext, dbContext.Categories, cancellationToken);
        await DeleteAndSaveAsync(dbContext, dbContext.AuditLogs, cancellationToken);
        await DeleteAndSaveAsync(dbContext, dbContext.Logs, cancellationToken);
    }

    private static async Task DeleteAndSaveAsync<TEntity>(
        ApplicationDbContext dbContext,
        DbSet<TEntity> set,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        if (dbContext.Database.IsRelational())
        {
            await set.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return;
        }

        var entities = await set.IgnoreQueryFilters().ToListAsync(cancellationToken);
        if (entities.Count > 0)
        {
            set.RemoveRange(entities);
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
        }
    }

    private static async Task RestoreTaxonomyAsync(
        ApplicationDbContext dbContext,
        BackupEnvelopeDto envelope,
        RestoreState state,
        IDictionary<string, int> restoredCounts,
        CancellationToken cancellationToken)
    {
        var categories = envelope.Payload.Taxonomy?.Records.Where(x => x.Table == "categories").ToList() ?? [];
        foreach (var record in categories)
        {
            var category = new Category();
            ApplyFields(category, record);
            dbContext.Categories.Add(category);
            await dbContext.SaveChangesAsync(cancellationToken);
            state.Map(record.PortableId, category.Id);
            state.MarkHandled(record.PortableId);
        }

        var tags = envelope.Payload.Taxonomy?.Records.Where(x => x.Table == "tags").ToList() ?? [];
        foreach (var record in tags)
        {
            var tag = new Tag();
            ApplyFields(tag, record, state, [("CategoryId", "category")], skipId: true);
            tag.ParentTagId = null;
            dbContext.Tags.Add(tag);
            await dbContext.SaveChangesAsync(cancellationToken);
            state.Map(record.PortableId, tag.Id);
            state.MarkHandled(record.PortableId);
        }

        foreach (var record in tags)
        {
            if (!record.References.TryGetValue("parentTag", out var parentPortableId))
            {
                continue;
            }

            var tagId = state.ResolveInt(record.PortableId);
            var parentId = state.ResolveInt(parentPortableId);
            var tag = await dbContext.Tags.FindAsync([tagId], cancellationToken);
            if (tag is null)
            {
                throw new BadRequestException($"Missing restored tag {record.PortableId}.");
            }

            tag.ParentTagId = parentId;
        }

        if (tags.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        restoredCounts["categories"] = categories.Count;
        restoredCounts["tags"] = tags.Count;
    }

    private static async Task RestoreProfilesAsync(
        ApplicationDbContext dbContext,
        BackupEnvelopeDto envelope,
        RestoreState state,
        IDictionary<string, int> restoredCounts,
        CancellationToken cancellationToken)
    {
        var users = envelope.Payload.Profiles?.Records.Where(x => x.Table == "users").ToList() ?? [];
        var existingUsers = await dbContext.Users
            .ToDictionaryAsync(x => x.Id, StringComparer.Ordinal, cancellationToken);

        foreach (var record in users.OrderBy(x => x.Fields.TryGetValue("Id", out var id) ? id : string.Empty, StringComparer.Ordinal))
        {
            var user = existingUsers.TryGetValue(record.Fields[nameof(User.Id)]!, out var existingUser)
                ? existingUser
                : new User();

            ApplyFields(user, record, skipId: false);
            if (!existingUsers.ContainsKey(user.Id))
            {
                dbContext.Users.Add(user);
                existingUsers[user.Id] = user;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            state.Map(record.PortableId, user.Id);
            state.MarkHandled(record.PortableId);
        }

        restoredCounts["users"] = users.Count;
    }

    private static async Task RestoreBudgetAsync(
        ApplicationDbContext dbContext,
        BackupEnvelopeDto envelope,
        RestoreState state,
        IDictionary<string, int> restoredCounts,
        CancellationToken cancellationToken)
    {
        var accounts = envelope.Payload.Budget?.Records.Where(x => x.Table == "accounts").ToList() ?? [];
        foreach (var record in accounts)
        {
            var account = new Account();
            ApplyFields(account, record, state, skipId: true);
            dbContext.Accounts.Add(account);
            await dbContext.SaveChangesAsync(cancellationToken);
            state.Map(record.PortableId, account.Id);
            state.MarkHandled(record.PortableId);
        }
        restoredCounts["accounts"] = accounts.Count;

        var monthPlans = envelope.Payload.Budget?.Records.Where(x => x.Table == "monthPlans").ToList() ?? [];
        foreach (var record in monthPlans)
        {
            var monthPlan = new MonthPlan();
            ApplyFields(monthPlan, record, state, skipId: true);
            dbContext.MonthPlans.Add(monthPlan);
            await dbContext.SaveChangesAsync(cancellationToken);
            state.Map(record.PortableId, monthPlan.Id);
            state.MarkHandled(record.PortableId);
        }
        restoredCounts["monthPlans"] = monthPlans.Count;

        var annualPlans = envelope.Payload.Budget?.Records.Where(x => x.Table == "annualPlans").ToList() ?? [];
        foreach (var record in annualPlans)
        {
            var annualPlan = new AnnualPlan();
            ApplyFields(annualPlan, record, state, skipId: true);
            dbContext.AnnualPlans.Add(annualPlan);
            await dbContext.SaveChangesAsync(cancellationToken);
            state.Map(record.PortableId, annualPlan.Id);
            state.MarkHandled(record.PortableId);
        }
        restoredCounts["annualPlans"] = annualPlans.Count;

        var regularExpenses = envelope.Payload.Budget?.Records.Where(x => x.Table == "regularExpenseDefinitions").ToList() ?? [];
        foreach (var record in regularExpenses)
        {
            var definition = new RegularExpenseDefinition();
            ApplyFields(definition, record, state, [("CategoryId", "category"), ("TagId", "tag")], skipId: true);
            dbContext.RegularExpenseDefinitions.Add(definition);
            await dbContext.SaveChangesAsync(cancellationToken);
            state.Map(record.PortableId, definition.Id);
            state.MarkHandled(record.PortableId);
        }
        restoredCounts["regularExpenseDefinitions"] = regularExpenses.Count;

        var regularIncomes = envelope.Payload.Budget?.Records.Where(x => x.Table == "regularIncomeDefinitions").ToList() ?? [];
        foreach (var record in regularIncomes)
        {
            var definition = new RegularIncomeDefinition();
            ApplyFields(definition, record, state, [("AccountId", "account")], skipId: true);
            dbContext.RegularIncomeDefinitions.Add(definition);
            await dbContext.SaveChangesAsync(cancellationToken);
            state.Map(record.PortableId, definition.Id);
            state.MarkHandled(record.PortableId);
        }
        restoredCounts["regularIncomeDefinitions"] = regularIncomes.Count;

        var loans = envelope.Payload.Budget?.Records.Where(x => x.Table == "loans").ToList() ?? [];
        foreach (var record in loans)
        {
            var loan = new Loan();
            ApplyFields(loan, record, state, [("TagId", "tag")], skipId: true);
            dbContext.Loans.Add(loan);
            await dbContext.SaveChangesAsync(cancellationToken);
            state.Map(record.PortableId, loan.Id);
            state.MarkHandled(record.PortableId);
        }
        restoredCounts["loans"] = loans.Count;

        var balances = envelope.Payload.Budget?.Records.Where(x => x.Table == "accountMonthBalances").ToList() ?? [];
        foreach (var record in balances)
        {
            var balance = new AccountMonthBalance();
            ApplyFields(balance, record, state, [("AccountId", "account")], skipId: true);
            dbContext.AccountMonthBalances.Add(balance);
            await dbContext.SaveChangesAsync(cancellationToken);
            state.Map(record.PortableId, balance.Id);
            state.MarkHandled(record.PortableId);
        }
        restoredCounts["accountMonthBalances"] = balances.Count;

        var incomes = envelope.Payload.Budget?.Records.Where(x => x.Table == "incomes").ToList() ?? [];
        foreach (var record in incomes)
        {
            var income = new Income();
            ApplyFields(income, record, state, [("AccountId", "account"), ("RegularIncomeDefinitionId", "regularIncomeDefinition")], skipId: true);
            dbContext.Incomes.Add(income);
            await dbContext.SaveChangesAsync(cancellationToken);
            state.Map(record.PortableId, income.Id);
            state.MarkHandled(record.PortableId);
        }
        restoredCounts["incomes"] = incomes.Count;

        var installments = envelope.Payload.Budget?.Records.Where(x => x.Table == "loanInstallments").ToList() ?? [];
        foreach (var record in installments)
        {
            var installment = new LoanInstallment();
            ApplyFields(installment, record, state, [("LoanId", "loan")], skipId: true);
            dbContext.LoanInstallments.Add(installment);
            await dbContext.SaveChangesAsync(cancellationToken);
            state.Map(record.PortableId, installment.Id);
            state.MarkHandled(record.PortableId);
        }
        restoredCounts["loanInstallments"] = installments.Count;

        var rateEntries = envelope.Payload.Budget?.Records.Where(x => x.Table == "loanRateEntries").ToList() ?? [];
        foreach (var record in rateEntries)
        {
            var entry = new LoanRateEntry();
            ApplyFields(entry, record, state, [("LoanId", "loan")], skipId: true);
            dbContext.LoanRateEntries.Add(entry);
            await dbContext.SaveChangesAsync(cancellationToken);
            state.Map(record.PortableId, entry.Id);
            state.MarkHandled(record.PortableId);
        }
        restoredCounts["loanRateEntries"] = rateEntries.Count;

        var charges = envelope.Payload.Budget?.Records.Where(x => x.Table == "loanCharges").ToList() ?? [];
        foreach (var record in charges)
        {
            var charge = new LoanCharge();
            ApplyFields(charge, record, state, [("LoanId", "loan")], skipId: true);
            dbContext.LoanCharges.Add(charge);
            await dbContext.SaveChangesAsync(cancellationToken);
            state.Map(record.PortableId, charge.Id);
            state.MarkHandled(record.PortableId);
        }
        restoredCounts["loanCharges"] = charges.Count;

        var expenses = envelope.Payload.Budget?.Records.Where(x => x.Table == "expenses").ToList() ?? [];
        foreach (var record in expenses)
        {
            var expense = new Expense();
            ApplyFields(expense, record, state, [
                ("MonthPlanId", "monthPlan"),
                ("CategoryId", "category"),
                ("TagId", "tag"),
                ("RegularExpenseDefinitionId", "regularExpenseDefinition"),
                ("LoanInstallmentId", "loanInstallment")
            ], skipId: true);
            dbContext.Expenses.Add(expense);
            await dbContext.SaveChangesAsync(cancellationToken);
            state.Map(record.PortableId, expense.Id);
            state.MarkHandled(record.PortableId);
        }
        restoredCounts["expenses"] = expenses.Count;

        var lineItems = envelope.Payload.Budget?.Records.Where(x => x.Table == "expenseLineItems").ToList() ?? [];
        foreach (var record in lineItems)
        {
            var lineItem = new ExpenseLineItem();
            ApplyFields(lineItem, record, state, [("ExpenseId", "expense"), ("TagId", "tag")], skipId: true);
            dbContext.ExpenseLineItems.Add(lineItem);
            await dbContext.SaveChangesAsync(cancellationToken);
            state.Map(record.PortableId, lineItem.Id);
            state.MarkHandled(record.PortableId);
        }
        restoredCounts["expenseLineItems"] = lineItems.Count;

        var transfers = envelope.Payload.Budget?.Records.Where(x => x.Table == "monthSavingsTransferItems").ToList() ?? [];
        foreach (var record in transfers)
        {
            var transfer = new MonthSavingsTransferItem();
            ApplyFields(transfer, record, state, [("MonthPlanId", "monthPlan")], skipId: true);
            dbContext.MonthSavingsTransferItems.Add(transfer);
            await dbContext.SaveChangesAsync(cancellationToken);
            state.Map(record.PortableId, transfer.Id);
            state.MarkHandled(record.PortableId);
        }
        restoredCounts["monthSavingsTransferItems"] = transfers.Count;
    }

    private static async Task RestoreAuditAndLogsAsync(
        ApplicationDbContext dbContext,
        BackupEnvelopeDto envelope,
        RestoreState state,
        IDictionary<string, int> restoredCounts,
        CancellationToken cancellationToken)
    {
        var audits = envelope.Payload.Audit?.Records.Where(x => x.Table == "auditLogs").ToList() ?? [];
        foreach (var record in audits)
        {
            var audit = new AuditLog();
            ApplyFields(audit, record, skipId: true);
            dbContext.AuditLogs.Add(audit);
            await dbContext.SaveChangesAsync(cancellationToken);
            state.Map(record.PortableId, audit.Id);
            state.MarkHandled(record.PortableId);
        }
        restoredCounts["auditLogs"] = audits.Count;

        var logs = envelope.Payload.Logs?.Records.Where(x => x.Table == "logs").ToList() ?? [];
        foreach (var record in logs)
        {
            var log = new LogEntry();
            ApplyFields(log, record, state, skipId: true);
            dbContext.Logs.Add(log);
            await dbContext.SaveChangesAsync(cancellationToken);
            state.Map(record.PortableId, log.Id);
            state.MarkHandled(record.PortableId);
        }
        restoredCounts["logs"] = logs.Count;
    }

    private static IReadOnlyList<string> GetUnhandledRecords(BackupEnvelopeDto envelope, ISet<string> handledPortableIds)
    {
        return EnumerateRecords(envelope)
            .Where(x => !handledPortableIds.Contains(x.PortableId))
            .Select(x => x.PortableId)
            .ToList();
    }

    private static IEnumerable<BackupRecordDto> EnumerateRecords(BackupEnvelopeDto envelope)
    {
        foreach (var section in new[]
                 {
                     envelope.Payload.Taxonomy,
                     envelope.Payload.Profiles,
                     envelope.Payload.Budget,
                     envelope.Payload.Audit,
                     envelope.Payload.Logs
                 })
        {
            if (section is null)
            {
                continue;
            }

            foreach (var record in section.Records)
            {
                yield return record;
            }
        }
    }

    private static void ApplyFields(
        object entity,
        BackupRecordDto record,
        RestoreState? state = null,
        IReadOnlyCollection<(string PropertyName, string ReferenceKey)>? referenceMap = null,
        bool skipId = false)
    {
        var referenceLookup = referenceMap?.ToDictionary(x => x.PropertyName, x => x.ReferenceKey, StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var property in entity.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.SetMethod is null)
            {
                continue;
            }

            if (skipId && property.Name == nameof(Account.Id) && property.PropertyType == typeof(int))
            {
                continue;
            }

            if (referenceLookup.TryGetValue(property.Name, out var referenceKey))
            {
                if (!record.References.TryGetValue(referenceKey, out var portableId))
                {
                    continue;
                }

                if (state is null)
                {
                    throw new InvalidOperationException("Restore state is required for reference mapping.");
                }

                SetProperty(property, entity, state.ResolvePortableValue(portableId, property.PropertyType));
                continue;
            }

            if (!record.Fields.TryGetValue(property.Name, out var value))
            {
                continue;
            }

            SetProperty(property, entity, ConvertValue(value, property.PropertyType));
        }
    }

    private static object? ConvertValue(string? value, Type targetType)
    {
        if (value is null)
        {
            return null;
        }

        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (underlying == typeof(string))
        {
            return value;
        }

        if (underlying == typeof(int))
        {
            return int.Parse(value, CultureInfo.InvariantCulture);
        }

        if (underlying == typeof(bool))
        {
            return bool.Parse(value);
        }

        if (underlying == typeof(decimal))
        {
            return decimal.Parse(value, CultureInfo.InvariantCulture);
        }

        if (underlying == typeof(DateOnly))
        {
            return DateOnly.Parse(value, CultureInfo.InvariantCulture);
        }

        if (underlying == typeof(DateTime))
        {
            return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        if (underlying == typeof(DateTimeOffset))
        {
            return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        if (underlying == typeof(Guid))
        {
            return Guid.Parse(value);
        }

        if (underlying.IsEnum)
        {
            return Enum.Parse(underlying, value, ignoreCase: true);
        }

        return value;
    }

    private static void SetProperty(PropertyInfo property, object entity, object? value)
    {
        property.SetValue(entity, value);
    }

    private sealed class RestoreState
    {
        private readonly Dictionary<string, string> _portableToPersisted = new(StringComparer.Ordinal);
        private readonly HashSet<string> _handledPortableIds = new(StringComparer.Ordinal);

        public ISet<string> HandledPortableIds => _handledPortableIds;

        public void Map(string portableId, object persistedValue)
        {
            _portableToPersisted[portableId] = Convert.ToString(persistedValue, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        public void MarkHandled(string portableId) => _handledPortableIds.Add(portableId);

        public object ResolvePortableValue(string portableId, Type propertyType)
        {
            if (!_portableToPersisted.TryGetValue(portableId, out var persistedValue))
            {
                throw new BadRequestException($"Missing restored value for portable ID {portableId}.");
            }

            return ConvertValue(persistedValue, propertyType) ?? throw new BadRequestException($"Missing restored value for portable ID {portableId}.");
        }

        public int ResolveInt(string portableId)
        {
            return int.Parse(_portableToPersisted[portableId], CultureInfo.InvariantCulture);
        }
    }
}
