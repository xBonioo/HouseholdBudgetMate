using FluentAssertions;
using HouseholdBudgetMate.Domain;
using HouseholdBudgetMate.Domain.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NetArchTest.Rules;

namespace HouseholdBudgetMate.Tests.Tests.Architecture.DomainTests;

public sealed class EntityTests
{
    [Fact]
    public void DomainEntities_Except_LogEntry_Should_Inherit_ATimestampable()
    {
        var domainAssembly = typeof(DomainAssemblyMarker).Assembly;

        var failingTypes = domainAssembly
            .GetTypes()
            .Where(t =>
                t is { IsClass: true, Namespace: "HouseholdBudgetMate.Domain.Entities" }
                && t.Name != nameof(LogEntry<>)
                && !typeof(ATimestampable).IsAssignableFrom(t))
            .Select(t => t.FullName)
            .ToList();

        failingTypes.Should().BeEmpty(string.Join("\n", failingTypes));
    }

    [Fact]
    public void Domain_Should_Not_Depend_On_Application_Layer()
    {
        var result = Types.InAssembly(typeof(DomainAssemblyMarker).Assembly)
            .Should()
            .NotHaveDependencyOn("HouseholdBudgetMate.Application")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(result.FailingTypes is not null
            ? string.Join("\n", result.FailingTypes.Select(x => x.FullName))
            : "");
    }

    [Fact]
    public void Domain_Should_Not_Depend_On_Web_Project()
    {
        var result = Types.InAssembly(typeof(DomainAssemblyMarker).Assembly)
            .Should()
            .NotHaveDependencyOn("HouseholdBudgetMate.Web")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(result.FailingTypes is not null
            ? string.Join("\n", result.FailingTypes.Select(x => x.FullName))
            : "");
    }
}