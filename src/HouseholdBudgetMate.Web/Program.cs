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
using HouseholdBudgetMate.Migrations;
using HouseholdBudgetMate.Web.Components;
using Microsoft.AspNetCore.Localization;
using MudBlazor.Services;
using QuestPDF;
using QuestPDF.Infrastructure;

Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
    throw new InvalidOperationException("DefaultConnection string is not configured.");

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
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
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();


builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

builder.Services.AddServerSideBlazor()
    .AddCircuitOptions(options => { options.DetailedErrors = true; });

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

builder.Services.AddScoped<CoreDataSeedService>();

builder.AddSerilogLogging();

var applicationConfig = builder.Configuration.GetSection("Application").Get<ApplicationConfiguration>()
                        ?? throw new InvalidOperationException("Application configuration is missing");
builder.Services.AddSingleton(applicationConfig);

try
{
    var app = builder.Build();

    app.UseSerilogRequestLoggingWithThreshold();

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    
    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", true);
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    //app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseHttpsRedirection();

    if (applicationConfig.MigrateDatabaseOnStart)
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

    using (var seedScope = app.Services.CreateScope())
    {
        if (applicationConfig.SeedDataToDatabase)
        {
            var coreDataSeedService = seedScope.ServiceProvider.GetRequiredService<CoreDataSeedService>();
            await coreDataSeedService.SeedOnStartupAsync(applicationConfig.SeedDataToDatabase, CancellationToken.None);
        }
    }

    // app.UseAuthentication();
    // app.UseAuthorization();

    app.UseAntiforgery();

    app.UseRequestLocalization("pl-PL");

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();
    app.MapControllers();

    //Create files folder
    var filesPath = Path.Combine(builder.Environment.ContentRootPath, "..", "..", Constants.FolderNameFiles);
    filesPath = Path.GetFullPath(filesPath);
    if (!Directory.Exists(filesPath)) Directory.CreateDirectory(filesPath);

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(filesPath),
        RequestPath = Constants.RequestPathFiles
    });

    app.Run();
}
catch (Exception ex)
{
    throw new ApplicationException($"Wystąpił błąd podczas ładowania aplikacji. {ex}");
}