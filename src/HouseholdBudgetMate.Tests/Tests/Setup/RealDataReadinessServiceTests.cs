using FluentAssertions;
using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Application.Kernel.Configurations;
using HouseholdBudgetMate.Tests.Shared;
using HouseholdBudgetMate.Web.Setup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HouseholdBudgetMate.Tests.Tests.Setup;

public sealed class RealDataReadinessServiceTests
{
    [Fact]
    public async Task GetReportAsync_Should_Report_App_Checks_Ready_When_Runtime_Gates_Are_Satisfied()
    {
        var service = CreateService(hasSecureAdmin: true);

        var report = await service.GetReportAsync(CancellationToken.None);

        report.IsAppCheckReady.Should().BeTrue();
        report.EvidencePath.Should().Contain("readiness-evidence.md");
        report.ManualItems.Select(x => x.Name).Should().Contain([
            "Manual pg_dump",
            "Restore smoke test",
            "Migration review"
        ]);
    }

    [Fact]
    public async Task GetReportAsync_Should_Report_Public_Files_As_Not_Ready_When_Enabled()
    {
        var service = CreateService(
            hasSecureAdmin: true,
            configurationValues: new Dictionary<string, string?>
            {
                [RuntimeSafetyOptions.PublicFileServingConfigurationKey] = "true"
            });

        var report = await service.GetReportAsync(CancellationToken.None);

        report.IsAppCheckReady.Should().BeFalse();
        report.AutomatedChecks.Single(x => x.Name == "Public /files").IsReady.Should().BeFalse();
    }

    [Fact]
    public async Task GetReportAsync_Should_Report_Access_Gate_Dependency_When_Admin_Is_Missing()
    {
        var service = CreateService(hasSecureAdmin: false);

        var report = await service.GetReportAsync(CancellationToken.None);

        report.IsAppCheckReady.Should().BeFalse();
        report.AutomatedChecks.Single(x => x.Name == "Secure interactive administrator").IsReady.Should().BeFalse();
    }

    [Fact]
    public async Task GetReportAsync_Should_Report_Log_Retention_As_Not_Ready_When_Disabled()
    {
        var service = CreateService(
            hasSecureAdmin: true,
            applicationConfiguration: new ApplicationConfiguration
            {
                LogCleanupTask = false,
                LogRetentionDays = 30
            });

        var report = await service.GetReportAsync(CancellationToken.None);

        report.IsAppCheckReady.Should().BeFalse();
        report.AutomatedChecks.Single(x => x.Name == "Operational log retention").IsReady.Should().BeFalse();
    }

    private static RealDataReadinessService CreateService(
        bool hasSecureAdmin,
        Dictionary<string, string?>? configurationValues = null,
        ApplicationConfiguration? applicationConfiguration = null)
    {
        var userService = new Mock<IUserService>();
        userService
            .Setup(x => x.HasSecureInteractiveAdministratorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasSecureAdmin);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues ?? new Dictionary<string, string?>())
            .Build();

        return new RealDataReadinessService(
            TestDbContextFactory.CreateFactory(),
            userService.Object,
            applicationConfiguration ?? new ApplicationConfiguration
            {
                LogCleanupTask = true,
                LogRetentionDays = 30
            },
            configuration,
            new TestHostEnvironment(Environments.Production),
            NullLoggerFactory.Instance);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "HouseholdBudgetMate.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
