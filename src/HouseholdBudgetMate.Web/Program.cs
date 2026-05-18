using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using HouseholdBudgetMate.Application.Kernel.Configurations;
using HouseholdBudgetMate.Application.Kernel.Extensions;
using HouseholdBudgetMate.Application.Kernel.Timing;
using HouseholdBudgetMate.Application.Shared;
using HouseholdBudgetMate.Web;
using HouseholdBudgetMate.Web.Middleware;
using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Application.Helpers;
using HouseholdBudgetMate.Application.Services;
using HouseholdBudgetMate.Domain.Infrastructure;
using HouseholdBudgetMate.Migrations;
using HouseholdBudgetMate.Web.Services;
using HouseholdBudgetMate.Web.Setup;
using HouseholdBudgetMate.Web.Components;
using Microsoft.AspNetCore.Localization;
using MudBlazor.Services;

var executableDirectory = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
var appDataDirectory = WritableAppDataPathResolver.Resolve("HouseholdBudgetMate");

var processName = Path.GetFileNameWithoutExtension(Environment.ProcessPath);

var isBuildOutputDirectory = executableDirectory.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                             || executableDirectory.Contains($"{Path.AltDirectorySeparatorChar}bin{Path.AltDirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
var isPublishedExecutable = string.Equals(processName, "HouseholdBudgetMate.Web", StringComparison.OrdinalIgnoreCase)
                            && !isBuildOutputDirectory;

var hasExplicitUrls = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS"))
                      || args.Any(arg => arg.StartsWith("--urls", StringComparison.OrdinalIgnoreCase));

var legacyConfigPath = Path.Combine(executableDirectory, "config.json");
var appDataConfigPath = Path.Combine(appDataDirectory, "config.json");

if (!File.Exists(appDataConfigPath) && File.Exists(legacyConfigPath) && isPublishedExecutable)
{
    // Migrate config from legacy portable location to writable user profile folder.
    File.Copy(legacyConfigPath, appDataConfigPath, overwrite: false);
}

var builderOptions = isPublishedExecutable
    ? new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = executableDirectory
    }
    : new WebApplicationOptions
    {
        Args = args
    };

var builder = WebApplication.CreateBuilder(builderOptions);
StartupHostingOptions? startupHostingOptions = null;

if (isPublishedExecutable && !hasExplicitUrls)
{
    startupHostingOptions = StartupHostingOptions.Create(builder.Configuration, appDataDirectory);
    builder.WebHost.ConfigureKestrel(startupHostingOptions.ConfigureKestrel);
}

var runtimeConfigurationState = new RuntimeConfigurationState(appDataDirectory);
builder.Services.AddSingleton(runtimeConfigurationState);

var runtimeConnectionString = runtimeConfigurationState.GetDatabaseConfiguration()?.ToConnectionString();
var fallbackConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var connectionString = isPublishedExecutable
    ? runtimeConnectionString
    : fallbackConnectionString;

if (string.IsNullOrWhiteSpace(connectionString))
{
    // Placeholder enables DI graph startup for /setup when no config exists.
    connectionString = "Host=localhost;Port=5432;Database=placeholder;Username=placeholder;Password=placeholder";
}

builder.Services.AddDbContextFactory<ApplicationDbContext>(
    (serviceProvider, options) =>
    {
        options.ConfigureWarnings(w => w.Ignore(RelationalEventId.MultipleCollectionIncludeWarning));
        options.UseNpgsql(
            connectionString,
            npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly("HouseholdBudgetMate.Migrations");
                npgsqlOptions.EnableRetryOnFailure(
                    5,
                    TimeSpan.FromSeconds(30),
                    null);
                npgsqlOptions.CommandTimeout(60);
            });
        options.UseApplicationServiceProvider(serviceProvider);
    },
    ServiceLifetime.Scoped);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure Blazor circuit options (DetailedErrors, disconnect timeout, etc.).
builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(options =>
{
    options.DetailedErrors = true;
});

builder.Services.AddMemoryCache();
builder.Services.AddScoped<ArchiveMonthsCacheService>();
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
builder.Services.AddScoped<CurrentUserContext>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("pl-PL")
    };
    options.DefaultRequestCulture = new RequestCulture("pl-PL", "pl-PL");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

builder.Services.AddLocalization();

builder.Services.AddMudServices();

builder.Services.AddControllers();

