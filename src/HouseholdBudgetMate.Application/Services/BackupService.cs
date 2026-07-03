using HouseholdBudgetMate.Abstractions.Contracts.Backup;
using HouseholdBudgetMate.Abstractions.Contracts.Backup.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Backup.Requests;
using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Application.Kernel.Timing;
using HouseholdBudgetMate.Application.Services.Backup;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Domain.Infrastructure;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Application.Services;

public sealed class BackupService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IDateTimeProvider dateTimeProvider,
    CurrentUserContext currentUserContext,
    IBackupSettingsStore backupSettingsStore) : IBackupService
{
    private const string CsvContentType = "text/csv; charset=utf-8";
    private const string JsonContentType = "application/json; charset=utf-8";

    private static readonly string[] CsvHeaders =
    [
        "Kind",
        "Year",
        "Month",
        "Name",
        "Category",
        "Tag",
        "PlannedAmount",
        "ActualAmount",
        "IsUnplanned",
        "LineItemDescription",
        "LineItemAmount",
        "LineItemOccurredAt",
        "LineItemTag",
        "IncomeAmount",
        "ExpectedDate",
        "Account",
        "IsRegular",
        "RegularSource",
        "IsDeleted"
    ];

    public async Task<CsvExportResultDto> ExportCsvAsync(
        ExportCsvRequest request,
        CancellationToken cancellationToken)
    {
        ValidateCsvRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var builder = new CsvBuilder();
        builder.AddRow(CsvHeaders.Cast<object?>().ToArray());

        var expenseRowCount = 0;
        var incomeRowCount = 0;

        if (request.Kind is BackupCsvExportKind.Expenses or BackupCsvExportKind.ExpensesAndIncomes)
        {
            expenseRowCount = await AppendExpenseRowsAsync(dbContext, builder, request, cancellationToken);
        }

        if (request.Kind is BackupCsvExportKind.Incomes or BackupCsvExportKind.ExpensesAndIncomes)
        {
            incomeRowCount = await AppendIncomeRowsAsync(dbContext, builder, request, cancellationToken);
        }

        return new CsvExportResultDto
        {
            FileName = BuildCsvFileName(request),
            ContentType = CsvContentType,
            Content = builder.ToUtf8BytesWithBom(),
            ExpenseRowCount = expenseRowCount,
            IncomeRowCount = incomeRowCount
        };
    }

    public async Task<BackupExportResultDto> CreateBackupAsync(
        CreateBackupRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Sections == BackupSection.None)
        {
            throw new BadRequestException("At least one backup section must be selected.");
        }

        ValidateBackupPeriodRange(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var snapshotBuilder = new BackupSnapshotBuilder(dateTimeProvider);
        var includeAllBudgetOwners = ShouldIncludeAllBudgetOwners(request);
        var envelope = await snapshotBuilder.BuildAsync(
            dbContext,
            request.Sections,
            includeAllBudgetOwners,
            BackupPeriodRange.FromRequest(request),
            cancellationToken);
        var content = BackupJsonSerializer.SerializeToUtf8Bytes(envelope);
        var fileName = BackupFileName.Build(envelope.Manifest.CreatedAtUtc, envelope.Manifest.IncludedSections);
        var writtenPath = await WriteBackupFileAsync(request.DestinationPath, fileName, content, cancellationToken);

        return new BackupExportResultDto
        {
            FileName = fileName,
            ContentType = JsonContentType,
            Content = content,
            WrittenPath = writtenPath,
            Warnings = envelope.Manifest.Warnings
        };
    }

    public Task<BackupValidationResultDto> ValidateBackupAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        var validator = new BackupValidator();
        return validator.ValidateAsync(content, cancellationToken);
    }

    public async Task<BackupRestorePreviewDto> PreviewRestoreAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken)
    {
        var validator = new BackupValidator();
        var contentBytes = await ReadAllBytesAsync(content, cancellationToken);
        var validation = validator.Validate(contentBytes);
        BackupEnvelopeDto? envelope = null;
        if (validation.IsValid)
        {
            envelope = validator.ParseEnvelope(contentBytes);
        }

        return new BackupRestorePreviewDto
        {
            FileName = fileName,
            CountsByTable = envelope?.Manifest.CountsByTable ?? new Dictionary<string, int>(),
            Warnings = validation.Warnings,
            IsAllowed = validation.IsValid && IsFullRestoreEnvelope(envelope),
            Errors = validation.IsValid && !IsFullRestoreEnvelope(envelope)
                ? ["Restore requires a full-app backup with budget, taxonomy, and profile sections."]
                : validation.Errors
        };
    }

    public async Task<BackupRestoreResultDto> RestoreBackupAsync(
        RestoreBackupRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRestoreRequest(request);

        var validator = new BackupValidator();
        var contentBytes = await ReadAllBytesAsync(request.Content, cancellationToken);
        var validation = validator.Validate(contentBytes);
        if (!validation.IsValid)
        {
            throw new BadRequestException(string.Join(" ", validation.Errors));
        }

        var envelope = validator.ParseEnvelope(contentBytes);
        if (!IsFullRestoreEnvelope(envelope))
        {
            throw new BadRequestException("Restore requires a full-app backup with budget, taxonomy, and profile sections.");
        }

        var preRestoreBackupPath = await CreatePreRestoreBackupAsync(cancellationToken);

        using var scope = currentUserContext.BeginTechnicalOwnerScope();
        await using var strategyContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (strategyContext.Database.IsRelational())
        {
            var strategy = strategyContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
                await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
                var result = await RestoreEnvelopeAsync(dbContext, envelope, validation.Warnings, preRestoreBackupPath, cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return result;
            });
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await RestoreEnvelopeAsync(dbContext, envelope, validation.Warnings, preRestoreBackupPath, cancellationToken);
    }

    public Task<BackupSettingsDto> GetBackupSettingsAsync(CancellationToken cancellationToken)
    {
        return backupSettingsStore.GetAsync(cancellationToken);
    }

    public Task<BackupSettingsDto> SaveBackupSettingsAsync(
        SaveBackupSettingsRequest request,
        CancellationToken cancellationToken)
    {
        return backupSettingsStore.SaveAsync(request, cancellationToken);
    }

    public async Task<BackupExportResultDto> RunScheduledBackupNowAsync(CancellationToken cancellationToken)
    {
        var settings = await backupSettingsStore.GetAsync(cancellationToken);
        try
        {
            var result = await CreateBackupAsync(
                new CreateBackupRequest
                {
                    Sections = settings.Sections,
                    DestinationPath = settings.BackupPath,
                    IncludeAllBudgetOwners = true
                },
                cancellationToken);

            await backupSettingsStore.RecordRunAsync(
                dateTimeProvider.GetUtcDateTime(),
                $"Succeeded: {result.FileName}",
                cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            await backupSettingsStore.RecordRunAsync(
                dateTimeProvider.GetUtcDateTime(),
                $"Failed: {ex.Message}",
                cancellationToken);
            throw;
        }
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream content, CancellationToken cancellationToken)
    {
        await using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }

    private async Task<string> CreatePreRestoreBackupAsync(CancellationToken cancellationToken)
    {
        var preRestoreFolder = Path.Combine(Path.GetTempPath(), "HouseholdBudgetMate", "pre-restore");
        var backup = await CreateBackupAsync(
            new CreateBackupRequest
            {
                Sections = BackupSection.FullApp,
                DestinationPath = preRestoreFolder,
                IncludeAllBudgetOwners = true
            },
            cancellationToken);

        return backup.WrittenPath ?? Path.Combine(preRestoreFolder, backup.FileName);
    }

    private static async Task<BackupRestoreResultDto> RestoreEnvelopeAsync(
        ApplicationDbContext dbContext,
        BackupEnvelopeDto envelope,
        IReadOnlyList<string> validationWarnings,
        string preRestoreBackupPath,
        CancellationToken cancellationToken)
    {
        var executor = new BackupRestoreExecutor();
        var result = await executor.RestoreAsync(dbContext, envelope, cancellationToken);

        return new BackupRestoreResultDto
        {
            IsSuccess = result.IsSuccess,
            Message = result.Message,
            PreRestoreBackupPath = preRestoreBackupPath,
            RestoredCounts = result.RestoredCounts,
            Warnings = validationWarnings
        };
    }

    private static void ValidateRestoreRequest(RestoreBackupRequest request)
    {
        if (!string.Equals(request.ConfirmationPhrase.Trim(), "RESTORE BACKUP", StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException("Typed confirmation phrase does not match.");
        }
    }

    private static void ValidateBackupPeriodRange(CreateBackupRequest request)
    {
        var hasAnyRangeValue = request.FromYear.HasValue
                               || request.FromMonth.HasValue
                               || request.ToYear.HasValue
                               || request.ToMonth.HasValue;
        if (!hasAnyRangeValue)
        {
            return;
        }

        if (!request.FromYear.HasValue
            || !request.FromMonth.HasValue
            || !request.ToYear.HasValue
            || !request.ToMonth.HasValue)
        {
            throw new BadRequestException("Backup period range requires from year/month and to year/month.");
        }

        if (request.FromMonth is < 1 or > 12 || request.ToMonth is < 1 or > 12)
        {
            throw new BadRequestException("Backup period month must be between 1 and 12.");
        }

        if (request.FromYear is < 2000 or > 3000 || request.ToYear is < 2000 or > 3000)
        {
            throw new BadRequestException("Backup period year is out of allowed range.");
        }

        if ((request.FromYear.Value * 12 + request.FromMonth.Value)
            > (request.ToYear.Value * 12 + request.ToMonth.Value))
        {
            throw new BadRequestException("Backup period start must be before or equal to period end.");
        }
    }

    private static async Task<int> AppendExpenseRowsAsync(
        ApplicationDbContext dbContext,
        CsvBuilder builder,
        ExportCsvRequest request,
        CancellationToken cancellationToken)
    {
        var query = request.IncludeDeleted
            ? dbContext.Expenses
                .IgnoreQueryFilters()
                .Where(x => x.UserId == dbContext.CurrentBudgetOwnerUserId)
            : dbContext.Expenses.AsQueryable();

        query = query
            .AsNoTracking()
            .Where(x => x.MonthPlan.Year == request.Year);

        if (request.Month.HasValue)
        {
            query = query.Where(x => x.MonthPlan.Month == request.Month.Value);
        }

        if (request.CategoryId.HasValue)
        {
            query = query.Where(x => x.CategoryId == request.CategoryId.Value);
        }

        var expenses = await query
            .Include(x => x.MonthPlan)
            .Include(x => x.Category)
            .Include(x => x.Tag)
            .Include(x => x.RegularExpenseDefinition)
            .Include(x => x.LineItems)
            .ThenInclude(x => x.Tag)
            .OrderBy(x => x.MonthPlan.Year)
            .ThenBy(x => x.MonthPlan.Month)
            .ThenBy(x => x.Order)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var rowCount = 0;
        foreach (var expense in expenses)
        {
            var lineItems = expense.LineItems
                .OrderBy(x => x.OccurredAt)
                .ThenBy(x => x.Id)
                .ToList();

            if (lineItems.Count == 0)
            {
                AddExpenseRow(builder, expense);
                rowCount++;
                continue;
            }

            foreach (var lineItem in lineItems)
            {
                AddExpenseRow(builder, expense, lineItem.Description, lineItem.Amount, lineItem.OccurredAt, lineItem.Tag?.Name);
                rowCount++;
            }
        }

        return rowCount;
    }

    private static async Task<int> AppendIncomeRowsAsync(
        ApplicationDbContext dbContext,
        CsvBuilder builder,
        ExportCsvRequest request,
        CancellationToken cancellationToken)
    {
        var query = request.IncludeDeleted
            ? dbContext.Incomes
                .IgnoreQueryFilters()
                .Where(x => x.UserId == dbContext.CurrentBudgetOwnerUserId)
            : dbContext.Incomes.AsQueryable();

        query = query
            .AsNoTracking()
            .Where(x => x.MonthPlan.Year == request.Year);

        if (request.Month.HasValue)
        {
            query = query.Where(x => x.MonthPlan.Month == request.Month.Value);
        }

        if (request.AccountId.HasValue)
        {
            query = query.Where(x => x.AccountId == request.AccountId.Value);
        }

        var incomes = await query
            .Include(x => x.MonthPlan)
            .Include(x => x.Account)
            .Include(x => x.RegularIncomeDefinition)
            .OrderBy(x => x.MonthPlan.Year)
            .ThenBy(x => x.MonthPlan.Month)
            .ThenBy(x => x.ExpectedDayOfMonth)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var income in incomes)
        {
            builder.AddRow(
                "Income",
                income.MonthPlan.Year,
                income.MonthPlan.Month,
                income.Name,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                income.Amount,
                income.ExpectedDayOfMonth,
                income.Account.Name,
                income.IsRegular,
                income.RegularIncomeDefinition?.Name,
                income.IsDeleted);
        }

        return incomes.Count;
    }

    private static void AddExpenseRow(
        CsvBuilder builder,
        Expense expense,
        string? lineItemDescription = null,
        decimal? lineItemAmount = null,
        DateOnly? lineItemOccurredAt = null,
        string? lineItemTag = null)
    {
        builder.AddRow(
            "Expense",
            expense.MonthPlan.Year,
            expense.MonthPlan.Month,
            expense.Name,
            expense.Category.Name,
            expense.Tag?.Name,
            expense.PlannedAmount,
            expense.ActualAmount,
            expense.PlannedAmount <= 0m && expense.ActualAmount > 0m,
            lineItemDescription,
            lineItemAmount,
            lineItemOccurredAt,
            lineItemTag,
            null,
            null,
            null,
            expense.RegularExpenseDefinition is not null,
            expense.RegularExpenseDefinition?.Name,
            expense.IsDeleted);
    }

    private static void ValidateCsvRequest(ExportCsvRequest request)
    {
        if (request.Year is < 2000 or > 3000)
        {
            throw new BadRequestException("Year is out of allowed range.");
        }

        if (request.Month is < 1 or > 12)
        {
            throw new BadRequestException("Month is out of allowed range.");
        }

        if (!Enum.IsDefined(request.Kind))
        {
            throw new BadRequestException("CSV export kind is invalid.");
        }
    }

    private static string BuildCsvFileName(ExportCsvRequest request)
    {
        var scope = request.Kind switch
        {
            BackupCsvExportKind.Expenses => "expenses",
            BackupCsvExportKind.Incomes => "incomes",
            _ => "expenses-incomes"
        };
        var period = request.Month.HasValue
            ? $"{request.Year}-{request.Month.Value:00}"
            : request.Year.ToString();

        return $"household-budget-mate-{scope}-{period}.csv";
    }

    private static bool ShouldIncludeAllBudgetOwners(CreateBackupRequest request)
    {
        return request.IncludeAllBudgetOwners
               || request.Sections == BackupSection.FullApp
               || request.Sections.HasFlag(BackupSection.Profiles);
    }

    private static bool IsFullRestoreEnvelope(BackupEnvelopeDto? envelope)
    {
        if (envelope is null)
        {
            return false;
        }

        var sections = envelope.Manifest.IncludedSections;
        return sections.HasFlag(BackupSection.Budget)
               && sections.HasFlag(BackupSection.Taxonomy)
               && sections.HasFlag(BackupSection.Profiles);
    }

    private static async Task<string?> WriteBackupFileAsync(
        string? destinationPath,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            return null;
        }

        var targetPath = Path.HasExtension(destinationPath)
            ? destinationPath
            : Path.Combine(destinationPath, fileName);

        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(targetPath, content, cancellationToken);
        return targetPath;
    }
}
