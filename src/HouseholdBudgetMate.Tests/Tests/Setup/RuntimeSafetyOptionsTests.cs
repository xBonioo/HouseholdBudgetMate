using FluentAssertions;
using HouseholdBudgetMate.Web.Setup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace HouseholdBudgetMate.Tests.Tests.Setup;

public sealed class RuntimeSafetyOptionsTests
{
    [Fact]
    public void ShouldEnableDetailedErrors_Should_Default_To_False_In_Production()
    {
        var configuration = BuildConfiguration();
        var environment = new TestHostEnvironment(Environments.Production);

        var enabled = RuntimeSafetyOptions.ShouldEnableDetailedErrors(environment, configuration);

        enabled.Should().BeFalse();
    }

    [Fact]
    public void ShouldEnableDetailedErrors_Should_Enable_For_Development()
    {
        var configuration = BuildConfiguration();
        var environment = new TestHostEnvironment(Environments.Development);

        var enabled = RuntimeSafetyOptions.ShouldEnableDetailedErrors(environment, configuration);

        enabled.Should().BeTrue();
    }

    [Fact]
    public void ShouldEnableDetailedErrors_Should_Allow_Explicit_Config_Override()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [RuntimeSafetyOptions.DetailedErrorsConfigurationKey] = "true"
        });
        var environment = new TestHostEnvironment(Environments.Production);

        var enabled = RuntimeSafetyOptions.ShouldEnableDetailedErrors(environment, configuration);

        enabled.Should().BeTrue();
    }

    [Fact]
    public void ShouldEnablePublicFileServing_Should_Default_To_Disabled()
    {
        var configuration = BuildConfiguration();

        var enabled = RuntimeSafetyOptions.ShouldEnablePublicFileServing(configuration);

        enabled.Should().BeFalse();
    }

    [Fact]
    public void ShouldEnablePublicFileServing_Should_Require_Explicit_Config()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [RuntimeSafetyOptions.PublicFileServingConfigurationKey] = "true"
        });

        var enabled = RuntimeSafetyOptions.ShouldEnablePublicFileServing(configuration);

        enabled.Should().BeTrue();
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?>? values = null)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
            .Build();
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "HouseholdBudgetMate.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
