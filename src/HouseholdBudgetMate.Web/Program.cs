using System.Globalization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using HouseholdBudgetMate.Application.Kernel.Configurations;
using HouseholdBudgetMate.Application.Kernel.Extensions;
using HouseholdBudgetMate.Application.Kernel.Timing;
using HouseholdBudgetMate.Application.Auditing;
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

// Hook-check comment.
var executableDirectory = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
var applicationBaseDirectory = AppContext.BaseDirectory;
var appDataDirectory = WritableAppDataPathResolver.Resolve("HouseholdBudgetMate");

var processName = Path.GetFileNameWithoutExtension(Environment.ProcessPath);

var isBuildOutputDirectory = executableDirectory.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                             || executableDirectory.Contains($"{Path.AltDirectorySeparatorChar}bin{Path.AltDirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
var isPublishedExecutable = string.Equals(processName, "HouseholdBudgetMate.Web", StringComparison.OrdinalIgnoreCase)
                            && !isBuildOutputDirectory;

var hasExplicitUrls = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS"))
                      || args.Any(arg => arg.StartsWith("--urls", StringComparison.OrdinalIgnoreCase));
var isContainerOrCloud = IsEnabled(Environment.GetEnvironmentVariable("HOUSEHOLDBUDGETMATE_CONTAINER"))
                         || IsEnabled(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"))
                         || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RENDER"))
                         || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PORT"));
var containerListenUrl = ResolveContainerListenUrl(hasExplicitUrls);

var legacyConfigPath = Path.Combine(executableDirectory, "config.json");
var appDataConfigPath = Path.Combine(appDataDirectory, "config.json");

if (!File.Exists(appDataConfigPath) && File.Exists(legacyConfigPath) && isPublishedExecutable)
{
    // Migrate config from legacy portable location to writable user profile folder.
    File.Copy(legacyConfigPath, appDataConfigPath, overwrite: false);
}

var isPublishedRuntime = isPublishedExecutable || isContainerOrCloud;
var contentRootPath = isPublishedExecutable
    ? executableDirectory
    : isContainerOrCloud
        ? applicationBaseDirectory
        : null;

var builderOptions = isPublishedRuntime
    ? new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = contentRootPath
    }
    : new WebApplicationOptions
    {
        Args = args
    };

var builder = WebApplication.CreateBuilder(builderOptions);
StartupHostingOptions? startupHostingOptions = null;

if (!string.IsNullOrWhiteSpace(containerListenUrl))
{
    builder.WebHost.UseUrls(containerListenUrl);
}
else if (isPublishedExecutable && !hasExplicitUrls)
{
    startupHostingOptions = StartupHostingOptions.Create(builder.Configuration, appDataDirectory);
    builder.WebHost.ConfigureKestrel(startupHostingOptions.ConfigureKestrel);
}

var runtimeConfigurationState = new RuntimeConfigurationState(appDataDirectory);
builder.Services.AddSingleton(runtimeConfigurationState);

var environmentConnectionString = PostgreSqlConnectionStringResolver.Resolve(builder.Configuration);
var usesRuntimeSetup = isPublishedExecutable
                       || isContainerOrCloud && string.IsNullOrWhiteSpace(environmentConnectionString);

builder.Services.AddDbContextFactory<ApplicationDbContext>(
    (serviceProvider, options) =>
    {
        var runtimeConfig = serviceProvider.GetRequiredService<RuntimeConfigurationState>();
        var dbConnectionString = !string.IsNullOrWhiteSpace(environmentConnectionString)
            ? environmentConnectionString
            : runtimeConfig.GetDatabaseConfiguration()?.ToConnectionString();

        if (string.IsNullOrWhiteSpace(dbConnectionString))
        {
            // Placeholder enables DI graph startup for /setup when no config exists.
            dbConnectionString = "Host=localhost;Port=5432;Database=placeholder;Username=placeholder;Password=placeholder";
        }

        options.ConfigureWarnings(w => w.Ignore(RelationalEventId.MultipleCollectionIncludeWarning));
        options.AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>());
        options.UseNpgsql(
            dbConnectionString,
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
    options.DetailedErrors = RuntimeSafetyOptions.ShouldEnableDetailedErrors(
        builder.Environment,
        builder.Configuration);
});

builder.Services.AddMemoryCache();
builder.Services.AddScoped<ArchiveMonthsCacheService>();
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
builder.Services.AddScoped<CurrentUserContext>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
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
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddSingleton<IStoragePathProvider, WebStoragePathProvider>();
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IIncomeService, IncomeService>();
builder.Services.AddScoped<ILoanService, LoanService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IAdminConfigurationService, AdminConfigurationService>();
builder.Services.AddScoped<IUserSessionService, UserSessionService>();
builder.Services.AddScoped<UnsavedChangesTracker>();

builder.Services.AddScoped<IAppEventPublisher, LoggingAppEventPublisher>();
builder.Services.AddScoped<CoreDataSeedService>();

builder.Services.AddScoped<ISetupConfigurationService, SetupConfigurationService>();
builder.Services.AddScoped<IDatabaseMigrationOrchestrator, DatabaseMigrationOrchestrator>();
builder.Services.AddScoped<IAccessHardeningService, AccessHardeningService>();
builder.Services.AddScoped<IAccessRecoveryService, AccessRecoveryService>();
builder.Services.AddScoped<IRealDataReadinessService, RealDataReadinessService>();
builder.Services.AddSingleton<ILocalAccessGrantService, LocalAccessGrantService>();

builder.AddSerilogLogging();

var applicationConfig = builder.Configuration.GetSection("Application").Get<ApplicationConfiguration>()
                        ?? throw new InvalidOperationException("Application configuration is missing");
builder.Services.AddSingleton(applicationConfig);
builder.Services.AddOperationalLogCleanup();
// builder.WebHost.UseUrls("https://0.0.0.0:5001");
// builder.WebHost.UseUrls("http://0.0.0.0:5000");

static string? ResolveContainerListenUrl(bool hasExplicitUrls)
{
    if (hasExplicitUrls)
    {
        return null;
    }

    var portValue = Environment.GetEnvironmentVariable("PORT");
    if (!int.TryParse(portValue, CultureInfo.InvariantCulture, out var port) || port <= 0)
    {
        return null;
    }

    return $"http://0.0.0.0:{port}";
}

static bool IsEnabled(string? value)
{
    return bool.TryParse(value, out var enabled) && enabled;
}

try
{
    var app = builder.Build();

    app.Use((context, next) =>
    {
        LocalAccessGrantService.CaptureDirectRemoteAddress(context);
        return next(context);
    });

    if (isContainerOrCloud)
    {
        app.UseForwardedHeaders();
    }

    app.UseSerilogRequestLoggingWithThreshold();

    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // Setup redirect is needed for installed/local-container mode when no DB env var is configured.
    // Render provides DATABASE_URL, so it skips setup and migrates automatically.
    if (usesRuntimeSetup)
    {
        app.UseMiddleware<SetupRedirectMiddleware>();
    }

    app.UseMiddleware<AccessHardeningRedirectMiddleware>();

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", true);
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    //app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    if (!isContainerOrCloud)
    {
        app.UseHttpsRedirection();
    }

    if (startupHostingOptions is not null)
    {
        var startupMessage = $"Aplikacja uruchamia sie pod adresami: {string.Join(", ", startupHostingOptions.GetStartupUrls())}";
        Console.WriteLine(startupMessage);
        app.Logger.LogInformation(startupMessage);

        if (!startupHostingOptions.IsHttpsCertificateTrusted)
        {
            const string sslWarning = "HTTPS dla localhost jest aktywne, ale certyfikat nie zostal dodany do zaufanych certyfikatow. Przegladarka moze pokazywac ostrzezenie SSL do czasu recznego zaufania certyfikatu.";
            Console.WriteLine(sslWarning);
            app.Logger.LogWarning(sslWarning);

            if (!string.IsNullOrWhiteSpace(startupHostingOptions.HttpsCertificateTrustWarning))
            {
                Console.WriteLine(startupHostingOptions.HttpsCertificateTrustWarning);
                app.Logger.LogWarning(startupHostingOptions.HttpsCertificateTrustWarning);
            }
        }
    }

    var hasEnvironmentConnectionString = !string.IsNullOrWhiteSpace(environmentConnectionString);
    var shouldMigrateDatabase = applicationConfig.MigrateDatabaseOnStart
                                && (app.Environment.IsDevelopment()
                                    || hasEnvironmentConnectionString
                                    || isPublishedExecutable && runtimeConfigurationState.IsConfigured);

    if (shouldMigrateDatabase)
    {
        using var scope = app.Services.CreateScope();

        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<ApplicationDbContext>>();

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            await dbContext.Database.MigrateAsync();
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
        // Env-configured containers do not use /setup, so ensure the root user and current month exist.
        var shouldSeed = applicationConfig.SeedDataToDatabase
                         || hasEnvironmentConnectionString
                         || isPublishedExecutable && runtimeConfigurationState.IsConfigured;

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
    app.MapReadinessEndpoint();

    // Create files folder in writable user profile location.
    var filesPath = Path.Combine(appDataDirectory, Constants.FolderNameFiles);
    if (!Directory.Exists(filesPath)) Directory.CreateDirectory(filesPath);

    if (RuntimeSafetyOptions.ShouldEnablePublicFileServing(app.Configuration))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(filesPath),
            RequestPath = Constants.RequestPathFiles
        });
    }

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
