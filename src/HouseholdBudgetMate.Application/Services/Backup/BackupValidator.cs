using System.Globalization;
using System.Reflection;
using HouseholdBudgetMate.Abstractions.Contracts.Backup;
using HouseholdBudgetMate.Abstractions.Contracts.Backup.Dto;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Domain.Entities;

namespace HouseholdBudgetMate.Application.Services.Backup;

internal sealed class BackupValidator
{
    private static readonly Dictionary<string, Type> TableTypes = new(StringComparer.Ordinal)
    {
        ["accounts"] = typeof(Account),
        ["accountMonthBalances"] = typeof(AccountMonthBalance),
        ["annualPlans"] = typeof(AnnualPlan),
        ["auditLogs"] = typeof(AuditLog),
        ["categories"] = typeof(Category),
        ["expenseLineItems"] = typeof(ExpenseLineItem),
        ["expenses"] = typeof(Expense),
        ["incomes"] = typeof(Income),
        ["loanCharges"] = typeof(LoanCharge),
        ["loanInstallments"] = typeof(LoanInstallment),
        ["loanRateEntries"] = typeof(LoanRateEntry),
        ["loans"] = typeof(Loan),
        ["logs"] = typeof(LogEntry),
        ["monthPlans"] = typeof(MonthPlan),
        ["monthSavingsTransferItems"] = typeof(MonthSavingsTransferItem),
        ["regularExpenseDefinitions"] = typeof(RegularExpenseDefinition),
        ["regularIncomeDefinitions"] = typeof(RegularIncomeDefinition),
        ["tags"] = typeof(Tag),
        ["users"] = typeof(User)
    };

    public async Task<BackupValidationResultDto> ValidateAsync(Stream content, CancellationToken cancellationToken)
    {
        var bytes = await ReadAllBytesAsync(content, cancellationToken);
        return Validate(bytes);
    }

    public BackupEnvelopeDto ParseEnvelope(ReadOnlySpan<byte> content)
    {
        return BackupJsonSerializer.Deserialize(content);
    }

    public BackupValidationResultDto Validate(ReadOnlySpan<byte> content)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        BackupEnvelopeDto envelope;
        try
        {
            envelope = BackupJsonSerializer.Deserialize(content);
        }
        catch (Exception ex)
        {
            return new BackupValidationResultDto
            {
                IsValid = false,
                Errors = [ex.Message]
            };
        }

        warnings.AddRange(envelope.Manifest.Warnings);

        if (envelope.SchemaVersion != BackupEnvelopeDto.CurrentSchemaVersion)
        {
            errors.Add("Backup schema version is not supported.");
        }

        if (envelope.Manifest.IncludedSections == BackupSection.None)
        {
            errors.Add("Backup does not contain any selected sections.");
        }

        var records = EnumerateRecords(envelope).ToList();
        var portableIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            if (!portableIds.Add(record.PortableId))
            {
                errors.Add($"Duplicate portable ID found: {record.PortableId}.");
            }

            if (!TableTypes.TryGetValue(record.Table, out var entityType))
            {
                continue;
            }

            ValidateTypedFields(record, entityType, errors);
        }

        ValidateReferences(records, portableIds, errors);
        ValidateAdminProfiles(envelope, errors);

        return new BackupValidationResultDto
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }

    private static void ValidateTypedFields(BackupRecordDto record, Type entityType, ICollection<string> errors)
    {
        foreach (var field in record.Fields)
        {
            var property = entityType.GetProperty(field.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property is null)
            {
                continue;
            }

            var value = field.Value;
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            try
            {
                if (targetType == typeof(int))
                {
                    _ = int.Parse(value, CultureInfo.InvariantCulture);
                    if (field.Key is "Month" && int.Parse(value, CultureInfo.InvariantCulture) is < 1 or > 12)
                    {
                        errors.Add($"Field {record.Table}.{field.Key} is out of range.");
                    }
                }
                else if (targetType == typeof(decimal))
                {
                    _ = decimal.Parse(value, CultureInfo.InvariantCulture);
                }
                else if (targetType == typeof(bool))
                {
                    _ = bool.Parse(value);
                }
                else if (targetType == typeof(DateOnly))
                {
                    _ = DateOnly.Parse(value, CultureInfo.InvariantCulture);
                }
                else if (targetType == typeof(DateTime))
                {
                    _ = DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                }
                else if (targetType == typeof(DateTimeOffset))
                {
                    _ = DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                }
                else if (targetType == typeof(Guid))
                {
                    _ = Guid.Parse(value);
                }
            }
            catch (Exception)
            {
                errors.Add($"Field {record.Table}.{field.Key} has an invalid value.");
            }
        }
    }

    private static void ValidateReferences(
        IReadOnlyCollection<BackupRecordDto> records,
        ISet<string> portableIds,
        ICollection<string> errors)
    {
        foreach (var record in records)
        {
            foreach (var reference in record.References)
            {
                if (!portableIds.Contains(reference.Value))
                {
                    errors.Add($"Missing reference for {record.PortableId}:{reference.Key} -> {reference.Value}.");
                }
            }
        }
    }

    private static void ValidateAdminProfiles(BackupEnvelopeDto envelope, ICollection<string> errors)
    {
        if (!envelope.Manifest.IncludedSections.HasFlag(BackupSection.Profiles))
        {
            return;
        }

        var users = envelope.Payload.Profiles?.Records ?? [];
        var hasAdmin = users.Any(x =>
            x.Fields.TryGetValue(nameof(User.IsAdmin), out var isAdminText)
            && bool.TryParse(isAdminText, out var isAdmin)
            && isAdmin);

        if (!hasAdmin)
        {
            errors.Add("Backup must contain at least one admin profile.");
        }
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

    private static async Task<byte[]> ReadAllBytesAsync(Stream content, CancellationToken cancellationToken)
    {
        if (content is MemoryStream memoryStream && memoryStream.TryGetBuffer(out var buffer))
        {
            return buffer.ToArray();
        }

        await using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }
}
