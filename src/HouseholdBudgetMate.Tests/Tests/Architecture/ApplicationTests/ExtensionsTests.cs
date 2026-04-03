using HouseholdBudgetMate.Application;
using NetArchTest.Rules;

namespace HouseholdBudgetMate.Tests.Tests.Architecture.ApplicationTests;

public sealed class ExtensionsTests
{
    [Fact]
    public void AreStaticAndAreProperlyNamed()
    {
        var rootNamespace = typeof(ApplicationAssemblyMarker);

        var result = Types.InAssembly(typeof(ApplicationAssemblyMarker).Assembly)
            .That()
            .AreClasses()
            .And().ResideInNamespace($"{rootNamespace}.Extensions")
            .Should()
            .BeStatic()
            .And().HaveNameEndingWith("Extensions")
            .GetResult();

        Assert.True(result.IsSuccessful, result.FailingTypes is not null
            ? string.Join("\n", result.FailingTypes.Select(x => x.FullName))
            : ""
        );
    }
}