using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HouseholdBudgetMate.Abstractions.Contracts.Backup;
using HouseholdBudgetMate.Abstractions.Contracts.Backup.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Backup.Requests;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Application.Services;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Domain.Infrastructure;
using HouseholdBudgetMate.Migrations;
using HouseholdBudgetMate.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class BackupServiceTests
{
    private const string VisibleUserId = "visible-admin";
    private const string OtherUserId = "other-admin";

    [Fact]
    public async Task ExportCsvAsync_Should_Return_Stable_Headers_And_Escaped_Expense_And_Income_Rows()
    {
        var dbName = Guid.NewGuid().ToString();
        var currentUser = CreateVisibleUserContext();
        var options = NewOptions(dbName);
        var factory = new ScopedInMemoryDbContextFactory(options, currentUser);

        await SeedVisibleBudgetAsync(options, currentUser);
        var service = CreateService(factory, currentUser);

        var result = await service.ExportCsvAsync(
            new ExportCsvRequest { Year = 2026, Month = 4 },
            CancellationToken.None);

        var csv = DecodeCsv(result.Content);

        Assert.Equal("household-budget-mate-expenses-incomes-2026-04.csv", result.FileName);
        Assert.Equal("text/csv; charset=utf-8", result.ContentType);
        Assert.Equal(1, result.ExpenseRowCount);
        Assert.Equal(1, result.IncomeRowCount);
        Assert.Contains("Kind,Year,Month,Name,Category,Tag,PlannedAmount,ActualAmount,IsUnplanned,LineItemDescription,LineItemAmount,LineItemOccurredAt,LineItemTag,IncomeAmount,ExpectedDate,Account,IsRegular,RegularSource,IsDeleted", csv);
        Assert.Contains("Expense,2026,4,\"Rent, utilities\",Home,Apartment,1200.00,450.00,false,\"April \"\"water\"\" bill\",45.50,2026-04-11,Apartment,,,,false,,false", csv);
        Assert.Contains("Income,2026,4,\"Salary, bonus\"", csv);
        Assert.Contains("5000.00,2026-04-05,Main,true,Salary,false", csv);
    }

    [Fact]
    public async Task ExportCsvAsync_Should_Filter_By_Kind_Period_Category_And_Account()
    {
        var dbName = Guid.NewGuid().ToString();
        var currentUser = CreateVisibleUserContext();
        var options = NewOptions(dbName);
        var factory = new ScopedInMemoryDbContextFactory(options, currentUser);
        var seed = await SeedVisibleBudgetAsync(options, currentUser);
        await SeedOutOfFilterDataAsync(options, currentUser, seed);

        var service = CreateService(factory, currentUser);

        var expenseResult = await service.ExportCsvAsync(
            new ExportCsvRequest
            {
                Year = 2026,
                Month = 4,
                Kind = BackupCsvExportKind.Expenses,
                CategoryId = seed.CategoryId
            },
            CancellationToken.None);

        var incomeResult = await service.ExportCsvAsync(
            new ExportCsvRequest
            {
                Year = 2026,
                Kind = BackupCsvExportKind.Incomes,
                AccountId = seed.AccountId
            },
            CancellationToken.None);

        var expenseCsv = DecodeCsv(expenseResult.Content);
        var incomeCsv = DecodeCsv(incomeResult.Content);

        Assert.Equal(1, expenseResult.ExpenseRowCount);
        Assert.Equal(0, expenseResult.IncomeRowCount);
        Assert.Contains("Rent, utilities", expenseCsv);
        Assert.DoesNotContain("Fuel", expenseCsv);
        Assert.DoesNotContain("May rent", expenseCsv);

        Assert.Equal(0, incomeResult.ExpenseRowCount);
        Assert.Equal(1, incomeResult.IncomeRowCount);
        Assert.Contains("Salary, bonus", incomeCsv);
        Assert.DoesNotContain("Side gig", incomeCsv);
    }

    [Fact]
    public async Task ExportCsvAsync_Should_Include_Deleted_Rows_Only_When_Requested()
    {
        var dbName = Guid.NewGuid().ToString();
        var currentUser = CreateVisibleUserContext();
        var options = NewOptions(dbName);
        var factory = new ScopedInMemoryDbContextFactory(options, currentUser);
        await SeedVisibleBudgetAsync(options, currentUser);
        await SeedDeletedRowsAsync(options, currentUser);

        var service = CreateService(factory, currentUser);

        var defaultResult = await service.ExportCsvAsync(
            new ExportCsvRequest { Year = 2026, Month = 4 },
            CancellationToken.None);

        var includeDeletedResult = await service.ExportCsvAsync(
            new ExportCsvRequest { Year = 2026, Month = 4, IncludeDeleted = true },
            CancellationToken.None);

        var defaultCsv = DecodeCsv(defaultResult.Content);
        var includeDeletedCsv = DecodeCsv(includeDeletedResult.Content);

        Assert.DoesNotContain("Deleted expense", defaultCsv);
        Assert.DoesNotContain("Deleted income", defaultCsv);
        Assert.Contains("Deleted expense", includeDeletedCsv);
        Assert.Contains("Deleted income", includeDeletedCsv);
        Assert.Equal(2, includeDeletedResult.ExpenseRowCount);
        Assert.Equal(2, includeDeletedResult.IncomeRowCount);
    }

    [Fact]
    public async Task ExportCsvAsync_Should_Not_Leak_Data_From_Another_BudgetScope()
    {
        var dbName = Guid.NewGuid().ToString();
        var visibleUser = CreateVisibleUserContext();
        var otherUser = new CurrentUserContext();
        otherUser.SetInteractiveUser(OtherUserId, OtherUserId);
        var options = NewOptions(dbName);
        var factory = new ScopedInMemoryDbContextFactory(options, visibleUser);

        await SeedVisibleBudgetAsync(options, visibleUser);
        await SeedOtherBudgetAsync(options, otherUser);

        var service = CreateService(factory, visibleUser);
        var result = await service.ExportCsvAsync(
            new ExportCsvRequest { Year = 2026, Month = 4, IncludeDeleted = true },
            CancellationToken.None);

        var csv = DecodeCsv(result.Content);

        Assert.Contains("Rent, utilities", csv);
        Assert.Contains("Salary, bonus", csv);
        Assert.DoesNotContain("Other budget expense", csv);
        Assert.DoesNotContain("Other budget income", csv);
    }

    [Fact]
    public async Task CreateBackupAsync_Should_Export_Stable_Json_With_Selected_Sections_Warnings_And_Portable_Ids()
    {
        var dbName = Guid.NewGuid().ToString();
        var currentUser = CreateVisibleUserContext();
        var options = NewOptions(dbName);
        var factory = new ScopedInMemoryDbContextFactory(options, currentUser);

        await SeedProfileAsync(options, currentUser);
        await SeedVisibleBudgetAsync(options, currentUser);

        var service = CreateService(factory, currentUser);
        var result = await service.CreateBackupAsync(
            new CreateBackupRequest { Sections = BackupSection.Budget },
            CancellationToken.None);

        var envelope = DecodeJson(result.Content);
        var allPortableIds = EnumerateRecords(envelope)
            .Select(x => x.PortableId)
            .ToHashSet(StringComparer.Ordinal);
        var missingReferences = EnumerateRecords(envelope)
            .SelectMany(x => x.References.Values)
            .Where(x => !allPortableIds.Contains(x))
            .ToArray();

        Assert.Equal("household-budget-mate-backup-20260607-160500-budget-taxonomy.json", result.FileName);
        Assert.Equal("application/json; charset=utf-8", result.ContentType);
        Assert.Null(result.WrittenPath);
        Assert.Equal(BackupEnvelopeDto.CurrentSchemaVersion, envelope.SchemaVersion);
        Assert.Equal("HouseholdBudgetMate", envelope.ApplicationName);
        Assert.Equal(new DateTimeOffset(2026, 6, 7, 16, 5, 0, TimeSpan.Zero), envelope.Manifest.CreatedAtUtc);
        Assert.Equal(BackupSection.Budget, envelope.Manifest.RequestedSections);
        Assert.True(envelope.Manifest.IncludedSections.HasFlag(BackupSection.Budget));
        Assert.True(envelope.Manifest.IncludedSections.HasFlag(BackupSection.Taxonomy));
        Assert.Empty(envelope.Manifest.Warnings);
        Assert.Contains(envelope.Payload.Budget!.Records, x => x.Table == "expenses" && x.PortableId.StartsWith("expenses:", StringComparison.Ordinal));
        Assert.Contains(envelope.Payload.Taxonomy!.Records, x => x.Table == "categories" && x.PortableId.StartsWith("categories:", StringComparison.Ordinal));
        Assert.Empty(missingReferences);
    }

    [Fact]
    public async Task CreateBackupAsync_Should_Exclude_Profile_Log_And_Audit_Sections_For_BudgetOnly_Backup()
    {
        var dbName = Guid.NewGuid().ToString();
        var currentUser = CreateVisibleUserContext();
        var options = NewOptions(dbName);
        var factory = new ScopedInMemoryDbContextFactory(options, currentUser);

        await SeedProfileAsync(options, currentUser);
        await SeedVisibleBudgetAsync(options, currentUser);

        var service = CreateService(factory, currentUser);
        var result = await service.CreateBackupAsync(
            new CreateBackupRequest { Sections = BackupSection.Budget },
            CancellationToken.None);

        var envelope = DecodeJson(result.Content);

        Assert.NotNull(envelope.Payload.Budget);
        Assert.NotNull(envelope.Payload.Taxonomy);
        Assert.Null(envelope.Payload.Profiles);
        Assert.Null(envelope.Payload.Logs);
        Assert.Null(envelope.Payload.Audit);
        Assert.DoesNotContain(EnumerateRecords(envelope), x => x.Table == "users");
    }

    [Fact]
    public async Task CreateBackupAsync_Should_Include_Profile_Pin_Hashes_Only_When_Profile_Section_Is_Selected()
    {
        var dbName = Guid.NewGuid().ToString();
        var currentUser = CreateVisibleUserContext();
        var options = NewOptions(dbName);
        var factory = new ScopedInMemoryDbContextFactory(options, currentUser);

        await SeedProfileAsync(options, currentUser);
        await SeedVisibleBudgetAsync(options, currentUser);

        var service = CreateService(factory, currentUser);
        var budgetOnlyResult = await service.CreateBackupAsync(
            new CreateBackupRequest { Sections = BackupSection.Budget },
            CancellationToken.None);
        var fullResult = await service.CreateBackupAsync(
            new CreateBackupRequest { Sections = BackupSection.FullApp },
            CancellationToken.None);

        var budgetOnlyEnvelope = DecodeJson(budgetOnlyResult.Content);
        var fullEnvelope = DecodeJson(fullResult.Content);
        var profileRecords = fullEnvelope.Payload.Profiles!.Records;

        Assert.Null(budgetOnlyEnvelope.Payload.Profiles);
        Assert.DoesNotContain("pin-hash-value", Encoding.UTF8.GetString(budgetOnlyResult.Content));
        Assert.Contains(fullResult.Warnings, x => x.Contains("authentication secrets", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(profileRecords, x =>
            x.Table == "users"
            && x.Fields.TryGetValue(nameof(User.PasswordHash), out var passwordHash)
            && passwordHash == "pin-hash-value");
    }

    [Fact]
    public async Task CreateBackupAsync_Should_Filter_Budget_Records_By_Optional_Period_Range()
    {
        var dbName = Guid.NewGuid().ToString();
        var currentUser = CreateVisibleUserContext();
        var options = NewOptions(dbName);
        var factory = new ScopedInMemoryDbContextFactory(options, currentUser);

        await SeedProfileAsync(options, currentUser);
        await SeedFullAppAsync(options, currentUser);
        await SeedHistoricalVisibleBudgetAsync(options, currentUser);

        var service = CreateService(factory, currentUser);
        var result = await service.CreateBackupAsync(
            new CreateBackupRequest
            {
                Sections = BackupSection.Budget,
                FromYear = 2024,
                FromMonth = 7,
                ToYear = 2024,
                ToMonth = 7
            },
            CancellationToken.None);

        var envelope = DecodeJson(result.Content);

        Assert.Equal(2024, envelope.Manifest.BudgetFromYear);
        Assert.Equal(7, envelope.Manifest.BudgetFromMonth);
        Assert.Equal(2024, envelope.Manifest.BudgetToYear);
        Assert.Equal(7, envelope.Manifest.BudgetToMonth);
        Assert.Contains(envelope.Payload.Budget!.Records, x => x.Table == "expenses" && x.Fields[nameof(Expense.Name)] == "Historical rent");
        Assert.Contains(envelope.Payload.Budget!.Records, x => x.Table == "incomes" && x.Fields[nameof(Income.Name)] == "Historical salary");
        Assert.DoesNotContain(envelope.Payload.Budget!.Records, x => x.Table == "expenses" && x.Fields[nameof(Expense.Name)] == "Rent, utilities");
        Assert.DoesNotContain(envelope.Payload.Budget!.Records, x => x.Table == "incomes" && x.Fields[nameof(Income.Name)] == "Salary, bonus");
    }

    [Fact]
    public async Task RestoreBackupAsync_Should_Round_Trip_Full_App_State_And_Create_PreRestore_Backup()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var currentUser = CreateVisibleUserContext();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        var factory = new ScopedDbContextFactory(options, currentUser);
        await using (var setup = new ApplicationDbContext(options, currentUser))
        {
            await setup.Database.EnsureCreatedAsync();
        }

        await SeedProfileAsync(options, currentUser);
        await SeedFullAppAsync(options, currentUser);
        await SeedHistoricalVisibleBudgetAsync(options, currentUser);

        var service = CreateService(factory, currentUser);
        var backup = await service.CreateBackupAsync(
            new CreateBackupRequest { Sections = BackupSection.FullApp },
            CancellationToken.None);

        await MutateVisibleStateAsync(options, currentUser);

        var restoreResult = await service.RestoreBackupAsync(
            new RestoreBackupRequest
            {
                Content = new MemoryStream(backup.Content),
                FileName = backup.FileName,
                ConfirmationPhrase = "RESTORE BACKUP"
            },
            CancellationToken.None);

        await using var verify = new ApplicationDbContext(options, currentUser);
        var restoredExpense = await verify.Expenses.AsNoTracking().SingleAsync(x => x.Name == "Rent, utilities");
        var restoredIncome = await verify.Incomes.AsNoTracking().SingleAsync(x => x.Name == "Salary, bonus");
        var restoredLoan = await verify.Loans.AsNoTracking().SingleAsync(x => x.Name == "Mortgage");
        var historicalExpense = await verify.Expenses.AsNoTracking().SingleAsync(x => x.Name == "Historical rent");
        var historicalExpensePlan = await verify.MonthPlans
            .AsNoTracking()
            .SingleAsync(x => x.Id == historicalExpense.MonthPlanId);
        var historicalIncome = await verify.Incomes.AsNoTracking().SingleAsync(x => x.Name == "Historical salary");

        Assert.True(restoreResult.IsSuccess);
        Assert.NotNull(restoreResult.PreRestoreBackupPath);
        Assert.True(File.Exists(restoreResult.PreRestoreBackupPath!));
        Assert.Equal("Rent, utilities", restoredExpense.Name);
        Assert.Equal(1200m, restoredExpense.PlannedAmount);
        Assert.Equal(5000m, restoredIncome.Amount);
        Assert.Equal(2024, historicalExpensePlan.Year);
        Assert.Equal(7, historicalExpensePlan.Month);
        Assert.Equal(2024, historicalIncome.Year);
        Assert.Equal(7, historicalIncome.Month);
        Assert.True(restoredLoan.IsActive);
        Assert.Contains(restoreResult.RestoredCounts, x => x.Key == "expenses" && x.Value > 0);
        Assert.Contains(restoreResult.RestoredCounts, x => x.Key == "users" && x.Value > 0);
    }

    [Fact]
    public void RestoreBackupAsync_Should_Run_User_Transaction_Inside_Ef_Execution_Strategy()
    {
        var source = ReadRepoFile("src/HouseholdBudgetMate.Application/Services/BackupService.cs");
        var strategyIndex = source.IndexOf("return await strategy.ExecuteAsync(async () =>", StringComparison.Ordinal);
        var transactionIndex = source.IndexOf("BeginTransactionAsync(cancellationToken)", StringComparison.Ordinal);

        Assert.Contains("var strategy = strategyContext.Database.CreateExecutionStrategy();", source);
        Assert.True(strategyIndex >= 0);
        Assert.True(transactionIndex > strategyIndex);
    }

    [Fact]
    public async Task RestoreBackupAsync_Should_Round_Trip_All_BudgetOwners_For_Full_App_Backup()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var visibleUser = CreateVisibleUserContext();
        var otherUser = new CurrentUserContext();
        otherUser.SetInteractiveUser(OtherUserId, OtherUserId);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        var factory = new ScopedDbContextFactory(options, visibleUser);
        await using (var setup = new ApplicationDbContext(options, visibleUser))
        {
            await setup.Database.EnsureCreatedAsync();
        }

        await SeedProfileAsync(options, visibleUser);
        await SeedOtherProfileAsync(options, visibleUser);
        await SeedVisibleBudgetAsync(options, visibleUser);
        await SeedOtherBudgetAsync(options, otherUser);

        var service = CreateService(factory, visibleUser);
        var backup = await service.CreateBackupAsync(
            new CreateBackupRequest { Sections = BackupSection.FullApp },
            CancellationToken.None);

        await MutateVisibleStateAsync(options, visibleUser);
        await MutateOtherStateAsync(options, otherUser);

        await service.RestoreBackupAsync(
            new RestoreBackupRequest
            {
                Content = new MemoryStream(backup.Content),
                FileName = backup.FileName,
                ConfirmationPhrase = "RESTORE BACKUP"
            },
            CancellationToken.None);

        await using var visibleVerify = new ApplicationDbContext(options, visibleUser);
        await using var otherVerify = new ApplicationDbContext(options, otherUser);

        Assert.Equal("Rent, utilities", await visibleVerify.Expenses.AsNoTracking().Select(x => x.Name).SingleAsync());
        Assert.Equal("Other budget expense", await otherVerify.Expenses.AsNoTracking().Select(x => x.Name).SingleAsync());
        Assert.Equal(OtherUserId, await otherVerify.Expenses.IgnoreQueryFilters().Where(x => x.Name == "Other budget expense").Select(x => x.UserId).SingleAsync());
    }

    [Fact]
    public async Task RestoreBackupAsync_Should_Reject_BudgetOnly_Backup_Before_Deleting_Data()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var currentUser = CreateVisibleUserContext();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        var factory = new ScopedDbContextFactory(options, currentUser);
        await using (var setup = new ApplicationDbContext(options, currentUser))
        {
            await setup.Database.EnsureCreatedAsync();
        }

        await SeedProfileAsync(options, currentUser);
        await SeedVisibleBudgetAsync(options, currentUser);

        var service = CreateService(factory, currentUser);
        var backup = await service.CreateBackupAsync(
            new CreateBackupRequest { Sections = BackupSection.Budget },
            CancellationToken.None);

        await MutateVisibleStateAsync(options, currentUser);

        await Assert.ThrowsAsync<BadRequestException>(() => service.RestoreBackupAsync(
            new RestoreBackupRequest
            {
                Content = new MemoryStream(backup.Content),
                FileName = backup.FileName,
                ConfirmationPhrase = "RESTORE BACKUP"
            },
            CancellationToken.None));

        Assert.Equal("Changed expense", await GetExpenseNameAsync(options, currentUser));
    }

    [Fact]
    public async Task PreviewRestoreAsync_Should_Return_Counts_And_Block_NonFull_Backup()
    {
        var dbName = Guid.NewGuid().ToString();
        var currentUser = CreateVisibleUserContext();
        var options = NewOptions(dbName);
        var factory = new ScopedInMemoryDbContextFactory(options, currentUser);
        await SeedProfileAsync(options, currentUser);
        await SeedVisibleBudgetAsync(options, currentUser);

        var service = CreateService(factory, currentUser);
        var fullBackup = await service.CreateBackupAsync(
            new CreateBackupRequest { Sections = BackupSection.FullApp },
            CancellationToken.None);
        var budgetOnlyBackup = await service.CreateBackupAsync(
            new CreateBackupRequest { Sections = BackupSection.Budget },
            CancellationToken.None);

        var fullPreview = await service.PreviewRestoreAsync(
            new MemoryStream(fullBackup.Content),
            fullBackup.FileName,
            CancellationToken.None);
        var budgetOnlyPreview = await service.PreviewRestoreAsync(
            new MemoryStream(budgetOnlyBackup.Content),
            budgetOnlyBackup.FileName,
            CancellationToken.None);

        Assert.True(fullPreview.IsAllowed);
        Assert.Contains(fullPreview.CountsByTable, x => x.Key == "expenses" && x.Value > 0);
        Assert.False(budgetOnlyPreview.IsAllowed);
        Assert.Contains(budgetOnlyPreview.Errors, x => x.Contains("full-app backup", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("unsupported-schema")]
    [InlineData("duplicate-portable-id")]
    [InlineData("missing-reference")]
    [InlineData("no-admin-profile")]
    [InlineData("malformed-json")]
    public async Task ValidateBackupAsync_Should_Reject_Invalid_Backups(string scenario)
    {
        var dbName = Guid.NewGuid().ToString();
        var currentUser = CreateVisibleUserContext();
        var options = NewOptions(dbName);
        var factory = new ScopedInMemoryDbContextFactory(options, currentUser);
        await SeedProfileAsync(options, currentUser);
        await SeedFullAppAsync(options, currentUser);

        var service = CreateService(factory, currentUser);
        var content = scenario == "malformed-json"
            ? new MemoryStream(Encoding.UTF8.GetBytes("{"))
            : await BuildScenarioStreamAsync(service, scenario, CancellationToken.None);

        var result = await service.ValidateBackupAsync(content, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task RestoreBackupAsync_Should_Roll_Back_When_Executor_Encounters_An_Unsupported_Record()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var currentUser = CreateVisibleUserContext();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        var factory = new ScopedDbContextFactory(options, currentUser);
        await using (var setup = new ApplicationDbContext(options, currentUser))
        {
            await setup.Database.EnsureCreatedAsync();
        }

        await SeedProfileAsync(options, currentUser);
        await SeedFullAppAsync(options, currentUser);

        var service = CreateService(factory, currentUser);
        var backup = await service.CreateBackupAsync(
            new CreateBackupRequest { Sections = BackupSection.FullApp },
            CancellationToken.None);
        var envelope = DecodeJson(backup.Content);
        var budgetRecords = envelope.Payload.Budget!.Records.ToList();
        budgetRecords.Add(new BackupRecordDto
        {
            Table = "unsupported-table",
            PortableId = "unsupported-table:1",
            Fields = new Dictionary<string, string?> { ["Name"] = "bad" }
        });
        envelope.Payload.Budget.Records = budgetRecords;
        var tampered = JsonSerializer.SerializeToUtf8Bytes(envelope, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        });

        await MutateVisibleStateAsync(options, currentUser);
        var changedName = await GetExpenseNameAsync(options, currentUser);

        await Assert.ThrowsAsync<BadRequestException>(() => service.RestoreBackupAsync(
            new RestoreBackupRequest
            {
                Content = new MemoryStream(tampered),
                FileName = backup.FileName,
                ConfirmationPhrase = "RESTORE BACKUP"
            },
            CancellationToken.None));

        var afterFailureName = await GetExpenseNameAsync(options, currentUser);
        Assert.Equal(changedName, afterFailureName);
    }

    [Fact]
    public async Task RunScheduledBackupNowAsync_Should_Record_Failed_Status_When_Backup_Write_Fails()
    {
        var dbName = Guid.NewGuid().ToString();
        var currentUser = CreateVisibleUserContext();
        var options = NewOptions(dbName);
        var factory = new ScopedInMemoryDbContextFactory(options, currentUser);
        await SeedProfileAsync(options, currentUser);
        await SeedVisibleBudgetAsync(options, currentUser);

        var tempDirectory = Path.Combine(Path.GetTempPath(), "HouseholdBudgetMateTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var blockedDirectoryPath = Path.Combine(tempDirectory, "blocked");
        await File.WriteAllTextAsync(blockedDirectoryPath, "not a directory");
        var settingsStore = new InMemoryBackupSettingsStore(new BackupSettingsDto
        {
            IsEnabled = true,
            BackupPath = blockedDirectoryPath,
            Frequency = BackupScheduleFrequency.Daily,
            LocalTime = new TimeOnly(2, 0),
            Sections = BackupSection.FullApp
        });
        var service = CreateService(factory, currentUser, settingsStore);

        await Assert.ThrowsAnyAsync<IOException>(() => service.RunScheduledBackupNowAsync(CancellationToken.None));

        Assert.NotNull(settingsStore.Current.LastRunAtUtc);
        Assert.StartsWith("Failed:", settingsStore.Current.LastStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunScheduledBackupNowAsync_Should_Back_Up_Budget_Data_Without_Interactive_User()
    {
        var dbName = Guid.NewGuid().ToString();
        var seedUser = CreateVisibleUserContext();
        var backgroundContext = new CurrentUserContext();
        var options = NewOptions(dbName);
        var factory = new ScopedInMemoryDbContextFactory(options, backgroundContext);
        await SeedProfileAsync(options, seedUser);
        await SeedVisibleBudgetAsync(options, seedUser);

        var settingsStore = new InMemoryBackupSettingsStore(new BackupSettingsDto
        {
            IsEnabled = true,
            BackupPath = Path.Combine(Path.GetTempPath(), "HouseholdBudgetMateTests", "backups"),
            Frequency = BackupScheduleFrequency.Daily,
            LocalTime = new TimeOnly(2, 0),
            Sections = BackupSection.FullApp
        });
        var service = CreateService(factory, backgroundContext, settingsStore);

        var result = await service.RunScheduledBackupNowAsync(CancellationToken.None);
        var envelope = DecodeJson(result.Content);

        Assert.Contains(EnumerateRecords(envelope), x => x.Table == "expenses" && x.Fields[nameof(Expense.Name)] == "Rent, utilities");
        Assert.NotNull(settingsStore.Current.LastRunAtUtc);
        Assert.StartsWith("Succeeded:", settingsStore.Current.LastStatus, StringComparison.Ordinal);
    }

    private static async Task<SeedIds> SeedVisibleBudgetAsync(
        DbContextOptions<ApplicationDbContext> options,
        CurrentUserContext currentUser)
    {
        await using var context = new ApplicationDbContext(options, currentUser);

        var category = new Category { Name = "Home", Color = "#336699" };
        var tag = new Tag { Name = "Apartment", Category = category };
        var account = new Account { Name = "Main", Type = (int)AccountType.Bank };
        var regularIncome = new RegularIncomeDefinition
        {
            Name = "Salary",
            Amount = 5000m,
            DayOfMonth = 5,
            Account = account,
            IsActive = true
        };
        var monthPlan = new MonthPlan { Year = 2026, Month = 4 };
        var expense = new Expense
        {
            MonthPlan = monthPlan,
            Name = "Rent, utilities",
            Category = category,
            Tag = tag,
            PlannedAmount = 1200m,
            ActualAmount = 450m,
            Order = 1
        };
        expense.LineItems.Add(new ExpenseLineItem
        {
            Description = "April \"water\" bill",
            Amount = 45.50m,
            OccurredAt = new DateOnly(2026, 4, 11),
            Tag = tag
        });

        context.Categories.Add(category);
        context.Tags.Add(tag);
        context.Accounts.Add(account);
        context.RegularIncomeDefinitions.Add(regularIncome);
        context.MonthPlans.Add(monthPlan);
        context.Expenses.Add(expense);
        context.Incomes.Add(new Income
        {
            Year = 2026,
            Month = 4,
            Name = "Salary, bonus",
            Amount = 5000m,
            ExpectedDayOfMonth = new DateOnly(2026, 4, 5),
            Account = account,
            IsRegular = true,
            RegularIncomeDefinition = regularIncome
        });

        await context.SaveChangesAsync();
        return new SeedIds(category.Id, account.Id);
    }

    private static async Task SeedProfileAsync(
        DbContextOptions<ApplicationDbContext> options,
        CurrentUserContext currentUser)
    {
        await using var context = new ApplicationDbContext(options, currentUser);

        context.Users.AddRange(
            new User
            {
                Id = User.DefaultUserId,
                Username = User.TechnicalOwnerUsername,
                PasswordHash = "technical-owner-hash",
                BudgetOwnerUserId = User.DefaultUserId,
                IsAdmin = true
            },
            new User
            {
                Id = VisibleUserId,
                Username = "visible-admin",
                PasswordHash = "pin-hash-value",
                BudgetOwnerUserId = User.DefaultUserId,
                IsAdmin = true
            });

        await context.SaveChangesAsync();
    }

    private static async Task SeedOtherProfileAsync(
        DbContextOptions<ApplicationDbContext> options,
        CurrentUserContext currentUser)
    {
        await using var context = new ApplicationDbContext(options, currentUser);

        context.Users.Add(new User
        {
            Id = OtherUserId,
            Username = "other-admin",
            PasswordHash = "other-pin-hash-value",
            BudgetOwnerUserId = OtherUserId,
            IsAdmin = true
        });

        await context.SaveChangesAsync();
    }

    private static async Task SeedFullAppAsync(
        DbContextOptions<ApplicationDbContext> options,
        CurrentUserContext currentUser)
    {
        await using var context = new ApplicationDbContext(options, currentUser);

        var category = new Category { Name = "Home", Color = "#336699" };
        var tag = new Tag { Name = "Apartment", Category = category };
        var account = new Account { Name = "Main", Type = (int)AccountType.Bank };
        var regularIncome = new RegularIncomeDefinition
        {
            Name = "Salary",
            Amount = 5000m,
            DayOfMonth = 5,
            Account = account,
            IsActive = true
        };
        var regularExpense = new RegularExpenseDefinition
        {
            Name = "Rent",
            Amount = 1200m,
            Category = category,
            Tag = tag,
            IsActive = true
        };
        var monthPlan = new MonthPlan { Year = 2026, Month = 4 };
        var loan = new Loan
        {
            Name = "Mortgage",
            LoanType = 1,
            InterestMode = 1,
            Principal = 100000m,
            InterestRate = 5.5m,
            RepaymentDayOfMonth = 15,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2030, 12, 31),
            Tag = tag
        };
        var installment = new LoanInstallment
        {
            Loan = loan,
            Year = 2026,
            Month = 4,
            DueDate = new DateOnly(2026, 4, 15),
            Amount = 800m,
            PrincipalAmount = 600m,
            InterestAmount = 200m
        };
        var rateEntry = new LoanRateEntry
        {
            Loan = loan,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            ReferenceRate = 4.5m
        };
        var charge = new LoanCharge
        {
            Loan = loan,
            Name = "Admin fee",
            ChargeType = 1,
            FrequencyType = 1,
            Amount = 25m,
            StartDate = new DateOnly(2026, 1, 1)
        };
        var expense = new Expense
        {
            MonthPlan = monthPlan,
            Name = "Rent, utilities",
            Category = category,
            Tag = tag,
            PlannedAmount = 1200m,
            ActualAmount = 450m,
            Order = 1,
            RegularExpenseDefinition = regularExpense,
            LoanInstallment = installment
        };
        expense.LineItems.Add(new ExpenseLineItem
        {
            Description = "April \"water\" bill",
            Amount = 45.50m,
            OccurredAt = new DateOnly(2026, 4, 11),
            Tag = tag
        });

        context.Categories.Add(category);
        context.Tags.Add(tag);
        context.Accounts.Add(account);
        context.RegularIncomeDefinitions.Add(regularIncome);
        context.RegularExpenseDefinitions.Add(regularExpense);
        context.MonthPlans.Add(monthPlan);
        context.Loans.Add(loan);
        context.LoanInstallments.Add(installment);
        context.LoanRateEntries.Add(rateEntry);
        context.LoanCharges.Add(charge);
        context.AccountMonthBalances.Add(new AccountMonthBalance
        {
            Account = account,
            Year = 2026,
            Month = 4,
            ClosingBalance = 1500m
        });
        context.AnnualPlans.Add(new AnnualPlan
        {
            Year = 2026,
            ExpectedIncomeAmount = 60000m,
            ExpectedSavingsAmount = 12000m
        });
        context.MonthSavingsTransferItems.Add(new MonthSavingsTransferItem
        {
            MonthPlan = monthPlan,
            Amount = 100m,
            TransferDate = new DateOnly(2026, 4, 10)
        });
        context.Expenses.Add(expense);
        context.Incomes.Add(new Income
        {
            Year = 2026,
            Month = 4,
            Name = "Salary, bonus",
            Amount = 5000m,
            ExpectedDayOfMonth = new DateOnly(2026, 4, 5),
            Account = account,
            IsRegular = true,
            RegularIncomeDefinition = regularIncome
        });
        context.AuditLogs.Add(new AuditLog
        {
            EntityType = nameof(Expense),
            EntityId = 1,
            UserId = VisibleUserId,
            BudgetOwnerUserId = User.DefaultUserId,
            Operation = "create",
            OldValuesJson = "{}",
            NewValuesJson = "{}",
            ChangedAtUtc = DateTime.UtcNow
        });
        context.Logs.Add(new LogEntry
        {
            Message = "Seed log",
            MessageTemplate = "Seed log",
            Level = "Information",
            Timestamp = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    private static async Task SeedHistoricalVisibleBudgetAsync(
        DbContextOptions<ApplicationDbContext> options,
        CurrentUserContext currentUser)
    {
        await using var context = new ApplicationDbContext(options, currentUser);
        var account = await context.Accounts.FirstAsync();
        var category = await context.Categories.FirstAsync();
        var historicalPlan = new MonthPlan { Year = 2024, Month = 7 };
        context.MonthPlans.Add(historicalPlan);
        context.Expenses.Add(new Expense
        {
            MonthPlan = historicalPlan,
            Name = "Historical rent",
            Category = category,
            PlannedAmount = 900m,
            ActualAmount = 900m,
            Order = 1
        });
        context.Incomes.Add(new Income
        {
            Year = 2024,
            Month = 7,
            Name = "Historical salary",
            Amount = 4500m,
            ExpectedDayOfMonth = new DateOnly(2024, 7, 5),
            Account = account
        });

        await context.SaveChangesAsync();
    }

    private static async Task MutateVisibleStateAsync(
        DbContextOptions<ApplicationDbContext> options,
        CurrentUserContext currentUser)
    {
        await using var context = new ApplicationDbContext(options, currentUser);
        var expense = await context.Expenses.FirstAsync();
        expense.Name = "Changed expense";
        await context.SaveChangesAsync();
    }

    private static async Task MutateOtherStateAsync(
        DbContextOptions<ApplicationDbContext> options,
        CurrentUserContext otherUser)
    {
        await using var context = new ApplicationDbContext(options, otherUser);
        var expense = await context.Expenses.FirstAsync();
        expense.Name = "Changed other expense";
        await context.SaveChangesAsync();
    }

    private static async Task<string> GetExpenseNameAsync(
        DbContextOptions<ApplicationDbContext> options,
        CurrentUserContext currentUser)
    {
        await using var context = new ApplicationDbContext(options, currentUser);
        return await context.Expenses.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Name).FirstAsync();
    }

    private static async Task<MemoryStream> BuildScenarioStreamAsync(
        BackupService service,
        string scenario,
        CancellationToken cancellationToken)
    {
        var backup = await service.CreateBackupAsync(
            new CreateBackupRequest { Sections = BackupSection.FullApp },
            cancellationToken);

        var envelope = DecodeJson(backup.Content);

        switch (scenario)
        {
            case "unsupported-schema":
                envelope.SchemaVersion = BackupEnvelopeDto.CurrentSchemaVersion + 1;
                break;
            case "duplicate-portable-id":
                if (envelope.Payload.Budget is not null && envelope.Payload.Budget.Records.Count > 0)
                {
                    var records = envelope.Payload.Budget.Records.ToList();
                    records.Add(records[0]);
                    envelope.Payload.Budget.Records = records;
                }
                break;
            case "missing-reference":
                if (envelope.Payload.Taxonomy is not null)
                {
                    envelope.Payload.Taxonomy.Records = envelope.Payload.Taxonomy.Records
                        .Where(x => x.Table != "categories")
                        .ToList();
                }
                break;
            case "no-admin-profile":
                if (envelope.Payload.Profiles is not null)
                {
                    foreach (var record in envelope.Payload.Profiles.Records)
                    {
                        var fields = record.Fields.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);
                        fields[nameof(User.IsAdmin)] = "false";
                        record.Fields = fields;
                    }
                }
                break;
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        });

        return new MemoryStream(bytes);
    }

    private static async Task SeedOutOfFilterDataAsync(
        DbContextOptions<ApplicationDbContext> options,
        CurrentUserContext currentUser,
        SeedIds seed)
    {
        await using var context = new ApplicationDbContext(options, currentUser);

        var fuelCategory = new Category { Name = "Car", Color = "#993333" };
        var otherAccount = new Account { Name = "Savings", Type = (int)AccountType.Savings };
        var mayPlan = new MonthPlan { Year = 2026, Month = 5 };
        context.Categories.Add(fuelCategory);
        context.Accounts.Add(otherAccount);
        context.MonthPlans.Add(mayPlan);
        context.Expenses.AddRange(
            new Expense
            {
                MonthPlan = mayPlan,
                Name = "May rent",
                CategoryId = seed.CategoryId,
                PlannedAmount = 1000m,
                ActualAmount = 1000m
            },
            new Expense
            {
                MonthPlan = new MonthPlan { Year = 2026, Month = 4 },
                Name = "Fuel",
                Category = fuelCategory,
                PlannedAmount = 300m,
                ActualAmount = 200m
            });
        context.Incomes.Add(new Income
        {
            Year = 2026,
            Month = 4,
            Name = "Side gig",
            Amount = 700m,
            ExpectedDayOfMonth = new DateOnly(2026, 4, 20),
            Account = otherAccount
        });

        await context.SaveChangesAsync();
    }

    private static async Task SeedDeletedRowsAsync(
        DbContextOptions<ApplicationDbContext> options,
        CurrentUserContext currentUser)
    {
        await using var context = new ApplicationDbContext(options, currentUser);

        var category = await context.Categories.FirstAsync();
        var account = await context.Accounts.FirstAsync();
        var plan = await context.MonthPlans.FirstAsync(x => x.Year == 2026 && x.Month == 4);

        context.Expenses.Add(new Expense
        {
            MonthPlanId = plan.Id,
            Name = "Deleted expense",
            CategoryId = category.Id,
            PlannedAmount = 100m,
            ActualAmount = 100m,
            IsDeleted = true,
            DeletedAtUtc = DateTime.UtcNow
        });
        context.Incomes.Add(new Income
        {
            Year = 2026,
            Month = 4,
            Name = "Deleted income",
            Amount = 100m,
            ExpectedDayOfMonth = new DateOnly(2026, 4, 8),
            AccountId = account.Id,
            IsDeleted = true,
            DeletedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    private static async Task SeedOtherBudgetAsync(
        DbContextOptions<ApplicationDbContext> options,
        CurrentUserContext otherUser)
    {
        await using var context = new ApplicationDbContext(options, otherUser);

        var category = new Category { Name = "Other category", Color = "#111111" };
        var account = new Account { Name = "Other account", Type = (int)AccountType.Bank };
        var plan = new MonthPlan { Year = 2026, Month = 4 };
        context.Categories.Add(category);
        context.Accounts.Add(account);
        context.MonthPlans.Add(plan);
        context.Expenses.Add(new Expense
        {
            MonthPlan = plan,
            Name = "Other budget expense",
            Category = category,
            PlannedAmount = 10m,
            ActualAmount = 10m
        });
        context.Incomes.Add(new Income
        {
            Year = 2026,
            Month = 4,
            Name = "Other budget income",
            Amount = 10m,
            ExpectedDayOfMonth = new DateOnly(2026, 4, 1),
            Account = account
        });

        await context.SaveChangesAsync();
    }

    private static CurrentUserContext CreateVisibleUserContext()
    {
        var currentUser = new CurrentUserContext();
        currentUser.SetInteractiveUser(VisibleUserId, User.DefaultUserId);
        return currentUser;
    }

    private static DbContextOptions<ApplicationDbContext> NewOptions(string dbName)
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
    }

    private static string DecodeCsv(byte[] content)
    {
        return Encoding.UTF8.GetString(content).TrimStart('\uFEFF');
    }

    private static BackupEnvelopeDto DecodeJson(byte[] content)
    {
        return JsonSerializer.Deserialize<BackupEnvelopeDto>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        })!;
    }

    private static BackupService CreateService(
        IDbContextFactory<ApplicationDbContext> factory,
        CurrentUserContext currentUserContext,
        IBackupSettingsStore? backupSettingsStore = null)
    {
        return new BackupService(
            factory,
            new StaticDateTimeProvider(new DateTime(2026, 6, 7, 16, 5, 0, DateTimeKind.Utc)),
            currentUserContext,
            backupSettingsStore ?? new InMemoryBackupSettingsStore());
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file '{relativePath}'.", relativePath);
    }

    private static IEnumerable<BackupRecordDto> EnumerateRecords(BackupEnvelopeDto envelope)
    {
        if (envelope.Payload.Taxonomy is not null)
        {
            foreach (var record in envelope.Payload.Taxonomy.Records)
            {
                yield return record;
            }
        }

        if (envelope.Payload.Budget is not null)
        {
            foreach (var record in envelope.Payload.Budget.Records)
            {
                yield return record;
            }
        }

        if (envelope.Payload.Profiles is not null)
        {
            foreach (var record in envelope.Payload.Profiles.Records)
            {
                yield return record;
            }
        }

        if (envelope.Payload.Audit is not null)
        {
            foreach (var record in envelope.Payload.Audit.Records)
            {
                yield return record;
            }
        }

        if (envelope.Payload.Logs is not null)
        {
            foreach (var record in envelope.Payload.Logs.Records)
            {
                yield return record;
            }
        }
    }

    private sealed record SeedIds(int CategoryId, int AccountId);

    private sealed class ScopedInMemoryDbContextFactory(
        DbContextOptions<ApplicationDbContext> options,
        CurrentUserContext currentUserContext) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options, currentUserContext);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }

    private sealed class ScopedDbContextFactory(
        DbContextOptions<ApplicationDbContext> options,
        CurrentUserContext currentUserContext) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options, currentUserContext);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }

    private sealed class InMemoryBackupSettingsStore(BackupSettingsDto? initialSettings = null) : IBackupSettingsStore
    {
        public BackupSettingsDto Current { get; private set; } = initialSettings ?? new BackupSettingsDto
        {
            IsEnabled = false,
            BackupPath = Path.Combine(Path.GetTempPath(), "HouseholdBudgetMateTests", "backups"),
            Frequency = BackupScheduleFrequency.Daily,
            LocalTime = new TimeOnly(2, 0),
            Sections = BackupSection.FullApp
        };

        public Task<BackupSettingsDto> GetAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Current);
        }

        public Task<BackupSettingsDto> SaveAsync(
            SaveBackupSettingsRequest request,
            CancellationToken cancellationToken)
        {
            Current = new BackupSettingsDto
            {
                IsEnabled = request.IsEnabled,
                BackupPath = request.BackupPath,
                Frequency = request.Frequency,
                LocalTime = request.LocalTime,
                Sections = request.Sections,
                LastRunAtUtc = Current.LastRunAtUtc,
                LastStatus = Current.LastStatus
            };

            return Task.FromResult(Current);
        }

        public Task<BackupSettingsDto> RecordRunAsync(
            DateTime utcNow,
            string status,
            CancellationToken cancellationToken)
        {
            Current.LastRunAtUtc = utcNow;
            Current.LastStatus = status;
            return Task.FromResult(Current);
        }
    }
}
