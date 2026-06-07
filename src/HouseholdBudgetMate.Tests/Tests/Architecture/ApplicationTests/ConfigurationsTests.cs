using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using HouseholdBudgetMate.Application;
using HouseholdBudgetMate.Tests.Rules;
using NetArchTest.Rules;

namespace HouseholdBudgetMate.Tests.Tests.Architecture.ApplicationTests;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
public sealed class ConfigurationsTests
{
    [Fact]
    public void AreSealedAndPublic()
    {
        var rootNamespace = typeof(ApplicationAssemblyMarker);

        var result = Types.InAssembly(typeof(ApplicationAssemblyMarker).Assembly)
            .That()
            .AreClasses()
                .And().ResideInNamespace($"{rootNamespace}.Configurations")
            .Should()
                .BeSealed()
                .And().BePublic()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(result.FailingTypes is not null
            ? string.Join("\n", result.FailingTypes.Select(x => x.FullName))
            : ""
        );
    }

    [Fact]
    public void AreNotInherit()
    {
        var rootNamespace = typeof(ApplicationAssemblyMarker);

        var result = Types.InAssembly(typeof(ApplicationAssemblyMarker).Assembly)
            .That()
            .AreClasses()
            .And().ResideInNamespace($"{rootNamespace}.Configurations")
            .Should()
            .MeetCustomRule(new HasNoInheritance())
            .GetResult();

        result.IsSuccessful.Should().BeTrue(result.FailingTypes is not null
            ? string.Join("\n", result.FailingTypes.Select(x => x.FullName))
            : ""
        );
    }
}
