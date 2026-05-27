using System.Text.Json;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Domain.Infrastructure;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HouseholdBudgetMate.Application.Auditing;

public sealed class AuditSaveChangesInterceptor(CurrentUserContext currentUserContext) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> IgnoredProperties =
    [
        nameof(ATimestampable.CreatedAtUtc),
        nameof(ATimestampable.UpdatedAtUtc),
        nameof(Expense.UserId),
        nameof(ExpenseLineItem.UserId),
        nameof(AuditLog.Id)
    ];

    private readonly Dictionary<Guid, List<PendingCreateAudit>> _pendingCreateAudits = [];
    private bool _isSavingAuditLogs;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        AddAuditLogsAndCollectCreates(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AddAuditLogsAndCollectCreates(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(
        SaveChangesCompletedEventData eventData,
        int result)
    {
        SavePendingCreateAuditLogs(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await SavePendingCreateAuditLogsAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAuditLogsAndCollectCreates(DbContext? dbContext)
    {
        if (_isSavingAuditLogs || dbContext is not ApplicationDbContext applicationDbContext)
        {
            return;
        }

        var auditLogs = new List<AuditLog>();
        var pendingCreates = new List<PendingCreateAudit>();

        foreach (var entry in applicationDbContext.ChangeTracker.Entries().Where(IsAuditableEntry))
        {
            if (entry.State == EntityState.Added)
            {
                var pendingCreate = CapturePendingCreateAudit(entry);
                if (pendingCreate is not null)
                {
                    pendingCreates.Add(pendingCreate);
                }

                continue;
            }

            var auditLog = BuildAuditLog(entry);
            if (auditLog is not null)
            {
                auditLogs.Add(auditLog);
            }
        }

        if (auditLogs.Count > 0)
        {
            applicationDbContext.AuditLogs.AddRange(auditLogs);
        }

        if (pendingCreates.Count > 0)
        {
            _pendingCreateAudits[applicationDbContext.ContextId.InstanceId] = pendingCreates;
        }
    }

    private void SavePendingCreateAuditLogs(DbContext? dbContext)
    {
        if (dbContext is not ApplicationDbContext applicationDbContext
            || !_pendingCreateAudits.Remove(applicationDbContext.ContextId.InstanceId, out var pendingCreates)
            || pendingCreates.Count == 0)
        {
            return;
        }

        var auditLogs = pendingCreates.Select(BuildAuditLog).ToList();
        _isSavingAuditLogs = true;
        try
        {
            applicationDbContext.AuditLogs.AddRange(auditLogs);
            applicationDbContext.SaveChanges();
        }
        finally
        {
            _isSavingAuditLogs = false;
        }
    }

    private async Task SavePendingCreateAuditLogsAsync(DbContext? dbContext, CancellationToken cancellationToken)
    {
        if (dbContext is not ApplicationDbContext applicationDbContext
            || !_pendingCreateAudits.Remove(applicationDbContext.ContextId.InstanceId, out var pendingCreates)
            || pendingCreates.Count == 0)
        {
            return;
        }

        var auditLogs = pendingCreates.Select(BuildAuditLog).ToList();
        _isSavingAuditLogs = true;
        try
        {
            applicationDbContext.AuditLogs.AddRange(auditLogs);
            await applicationDbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _isSavingAuditLogs = false;
        }
    }

    private static bool IsAuditableEntry(EntityEntry entry)
    {
        if (entry.Entity is not Expense
            and not Income
            and not Account
            and not AccountMonthBalance
            and not ExpenseLineItem
            and not MonthSavingsTransferItem
            and not Category
            and not LoanInstallment
            and not RegularExpenseDefinition
            and not RegularIncomeDefinition)
        {
            return false;
        }

        if (entry.Entity is LoanInstallment)
        {
            return entry.State == EntityState.Modified;
        }

        return entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted;
    }

    private PendingCreateAudit? CapturePendingCreateAudit(EntityEntry entry)
    {
        var newValues = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            if (ShouldSkipProperty(property))
            {
                continue;
            }

            newValues[property.Metadata.Name] = NormalizeValue(property.CurrentValue);
        }

        if (newValues.Count == 0)
        {
            return null;
        }

        var (actorUserId, budgetOwnerUserId) = ResolveUserScope();
        return new PendingCreateAudit(
            entry,
            entry.Metadata.ClrType.Name,
            actorUserId,
            budgetOwnerUserId,
            DateTime.UtcNow,
            newValues);
    }

    private AuditLog BuildAuditLog(PendingCreateAudit pendingCreate)
    {
        return CreateAuditLog(
            pendingCreate.EntityType,
            GetEntityId(pendingCreate.Entry),
            pendingCreate.ActorUserId,
            pendingCreate.BudgetOwnerUserId,
            "Create",
            [],
            pendingCreate.NewValues,
            pendingCreate.ChangedAtUtc);
    }

    private AuditLog? BuildAuditLog(EntityEntry entry)
    {
        var operation = ResolveOperation(entry);
        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            if (ShouldSkipProperty(property))
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Deleted:
                    oldValues[property.Metadata.Name] = NormalizeValue(property.OriginalValue);
                    break;
                case EntityState.Modified:
                    if (!property.IsModified || ValuesEqual(property.OriginalValue, property.CurrentValue))
                    {
                        continue;
                    }

                    oldValues[property.Metadata.Name] = NormalizeValue(property.OriginalValue);
                    newValues[property.Metadata.Name] = NormalizeValue(property.CurrentValue);
                    break;
                case EntityState.Added:
                case EntityState.Detached:
                case EntityState.Unchanged:
                default:
                    break;
            }
        }

        if (operation == "Update" && oldValues.Count == 0 && newValues.Count == 0)
        {
            return null;
        }

        var (actorUserId, budgetOwnerUserId) = ResolveUserScope();
        return CreateAuditLog(
            entry.Metadata.ClrType.Name,
            GetEntityId(entry),
            actorUserId,
            budgetOwnerUserId,
            operation,
            oldValues,
            newValues,
            DateTime.UtcNow);
    }

    private static AuditLog CreateAuditLog(
        string entityType,
        int entityId,
        string actorUserId,
        string budgetOwnerUserId,
        string operation,
        Dictionary<string, object?> oldValues,
        Dictionary<string, object?> newValues,
        DateTime changedAtUtc)
    {
        return new AuditLog
        {
            EntityType = entityType,
            EntityId = entityId,
            UserId = actorUserId,
            BudgetOwnerUserId = budgetOwnerUserId,
            Operation = operation,
            OldValuesJson = JsonSerializer.Serialize(oldValues, JsonOptions),
            NewValuesJson = JsonSerializer.Serialize(newValues, JsonOptions),
            ChangedAtUtc = changedAtUtc,
            CreatedAtUtc = changedAtUtc,
            UpdatedAtUtc = changedAtUtc
        };
    }

    private (string ActorUserId, string BudgetOwnerUserId) ResolveUserScope()
    {
        if (currentUserContext.IsSystemOperation
            && currentUserContext.UserId == User.DefaultUserId
            && currentUserContext.BudgetOwnerUserId == User.DefaultUserId)
        {
            return (User.DefaultUserId, User.DefaultUserId);
        }

        if (string.IsNullOrWhiteSpace(currentUserContext.UserId)
            || currentUserContext.UserId == User.DefaultUserId
            || string.IsNullOrWhiteSpace(currentUserContext.BudgetOwnerUserId))
        {
            throw new InvalidOperationException(
                "An authenticated user or explicit system scope is required for audited changes.");
        }

        return (currentUserContext.UserId, currentUserContext.BudgetOwnerUserId);
    }

    private static string ResolveOperation(EntityEntry entry)
    {
        if (entry.State == EntityState.Deleted)
        {
            return "Delete";
        }

        var isDeletedProperty = entry.Properties.FirstOrDefault(x => x.Metadata.Name == "IsDeleted");
        if (isDeletedProperty is not null
            && isDeletedProperty.IsModified
            && isDeletedProperty.OriginalValue is false
            && isDeletedProperty.CurrentValue is true)
        {
            return entry.Entity is Expense expense && expense.LoanInstallmentId.HasValue
                ? "Update"
                : "Delete";
        }

        return "Update";
    }

    private static bool ShouldSkipProperty(PropertyEntry property)
    {
        if (property.Metadata.IsPrimaryKey())
        {
            return true;
        }

        return IgnoredProperties.Contains(property.Metadata.Name);
    }

    private static int GetEntityId(EntityEntry entry)
    {
        var keyProperty = entry.Metadata.FindPrimaryKey()?.Properties.FirstOrDefault();
        if (keyProperty is null)
        {
            return 0;
        }

        var value = entry.Property(keyProperty.Name).CurrentValue ?? entry.Property(keyProperty.Name).OriginalValue;
        return value is int id ? id : 0;
    }

    private static object? NormalizeValue(object? value)
    {
        return value switch
        {
            DateOnly dateOnly => dateOnly.ToString("yyyy-MM-dd"),
            TimeOnly timeOnly => timeOnly.ToString("HH:mm:ss"),
            decimal decimalValue => decimalValue,
            _ => value
        };
    }

    private static bool ValuesEqual(object? oldValue, object? newValue)
    {
        return oldValue switch
        {
            null when newValue is null => true,
            null => false,
            _ => oldValue.Equals(newValue)
        };
    }

    private sealed record PendingCreateAudit(
        EntityEntry Entry,
        string EntityType,
        string ActorUserId,
        string BudgetOwnerUserId,
        DateTime ChangedAtUtc,
        Dictionary<string, object?> NewValues);
}
