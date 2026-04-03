using FluentAssertions;
using HouseholdBudgetMate.Application;
using NetArchTest.Rules;

namespace HouseholdBudgetMate.Tests.Tests.Architecture.ApplicationTests;

public sealed class DependencyDirectionTests
{
    [Fact]
    public void Application_Should_Not_Depend_On_Web_Project()
    {
        var result = Types.InAssembly(typeof(ApplicationAssemblyMarker).Assembly)
            .Should()
            .NotHaveDependencyOn("HouseholdBudgetMate.Web")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(result.FailingTypes is not null
            ? string.Join("\n", result.FailingTypes.Select(x => x.FullName))
            : "");
    }

    [Fact]
    public void ApplicationServices_Should_Not_Depend_On_Presentation_Layers()
    {
        var result = Types.InAssembly(typeof(ApplicationAssemblyMarker).Assembly)
            .That()
            .ResideInNamespace("HouseholdBudgetMate.Application.Services")
            .Or()
            .ResideInNamespace("HouseholdBudgetMate.Application.Services.AdminModule")
            .Should()
            .NotHaveDependencyOn("HouseholdBudgetMate.Web")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(result.FailingTypes is not null
            ? string.Join("\n", result.FailingTypes.Select(x => x.FullName))
            : "");
    }
}