builder.Services.AddSingleton<IStoragePathProvider, WebStoragePathProvider>();
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IIncomeService, IncomeService>();
builder.Services.AddScoped<ILoanService, LoanService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAdminConfigurationService, AdminConfigurationService>();
builder.Services.AddScoped<IUserSessionService, UserSessionService>();

builder.Services.AddScoped<IAppEventPublisher, LoggingAppEventPublisher>();
builder.Services.AddScoped<CoreDataSeedService>();

builder.Services.AddScoped<ISetupConfigurationService, SetupConfigurationService>();
builder.Services.AddScoped<IDatabaseMigrationOrchestrator, DatabaseMigrationOrchestrator>();

builder.AddSerilogLogging();

var applicationConfig = builder.Configuration.GetSection("Application").Get<ApplicationConfiguration>()
                        ?? throw new InvalidOperationException("Application configuration is missing");
builder.Services.AddSingleton(applicationConfig);
// builder.WebHost.UseUrls("https://0.0.0.0:5001");
// builder.WebHost.UseUrls("http://0.0.0.0:5000");

try
{
    var app = builder.Build();

    app.UseSerilogRequestLoggingWithThreshold();

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    
    // Setup redirect is only needed in published (installed) mode.
    // In development the connection is configured via appsettings.Development.json.
    if (isPublishedExecutable)
    {
        app.UseMiddleware<SetupRedirectMiddleware>();
    }
    
    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", true);
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    //app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseHttpsRedirection();

    if (startupHostingOptions is not null)
    {
        var startupMessage = $"Aplikacja uruchamia sie pod adresami: http://localhost:{startupHostingOptions.HttpPort} oraz {startupHostingOptions.HttpsUrl}";
        Console.WriteLine(startupMessage);
        app.Logger.LogInformation(startupMessage);
    }

    if (app.Environment.IsDevelopment() && applicationConfig.MigrateDatabaseOnStart)
    {
        using var scope = app.Services.CreateScope();

        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<ApplicationDbContext>>();

        await using var dbContext = dbContextFactory.CreateDbContext();

        try
        {
            dbContext.Database.Migrate();
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogCritical(ex, "Database migration failed on startup");
            throw;
        }
    }
    
    if (isPublishedExecutable && runtimeConfigurationState.IsConfigured && applicationConfig.MigrateDatabaseOnStart)
    {
        using var scope = app.Services.CreateScope();
        
        var migrationOrchestrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrationOrchestrator>();

        try
        {
            await migrationOrchestrator.MigrateConfiguredDatabaseAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogCritical(ex, "Database migration failed on startup");
            throw;
        }
    }

    using (var seedScope = app.Services.CreateScope())
    {
        // In published mode: seed only when the runtime config.json is present.
        // In dev mode: always attempt seeding (EnsureCurrentMonthPlanAsync has CanConnect protection).
        var shouldSeed = isPublishedExecutable
            ? runtimeConfigurationState.IsConfigured && applicationConfig.SeedDataToDatabase
            : applicationConfig.SeedDataToDatabase;

        if (shouldSeed)
        {
            var coreDataSeedService = seedScope.ServiceProvider.GetRequiredService<CoreDataSeedService>();
            await coreDataSeedService.SeedOnStartupAsync(applicationConfig.SeedDataToDatabase, CancellationToken.None);
        }
    }

    // app.UseAuthentication();
    // app.UseAuthorization();

    app.UseAntiforgery();
    app.UseRequestLocalization("pl-PL");

    // In published (single-file) mode UseStaticFiles is required because the SWA manifest
    // is not available as a separate file at runtime.
    // In development MapStaticAssets is required so that @Assets and <ImportMap/> correctly
    // resolve fingerprinted paths, enabling blazor.web.js to load its ES-module dependencies
    // and establish the interactive server circuit (which in turn makes MudHidden work).
    app.UseStaticFiles();
    if (!isPublishedExecutable)
    {
        app.MapStaticAssets();
    }

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();
    app.MapControllers();

    // Create files folder in writable user profile location.
    var filesPath = Path.Combine(appDataDirectory, Constants.FolderNameFiles);
    if (!Directory.Exists(filesPath)) Directory.CreateDirectory(filesPath);

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(filesPath),
        RequestPath = Constants.RequestPathFiles
    });

    if (startupHostingOptions is not null)
    {
        app.Lifetime.ApplicationStarted.Register(() => startupHostingOptions.OpenBrowserIfEnabled(app.Logger));
    }

    app.Run();
}
catch (Exception ex)
{
    throw new ApplicationException($"Wystąpił błąd podczas ładowania aplikacji. {ex}");
}
