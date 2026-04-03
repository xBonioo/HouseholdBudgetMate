using HouseholdBudgetMate.Abstractions;
using NetArchTest.Rules;

namespace HouseholdBudgetMate.Tests.Tests.Architecture.AbstractionsTests;

public sealed class GeneralTests
{
    [Fact]
    public void InterfacesAndClassesArePublic()
    {
        var result = Types.InAssembly(typeof(IServiceResult).Assembly)
            .That()
            .AreInterfaces().Or().AreClasses()
            .Should()
            .BePublic()
            .GetResult();

        Assert.True(result.IsSuccessful, result.FailingTypes is not null
            ? string.Join("\n", result.FailingTypes.Select(x => x.FullName))
            : ""
        );
    }

    [Fact]
    public void DoesNotHaveDependencies()
    {
        var result = Types.InAssembly(typeof(IServiceResult).Assembly)
            .Should()
            .OnlyHaveDependenciesOn(typeof(IServiceResult).Namespace, "System")
            .GetResult();
        
        Assert.True(result.IsSuccessful, result.FailingTypes is not null
            ? string.Join("\n", result.FailingTypes.Select(x => x.FullName))
            : ""
        );
    }
}